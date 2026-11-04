using System.Text.Json;
using Prism.Content;
using Prism.Core.Actions;

namespace Prism.Content.Tests;

public sealed class ContainerDecayRuntimeCompilerTests
{
    [Fact]
    public void MinimalPackDeclaresTheGranarySlowdownItRuns()
    {
        var pack = CompileMinimal();

        var rules = ContainerDecayRuntimeCompiler.Compile(pack);

        Assert.Equal(1_000, rules.BasePermillePerTick);
        Assert.Equal(500, rules.StorageSlowdownPermille);
        Assert.Equal(500, rules.EffectivePermillePerTick(insideStorage: true));
        Assert.Equal(1_000, rules.EffectivePermillePerTick(insideStorage: false));
    }

    [Fact]
    public void MissingSectionsKeepTheDefaults()
    {
        Assert.Equal(
            new ContainerDecayRules(),
            ContainerDecayRuntimeCompiler.Compile(WithParameters("""{ "world": { "size": [64, 64, 32] } }""")));
        Assert.Equal(
            new ContainerDecayRules(),
            ContainerDecayRuntimeCompiler.Compile(WithParameters("[]")));
    }

    [Fact]
    public void DeclaredRatesAreCompiledIndependently()
    {
        var rules = ContainerDecayRuntimeCompiler.Compile(
            WithParameters("""{ "container": { "decayPermillePerTick": 400 }, "granary": { "decaySlowdownPermille": 750 } }"""));

        Assert.Equal(400, rules.BasePermillePerTick);
        Assert.Equal(750, rules.StorageSlowdownPermille);
        Assert.Equal(100, rules.EffectivePermillePerTick(insideStorage: true));
    }

    [Fact]
    public void OutOfRangeOrNonIntegerRatesAreRejectedAtCompileTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ContainerDecayRuntimeCompiler.Compile(
                WithParameters("""{ "container": { "decayPermillePerTick": 10001 } }""")));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ContainerDecayRuntimeCompiler.Compile(
                WithParameters("""{ "granary": { "decaySlowdownPermille": 1001 } }""")));
        Assert.Throws<InvalidOperationException>(() =>
            ContainerDecayRuntimeCompiler.Compile(
                WithParameters("""{ "container": { "decayPermillePerTick": 2.5 } }""")));
    }

    private static CompiledContentPack CompileMinimal()
    {
        var compilation = new ContentPackCompiler().CompileDirectory(
            Path.Combine(FindPrismRoot(), "content", "minimal"));
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        return compilation.Pack!;
    }

    private static CompiledContentPack WithParameters(string parameters)
    {
        using var document = JsonDocument.Parse(parameters);
        return CompileMinimal() with { Parameters = document.RootElement.Clone() };
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
