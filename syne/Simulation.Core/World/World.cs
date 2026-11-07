// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Simulation.Core.World;

using Simulation.Core.Configuration;

/// <summary>Sens d'une modification d'environnement (SYNE-071).</summary>
public enum EnvironmentChangeKind
{
    Added,
    Removed,
}

/// <summary>
/// Modification d'environnement tracée (SYNE-071).
/// </summary>
public sealed record EnvironmentChange(EnvironmentChangeKind Kind, Obstacle Obstacle);

/// <summary>
/// Monde observé (SYNE-220 étape 3c) : conteneur minimal d'entités et de
/// références (territoires, livres, contenu). La géométrie locale a migré
/// vers PRISM ; les membres historiques sont conservés comme shims vides pour
/// la compatibilité des tests existants.
/// </summary>
public sealed class World : IWorld
{
    private readonly List<Simulation.Core.Entities.Entity> _entities = [];
    private readonly List<Territory> _territories = [];
    private readonly List<Book> _books = [];

    public World(WorldSize size)
        : this(size, null)
    {
    }

    public World(WorldSize size, double spatialCellSize)
        : this(size, null)
    {
    }

    public World(WorldSize size, string? contentHash)
    {
        Size = size;
        ContentHash = contentHash;
    }

    public WorldSize Size { get; }

    public string? ContentHash { get; }

    public IReadOnlyList<Simulation.Core.Entities.Entity> Entities => _entities;

    public IReadOnlyList<Territory> Territories => _territories;

    public IReadOnlyList<Book> Books => _books;

    public void AddEntity(Simulation.Core.Entities.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _entities.Add(entity);
        Grid.Add(entity);
    }

    public void RemoveEntity(Simulation.Core.Entities.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!_entities.Remove(entity))
        {
            throw new InvalidOperationException($"L'entité {entity.Id.Value} n'est pas indexée dans le monde.");
        }
        Grid.Remove(entity);
    }

    public void AddBook(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (_books.Any(existing => existing.Id == book.Id))
        {
            throw new ArgumentException($"Un livre portant l'identifiant « {book.Id} » existe déjà.", nameof(book));
        }
        _books.Add(book);
    }

    public void AddTerritory(Territory territory)
    {
        ArgumentNullException.ThrowIfNull(territory);
        if (territory.Center != Position.Clamp(territory.Center, Size))
        {
            throw new ArgumentOutOfRangeException(nameof(territory), "Le centre de la zone de territoire sort du monde (non-toroidal).");
        }
        if (_territories.Any(existing => existing.Id == territory.Id))
        {
            throw new ArgumentException($"Une zone de territoire portant l'identifiant « {territory.Id} » existe déjà.", nameof(territory));
        }
        _territories.Add(territory);
    }

    // --- Shims de compatibilité (géométrie retirée du moteur) ---
    private SpatialGrid? _grid;
    public SpatialGrid Grid => _grid ??= new SpatialGrid(Size, 10.0);
    public IReadOnlyList<Obstacle> Obstacles => Array.Empty<Obstacle>();
    public ulong ObstacleRevision => 0;
    public IReadOnlyList<EnvironmentChange> LastEnvironmentChanges => Array.Empty<EnvironmentChange>();
    public void ClearEnvironmentChanges() { }
    public void AddObstacle(Obstacle obstacle) { }
    public void PlaceConstruction(Obstacle obstacle) { }
    public bool RemoveConstruction(string id) => false;
    public void ApplyConfiguredLayout(Configuration.WorldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Territories.Enabled)
        {
            foreach (Configuration.TerritoryZoneDefinition spec in settings.Territories.Zones)
            {
                AddTerritory(new Territory(spec.Id, new Position(spec.CenterX, spec.CenterY), spec.Radius));
            }
        }
    }
    public void ApplyResourceLayout(Configuration.ResourceSettings settings, ulong seed, double cellSize) { }
    public void ReplaceResourceNodes(IEnumerable<ResourceNode> nodes) { }
    public void AddResourceNode(ResourceNode node) { }
    public ResourceNode? NearestResourceNode(Position position, ResourceKind kind, double maxDistance) => null;
    public (Position Position, Simulation.Core.Prng.Xoshiro256StarStar Next) SamplePosition(Simulation.Core.Prng.Xoshiro256StarStar rng)
    {
        var next = rng.NextDouble(out double u);
        next = next.NextDouble(out double v);
        return (new Position(u * Size.Width, v * Size.Height), next);
    }
}