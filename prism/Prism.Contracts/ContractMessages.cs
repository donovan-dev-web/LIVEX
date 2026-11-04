using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prism.Contracts;

public interface IProtocolMessage
{
    string ProtocolVersion { get; }
    long Tick { get; }
}

public sealed record CommandEnvelope(
    string ProtocolVersion,
    long Tick,
    string CommandId,
    ulong AgentId,
    string Kind,
    JsonElement Payload) : IProtocolMessage;

public sealed record EventEnvelope(
    string ProtocolVersion,
    long Tick,
    string EventId,
    string Kind,
    ulong? AgentId,
    JsonElement Payload) : IProtocolMessage;

public sealed record ObservationEnvelope(
    string ProtocolVersion,
    long Tick,
    ulong AgentId,
    JsonElement Changes,
    JsonElement Self) : IProtocolMessage;

public sealed record TickRequest(
    string ProtocolVersion,
    long Tick,
    IReadOnlyList<CommandEnvelope> Commands) : IProtocolMessage;

public sealed record TickResult(
    string ProtocolVersion,
    long Tick,
    IReadOnlyList<ObservationEnvelope> Observations,
    IReadOnlyList<EventEnvelope> Events) : IProtocolMessage;

public static class ProtocolJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static T Deserialize<T>(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var value = JsonSerializer.Deserialize<T>(json, Options)
                    ?? throw new JsonException($"The JSON value could not be deserialized as {typeof(T).Name}.");
        ValidateMessage(value);
        return value;
    }

    public static string Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateMessage(value);
        return JsonSerializer.Serialize(value, Options);
    }

    public static void ValidateEnvelopeVersion(string protocolVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protocolVersion);
        if (!ProtocolVersion.TryParse(protocolVersion, out var version))
        {
            throw new JsonException($"Protocol version '{protocolVersion}' is invalid.");
        }

        if (!version.IsCompatibleWith(ProtocolVersion.Current))
        {
            throw new UnsupportedProtocolVersionException(version, ProtocolVersion.Current);
        }
    }

    private static void ValidateMessage<T>(T value)
    {
        if (value is not IProtocolMessage message)
        {
            return;
        }

        ValidateEnvelopeVersion(message.ProtocolVersion);
        if (message.Tick < 0)
        {
            throw new JsonException("Message tick must be non-negative.");
        }

        switch (value)
        {
            case CommandEnvelope command:
                ValidateCommand(command);
                break;
            case EventEnvelope eventEnvelope:
                ValidateEvent(eventEnvelope);
                break;
            case ObservationEnvelope observation:
                if (observation.Changes.ValueKind != JsonValueKind.Object ||
                    observation.Self.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException("Observation changes and self values must be JSON objects.");
                }

                break;
            case TickRequest request:
                if (request.Commands is null)
                {
                    throw new JsonException("Tick request commands are required.");
                }

                foreach (var command in request.Commands)
                {
                    ValidateCommand(command);
                    if (command.Tick != request.Tick)
                    {
                        throw new JsonException("Every command in a tick request must match the request tick.");
                    }
                }

                break;
            case TickResult result:
                if (result.Observations is null || result.Events is null)
                {
                    throw new JsonException("Tick result observations and events are required.");
                }

                foreach (var observation in result.Observations)
                {
                    ValidateMessage(observation);
                    if (observation.Tick != result.Tick)
                    {
                        throw new JsonException("Every observation in a tick result must match the result tick.");
                    }
                }

                foreach (var eventEnvelope in result.Events)
                {
                    ValidateEvent(eventEnvelope);
                    if (eventEnvelope.Tick != result.Tick)
                    {
                        throw new JsonException("Every event in a tick result must match the result tick.");
                    }
                }

                break;
        }
    }

    private static void ValidateCommand(CommandEnvelope command)
    {
        ValidateEnvelopeVersion(command.ProtocolVersion);
        if (command.Tick < 0 ||
            string.IsNullOrWhiteSpace(command.CommandId) ||
            string.IsNullOrWhiteSpace(command.Kind) ||
            command.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Command envelope fields are invalid or missing.");
        }
    }

    private static void ValidateEvent(EventEnvelope eventEnvelope)
    {
        ValidateEnvelopeVersion(eventEnvelope.ProtocolVersion);
        if (eventEnvelope.Tick < 0 ||
            string.IsNullOrWhiteSpace(eventEnvelope.EventId) ||
            string.IsNullOrWhiteSpace(eventEnvelope.Kind) ||
            eventEnvelope.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Event envelope fields are invalid or missing.");
        }
    }

    private static JsonSerializerOptions CreateOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        };
}

public sealed class UnsupportedProtocolVersionException(
    ProtocolVersion received,
    ProtocolVersion supported)
    : InvalidOperationException(
        $"Protocol major version {received.Major} is unsupported; expected {supported.Major}.")
{
    public ProtocolVersion Received { get; } = received;
    public ProtocolVersion Supported { get; } = supported;
    public string ErrorCode => "protocol_unsupported";
}
