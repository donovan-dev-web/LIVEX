using Prism.Content;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Content.Tests;

/// <summary>
/// Acceptance chain of PRISM-094: bare hands reach the stone pickaxe, the stone pickaxe unlocks
/// iron ore, and the furnace turns the ore into the ingots of the iron pickaxe. The test uses the
/// shipped minimal pack, so it fails as soon as a recipe, a drop, a station or a tool tier drifts.
/// </summary>
public sealed class ToolChainAcceptanceTests
{
    private const ulong AgentId = 1;
    private const ulong RunSeed = 20261104;

    [Fact]
    public void BareHandsToIronPickaxeBuildsTheWholeToolChain()
    {
        var pack = CompileMinimal();
        ushort Material(string id) => pack.Registries.Materials.GetId(id);
        ushort Item(string id) => pack.Registries.Items.GetId(id);
        ushort Recipe(string id) => pack.Registries.Recipes.GetId(id);

        var world = new WorldGrid(20, 2, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(4, Material("dirt"), 0, 0));
            }
        }

        var corridor = new (int X, string Material)[]
        {
            (1, "log"), (2, "log"), (3, "log"),
            (4, "stone"), (5, "stone"), (6, "stone"), (7, "stone"), (8, "stone"),
            (9, "stone"), (10, "stone"), (11, "stone"), (12, "stone"), (13, "stone"),
            (14, "iron_ore"), (15, "iron_ore"), (16, "iron_ore"),
        };
        foreach (var (x, materialId) in corridor)
        {
            world.SetBlockOverride(
                new BlockCoordinate(x, 0, 5),
                new BlockOverride(Material(materialId), 0, IsAir: false));
        }

        var blockRules = BlockActionRuleRuntimeCompiler.Compile(pack);
        var craftingRules = CraftingRuleRuntimeCompiler.Compile(pack);
        var lifecycle = new AgentLifecycleSystem(
            world,
            MovementRuleRuntimeCompiler.Compile(pack),
            PerceptionRuleRuntimeCompiler.Compile(pack));
        var agent = lifecycle.Incarnate(
            AgentId,
            AgentPosition.AtCell(0, 0, 5),
            new AgentBodyProfile(CarrySlots: 12));
        var ledger = new MatterLedger();
        var stations = new CraftingStationRegistry();
        var tick = 0L;

        void Mine(int x)
        {
            lifecycle.ObserveAgents();
            var result = lifecycle.MineBlock(
                AgentId,
                blockRules,
                ledger,
                RunSeed,
                tick++,
                new BlockCoordinate(x, 0, 5));
            Assert.True(result.Succeeded, $"Mining the block at x={x} failed: {result.ErrorCode}");
        }

        void WalkTo(int x)
        {
            while (agent.Position.CellX != x)
            {
                Assert.True(
                    lifecycle.TryMoveOneStep(AgentId, new BlockCoordinate(x, 0, 5), out var error),
                    $"Walking to x={x} failed: {error}");
            }
        }

        CraftingActionResult Craft(string recipeId, BlockCoordinate? station = null)
        {
            tick += 50;
            return CraftingSystem.Craft(
                world,
                agent,
                stations,
                craftingRules,
                ledger,
                Recipe(recipeId),
                station,
                tick,
                mastery: 0);
        }

        // Bare hands: logs and stone first, one swing per corridor cell.
        for (var x = 1; x <= 3; x++)
        {
            Mine(x);
            WalkTo(x);
        }

        for (var x = 4; x <= 13; x++)
        {
            Mine(x);
            WalkTo(x);
        }

        // Without a pickaxe, iron ore refuses the swing and stays in place.
        var ore = new BlockCoordinate(14, 0, 5);
        lifecycle.ObserveAgents();
        var bareHands = lifecycle.MineBlock(AgentId, blockRules, ledger, RunSeed, tick++, ore);
        Assert.False(bareHands.Succeeded);
        Assert.Equal("tool_tier_insufficient", bareHands.ErrorCode);
        Assert.True(world.TryGetBlockOverride(ore, out var stillOre));
        Assert.Equal(Material("iron_ore"), stillOre.MaterialId);

        // Workbench, then the stone pickaxe that unlocks the ore.
        Assert.True(Craft("make_plank").Succeeded);
        Assert.True(Craft("make_plank").Succeeded);
        Assert.True(Craft("make_plank").Succeeded);
        Assert.True(Craft("make_handle").Succeeded);
        Assert.True(Craft("make_stone_head").Succeeded);
        Assert.True(Craft("craft_workbench").Succeeded);
        var workbench = new BlockCoordinate(12, 0, 5);
        Assert.True(lifecycle.PlaceBlock(
            AgentId,
            blockRules,
            ledger,
            workbench,
            Item("workbench"),
            Material("workbench")).Succeeded);
        Assert.True(Craft("craft_stone_pickaxe", workbench).Succeeded);
        Assert.Equal(1, agent.Inventory.Count(Item("stone_pickaxe")));

        // Stone pickaxe: mine the three iron ore blocks and walk through the corridor.
        for (var x = 14; x <= 16; x++)
        {
            Mine(x);
            WalkTo(x);
        }

        Assert.Equal(3, agent.Inventory.Count(Item("iron_ore_chunk")));
        var stonePickaxe = Assert.Single(
            agent.Inventory.Snapshot(),
            stack => stack.ItemTypeId == Item("stone_pickaxe"));
        Assert.Equal(57, stonePickaxe.DurabilityRemaining);

        // Back to the workbench, then furnace and three smelts on the way.
        WalkTo(13);
        Assert.True(Craft("craft_furnace").Succeeded);
        var furnace = new BlockCoordinate(13, 1, 5);
        Assert.True(lifecycle.PlaceBlock(
            AgentId,
            blockRules,
            ledger,
            furnace,
            Item("furnace"),
            Material("furnace")).Succeeded);
        for (var smelt = 0; smelt < 3; smelt++)
        {
            Assert.True(Craft("smelt_iron", furnace).Succeeded, "The furnace refused a smelt.");
        }

        Assert.Equal(3, agent.Inventory.Count(Item("iron_ingot")));
        Assert.Equal(0, agent.Inventory.Count(Item("iron_ore_chunk")));

        // A second handle and the ingots: the iron pickaxe closes the chain.
        Assert.True(Craft("make_handle").Succeeded);
        Assert.True(Craft("craft_iron_pickaxe", workbench).Succeeded);

        Assert.Equal(1, agent.Inventory.Count(Item("iron_pickaxe")));
        Assert.Equal(1, agent.Inventory.Count(Item("plank")));
        Assert.True(world.TryGetBlockOverride(workbench, out var placedWorkbench));
        Assert.Equal(Material("workbench"), placedWorkbench.MaterialId);
        Assert.True(world.TryGetBlockOverride(furnace, out var placedFurnace));
        Assert.Equal(Material("furnace"), placedFurnace.MaterialId);
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
