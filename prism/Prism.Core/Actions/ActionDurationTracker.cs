using System.Text.Json;

namespace Prism.Core.Actions;

/// <summary>
/// Tracks the agent occupancy produced by an action duration: an agent that started an action is
/// busy until the action's configured duration has elapsed.
/// </summary>
public sealed record ActiveAgentAction(
    ulong AgentId,
    string Kind,
    string CommandId,
    long StartedTick,
    long BusyUntilTick);

public sealed class ActionDurationTracker
{
    private const int CheckpointVersion = 1;
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SortedDictionary<ulong, ActiveAgentAction> _active = [];

    public int Count => _active.Count;

    public bool IsBusy(ulong agentId, long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        return _active.TryGetValue(agentId, out var action) && action.BusyUntilTick > tick;
    }

    public bool TryGetActive(ulong agentId, long tick, out ActiveAgentAction action)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        if (_active.TryGetValue(agentId, out var found) && found.BusyUntilTick > tick)
        {
            action = found;
            return true;
        }

        action = null!;
        return false;
    }

    public IReadOnlyList<ActiveAgentAction> Snapshot(long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        return _active.Values
            .Where(action => action.BusyUntilTick > tick)
            .ToArray();
    }

    public bool TryBegin(
        ulong agentId,
        string kind,
        string commandId,
        long tick,
        int durationTicks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfNegative(durationTicks);
        if (IsBusy(agentId, tick))
        {
            return false;
        }

        if (durationTicks == 0)
        {
            _active.Remove(agentId);
            return true;
        }

        _active[agentId] = new ActiveAgentAction(
            agentId,
            kind,
            commandId,
            tick,
            checked(tick + durationTicks));
        return true;
    }

    public bool Release(ulong agentId) => _active.Remove(agentId);

    public byte[] CaptureState() =>
        JsonSerializer.SerializeToUtf8Bytes(
            new ActionDurationCheckpoint(
                CheckpointVersion,
                _active.Values.ToArray()),
            CheckpointJsonOptions);

    public void RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        if (_active.Count != 0)
        {
            throw new InvalidOperationException("Action durations can only be restored into a new tracker.");
        }

        var checkpoint = JsonSerializer.Deserialize<ActionDurationCheckpoint>(
                             checkpointBytes,
                             CheckpointJsonOptions)
                         ?? throw new InvalidDataException("Action duration checkpoint is empty.");
        if (checkpoint.Version != CheckpointVersion || checkpoint.Actions is null)
        {
            throw new InvalidDataException("Action duration checkpoint version or contents are invalid.");
        }

        foreach (var action in checkpoint.Actions)
        {
            if (action is null ||
                string.IsNullOrWhiteSpace(action.Kind) ||
                string.IsNullOrWhiteSpace(action.CommandId) ||
                action.StartedTick < 0 ||
                action.BusyUntilTick <= action.StartedTick ||
                !_active.TryAdd(action.AgentId, action))
            {
                throw new InvalidDataException("Action duration checkpoint contains an invalid action.");
            }
        }
    }

    private sealed record ActionDurationCheckpoint(
        int Version,
        ActiveAgentAction[]? Actions);
}
