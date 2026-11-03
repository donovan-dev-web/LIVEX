using Prism.Content;
using Prism.Core.World;

namespace Prism.Content.Tests;

public sealed class WorldGenerationConfigCompilerTests
{
    [Fact]
    public void CompilesMinimalPackWorldParametersAndTreeTemplates()
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));

        var config = WorldGenerationConfigCompiler.Compile(compilation.Pack!);

        Assert.Equal((256, 256, 64, 16), (config.Width, config.Height, config.VerticalSize, config.RegionSize));
        Assert.Equal(compilation.Pack!.ContentHash, config.ContentHash);
        Assert.Contains(config.TreeTemplates, template => template.Id == "tree_oak_small");
        Assert.Contains(config.OreVeins, vein => vein.Id == "iron_ore");
        Assert.NotNull(config.SpawnResources);
        Assert.NotNull(config.SpawnConfig);
        Assert.Equal(compilation.Pack.Registries.Materials.GetId("grass"), config.Materials.Grass);
    }

    [Fact]
    public void RejectsUnsupportedWorldDimensions()
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        using var parameters = System.Text.Json.JsonDocument.Parse(
            """{"world":{"size":[8,256,64],"regionSize":[16,16]}}""");

        var exception = Assert.Throws<InvalidDataException>(() =>
            WorldGenerationConfigCompiler.Compile(compilation.Pack! with
            {
                Parameters = parameters.RootElement.Clone(),
            }));

        Assert.Contains("dimensions", exception.Message, StringComparison.Ordinal);
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
