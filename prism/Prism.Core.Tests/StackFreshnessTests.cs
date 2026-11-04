using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class StackFreshnessTests
{
    private static readonly BlockCoordinate Origin = new(1, 2, 3);

    [Fact]
    public void InventoryMergesOnlyStacksWithIdenticalFreshnessState()
    {
        var inventory = new AgentInventory(4);
        var fresh = new AgentItemStack(7, 3, 16, FreshnessPermilleTicks: 900, FreshnessUpdatedTick: 4);
        var older = fresh with { FreshnessPermilleTicks = 400, FreshnessUpdatedTick = 9 };
        var plain = new AgentItemStack(7, 2, 16);

        Assert.True(inventory.TryAdd(fresh));
        Assert.True(inventory.TryAdd(plain));
        Assert.True(inventory.TryAdd(older));

        var stacks = inventory.Snapshot();
        Assert.Equal(3, stacks.Count);
        Assert.Equal(900, stacks[0].FreshnessPermilleTicks);
        Assert.Equal(4, stacks[0].FreshnessUpdatedTick);
        Assert.Equal(3, stacks[0].Quantity);
        Assert.Null(stacks[1].FreshnessPermilleTicks);
        Assert.Equal(400, stacks[2].FreshnessPermilleTicks);
    }

    [Fact]
    public void PerishableStacksWithSameFreshnessStillMerge()
    {
        var inventory = new AgentInventory(2);
        var stack = new AgentItemStack(7, 5, 16, FreshnessPermilleTicks: 750, FreshnessUpdatedTick: 11);

        Assert.True(inventory.TryAdd(stack));
        Assert.True(inventory.TryAdd(stack with { Quantity = 4 }));

        var merged = Assert.Single(inventory.Snapshot());
        Assert.Equal(9, merged.Quantity);
        Assert.Equal(750, merged.FreshnessPermilleTicks);
        Assert.Equal(11, merged.FreshnessUpdatedTick);
    }

    [Fact]
    public void GroundStorePreservesFreshnessAndNeverMergesIntoStaleStacks()
    {
        var store = new GroundItemStore();
        var fresh = new AgentItemStack(7, 4, 16, FreshnessPermilleTicks: 800, FreshnessUpdatedTick: 3);

        store.AddStack(7, 10, 16, Origin);
        store.AddItem(fresh, Origin);

        var items = store.Snapshot();
        Assert.Equal(2, items.Count);
        Assert.Equal(10, items[0].Stack.Quantity);
        Assert.Null(items[0].Stack.FreshnessPermilleTicks);
        Assert.Equal(4, items[1].Stack.Quantity);
        Assert.Equal(800, items[1].Stack.FreshnessPermilleTicks);
        Assert.Equal(3, items[1].Stack.FreshnessUpdatedTick);
    }

    [Fact]
    public void GroundStoreMergesIdenticalPerishableStacksUpToTheStackLimit()
    {
        var store = new GroundItemStore();
        var fresh = new AgentItemStack(7, 6, 8, FreshnessPermilleTicks: 500, FreshnessUpdatedTick: 2);

        store.AddItem(fresh, Origin);
        store.AddItem(fresh with { Quantity = 5 }, Origin);

        var items = store.Snapshot();
        Assert.Equal(2, items.Count);
        Assert.Equal(8, items[0].Stack.Quantity);
        Assert.Equal(3, items[1].Stack.Quantity);
        Assert.Equal(11, items.Sum(item => item.Stack.Quantity));
        Assert.All(items, item => Assert.Equal(500, item.Stack.FreshnessPermilleTicks));
    }

    [Fact]
    public void GroundStoreRejectsInvalidFreshness()
    {
        var store = new GroundItemStore();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            store.AddItem(new AgentItemStack(7, 1, 8, FreshnessPermilleTicks: -1, FreshnessUpdatedTick: 2), Origin));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            store.AddItem(new AgentItemStack(7, 1, 8, FreshnessPermilleTicks: 500, FreshnessUpdatedTick: -1), Origin));
        Assert.Equal(0, store.Count);
    }
}
