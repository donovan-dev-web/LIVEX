using System.Diagnostics;
using System.Text.Json;
using Prism.Contracts;

namespace Prism.Host.Ticking;

public sealed class OrderedWorldHost : ICheckpointableWorldHost
{
    /// <summary>
    /// Opt-in per-phase profiling. Off by default and free when off; enable with
    /// <c>PRISM_TICK_PROFILE=1</c> to see where a tick spends its time (perf runs, R1/R2).
    /// </summary>
    private static readonly bool TickProfiling =
        Environment.GetEnvironmentVariable("PRISM_TICK_PROFILE") == "1";

    /// <summary>
    /// Relevé JSONL par tick (<c>PRISM_BENCHMARK_JSON</c>) : la forme rejouable en
    /// CI de la même mesure (PRISM-106). Nul quand il n'est pas demandé.
    /// </summary>
    private static readonly TickBenchmarkRecorder? BenchmarkRecorder = TickBenchmarkRecorder.Current;

    private readonly object _gate = new();
    private readonly IReadOnlyList<IWorldTickPhaseHandler> _phases;
    private readonly IPathfindingMetricsSource? _pathfindingSource;
    private long _lastCompletedTick = -1;
    private bool _faulted;
    private bool _stepping;

    public OrderedWorldHost(IEnumerable<IWorldTickPhaseHandler> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        var registeredPhases = phases.ToArray();
        if (registeredPhases.Any(static handler => handler is null))
        {
            throw new ArgumentException("World tick phase handlers must not contain null entries.", nameof(phases));
        }

        var expectedPhases = Enum.GetValues<WorldTickPhase>();
        if (registeredPhases.Length != expectedPhases.Length ||
            registeredPhases.Select(static handler => handler.Phase).Distinct().Count() != expectedPhases.Length ||
            registeredPhases.Any(handler => !Enum.IsDefined(handler.Phase)))
        {
            throw new ArgumentException("Exactly one handler is required for every world tick phase.", nameof(phases));
        }

        _phases = registeredPhases.OrderBy(static handler => handler.Phase).ToArray();
        _pathfindingSource = registeredPhases.OfType<IPathfindingMetricsSource>().FirstOrDefault();
    }

    public long LastCompletedTick
    {
        get
        {
            lock (_gate)
            {
                return _lastCompletedTick;
            }
        }
    }

    public bool IsFaulted
    {
        get
        {
            lock (_gate)
            {
                return _faulted;
            }
        }
    }

    public byte[] CaptureCheckpoint()
    {
        lock (_gate)
        {
            if (_stepping || _faulted)
            {
                throw new InvalidOperationException("A checkpoint requires a stable, healthy world host.");
            }

            var phaseStates = _phases
                .OfType<ICheckpointableWorldTickPhaseHandler>()
                .ToDictionary(
                    static phase => phase.Phase.ToString(),
                    static phase => phase.CaptureState(),
                    StringComparer.Ordinal);
            return JsonSerializer.SerializeToUtf8Bytes(
                new WorldHostCheckpoint(1, _lastCompletedTick, phaseStates),
                ProtocolJson.Options);
        }
    }

    public void RestoreCheckpoint(ReadOnlySpan<byte> checkpoint)
    {
        lock (_gate)
        {
            if (_stepping || _faulted || _lastCompletedTick != -1)
            {
                throw new InvalidOperationException("A checkpoint can only be restored into a new, healthy world host.");
            }

            var snapshot = JsonSerializer.Deserialize<WorldHostCheckpoint>(checkpoint, ProtocolJson.Options)
                           ?? throw new JsonException("The world host checkpoint is empty.");
            var statefulPhases = _phases.OfType<ICheckpointableWorldTickPhaseHandler>()
                .ToDictionary(static phase => phase.Phase.ToString(), StringComparer.Ordinal);
            if (snapshot.FormatVersion != 1 ||
                snapshot.LastCompletedTick < -1 ||
                snapshot.PhaseStates is null ||
                snapshot.PhaseStates.Keys.Any(key => !statefulPhases.ContainsKey(key)) ||
                statefulPhases.Any(entry =>
                    !snapshot.PhaseStates.ContainsKey(entry.Key) &&
                    entry.Value is not IOptionalCheckpointableWorldTickPhaseHandler))
            {
                throw new JsonException("The world host checkpoint does not match the current phase handlers.");
            }

            try
            {
                foreach (var phase in statefulPhases.OrderBy(
                             entry => Enum.Parse<WorldTickPhase>(entry.Key)))
                {
                    if (snapshot.PhaseStates.TryGetValue(phase.Key, out var state))
                    {
                        phase.Value.RestoreState(state);
                    }
                    else
                    {
                        ((IOptionalCheckpointableWorldTickPhaseHandler)phase.Value).RestoreEmptyState();
                    }
                }

                _lastCompletedTick = snapshot.LastCompletedTick;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }
    }

    public TickResult Step(TickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (_faulted)
            {
                throw new InvalidOperationException("The world host is faulted and cannot advance.");
            }

            if (_stepping)
            {
                throw new InvalidOperationException("A world tick cannot be started recursively.");
            }

            bool measuring = TickProfiling || BenchmarkRecorder is not null;
            long stepStarted = measuring ? Stopwatch.GetTimestamp() : 0;
            BenchmarkRecorder?.BeginTick(request.Tick);
            long roundtripStarted = stepStarted;
            long requestAllocatedStart = measuring
                ? GC.GetTotalAllocatedBytes(precise: false)
                : 0;
            request = ProtocolJson.Deserialize<TickRequest>(ProtocolJson.Serialize(request));
            double roundtripMs = TickProfiling
                ? (Stopwatch.GetTimestamp() - roundtripStarted) * 1000.0 / Stopwatch.Frequency
                : 0.0;
            BenchmarkRecorder?.RecordRequest(
                roundtripMs,
                measuring ? GC.GetTotalAllocatedBytes(precise: false) - requestAllocatedStart : 0);
            var expectedTick = checked(_lastCompletedTick + 1);
            if (request.Tick != expectedTick)
            {
                throw new ArgumentException(
                    $"Expected tick {expectedTick}, received {request.Tick}.",
                    nameof(request));
            }

            var context = new WorldTickContext(request);
            var pathfindingAtStart = _pathfindingSource?.PathfindingTotals ?? default;
            var completed = false;
            var worldExecutionStarted = false;
            _stepping = true;
            try
            {
                foreach (var phase in _phases)
                {
                    if (phase.Phase != WorldTickPhase.Receive)
                    {
                        worldExecutionStarted = true;
                    }

                    if (!measuring)
                    {
                        phase.Execute(context);
                        continue;
                    }

                    long phaseAllocatedStart = GC.GetTotalAllocatedBytes(precise: false);
                    var phaseStarted = Stopwatch.GetTimestamp();
                    phase.Execute(context);
                    double phaseMs = Stopwatch.GetElapsedTime(phaseStarted).TotalMilliseconds;
                    long phaseAllocated = GC.GetTotalAllocatedBytes(precise: false) - phaseAllocatedStart;
                    BenchmarkRecorder?.RecordPhase(phase.Phase, phaseMs, phaseAllocated);
                    if (TickProfiling)
                    {
                        Console.Error.WriteLine(string.Create(
                            System.Globalization.CultureInfo.InvariantCulture,
                            $"[profile] phase {phase.Phase}: {phaseMs:F1} ms, {phaseAllocated / 1024 / 1024.0:F1} Mo"));
                    }
                }

                long resultStarted = measuring ? Stopwatch.GetTimestamp() : 0;
                long outputAllocatedStart = measuring
                    ? GC.GetTotalAllocatedBytes(precise: false)
                    : 0;
                var result = context.CreateResult();
                // Une seule sérialisation, en octets : c'est le corps de réponse de
                // la pagination, et c'est la mesure. La chaîne intermédiaire coûtait
                // une passe complète d'encodage et ~82 Mo à 1 000 agents (PRISM-141).
                byte[] payload = ProtocolJson.SerializeToUtf8Bytes(result);
                result = result with { SerializedUtf8 = payload };
                double resultMs = measuring
                    ? (Stopwatch.GetTimestamp() - resultStarted) * 1000.0 / Stopwatch.Frequency
                    : 0.0;
                long outputAllocated = measuring
                    ? GC.GetTotalAllocatedBytes(precise: false) - outputAllocatedStart
                    : 0;
                BenchmarkRecorder?.RecordOutput(
                    resultMs,
                    payload.Length,
                    result.Observations.Count,
                    result.Events.Count,
                    outputAllocated);
                var pathfinding = _pathfindingSource is null
                    ? default
                    : _pathfindingSource.PathfindingTotals - pathfindingAtStart;
                BenchmarkRecorder?.RecordPathfinding(pathfinding);
                if (TickProfiling && _pathfindingSource is not null)
                {
                    Console.Error.WriteLine(string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"[profile] pathfinding tick {request.Tick}: {pathfinding.Searches} recherche(s), " +
                        $"{pathfinding.CacheHits} cache, {pathfinding.CacheInvalidations} périmée(s), " +
                        $"{pathfinding.ExpandedNodes} nœuds, {pathfinding.FailedSearches} échec(s), " +
                        $"{pathfinding.BudgetExhaustedSearches} budget(s) épuisé(s)"));
                }

                if (measuring)
                {
                    BenchmarkRecorder?.CompleteTick(
                        request.Tick,
                        (Stopwatch.GetTimestamp() - stepStarted) * 1000.0 / Stopwatch.Frequency);
                }

                if (TickProfiling)
                {
                    // Ligne compacte : le total du tick dit si le coût restant est
                    // dans le protocole (aller-retour de requête, sérialisation et
                    // taille de la réponse) plutôt que dans les phases du monde.
                    double stepMs = (Stopwatch.GetTimestamp() - stepStarted) * 1000.0 / Stopwatch.Frequency;
                    Console.Error.WriteLine(string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"[profile] tick {request.Tick} total {stepMs:F1} ms (requête {roundtripMs:F1}, " +
                        $"résultat {resultMs:F1}, {payload.Length} octets, {result.Observations.Count} obs, " +
                        $"{result.Events.Count} évén.)"));
                }

                _lastCompletedTick = request.Tick;
                completed = true;
                return result;
            }
            finally
            {
                _stepping = false;
                if (!completed && worldExecutionStarted)
                {
                    _faulted = true;
                }
            }
        }
    }

    private sealed record WorldHostCheckpoint(
        int FormatVersion,
        long LastCompletedTick,
        Dictionary<string, JsonElement> PhaseStates);
}
