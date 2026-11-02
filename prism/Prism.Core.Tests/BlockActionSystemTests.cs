using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class BlockActionSystemTests
{
    [Fact]
    public void MiningCreatesDeterministicDropsAndPreservesItemLedger()
    {
        var (world, movement, registry) = CreateWorld();
        var agent = Incarnate(registry, movement, 7, 0);
        var rules = CreateRules(quantity: 3);
        var ledger = new MatterLedger();
        var groundItems = new GroundItemStore();

        var result = BlockActionSystem.Mine(
            world,
            agent,
            groundItems,
            rules,
            ledger,
            runSeed: 42,
            tick: 12,
            new BlockCoordinate(0, 0, 4),
            targetPerceived: true);

        Assert.True(result.Succeeded);
        Assert.Equal(20, result.DurationTicks);
        Assert.Equal(new AgentItemStack(10, 3, 10), Assert.Single(result.Produced));
        Assert.Equal(3, agent.Inventory.Count(10));
        Assert.Empty(groundItems.Snapshot());
        Assert.True(world.TryGetBlockOverride(new BlockCoordinate(0, 0, 4), out var mined));
        Assert.True(mined.IsAir);

        var placed = BlockActionSystem.Place(
            world,
            agent,
            rules,
            ledger,
            new BlockCoordinate(1, 0, 5),
            itemTypeId: 10,
            materialId: 1,
            registry,
            targetPerceived: true);

        Assert.True(placed.Succeeded);
        Assert.Equal(2, agent.Inventory.Count(10));
        Assert.True(world.TryGetBlockOverride(new BlockCoordinate(1, 0, 5), out var block));
        Assert.Equal((ushort)1, block.MaterialId);
        Assert.Empty(ledger.Audit(new Dictionary<ushort, long> { [10] = 2 }));
    }

    [Fact]
    public void MiningDropsExcessItemsOnGroundWhenInventoryIsFull()
    {
        var (world, movement, registry) = CreateWorld();
        var agent = Incarnate(registry, movement, 7, 0, carrySlots: 1);
        Assert.True(agent.Inventory.TryAdd(99, 1, 1, out var remaining));
        Assert.Equal(0, remaining);
        var groundItems = new GroundItemStore();
        var ledger = new MatterLedger();

        var result = BlockActionSystem.Mine(
            world,
            agent,
            groundItems,
            CreateRules(quantity: 5, maxStackSize: 2),
            ledger,
            runSeed: 42,
            tick: 12,
            new BlockCoordinate(0, 0, 4),
            targetPerceived: true);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { 2, 2, 1 }, groundItems.Snapshot().Select(item => item.Stack.Quantity));
        Assert.Equal(5, groundItems.Snapshot().Sum(item => item.Stack.Quantity));
        Assert.Equal(5, ledger.EnumerateEntries().Single().Created);
    }

    [Fact]
    public void PlacingFailsWithoutConsumingItemWhenAnotherAgentOccupiesCell()
    {
        var (world, movement, registry) = CreateWorld();
        var agent = Incarnate(registry, movement, 7, 0);
        var occupant = Incarnate(registry, movement, 8, 1);
        Assert.True(agent.Inventory.TryAdd(10, 1, 10, out var remaining));
        Assert.Equal(0, remaining);

        var result = BlockActionSystem.Place(
            world,
            agent,
            CreateRules(),
            new MatterLedger(),
            new BlockCoordinate(1, 0, occupant.Position.FeetZ),
            itemTypeId: 10,
            materialId: 1,
            registry,
            targetPerceived: true);

        Assert.False(result.Succeeded);
        Assert.Equal("agent_in_cell", result.ErrorCode);
        Assert.Equal(1, agent.Inventory.Count(10));
        Assert.False(world.TryGetBlockOverride(new BlockCoordinate(1, 0, 5), out _));
    }

    [Fact]
    public void MiningRejectsOutOfWorldTargetWithoutThrowing()
    {
        var (world, movement, registry) = CreateWorld();
        var agent = Incarnate(registry, movement, 7, 0);

        var result = BlockActionSystem.Mine(
            world,
            agent,
            new GroundItemStore(),
            CreateRules(),
            new MatterLedger(),
            runSeed: 42,
            tick: 0,
            new BlockCoordinate(0, 0, world.VerticalSize),
            targetPerceived: true);

        Assert.False(result.Succeeded);
        Assert.Equal("target_out_of_world", result.ErrorCode);
    }

    [Fact]
    public void MiningWearsToolAndDestroysItAtZeroDurability()
    {
        var (world, movement, registry) = CreateWorld();
        var agent = Incarnate(registry, movement, 7, 0);
        Assert.True(agent.Inventory.TryAdd(20, 1, 1, out var remaining));
        Assert.Equal(0, remaining);
        var rules = new BlockActionRules(
            [
                new MiningMaterialRule(
                    1,
                    Mineable: true,
                    Hardness: 1,
                    MinimumToolTier: 1,
                    [new ActionDropRule(10, 1, 1, 1000, null)]),
            ],
            [
                new ActionItemRule(10, 10, ToolTier: 0),
                new ActionItemRule(20, 1, ToolTier: 1, Durability: 3, WearPerAction: 2),
            ]);
        var ledger = new MatterLedger();
        ledger.SetInitialCount(20, 1);
        var target = new BlockCoordinate(0, 0, 4);

        var first = BlockActionSystem.Mine(
            world, agent, new GroundItemStore(), rules, ledger, 42, 0, target, targetPerceived: true);

        Assert.True(first.Succeeded);
        Assert.Null(first.BrokenToolItemTypeId);
        Assert.Equal(1, Assert.Single(agent.Inventory.Snapshot(), stack => stack.ItemTypeId == 20).DurabilityRemaining);

        world.SetBlockOverride(target, new BlockOverride(1, 0, IsAir: false));
        var second = BlockActionSystem.Mine(
            world, agent, new GroundItemStore(), rules, ledger, 42, 1, target, targetPerceived: true);

        Assert.True(second.Succeeded);
        Assert.Equal((ushort)20, second.BrokenToolItemTypeId);
        Assert.Equal(0, agent.Inventory.Count(20));
        Assert.Equal(
            1,
            Assert.Single(ledger.EnumerateEntries(), entry => entry.ItemTypeId == 20).Destroyed);

        world.SetBlockOverride(target, new BlockOverride(1, 0, IsAir: false));
        var noTool = BlockActionSystem.Mine(
            world, agent, new GroundItemStore(), rules, ledger, 42, 2, target, targetPerceived: true);
        Assert.Equal("tool_tier_insufficient", noTool.ErrorCode);
    }

    private static BlockActionRules CreateRules(int quantity = 3, int maxStackSize = 10) =>
        new(
            [
                new MiningMaterialRule(
                    1,
                    Mineable: true,
                    Hardness: 2,
                    MinimumToolTier: 0,
                    [new ActionDropRule(10, quantity, quantity, ChancePermille: 1000, RequiredState: null)],
                    Placeable: true),
            ],
            [new ActionItemRule(10, maxStackSize, ToolTier: 0, PlaceMaterialId: 1)]);

    private static AgentBody Incarnate(
        AgentRegistry registry,
        MovementGraph movement,
        ulong agentId,
        int x,
        int carrySlots = 4) =>
        registry.Incarnate(
            agentId,
            AgentPosition.AtCell(x, 0, 5),
            new AgentBodyProfile(CarrySlots: carrySlots),
            movement);

    private static (WorldGrid World, MovementGraph Movement, AgentRegistry Registry) CreateWorld()
    {
        var world = new WorldGrid(2, 1, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(4, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(4, 1, 0, 0));
        var movement = new MovementGraph(
            world,
            new MovementRules(new HashSet<ushort> { 1 }, new HashSet<ushort>()));
        return (world, movement, new AgentRegistry());
    }
}
