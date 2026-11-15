using Prism.Core.Navigation;
using Prism.Core.World;

namespace Prism.Core.Tests;

/// <summary>
/// Cache de chemins à invalidation exacte (PRISM-104) : une entrée ne survit que tant qu'aucune
/// région qu'elle a <em>lue</em> n'a été écrite. Le point de ces tests n'est pas la vitesse mais
/// l'égalité : un résultat servi par le cache doit être ce qu'une recherche fraîche aurait rendu,
/// y compris quand le monde a changé ailleurs.
/// </summary>
public sealed class PathCacheRevisionTests
{
    [Fact]
    public void CacheSurvivesAWriteOutsideEveryRegionItRead()
    {
        var world = CreateWorld(32, 32, regionSize: 8);
        var graph = CreateMovement(world);
        var cache = new PathfindingCache(8);

        var first = AStarPathfinder.FindPath(graph, 1, 1, 10, 10, 4096, cache);
        Assert.Equal(PathfindingStatus.Found, first.Status);
        var reads = first.Reads;
        Assert.NotNull(reads);
        // Deux extrémités et un couloir de trois cases de côté : la recherche ne lit qu'une
        // poignée de régions, et sûrement pas les seize que compte le monde.
        Assert.InRange(reads!.Count, 1, 8);
        Assert.DoesNotContain(15, reads.Regions.ToArray());

        // Une écriture dans un coin opposé, hors de toute région lue.
        world.SetBlockOverride(new BlockCoordinate(30, 30, 5), new BlockOverride(1, 0, false));

        var second = AStarPathfinder.FindPath(graph, 1, 1, 10, 10, 4096, cache);
        Assert.Equal(1, cache.Statistics.Hits);
        Assert.Equal(0, cache.Invalidations);
        Assert.Equal(first.Path, second.Path);
        Assert.Equal(first.Cost, second.Cost);
    }

    [Fact]
    public void CacheIsRejectedWhenAReadRegionIsWritten()
    {
        var world = CreateWorld(32, 32, regionSize: 8);
        var graph = CreateMovement(world);
        var cache = new PathfindingCache(8);

        var first = AStarPathfinder.FindPath(graph, 1, 1, 10, 1, 4096, cache);
        Assert.Equal(PathfindingStatus.Found, first.Status);

        // Sur le chemin retenu : la case est certainement dans l'ensemble lu.
        world.SetBlockOverride(new BlockCoordinate(5, 1, 5), new BlockOverride(1, 0, false));

        var second = AStarPathfinder.FindPath(graph, 1, 1, 10, 1, 4096, cache);
        Assert.Equal(1, cache.Invalidations);
        Assert.False(first.Path.SequenceEqual(second.Path));
        Assert.Equal(PathfindingStatus.Found, second.Status);
    }

    [Fact]
    public void FailureIsReusedUntilAReadRegionOpensThePath()
    {
        var world = CreateWorld(24, 24, regionSize: 8, wallX: 12);
        var graph = CreateMovement(world);
        var cache = new PathfindingCache(8);
        var metrics = new PathfindingMetrics();

        var blocked = AStarPathfinder.FindPath(graph, 2, 12, 20, 12, 65536, cache, metrics);
        Assert.Equal(PathfindingStatus.NotFound, blocked.Status);
        Assert.True(blocked.ExpandedNodes > 0);

        // Aucune région lue n'a bougé : rejouer la recherche donnerait le même échec, donc le
        // cache doit servir cet échec au lieu de réexplorer tout le composant.
        var again = AStarPathfinder.FindPath(graph, 2, 12, 20, 12, 65536, cache, metrics);
        Assert.Equal(PathfindingStatus.NotFound, again.Status);
        Assert.Equal(1, cache.Statistics.Hits);
        Assert.Equal(1, metrics.Snapshot().Searches);

        // Le trou est percé dans le mur : l'échec mémorisé est périmé, et la recherche doit
        // aboutir. C'est exactement le mensonge qu'une invalidation « par le chemin retenu »
        // n'aurait pas vu.
        world.SetBlockOverride(new BlockCoordinate(12, 12, 5), new BlockOverride(0, 0, true));
        world.SetBlockOverride(new BlockCoordinate(12, 12, 6), new BlockOverride(0, 0, true));

        var reopened = AStarPathfinder.FindPath(graph, 2, 12, 20, 12, 65536, cache);
        Assert.Equal(PathfindingStatus.Found, reopened.Status);
        Assert.Equal(1, cache.Invalidations);
    }

    [Fact]
    public void CachedResultIsExactlyWhatAFreshSearchReturns()
    {
        var world = CreateWorld(48, 48, regionSize: 8);
        var graph = CreateMovement(world);
        var cache = new PathfindingCache(16);

        var cached = AStarPathfinder.FindPath(graph, 3, 3, 40, 37, 65536, cache);
        Assert.True(cached.Succeeded);

        // Plusieurs écritures hors du couloir lu, puis une seconde interrogation.
        for (var step = 0; step < 4; step++)
        {
            world.SetSurfaceColumn(45 - step, 45, new SurfaceColumn(5, 1, 0, 0));
        }

        var served = AStarPathfinder.FindPath(graph, 3, 3, 40, 37, 65536, cache);
        Assert.Equal(1, cache.Statistics.Hits);

        // Témoin : la même recherche, sans cache, sur le monde courant.
        var fresh = AStarPathfinder.FindPath(graph, 3, 3, 40, 37, 65536);
        Assert.Equal(fresh.Path, served.Path);
        Assert.Equal(fresh.Cost, served.Cost);
        Assert.Equal(fresh.ExpandedNodes, served.ExpandedNodes);
    }

    [Fact]
    public void ReadsCoverStartAndGoalRegions()
    {
        var world = CreateWorld(32, 32, regionSize: 8);
        var graph = CreateMovement(world);

        var result = AStarPathfinder.FindPath(graph, 0, 0, 31, 31, 65536);
        var reads = result.Reads;
        Assert.NotNull(reads);
        // Régions 0 (0,0) et 15 (31,31) — l'échec mémorisé doit périmer si la cible change.
        var regions = reads!.Regions.ToArray();
        Assert.Contains(0, regions);
        Assert.Contains(15, regions);
        Assert.Equal(regions.Order(), regions);
    }

    [Fact]
    public void MetricsCountSearchesHitsFailuresAndBudgetExhaustion()
    {
        var world = CreateWorld(16, 16, regionSize: 8, wallX: 8);
        var graph = CreateMovement(world);
        var cache = new PathfindingCache(8);
        var metrics = new PathfindingMetrics();

        AStarPathfinder.FindPath(graph, 1, 1, 12, 12, 65536, cache, metrics);
        AStarPathfinder.FindPath(graph, 1, 1, 4, 12, 65536, cache, metrics);
        AStarPathfinder.FindPath(graph, 1, 1, 4, 12, 65536, cache, metrics);
        AStarPathfinder.FindPath(graph, 1, 1, 12, 12, 2, cache, metrics);

        var snapshot = metrics.Snapshot();
        Assert.Equal(3, snapshot.Searches);
        Assert.Equal(1, snapshot.CacheHits);
        Assert.Equal(1, snapshot.FailedSearches);
        Assert.Equal(1, snapshot.BudgetExhaustedSearches);
        Assert.True(snapshot.ExpandedNodes > 0);
        Assert.Equal(666, snapshot.FailureRatePermille);

        var delta = snapshot - metrics.Snapshot();
        Assert.Equal(0, delta.Searches);
        Assert.Equal(0, delta.ExpandedNodes);
    }

    [Fact]
    public void FailureRateIsZeroWithoutSearchAndNeverExceedsOneThousand()
    {
        Assert.Equal(0, default(PathfindingMetricsSnapshot).FailureRatePermille);
        var exhausted = new PathfindingMetricsSnapshot(2, 0, 0, 10, 5, 5);
        Assert.Equal(1000, exhausted.FailureRatePermille);
    }

    private static WorldGrid CreateWorld(int width, int height, int regionSize, int wallX = -1)
    {
        var world = new WorldGrid(width, height, regionSize, verticalSize: 16);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 1, 0, 0));
            }
        }

        if (wallX >= 0)
        {
            for (var y = 0; y < height; y++)
            {
                world.SetBlockOverride(new BlockCoordinate(wallX, y, 5), new BlockOverride(1, 0, false));
                world.SetBlockOverride(new BlockCoordinate(wallX, y, 6), new BlockOverride(1, 0, false));
            }
        }

        return world;
    }

    private static MovementGraph CreateMovement(WorldGrid world) =>
        new(world, new MovementRules(new HashSet<ushort> { 1 }, new HashSet<ushort> { 2 }));
}
