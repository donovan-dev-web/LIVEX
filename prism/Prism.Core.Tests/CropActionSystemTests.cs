using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Core.Tests;

public sealed class CropActionSystemTests
{
    [Fact]
    public void TillPlantAndHarvestCompleteSeedToCropToSeedCycle()
    {
        var (world, movement) = CreateWorld();
        var agents = new AgentRegistry();
        var agent = agents.Incarnate(
            7,
            AgentPosition.AtCell(1, 0, 4),
            new AgentBodyProfile(CarrySlots: 4),
            movement);
        Assert.True(agent.Inventory.TryAdd(10, 1, 1, out var remaining));
        Assert.Equal(0, remaining);
        Assert.True(agent.Inventory.TryAdd(13, 1, 1, out remaining));
        Assert.Equal(0, remaining);
        var rules = CreateRules();
        var ledger = new MatterLedger();
        ledger.SetInitialCount(10, 1);
        var target = new BlockCoordinate(1, 0, 4);

        var tilled = CropActionSystem.Till(
            world,
            agent,
            rules,
            ledger,
            new BlockCoordinate(1, 0, 3));
        Assert.True(tilled.Succeeded);
        Assert.True(world.TryGetBlockOverride(new BlockCoordinate(1, 0, 3), out var farmland));
        Assert.Equal((ushort)11, farmland.MaterialId);

        var wheel = new EventWheel();
        var planted = CropActionSystem.Plant(
            world,
            agent,
            wheel,
            rules,
            ledger,
            runSeed: 42,
            tick: 0,
            target,
            seedItemTypeId: 10,
            WorldSeason.Spring);
        Assert.True(planted.Succeeded);
        Assert.Equal(0, agent.Inventory.Count(10));
        Assert.Equal(1, wheel.Count);

        IReadOnlyList<CropGrowthEvent> growthEvents = [];
        for (var tick = 9L; tick <= 27; tick += 9)
        {
            var dueEvents = wheel.AdvanceTo(tick);
            growthEvents = CropGrowthSystem.ProcessDueEvents(
                world,
                wheel,
                runSeed: 42,
                tick,
                WorldSeason.Spring,
                rules.CropRules.Single().Growth,
                dueEvents);
        }

        Assert.Equal(CropGrowthEventKind.Ripe, Assert.Single(growthEvents).Kind);
        Assert.True(world.TryGetBlockOverride(target, out var ripeCrop));
        Assert.Equal((byte)3, ripeCrop.State);
        var harvested = CropActionSystem.Harvest(
            world,
            agent,
            rules,
            ledger,
            runSeed: 42,
            tick: 100,
            target);

        Assert.True(harvested.Succeeded);
        Assert.Equal(new[]
        {
            new AgentItemStack(10, 2, 10),
            new AgentItemStack(12, 3, 10),
        }, harvested.Produced.OrderBy(stack => stack.ItemTypeId));
        Assert.Equal(2, agent.Inventory.Count(10));
        Assert.Equal(3, agent.Inventory.Count(12));
        Assert.True(world.TryGetBlockOverride(target, out var harvestedCrop));
        Assert.True(harvestedCrop.IsAir);
        Assert.Empty(ledger.Audit(new Dictionary<ushort, long> { [10] = 2, [12] = 3 }));
    }

    [Fact]
    public void HarvestFailsBeforeRipeOrWhenInventoryCannotFitDrops()
    {
        var (world, movement) = CreateWorld();
        var agents = new AgentRegistry();
        var agent = agents.Incarnate(
            7,
            AgentPosition.AtCell(1, 0, 4),
            new AgentBodyProfile(CarrySlots: 1),
            movement);
        var rules = CreateRules();
        var ledger = new MatterLedger();
        var target = new BlockCoordinate(1, 0, 4);
        world.SetBlockOverride(target, new BlockOverride(20, 2, IsAir: false));

        var unripe = CropActionSystem.Harvest(
            world, agent, rules, ledger, 42, 0, target);
        Assert.False(unripe.Succeeded);
        Assert.Equal("not_ripe", unripe.ErrorCode);
        Assert.True(world.TryGetBlockOverride(target, out var crop));
        Assert.False(crop.IsAir);

        world.SetBlockOverride(target, crop with { State = 3 });
        Assert.True(agent.Inventory.TryAdd(13, 1, 1, out var remaining));
        Assert.Equal(0, remaining);
        var full = CropActionSystem.Harvest(
            world, agent, rules, ledger, 42, 0, target);
        Assert.False(full.Succeeded);
        Assert.Equal("capacity_reached", full.ErrorCode);
        Assert.True(world.TryGetBlockOverride(target, out crop));
        Assert.False(crop.IsAir);
        Assert.Empty(ledger.EnumerateEntries());
    }

    [Fact]
    public void TillingRequiresAValidSoilAndHoeWithoutChangingAnythingOnFailure()
    {
        var (world, movement) = CreateWorld();
        var agents = new AgentRegistry();
        var agent = agents.Incarnate(
            7,
            AgentPosition.AtCell(1, 0, 4),
            new AgentBodyProfile(),
            movement);
        var rules = CreateRules();

        var noHoe = CropActionSystem.Till(world, agent, rules, new MatterLedger(), new BlockCoordinate(1, 0, 3));
        Assert.False(noHoe.Succeeded);
        Assert.Equal("tool_missing", noHoe.ErrorCode);
        Assert.False(world.TryGetBlockOverride(new BlockCoordinate(1, 0, 3), out _));

        world.SetBlockOverride(
            new BlockCoordinate(1, 0, 3),
            new BlockOverride(2, 0, IsAir: false));
        var unsuitable = CropActionSystem.Till(world, agent, rules, new MatterLedger(), new BlockCoordinate(1, 0, 3));
        Assert.False(unsuitable.Succeeded);
        Assert.Equal("soil_unsuitable", unsuitable.ErrorCode);
    }

    [Fact]
    public void TillingWearsAndBreaksHoeOnlyAfterSuccessfulAction()
    {
        var (world, movement) = CreateWorld();
        var agent = new AgentRegistry().Incarnate(
            7,
            AgentPosition.AtCell(1, 0, 4),
            new AgentBodyProfile(),
            movement);
        Assert.True(agent.Inventory.TryAdd(13, 1, 1, out _));
        var rules = CreateRules(hoeDurability: 1);
        var ledger = new MatterLedger();
        ledger.SetInitialCount(13, 1);
        var target = new BlockCoordinate(1, 0, 3);

        var tilled = CropActionSystem.Till(world, agent, rules, ledger, target);

        Assert.True(tilled.Succeeded);
        Assert.Equal((ushort)13, tilled.BrokenToolItemTypeId);
        Assert.Equal(0, agent.Inventory.Count(13));
        Assert.Equal(1, Assert.Single(ledger.EnumerateEntries(), entry => entry.ItemTypeId == 13).Destroyed);
    }

    private static CropActionRules CreateRules(int hoeDurability = 0) =>
        new(
            [
                new CropActionRule(
                    new CropGrowthRule(
                        CropMaterialId: 20,
                        FarmlandMaterialId: 11,
                        WaterMaterialId: 12,
                        Stages: 4,
                        StageTicks: 10,
                        JitterTicks: 0,
                        WitherAfterTicks: 30,
                        WaterRadius: 4),
                    SeedItemTypeId: 10,
                    [
                        new ActionDropRule(12, 3, 3, 1000, RequiredState: 3),
                        new ActionDropRule(10, 2, 2, 1000, RequiredState: 3),
                    ]),
            ],
            tillableMaterials: [1],
            farmlandMaterialId: 11,
            hoeItemTypeId: 13,
            [
                new ActionItemRule(10, 10, 0),
                new ActionItemRule(12, 10, 0),
                new ActionItemRule(13, 1, 0, Durability: hoeDurability),
            ]);

    private static (WorldGrid World, MovementGraph Movement) CreateWorld()
    {
        var world = new WorldGrid(3, 2, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(3, 1, 0, 0));
            }
        }

        world.SetBlockOverride(new BlockCoordinate(2, 1, 4), new BlockOverride(12, 0, false));
        var movement = new MovementGraph(
            world,
            new MovementRules(new HashSet<ushort> { 1, 11 }, new HashSet<ushort> { 12 }));
        return (world, movement);
    }
}
