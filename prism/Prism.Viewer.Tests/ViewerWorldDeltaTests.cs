using Prism.Core.World;
using Prism.Host.Hosting;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerWorldDeltaTests
{
    [Fact]
    public void DeltaRemovesOverridesThatAreNoLongerPublished()
    {
        var grid = CreateFlatGrid(4);
        var mined = new BlockCoordinate(1, 1, 5);
        grid.SetBlockOverride(mined, new BlockOverride(8, 0, IsAir: false));
        var delta = CreateDelta(
            grid,
            [new KeyValuePair<BlockCoordinate, BlockOverride>(
                new BlockCoordinate(2, 2, 6),
                new BlockOverride(9, 0, IsAir: false))]);

        ViewerWorldFactory.ApplyDelta(grid, delta);

        Assert.False(grid.TryGetBlockOverride(mined, out _));
        Assert.True(grid.TryGetBlockOverride(new BlockCoordinate(2, 2, 6), out var added));
        Assert.Equal(9, added.MaterialId);
        Assert.True(grid.IsRegionDirty(0));
    }

    [Fact]
    public void DeltaKeepsOverridesStillPublishedIncludingAirEntries()
    {
        var grid = CreateFlatGrid(4);
        var air = new BlockCoordinate(0, 0, 4);
        grid.SetBlockOverride(air, new BlockOverride(0, 0, IsAir: true));
        var kept = new BlockCoordinate(3, 3, 5);
        grid.SetBlockOverride(kept, new BlockOverride(7, 0, IsAir: false));
        var delta = CreateDelta(
            grid,
            [
                new KeyValuePair<BlockCoordinate, BlockOverride>(
                    air,
                    new BlockOverride(0, 0, IsAir: true)),
                new KeyValuePair<BlockCoordinate, BlockOverride>(
                    kept,
                    new BlockOverride(7, 0, IsAir: false)),
            ]);

        ViewerWorldFactory.ApplyDelta(grid, delta);

        Assert.True(grid.TryGetBlockOverride(air, out var airBlock));
        Assert.True(airBlock.IsAir);
        Assert.True(grid.TryGetBlockOverride(kept, out _));
    }

    [Fact]
    public void MiningRemovesTheBlockFromTheRenderedMesh()
    {
        var world = CreateFlatGrid(3);
        var zones = Enumerable.Repeat(WorldZone.Forest, 9).ToArray();
        var tree = new BlockCoordinate(1, 1, 4);
        world.SetBlockOverride(tree, new BlockOverride(8, 0, IsAir: false));
        var palette = new TerrainMaterialPalette(1, 2, 3, 4, 5, 6, Tree: 8, Leaves: 9, Ore: 7);
        var before = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            zones,
            palette));
        Assert.Equal(66, before.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);

        // Le host publie un override air à la place du bloc miné, dans le delta de région.
        var delta = new WorldDeltaSnapshot(
            1,
            0UL,
            [
                new WorldRegionDelta(
                    0, 0, 0, 3, 3,
                    Enumerable.Repeat(new SurfaceColumn(3, 1, 0, 0), 9).ToArray(),
                    [new KeyValuePair<BlockCoordinate, BlockOverride>(
                        tree,
                        new BlockOverride(0, 0, IsAir: true))],
                    0UL),
            ]);

        ViewerWorldFactory.ApplyDelta(world, delta);

        var after = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            zones,
            palette));
        Assert.Equal(30, after.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);
    }

    private static WorldGrid CreateFlatGrid(int size)
    {
        var grid = new WorldGrid(size, size, regionSize: size, verticalSize: 16);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                grid.SetSurfaceColumn(x, y, new SurfaceColumn(3, 1, 0, 0));
            }
        }

        for (var regionId = 0; regionId < grid.RegionCount; regionId++)
        {
            grid.ClearRegionDirty(regionId);
        }

        return grid;
    }

    private static WorldDeltaSnapshot CreateDelta(
        WorldGrid grid,
        KeyValuePair<BlockCoordinate, BlockOverride>[] overrides)
    {
        var columns = new SurfaceColumn[grid.Width * grid.Height];
        Array.Fill(columns, new SurfaceColumn(3, 1, 0, 0));
        return new WorldDeltaSnapshot(
            1,
            0UL,
            [new WorldRegionDelta(0, 0, 0, grid.Width, grid.Height, columns, overrides, 0UL)]);
    }
}
