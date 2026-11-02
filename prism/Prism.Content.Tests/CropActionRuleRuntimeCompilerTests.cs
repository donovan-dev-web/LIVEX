using Prism.Content;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.World;

namespace Prism.Content.Tests;

public sealed class CropActionRuleRuntimeCompilerTests
{
    [Fact]
    public void CompilesMinimalPackSeedHarvestAndTillageRules()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Prism.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var compilation = new ContentPackCompiler()
            .CompileDirectory(Path.Combine(directory!.FullName, "content", "minimal"));
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        Assert.NotNull(compilation.Pack);
        var pack = compilation.Pack!;

        var rules = CropActionRuleRuntimeCompiler.Compile(pack);

        Assert.Equal(
            pack.Registries.Items.GetId("wheat_seed"),
            rules.SeedItemTypeIds.Single());
        Assert.Contains(
            pack.Registries.Materials.GetId("grass"),
            rules.TillableMaterialIds);
        Assert.Contains(
            pack.Registries.Materials.GetId("dirt"),
            rules.TillableMaterialIds);
        Assert.Equal(
            pack.Registries.Items.GetId("stone_hoe"),
            rules.HoeItemTypeId);
    }

    [Fact]
    public void CompiledHoeDurabilityIsAppliedWhenTilling()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Prism.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var compilation = new ContentPackCompiler()
            .CompileDirectory(Path.Combine(directory!.FullName, "content", "minimal"));
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var pack = compilation.Pack!;
        var world = new WorldGrid(1, 1, verticalSize: 16);
        var dirtId = pack.Registries.Materials.GetId("dirt");
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(3, dirtId, 0, 0));
        var movement = new MovementGraph(world, MovementRuleRuntimeCompiler.Compile(pack));
        var agent = new AgentRegistry().Incarnate(
            1,
            AgentPosition.AtCell(0, 0, 4),
            new AgentBodyProfile(),
            movement);
        var hoeId = pack.Registries.Items.GetId("stone_hoe");
        Assert.True(agent.Inventory.TryAdd(hoeId, 1, 1, out _));
        var ledger = new MatterLedger();
        ledger.SetInitialCount(hoeId, 1);
        var rules = CropActionRuleRuntimeCompiler.Compile(pack);

        var result = CropActionSystem.Till(
            world,
            agent,
            rules,
            ledger,
            new BlockCoordinate(0, 0, 3));

        Assert.True(result.Succeeded);
        Assert.Equal(59, Assert.Single(agent.Inventory.Snapshot()).DurabilityRemaining);
    }
}
