using System.Numerics;
using Prism.Viewer;

namespace Prism.Viewer.Tests;

public sealed class ViewerCameraTests
{
    [Fact]
    public void OrbitRotationKeepsTargetFixedAndZoomRespectsDistanceBounds()
    {
        var camera = ViewerCamera.LookAt(new Vector3(10, 20, 3), 0.4f, -0.3f, 50);
        var target = camera.Target;
        var initialPosition = camera.Position;

        camera.Rotate(0.5f, 0.2f);
        var rotatedPosition = camera.Position;
        camera.Zoom(-1000);

        Assert.Equal(ViewerCameraMode.Orbit, camera.Mode);
        Assert.Equal(target, camera.Target);
        Assert.NotEqual(initialPosition, rotatedPosition);
        Assert.Equal(2f, camera.OrbitDistance);
        Assert.Equal(2f, Vector3.Distance(target, camera.Position), 4);
        Assert.True(IsFinite(camera.CreateViewProjection(1.5f)));
    }

    [Fact]
    public void SwitchingToFreePreservesPositionAndFreeRotationDoesNotMoveCamera()
    {
        var camera = ViewerCamera.LookAt(new Vector3(10, 20, 3), 0.4f, -0.3f, 50);
        var initialPosition = camera.Position;
        camera.SetMode(ViewerCameraMode.Free);
        var freePosition = camera.Position;
        var initialView = camera.CreateViewProjection(1.5f);
        camera.Rotate(0.5f, 0.2f);

        Assert.Equal(ViewerCameraMode.Free, camera.Mode);
        Assert.Equal(initialPosition, freePosition);
        Assert.Equal(freePosition, camera.Position);
        Assert.NotEqual(initialView, camera.CreateViewProjection(1.5f));

        camera.Move(new Vector3(0, 0, 1), 1);
        Assert.Equal(30f, camera.Position.Z - freePosition.Z, 3);
    }

    [Fact]
    public void OrbitMovementPansTargetAndRejectsInvalidInputs()
    {
        var camera = ViewerCamera.LookAt(Vector3.Zero, 0, 0, 10);
        camera.Move(new Vector3(1, 0, 0), 1);

        Assert.Equal(new Vector3(0, 30, 0), camera.Target);
        Assert.Equal(10f, camera.OrbitDistance);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Move(Vector3.One, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Zoom(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.SetMode((ViewerCameraMode)99));
    }

    [Fact]
    public void FollowingAgentKeepsCameraOffsetAndCanBeStopped()
    {
        var camera = ViewerCamera.LookAt(new Vector3(10, 20, 3), 0.4f, -0.3f, 50);
        var initialOffset = camera.Position - camera.Target;
        camera.FollowAgent(17, new Vector3(15, 25, 4));
        var followedPosition = new Vector3(20, 30, 6);

        Assert.True(camera.UpdateFollowedAgent(17, followedPosition));
        Assert.Equal(followedPosition, camera.Target);
        Assert.InRange(
            Vector3.Distance(initialOffset, camera.Position - camera.Target),
            0,
            0.0001f);
        Assert.False(camera.UpdateFollowedAgent(18, Vector3.One));
        Assert.Equal((ulong)17, camera.FollowedAgentId);

        camera.StopFollowingAgent();
        Assert.Null(camera.FollowedAgentId);
        Assert.False(camera.UpdateFollowedAgent(17, Vector3.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => camera.FollowAgent(17, new Vector3(float.NaN, 0, 0)));
    }

    [Fact]
    public void FollowingAgentInFreeModeTranslatesCameraWithoutChangingItsViewDirection()
    {
        var camera = ViewerCamera.LookAt(Vector3.Zero, 0.4f, -0.3f, 50);
        camera.SetMode(ViewerCameraMode.Free);
        camera.FollowAgent(5, new Vector3(1, 2, 3));
        var positionBefore = camera.Position;
        var targetBefore = camera.Target;

        Assert.True(camera.UpdateFollowedAgent(5, new Vector3(4, 6, 8)));

        Assert.InRange(
            Vector3.Distance(new Vector3(3, 4, 5), camera.Position - positionBefore),
            0,
            0.0001f);
        Assert.InRange(
            Vector3.Distance(new Vector3(3, 4, 5), camera.Target - targetBefore),
            0,
            0.0001f);
    }

    private static bool IsFinite(Matrix4x4 matrix) =>
        float.IsFinite(matrix.M11) &&
        float.IsFinite(matrix.M12) &&
        float.IsFinite(matrix.M13) &&
        float.IsFinite(matrix.M14) &&
        float.IsFinite(matrix.M21) &&
        float.IsFinite(matrix.M22) &&
        float.IsFinite(matrix.M23) &&
        float.IsFinite(matrix.M24) &&
        float.IsFinite(matrix.M31) &&
        float.IsFinite(matrix.M32) &&
        float.IsFinite(matrix.M33) &&
        float.IsFinite(matrix.M34) &&
        float.IsFinite(matrix.M41) &&
        float.IsFinite(matrix.M42) &&
        float.IsFinite(matrix.M43) &&
        float.IsFinite(matrix.M44);
}
