using System.Text.Json;
using Prism.Contracts;

namespace Prism.Host.Ticking;

public sealed class OrderedWorldHost : ICheckpointableWorldHost
{
    private readonly object _gate = new();
    private readonly IReadOnlyList<IWorldTickPhaseHandler> _phases;
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

            request = ProtocolJson.Deserialize<TickRequest>(ProtocolJson.Serialize(request));
            var expectedTick = checked(_lastCompletedTick + 1);
            if (request.Tick != expectedTick)
            {
                throw new ArgumentException(
                    $"Expected tick {expectedTick}, received {request.Tick}.",
                    nameof(request));
            }

            var context = new WorldTickContext(request);
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

                    phase.Execute(context);
                }

                var result = context.CreateResult();
                ProtocolJson.Serialize(result);
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
