namespace Prism.Core.World;

public readonly record struct SurfaceColumn(
    byte SurfaceZ,
    ushort MaterialId,
    byte State,
    ushort StrataProfileId);

public readonly record struct BlockCoordinate(int X, int Y, int Z);

public readonly record struct BlockOverride(ushort MaterialId, byte State, bool IsAir);
