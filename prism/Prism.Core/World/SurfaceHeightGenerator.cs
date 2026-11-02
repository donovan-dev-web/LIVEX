using Prism.Core.Determinism;

namespace Prism.Core.World;

public sealed record SurfaceHeightConfig(
    int BaseHeight = 30,
    int Amplitude = 12,
    int BaseScale = 64,
    int Octaves = 4,
    int PersistencePermille = 500,
    int WarpScale = 128,
    int WarpStrength = 8,
    int BorderWidth = 6,
    int BorderHeightOffset = 8);

public static class SurfaceHeightGenerator
{
    public static byte[] Generate(
        int width,
        int height,
        int verticalSize,
        ulong seed,
        SurfaceHeightConfig? config = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (verticalSize is <= 0 or > byte.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(verticalSize));
        }

        config ??= new SurfaceHeightConfig();
        if (config.BaseHeight < 0 || config.BaseHeight >= verticalSize)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "Base height must be inside the vertical world bounds.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(config.Amplitude);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(config.BaseScale);
        ArgumentOutOfRangeException.ThrowIfNegative(config.WarpStrength);
        ArgumentOutOfRangeException.ThrowIfNegative(config.BorderWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(config.BorderHeightOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(config.WarpScale);
        if (config.Octaves is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "Octave count must be between 1 and 16.");
        }

        if (config.PersistencePermille is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "Persistence must be between 0 and 1000 permille.");
        }

        if ((long)config.BorderWidth * 2 >= Math.Min(width, height))
        {
            throw new ArgumentOutOfRangeException(nameof(config), "Border must leave at least one interior column.");
        }

        if (verticalSize <= config.BorderHeightOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "Vertical world size must leave room below the border height.");
        }

        var heights = new byte[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = x + width * y;
                if (x < config.BorderWidth || y < config.BorderWidth ||
                    x >= width - config.BorderWidth || y >= height - config.BorderWidth)
                {
                    heights[index] = (byte)(verticalSize - config.BorderHeightOffset);
                    continue;
                }

                var noise = config.WarpStrength == 0
                    ? FixedPointNoise.SampleFractal2D(
                        seed, RandomStreamNames.Terrain, x, y, config.BaseScale,
                        config.Octaves, config.PersistencePermille)
                    : FixedPointNoise.SampleWarpedFractal2D(
                        seed, RandomStreamNames.Terrain, x, y, config.BaseScale,
                        config.Octaves, config.PersistencePermille, config.WarpScale, config.WarpStrength);
                var variation = (long)noise * config.Amplitude / (short.MaxValue + 1);
                heights[index] = (byte)Math.Clamp(config.BaseHeight + variation, 0, verticalSize - 1);
            }
        }

        return heights;
    }
}
