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

/// <summary>
/// Une observation d'agent : ses changements et son état propre.
/// </summary>
/// <remarks>
/// <para>
/// Les deux charges utiles peuvent être portées de deux façons. Un lecteur du
/// protocole les reçoit sous forme de <see cref="JsonElement"/>, exactement comme
/// avant. L'hôte, lui, les produit à partir des objets typés du tick : le payload
/// typé est sérialisé directement vers le flux de sortie, sans jamais construire
/// de DOM. C'était le poste dominant du tick à 1 000 agents : un
/// <c>SerializeToElement</c> par observation et par tick valait 164 Mo de
/// documents JSON alloués pour 41 Mo de charge utile réellement émise
/// (PRISM-141).
/// </para>
/// <para>
/// Le JSON produit est identique dans les deux cas : les propriétés gardent leur
/// ordre et leur nom camelCase, et le convertisseur écrit la charge utile typée
/// avec les mêmes options que la sérialisation par réflexion. Une empreinte
/// d'octets ne bouge donc pas ; seule l'allocation disparaît.
/// </para>
/// </remarks>
[JsonConverter(typeof(ObservationEnvelopeConverter))]
public sealed record ObservationEnvelope : IProtocolMessage
{
    private readonly JsonElement _changes;
    private readonly JsonElement _self;
    private readonly object? _changesPayload;
    private readonly object? _selfPayload;

    /// <summary>Observation lue du protocole : ses charges utiles sont déjà du JSON.</summary>
    public ObservationEnvelope(
        string protocolVersion,
        long tick,
        ulong agentId,
        JsonElement changes,
        JsonElement self)
    {
        ProtocolVersion = protocolVersion;
        Tick = tick;
        AgentId = agentId;
        _changes = changes;
        _self = self;
    }

    /// <summary>
    /// Observation produite par l'hôte : les charges utiles typées seront
    /// sérialisées à la sortie, sans passer par un <see cref="JsonElement"/>.
    /// </summary>
    public ObservationEnvelope(
        string protocolVersion,
        long tick,
        ulong agentId,
        object changesPayload,
        object selfPayload)
    {
        ArgumentNullException.ThrowIfNull(changesPayload);
        ArgumentNullException.ThrowIfNull(selfPayload);
        ProtocolVersion = protocolVersion;
        Tick = tick;
        AgentId = agentId;
        _changesPayload = changesPayload;
        _selfPayload = selfPayload;
    }

    public string ProtocolVersion { get; init; }

    public long Tick { get; init; }

    public ulong AgentId { get; init; }

    /// <summary>
    /// Changements de l'agent. Un payload typé n'est matérialisé en DOM qu'au
    /// premier accès d'un consommateur in-process (empreinte, viewer, test) :
    /// le chemin de sortie de l'hôte ne l'appelle jamais.
    /// </summary>
    public JsonElement Changes
    {
        get => _changesPayload is null
            ? _changes
            : JsonSerializer.SerializeToElement(_changesPayload, _changesPayload.GetType(), ProtocolJson.Options);
        init
        {
            _changes = value;
            _changesPayload = null;
        }
    }

    /// <summary>État propre de l'agent ; voir <see cref="Changes"/> pour le payload typé.</summary>
    public JsonElement Self
    {
        get => _selfPayload is null
            ? _self
            : JsonSerializer.SerializeToElement(_selfPayload, _selfPayload.GetType(), ProtocolJson.Options);
        init
        {
            _self = value;
            _selfPayload = null;
        }
    }

    /// <summary>
    /// Charge utile typique telle quelle, sans matérialiser de DOM : le convertisseur de sortie
    /// l'écrit directement (PRISM-141). <c>null</c> quand l'observation vient du protocole.
    /// </summary>
    internal object? ChangesPayload => _changesPayload;

    internal object? SelfPayload => _selfPayload;

    /// <summary>Charge utile déjà en JSON, sans matérialiser un payload typé.</summary>
    internal JsonElement ChangesElement => _changes;

    internal JsonElement SelfElement => _self;

    /// <summary>
    /// Vrai quand les deux charges utiles sont des objets JSON, sans matérialiser
    /// un payload typé. La validation du protocole s'en sert pour rester gratuite
    /// sur le chemin chaud.
    /// </summary>
    internal bool HasObjectPayloads =>
        IsObjectPayload(_changes, _changesPayload) && IsObjectPayload(_self, _selfPayload);

    private static bool IsObjectPayload(JsonElement element, object? payload) =>
        payload is JsonElement typedElement
            ? typedElement.ValueKind == JsonValueKind.Object
            : payload is not null || element.ValueKind == JsonValueKind.Object;
}

public sealed record TickRequest(
    string ProtocolVersion,
    long Tick,
    IReadOnlyList<CommandEnvelope> Commands) : IProtocolMessage;

public sealed record TickResult(
    string ProtocolVersion,
    long Tick,
    IReadOnlyList<ObservationEnvelope> Observations,
    IReadOnlyList<EventEnvelope> Events) : IProtocolMessage
{
    /// <summary>
    /// Sérialisation UTF-8 déjà produite par l'hôte pour ce résultat, s'il en a
    /// une. La pagination la réutilise au lieu de sérialiser une seconde fois :
    /// une seule passe de 41 Mo au lieu de deux. Elle n'appartient pas au
    /// protocole et n'est jamais écrite ni relue du JSON.
    /// </summary>
    [JsonIgnore]
    public byte[]? SerializedUtf8 { get; init; }
}

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

    /// <summary>
    /// Désérialise un message depuis un élément déjà analysé : un lecteur qui doit
    /// d'abord reconnaître la forme d'une réponse de tick (résultat complet ou
    /// première page, PRISM-142) n'analyse ainsi le JSON qu'une seule fois.
    /// </summary>
    public static T Deserialize<T>(JsonElement element)
    {
        var value = element.Deserialize<T>(Options)
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

    /// <summary>
    /// Sérialise un message directement en UTF-8, sans chaîne intermédiaire.
    /// </summary>
    /// <remarks>
    /// Le flux de sortie de l'hôte n'a besoin que d'octets : passer par une
    /// <see cref="string"/> allouait une copie UTF-16 du tick entier (~82 Mo de
    /// chaîne pour 41 Mo de JSON à 1 000 agents), qu'il fallait ensuite réencoder.
    /// Mêmes options, même validation, mêmes octets que <see cref="Serialize{T}"/>.
    /// </remarks>
    public static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateMessage(value);
        return JsonSerializer.SerializeToUtf8Bytes(value, Options);
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
                // Sans ce chemin court, lire `Changes`/`Self` matérialisait un DOM
                // par observation et par tick (164 Mo de documents pour 41 Mo de
                // charge utile à 1 000 agents) — précisément ce que le payload typé
                // de PRISM-141 évite. Le convertisseur a déjà validé la forme à
                // l'écriture ; ici on ne paie que la présence des deux objets.
                if (!observation.HasObjectPayloads)
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
            case TickPageEnvelope paged:
                ValidateTickPage(paged);
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

    /// <summary>
    /// Une page de tick ne peut pas se faire passer pour un tick complet : elle
    /// doit annoncer sa position dans une séquence connue, et un curseur est
    /// exigé dès qu'il reste des pages (PRISM-142, ADR-014).
    /// </summary>
    private static void ValidateTickPage(TickPageEnvelope paged)
    {
        if (paged.Page is null || paged.Chunk is null)
        {
            throw new JsonException("Tick page position and chunk are required.");
        }

        if (paged.Chunk.Observations is null || paged.Chunk.Events is null)
        {
            throw new JsonException("Tick page chunk observations and events are required.");
        }

        if (paged.Page.Index < 0 ||
            paged.Page.PageCount < 1 ||
            paged.Page.Index >= paged.Page.PageCount)
        {
            throw new JsonException("Tick page index and page count are inconsistent.");
        }

        if (paged.Page.Complete != (paged.Page.Index == paged.Page.PageCount - 1))
        {
            throw new JsonException("Only the last tick page may be flagged complete.");
        }

        if (paged.Page.Complete && paged.Page.Cursor is not null)
        {
            throw new JsonException("A complete tick page must not carry a cursor.");
        }

        if (!paged.Page.Complete && string.IsNullOrWhiteSpace(paged.Page.Cursor))
        {
            throw new JsonException("An incomplete tick page must carry the next cursor.");
        }

        foreach (var observation in paged.Chunk.Observations)
        {
            ValidateMessage(observation);
            if (observation.Tick != paged.Tick)
            {
                throw new JsonException("Every observation of a tick page must match the page tick.");
            }
        }

        foreach (var eventEnvelope in paged.Chunk.Events)
        {
            ValidateEvent(eventEnvelope);
            if (eventEnvelope.Tick != paged.Tick)
            {
                throw new JsonException("Every event of a tick page must match the page tick.");
            }
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
