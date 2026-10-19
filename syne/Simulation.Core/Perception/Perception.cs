// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Observation : identité, type, position, confiance, tick, attributs (représentation canonique de type
//        sérialisable).

using System.Globalization;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Performance;
using Simulation.Core.Prng;
using Simulation.Core.World;

namespace Simulation.Core.Perception;

/// <summary>
/// Observation : identité, type, position, confiance, tick,
/// attributs (représentation canonique de type sérialisable).
/// </summary>
public sealed record Observation
{
    public Observation(
        ulong entityId,
        string entityType,
        Position position,
        double confidence,
        ulong tick,
        IReadOnlyDictionary<string, string> attributes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        if (confidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "La confiance doit être dans [0, 1].");
        }

        EntityId = entityId;
        EntityType = entityType;
        Position = position;
        Confidence = confidence;
        Tick = tick;
        Attributes = attributes;
    }

    public ulong EntityId { get; }

    public string EntityType { get; }

    public Position Position { get; }

    public double Confidence { get; }

    public ulong Tick { get; }

    public IReadOnlyDictionary<string, string> Attributes { get; }
}

/// <summary>
/// Perception partielle (SYNE-011) : chaque entité ne perçoit que dans son rayon
/// (défaut 50), la ligne de vue peut être masquée par un obstacle
/// la confiance décroît avec la distance
/// <c>1 − (distance/radius) × 0.3</c> clampée [0.7, 1.0], et
/// la perception est étagée en groupes de rotation
/// — groupe = <c>id % rotationInterval</c>, perçoit au tick <c>t ≡ groupe</c>).
/// </summary>
public sealed class PerceptionSystem
{
    private readonly Simulation.Core.World.World _world;
    private readonly PerceptionSettings _settings;

    /// <summary>
    /// Pool du buffer de tri des candidats (SYNE-092) : la
    /// liste temporaire (candidat, distance) est ré-empruntée entre les
    /// perceptions — aucune influence sur l'ordre (tri stable, contenu vide
    /// avant ré-emprunt), déterminisme conservé.
    /// </summary>
    private readonly ObjectPool<(Entity Candidate, double Distance)> _sortBufferPool = new();

    /// <summary>
    /// Cache d'attributs par entité et par tick : les attributs d'une entité
    /// (« espèce », « x », « y ») ne dépendent pas de l'observateur, et la position
    /// d'une entité ne change qu'au commit de fin de tick (fenêtre causale fermée) —
    /// la même table vaut donc pour tous les observateurs du tick. Sans ce cache,
    /// chaque observation allouait une table à trois entrées : à forte densité (un
    /// agent peut percevoir des dizaines de voisins), c'était des milliers
    /// d'allocations par tick pour des valeurs identiques.
    /// </summary>
    private readonly Dictionary<ulong, IReadOnlyDictionary<string, string>> _attributeCache = new();
    private ulong _attributeCacheTick = ulong.MaxValue;

    public PerceptionSystem(Simulation.Core.World.World world, PerceptionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Le rayon de perception doit être &gt; 0.");
        }

        _world = world;
        _settings = settings;
    }

    public PerceptionSettings Settings => _settings;

    /// <summary>Pool du buffer de tri (observabilité SYNE-092 — compteurs Rent/Return).</summary>
    public ObjectPool<(Entity Candidate, double Distance)> SortBuffers => _sortBufferPool;

    public int PerceptionGroupIndex(Entity subject) => (int)(subject.Id.Value % (ulong)_settings.RotationInterval);

    /// <summary>
    /// Vrai si l'entité perçoit au tick <paramref name="tick"/> (perception étagée).
    /// </summary>
    public bool ShouldPerceive(Entity subject, ulong tick) =>
        tick % (ulong)_settings.RotationInterval == (ulong)PerceptionGroupIndex(subject);

    /// <summary>
    /// Perçoit les entités et obstacles dans le rayon (ordre déterministe par
    /// distance puis identifiant). Renvoie l'ensemble vide si la rotation ne
    /// concerne pas ce tick.
    /// </summary>
    public IReadOnlyList<Observation> Perceive(Entity subject, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (!ShouldPerceive(subject, tick))
        {
            return [];
        }

        var result = new List<Observation>(_sortBufferPool.Count > 0 ? 16 : 8);
        IReadOnlyList<Entity> candidates = _world.Grid.QueryCircle(
            subject.Position,
            _settings.Radius,
            excludeId: subject.Id.Value);

        List<(Entity Candidate, double Distance)> ordered = _sortBufferPool.Rent();
        try
        {
            foreach (Entity candidate in candidates)
            {
                ordered.Add((candidate, subject.Position.DistanceTo(candidate.Position)));
            }

            ordered.Sort(static (a, b) =>
            {
                int byDistance = a.Distance.CompareTo(b.Distance);
                return byDistance != 0 ? byDistance : a.Candidate.Id.Value.CompareTo(b.Candidate.Id.Value);
            });

            foreach ((Entity candidate, double distance) in ordered)
            {
                if (_settings.LineOfSight &&
                    !LineOfSight.IsClear(subject.Position, candidate.Position, _world.Obstacles))
                {
                    continue;
                }

                result.Add(ObservationOf(candidate, distance, tick));
            }
        }
        finally
        {
            _sortBufferPool.Return(ordered);
        }

        foreach (Obstacle obstacle in _world.Obstacles)
        {
            double distance = subject.Position.DistanceTo(obstacle.Position);
            if (distance > _settings.Radius)
            {
                continue;
            }

            var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["radius"] = obstacle.Radius.ToString("0.00", CultureInfo.InvariantCulture),
            };
            result.Add(new Observation(
                DeterministicId(obstacle.Id),
                "obstacle",
                obstacle.Position,
                ConfidenceAt(distance),
                tick,
                attributes));
        }

        // Gisements localisés (Phase 0) : repères statiques du paysage, perçus
        // dans une portée dédiée (<c>agents.perception.resourceRadius</c>) — la
        // perception reste partielle (rayon fini, masquage par obstacle), mais un
        // gisement doit pouvoir être <b>découvert</b> pour que la chaîne
        // perception → mémoire → croyance → but ait un objet. L'agent perçoit
        // l'état du gisement (position et quantité courante), pas seulement son
        // existence : une source épuisée est un fait observable.
        foreach (ResourceNode node in _world.ResourceNodes)
        {
            double distance = subject.Position.DistanceTo(node.Position);
            if (distance > _settings.ResourceRadius)
            {
                continue;
            }

            if (_settings.LineOfSight &&
                !LineOfSight.IsClear(subject.Position, node.Position, _world.Obstacles))
            {
                continue;
            }

            var nodeAttributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = node.Id,
                ["kind"] = ResourceNodePlacement.KindName(node.Kind),
                ["amount"] = node.Amount.ToString("0.###", CultureInfo.InvariantCulture),
                ["x"] = node.Position.X.ToString("0.00", CultureInfo.InvariantCulture),
                ["y"] = node.Position.Y.ToString("0.00", CultureInfo.InvariantCulture),
            };
            result.Add(new Observation(
                node.NumericId,
                "node",
                node.Position,
                ConfidenceAt(distance, _settings.ResourceRadius),
                tick,
                nodeAttributes));
        }

        return result;
    }

    private Observation ObservationOf(Entity candidate, double distance, ulong tick)
    {
        if (_attributeCacheTick != tick)
        {
            _attributeCacheTick = tick;
            _attributeCache.Clear();
        }

        if (!_attributeCache.TryGetValue(candidate.Id.Value, out IReadOnlyDictionary<string, string>? attributes))
        {
            attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["species"] = candidate.Species,
                ["x"] = candidate.Position.X.ToString("0.00", CultureInfo.InvariantCulture),
                ["y"] = candidate.Position.Y.ToString("0.00", CultureInfo.InvariantCulture),
            };
            _attributeCache[candidate.Id.Value] = attributes;
        }

        return new Observation(
            candidate.Id.Value,
            "entity",
            candidate.Position,
            ConfidenceAt(distance),
            tick,
            attributes);
    }

    /// <summary>Confiance = 1 − (distance/radius) × 0.3, clampée [0.7, 1.0].</summary>
    private double ConfidenceAt(double distance) => ConfidenceAt(distance, _settings.Radius);

    /// <summary>Confiance à une portée donnée (portée des gisements distincte du rayon des entités).</summary>
    private double ConfidenceAt(double distance, double radius) =>
        radius <= 0.0
            ? 0.7
            : Math.Clamp(1.0 - ((distance / radius) * _settings.ConfidenceFalloff), 0.7, 1.0);

    /// <summary>Identifiant déterministe d'un obstacle (FNV-1a canonique).</summary>
    private static ulong DeterministicId(string value) => Fnv1a64.HashUtf8(value);
}
