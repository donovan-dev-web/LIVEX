using Prism.Content;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Content.Tests;

public sealed class CraftingRuleRuntimeCompilerTests
{
    [Fact]
    public void CompilesTagInputsAndCraftsMinimalPackWorkbenchRecipe()
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var pack = compilation.Pack!;
        var rules = CraftingRuleRuntimeCompiler.Compile(pack);
        var world = new WorldGrid(2, 1, verticalSize: 16);
        var dirtId = pack.Registries.Materials.GetId("dirt");
        var workbenchId = pack.Registries.Materials.GetId("workbench");
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(4, dirtId, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(4, dirtId, 0, 0));
        var stationPosition = new BlockCoordinate(1, 0, 5);
        world.SetBlockOverride(stationPosition, new BlockOverride(workbenchId, 0, IsAir: false));
        var movement = new MovementGraph(world, MovementRuleRuntimeCompiler.Compile(pack));
        var agents = new AgentRegistry();
        var agent = agents.Incarnate(
            1,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(CarrySlots: 4),
            movement);
        var handleId = pack.Registries.Items.GetId("handle");
        var stoneHeadId = pack.Registries.Items.GetId("stone_head");
        Assert.True(agent.Inventory.TryAdd(handleId, 1, 32, out var remaining));
        Assert.Equal(0, remaining);
        Assert.True(agent.Inventory.TryAdd(stoneHeadId, 1, 16, out remaining));
        Assert.Equal(0, remaining);
        var ledger = new MatterLedger();
        ledger.SetInitialCount(handleId, 1);
        ledger.SetInitialCount(stoneHeadId, 1);

        var result = CraftingSystem.Craft(
            world,
            agent,
            new CraftingStationRegistry(),
            rules,
            ledger,
            pack.Registries.Recipes.GetId("craft_stone_pickaxe"),
            stationPosition,
            tick: 0,
            mastery: 0);

        Assert.True(result.Succeeded);
        Assert.Equal(20, result.DurationTicks);
        Assert.Equal(
            new AgentItemStack(pack.Registries.Items.GetId("stone_pickaxe"), 1, 1),
            Assert.Single(result.Produced));
        Assert.Empty(ledger.Audit(new Dictionary<ushort, long>
        {
            [pack.Registries.Items.GetId("stone_pickaxe")] = 1,
        }));
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
