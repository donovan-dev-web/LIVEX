using Prism.Core.Navigation;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class MovementGraphTests
{
    [Fact]
    public void MovementHonorsOneBlockAscentThreeBlockDescentAndBlockedWater()
    {
        var world = CreateFlatWorld(6, 2, surfaceZ: 5);
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(6, 1, 0, 0));
        world.SetSurfaceColumn(2, 0, new SurfaceColumn(8, 1, 0, 0));
        world.SetSurfaceColumn(3, 0, new SurfaceColumn(3, 1, 0, 0));
        world.SetSurfaceColumn(0, 1, new SurfaceColumn(8, 1, 0, 0));
        world.SetSurfaceColumn(1, 1, new SurfaceColumn(5, 1, 0, 0));
        world.SetBlockOverride(new BlockCoordinate(4, 0, 6), new BlockOverride(2, 0, false));
        var graph = CreateMovement(world);

        Assert.Contains(graph.GetNeighbors(new MovementNode(0, 0, 6)),
            edge => edge.Destination == new MovementNode(1, 0, 7));
        Assert.DoesNotContain(graph.GetNeighbors(new MovementNode(1, 0, 7)),
            edge => edge.Destination == new MovementNode(2, 0, 9));
        Assert.DoesNotContain(graph.GetNeighbors(new MovementNode(2, 0, 9)),
            edge => edge.Destination == new MovementNode(3, 0, 4));
        Assert.Contains(graph.GetNeighbors(new MovementNode(0, 1, 9)),
            edge => edge.Destination == new MovementNode(1, 1, 6));
        Assert.False(graph.CanStandAt(4, 0, out _));
    }

    [Fact]
    public void SolidOverridesBlockTheBodyAndAirOverridesOpenIt()
    {
        var world = CreateFlatWorld(2, 1);
        var graph = CreateMovement(world);
        var head = new BlockCoordinate(1, 0, 6);
        world.SetBlockOverride(head, new BlockOverride(1, 0, false));
        Assert.False(graph.CanStandAt(1, 0, out _));

        world.SetBlockOverride(head, new BlockOverride(0, 0, true));
        Assert.True(graph.CanStandAt(1, 0, out var feetZ));
        Assert.Equal(5, feetZ);

        world.SetBlockOverride(new BlockCoordinate(0, 0, 4), new BlockOverride(0, 0, true));
        Assert.False(graph.CanStandAt(0, 0, out _));
    }

    [Fact]
    public void AStarFindsDeterministicOptimalPathAndHonorsNodeBudget()
    {
        var graph = CreateMovement(CreateFlatWorld(5, 5));
        var first = AStarPathfinder.FindPath(graph, 0, 0, 4, 4, 100);
        var second = AStarPathfinder.FindPath(graph, 0, 0, 4, 4, 100);

        Assert.Equal(PathfindingStatus.Found, first.Status);
        Assert.Equal(5656, first.Cost);
        Assert.Equal(first.Path, second.Path);
        Assert.Equal(first.Cost, second.Cost);
        Assert.Equal(PathfindingStatus.NodeBudgetExceeded,
            AStarPathfinder.FindPath(graph, 0, 0, 4, 4, 1).Status);
    }

    [Fact]
    public void AStarChoosesShortestDetourAroundBlockedDiagonal()
    {
        var world = CreateFlatWorld(5, 5);
        world.SetBlockOverride(new BlockCoordinate(1, 1, 5), new BlockOverride(1, 0, false));
        var result = AStarPathfinder.FindPath(CreateMovement(world), 0, 0, 4, 4, 100);

        Assert.Equal(PathfindingStatus.Found, result.Status);
        Assert.Equal(6828, result.Cost);
        Assert.DoesNotContain(new MovementNode(1, 1, 5), result.Path);
    }

    [Fact]
    public void PathSpeedupAppliesOnlyWhileWalkingBetweenPathBlocks()
    {
        var world = CreateFlatWorld(4, 1);
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(4, 2, 0, 0));
        world.SetSurfaceColumn(2, 0, new SurfaceColumn(4, 2, 0, 0));
        var graph = new MovementGraph(
            world,
            new MovementRules(
                new HashSet<ushort> { 1, 2 },
                new HashSet<ushort>(),
                pathMaterials: new HashSet<ushort> { 2 },
                pathSpeedupPermille: 2000));

        Assert.Equal(1000, graph.GetNeighbors(new MovementNode(0, 0, 5))
            .Single(edge => edge.Destination.X == 1).Cost);
        Assert.Equal(500, graph.GetNeighbors(new MovementNode(1, 0, 5))
            .Single(edge => edge.Destination.X == 2).Cost);
        Assert.Equal(1000, graph.GetNeighbors(new MovementNode(2, 0, 5))
            .Single(edge => edge.Destination.X == 3).Cost);
        Assert.Equal(
            2500,
            AStarPathfinder.FindPath(graph, 0, 0, 3, 0, 10).Cost);
    }

    [Fact]
    public void PathCacheIsBoundToWorldRevisionAndExpansionBudget()
    {
        var world = CreateFlatWorld(4, 3);
        var graph = CreateMovement(world);
        var cache = new PathfindingCache(4);
        var initial = AStarPathfinder.FindPath(graph, 0, 1, 3, 1, 100, cache);
        var cached = AStarPathfinder.FindPath(graph, 0, 1, 3, 1, 100, cache);
        Assert.Equal(initial.Path, cached.Path);
        Assert.Equal(1, cache.Statistics.Hits);

        world.SetBlockOverride(new BlockCoordinate(1, 1, 5), new BlockOverride(1, 0, false));
        var rerouted = AStarPathfinder.FindPath(graph, 0, 1, 3, 1, 100, cache);
        Assert.Equal(PathfindingStatus.Found, rerouted.Status);
        Assert.False(initial.Path.SequenceEqual(rerouted.Path));
        Assert.Equal(2, cache.Statistics.Misses);

        var limited = AStarPathfinder.FindPath(graph, 0, 1, 3, 1, 1, cache);
        Assert.Equal(PathfindingStatus.NodeBudgetExceeded, limited.Status);
        Assert.Equal(3, cache.Statistics.Misses);
    }

    [Fact]
    public void PathCacheDoesNotReuseResultsAcrossDifferentMovementRules()
    {
        var world = CreateFlatWorld(4, 1);
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(4, 2, 0, 0));
        world.SetSurfaceColumn(2, 0, new SurfaceColumn(4, 2, 0, 0));
        var ordinaryGraph = new MovementGraph(
            world,
            new MovementRules(new HashSet<ushort> { 1, 2 }, new HashSet<ushort>()));
        var pathGraph = new MovementGraph(
            world,
            new MovementRules(
                new HashSet<ushort> { 1, 2 },
                new HashSet<ushort>(),
                pathMaterials: new HashSet<ushort> { 2 },
                pathSpeedupPermille: 2000));
        var cache = new PathfindingCache(4);

        var ordinary = AStarPathfinder.FindPath(ordinaryGraph, 0, 0, 3, 0, 10, cache);
        var accelerated = AStarPathfinder.FindPath(pathGraph, 0, 0, 3, 0, 10, cache);

        Assert.Equal(3000, ordinary.Cost);
        Assert.Equal(2500, accelerated.Cost);
        Assert.Equal(2, cache.Statistics.Misses);
    }

    [Fact]
    public void RegionGraphReportsCheapestPortalsBetweenAdjacentRegions()
    {
        var world = CreateRegionedWorld(32, 32, regionSize: 8);
        var regions = RegionGraph.Build(CreateMovement(world));

        Assert.Equal(4, regions.RegionCountX);
        Assert.Equal(4, regions.RegionCountY);
        Assert.Equal(16, regions.RegionCount);

        // An interior region touches its four cardinal and four diagonal neighbours.
        var middle = regions.GetPortals(1 + 4 * 1);
        Assert.Equal(8, middle.Count);
        // Region 5 is (1,1) in a 4x4 map: cardinal neighbours are up 1, left 4, right 6, down 9.
        Assert.Equal([1, 4, 6, 9], middle
            .Where(static portal => portal.Cost == 1000)
            .Select(static portal => portal.ToRegion));
        Assert.All(middle, static portal => Assert.Contains(portal.Cost, new[] { 1000, 1414 }));
        Assert.True(regions.TryFindRegionPath(0, 15, out var path));
        Assert.Equal(0, path[0]);
        Assert.Equal(15, path[^1]);
    }

    [Fact]
    public void HierarchicalSearchKeepsTheOptimalCostOnAnOpenMapAndIsDeterministic()
    {
        var world = CreateRegionedWorld(64, 64, regionSize: 8);
        var graph = CreateMovement(world);
        var regions = RegionGraph.Build(graph);

        var plain = AStarPathfinder.FindPath(graph, 2, 3, 61, 58, 65536);
        var first = HierarchicalPathfinder.FindPath(graph, 2, 3, 61, 58, 65536, regions);
        var second = HierarchicalPathfinder.FindPath(graph, 2, 3, 61, 58, 65536, regions);

        Assert.Equal(PathfindingStatus.Found, first.Status);
        Assert.Equal(plain.Cost, first.Cost);
        Assert.Equal(first.Path, second.Path);
        Assert.Equal(first.ExpandedNodes, second.ExpandedNodes);
    }

    [Fact]
    public void HierarchicalSearchExpandsFewerNodesThroughAWallAtTheCostOfALongerCorridor()
    {
        var world = CreateRegionedWorld(64, 64, regionSize: 8);
        // A wall splits the world in two; the only passage is the gap around y = 32.
        for (var y = 0; y < 64; y++)
        {
            if (y is 32 or 33)
            {
                continue;
            }

            world.SetBlockOverride(new BlockCoordinate(32, y, 5), new BlockOverride(1, 0, false));
            world.SetBlockOverride(new BlockCoordinate(32, y, 6), new BlockOverride(1, 0, false));
        }

        var graph = CreateMovement(world);
        var regions = RegionGraph.Build(graph);

        var plain = AStarPathfinder.FindPath(graph, 1, 1, 62, 62, 65536);
        var hierarchical = HierarchicalPathfinder.FindPath(graph, 1, 1, 62, 62, 65536, regions);
        var again = HierarchicalPathfinder.FindPath(graph, 1, 1, 62, 62, 65536, regions);

        Assert.Equal(PathfindingStatus.Found, hierarchical.Status);
        Assert.Equal(hierarchical.Path, again.Path);
        // The corridor search is a performance trade-off, not an optimality claim: the region
        // corridors bound the work, and the path may be longer than the exhaustive search.
        Assert.True(hierarchical.Cost >= plain.Cost);
        Assert.True(
            hierarchical.ExpandedNodes < plain.ExpandedNodes,
            $"expected fewer expansions than {plain.ExpandedNodes}, got {hierarchical.ExpandedNodes}");
    }

    [Fact]
    public void HierarchicalSearchFallsBackToThePlainSearchWhenTheRegionPathIsMissing()
    {
        var world = CreateRegionedWorld(32, 32, regionSize: 8);
        var graph = CreateMovement(world);
        // Portals of an unrelated, fully blocked world: the corridor search cannot rely on them.
        var blocked = CreateRegionedWorld(32, 32, regionSize: 8);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                blocked.SetBlockOverride(new BlockCoordinate(x, y, 5), new BlockOverride(1, 0, false));
                blocked.SetBlockOverride(new BlockCoordinate(x, y, 6), new BlockOverride(1, 0, false));
            }
        }

        var disconnected = RegionGraph.Build(CreateMovement(blocked));
        var plain = AStarPathfinder.FindPath(graph, 0, 0, 31, 31, 65536);
        var hierarchical = HierarchicalPathfinder.FindPath(graph, 0, 0, 31, 31, 65536, disconnected);

        Assert.Equal(PathfindingStatus.Found, hierarchical.Status);
        Assert.Equal(plain.Cost, hierarchical.Cost);
    }

    private static WorldGrid CreateRegionedWorld(int width, int height, int regionSize, byte surfaceZ = 4)
    {
        var world = new WorldGrid(width, height, regionSize, verticalSize: 16);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(surfaceZ, 1, 0, 0));
            }
        }

        return world;
    }

    private static WorldGrid CreateFlatWorld(int width, int height, byte surfaceZ = 4)
    {
        var world = new WorldGrid(width, height, verticalSize: 16);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(surfaceZ, 1, 0, 0));
            }
        }

        return world;
    }

    private static MovementGraph CreateMovement(WorldGrid world) =>
        new(world, new MovementRules(new HashSet<ushort> { 1 }, new HashSet<ushort> { 2 }));
}
