using System.Text.Json;
using Prism.Contracts;
using Prism.Core.Actions;

namespace Prism.Host.Ticking;

public sealed class CommandDispatchPhase : ICheckpointableWorldTickPhaseHandler
{
    private const int DispatchCheckpointVersion = 1;
    private readonly IReadOnlyDictionary<string, IWorldCommandHandler> _handlers;
    private readonly CommandIdempotencyWindow<CommandExecutionResult> _idempotencyWindow;
    private readonly ActionDurationTracker? _durations;
    private readonly SortedDictionary<ulong, PendingAction> _pending = [];

    public CommandDispatchPhase(
        IEnumerable<IWorldCommandHandler> handlers,
        CommandIdempotencyWindow<CommandExecutionResult>? idempotencyWindow = null,
        ActionDurationTracker? durations = null)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        var handlerMap = new Dictionary<string, IWorldCommandHandler>(StringComparer.Ordinal);
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentException.ThrowIfNullOrWhiteSpace(handler.Kind);
            if (!handlerMap.TryAdd(handler.Kind, handler))
            {
                throw new ArgumentException($"A handler for command kind '{handler.Kind}' is already registered.", nameof(handlers));
            }
        }

        _handlers = handlerMap;
        _idempotencyWindow = idempotencyWindow ?? new CommandIdempotencyWindow<CommandExecutionResult>();
        _durations = durations;
        ReceivePhase = new CommandReceivePhase(_idempotencyWindow, CreateFingerprint);
    }

    public CommandReceivePhase ReceivePhase { get; }
    public WorldTickPhase Phase => WorldTickPhase.StartOrCancelActions;

    public JsonElement CaptureState() =>
        JsonSerializer.SerializeToElement(
            new DispatchCheckpoint(
                DispatchCheckpointVersion,
                _idempotencyWindow.SnapshotEntries(),
                _durations?.CaptureState(),
                _pending.Values.ToArray()),
            ProtocolJson.Options);

    public void RestoreState(JsonElement state)
    {
        if (state.ValueKind == JsonValueKind.Array)
        {
            _idempotencyWindow.RestoreEntries(
                state.Deserialize<IdempotencyCacheEntry<CommandExecutionResult>[]>(ProtocolJson.Options)
                ?? throw new JsonException("The command idempotency checkpoint is empty."));
            return;
        }

        var checkpoint = state.Deserialize<DispatchCheckpoint>(ProtocolJson.Options)
                         ?? throw new JsonException("The command dispatch checkpoint is empty.");
        if (checkpoint.Version is not (1 or DispatchCheckpointVersion))
        {
            throw new JsonException("The command dispatch checkpoint version is unsupported.");
        }

        _idempotencyWindow.RestoreEntries(checkpoint.Entries ?? []);
        if (checkpoint.Durations is { } durations)
        {
            if (_durations is null)
            {
                throw new JsonException(
                    "The command dispatch checkpoint carries action durations but no tracker is configured.");
            }

            _durations.RestoreState(durations);
        }

        foreach (var pending in checkpoint.Pending ?? [])
        {
            if (pending is null ||
                string.IsNullOrWhiteSpace(pending.CommandId) ||
                string.IsNullOrWhiteSpace(pending.Kind) ||
                pending.ResolveTick <= pending.StartedTick ||
                !_pending.TryAdd(pending.AgentId, pending))
            {
                throw new JsonException("The command dispatch checkpoint contains invalid pending actions.");
            }
        }
    }

    private sealed record DispatchCheckpoint(
        int Version,
        IReadOnlyList<IdempotencyCacheEntry<CommandExecutionResult>>? Entries,
        byte[]? Durations,
        PendingAction[]? Pending = null);

    private sealed record PendingAction(
        string CommandId,
        string Kind,
        ulong AgentId,
        long StartedTick,
        long ResolveTick,
        JsonElement Payload);

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ResolveDueActions(context);
        var emittedCommands = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in context.Request.Commands)
        {
            var cancelled = command.Kind == "Annuler" && _pending.Remove(command.AgentId);
            if (cancelled)
            {
                _durations?.Release(command.AgentId);
            }

            var result = _idempotencyWindow.ExecuteOnce(
                command,
                () => _handlers.TryGetValue(command.Kind, out var handler)
                    ? handler.ValidatePayload(command.Payload, out _)
                        ? handler.Execute(command, context)
                        : CommandExecutionResult.Failure("invalid_payload")
                    : CommandExecutionResult.Failure("unknown_command"),
                CreateFingerprint);
            if (cancelled && !result.Succeeded && result.ErrorCode == "nothing_to_cancel")
            {
                result = CommandExecutionResult.Success(JsonSerializer.SerializeToElement(
                    new { cancelled = true },
                    ProtocolJson.Options));
            }

            if (result.Deferred)
            {
                _pending[command.AgentId] = new PendingAction(
                    command.CommandId,
                    command.Kind,
                    command.AgentId,
                    command.Tick,
                    ReadResolveTick(command, result.Payload),
                    command.Payload.Clone());
            }

            if (emittedCommands.Add(command.CommandId))
            {
                context.AddEvent(CreateEvent(command, context.Tick, result));
            }
        }
    }

    private void ResolveDueActions(WorldTickContext context)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var due = _pending.Values
            .Where(pending => pending.ResolveTick <= context.Tick)
            .OrderBy(static pending => pending.AgentId)
            .ToArray();
        foreach (var pending in due)
        {
            _pending.Remove(pending.AgentId);
            var command = new CommandEnvelope(
                ProtocolVersion.Current.ToString(),
                context.Tick,
                pending.CommandId,
                pending.AgentId,
                pending.Kind,
                pending.Payload);
            var result = _handlers.TryGetValue(pending.Kind, out var handler)
                ? handler.ResolveDeferred(command, context)
                : CommandExecutionResult.Failure("unknown_command");
            context.AddEvent(CreateEvent(command, context.Tick, result));
        }
    }

    private static long ReadResolveTick(CommandEnvelope command, JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty("busyUntilTick", out var busyUntil) &&
        busyUntil.ValueKind == JsonValueKind.Number &&
        busyUntil.TryGetInt64(out var resolveTick) &&
        resolveTick > command.Tick
            ? resolveTick
            : checked(command.Tick + 1);

    private static string CreateFingerprint(CommandEnvelope command) =>
        ProtocolJson.Serialize(new CommandFingerprint(
            command.ProtocolVersion,
            command.AgentId,
            command.Kind,
            command.Payload));

    private static EventEnvelope CreateEvent(
        CommandEnvelope command,
        long tick,
        CommandExecutionResult result)
    {
        var payload = JsonSerializer.SerializeToElement(
            new CommandEventPayload(command.CommandId, result.ErrorCode, result.Payload),
            ProtocolJson.Options);
        return new EventEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            $"{command.CommandId}:{tick}",
            result.Deferred
                ? "action_started"
                : result.Succeeded
                    ? "action_completed"
                    : "action_failed",
            command.AgentId,
            payload);
    }

    private sealed record CommandFingerprint(
        string ProtocolVersion,
        ulong AgentId,
        string Kind,
        JsonElement Payload);

    private sealed record CommandEventPayload(
        string CommandId,
        string? Reason,
        JsonElement Result);
}
