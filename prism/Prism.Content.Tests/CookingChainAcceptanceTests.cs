using Prism.Content;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Content.Tests;

/// <summary>
/// Acceptance chain of PRISM-097 on the shipped minimal pack: three wheat and one fuel bake into a
/// flatbread at the furnace, and the bake trades three perishable slots (600 ticks) for one loaf
/// that keeps four times longer and restores more energy for the same hunger.
/// </summary>
public sealed class CookingChainAcceptanceTests
{
    [Fact]
    public void BakingFlatbreadTradesThreeWheatForOneLoafThatKeepsFourTimesLonger()
    {
        var pack = CompileMinimal();
        ushort Material(string id) => pack.Registries.Materials.GetId(id);
        ushort Item(string id) => pack.Registries.Items.GetId(id);
        ushort Recipe(string id) => pack.Registries.Recipes.GetId(id);

        var world = new WorldGrid(2, 1, verticalSize: 16);
        for (var x = 0; x < world.Width; x++)
        {
            world.SetSurfaceColumn(x, 0, new SurfaceColumn(4, Material("dirt"), 0, 0));
        }

        var furnace = new BlockCoordinate(1, 0, 5);
        world.SetBlockOverride(furnace, new BlockOverride(Material("furnace"), 0, IsAir: false));

        var craftingRules = CraftingRuleRuntimeCompiler.Compile(pack);
        var lifecycle = new AgentLifecycleSystem(
            world,
            MovementRuleRuntimeCompiler.Compile(pack),
            PerceptionRuleRuntimeCompiler.Compile(pack));
        var agent = lifecycle.Incarnate(
            1,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(CarrySlots: 6));
        var ledger = new MatterLedger();
        var stations = new CraftingStationRegistry();

        // Three harvested wheat (600 ticks of shelf life each) and one plank of fuel.
        Assert.True(agent.Inventory.TryAdd(Item("wheat"), 3, 64, out var remaining, 600_000, 0));
        Assert.Equal(0, remaining);
        Assert.True(agent.Inventory.TryAdd(Item("plank"), 1, 64, out remaining));
        Assert.Equal(0, remaining);

        const long BakeTick = 10;
        var baked = CraftingSystem.Craft(
            world,
            agent,
            stations,
            craftingRules,
            ledger,
            Recipe("bake_flatbread"),
            furnace,
            BakeTick,
            mastery: 0);
        Assert.True(baked.Succeeded, baked.ErrorCode);
        Assert.Equal(0, agent.Inventory.Count(Item("wheat")));
        Assert.Equal(0, agent.Inventory.Count(Item("plank")));

        var loaf = Assert.Single(agent.Inventory.Snapshot(), stack => stack.ItemTypeId == Item("flatbread"));
        Assert.Equal(1, loaf.Quantity);
        Assert.Equal(2_400 * 1_000, loaf.FreshnessPermilleTicks);
        Assert.Equal(BakeTick, loaf.FreshnessUpdatedTick);

        // Conservation: the wheat is gone after 600 ticks, the loaf survives 2400 ticks (x4).
        var wheatStack = new AgentItemStack(Item("wheat"), 3, 64, FreshnessPermilleTicks: 600_000, FreshnessUpdatedTick: 0);
        Assert.Equal(3, wheatStack.AdvanceFreshness(600, 1_000).SpoiledQuantity);
        Assert.Null(wheatStack.AdvanceFreshness(600, 1_000).Stack);

        var halfLived = loaf.AdvanceFreshness(BakeTick + 600, 1_000);
        Assert.Equal(0, halfLived.SpoiledQuantity);
        Assert.NotNull(halfLived.Stack);
        Assert.Equal(1_800_000, halfLived.Stack!.Value.FreshnessPermilleTicks);
        Assert.Equal(BakeTick + 600, halfLived.Stack.Value.FreshnessUpdatedTick);
        Assert.Equal(0, loaf.AdvanceFreshness(BakeTick + 2_399, 1_000).SpoiledQuantity);
        Assert.Equal(1, loaf.AdvanceFreshness(BakeTick + 2_400, 1_000).SpoiledQuantity);
        Assert.Null(loaf.AdvanceFreshness(BakeTick + 2_400, 1_000).Stack);

        // Value: the same 300 hunger points, but three slots and 15 energy become one slot and 20 energy.
        var wheatEffect = pack.Effects.Single(effect => effect.Id == "eat_wheat").Values;
        var breadEffect = pack.Effects.Single(effect => effect.Id == "eat_flatbread").Values;
        Assert.Equal(-100, wheatEffect["hunger"]);
        Assert.Equal(-300, breadEffect["hunger"]);
        Assert.Equal(5, wheatEffect["energyRecovery"]);
        Assert.Equal(20, breadEffect["energyRecovery"]);
        Assert.Equal(3 * wheatEffect["hunger"], breadEffect["hunger"]);
        Assert.True(breadEffect["energyRecovery"] > 3 * wheatEffect["energyRecovery"]);
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
