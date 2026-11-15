using Prism.Core.World;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class TerrainOverrideMeshTests
{
    private const ushort Tree = 8;
    private const ushort Leaves = 9;
    private const ushort Ore = 7;
    private static readonly TerrainMaterialPalette Palette =
        new(1, 2, 3, 4, 5, 6, Tree: Tree, Leaves: Leaves, Ore: Ore);

    [Fact]
    public void TreeOverrideRendersAsVolumeAboveTheGround()
    {
        var world = CreateWorld(3);
        world.SetBlockOverride(new BlockCoordinate(1, 1, 4), new BlockOverride(Tree, 0, false));

        var mesh = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            CreateZones(3),
            Palette));

        // Surface plate : 6 sommets de dessus + 4 faces de bord (24) = 30, plus un cube (36).
        Assert.Equal(66, mesh.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);
        Assert.Contains(0.40f, mesh.Vertices);
        Assert.Contains(5f, mesh.Vertices);
    }

    [Fact]
    public void RemovingTheOverrideRemovesItsGeometry()
    {
        var world = CreateWorld(3);
        var coordinate = new BlockCoordinate(1, 1, 4);
        world.SetBlockOverride(coordinate, new BlockOverride(Tree, 0, false));

        var withTree = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            CreateZones(3),
            Palette));
        world.RemoveBlockOverride(coordinate);
        var withoutTree = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            CreateZones(3),
            Palette));

        Assert.Equal(66, withTree.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);
        Assert.Equal(30, withoutTree.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);
    }

    [Fact]
    public void AirOverrideIsNotDrawn()
    {
        var world = CreateWorld(3);
        world.SetBlockOverride(new BlockCoordinate(1, 1, 4), new BlockOverride(0, 0, IsAir: true));

        var mesh = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            CreateZones(3),
            Palette));

        Assert.Equal(30, mesh.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);
    }

    [Fact]
    public void BuriedOreOnlyAppearsInTheSectionView()
    {
        var world = CreateWorld(3);
        world.SetBlockOverride(new BlockCoordinate(1, 1, 1), new BlockOverride(Ore, 0, false));

        var visible = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            CreateZones(3),
            Palette));
        var section = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            CreateZones(3),
            Palette with { ShowBuried = true }));

        Assert.Equal(30, visible.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);
        Assert.Equal(66, section.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);
        Assert.Contains(0.74f, section.Vertices);
    }

    [Fact]
    public void PlacedBlockOnTheWorldBorderStillRenders()
    {
        var world = CreateWorld(3);
        world.SetBlockOverride(new BlockCoordinate(0, 1, 4), new BlockOverride(Tree, 0, false));

        var mesh = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            CreateZones(3),
            Palette));

        // Cinq faces au lieu de six : la face ouest est la paroi du monde, déjà dessinée.
        Assert.Equal(60, mesh.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);
    }

    [Fact]
    public void WaterStaysAFlatSurfaceInsteadOfACube()
    {
        var world = CreateWorld(1);
        world.SetBlockOverride(new BlockCoordinate(0, 0, 6), new BlockOverride(6, 0, false));

        var mesh = Assert.Single(TerrainMeshBuilder.BuildRegions(
            world,
            CreateZones(1),
            Palette));

        Assert.Equal(30, mesh.Vertices.Length / TerrainMeshBuilder.FloatsPerVertex);
        Assert.Contains(7f, mesh.Vertices);
    }

    private static WorldGrid CreateWorld(int size)
    {
        var world = new WorldGrid(size, size, regionSize: size, verticalSize: 16);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(3, 1, 0, 0));
            }
        }

        return world;
    }

    private static WorldZone[] CreateZones(int size) =>
        Enumerable.Repeat(WorldZone.Forest, size * size).ToArray();
}
