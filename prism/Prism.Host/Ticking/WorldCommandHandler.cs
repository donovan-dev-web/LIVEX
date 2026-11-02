using System.Text.Json;
using Prism.Contracts;

namespace Prism.Host.Ticking;

public interface IWorldCommandHandler
{
    string Kind { get; }
    CommandExecutionResult Execute(CommandEnvelope command, WorldTickContext context);
}

public sealed record CommandExecutionResult(
    bool Succeeded,
    string? ErrorCode,
    JsonElement Payload)
{
    public static CommandExecutionResult Success(JsonElement payload) =>
        new(true, null, RequireObjectPayload(payload));

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
