using System.Buffers.Binary;
using Prism.Core.Determinism;

namespace Prism.Core.World;

public static class WorldGridHasher
{
    private const ulong OffsetBasis = 14_695_981_039_346_656_037UL;
    private const ulong Prime = 1_099_511_628_211UL;

    public static ulong ComputeRegionHash(WorldGrid world, int regionId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if ((uint)regionId >= (uint)world.RegionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(regionId));
        }

        var hash = OffsetBasis;
        AppendInt32(ref hash, regionId);
        AppendInt32(ref hash, world.Width);
        AppendInt32(ref hash, world.Height);
        AppendInt32(ref hash, world.RegionSize);

        var regionX = regionId % world.RegionCountX;
        var regionY = regionId / world.RegionCountX;
        var minX = regionX * world.RegionSize;
        var minY = regionY * world.RegionSize;
        var maxX = Math.Min(minX + world.RegionSize, world.Width);
        var maxY = Math.Min(minY + world.RegionSize, world.Height);
        AppendInt32(ref hash, maxX - minX);
        AppendInt32(ref hash, maxY - minY);

        for (var y = minY; y < maxY; y++)
        {
            for (var x = minX; x < maxX; x++)
            {
                AppendInt32(ref hash, x);
                AppendInt32(ref hash, y);
                var column = world.GetSurfaceColumn(x, y);
                AppendByte(ref hash, column.SurfaceZ);
                AppendUInt16(ref hash, column.MaterialId);
                AppendByte(ref hash, column.State);
                AppendUInt16(ref hash, column.StrataProfileId);
            }
        }

        AppendInt32(ref hash, world.GetRegionOverrideCount(regionId));
        foreach (var entry in world.EnumerateRegionOverrides(regionId))
        {
            AppendInt32(ref hash, entry.Key.Z);
            AppendInt32(ref hash, entry.Key.Y);
            AppendInt32(ref hash, entry.Key.X);
            AppendUInt16(ref hash, entry.Value.MaterialId);
            AppendByte(ref hash, entry.Value.State);
            AppendByte(ref hash, entry.Value.IsAir ? (byte)1 : (byte)0);
        }

        return hash;
    }

    public static ulong ComputeWorldHash(WorldGrid world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var hash = OffsetBasis;
        AppendInt32(ref hash, world.Width);
        AppendInt32(ref hash, world.Height);
        AppendInt32(ref hash, world.RegionSize);
        AppendInt32(ref hash, world.VerticalSize);
        AppendInt32(ref hash, world.RegionCount);
        for (var regionId = 0; regionId < world.RegionCount; regionId++)
        {
            AppendInt32(ref hash, regionId);
            AppendUInt64(ref hash, ComputeRegionHash(world, regionId));
        }

        return hash;
    }

    public static ulong ComputeWorldHash(
        WorldGrid world,
        int worldGenVersion,
        ulong seed,
        int attempt,
        ReadOnlySpan<WorldZone> zones,
        IReadOnlyList<BlockCoordinate> spawnSites,
        string contentHash)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(spawnSites);
        ArgumentNullException.ThrowIfNull(contentHash);
        if (zones.Length != checked(world.Width * world.Height))
        {
            throw new ArgumentException("Zone map length must match world dimensions.", nameof(zones));
        }

        var hash = OffsetBasis;
        AppendUInt64(ref hash, ComputeWorldHash(world));
        AppendInt32(ref hash, worldGenVersion);
        AppendUInt64(ref hash, seed);
        AppendInt32(ref hash, attempt);
        AppendUInt64(ref hash, string.IsNullOrEmpty(contentHash)
            ? 0
            : DeterministicRandom.HashName(contentHash));
        AppendInt32(ref hash, zones.Length);
        foreach (var zone in zones)
        {
            AppendByte(ref hash, (byte)zone);
        }

        AppendInt32(ref hash, spawnSites.Count);
        foreach (var site in spawnSites)
        {
            AppendInt32(ref hash, site.X);
            AppendInt32(ref hash, site.Y);
            AppendInt32(ref hash, site.Z);
        }

        return hash;
    }

    private static void AppendByte(ref ulong hash, byte value) =>
        hash = (hash ^ value) * Prime;

    private static void AppendUInt16(ref ulong hash, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        AppendBytes(ref hash, bytes);
    }

    private static void AppendInt32(ref ulong hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        AppendBytes(ref hash, bytes);
    }

    private static void AppendUInt64(ref ulong hash, ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        AppendBytes(ref hash, bytes);
    }

    private static void AppendBytes(ref ulong hash, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            AppendByte(ref hash, value);
        }
    }
}
