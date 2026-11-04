using System.Text;

namespace Prism.Core.Determinism;

public static class DeterministicRandom
{
    public static ulong Mix64(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public static ulong HashName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        const ulong offsetBasis = 14_695_981_039_346_656_037UL;
        const ulong prime = 1_099_511_628_211UL;
        var hash = offsetBasis;
        foreach (var value in Encoding.UTF8.GetBytes(name))
        {
            hash = (hash ^ value) * prime;
        }

        return hash;
    }

    public static ulong DeriveStreamSeed(ulong runSeed, string streamName) =>
        Mix64(runSeed ^ HashName(streamName));

    public static ulong CounterValue(ulong runSeed, string streamName, ulong tick, ulong key)
    {
        var streamSeed = DeriveStreamSeed(runSeed, streamName);
        return Mix64(streamSeed ^ Mix64(tick) ^ Mix64(key));
    }
}
