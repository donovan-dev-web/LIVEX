using System.Text.Json;
using Prism.Contracts;

namespace Prism.Host.Ticking;

public interface IWorldCommandHandler
{
    string Kind { get; }

    /// <summary>
    /// Contract-level validation of the command payload, executed before <see cref="Execute"/>.
    /// Handlers that carry a documented payload schema override it; simple handlers accept any object.
    /// </summary>
    bool ValidatePayload(JsonElement payload, out string? detail)
    {
        detail = null;
        return true;
    }

    CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context);

    /// <summary>
    /// Resolves a command whose effect was deferred at scheduling time. Handlers that defer work
    /// override it; the default re-executes the command.
    /// </summary>
    CommandExecutionResult ResolveDeferred(CommandEnvelope command, WorldTickContext context) =>
        Execute(command, context);
}

public sealed record CommandExecutionResult(
    bool Succeeded,
    string? ErrorCode,
    JsonElement Payload,
    bool Deferred = false)
{
    public static CommandExecutionResult Success(JsonElement payload) =>
        new(true, null, RequireObjectPayload(payload));

    public static CommandExecutionResult Scheduled(JsonElement payload) =>
        new(true, null, RequireObjectPayload(payload), Deferred: true);

    public static CommandExecutionResult Failure(string errorCode, JsonElement? payload = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        using var emptyPayload = JsonDocument.Parse("{}");
        return new(false, errorCode, payload is { } value
            ? RequireObjectPayload(value)
            : emptyPayload.RootElement.Clone());
    }

    private static JsonElement RequireObjectPayload(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object
            ? payload.Clone()
            : throw new ArgumentException("Command result payload must be a JSON object.", nameof(payload));
}
