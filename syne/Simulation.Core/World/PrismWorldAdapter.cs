// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Simulation.Core.World;

/// <summary>
/// Adaptateur de monde adossé à une source externe (PRISM) pour l'étape 1
/// (mode parallèle) de SYNE-220. Il expose la même surface de lecture
/// (<see cref="IWorld"/>) qu'un monde local, mais son état est alimenté par
/// les observations/perceptions reçues chaque tick plutôt que généré par SYNE.
/// L'étape 2 (bascule par profil) pourra l'injecter à la place de
/// <see cref="World"/> ; l'étape 3 supprimera alors le monde local.
/// </summary>
public sealed class PrismWorldAdapter : IWorld
{
    private readonly List<Simulation.Core.Entities.Entity> _entities = [];
    private readonly List<Obstacle> _obstacles = [];
    private readonly List<Territory> _territories = [];
    private readonly List<Book> _books = [];
    private readonly List<ResourceNode> _resourceNodes = [];
    private ulong _obstacleRevision;

    public PrismWorldAdapter(WorldSize size, double spatialCellSize)
    {
        Size = size;
        Grid = new SpatialGrid(size, spatialCellSize);
    }

    public WorldSize Size { get; }

    public SpatialGrid Grid { get; }

    public IReadOnlyList<Simulation.Core.Entities.Entity> Entities => _entities;

    public IReadOnlyList<Obstacle> Obstacles => _obstacles;

    public IReadOnlyList<Territory> Territories => _territories;

    public IReadOnlyList<Book> Books => _books;

    public IReadOnlyList<ResourceNode> ResourceNodes => _resourceNodes;

    public IReadOnlyList<EnvironmentChange> LastEnvironmentChanges => Array.Empty<EnvironmentChange>();

    public ulong ObstacleRevision => _obstacleRevision;

    public (Simulation.Core.World.Position Position, Simulation.Core.Prng.Xoshiro256StarStar Next) SamplePosition(Simulation.Core.Prng.Xoshiro256StarStar rng)
    {
        var next = rng.NextDouble(out double u);
        next = next.NextDouble(out double v);
        return (new Simulation.Core.World.Position(u * Size.Width, v * Size.Height), next);
    }

    public void AddEntity(Simulation.Core.Entities.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _entities.Add(entity);
    }

    public void AddBook(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _books.Add(book);
    }

    public void ReplaceResourceNodes(IEnumerable<ResourceNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _resourceNodes.Clear();
        _resourceNodes.AddRange(nodes);
    }

    public void ApplyResourceLayout(Simulation.Core.Configuration.ResourceSettings settings, ulong seed, double cellSize)
    {
        // Source externe : la disposition des ressources est fournie par PRISM.
    }

    public void ReplaceEntities(IEnumerable<Simulation.Core.Entities.Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _entities.Clear();
        _entities.AddRange(entities);
    }

    public void ReplaceObstacles(IEnumerable<Obstacle> obstacles)
    {
        ArgumentNullException.ThrowIfNull(obstacles);
        _obstacles.Clear();
        _obstacles.AddRange(obstacles);
        _obstacleRevision++;
    }

    public void ReplaceTerritories(IEnumerable<Territory> territories)
    {
        ArgumentNullException.ThrowIfNull(territories);
        _territories.Clear();
        _territories.AddRange(territories);
    }
}