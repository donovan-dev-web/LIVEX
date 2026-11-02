using Prism.Contracts;

namespace Prism.Host.Ticking;

public sealed class OrderedWorldHost : IWorldHost
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
}
