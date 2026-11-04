using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class ContainerDecayRulesTests
{
    [Fact]
    public void StorageSlowdownScalesTheBaseRate()
    {
        var rules = new ContainerDecayRules(BasePermillePerTick: 1_000, StorageSlowdownPermille: 500);

        Assert.Equal(1_000, rules.EffectivePermillePerTick(insideStorage: false));
        Assert.Equal(500, rules.EffectivePermillePerTick(insideStorage: true));

        var disabled = new ContainerDecayRules(BasePermillePerTick: 800, StorageSlowdownPermille: 0);
        Assert.Equal(800, disabled.EffectivePermillePerTick(insideStorage: true));

        var full = new ContainerDecayRules(BasePermillePerTick: 800, StorageSlowdownPermille: 1_000);
        Assert.Equal(0, full.EffectivePermillePerTick(insideStorage: true));
    }

    [Fact]
    public void OutOfRangeRatesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ContainerDecayRules(BasePermillePerTick: 10_001).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ContainerDecayRules(StorageSlowdownPermille: 1_001).Validate());
    }

    [Fact]
    public void ContainerDecayAcceptsAnExplicitRateThatOverridesTheContainerBase()
    {
        var ledger = new MatterLedger();
        var container = new ContainerInventory(4, 1_000, new BlockCoordinate(1, 1, 5), AccessPrincipal.ForAgent(1));
        Assert.True(container.TryAdd(Stack(10, freshness: 1_000)));

        var spoiled = container.ApplyDecay(1, ledger, decayPermillePerTick: 250);

        Assert.Empty(spoiled);
        var remaining = Assert.Single(container.Snapshot());
        Assert.Equal(750, remaining.FreshnessPermilleTicks);
        Assert.Equal(1, remaining.FreshnessUpdatedTick);
    }

    private static AgentItemStack Stack(ushort itemTypeId, int freshness) =>
        new(itemTypeId, 1, 8, DurabilityRemaining: null, FreshnessPermilleTicks: freshness, FreshnessUpdatedTick: 0);
}
