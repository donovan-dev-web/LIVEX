namespace Prism.Core.Determinism;

public sealed class NamedRandomStreams
{
    private readonly ulong _runSeed;
    private readonly Dictionary<string, Xoshiro256StarStar> _streams = new(StringComparer.Ordinal);

    public NamedRandomStreams(ulong runSeed, IEnumerable<string>? streamNames = null)
    {
        _runSeed = runSeed;
        foreach (var streamName in streamNames ?? RandomStreamNames.All)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
            if (!_streams.TryAdd(
                    streamName,
                    new Xoshiro256StarStar(DeterministicRandom.DeriveStreamSeed(runSeed, streamName))))
            {
                throw new ArgumentException($"Duplicate random stream '{streamName}'.", nameof(streamNames));
            }
        }
    }

    public Xoshiro256StarStar GetStream(string streamName) =>
        _streams.TryGetValue(streamName, out var stream)
            ? stream
            : throw new KeyNotFoundException($"Random stream '{streamName}' was not registered.");

    public ulong GetCounterValue(string streamName, ulong tick, ulong key)
    {
        if (!_streams.ContainsKey(streamName))
        {
            throw new KeyNotFoundException($"Random stream '{streamName}' was not registered.");
        }

        return DeterministicRandom.CounterValue(_runSeed, streamName, tick, key);
    }
}
