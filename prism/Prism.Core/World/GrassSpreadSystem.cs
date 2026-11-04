using Prism.Core.Determinism;
using Prism.Core.Time;

namespace Prism.Core.World;

public sealed record GrassSpreadRule(
    ushort GrassMaterialId,
    ushort SoilMaterialId,
    int ChancePermille = 1000)
{
    public void Validate()
    {
        if (ChancePermille is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(ChancePermille));
        }
    }
}

public static class GrassSpreadSystem
{
    private static readonly (int X, int Y)[] Neighbors =
    [
        (0, -1),
        (-1, 0),
        (1, 0),
        (0, 1),
    ];

    public static BlockOverride? TrySpread(
        WorldGrid world,
        ulong runSeed,
        RandomTickSample sample,
        GrassSpreadRule rule)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rule);
        rule.Validate();
        if (sample.Tick < 0 ||
            sample.RegionId < 0 ||
            sample.Ordinal < 0 ||
            !world.TryGetSurfaceColumn(sample.X, sample.Y, out var target))
        {
            return null;
        }

        if (target.MaterialId != rule.SoilMaterialId ||
            HasBlockingOverride(world, sample.X, sample.Y, target.SurfaceZ + 1))
        {
            return null;
        }

        if (rule.ChancePermille == 0 ||
            DeterministicRandom.CounterValue(
                runSeed,
                RandomStreamNames.GrassSpread,
                (ulong)sample.Tick,
                GetColumnKey(sample.X, sample.Y)) % 1000 >= (ulong)rule.ChancePermille)
        {
            return null;
        }

        foreach (var (offsetX, offsetY) in Neighbors)
        {
            var neighborX = sample.X + offsetX;
            var neighborY = sample.Y + offsetY;
            if (!world.TryGetSurfaceColumn(neighborX, neighborY, out var neighbor))
            {
                continue;
            }

            var neighborBlock = new BlockCoordinate(neighborX, neighborY, neighbor.SurfaceZ + 1);
            if (world.TryGetBlockOverride(neighborBlock, out var block) &&
                !block.IsAir &&
                block.MaterialId == rule.GrassMaterialId)
            {
                return new BlockOverride(rule.GrassMaterialId, 0, IsAir: false);
            }
        }

        return null;
    }

    private static bool HasBlockingOverride(WorldGrid world, int x, int y, int z) =>
        world.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var block) &&
        !block.IsAir;

    private static ulong GetColumnKey(int x, int y) =>
        ((ulong)(uint)y << 32) | (uint)x;
}
