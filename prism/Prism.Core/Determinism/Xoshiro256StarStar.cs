namespace Prism.Core.Determinism;

public sealed class Xoshiro256StarStar
{
    private ulong _state0;
    private ulong _state1;
    private ulong _state2;
    private ulong _state3;

    public Xoshiro256StarStar(ulong seed)
    {
        var splitMixState = seed;
        _state0 = NextSplitMix64(ref splitMixState);
        _state1 = NextSplitMix64(ref splitMixState);
        _state2 = NextSplitMix64(ref splitMixState);
        _state3 = NextSplitMix64(ref splitMixState);
    }

    public ulong NextUInt64()
    {
        var result = RotateLeft(_state1 * 5, 7) * 9;
        var temporary = _state1 << 17;

        _state2 ^= _state0;
        _state3 ^= _state1;
        _state1 ^= _state2;
        _state0 ^= _state3;
        _state2 ^= temporary;
        _state3 = RotateLeft(_state3, 45);

        return result;
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "The exclusive upper bound must be greater than the lower bound.");
        }

        var range = (ulong)((long)maxExclusive - minInclusive);
        var rejectionThreshold = unchecked(0UL - range) % range;
        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value < rejectionThreshold);

        return (int)((long)minInclusive + (long)(value % range));
    }

    private static ulong NextSplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        return DeterministicRandom.Mix64(state);
    }

    private static ulong RotateLeft(ulong value, int offset) =>
        (value << offset) | (value >> (64 - offset));
}
