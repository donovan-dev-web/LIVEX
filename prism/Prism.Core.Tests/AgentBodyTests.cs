using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class AgentBodyTests
{
    [Fact]
    public void IncarnateValidatesPositionAndCreatesConfiguredInventory()
    {
        var world = CreateFlatWorld(3, 3);
        var movement = CreateMovement(world);
        var registry = new AgentRegistry();

        var body = registry.Incarnate(
            7,
            AgentPosition.AtCell(1, 2, 5),
            new AgentBodyProfile(SpeedPermille: 1250, CarrySlots: 4),
            movement);

        Assert.Equal(1, body.Position.CellX);
        Assert.Equal(2, body.Position.CellY);
        Assert.Equal(4, body.Inventory.Capacity);
        Assert.Throws<AgentLifecycleException>(() =>
            registry.Incarnate(7, AgentPosition.AtCell(1, 2, 5), new(), movement));
        Assert.Equal("invalid_position", Assert.Throws<AgentLifecycleException>(() =>
            registry.Incarnate(8, AgentPosition.AtCell(5, 2, 5), new(), movement)).Code);
        Assert.Equal("invalid_position", Assert.Throws<AgentLifecycleException>(() =>
            registry.Incarnate(9, new AgentPosition(1000, 2000, 5999), new(), movement)).Code);
    }

    [Fact]
    public void RetiringAgentDropsEveryInventoryStackAtItsFeetPosition()
    {
        var world = CreateFlatWorld(2, 2);
        var registry = new AgentRegistry();
        var agent = registry.Incarnate(
            7,
            AgentPosition.AtCell(1, 0, 5),
            new AgentBodyProfile(CarrySlots: 3),
            CreateMovement(world));
        Assert.True(agent.Inventory.TryAdd(10, 5, 4, out var remaining));
        Assert.Equal(0, remaining);
        Assert.True(agent.Inventory.TryAdd(11, 2, 10, out remaining));
        Assert.Equal(0, remaining);
        var groundItems = new GroundItemStore();

        Assert.True(registry.Retire(7, groundItems));
        Assert.False(registry.Retire(7, groundItems));
        Assert.Empty(registry.Agents);
        Assert.Equal(
            new[]
            {
                new GroundItem(0, new BlockCoordinate(1, 0, 5), new AgentItemStack(10, 4, 4)),
                new GroundItem(1, new BlockCoordinate(1, 0, 5), new AgentItemStack(10, 1, 4)),
                new GroundItem(2, new BlockCoordinate(1, 0, 5), new AgentItemStack(11, 2, 10)),
            },
            groundItems.Snapshot());
        Assert.Empty(agent.Inventory.Snapshot());
    }

    [Fact]
    public void InventoryAddsPartiallyAndTakesAtomically()
    {
        var inventory = new AgentInventory(1);

        Assert.False(inventory.TryAdd(5, 7, 4, out var remaining));
        Assert.Equal(3, remaining);
        Assert.Equal(4, inventory.Count(5));
        Assert.False(inventory.TryTake(5, 5, out var removed));
        Assert.Empty(removed);
        Assert.Equal(4, inventory.Count(5));
        Assert.True(inventory.TryTake(5, 3, out removed));
        Assert.Equal(3, Assert.Single(removed).Quantity);
        Assert.Equal(1, inventory.Count(5));
    }

    [Fact]
    public void AgentLifecycleCheckpointPreservesAgentsInventoriesAndGroundItemIds()
    {
        var world = CreateFlatWorld(3, 3);
        var lifecycle = new AgentLifecycleSystem(
            world,
            new MovementRules([1], [2], pathMaterials: [1]));
        var agent = lifecycle.Incarnate(
            7,
            AgentPosition.AtCell(1, 2, 5),
            new AgentBodyProfile(SpeedPermille: 1250, CarrySlots: 4));
        Assert.True(agent.Inventory.TryAdd(10, 5, 4, out var remaining));
        Assert.Equal(0, remaining);

        var restored = new AgentLifecycleSystem(world, new MovementRules([], []));
        restored.RestoreState(lifecycle.CaptureState());

        var snapshot = Assert.Single(restored.SnapshotAgents());
        Assert.Equal(7UL, snapshot.AgentId);
        Assert.Equal(agent.Position, snapshot.Position);
        Assert.Equal(agent.Profile, snapshot.Profile);
        Assert.Equal(5, snapshot.Inventory.Sum(stack => stack.Quantity));
        Assert.True(restored.Retire(7));
        Assert.Equal([0UL, 1UL], restored.SnapshotGroundItems().Select(item => item.Id));

        var restoredAgain = new AgentLifecycleSystem(world, new MovementRules([], []));
        restoredAgain.RestoreState(restored.CaptureState());
        Assert.Equal(restored.Revision, restoredAgain.Revision);
        Assert.Equal(restored.SnapshotGroundItems(), restoredAgain.SnapshotGroundItems());

        var nextAgent = restoredAgain.Incarnate(8, AgentPosition.AtCell(0, 0, 5), new());
        Assert.True(nextAgent.Inventory.TryAdd(11, 1, 10, out remaining));
        Assert.Equal(0, remaining);
        Assert.True(restoredAgain.Retire(8));
        Assert.Equal(2UL, restoredAgain.SnapshotGroundItems().Last().Id);
    }

    private static WorldGrid CreateFlatWorld(int width, int height)
    {
        var world = new WorldGrid(width, height, verticalSize: 16);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, 1, 0, 0));
            }
        }

        return world;
    }

    private static MovementGraph CreateMovement(WorldGrid world) =>
        new(world, new MovementRules(new HashSet<ushort> { 1 }, new HashSet<ushort> { 2 }));
}
