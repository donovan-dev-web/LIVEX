using Prism.Core.Determinism;

namespace Prism.Core.World;

public enum WorldZone : byte
{
    Plains,
    Forest,
    Rocky,
    Shore,
}

public sealed record WorldZoneConfig(
    int SeaLevelZ = 26,
    int ShoreBand = 2,
    int MoistureScale = 96,
    int ForestThresholdPermille = 150,
    int RockScale = 48,
    int RockyThresholdPermille = 650,
    int RockyMinHeight = 42);

public static class WorldZoneGenerator
{
    public static WorldZone[] Generate(
        int width,
        int height,
        ReadOnlySpan<byte> surfaceHeights,
        int verticalSize,
        ulong seed,
        WorldZoneConfig? config = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (verticalSize is <= 0 or > byte.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(verticalSize));
        }

        if (surfaceHeights.Length != checked(width * height))
        {
            throw new ArgumentException("Surface height map length must match world dimensions.", nameof(surfaceHeights));
        }

        config ??= new WorldZoneConfig();
        if ((uint)config.SeaLevelZ >= (uint)verticalSize ||
            config.ShoreBand < 0 || config.ShoreBand > verticalSize ||
            config.MoistureScale <= 0 ||
            config.ForestThresholdPermille is < -1000 or > 1000 ||
            config.RockScale <= 0 ||
            config.RockyThresholdPermille is < -1000 or > 1000 ||
            (uint)config.RockyMinHeight >= (uint)verticalSize)
        {
            throw new ArgumentOutOfRangeException(nameof(config));
        }

        var zones = new WorldZone[surfaceHeights.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = x + width * y;
                var surfaceZ = surfaceHeights[index];
                if (surfaceZ >= verticalSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(surfaceHeights),
                        $"Surface height at ({x}, {y}) is outside the vertical world.");
                }

                if (surfaceZ <= config.SeaLevelZ + config.ShoreBand)
                {
                    zones[index] = WorldZone.Shore;
                    continue;
                }

                var rockiness = FixedPointNoise.SampleValue2D(
                    seed, RandomStreamNames.Terrain + ".zones.rock", x, y, config.RockScale);
                if (surfaceZ >= config.RockyMinHeight ||
                    ToPermille(rockiness) >= config.RockyThresholdPermille)
                {
                    zones[index] = WorldZone.Rocky;
                    continue;
                }

                var moisture = FixedPointNoise.SampleValue2D(
                    seed, RandomStreamNames.Terrain + ".zones.moisture", x, y, config.MoistureScale);
                zones[index] = ToPermille(moisture) >= config.ForestThresholdPermille
                    ? WorldZone.Forest
                    : WorldZone.Plains;
            }
        }

        return zones;
    }

    private static int ToPermille(short value) =>
        (int)((long)value * 1000 / (short.MaxValue + 1));
}
