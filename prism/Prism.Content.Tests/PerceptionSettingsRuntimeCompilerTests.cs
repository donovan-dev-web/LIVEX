using System.Text.Json;
using Prism.Content;
using Prism.Core.Perception;

namespace Prism.Content.Tests;

public sealed class PerceptionSettingsRuntimeCompilerTests
{
    [Fact]
    public void MinimalPackDeclaresThePerceptionSettingsItRuns()
    {
        var compilation = new ContentPackCompiler().CompileDirectory(
            Path.Combine(FindPrismRoot(), "content", "minimal"));
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));

        var settings = PerceptionSettingsRuntimeCompiler.Compile(compilation.Pack!);

        Assert.Equal(new PerceptionSettings(16, 3, 256), settings);
    }

    [Fact]
    public void PerceptionBlockOverridesEverySettingIndependently()
    {
        var pack = WithParameters("""{ "perception": { "nearRadius": 4, "midRadiusChunks": 1, "maxObservedPerAgent": 32 } }""");

        var settings = PerceptionSettingsRuntimeCompiler.Compile(pack);

        Assert.Equal(new PerceptionSettings(4, 1, 32), settings);
    }

    [Fact]
    public void MissingOrEmptyPerceptionBlockKeepsDefaults()
    {
        var defaults = new PerceptionSettings();

        Assert.Equal(defaults, PerceptionSettingsRuntimeCompiler.Compile(WithParameters("""{ "world": { "size": [64, 64, 32] } }""")));
        Assert.Equal(defaults, PerceptionSettingsRuntimeCompiler.Compile(WithParameters("""{ "perception": {} }""")));
        Assert.Equal(defaults, PerceptionSettingsRuntimeCompiler.Compile(WithParameters("[]")));
        Assert.Equal(defaults, PerceptionSettingsRuntimeCompiler.Compile(WithParameters("null")));
    }

    [Fact]
    public void PartialPerceptionBlockOnlyOverridesTheDeclaredKeys()
    {
        var settings = PerceptionSettingsRuntimeCompiler.Compile(
            WithParameters("""{ "perception": { "nearRadius": 8 } }"""));

        Assert.Equal(new PerceptionSettings(8, 3, 256), settings);
    }

    [Fact]
    public void OutOfRangePerceptionSettingIsRejectedAtCompileTime()
    {
        var tooWide = WithParameters("""{ "perception": { "nearRadius": 64 } }""");
        var zeroBudget = WithParameters("""{ "perception": { "maxObservedPerAgent": 0 } }""");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PerceptionSettingsRuntimeCompiler.Compile(tooWide));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PerceptionSettingsRuntimeCompiler.Compile(zeroBudget));
    }

    [Fact]
    public void NonIntegerPerceptionSettingIsRejectedAtCompileTime()
    {
        var fractional = WithParameters("""{ "perception": { "nearRadius": 2.5 } }""");
        var text = WithParameters("""{ "perception": { "midRadiusChunks": "three" } }""");

        Assert.Throws<InvalidOperationException>(
            () => PerceptionSettingsRuntimeCompiler.Compile(fractional));
        Assert.Throws<InvalidOperationException>(
            () => PerceptionSettingsRuntimeCompiler.Compile(text));
    }

    private static CompiledContentPack WithParameters(string parameters)
    {
        var compilation = new ContentPackCompiler().CompileDirectory(
            Path.Combine(FindPrismRoot(), "content", "minimal"));
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        using var document = JsonDocument.Parse(parameters);
        return compilation.Pack! with { Parameters = document.RootElement.Clone() };
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
