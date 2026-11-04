using Prism.Content;

namespace Prism.Content.Tests;

public sealed class ConsumptionRuleRuntimeCompilerTests
{
    [Fact]
    public void CompilesEdibleItemsAndDrinkableMaterialsFromMinimalPack()
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

        var rules = ConsumptionRuleRuntimeCompiler.Compile(pack);

        var item = Assert.Single(rules.Items, rule =>
            rule.ItemTypeId == pack.Registries.Items.GetId("flatbread"));
        Assert.Equal(-300, item.Effects["hunger"]);
        var source = Assert.Single(rules.Sources, rule =>
            rule.MaterialId == pack.Registries.Materials.GetId("water"));
        Assert.Equal(-300, source.Effects["thirst"]);
        Assert.DoesNotContain(
            rules.Items,
            rule => rule.ItemTypeId == pack.Registries.Items.GetId("water_bucket"));
    }
}
