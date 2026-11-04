using System.Text.Json;
using Prism.Content;

namespace Prism.Content.Tests;

public sealed class MasteryRuleRuntimeCompilerTests
{
    [Fact]
    public void CompilesTheMinimalPackMasteryParameters()
    {
        var pack = CompileMinimalPack();

        var rules = MasteryRuleRuntimeCompiler.Compile(pack);

        Assert.Equal(5, rules.GainPerActionPermille);
        Assert.Equal(500, rules.MaximumSpeedupPermille);
    }

    [Fact]
    public void UsesDefaultsWhenTheMasteryGroupIsAbsent()
    {
        var pack = CompileMinimalPack();
        using var parameters = JsonDocument.Parse("""{"time":{"dayLengthTicks":480}}""");

        var rules = MasteryRuleRuntimeCompiler.Compile(pack with
        {
            Parameters = parameters.RootElement.Clone(),
        });

        Assert.Equal(5, rules.GainPerActionPermille);
        Assert.Equal(500, rules.MaximumSpeedupPermille);
    }

    [Fact]
    public void RejectsOutOfRangeMasteryParameters()
    {
        var pack = CompileMinimalPack();
        using var parameters = JsonDocument.Parse(
            """{"mastery":{"gainPerActionPermille":5,"maxSpeedupPermille":1001}}""");

        Assert.Throws<InvalidOperationException>(() =>
            MasteryRuleRuntimeCompiler.Compile(pack with
            {
                Parameters = parameters.RootElement.Clone(),
            }));
    }

    private static CompiledContentPack CompileMinimalPack()
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
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
