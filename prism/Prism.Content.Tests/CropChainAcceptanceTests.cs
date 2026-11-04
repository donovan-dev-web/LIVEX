using Prism.Content;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Content.Tests;

/// <summary>
/// Acceptance chain of PRISM-095 on the shipped minimal pack: a wheat seed is planted on tilled
/// soil next to water, grows to ripeness, is harvested into wheat and fresh seeds, and those seeds
/// immediately replant the next cycle.
/// </summary>
public sealed class CropChainAcceptanceTests
{
    private const ulong AgentId = 1;
    private const ulong RunSeed = 20261104;

    [Fact]
    public void WheatSeedToHarvestToNewSeedsClosesTheCycleOnTheShippedPack()
    {
        var pack = CompileMinimal();
        ushort Material(string id) => pack.Registries.Materials.GetId(id);
        ushort Item(string id) => pack.Registries.Items.GetId(id);

        var world = new WorldGrid(6, 1, verticalSize: 16);
        for (var x = 0; x < world.Width; x++)
        {
            world.SetSurfaceColumn(x, 0, new SurfaceColumn(4, Material("dirt"), 0, 0));
        }

        // Water within the four-cell radius the pack requires for growth.
        world.SetBlockOverride(
            new BlockCoordinate(3, 0, 4),
            new BlockOverride(Material("water"), 0, IsAir: false));

        var cropRules = CropActionRuleRuntimeCompiler.Compile(pack);
        var lifecycle = new AgentLifecycleSystem(
            world,
            MovementRuleRuntimeCompiler.Compile(pack),
            PerceptionRuleRuntimeCompiler.Compile(pack));
        var agent = lifecycle.Incarnate(
            AgentId,
            AgentPosition.AtCell(1, 0, 5),
            new AgentBodyProfile(CarrySlots: 8));
        var ledger = new MatterLedger();
        var soil = new BlockCoordinate(1, 0, 4);
        var crop = new BlockCoordinate(1, 0, 5);
        var wheel = new EventWheel();

        // Tilling needs the hoe the pack declares.
        var noHoe = CropActionSystem.Till(world, agent, cropRules, ledger, soil);
        Assert.False(noHoe.Succeeded);
        Assert.Equal("tool_missing", noHoe.ErrorCode);
        Assert.True(agent.Inventory.TryAdd(Item("stone_hoe"), 1, 1, out var remaining));
        Assert.Equal(0, remaining);
        var tilled = CropActionSystem.Till(world, agent, cropRules, ledger, soil);
        Assert.True(tilled.Succeeded, tilled.ErrorCode);
        Assert.True(world.TryGetBlockOverride(soil, out var farmland));
        Assert.Equal(Material("farmland"), farmland.MaterialId);

        // Step aside so the crop cell is free, then plant the seed.
        Assert.True(lifecycle.TryMoveOneStep(AgentId, new BlockCoordinate(0, 0, 5), out var moveError), moveError);
        Assert.True(agent.Inventory.TryAdd(Item("wheat_seed"), 1, 64, out remaining));
        Assert.Equal(0, remaining);
        var planted = CropActionSystem.Plant(
            world,
            agent,
            wheel,
            cropRules,
            ledger,
            RunSeed,
            tick: 0,
            crop,
            Item("wheat_seed"),
            WorldSeason.Spring);
        Assert.True(planted.Succeeded, planted.ErrorCode);
        Assert.Equal(0, agent.Inventory.Count(Item("wheat_seed")));
        Assert.Equal(1, wheel.Count);
        Assert.True(world.TryGetBlockOverride(crop, out var seedling));
        Assert.Equal(Material("wheat_crop"), seedling.MaterialId);
        Assert.Equal(0, seedling.State);

        // Spring divides the stage delay by 1.2, so four stages ripen inside ~500 ticks.
        var growthRule = Assert.Single(cropRules.CropRules).Growth;
        var harvestTick = -1L;
        IReadOnlyList<CropGrowthEvent> growthEvents = [];
        for (var tick = 1L; tick <= 1_200; tick++)
        {
            growthEvents = CropGrowthSystem.ProcessDueEvents(
                world,
                wheel,
                RunSeed,
                tick,
                WorldSeason.Spring,
                growthRule,
                wheel.AdvanceTo(tick));
            if (growthEvents.Any(static entry => entry.Kind == CropGrowthEventKind.Ripe))
            {
                harvestTick = tick;
                break;
            }
        }

        Assert.Contains(growthEvents, static entry => entry.Kind == CropGrowthEventKind.Ripe);
        Assert.True(harvestTick > 0);
        Assert.True(world.TryGetBlockOverride(crop, out var ripe));
        Assert.Equal((byte)(growthRule.Stages - 1), ripe.State);

        var harvested = CropActionSystem.Harvest(world, agent, cropRules, ledger, RunSeed, harvestTick, crop);
        Assert.True(harvested.Succeeded, harvested.ErrorCode);
        var wheat = harvested.Produced.Single(stack => stack.ItemTypeId == Item("wheat"));
        var seeds = harvested.Produced.Single(stack => stack.ItemTypeId == Item("wheat_seed"));
        Assert.InRange(wheat.Quantity, 2, 3);
        Assert.InRange(seeds.Quantity, 1, 2);
        Assert.Equal(wheat.Quantity, agent.Inventory.Count(Item("wheat")));
        Assert.Equal(seeds.Quantity, agent.Inventory.Count(Item("wheat_seed")));
        Assert.True(world.TryGetBlockOverride(crop, out var cleared));
        Assert.True(cleared.IsAir);

        // The harvested wheat is born with the 600-tick shelf life of the pack, the seeds are not perishable.
        var freshWheat = agent.Inventory.Snapshot().Single(stack => stack.ItemTypeId == Item("wheat"));
        Assert.Equal(600 * 1_000, freshWheat.FreshnessPermilleTicks);
        Assert.Equal(harvestTick, freshWheat.FreshnessUpdatedTick);
        var freshSeeds = agent.Inventory.Snapshot().Single(stack => stack.ItemTypeId == Item("wheat_seed"));
        Assert.Null(freshSeeds.FreshnessPermilleTicks);

        // Closing the loop: a harvested seed replants the next cycle on the same farmland.
        var replanted = CropActionSystem.Plant(
            world,
            agent,
            wheel,
            cropRules,
            ledger,
            RunSeed,
            harvestTick + 1,
            crop,
            Item("wheat_seed"),
            WorldSeason.Spring);
        Assert.True(replanted.Succeeded, replanted.ErrorCode);
        Assert.Equal(2, wheel.Count);
        Assert.Equal(seeds.Quantity - 1, agent.Inventory.Count(Item("wheat_seed")));
    }

    private static CompiledContentPack CompileMinimal()
    {
        var compilation = new ContentPackCompiler().CompileDirectory(
            Path.Combine(FindPrismRoot(), "content", "minimal"));
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        return compilation.Pack!;
    }

    private static string FindPrismRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Prism.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Prism.sln from the content test output directory.");
    }
}
