namespace Prism.Contracts;

public sealed class CommandIdempotencyWindow<TResult>
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<string, CachedResult> _results = new(StringComparer.Ordinal);
    private readonly Queue<string> _insertionOrder = [];
    private readonly HashSet<string> _executing = new(StringComparer.Ordinal);

    public CommandIdempotencyWindow(int capacity = 100_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _results.Count;
            }
        }
    }

    public string Validate(
        CommandEnvelope command,
        Func<CommandEnvelope, string>? fingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        var commandFingerprint = GetFingerprint(command, fingerprint);
        lock (_gate)
        {
            ThrowIfConflicting(command, commandFingerprint);
        }

        return commandFingerprint;
    }

    public TResult ExecuteOnce(
        CommandEnvelope command,
        Func<TResult> execute,
        Func<CommandEnvelope, string>? fingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(execute);

        var commandFingerprint = GetFingerprint(command, fingerprint);

        lock (_gate)
        {
            ThrowIfConflicting(command, commandFingerprint);
            if (_results.TryGetValue(command.CommandId, out var cached))
            {
                return ProtocolJson.Deserialize<TResult>(cached.SerializedResult);
            }

            if (!_executing.Add(command.CommandId))
            {
                throw new InvalidOperationException($"Command '{command.CommandId}' is already being executed.");
            }

            string serializedResult;
            try
            {
                serializedResult = ProtocolJson.Serialize(execute());
            }
            finally
            {
                _executing.Remove(command.CommandId);
            }

            _results.Add(command.CommandId, new CachedResult(commandFingerprint, serializedResult));
            _insertionOrder.Enqueue(command.CommandId);
            while (_results.Count > _capacity)
            {
                var expiredCommandId = _insertionOrder.Dequeue();
                _results.Remove(expiredCommandId);
            }

            return ProtocolJson.Deserialize<TResult>(serializedResult);
        }
    }

    private sealed record CachedResult(string Fingerprint, string SerializedResult);

    private static string GetFingerprint(
        CommandEnvelope command,
        Func<CommandEnvelope, string>? fingerprint)
    {
        var serializedCommand = ProtocolJson.Serialize(command);
        var commandFingerprint = fingerprint is null ? serializedCommand : fingerprint(command);
        if (string.IsNullOrWhiteSpace(commandFingerprint))
        {
            throw new ArgumentException("Command fingerprints must not be empty.", nameof(fingerprint));
        }

        return commandFingerprint;
    }

    private void ThrowIfConflicting(CommandEnvelope command, string commandFingerprint)
    {
        if (_results.TryGetValue(command.CommandId, out var cached) &&
            !StringComparer.Ordinal.Equals(cached.Fingerprint, commandFingerprint))
        {
            throw new CommandIdConflictException(command.CommandId);
        }
    }
}

public sealed class CommandIdConflictException(string commandId)
    : InvalidOperationException($"Command id '{commandId}' was reused with a different request.")
{
    public string CommandId { get; } = commandId;
    public string ErrorCode => "command_id_conflict";
}
