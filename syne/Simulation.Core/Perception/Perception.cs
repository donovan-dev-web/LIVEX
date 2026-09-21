using System.Globalization;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;

namespace Simulation.Core.Perception;

/// <summary>
/// Observation (DATA_MODEL.md §4) : identité, type, position, confiance, tick,
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
/// (décision n°6, défaut 50), la ligne de vue peut être masquée par un obstacle
/// (ADR-013), la confiance décroît avec la distance
/// <c>1 − (distance/radius) × 0.3</c> clampée [0.7, 1.0] (DATA_MODEL.md §4), et
/// la perception est étagée en groupes de rotation (COGNITIVE_ARCHITECTURE.md §3
/// — groupe = <c>id % rotationInterval</c>, perçoit au tick <c>t ≡ groupe</c>).
/// </summary>
public sealed class PerceptionSystem
{
    private readonly Simulation.Core.World.World _world;
    private readonly PerceptionSettings _settings;

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

        var result = new List<Observation>();
        IReadOnlyList<Entity> candidates = _world.Grid.QueryCircle(
            subject.Position,
            _settings.Radius,
            excludeId: subject.Id.Value);

        var ordered = candidates
            .Select(candidate => (Candidate: candidate, Distance: subject.Position.DistanceTo(candidate.Position)))
            .ToList();
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

        return result;
    }

    private Observation ObservationOf(Entity candidate, double distance, ulong tick)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["species"] = candidate.Species,
            ["x"] = candidate.Position.X.ToString("0.00", CultureInfo.InvariantCulture),
            ["y"] = candidate.Position.Y.ToString("0.00", CultureInfo.InvariantCulture),
        };
        return new Observation(
            candidate.Id.Value,
            "entity",
            candidate.Position,
            ConfidenceAt(distance),
            tick,
            attributes);
    }

    /// <summary>Confiance = 1 − (distance/radius) × 0.3, clampée [0.7, 1.0].</summary>
    private double ConfidenceAt(double distance) =>
        Math.Clamp(1.0 - ((distance / _settings.Radius) * _settings.ConfidenceFalloff), 0.7, 1.0);

    /// <summary>Identifiant déterministe d'un obstacle (FNV-1a).</summary>
    private static ulong DeterministicId(string value)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}