using Prism.Content;

namespace Prism.Content.Tests;

public sealed class StructureTemplateRuntimeCompilerTests
{
    [Fact]
    public void CompilesMinimalPackTemplatesToStableMaterialIds()
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));

        var templates = StructureTemplateRuntimeCompiler.Compile(compilation.Pack!);

        Assert.Equal(8, templates.Count);
        Assert.Equal(120, templates["shelter_3x3"].WorkTicks);
        Assert.Equal(
            compilation.Pack!.Registries.Materials.GetId("planks"),
            templates["shelter_3x3"].Blocks[0].MaterialId);
        var shelterEffect = Assert.IsType<Prism.Core.World.StructureEffectRule>(templates["shelter_3x3"].Effect);
        Assert.Equal("shelter", shelterEffect.Type);
        Assert.Equal(new Prism.Core.World.BlockCoordinate(0, 0, 0), shelterEffect.MinimumOffset);
        Assert.Equal(new Prism.Core.World.BlockCoordinate(2, 2, 2), shelterEffect.MaximumOffset);
        Assert.False(templates["stair"].RequiresFlatGround);
        Assert.Equal(
            compilation.Pack.Registries.Items.GetId("plank"),
            Assert.Single(templates["path_segment"].Materials!).ItemTypeId);
        Assert.Equal(1, templates["path_segment"].Materials![0].Quantity);
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
