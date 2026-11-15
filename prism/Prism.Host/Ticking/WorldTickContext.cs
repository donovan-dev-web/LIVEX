using Prism.Contracts;

namespace Prism.Host.Ticking;

public sealed class WorldTickContext
{
    private readonly List<ObservationEnvelope> _observations = [];
    private readonly List<EventEnvelope> _events = [];
    private readonly List<(Action Write, string Label)> _worldWrites = [];

    internal WorldTickContext(TickRequest request) => Request = request;

    public TickRequest Request { get; }
    public long Tick => Request.Tick;

    public void AddObservation(ObservationEnvelope observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Tick != Tick)
        {
            throw new ArgumentException("Observation tick must match the active world tick.", nameof(observation));
        }

        _observations.Add(observation);
    }

    public void AddEvent(EventEnvelope eventEnvelope)
    {
        ArgumentNullException.ThrowIfNull(eventEnvelope);
        if (eventEnvelope.Tick != Tick)
        {
            throw new ArgumentException("Event tick must match the active world tick.", nameof(eventEnvelope));
        }

        _events.Add(eventEnvelope);
    }

    internal void QueueWorldWrite(Action write, string label = "")
    {
        ArgumentNullException.ThrowIfNull(write);
        _worldWrites.Add((write, label));
    }

    private static readonly bool WriteProfiling =
        Environment.GetEnvironmentVariable("PRISM_TICK_PROFILE") == "1";

    /// <summary>
    /// Relevé JSONL par tick (<c>PRISM_BENCHMARK_JSON</c>) : les écritures
    /// différées sont le poste mesuré ici, et leur allocation avec elles.
    /// </summary>
    private static readonly TickBenchmarkRecorder? BenchmarkRecorder = TickBenchmarkRecorder.Current;

    internal void ApplyWorldWrites()
    {
        if (!WriteProfiling && BenchmarkRecorder is null)
        {
            foreach (var (write, _) in _worldWrites)
            {
                write();
            }

            _worldWrites.Clear();
            return;
        }

        // Profilage opt-in : une écriture lente dit si le coût est dans l'action
        // elle-même ou dans une conséquence globale attachée à la mutation.
        TimeSpan pauseBefore = GC.GetTotalPauseDuration();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
        double totalMs = 0;
        double maxMs = 0;
        int index = 0;
        foreach (var (write, label) in _worldWrites)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            write();
            double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 /
                             System.Diagnostics.Stopwatch.Frequency;
            totalMs += elapsed;
            maxMs = Math.Max(maxMs, elapsed);
            if (WriteProfiling && elapsed >= 50)
            {
                Console.Error.WriteLine(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"[profile] write tick {Tick} #{index} [{label}]: {elapsed:F1} ms"));
            }

            index++;
        }

        TimeSpan pauseGc = GC.GetTotalPauseDuration() - pauseBefore;
        long allocated = GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore;
        BenchmarkRecorder?.RecordWrites(
            Tick,
            _worldWrites.Count,
            totalMs,
            maxMs,
            pauseGc.TotalMilliseconds,
            allocated);
        if (WriteProfiling)
        {
            Console.Error.WriteLine(string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"[profile] writes tick {Tick}: {_worldWrites.Count} écriture(s), total {totalMs:F1} ms, max {maxMs:F1} ms, " +
                $"pause GC {pauseGc.TotalMilliseconds:F1} ms, alloué {allocated / 1024 / 1024} Mo"));
        }

        _worldWrites.Clear();
    }

    internal TickResult CreateResult() =>
        new(
            ProtocolVersion.Current.ToString(),
            Tick,
            _observations.ToArray(),
            _events.ToArray());
}
