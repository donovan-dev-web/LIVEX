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

        Assert.Equal(30, mesh.Vertices.Length / 6);
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

    [Fact]
    public void ZoneOverlayUsesDistinctColorsForEachBiome()
    {
        var overlay = Palette with { ZoneOverlay = true };

        Assert.NotEqual(
            overlay.GetColor(Palette.Grass, WorldZone.Forest),
            overlay.GetColor(Palette.Grass, WorldZone.Plains));
        Assert.NotEqual(
            overlay.GetColor(Palette.Grass, WorldZone.Rocky),
            overlay.GetColor(Palette.Grass, WorldZone.Shore));
        Assert.NotEqual(
            Palette.GetColor(Palette.Grass, WorldZone.Plains),
            overlay.GetColor(Palette.Grass, WorldZone.Plains));
    }

    [Fact]
    public void HigherAdjacentTerrainDarkensSharedTopSurfaceCorner()
    {
        var world = new WorldGrid(2, 1, regionSize: 2, verticalSize: 16);
        world.SetSurfaceColumn(0, 0, new SurfaceColumn(3, 1, 0, 0));
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(5, 1, 0, 0));

        var mesh = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            [WorldZone.Plains, WorldZone.Plains],
            Palette));

        Assert.Equal(0.598f, mesh.Vertices[10], 3);
    }

    [Fact]
    public void IncrementalMesherBuildsOnlyDirtyRegionsWithinFrameBudget()
    {
        var world = new WorldGrid(4, 2, regionSize: 2, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(3, 1, 0, 0));
            }
        }

        var zones = Enumerable.Repeat(WorldZone.Plains, world.Width * world.Height).ToArray();
        var mesher = new IncrementalTerrainMesher();
        var firstFrame = mesher.Update(world, zones, Palette, maximumRegions: 1);
        var secondFrame = mesher.Update(world, zones, Palette, maximumRegions: 1);

        Assert.Single(firstFrame);
        Assert.Single(secondFrame);
        Assert.Equal(2, mesher.Meshes.Count);
        Assert.Empty(mesher.Update(world, zones, Palette, maximumRegions: 2));

        world.SetSurfaceColumn(3, 1, new SurfaceColumn(4, 1, 0, 0));
        var changed = Assert.Single(mesher.Update(world, zones, Palette, maximumRegions: 1));
        Assert.Equal(1, changed.RegionId);
        Assert.Empty(mesher.Update(world, zones, Palette, maximumRegions: 1));
    }

    [Fact]
    public void BoundaryEditInvalidatesBothAdjacentRegionMeshes()
    {
        var world = new WorldGrid(4, 1, regionSize: 2, verticalSize: 16);
        for (var x = 0; x < world.Width; x++)
        {
            world.SetSurfaceColumn(x, 0, new SurfaceColumn(3, 1, 0, 0));
        }

        var mesher = new IncrementalTerrainMesher();
        var zones = Enumerable.Repeat(WorldZone.Plains, world.Width).ToArray();
        Assert.Equal(2, mesher.Update(world, zones, Palette, maximumRegions: 2).Count);
        world.SetSurfaceColumn(1, 0, new SurfaceColumn(5, 1, 0, 0));

        var rebuilt = mesher.Update(world, zones, Palette, maximumRegions: 2);

        Assert.Equal([0, 1], rebuilt.Select(mesh => mesh.RegionId));
    }

    [Fact]
    public void ExplicitInvalidationRebuildsCachedRegionsWithinTheFrameBudget()
    {
        var world = new WorldGrid(4, 1, regionSize: 2, verticalSize: 16);
        for (var x = 0; x < world.Width; x++)
        {
            world.SetSurfaceColumn(x, 0, new SurfaceColumn(3, 1, 0, 0));
        }

        var zones = Enumerable.Repeat(WorldZone.Plains, world.Width).ToArray();
        var mesher = new IncrementalTerrainMesher();
        Assert.Equal(2, mesher.Update(world, zones, Palette, 2).Count);
        mesher.InvalidateAll();

        Assert.Single(mesher.Update(world, zones, Palette, 1));
        Assert.Single(mesher.Update(world, zones, Palette, 1));
        Assert.Equal(2, mesher.Meshes.Count);
    }
}
