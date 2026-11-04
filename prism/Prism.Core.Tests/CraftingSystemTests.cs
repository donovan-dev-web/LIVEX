using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class CraftingSystemTests
{
    [Fact]
    public void CraftConsumesInputsProducesOutputsAndReservesStation()
    {
        var (world, movement) = CreateWorld();
        var agents = new AgentRegistry();
        var agent = agents.Incarnate(
            7,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(CarrySlots: 4),
            movement);
        Assert.True(agent.Inventory.TryAdd(10, 1, 10, out var remaining));
        Assert.Equal(0, remaining);
        Assert.True(agent.Inventory.TryAdd(11, 1, 10, out remaining));
        Assert.Equal(0, remaining);
        var ledger = new MatterLedger();
        ledger.SetInitialCount(10, 1);
        ledger.SetInitialCount(11, 1);
        var stations = new CraftingStationRegistry();

        var result = CraftingSystem.Craft(
            world,
            agent,
            stations,
            CreateRules(),
            ledger,
            recipeId: 1,
            stationPosition: new BlockCoordinate(1, 0, 5),
            tick: 20,
            mastery: 100);

        Assert.True(result.Succeeded);
        Assert.Equal(18, result.DurationTicks);
        Assert.Equal(new AgentItemStack(12, 2, 10), Assert.Single(result.Produced));
        Assert.Equal(2, agent.Inventory.Count(12));
        Assert.Equal(0, agent.Inventory.Count(10));
        Assert.Equal(0, agent.Inventory.Count(11));
        Assert.True(stations.IsBusy(new BlockCoordinate(1, 0, 5), 21));
        Assert.False(stations.IsBusy(new BlockCoordinate(1, 0, 5), 38));
        Assert.Empty(ledger.Audit(new Dictionary<ushort, long> { [12] = 2 }));
    }

    [Fact]
    public void BusyStationAndMissingInputsFailWithoutChangingInventory()
    {
        var (world, movement) = CreateWorld();
        var agents = new AgentRegistry();
        var agent = agents.Incarnate(
            7,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(CarrySlots: 4),
            movement);
        Assert.True(agent.Inventory.TryAdd(10, 1, 10, out var remaining));
        Assert.Equal(0, remaining);
        Assert.True(agent.Inventory.TryAdd(11, 1, 10, out remaining));
        Assert.Equal(0, remaining);
        var stations = new CraftingStationRegistry();
        var rules = CreateRules();

        Assert.True(CraftingSystem.Craft(
            world,
            agent,
            stations,
            rules,
            new MatterLedger(),
            1,
            new BlockCoordinate(1, 0, 5),
            tick: 5,
            mastery: 0).Succeeded);

        var busy = CraftingSystem.Craft(
            world,
            agent,
            stations,
            rules,
            new MatterLedger(),
            1,
            new BlockCoordinate(1, 0, 5),
            tick: 6,
            mastery: 0);
        Assert.False(busy.Succeeded);
        Assert.Equal("station_busy", busy.ErrorCode);
        Assert.Equal(0, agent.Inventory.Count(10));

        var noIngredients = CraftingSystem.Craft(
            world,
            agent,
            stations,
            rules,
            new MatterLedger(),
            1,
            new BlockCoordinate(1, 0, 5),
            tick: 25,
            mastery: 0);
        Assert.False(noIngredients.Succeeded);
        Assert.Equal("input_missing", noIngredients.ErrorCode);
        Assert.Equal(2, agent.Inventory.Count(12));
    }

    [Fact]
    public void FailedOutputCapacityPreflightDoesNotConsumeInputs()
    {
        var (world, movement) = CreateWorld();
        var agents = new AgentRegistry();
        var agent = agents.Incarnate(
            7,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(CarrySlots: 2),
            movement);
        Assert.True(agent.Inventory.TryAdd(10, 1, 1, out var remaining));
        Assert.Equal(0, remaining);
        Assert.True(agent.Inventory.TryAdd(11, 1, 1, out remaining));
        Assert.Equal(0, remaining);
        var rules = new CraftingRules(
            [
                new CraftingRecipeRule(
                    1,
                    "multi_output",
                    [
                        new CraftingIngredientRule([10], 1),
                        new CraftingIngredientRule([11], 1),
                    ],
                    [
                        new CraftingProductRule(12, 1),
                        new CraftingProductRule(13, 1),
                        new CraftingProductRule(14, 1),
                    ],
                    [],
                    null,
                    null,
                    1,
                    0,
                    "craft"),
            ],
            [
                new ActionItemRule(10, 1, 0),
                new ActionItemRule(11, 1, 0),
                new ActionItemRule(12, 1, 0),
                new ActionItemRule(13, 1, 0),
                new ActionItemRule(14, 1, 0),
            ]);

        var result = CraftingSystem.Craft(
            world,
            agent,
            new CraftingStationRegistry(),
            rules,
            new MatterLedger(),
            1,
            null,
            tick: 0,
            mastery: 0);

        Assert.False(result.Succeeded);
        Assert.Equal("capacity_reached", result.ErrorCode);
        Assert.Equal(1, agent.Inventory.Count(10));
        Assert.Equal(1, agent.Inventory.Count(11));
        Assert.Equal(0, agent.Inventory.Count(12));
    }

    [Fact]
    public void RequiredCraftingToolIsNotConsumedAsAnIngredient()
    {
        var (world, movement) = CreateWorld();
        var agents = new AgentRegistry();
        var agent = agents.Incarnate(
            7,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(CarrySlots: 2),
            movement);
        Assert.True(agent.Inventory.TryAdd(10, 1, 1, out var remaining));
        Assert.Equal(0, remaining);
        var rules = new CraftingRules(
            [
                new CraftingRecipeRule(
                    1,
                    "tool_recipe",
                    [new CraftingIngredientRule([10], 1)],
                    [new CraftingProductRule(11, 1)],
                    [],
                    null,
                    10,
                    1,
                    0,
                    "craft"),
            ],
            [new ActionItemRule(10, 1, 0), new ActionItemRule(11, 1, 0)]);

        var result = CraftingSystem.Craft(
            world,
            agent,
            new CraftingStationRegistry(),
            rules,
            new MatterLedger(),
            1,
            null,
            tick: 0,
            mastery: 0);

        Assert.False(result.Succeeded);
        Assert.Equal("input_missing", result.ErrorCode);
        Assert.Equal(1, agent.Inventory.Count(10));
    }

    [Fact]
    public void SuccessfulCraftingWearsRequiredToolAndRecordsBreakage()
    {
        var (world, movement) = CreateWorld();
        var agent = new AgentRegistry().Incarnate(
            7,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(CarrySlots: 3),
            movement);
        Assert.True(agent.Inventory.TryAdd(10, 1, 1, out _));
        Assert.True(agent.Inventory.TryAdd(11, 1, 1, out _));
        var rules = new CraftingRules(
            [
                new CraftingRecipeRule(
                    1,
                    "tool_recipe",
                    [new CraftingIngredientRule([11], 1)],
                    [new CraftingProductRule(12, 1)],
                    [],
                    null,
                    10,
                    1,
                    0,
                    "craft"),
            ],
            [
                new ActionItemRule(10, 1, 0, Durability: 1),
                new ActionItemRule(11, 1, 0),
                new ActionItemRule(12, 1, 0),
            ]);
        var ledger = new MatterLedger();
        ledger.SetInitialCount(10, 1);
        ledger.SetInitialCount(11, 1);

        var result = CraftingSystem.Craft(
            world,
            agent,
            new CraftingStationRegistry(),
            rules,
            ledger,
            1,
            null,
            tick: 0,
            mastery: 0);

        Assert.True(result.Succeeded);
        Assert.Equal((ushort)10, result.BrokenToolItemTypeId);
        Assert.Equal(0, agent.Inventory.Count(10));
        Assert.Equal(1, agent.Inventory.Count(12));
        Assert.Equal(1, Assert.Single(ledger.EnumerateEntries(), entry => entry.ItemTypeId == 10).Destroyed);
    }

    private static CraftingRules CreateRules() =>
        new(
            [
                new CraftingRecipeRule(
                    1,
                    "craft_items",
                    [
                        new CraftingIngredientRule([10], 1),
                        new CraftingIngredientRule([11], 1),
                    ],
                    [new CraftingProductRule(12, 2)],
                    [],
                    StationMaterialId: 20,
                    ToolItemTypeId: null,
                    DurationTicks: 20,
                    MinimumMastery: 0,
                    MasteryCategory: "craft"),
            ],
            [
                new ActionItemRule(10, 10, 0),
                new ActionItemRule(11, 10, 0),
                new ActionItemRule(12, 10, 0),
            ]);

    private static (WorldGrid World, MovementGraph Movement) CreateWorld()
    {
        var world = new WorldGrid(2, 1, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(4, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(4, 1, 0, 0));
        world.SetBlockOverride(
            new BlockCoordinate(1, 0, 5),
            new BlockOverride(20, 0, IsAir: false));
        return (
            world,
            new MovementGraph(
                world,
                new MovementRules(new HashSet<ushort> { 1, 20 }, new HashSet<ushort>())));
    }
}
