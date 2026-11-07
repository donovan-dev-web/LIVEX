using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Prism.Contracts;

namespace Simulation.Console.Integration;

public sealed record PrismReadStreamEnvelope(
    string ProtocolVersion,
    long Sequence,
    long Tick,
    string Kind,
    JsonElement Payload)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<PrismAgentObservation> GetAgentObservations()
    {
        if (Kind != "tick_result" ||
            Payload.ValueKind != JsonValueKind.Object ||
            !Payload.TryGetProperty("observations", out var observations) ||
            observations.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The read envelope does not contain a tick_result observation list.");
        }

        var result = new List<PrismAgentObservation>(observations.GetArrayLength());
        foreach (var item in observations.EnumerateArray())
        {
            var observation = item.Deserialize<PrismAgentObservation>(JsonOptions)
                             ?? throw new JsonException("PRISM returned an empty agent observation.");
            if (!Prism.Contracts.ProtocolVersion.TryParse(observation.ProtocolVersion, out var version) ||
                !version.IsCompatibleWith(Prism.Contracts.ProtocolVersion.Current) ||
                observation.Tick != Tick ||
                observation.Changes.ValueKind != JsonValueKind.Object ||
                observation.Self.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("PRISM returned an invalid or misaligned agent observation.");
            }

            result.Add(observation with
            {
                Changes = observation.Changes.Clone(),
                Self = observation.Self.Clone(),
            });
        }

        return result;
    }
}

public sealed record PrismAgentObservation(
    string ProtocolVersion,
    long Tick,
    ulong AgentId,
    JsonElement Changes,
    JsonElement Self);

public interface IPrismReadStreamTransport : IAsyncDisposable
{
    Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken);

    Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken);

    Task CloseOutputAsync(CancellationToken cancellationToken);
}

public sealed class PrismReadStreamClient : IAsyncDisposable
{
    /// <summary>
    /// Borne de sécurité d'une enveloppe de lecture. Elle doit couvrir
    /// l'instantané de monde complet — 65 536 colonnes de surface plus les
    /// surcharges pour un monde de 256 × 256 blocs dépassent 4 Mio — sans
    /// laisser un producteur défaillant saturer la mémoire : le flux PRISM
    /// n'impose aucune borne (le Viewer n'en a pas non plus).
    /// </summary>
    private const int MaximumMessageBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Uri _endpoint;
    private readonly IPrismReadStreamTransport _transport;
    private bool _connected;
    private bool _disposed;

    public PrismReadStreamClient(
        Uri endpoint,
        IPrismReadStreamTransport? transport = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("ws" or "wss"))
        {
            throw new ArgumentException("The PRISM read stream requires an absolute ws:// or wss:// URI.", nameof(endpoint));
        }

        _endpoint = endpoint;
        _transport = transport ?? new ClientWebSocketPrismReadStreamTransport();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_connected)
        {
            throw new InvalidOperationException("The PRISM read stream is already connected.");
        }

        await _transport.ConnectAsync(_endpoint, cancellationToken).ConfigureAwait(false);
        _connected = true;
    }

    public async IAsyncEnumerable<PrismReadStreamEnvelope> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_connected)
        {
            throw new InvalidOperationException("Connect the PRISM read stream before reading.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            long previousSequence = -1;
            long previousTick = -1;
            while (true)
            {
                var envelope = await ReceiveEnvelopeAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (envelope is null)
                {
                    yield break;
                }

                if (envelope.Sequence <= previousSequence)
                {
                    throw new JsonException(
                        $"PRISM read sequence {envelope.Sequence} is not greater than {previousSequence}.");
                }

                if (previousSequence < 0 && envelope.Kind != "world_snapshot")
                {
                    throw new JsonException("The PRISM read stream must start with a world_snapshot.");
                }

                if (previousSequence >= 0 &&
                    envelope.Sequence != previousSequence + 1 &&
                    envelope.Kind != "world_snapshot")
                {
                    throw new JsonException(
                        "A PRISM read sequence gap must be followed by a world_snapshot.");
                }

                if (envelope.Tick < previousTick)
                {
                    throw new JsonException("PRISM read envelope ticks must not move backwards.");
                }

                previousSequence = envelope.Sequence;
                previousTick = envelope.Tick;
                yield return envelope;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<PrismReadStreamEnvelope?> ReceiveEnvelopeAsync(
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var message = new MemoryStream();
        while (true)
        {
            var result = await _transport.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _transport.CloseOutputAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new JsonException("PRISM read stream accepts text frames only.");
            }

            if (message.Length + result.Count > MaximumMessageBytes)
            {
                throw new JsonException("PRISM read envelope exceeds the 4 MiB limit.");
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        var raw = Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length));
        var envelope = JsonSerializer.Deserialize<PrismReadStreamEnvelope>(raw, JsonOptions)
                       ?? throw new JsonException("PRISM read envelope is empty.");
        if (!ProtocolVersion.TryParse(envelope.ProtocolVersion, out var version) ||
            !version.IsCompatibleWith(ProtocolVersion.Current))
        {
            throw new JsonException(
                $"PRISM read protocol version '{envelope.ProtocolVersion}' is invalid or unsupported.");
        }

        if (envelope.Sequence < 0 ||
            envelope.Tick < -1 ||
            string.IsNullOrWhiteSpace(envelope.Kind) ||
            envelope.Payload.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            throw new JsonException("PRISM read envelope fields are invalid.");
        }

        return envelope with { Payload = envelope.Payload.Clone() };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _transport.DisposeAsync().ConfigureAwait(false);
    }
}

internal sealed class ClientWebSocketPrismReadStreamTransport : IPrismReadStreamTransport
{
    private readonly ClientWebSocket _socket = new();

    public Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken) =>
        _socket.ConnectAsync(endpoint, cancellationToken);

    public Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken) =>
        _socket.ReceiveAsync(buffer, cancellationToken);

    public Task CloseOutputAsync(CancellationToken cancellationToken) =>
        _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", cancellationToken);

    public ValueTask DisposeAsync()
    {
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
