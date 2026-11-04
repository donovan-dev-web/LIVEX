using System.Numerics;
using Prism.Core.World;

namespace Prism.Viewer;

public static class ViewerFrustumCuller
{
    public static bool IsVisible(
        Vector3 minimum,
        Vector3 maximum,
        Matrix4x4 viewProjection)
    {
        if (!IsFinite(minimum) || !IsFinite(maximum) ||
            minimum.X > maximum.X ||
            minimum.Y > maximum.Y ||
            minimum.Z > maximum.Z)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "Bounds must be finite and ordered.");
        }

        Span<Vector4> clipCorners = stackalloc Vector4[8];
        var index = 0;
        for (var z = 0; z < 2; z++)
        {
            for (var y = 0; y < 2; y++)
            {
                for (var x = 0; x < 2; x++)
                {
                    var point = new Vector4(
                        x == 0 ? minimum.X : maximum.X,
                        y == 0 ? minimum.Y : maximum.Y,
                        z == 0 ? minimum.Z : maximum.Z,
                        1f);
                    var clip = Vector4.Transform(point, viewProjection);
                    if (!IsFinite(clip))
                    {
                        return true;
                    }

                    clipCorners[index++] = clip;
                }
            }
        }

        var outsideLeft = true;
        var outsideRight = true;
        var outsideBottom = true;
        var outsideTop = true;
        var outsideNear = true;
        var outsideFar = true;
        foreach (var corner in clipCorners)
        {
            outsideLeft &= corner.X < -corner.W;
            outsideRight &= corner.X > corner.W;
            outsideBottom &= corner.Y < -corner.W;
            outsideTop &= corner.Y > corner.W;
            outsideNear &= corner.Z < -corner.W;
            outsideFar &= corner.Z > corner.W;
        }

        return !(outsideLeft || outsideRight || outsideBottom || outsideTop || outsideNear || outsideFar);
    }

    public static (Vector3 Minimum, Vector3 Maximum) GetRegionBounds(
        WorldGrid world,
        int regionId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if ((uint)regionId >= (uint)world.RegionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(regionId));
        }

        var regionX = regionId % world.RegionCountX;
        var regionY = regionId / world.RegionCountX;
        var minimumX = regionX * world.RegionSize;
        var minimumY = regionY * world.RegionSize;
        var maximumX = Math.Min(minimumX + world.RegionSize, world.Width);
        var maximumY = Math.Min(minimumY + world.RegionSize, world.Height);
        return (
            new Vector3(minimumX, minimumY, 0),
            new Vector3(maximumX, maximumY, world.VerticalSize));
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsFinite(Vector4 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) &&
        float.IsFinite(value.W);
}
