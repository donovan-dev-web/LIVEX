using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerAssetsTests
{
    private static readonly TerrainMaterialPalette Fallback = new(1, 2, 3, 4, 5, 6);

    [Fact]
    public void PngDecodeReadsScreenshotPngTopDown()
    {
        // Tampon OpenGL : ligne du bas rouge, ligne du haut verte.
        var pixels = new byte[]
        {
            255, 0, 0, 255,
            0, 255, 0, 255,
        };

        var image = ViewerPng.Decode(ViewerScreenshot.EncodePng(1, 2, pixels));

        Assert.Equal(1, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(new byte[] { 0, 255, 0, 255, 255, 0, 0, 255 }, image.Rgba);
    }

    [Fact]
    public void PngDecodeRejectsForeignData()
    {
        Assert.Throws<InvalidDataException>(() => ViewerPng.Decode("pas un png"u8.ToArray()));
    }

    [Fact]
    public void MissingExplicitAssetsDirectoryFallsBack()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"prism-assets-missing-{Guid.NewGuid():N}");

        var assets = ViewerAssets.Load(missing, AppContext.BaseDirectory, Fallback);

        Assert.Null(assets.Root);
        Assert.Equal(ViewerTextureAtlas.DefaultTileSize, assets.Atlas.TileSize);
        Assert.Same(ViewerAgentPalette.Default, assets.Agents);
        Assert.Same(ViewerHudFont.Default, assets.Font);
        Assert.Equal(Fallback, assets.Palette);
        Assert.Contains(assets.Warnings, warning => warning.Contains("introuvable", StringComparison.Ordinal));
    }

    [Fact]
    public void AssetsDirectoryOverridesAtlasPaletteAgentsAndFont()
    {
        var root = CreateRoot();
        try
        {
            var atlas = ViewerTextureAtlas.Create();
            WriteAtlas(root, atlas);
            WriteConfig(root, "terrain-palette.json", "{\"schema\":1,\"grass\":12,\"water\":13}");
            WriteConfig(
                root,
                "agent-palette.json",
                "{\"schema\":1,\"idle\":[255,0,0],\"carrying\":[0,255,0],\"acting\":[0,0,255]," +
                "\"headBoost\":0.2,\"legDarken\":0.5}");
            var fonts = Path.Combine(root, "fonts");
            Directory.CreateDirectory(fonts);
            File.WriteAllText(
                Path.Combine(fonts, "hud-5x7.json"),
                "{\"schema\":1,\"width\":5,\"height\":7,\"advance\":6," +
                "\"glyphs\":{\"A\":[31,17,17,31,17,17,17]}}");

            var assets = ViewerAssets.Load(root, AppContext.BaseDirectory, Fallback);

            Assert.Equal(root, assets.Root);
            Assert.Equal(12, assets.Palette.Grass);
            Assert.Equal(13, assets.Palette.Water);
            Assert.Equal(3, assets.Palette.Stone);
            Assert.Equal(atlas.Rgba, assets.Atlas.Rgba);
            Assert.Equal(1f, assets.Agents.Idle.R, 3);
            Assert.Equal(0.5f, assets.Agents.LegDarken, 3);
            Assert.True(assets.Font.TryGetGlyph('a', out var glyph));
            Assert.Equal(31, glyph[0]);
            Assert.Empty(assets.Warnings);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvalidFilesFallBackWithWarnings()
    {
        var root = CreateRoot();
        try
        {
            var textures = Path.Combine(root, "textures");
            Directory.CreateDirectory(textures);
            File.WriteAllBytes(Path.Combine(textures, "viewer-atlas.png"), [1, 2, 3, 4]);
            WriteConfig(root, "terrain-palette.json", "{ pas du json");

            var assets = ViewerAssets.Load(root, AppContext.BaseDirectory, Fallback);

            Assert.Equal(ViewerTextureAtlas.DefaultTileSize, assets.Atlas.TileSize);
            Assert.Equal(Fallback, assets.Palette);
            Assert.Equal(2, assets.Warnings.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DefaultAtlasCarriesOneTilePerMaterialRole()
    {
        var atlas = ViewerTextureAtlas.Create();

        Assert.Equal(ViewerTextureAtlas.Columns * ViewerTextureAtlas.DefaultTileSize, atlas.Width);
        Assert.Equal(ViewerTextureAtlas.DefaultTileSize, atlas.TileSize);
        Assert.Equal(
            ViewerTextureAtlas.StructureTile,
            ViewerTextureAtlas.GetTileIndex(99, new TerrainMaterialPalette(1, 2, 3, 4, 5, 6)));
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"prism-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteAtlas(string root, ViewerTextureAtlas atlas)
    {
        var textures = Path.Combine(root, "textures");
        Directory.CreateDirectory(textures);
        var flipped = FlipRows(atlas.Rgba, atlas.Width, atlas.Height);
        File.WriteAllBytes(
            Path.Combine(textures, "viewer-atlas.png"),
            ViewerScreenshot.EncodePng(atlas.Width, atlas.Height, flipped));
    }

    private static void WriteConfig(string root, string name, string json)
    {
        var config = Path.Combine(root, "config");
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, name), json);
    }

    private static byte[] FlipRows(byte[] rgba, int width, int height)
    {
        var stride = width * 4;
        var flipped = new byte[rgba.Length];
        for (var row = 0; row < height; row++)
        {
            Array.Copy(
                rgba,
                row * stride,
                flipped,
                (height - 1 - row) * stride,
                stride);
        }

        return flipped;
    }
}
