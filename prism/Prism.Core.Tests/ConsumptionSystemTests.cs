using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class ConsumptionSystemTests
{
    [Fact]
    public void ConsumingItemRemovesOneAndReturnsConfiguredEffects()
    {
        var (_, agent) = CreateAgent();
        Assert.True(agent.Inventory.TryAdd(10, 2, 10, out var remaining));
        Assert.Equal(0, remaining);
        var ledger = new MatterLedger();
        ledger.SetInitialCount(10, 2);
        var effects = new Dictionary<string, int> { ["hunger"] = -300 };
        var rules = new ConsumptionRules(
            [new ConsumableItemRule(10, effects)],
            []);
        effects["hunger"] = 0;

        var result = ConsumptionSystem.ConsumeItem(agent, rules, ledger, 10);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.DurationTicks);
        Assert.Equal(1, agent.Inventory.Count(10));
        Assert.Equal("item", result.Event!.Source);
        Assert.Equal((ushort)10, result.Event.ItemTypeId);
        Assert.Equal(-300, result.Event.Effects["hunger"]);
        Assert.Empty(ledger.Audit(new Dictionary<ushort, long> { [10] = 1 }));
    }

    [Fact]
    public void ConsumptionFailuresDoNotChangeInventoryOrLedger()
    {
        var (_, agent) = CreateAgent();
        Assert.True(agent.Inventory.TryAdd(10, 1, 10, out _));
        var ledger = new MatterLedger();
        var rules = new ConsumptionRules(
            [
                new ConsumableItemRule(10, new Dictionary<string, int> { ["hunger"] = -300 }),
                new ConsumableItemRule(11, new Dictionary<string, int> { ["hunger"] = -300 }),
            ],
            []);

        var missing = ConsumptionSystem.ConsumeItem(agent, rules, ledger, 11);
        var notEdible = ConsumptionSystem.ConsumeItem(agent, rules, ledger, 12);

        Assert.Equal("item_missing", missing.ErrorCode);
        Assert.Equal("not_edible", notEdible.ErrorCode);
        Assert.Equal(1, agent.Inventory.Count(10));
        Assert.Empty(ledger.EnumerateEntries());
    }

    [Fact]
    public void DrinkingFromAdjacentSourceEmitsEffectsWithoutConsumingTheSource()
    {
        var (world, agent) = CreateAgent();
        var source = new BlockCoordinate(0, 0, 6);
        world.SetBlockOverride(source, new BlockOverride(2, 0, IsAir: false));
        var rules = new ConsumptionRules(
            [],
            [new DrinkSourceRule(2, new Dictionary<string, int> { ["thirst"] = -300 })]);

        var result = ConsumptionSystem.DrinkFromSource(world, agent, rules, source);

        Assert.True(result.Succeeded);
        Assert.Equal("source", result.Event!.Source);
        Assert.Equal((ushort)2, result.Event.MaterialId);
        Assert.Equal(-300, result.Event.Effects["thirst"]);
        Assert.True(world.TryGetBlockOverride(source, out var unchangedSource));
        Assert.Equal((ushort)2, unchangedSource.MaterialId);
    }

    [Fact]
    public void DrinkingRejectsUnperceivedDistantAndNonDrinkableTargets()
    {
        var (world, agent) = CreateAgent();
        var source = new BlockCoordinate(0, 0, 6);
        world.SetBlockOverride(source, new BlockOverride(3, 0, IsAir: false));
        var rules = new ConsumptionRules(
            [],
            [new DrinkSourceRule(2, new Dictionary<string, int> { ["thirst"] = -300 })]);

        Assert.Equal(
            "not_perceived",
            ConsumptionSystem.DrinkFromSource(world, agent, rules, source, targetPerceived: false).ErrorCode);
        Assert.Equal(
            "not_adjacent",
            ConsumptionSystem.DrinkFromSource(
                world, agent, rules, new BlockCoordinate(1, 0, 6)).ErrorCode);
        Assert.Equal(
            "not_drinkable",
            ConsumptionSystem.DrinkFromSource(world, agent, rules, source).ErrorCode);
        Assert.Equal((ushort)3, Assert.Single(world.EnumerateOverrides()).Value.MaterialId);
    }

    private static (WorldGrid World, AgentBody Agent) CreateAgent()
    {
        var world = new WorldGrid(2, 1, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(4, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(4, 1, 0, 0));
        var movement = new MovementGraph(
            world,
            new MovementRules([1], [2]));
        var agent = new AgentRegistry().Incarnate(
            7,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(),
            movement);
        return (world, agent);
    }
}
