using Prism.Core.World;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerOverlayMeshBuilderTests
{
    [Fact]
    public void ConstructionGhostsGrowWithProgressAndKeepAStableOrder()
    {
        var planned = new ViewerConstructionSiteSample("s1", "hut", 2, 3, 4, "Planned", 10, 0);
        var half = new ViewerConstructionSiteSample("s2", "hut", 2, 3, 4, "InProgress", 10, 5);
        var completed = new ViewerConstructionSiteSample("s3", "hut", 2, 3, 4, "Completed", 10, 10);

        var plannedVertices = ViewerOverlayMeshBuilder.BuildConstructionGhosts([planned]);
        var halfVertices = ViewerOverlayMeshBuilder.BuildConstructionGhosts([half]);
        var completedVertices = ViewerOverlayMeshBuilder.BuildConstructionGhosts([completed]);

        Assert.NotEmpty(plannedVertices);
        Assert.Equal(plannedVertices.Length, halfVertices.Length);
        Assert.Equal(plannedVertices.Length, completedVertices.Length);
        Assert.Equal(0, plannedVertices.Length % TerrainMeshBuilder.FloatsPerVertex);
        Assert.True(MaxHeight(completedVertices) > MaxHeight(halfVertices));
        Assert.True(MaxHeight(halfVertices) > MaxHeight(plannedVertices));
        Assert.Equal(
            ViewerOverlayMeshBuilder.BuildConstructionGhosts([planned, half]),
            ViewerOverlayMeshBuilder.BuildConstructionGhosts([planned, half]));
    }

    [Fact]
    public void HeatmapSkipsEmptyCellsAndStaysAboveTheSurface()
    {
        var world = new WorldGrid(2, 2, regionSize: 2, verticalSize: 16);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetSurfaceColumn(x, y, new SurfaceColumn(3, 1, 0, 0));
            }
        }

        Assert.Empty(ViewerOverlayMeshBuilder.BuildHeatmap([], world, 1));

        var vertices = ViewerOverlayMeshBuilder.BuildHeatmap(
            [new ViewerHeatCell(0, 0, 4, 3)],
            world,
            peak: 3);

        Assert.NotEmpty(vertices);
        var positions = vertices.Chunk(TerrainMeshBuilder.FloatsPerVertex)
            .Select(vertex => vertex[2])
            .ToArray();
        Assert.All(positions, z => Assert.True(z >= 4f));
    }

    [Fact]
    public void GroundItemsAreColouredByFreshnessOnlyWhenTheLayerIsOn()
    {
        var fresh = new ViewerGroundItemSample(1, 1, 1, 4, 2, 3, 2_400);
        var old = new ViewerGroundItemSample(2, 2, 1, 4, 2, 3, 200);

        var neutral = ViewerOverlayMeshBuilder.BuildGroundItems([fresh, old], freshness: false);
        var coloured = ViewerOverlayMeshBuilder.BuildGroundItems([fresh, old], freshness: true);

        Assert.Equal(neutral.Length, coloured.Length);
        Assert.NotEqual(neutral, coloured);
        // Le premier sommet de chaque cube porte la couleur de sa face supérieure.
        var itemStride = neutral.Length / 2;
        Assert.Equal(
            ColourAt(neutral, 0),
            ColourAt(neutral, itemStride));
        Assert.NotEqual(
            ColourAt(coloured, 0),
            ColourAt(coloured, itemStride));
        Assert.NotEqual(ColourAt(neutral, 0), ColourAt(coloured, 0));
    }

    [Fact]
    public void MovementHeatmapCountsVisitsPerCellAndClampsOutOfBounds()
    {
        var heatmap = new ViewerMovementHeatmap(4, 4);
        heatmap.Visit(1, 1, 3);
        heatmap.Visit(1, 1, 3);
        heatmap.Visit(9, 9, 3);

        var cell = Assert.Single(heatmap.Cells);
        Assert.Equal(1, cell.X);
        Assert.Equal(1, cell.Y);
        Assert.Equal(2, cell.Count);
        Assert.Equal(2, heatmap.Peak);

        heatmap.Clear();
        Assert.Empty(heatmap.Cells);
        Assert.Equal(0, heatmap.Peak);
    }

    private static float MaxHeight(float[] vertices) =>
        vertices.Chunk(TerrainMeshBuilder.FloatsPerVertex).Max(vertex => vertex[2]);

    private static (float R, float G, float B) ColourAt(float[] vertices, int offset) =>
        (vertices[offset + 3], vertices[offset + 4], vertices[offset + 5]);
}
