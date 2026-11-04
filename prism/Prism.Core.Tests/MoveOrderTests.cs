using Prism.Core.Navigation;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class MoveOrderTests
{
    [Fact]
    public void MoveOrderAdvancesOneStepPerTickAndCompletes()
    {
        var world = CreateFlatWorld(4, 1);
        var lifecycle = CreateLifecycle(world);
        lifecycle.Incarnate(7, AgentPosition.AtCell(0, 0, 5), new AgentBodyProfile());

        Assert.True(lifecycle.StartMoveOrder(
            7,
            "move-1",
            new BlockCoordinate(2, 0, 5),
            out var error,
            out var steps));
        Assert.Null(error);
        Assert.Equal(2, steps);
        Assert.Equal(1, lifecycle.MoveOrderCount);

        Assert.Empty(lifecycle.AdvanceMoveOrders());
        Assert.Equal(1, Assert.Single(lifecycle.SnapshotAgents()).Position.CellX);
        var completed = Assert.Single(lifecycle.AdvanceMoveOrders());
        Assert.True(completed.Completed);
        Assert.Equal("move-1", completed.CommandId);
        Assert.Equal(0, completed.StepsRemaining);
        Assert.Equal(2, Assert.Single(lifecycle.SnapshotAgents()).Position.CellX);
        Assert.Equal(0, lifecycle.MoveOrderCount);
    }

    [Fact]
    public void MoveOrderToTheCurrentCellCompletesWithoutMoving()
    {
        var world = CreateFlatWorld(2, 2);
        var lifecycle = CreateLifecycle(world);
        lifecycle.Incarnate(3, AgentPosition.AtCell(1, 1, 5), new AgentBodyProfile());

        Assert.True(lifecycle.StartMoveOrder(
            3,
            "stay",
            new BlockCoordinate(1, 1, 5),
            out _,
            out var steps));
        Assert.Equal(0, steps);
        var status = Assert.Single(lifecycle.AdvanceMoveOrders());
        Assert.True(status.Completed);
        Assert.Equal(1, Assert.Single(lifecycle.SnapshotAgents()).Position.CellX);
    }

    [Fact]
    public void MoveOrderRejectsUnknownAgentsAndUnreachableTargets()
    {
        var world = CreateFlatWorld(3, 1);
        world.SetSurfaceColumn(2, 0, new SurfaceColumn(4, 2, 0, 0));
        var lifecycle = CreateLifecycle(world);
        lifecycle.Incarnate(7, AgentPosition.AtCell(0, 0, 5), new AgentBodyProfile());

        Assert.False(lifecycle.StartMoveOrder(
            8,
            "missing",
            new BlockCoordinate(1, 0, 5),
            out var agentError,
            out _));
        Assert.Equal("agent_not_found", agentError);

        Assert.False(lifecycle.StartMoveOrder(
            7,
            "liquid",
            new BlockCoordinate(2, 0, 5),
            out var pathError,
            out _));
        Assert.Equal("path_not_found", pathError);
        Assert.Equal(0, lifecycle.MoveOrderCount);

        Assert.False(lifecycle.StartMoveOrder(
            7,
            "outside",
            new BlockCoordinate(9, 9, 5),
            out var boundsError,
            out _));
        Assert.Equal("target_out_of_bounds", boundsError);
    }

    [Fact]
    public void MoveOrdersSurviveCheckpointAndRestore()
    {
        var world = CreateFlatWorld(6, 1);
        var lifecycle = CreateLifecycle(world);
        lifecycle.Incarnate(7, AgentPosition.AtCell(0, 0, 5), new AgentBodyProfile());
        Assert.True(lifecycle.StartMoveOrder(
            7,
            "move-restored",
            new BlockCoordinate(5, 0, 5),
            out _,
            out _));
        Assert.Empty(lifecycle.AdvanceMoveOrders());
        Assert.Empty(lifecycle.AdvanceMoveOrders());

        var checkpoint = lifecycle.CaptureState();
        var restored = CreateLifecycle(world);
        restored.RestoreState(checkpoint);

        Assert.Equal(1, restored.MoveOrderCount);
        Assert.Equal(2, Assert.Single(restored.SnapshotAgents()).Position.CellX);
        Assert.Empty(restored.AdvanceMoveOrders());
        Assert.Empty(restored.AdvanceMoveOrders());
        var completed = Assert.Single(restored.AdvanceMoveOrders());
        Assert.True(completed.Completed);
        Assert.Equal("move-restored", completed.CommandId);
        Assert.Equal(5, Assert.Single(restored.SnapshotAgents()).Position.CellX);
    }

    [Fact]
    public void GroundItemsMergeOnlyWhenItemSizeAndDurabilityAllowIt()
    {
        var store = new GroundItemStore();
        var position = new BlockCoordinate(1, 1, 5);

        store.AddStack(10, 3, 8, position);
        store.AddStack(10, 2, 8, position);

        var merged = Assert.Single(store.Snapshot());
        Assert.Equal(0UL, merged.Id);
        Assert.Equal(5, merged.Stack.Quantity);

        store.AddStack(11, 1, 8, position);
        Assert.Equal(2, store.Count);

        store.AddStack(10, 4, 8, position);
        Assert.Equal(3, store.Count);
        Assert.Equal(
            new[] { 8, 1 },
            store.Snapshot()
                .Where(item => item.Stack.ItemTypeId == 10)
                .Select(item => item.Stack.Quantity)
                .ToArray());
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

    private static AgentLifecycleSystem CreateLifecycle(WorldGrid world) =>
        new(world, new MovementRules(new HashSet<ushort> { 1 }, new HashSet<ushort> { 2 }));
}
