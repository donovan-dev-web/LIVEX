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
