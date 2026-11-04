using System.Numerics;
using Prism.Core.World;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerFrustumCullerTests
{
    [Fact]
    public void KeepsVisibleAndFrustumIntersectingBoundsButRejectsOutsideBounds()
    {
        var view = Matrix4x4.CreateLookAt(
            new Vector3(0, -10, 4),
            new Vector3(0, 0, 0),
            Vector3.UnitZ);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3,
            aspectRatio: 1,
            nearPlaneDistance: 0.1f,
            farPlaneDistance: 100);
        var viewProjection = view * projection;

        Assert.True(ViewerFrustumCuller.IsVisible(
            new Vector3(-1, -1, -1),
            new Vector3(1, 1, 1),
            viewProjection));
        Assert.True(ViewerFrustumCuller.IsVisible(
            new Vector3(8, 0, 0),
            new Vector3(12, 4, 4),
            viewProjection));
        Assert.False(ViewerFrustumCuller.IsVisible(
            new Vector3(100, 0, 0),
            new Vector3(102, 2, 2),
            viewProjection));
        Assert.False(ViewerFrustumCuller.IsVisible(
            new Vector3(-1, -30, -1),
            new Vector3(1, -28, 1),
            viewProjection));
    }

    [Fact]
    public void RegionBoundsRespectWorldEdgesAndRejectInvalidRegionIds()
    {
        var world = new WorldGrid(5, 3, regionSize: 2, verticalSize: 16);

        var bounds = ViewerFrustumCuller.GetRegionBounds(world, regionId: 5);

        Assert.Equal(new Vector3(4, 2, 0), bounds.Minimum);
        Assert.Equal(new Vector3(5, 3, 16), bounds.Maximum);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ViewerFrustumCuller.GetRegionBounds(world, world.RegionCount));
    }

    [Fact]
    public void RejectsInvalidBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ViewerFrustumCuller.IsVisible(
                new Vector3(2, 0, 0),
                new Vector3(1, 1, 1),
                Matrix4x4.Identity));
    }
}
