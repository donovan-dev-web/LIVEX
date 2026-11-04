using Prism.Core.Determinism;

namespace Prism.Core.World;

public sealed record OreVeinDefinition(
    string Id,
    ushort MaterialId,
    int MinimumDepthBelowSurface,
    int MaximumDepthBelowSurface,
    int LatticeScale,
    short Threshold);

public static class OreDepositGenerator
{
    public static int Generate(WorldGrid world, ulong seed, IEnumerable<OreVeinDefinition> veins)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(veins);

        var definitions = veins.OrderBy(vein => vein.Id, StringComparer.Ordinal).ToArray();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var vein in definitions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(vein.Id);
            if (!seenIds.Add(vein.Id))
            {
                throw new ArgumentException($"Duplicate ore vein definition '{vein.Id}'.", nameof(veins));
            }

            if (vein.MinimumDepthBelowSurface < 1 ||
                vein.MaximumDepthBelowSurface < vein.MinimumDepthBelowSurface ||
                vein.LatticeScale <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(veins),
                    $"Ore vein '{vein.Id}' has invalid depth bounds or lattice scale.");
            }
        }

        var added = 0;
        foreach (var vein in definitions)
        {
            var noise = FixedPointNoise.CreateSampler(seed, $"{RandomStreamNames.Ores}.{vein.Id}");
            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    var column = world.GetSurfaceColumn(x, y);
                    var minZ = Math.Max(1, column.SurfaceZ - vein.MaximumDepthBelowSurface);
                    var maxZ = column.SurfaceZ - vein.MinimumDepthBelowSurface;
                    for (var z = minZ; z <= maxZ; z++)
                    {
                        if (noise.SampleValue3D(x, y, z, vein.LatticeScale) < vein.Threshold)
                        {
                            continue;
                        }

                        var coordinate = new BlockCoordinate(x, y, z);
                        if (world.TryGetBlockOverride(coordinate, out _))
                        {
                            continue;
                        }

                        world.SetBlockOverride(coordinate, new BlockOverride(vein.MaterialId, 0, IsAir: false));
                        added++;
                    }
                }
            }
        }

        return added;
    }
}
