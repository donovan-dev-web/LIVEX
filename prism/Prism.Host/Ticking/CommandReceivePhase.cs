using Prism.Contracts;

namespace Prism.Host.Ticking;

public sealed class CommandReceivePhase : IWorldTickPhaseHandler
{
    private readonly CommandIdempotencyWindow<CommandExecutionResult> _idempotencyWindow;
    private readonly Func<CommandEnvelope, string> _fingerprint;

    internal CommandReceivePhase(
        CommandIdempotencyWindow<CommandExecutionResult> idempotencyWindow,
        Func<CommandEnvelope, string> fingerprint)
    {
        _idempotencyWindow = idempotencyWindow;
        _fingerprint = fingerprint;
    }

    public WorldTickPhase Phase => WorldTickPhase.Receive;

    public void Execute(WorldTickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var currentRequestCommands = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var command in context.Request.Commands)
        {
            var fingerprint = _idempotencyWindow.Validate(command, _fingerprint);
            if (currentRequestCommands.TryGetValue(command.CommandId, out var existingFingerprint) &&
                !StringComparer.Ordinal.Equals(existingFingerprint, fingerprint))
            {
                throw new CommandIdConflictException(command.CommandId);
            }

            currentRequestCommands.TryAdd(command.CommandId, fingerprint);
        }
    }
}
