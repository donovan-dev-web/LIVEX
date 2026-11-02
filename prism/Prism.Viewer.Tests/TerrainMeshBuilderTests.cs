using Prism.Core.World;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class TerrainMeshBuilderTests
{
    private static readonly TerrainMaterialPalette Palette = new(1, 2, 3, 4, 5, 6);

    [Fact]
    public void FlatTwoColumnRegionContainsOnlyTopAndBoundaryFaces()
    {
        var world = new WorldGrid(2, 1, regionSize: 2, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(3, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(3, 1, 0, 0));
        var zones = new[] { WorldZone.Plains, WorldZone.Plains };

        var mesh = Assert.Single(TerrainMeshBuilder.BuildRegions(world, zones, Palette));
        var repeatedMesh = Assert.Single(TerrainMeshBuilder.BuildRegions(world, zones, Palette));

        Assert.Equal(48, mesh.Vertices.Length / 6);
        Assert.Equal(mesh.Vertices, repeatedMesh.Vertices);
    }

    [Fact]
    public void HeightDifferenceAddsOnlyTheExposedCliffFace()
    {
        var world = new WorldGrid(2, 1, regionSize: 2, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(3, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(5, 1, 0, 0));
        var zones = new[] { WorldZone.Plains, WorldZone.Plains };

        var mesh = Assert.Single(TerrainMeshBuilder.BuildRegions(world, zones, Palette));

        Assert.Equal(54, mesh.Vertices.Length / 6);
    }

    [Fact]
    public void WaterOverrideRaisesVisibleSurfaceToTheWaterTop()
    {
        var world = new WorldGrid(1, 1, regionSize: 1, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(3, 1, 0, 0));
        world.SetBlockOverride(new BlockCoordinate(0, 0, 7), new BlockOverride(6, 0, false));
        var zones = new[] { WorldZone.Shore };

        var mesh = Assert.Single(TerrainMeshBuilder.BuildRegions(world, zones, Palette));

        Assert.Contains(8f, mesh.Vertices);
        Assert.Contains(0.20f, mesh.Vertices);
    }
}
