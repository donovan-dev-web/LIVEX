using Prism.Contracts;

namespace Prism.Host.Ticking;

public sealed class WorldTickContext
{
    private readonly List<ObservationEnvelope> _observations = [];
    private readonly List<EventEnvelope> _events = [];
    private readonly List<Action> _worldWrites = [];

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

    internal void QueueWorldWrite(Action write)
    {
        ArgumentNullException.ThrowIfNull(write);
        _worldWrites.Add(write);
    }

    internal void ApplyWorldWrites()
    {
        foreach (var write in _worldWrites)
        {
            write();
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
