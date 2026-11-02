using System.Text.Json;
using Prism.Contracts;

namespace Prism.Host.Ticking;

public sealed class CommandDispatchPhase : IWorldTickPhaseHandler
{
    private readonly IReadOnlyDictionary<string, IWorldCommandHandler> _handlers;
    private readonly CommandIdempotencyWindow<CommandExecutionResult> _idempotencyWindow;

    public CommandDispatchPhase(
        IEnumerable<IWorldCommandHandler> handlers,
        CommandIdempotencyWindow<CommandExecutionResult>? idempotencyWindow = null)
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
        ReceivePhase = new CommandReceivePhase(_idempotencyWindow, CreateFingerprint);
    }

    public CommandReceivePhase ReceivePhase { get; }
    public WorldTickPhase Phase => WorldTickPhase.StartOrCancelActions;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var emittedCommands = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in context.Request.Commands)
        {
            var result = _idempotencyWindow.ExecuteOnce(
                command,
                () => _handlers.TryGetValue(command.Kind, out var handler)
                    ? handler.Execute(command, context)
                    : CommandExecutionResult.Failure("unknown_command"),
                CreateFingerprint);

            if (emittedCommands.Add(command.CommandId))
            {
                context.AddEvent(CreateEvent(command, context.Tick, result));
            }
        }
    }

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
            result.Succeeded ? "action_completed" : "action_failed",
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
