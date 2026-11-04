using System.Text.Json;
using Prism.Core.Actions;

namespace Prism.Host.Ticking;

/// <summary>
/// A macro step whose action was accepted with a deferred effect. The step stays uncompleted until
/// the dispatch resolves the action at its due tick, so the macro never advances on an unpaid effect.
/// </summary>
public sealed record PendingMacroStep(
    string CommandId,
    MacroStepKind Kind,
    ushort ItemTypeId,
    int Quantity);

/// <summary>
/// The resolved outcome of a deferred action, handed back to the macro phase.
/// </summary>
public sealed record ResolvedMacroStep(
    PendingMacroStep Step,
    bool Succeeded,
    string? ErrorCode,
    JsonElement Payload);

/// <summary>
/// Carries action resolutions from the command dispatch (which resolves deferred effects) to the
/// macro phase (which owns the step that requested the action). Only one action per agent can be in
/// flight because the agent is busy until the deferred effect is due, so expectations are keyed by
/// agent and matched on the command identifier.
/// </summary>
public sealed class DeferredActionResolutions
{
    private const int CheckpointVersion = 1;
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SortedDictionary<ulong, PendingMacroStep> _pending = [];
    private readonly SortedDictionary<ulong, ResolvedMacroStep> _resolved = [];

    public int PendingCount => _pending.Count;
    public int ResolvedCount => _resolved.Count;

    public void Expect(ulong agentId, string commandId, MacroActionStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        if (_pending.ContainsKey(agentId) || _resolved.ContainsKey(agentId))
        {
            throw new InvalidOperationException("The agent already owns a pending deferred action.");
        }

        _pending.Add(agentId, new PendingMacroStep(
            commandId,
            step.Kind,
            step.ItemTypeId,
            step.Quantity));
    }

    public bool IsPending(ulong agentId) => _pending.ContainsKey(agentId);

    /// <summary>
    /// Records the resolution of a deferred action. Resolutions are ignored when no macro step
    /// expects the command, so host-issued deferred actions cost no state.
    /// </summary>
    public void Record(string commandId, CommandExecutionResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(result);
        foreach (var (agentId, pending) in _pending)
        {
            if (!string.Equals(pending.CommandId, commandId, StringComparison.Ordinal))
            {
                continue;
            }

            _resolved[agentId] = new ResolvedMacroStep(
                pending,
                result.Succeeded,
                result.ErrorCode,
                result.Payload.Clone());
            return;
        }
    }

    public bool TryTake(ulong agentId, out ResolvedMacroStep resolved)
    {
        if (_resolved.Remove(agentId, out var found) && found is not null)
        {
            _pending.Remove(agentId);
            resolved = found;
            return true;
        }

        resolved = null!;
        return false;
    }

    /// <summary>
    /// Forgets a pending step that will never resolve, for example when the macro is cancelled.
    /// </summary>
    public bool Abandon(ulong agentId) => _pending.Remove(agentId) | _resolved.Remove(agentId);

    /// <summary>
    /// Drops every entry whose agent no longer drives a macro, so cancellations cannot leave state behind.
    /// </summary>
    public int Prune(IReadOnlyCollection<ulong> activeAgents)
    {
        ArgumentNullException.ThrowIfNull(activeAgents);
        var active = activeAgents.ToHashSet();
        var removed = 0;
        foreach (var agentId in _pending.Keys.Where(id => !active.Contains(id)).ToArray())
        {
            removed += _pending.Remove(agentId) ? 1 : 0;
        }

        foreach (var agentId in _resolved.Keys.Where(id => !active.Contains(id)).ToArray())
        {
            removed += _resolved.Remove(agentId) ? 1 : 0;
        }

        return removed;
    }

    public byte[] CaptureState() =>
        JsonSerializer.SerializeToUtf8Bytes(
            new DeferredResolutionCheckpoint(
                CheckpointVersion,
                _pending.OrderBy(static entry => entry.Key)
                    .Select(static entry => new PendingEntry(entry.Key, entry.Value))
                    .ToArray(),
                _resolved.OrderBy(static entry => entry.Key)
                    .Select(static entry => new ResolvedEntry(entry.Key, entry.Value))
                    .ToArray()),
            CheckpointJsonOptions);

    public void RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        if (_pending.Count != 0 || _resolved.Count != 0)
        {
            throw new InvalidOperationException("Deferred action resolutions can only be restored into an empty channel.");
        }

        var checkpoint = JsonSerializer.Deserialize<DeferredResolutionCheckpoint>(
                             checkpointBytes,
                             CheckpointJsonOptions)
                         ?? throw new InvalidDataException("The deferred action resolution checkpoint is empty.");
        if (checkpoint.Version != CheckpointVersion ||
            checkpoint.Pending is null ||
            checkpoint.Resolved is null)
        {
            throw new InvalidDataException("The deferred action resolution checkpoint version or contents are invalid.");
        }

        foreach (var entry in checkpoint.Pending)
        {
            if (entry is null ||
                entry.Step is null ||
                string.IsNullOrWhiteSpace(entry.Step.CommandId) ||
                !_pending.TryAdd(entry.AgentId, entry.Step))
            {
                throw new InvalidDataException("The deferred action resolution checkpoint contains invalid pending steps.");
            }
        }

        foreach (var entry in checkpoint.Resolved)
        {
            if (entry is null ||
                entry.Resolution is null ||
                entry.Resolution.Step is null ||
                !_pending.ContainsKey(entry.AgentId) ||
                !_resolved.TryAdd(entry.AgentId, entry.Resolution))
            {
                throw new InvalidDataException("The deferred action resolution checkpoint contains invalid resolutions.");
            }
        }
    }

    private sealed record DeferredResolutionCheckpoint(
        int Version,
        PendingEntry[]? Pending,
        ResolvedEntry[]? Resolved);

    private sealed record PendingEntry(ulong AgentId, PendingMacroStep? Step);

    private sealed record ResolvedEntry(ulong AgentId, ResolvedMacroStep? Resolution);
}
