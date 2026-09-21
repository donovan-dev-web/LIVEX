namespace Simulation.Core.World;

/// <summary>
/// Le monde : plan 2D continu non-toroidal, grille spatiale de perception,
/// index des entités. Positions dans [0, Width] x [0, Height] (DATA_MODEL.md §2).
/// </summary>
public sealed class World
{
    private readonly List<Simulation.Core.Entities.Entity> _entities = [];

    public World(WorldSize size)
        : this(size, size.Width / 10.0)
    {
    }

    public World(WorldSize size, double spatialCellSize)
    {
        Size = size;
        Grid = new SpatialGrid(size, spatialCellSize);
    }

    public WorldSize Size { get; }

    public SpatialGrid Grid { get; }

    public IReadOnlyList<Simulation.Core.Entities.Entity> Entities => _entities;

    public void AddEntity(Simulation.Core.Entities.Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Position != Position.Clamp(entity.Position, Size))
        {
            throw new ArgumentOutOfRangeException(nameof(entity), "La position de l'entité sort du monde (non-toroidal).");
        }

        Grid.Add(entity);
        _entities.Add(entity);
    }

    /// <summary>
    /// Position uniforme dans le monde (déterministe en fonction de la graine du PRNG).
    /// </summary>
    public (Position Position, Simulation.Core.Prng.Xoshiro256StarStar Next) SamplePosition(Simulation.Core.Prng.Xoshiro256StarStar rng)
    {
        var next = rng.NextDouble(out double u);
        next = next.NextDouble(out double v);
        return (new Position(u * Size.Width, v * Size.Height), next);
    }
}