// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Simulation.Core.World;

/// <summary>
/// Adaptateur de monde adossé à une source externe (PRISM) pour SYNE-220.
/// Il expose la même surface de lecture (<see cref="IWorld"/>) qu'un monde
/// observé, mais son état est alimenté par les observations/perceptions reçues
/// chaque tick plutôt que généré par SYNE.
/// </summary>
/// <remarks>
/// Étape 1 (mode parallèle) : l'interface <see cref="IWorld"/> existe, le monde
/// local reste le défaut. Étape 2 (bascule par profil) : le profil
/// <c>world.externalWorld</c> injecte cet adaptateur à la place de
/// <see cref="World"/> et <see cref="ApplyAgentStates"/> absorbe les positions
/// publiées par PRISM. Étape 3 : suppression du monde local.
/// <para>
/// Contrairement à <see cref="World"/>, l'adaptateur ne <b>crée</b> pas de
/// terrain : il ne fait que refléter la source externe. Les entités qu'il
/// expose sont les mêmes instances que celles de la boucle SYNE (elles sont
/// déplacées, jamais remplacées), afin que la cognition garde ses références
/// d'un tick à l'autre.
/// </para>
/// </remarks>
public sealed class PrismWorldAdapter : IWorld
{
    private readonly List<Simulation.Core.Entities.Entity> _entities = [];
    private readonly Dictionary<ulong, Simulation.Core.Entities.Entity> _entitiesById = [];
    private readonly List<Territory> _territories = [];
    private readonly List<Book> _books = [];

    public PrismWorldAdapter(WorldSize size, double spatialCellSize)
        : this(size, null)
    {
    }

    public PrismWorldAdapter(WorldSize size, string? contentHash)
    {
        Size = size;
        ContentHash = contentHash;
    }

    public WorldSize Size { get; }

    public string? ContentHash { get; }

    private SpatialGrid? _grid;
    public SpatialGrid Grid => _grid ??= new SpatialGrid(Size, 10.0);
    public IReadOnlyList<Obstacle> Obstacles => Array.Empty<Obstacle>();
    public ulong ObstacleRevision => 0;
    public IReadOnlyList<EnvironmentChange> LastEnvironmentChanges => Array.Empty<EnvironmentChange>();
    public void ClearEnvironmentChanges() { }
    public void ReplaceObstacles(IEnumerable<Obstacle> obstacles) { }
    public void AddObstacle(Obstacle obstacle) { }
    public ResourceNode? NearestResourceNode(Position position, ResourceKind kind, double maxDistance) => null;
    public void ReplaceResourceNodes(IEnumerable<ResourceNode> nodes) { }
    public void ApplyResourceLayout(Simulation.Core.Configuration.ResourceSettings settings, ulong seed, double cellSize) { }
    public (Position Position, Simulation.Core.Prng.Xoshiro256StarStar Next) SamplePosition(Simulation.Core.Prng.Xoshiro256StarStar rng)
    {
        var next = rng.NextDouble(out double u);
        next = next.NextDouble(out double v);
        return (new Position(u * Size.Width, v * Size.Height), next);
    }

    public IReadOnlyList<Simulation.Core.Entities.Entity> Entities => _entities;

    public IReadOnlyList<Territory> Territories => _territories;

    public IReadOnlyList<Book> Books => _books;

    /// <summary>
    /// Ajoute une entité (sans grille locale). Même garde que
    /// <see cref="World.AddEntity"/> : le plan n'est pas toroidal.
    /// </summary>
    public void AddEntity(Simulation.Core.Entities.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Position != Position.Clamp(entity.Position, Size))
        {
            throw new ArgumentOutOfRangeException(nameof(entity), "La position de l'entité sort du monde (non-toroidal).");
        }

        if (_entitiesById.ContainsKey(entity.Id.Value))
        {
            throw new ArgumentException($"Une entité portant l'identifiant {entity.Id.Value} existe déjà.", nameof(entity));
        }

        _entities.Add(entity);
        _entitiesById[entity.Id.Value] = entity;
        Grid.Add(entity);
    }

    /// <summary>
    /// Retire une entité (mortalité). Lève si elle n'est pas suivie.
    /// </summary>
    public void RemoveEntity(Simulation.Core.Entities.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!TryRemoveEntity(entity.Id.Value))
        {
            throw new InvalidOperationException($"L'entité {entity.Id.Value} n'est pas indexée dans le monde.");
        }
    }

    /// <summary>
    /// Retire l'entité d'identifiant donné si elle est suivie.
    /// </summary>
    public bool TryRemoveEntity(ulong entityId)
    {
        if (!_entitiesById.Remove(entityId, out Simulation.Core.Entities.Entity? entity))
        {
            return false;
        }

        _entities.Remove(entity);
        Grid.Remove(entity);
        return true;
    }

    /// <summary>
    /// Applique les positions publiées par PRISM. Un identifiant inconnu est
    /// ignoré.
    /// </summary>
    public int ApplyAgentStates(IEnumerable<(ulong AgentId, Position Position)> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        var latest = new SortedDictionary<ulong, Position>();
        foreach ((ulong agentId, Position position) in states)
        {
            latest[agentId] = position;
        }

        int moved = 0;
        foreach ((ulong agentId, Position position) in latest)
        {
            if (!_entitiesById.TryGetValue(agentId, out Simulation.Core.Entities.Entity? entity))
            {
                continue;
            }

            Position clamped = Position.Clamp(position, Size);
            if (entity.Position == clamped)
            {
                continue;
            }

            // Mise à jour de la position observée (pas de grille locale).
            entity.SetPosition(clamped);
            moved++;
        }

        return moved;
    }

    public void AddBook(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _books.Add(book);
    }

    public void ReplaceTerritories(IEnumerable<Territory> territories)
    {
        ArgumentNullException.ThrowIfNull(territories);
        _territories.Clear();
        _territories.AddRange(territories);
    }

    public void SyncEntities(IEnumerable<Simulation.Core.Entities.Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var incoming = new Dictionary<ulong, Simulation.Core.Entities.Entity>();
        var ordered = new List<Simulation.Core.Entities.Entity>();
        foreach (Simulation.Core.Entities.Entity entity in entities)
        {
            ArgumentNullException.ThrowIfNull(entity);
            if (!incoming.TryAdd(entity.Id.Value, entity))
            {
                throw new ArgumentException(
                    $"L'état de référence contient deux entités d'identifiant {entity.Id.Value}.",
                    nameof(entities));
            }

            ordered.Add(entity);
        }

        foreach (ulong trackedId in _entitiesById.Keys.ToArray())
        {
            if (!incoming.ContainsKey(trackedId))
            {
                TryRemoveEntity(trackedId);
            }
        }

        foreach (Simulation.Core.Entities.Entity entity in ordered)
        {
            if (_entitiesById.TryGetValue(entity.Id.Value, out Simulation.Core.Entities.Entity? tracked))
            {
                Position clamped = Position.Clamp(entity.Position, Size);
                if (tracked.Position != clamped)
                {
                    tracked.SetPosition(clamped);
                }

                continue;
            }

            AddEntity(entity);
        }
    }
}