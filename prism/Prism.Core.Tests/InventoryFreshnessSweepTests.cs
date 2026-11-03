using Prism.Core.Accounting;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class InventoryFreshnessSweepTests
{
    [Fact]
    public void SweepSpoilsExpiredStacksAndAccountsThem()
    {
        var inventory = new AgentInventory(4);
        var ledger = new MatterLedger();
        Assert.True(inventory.TryAdd(
            new AgentItemStack(12, 3, 16, FreshnessPermilleTicks: 1_000, FreshnessUpdatedTick: 0)));
        Assert.True(inventory.TryAdd(
            new AgentItemStack(12, 2, 16, FreshnessPermilleTicks: 9_000, FreshnessUpdatedTick: 0)));
        Assert.True(inventory.TryAdd(new AgentItemStack(13, 4, 16)));

        var spoiled = inventory.ApplyDecay(2, 1_000, ledger);

        var spoilEvent = Assert.Single(spoiled);
        Assert.Equal(12, spoilEvent.ItemTypeId);
        Assert.Equal(3, spoilEvent.Quantity);
        Assert.Equal(2, inventory.UsedSlots);
        var remaining = inventory.Snapshot();
        Assert.Equal(7_000, remaining[0].FreshnessPermilleTicks);
        Assert.Null(remaining[1].FreshnessPermilleTicks);
        Assert.Equal(4, remaining[1].Quantity);
        var destroyed = ledger.EnumerateEntries().Single(entry => entry.RuleId == "spoil");
        Assert.Equal(3, destroyed.Destroyed);
    }

    [Fact]
    public void SweepDoesNothingWithoutPerishableStacks()
    {
        var inventory = new AgentInventory(2);
        var ledger = new MatterLedger();
        Assert.True(inventory.TryAdd(new AgentItemStack(13, 1, 1, DurabilityRemaining: 12)));

        var spoiled = inventory.ApplyDecay(500, 1_000, ledger);

        Assert.Empty(spoiled);
        Assert.True(ledger.IsEmpty);
        Assert.Equal(12, Assert.Single(inventory.Snapshot()).DurabilityRemaining);
    }

    [Fact]
    public void SweepRejectsATickBeforeTheLastFreshnessUpdate()
    {
        var inventory = new AgentInventory(1);
        Assert.True(inventory.TryAdd(
            new AgentItemStack(12, 1, 16, FreshnessPermilleTicks: 500, FreshnessUpdatedTick: 10)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            inventory.ApplyDecay(9, 1_000, new MatterLedger()));
    }
}
