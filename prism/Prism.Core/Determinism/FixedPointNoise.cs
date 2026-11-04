namespace Prism.Core.Determinism;

public static class FixedPointNoise
{
    private const int FractionBits = 16;
    private const long FixedOne = 1L << FractionBits;

    public static short SampleValue2D(
        ulong seed,
        string streamName,
        int x,
        int y,
        int latticeScale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(latticeScale);

        var streamSeed = DeterministicRandom.DeriveStreamSeed(seed, streamName);
        return SampleValue2DAtFrequency(streamSeed, x, y, latticeScale, 1);
    }

    public static FixedPointNoiseSampler CreateSampler(ulong seed, string streamName) =>
        new(DeterministicRandom.DeriveStreamSeed(
            seed, string.IsNullOrWhiteSpace(streamName)
                ? throw new ArgumentException("Noise stream name is required.", nameof(streamName))
                : streamName));


    public static short SampleFractal2D(
        ulong seed,
        string streamName,
        int x,
        int y,
        int baseScale,
        int octaves,
        int persistencePermille)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScale);
        if (octaves is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(octaves), "Octave count must be between 1 and 16.");
        }

        if (persistencePermille is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(persistencePermille));
        }

        long weightedSamples = 0;
        long totalAmplitude = 0;
        var amplitude = 1000;
        var streamSeed = DeterministicRandom.DeriveStreamSeed(seed, streamName);
        for (var octave = 0; octave < octaves && amplitude > 0; octave++)
        {
            var sample = SampleValue2DAtFrequency(streamSeed, x, y, baseScale, 1 << octave);
            weightedSamples += (long)sample * amplitude;
            totalAmplitude += amplitude;
            amplitude = amplitude * persistencePermille / 1000;
        }

        return (short)(weightedSamples / totalAmplitude);
    }

    public static short SampleWarpedFractal2D(
        ulong seed,
        string streamName,
        int x,
        int y,
        int baseScale,
        int octaves,
        int persistencePermille,
        int warpScale,
        int warpStrength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(warpScale);
        ArgumentOutOfRangeException.ThrowIfNegative(warpStrength);

        var warpXSeed = DeterministicRandom.DeriveStreamSeed(seed, $"{streamName}.warpX");
        var warpYSeed = DeterministicRandom.DeriveStreamSeed(seed, $"{streamName}.warpY");
        var offsetX = SampleValue2DAtFrequency(warpXSeed, x, y, warpScale, 1);
        var offsetY = SampleValue2DAtFrequency(warpYSeed, x, y, warpScale, 1);
        var warpedX = ClampCoordinate((long)x + (long)offsetX * warpStrength / (short.MaxValue + 1));
        var warpedY = ClampCoordinate((long)y + (long)offsetY * warpStrength / (short.MaxValue + 1));
        return SampleFractal2D(
            seed, streamName, warpedX, warpedY, baseScale, octaves, persistencePermille);
    }

    internal static short SampleValue2DAtFrequency(
        ulong streamSeed,
        int x,
        int y,
        int latticeScale,
        int frequency)
    {
        var xFixed = ToFixedCoordinate(x, latticeScale, frequency);
        var yFixed = ToFixedCoordinate(y, latticeScale, frequency);
        var x0 = xFixed >> FractionBits;
        var y0 = yFixed >> FractionBits;
        var xFraction = xFixed & (FixedOne - 1);
        var yFraction = yFixed & (FixedOne - 1);
        var xBlend = Smooth(xFraction);
        var yBlend = Smooth(yFraction);

        var top = Lerp(
            LatticeValue(streamSeed, x0, y0),
            LatticeValue(streamSeed, x0 + 1, y0),
            xBlend);
        var bottom = Lerp(
            LatticeValue(streamSeed, x0, y0 + 1),
            LatticeValue(streamSeed, x0 + 1, y0 + 1),
            xBlend);
        return (short)Lerp(top, bottom, yBlend);
    }

    private static long ToFixedCoordinate(int coordinate, int latticeScale, int frequency)
    {
        var numerator = (long)coordinate * FixedOne * frequency;
        var quotient = numerator / latticeScale;
        return numerator < 0 && numerator % latticeScale != 0 ? quotient - 1 : quotient;
    }

    private static short LatticeValue(ulong streamSeed, long x, long y)
    {
        var xHash = DeterministicRandom.Mix64(unchecked((ulong)x) ^ 0x243F6A8885A308D3UL);
        var yHash = DeterministicRandom.Mix64(unchecked((ulong)y) ^ 0x13198A2E03707344UL);
        var coordinateHash = DeterministicRandom.Mix64(xHash ^ RotateLeft(yHash, 1));
        var hash = DeterministicRandom.Mix64(streamSeed ^ coordinateHash);
        return unchecked((short)(hash >> 48));
    }

    internal static long Smooth(long value)
    {
        var squared = value * value / FixedOne;
        return squared * (3 * FixedOne - 2 * value) / FixedOne;
    }

    internal static long Lerp(long from, long to, long amount) =>
        from + ((to - from) * amount >> FractionBits);

    private static int ClampCoordinate(long coordinate) =>
        (int)Math.Clamp(coordinate, int.MinValue, int.MaxValue);

    private static ulong RotateLeft(ulong value, int offset) =>
        (value << offset) | (value >> (64 - offset));
}

public readonly struct FixedPointNoiseSampler(ulong streamSeed)
{
    public short SampleValue2D(int x, int y, int latticeScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(latticeScale);
        return FixedPointNoise.SampleValue2DAtFrequency(streamSeed, x, y, latticeScale, 1);
    }

    public short SampleValue3D(int x, int y, int z, int latticeScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(latticeScale);
        var xFixed = ToFixedCoordinate(x, latticeScale);
        var yFixed = ToFixedCoordinate(y, latticeScale);
        var zFixed = ToFixedCoordinate(z, latticeScale);
        var x0 = xFixed >> 16;
        var y0 = yFixed >> 16;
        var z0 = zFixed >> 16;
        var xBlend = Smooth(xFixed & 0xFFFF);
        var yBlend = Smooth(yFixed & 0xFFFF);
        var zBlend = Smooth(zFixed & 0xFFFF);

        var zLow = Lerp(
            Lerp(LatticeValue(x0, y0, z0), LatticeValue(x0 + 1, y0, z0), xBlend),
            Lerp(LatticeValue(x0, y0 + 1, z0), LatticeValue(x0 + 1, y0 + 1, z0), xBlend),
            yBlend);
        var zHigh = Lerp(
            Lerp(LatticeValue(x0, y0, z0 + 1), LatticeValue(x0 + 1, y0, z0 + 1), xBlend),
            Lerp(LatticeValue(x0, y0 + 1, z0 + 1), LatticeValue(x0 + 1, y0 + 1, z0 + 1), xBlend),
            yBlend);
        return (short)Lerp(zLow, zHigh, zBlend);
    }

    private short LatticeValue(long x, long y, long z)
    {
        var xHash = DeterministicRandom.Mix64(unchecked((ulong)x) ^ 0x243F6A8885A308D3UL);
        var yHash = DeterministicRandom.Mix64(unchecked((ulong)y) ^ 0x13198A2E03707344UL);
        var zHash = DeterministicRandom.Mix64(unchecked((ulong)z) ^ 0xA4093822299F31D0UL);
        var coordinateHash = DeterministicRandom.Mix64(xHash ^ RotateLeft(yHash, 1) ^ RotateLeft(zHash, 2));
        return unchecked((short)(DeterministicRandom.Mix64(streamSeed ^ coordinateHash) >> 48));
    }

    private static long ToFixedCoordinate(int coordinate, int latticeScale)
    {
        var numerator = (long)coordinate << 16;
        var quotient = numerator / latticeScale;
        return numerator < 0 && numerator % latticeScale != 0 ? quotient - 1 : quotient;
    }

    private static long Smooth(long value)
    {
        var squared = value * value >> 16;
        return squared * (3 * 65536 - 2 * value) >> 16;
    }

    private static long Lerp(long from, long to, long amount) =>
        from + ((to - from) * amount >> 16);

    private static ulong RotateLeft(ulong value, int offset) =>
        (value << offset) | (value >> (64 - offset));
}
