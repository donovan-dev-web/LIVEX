using System.Numerics;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerCameraPanTests
{
    [Fact]
    public void PanMovesTheTargetAlongTheCameraAxes()
    {
        var camera = ViewerCamera.LookAt(Vector3.Zero, 0f, -0.5f, 10f);

        camera.Pan(1f, 0f);

        Assert.Equal(0.3f, camera.Target.Y, 3);
        Assert.Equal(0f, camera.Target.X, 3);

        camera.Pan(0f, 1f);

        Assert.Equal(0.3f, camera.Target.X, 3);
    }

    [Fact]
    public void WheelZoomClampsBetweenBothOrbitLimits()
    {
        var camera = ViewerCamera.LookAt(Vector3.Zero, 0f, -0.5f, 10f);

        camera.Zoom(5_000f);
        Assert.Equal(2_000f, camera.OrbitDistance);

        camera.Zoom(-5_000f);
        Assert.Equal(2f, camera.OrbitDistance);
    }

    [Fact]
    public void PanRejectsNonFiniteInput()
    {
        var camera = ViewerCamera.LookAt(Vector3.Zero, 0f, -0.5f, 10f);

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Pan(float.NaN, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Pan(0f, float.PositiveInfinity));
    }
}
