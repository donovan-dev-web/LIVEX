namespace Prism.Core.World;

public sealed record StrataProfile(
    ushort Id,
    ushort SoilMaterialId,
    ushort StoneMaterialId,
    ushort BedrockMaterialId,
    byte SoilDepth);

public static class WorldStrataResolver
{
    public static ushort ResolveMaterialId(SurfaceColumn column, int z, StrataProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (z < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(z));
        }

        if (z > column.SurfaceZ)
        {
            throw new ArgumentOutOfRangeException(nameof(z), "Coordinate is above the surface of this column.");
        }

        if (z == 0)
        {
            return profile.BedrockMaterialId;
        }

        if (z == column.SurfaceZ)
        {
            return column.MaterialId;
        }

        var depthBelowSurface = column.SurfaceZ - z;
        return depthBelowSurface <= profile.SoilDepth
            ? profile.SoilMaterialId
            : profile.StoneMaterialId;
    }
}
