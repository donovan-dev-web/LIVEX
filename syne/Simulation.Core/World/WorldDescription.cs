namespace Simulation.Core.World;

/// <summary>Versioned, deterministic description sent to Unreal before snapshots.</summary>
public sealed record WorldDescription(
    string Version,
    int Width,
    int Height,
    double CellSize,
    int TicksPerSecond,
    // Échelle temporelle/ spatiale du run (champs additifs ADR-017, description
    // 1.0 → 1.1) : secondes simulées par tick (défaut 60 = 1 tick = 1 minute,
    // ADR-005) et mètres par unité SYNE (informatif, PRISM k = 100 uu / unité).
    int SimulatedSecondsPerTick,
    double MetersPerUnit,
    int CellCountX,
    int CellCountY,
    IReadOnlyList<WorldAgent> Agents,
    IReadOnlyList<WorldCell> Cells,
    IReadOnlyList<WorldObstacle> Obstacles,
    IReadOnlyList<WorldResource> Resources,
    IReadOnlyList<WorldRegion> Regions);

public sealed record WorldAgent(ulong Id, string Species, WorldAgentPosition Position);

public sealed record WorldAgentPosition(double X, double Y);

public sealed record WorldCell(
    int X, int Y, string TerrainType, bool Walkable, double Height,
    double MovementCost, IReadOnlyList<string> Obstacles);

public sealed record WorldObstacle(string Id, double X, double Y, double Radius);

public sealed record WorldResource(string Id, string Kind, int X, int Y, double Quantity);

public sealed record WorldRegion(string Id, int X, int Y, int Width, int Height);

public static class WorldDescriptionBuilder
{
    /// <summary>1.0 → 1.1 (ADR-017) : champs additifs <c>simulatedSecondsPerTick</c> et <c>metersPerUnit</c>.</summary>
    public const string Version = "1.1";
    private const int ResourceLocationsPerType = 3;

    public static WorldDescription Build(
        World world,
        ulong seed,
        double cellSize = 10,
        int ticksPerSecond = 10,
        int simulatedSecondsPerTick = Simulation.Core.Configuration.SimulationClock.DefaultSimulatedSecondsPerTick,
        double metersPerUnit = 1.0)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
        if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
        if (simulatedSecondsPerTick is < Simulation.Core.Configuration.SimulationClock.MinSimulatedSecondsPerTick
            or > Simulation.Core.Configuration.SimulationClock.MaxSimulatedSecondsPerTick)
            throw new ArgumentOutOfRangeException(nameof(simulatedSecondsPerTick));
        if (!double.IsFinite(metersPerUnit) || metersPerUnit <= 0)
            throw new ArgumentOutOfRangeException(nameof(metersPerUnit));
        int countX = (int)Math.Ceiling(world.Size.Width / cellSize);
        int countY = (int)Math.Ceiling(world.Size.Height / cellSize);
        var agents = world.Entities
            .OrderBy(entity => entity.Id.Value)
            .Select(entity => new WorldAgent(entity.Id.Value, entity.Species,
                new WorldAgentPosition(entity.Position.X, entity.Position.Y)))
            .ToArray();
        var initialObstacles = world.Obstacles
            .OrderBy(obstacle => obstacle.Id, StringComparer.Ordinal)
            .Select(obstacle => new WorldObstacle(
                obstacle.Id,
                obstacle.Position.X,
                obstacle.Position.Y,
                obstacle.Radius))
            .ToArray();
        var cells = new List<WorldCell>(countX * countY);
        for (int y = 0; y < countY; y++)
        for (int x = 0; x < countX; x++)
        {
            double cx = (x + 0.5) * cellSize;
            double cy = (y + 0.5) * cellSize;
            var obstacles = world.Obstacles
                .Where(o => Math.Sqrt(Math.Pow(o.Position.X - cx, 2) + Math.Pow(o.Position.Y - cy, 2)) <= o.Radius + cellSize / 2)
                .Select(o => o.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            cells.Add(new WorldCell(x, y, "plains", obstacles.Length == 0, 0, obstacles.Length == 0 ? 1 : 100, obstacles));
        }

        string[] kinds = ["food", "water", "wood", "mineral"];
        var resources = BuildResourceLocations(seed, kinds, countX, countY);

        int regionSize = 10;
        var regions = new List<WorldRegion>();
        for (int y = 0; y < countY; y += regionSize)
        for (int x = 0; x < countX; x += regionSize)
            regions.Add(new WorldRegion($"chunk-{x / regionSize}-{y / regionSize}", x, y,
                Math.Min(regionSize, countX - x), Math.Min(regionSize, countY - y)));
        return new WorldDescription(Version, world.Size.Width, world.Size.Height, cellSize,
            ticksPerSecond, simulatedSecondsPerTick, metersPerUnit,
            countX, countY, agents, cells, initialObstacles, resources, regions);
    }

    private static List<WorldResource> BuildResourceLocations(
        ulong seed, IReadOnlyList<string> kinds, int countX, int countY)
    {
        int cellCount = countX * countY;
        // LCG numérique explicite, distinct du PRNG de simulation
        // (Xoshiro256**) : la description du monde ne doit consommer aucun tirage
        // de la simulation (DETERMINISM.md §3, l'observabilité n'a pas le droit
        // de déplacer la trajectoire). C'est pourquoi ce générateur est local et
        // documenté plutôt que partagé : les paramètres ci-dessous fixent la
        // disposition des ressources annoncée au client, et en changer invalide
        // l'accord entre la description et le monde affiché.
        uint state = unchecked((uint)seed ^ (uint)(seed >> 32) ^ 0xA511E9B3u);
        var resources = new List<WorldResource>(kinds.Count * Math.Min(ResourceLocationsPerType, cellCount));

        uint NextCell()
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return state;
        }

        for (int kindIndex = 0; kindIndex < kinds.Count; kindIndex++)
        {
            var usedCells = new HashSet<int>();
            int locationCount = Math.Min(ResourceLocationsPerType, cellCount);
            for (int locationIndex = 0; locationIndex < locationCount; locationIndex++)
            {
                int cellIndex = (int)(NextCell() % (uint)cellCount);
                while (!usedCells.Add(cellIndex))
                    cellIndex = (cellIndex + 1) % cellCount;

                resources.Add(new WorldResource(
                    $"resource-{kinds[kindIndex]}-{locationIndex + 1}",
                    kinds[kindIndex],
                    cellIndex % countX,
                    cellIndex / countX,
                    100));
            }
        }

        return resources;
    }
}
