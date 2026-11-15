using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerTextureAtlasTests
{
    private static readonly TerrainMaterialPalette Palette = new(1, 2, 3, 4, 5, 6);

    [Fact]
    public void AtlasIsDeterministicAndCoversEveryTile()
    {
        var first = ViewerTextureAtlas.Create();
        var second = ViewerTextureAtlas.Create();

        Assert.Equal(first.Rgba, second.Rgba);
        Assert.Equal(first.Width * first.Height * 4, first.Rgba.Length);
        Assert.Equal(ViewerTextureAtlas.Columns * first.TileSize, first.Width);
        Assert.Equal(ViewerTextureAtlas.Rows * first.TileSize, first.Height);
        Assert.All(first.Rgba.Chunk(4), pixel => Assert.Equal(255, pixel[3]));
    }

    [Fact]
    public void EveryPaletteMaterialResolvesToItsOwnTile()
    {
        Assert.Equal(ViewerTextureAtlas.GrassTile, ViewerTextureAtlas.GetTileIndex(Palette.Grass, Palette));
        Assert.Equal(ViewerTextureAtlas.SandTile, ViewerTextureAtlas.GetTileIndex(Palette.Sand, Palette));
        Assert.Equal(ViewerTextureAtlas.StoneTile, ViewerTextureAtlas.GetTileIndex(Palette.Stone, Palette));
        Assert.Equal(ViewerTextureAtlas.SoilTile, ViewerTextureAtlas.GetTileIndex(Palette.Soil, Palette));
        Assert.Equal(ViewerTextureAtlas.BedrockTile, ViewerTextureAtlas.GetTileIndex(Palette.Bedrock, Palette));
        Assert.Equal(ViewerTextureAtlas.WaterTile, ViewerTextureAtlas.GetTileIndex(Palette.Water, Palette));
        Assert.Equal(ViewerTextureAtlas.StructureTile, ViewerTextureAtlas.GetTileIndex(99, Palette));
    }

    [Fact]
    public void TileBoundsStayInsideTheAtlasAndDoNotOverlap()
    {
        var atlas = ViewerTextureAtlas.Create();
        var bounds = Enumerable.Range(0, ViewerTextureAtlas.Columns * ViewerTextureAtlas.Rows)
            .Select(atlas.GetTileBounds)
            .ToArray();

        Assert.All(bounds, tile =>
        {
            Assert.InRange(tile.MinU, 0f, 1f);
            Assert.InRange(tile.MinV, 0f, 1f);
            Assert.InRange(tile.MaxU, 0f, 1f);
            Assert.InRange(tile.MaxV, 0f, 1f);
            Assert.True(tile.MaxU > tile.MinU);
            Assert.True(tile.MaxV > tile.MinV);
        });
        Assert.Equal(bounds.Length, bounds.Distinct().Count());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => atlas.GetTileBounds(ViewerTextureAtlas.Columns * ViewerTextureAtlas.Rows));
    }

    [Fact]
    public void TerrainMeshCarriesAtlasCoordinatesForItsMaterial()
    {
        var world = new Prism.Core.World.WorldGrid(1, 1, regionSize: 1, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new Prism.Core.World.SurfaceColumn(3, Palette.Stone, 0, 0));

        var mesh = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            [Prism.Core.World.WorldZone.Plains],
            Palette));

        var atlas = ViewerTextureAtlas.Create();
        var (minU, minV, maxU, maxV) = atlas.GetTileBounds(ViewerTextureAtlas.StoneTile);
        var topVertex = mesh.Vertices.Chunk(TerrainMeshBuilder.FloatsPerVertex).First();
        Assert.Equal(minU, topVertex[6], 4);
        Assert.Equal(minV, topVertex[7], 4);
        Assert.Contains(mesh.Vertices, value => Math.Abs(value - maxU) < 0.0001f);
        Assert.Contains(mesh.Vertices, value => Math.Abs(value - maxV) < 0.0001f);
    }
}
