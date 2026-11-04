using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class ContainerFreshnessTests
{
    private static readonly BlockCoordinate Position = new(4, 5, 6);

    [Fact]
    public void FreshnessDecaysAtTheRequestedRateAndKeepsTheStack()
    {
        var stack = new AgentItemStack(12, 3, 16, FreshnessPermilleTicks: 5_000, FreshnessUpdatedTick: 2);

        var advanced = stack.AdvanceFreshness(4, 1_000);

        Assert.Equal(0, advanced.SpoiledQuantity);
        var remaining = Assert.IsType<AgentItemStack>(advanced.Stack);
        Assert.Equal(3, remaining.Quantity);
        Assert.Equal(3_000, remaining.FreshnessPermilleTicks);
        Assert.Equal(4, remaining.FreshnessUpdatedTick);
        Assert.Equal(stack with { FreshnessPermilleTicks = 3_000, FreshnessUpdatedTick = 4 }, remaining);
    }

    [Fact]
    public void FreshnessThatRunsOutSpoilsTheWholeStack()
    {
        var stack = new AgentItemStack(12, 7, 16, FreshnessPermilleTicks: 2_000, FreshnessUpdatedTick: 1);

        var spoiled = stack.AdvanceFreshness(3, 1_000);

        Assert.Null(spoiled.Stack);
        Assert.Equal(7, spoiled.SpoiledQuantity);
    }

    [Fact]
    public void StacksWithoutFreshnessAreNeverAged()
    {
        var plain = new AgentItemStack(12, 3, 16);
        var durable = new AgentItemStack(13, 1, 1, DurabilityRemaining: 40);

        Assert.Equal(plain, plain.AdvanceFreshness(100, 1_000).Stack);
        Assert.Equal(durable, durable.AdvanceFreshness(100, 1_000).Stack);
        Assert.Equal(0, plain.AdvanceFreshness(100, 1_000).SpoiledQuantity);
    }

    [Fact]
    public void ContainerDecaySpoilsExpiredStacksAndKeepsTheOthers()
    {
        var container = new ContainerInventory(4, 1_000, Position, AccessPrincipal.ForAgent(1));
        var ledger = new MatterLedger();
        Assert.True(container.TryAdd(
            new AgentItemStack(12, 2, 16, FreshnessPermilleTicks: 500, FreshnessUpdatedTick: 0)));
        Assert.True(container.TryAdd(
            new AgentItemStack(12, 4, 16, FreshnessPermilleTicks: 9_000, FreshnessUpdatedTick: 0)));
        Assert.True(container.TryAdd(new AgentItemStack(13, 5, 16)));

        var spoiled = container.ApplyDecay(2, ledger);

        var spoilEvent = Assert.Single(spoiled);
        Assert.Equal(12, spoilEvent.ItemTypeId);
        Assert.Equal(2, spoilEvent.Quantity);
        var remaining = container.Snapshot();
        Assert.Equal(2, remaining.Count);
        Assert.Equal(7_000, remaining[0].FreshnessPermilleTicks);
        Assert.Null(remaining[1].FreshnessPermilleTicks);
        Assert.Equal(5, remaining[1].Quantity);
    }

    [Fact]
    public void ContainersOnlyMergeStacksWithIdenticalFreshness()
    {
        var container = new ContainerInventory(4, 1_000, Position, AccessPrincipal.ForAgent(1));
        var fresh = new AgentItemStack(12, 3, 16, FreshnessPermilleTicks: 6_000, FreshnessUpdatedTick: 1);
        Assert.True(container.TryAdd(fresh));
        Assert.True(container.TryAdd(fresh with { FreshnessPermilleTicks = 2_000 }));

        var stacks = container.Snapshot();
        Assert.Equal(2, stacks.Count);
        Assert.Equal(6_000, stacks[0].FreshnessPermilleTicks);
        Assert.Equal(2_000, stacks[1].FreshnessPermilleTicks);
    }
}
