// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Description versionnée et déterministe du monde envoyée à Unreal avant les snapshots.

namespace Simulation.Core.World;

/// <summary>Description versionnée et déterministe du monde envoyée à Unreal avant les snapshots.</summary>
public sealed record WorldDescription(
    string Version,
    int Width,
    int Height,
    double CellSize,
    int TicksPerSecond,
    // Échelle temporelle/ spatiale du run (champs additifs, description
    // 1.0 → 1.1) : secondes simulées par tick (défaut 60 = 1 tick = 1 minute,
    // et mètres par unité SYNE (informatif, PRISM k = 100 uu / unité).
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

/// <summary>
/// Géométrie des obstacles : règle <b>unique</b> de découpage des cases, partagée
/// par la description du monde (ce que le client lit) et par le placement initial
/// (ce que le monde fait). Elle a déjà été dupliquée : le corps de la règle vivait
/// inline dans <see cref="WorldDescriptionBuilder.Build"/> et le placement
/// l'ignorait, si bien que des agents démarraient dans un obstacle que la
/// description publiait pourtant comme bloqué.
/// </summary>
public static class ObstacleGeometry
{
    /// <summary>
    /// Vrai si <paramref name="obstacle"/> couvre la case dont le centre est
    /// <paramref name="cellCenterX"/>/<paramref name="cellCenterY"/> : distance de
    /// centre à centre inférieure ou égale à <c>rayon + taille de case / 2</c>.
    /// </summary>
    public static bool CoversCell(Obstacle obstacle, double cellCenterX, double cellCenterY, double cellSize)
    {
        ArgumentNullException.ThrowIfNull(obstacle);
        return Math.Sqrt(Math.Pow(obstacle.Position.X - cellCenterX, 2) + Math.Pow(obstacle.Position.Y - cellCenterY, 2))
            <= obstacle.Radius + (cellSize / 2);
    }
}

public static class WorldDescriptionBuilder
{
    /// <summary>1.0 → 1.1 : champs additifs <c>simulatedSecondsPerTick</c> et <c>metersPerUnit</c>.</summary>
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
        // Couverture calculée **une fois** par case, et partagée par `cells[]` et par
        // le placement des marqueurs de ressources : `walkable` et « marchable pour
        // le placement » sont la même propriété, décidée par la même règle
        // (`ObstacleGeometry`). Les recalculer séparément est ce qui avait laissé
        // partir un marqueur de ressource dans un obstacle que la description
        // publiait pourtant comme bloqué.
        var blocked = new bool[countX * countY];
        for (int y = 0; y < countY; y++)
        for (int x = 0; x < countX; x++)
        {
            double cx = (x + 0.5) * cellSize;
            double cy = (y + 0.5) * cellSize;
            var obstacles = world.Obstacles
                .Where(o => ObstacleGeometry.CoversCell(o, cx, cy, cellSize))
                .Select(o => o.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            blocked[(y * countX) + x] = obstacles.Length > 0;
            cells.Add(new WorldCell(x, y, "plains", obstacles.Length == 0, 0, obstacles.Length == 0 ? 1 : 100, obstacles));
        }

        string[] kinds = ["food", "water", "wood", "mineral"];
        var resources = BuildResourceLocations(seed, kinds, countX, countY, blocked);

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

    /// <summary>
    /// Emplacements initiaux des ressources publiés au client. La règle de
    /// placement vit désormais dans <see cref="ResourceNodePlacement"/> — la
    /// <b>même</b> que celle qui pose les gisements simulés : le moteur ne publie
    /// plus une ressource qu'il ne simule pas, et les deux ne peuvent plus
    /// diverger (tirages du LCG local, sur les seules cases marchables
    /// <paramref name="blocked"/> issues de la même passe de couverture que
    /// <c>cells[]</c>). Un tirage tombant dans un obstacle avance jusqu'à la
    /// première case libre, exactement comme le placement des agents et comme le
    /// miroir du mock.
    /// </summary>
    private static List<WorldResource> BuildResourceLocations(
        ulong seed, IReadOnlyList<string> kinds, int countX, int countY, bool[] blocked)
    {
        IReadOnlyList<ResourceLocation> locations = ResourceNodePlacement.Locations(
            countX,
            countY,
            blocked,
            seed,
            ResourceLocationsPerType);

        var resources = new List<WorldResource>(locations.Count);
        foreach (ResourceLocation location in locations)
        {
            resources.Add(new WorldResource(
                location.Id,
                kinds[(int)location.Kind],
                location.CellX,
                location.CellY,
                ResourceNodePlacement.DefaultAmount));
        }

        return resources;
    }
}
