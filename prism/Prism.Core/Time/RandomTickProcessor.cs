using Prism.Core.Determinism;
using Prism.Core.World;

namespace Prism.Core.Time;

public readonly record struct RandomTickSample(
    long Tick,
    int RegionId,
    int Ordinal,
    int X,
    int Y);

public readonly record struct RandomTickResult(int SamplesProcessed, int BlocksChanged);

public static class RandomTickProcessor
{
    public static RandomTickResult Execute(
        WorldGrid world,
        ulong runSeed,
        long tick,
        int ticksPerRegion,
        Func<WorldGrid, RandomTickSample, BlockOverride?> update,
        int maxDegreeOfParallelism = 1)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(update);
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        if (ticksPerRegion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerRegion));
        }

        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        }

        for (var regionId = 0; regionId < world.RegionCount; regionId++)
        {
            var (regionWidth, regionHeight, _, _) = GetRegionBounds(world, regionId);
            if (ticksPerRegion > checked(regionWidth * regionHeight))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ticksPerRegion), "Random ticks per region cannot exceed the number of columns.");
            }
        }

        var results = new List<(RandomTickSample Sample, BlockOverride Block)>[world.RegionCount];
        Parallel.For(
            0,
            world.RegionCount,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            regionId =>
            {
                var (regionWidth, regionHeight, originX, originY) = GetRegionBounds(world, regionId);
                var regionArea = checked(regionWidth * regionHeight);
                var samples = new List<(RandomTickSample, BlockOverride)>(ticksPerRegion);
                var start = (int)(DeterministicRandom.CounterValue(
                    runSeed, RandomStreamNames.RandomTicks, (ulong)tick, GetCounterKey(regionId, 0)) %
                    (ulong)regionArea);
                var stride = GetCoprimeStride(
                    regionArea,
                    DeterministicRandom.CounterValue(
                        runSeed, RandomStreamNames.RandomTicks, (ulong)tick, GetCounterKey(regionId, 1)));

                for (var ordinal = 0; ordinal < ticksPerRegion; ordinal++)
                {
                    var localIndex = (int)(((long)start + (long)ordinal * stride) % regionArea);
                    var sample = new RandomTickSample(
                        tick,
                        regionId,
                        ordinal,
                        originX + localIndex % regionWidth,
                        originY + localIndex / regionWidth);
                    var updateBlock = update(world, sample);
                    if (updateBlock is { } block)
                    {
                        samples.Add((sample, block));
                    }
                }

                results[regionId] = samples;
            });

        var samplesProcessed = 0;
        var blocksChanged = 0;
        for (var regionId = 0; regionId < results.Length; regionId++)
        {
            var regionResults = results[regionId];
            if (regionResults is null)
            {
                continue;
            }

            samplesProcessed += ticksPerRegion;
            foreach (var (sample, block) in regionResults)
            {
                world.SetBlockOverride(
                    new BlockCoordinate(sample.X, sample.Y, world.GetSurfaceColumn(sample.X, sample.Y).SurfaceZ),
                    block);
                blocksChanged++;
            }
        }

        return new RandomTickResult(samplesProcessed, blocksChanged);
    }

    private static (int Width, int Height, int OriginX, int OriginY) GetRegionBounds(
        WorldGrid world,
        int regionId)
    {
        var originX = (regionId % world.RegionCountX) * world.RegionSize;
        var originY = (regionId / world.RegionCountX) * world.RegionSize;
        return (
            Math.Min(world.RegionSize, world.Width - originX),
            Math.Min(world.RegionSize, world.Height - originY),
            originX,
            originY);
    }

    private static ulong GetCounterKey(int regionId, ulong purpose) =>
        ((ulong)(uint)regionId << 32) | purpose;

    private static int GetCoprimeStride(int area, ulong value)
    {
        if (area <= 1)
        {
            return 1;
        }

        var stride = (int)(value % (ulong)(area - 1)) + 1;
        while (GreatestCommonDivisor(stride, area) != 1)
        {
            stride = stride == area - 1 ? 1 : stride + 1;
        }

        return stride;
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left;
    }
}
