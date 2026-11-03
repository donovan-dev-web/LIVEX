using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;

namespace Prism.Viewer;

/// <summary>One envelope of the PRISM read stream (<c>/data/read</c>).</summary>
public sealed record ViewerStreamEnvelope(
    string ProtocolVersion,
    long Sequence,
    long Tick,
    string Kind,
    JsonElement Payload)
{
    public static ViewerStreamEnvelope Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var envelope = JsonSerializer.Deserialize<ViewerStreamEnvelope>(json, ProtocolJson.Options)
                       ?? throw new JsonException("The read-stream envelope is empty.");
        ProtocolJson.ValidateEnvelopeVersion(envelope.ProtocolVersion);
        if (envelope.Sequence < 0)
        {
            throw new JsonException("The read-stream envelope sequence must be non-negative.");
        }

        if (envelope.Tick < 0)
        {
            throw new JsonException("The read-stream envelope tick must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Kind) ||
            envelope.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The read-stream envelope kind or payload is invalid.");
        }

        return envelope;
    }
}

/// <summary>
/// Client mode of the viewer: consumes the host read stream and applies it to the entity store.
/// A sequence gap never corrupts the view: intermediate deltas are dropped and the view is
/// re-instantiated from the next full snapshot (RENDERING_SPEC.md §8).
/// </summary>
public sealed class ViewerLiveSession(ViewerEntityStore? store = null)
{
    public ViewerEntityStore Store { get; } = store ?? new ViewerEntityStore();

    public long Tick { get; private set; } = -1;

    public ulong? WorldHash { get; private set; }

    public long LastSequence { get; private set; } = -1;

    public int Resynchronisations { get; private set; }

    public bool AwaitingSnapshot { get; private set; }

    public ViewerApplyStatus Apply(ViewerStreamEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Tick < Tick)
        {
            return ViewerApplyStatus.Stale;
        }

        if (LastSequence >= 0 && envelope.Sequence != LastSequence + 1 && !AwaitingSnapshot)
        {
            AwaitingSnapshot = true;
            Resynchronisations++;
        }

        if (AwaitingSnapshot && envelope.Kind != "world_snapshot")
        {
            return ViewerApplyStatus.Resynchronised;
        }

        switch (envelope.Kind)
        {
            case "world_snapshot":
                ApplyWorldSnapshot(envelope);
                AwaitingSnapshot = false;
                break;
            case "entity_snapshot":
                ApplyEntitySnapshot(envelope);
                break;
            case "tick_result":
                ApplyTickResult(envelope);
                break;
            case "checksum":
                if (envelope.Payload.TryGetProperty("worldHash", out var hash))
                {
                    WorldHash = hash.GetUInt64();
                }

                Tick = Math.Max(Tick, envelope.Tick);
                break;
            default:
                LastSequence = envelope.Sequence;
                return ViewerApplyStatus.Ignored;
        }

        LastSequence = envelope.Sequence;
        Tick = Math.Max(Tick, envelope.Tick);
        return ViewerApplyStatus.Applied;
    }

    private void ApplyWorldSnapshot(ViewerStreamEnvelope envelope)
    {
        if (envelope.Payload.TryGetProperty("tick", out var tick))
        {
            Tick = tick.GetInt64();
        }

        if (envelope.Payload.TryGetProperty("worldHash", out var hash))
        {
            WorldHash = hash.GetUInt64();
        }

        if (envelope.Payload.TryGetProperty("agents", out var agents) &&
            agents.ValueKind == JsonValueKind.Array)
        {
            var parsed = agents.Deserialize<AgentEntitySnapshot[]>(ProtocolJson.Options) ?? [];
            Store.ApplySnapshot(parsed, ReadGroundItems(envelope.Payload), envelope.Tick);
        }
    }

    private void ApplyEntitySnapshot(ViewerStreamEnvelope envelope)
    {
        if (!envelope.Payload.TryGetProperty("agents", out var agents) ||
            agents.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var parsed = agents.Deserialize<AgentEntitySnapshot[]>(ProtocolJson.Options) ?? [];
        var tick = envelope.Payload.TryGetProperty("tick", out var payloadTick)
            ? payloadTick.GetInt64()
            : envelope.Tick;
        Store.ApplySnapshot(parsed, ReadGroundItems(envelope.Payload), tick);
    }

    private void ApplyTickResult(ViewerStreamEnvelope envelope)
    {
        var result = envelope.Payload.Deserialize<TickResult>(ProtocolJson.Options);
        if (result is not null)
        {
            Store.ApplyTickResult(result);
        }
    }

    private static GroundItem[] ReadGroundItems(JsonElement payload) =>
        payload.TryGetProperty("groundItems", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.Deserialize<GroundItem[]>(ProtocolJson.Options) ?? []
            : [];
}

public enum ViewerApplyStatus
{
    Applied,
    Ignored,
    Resynchronised,
    Stale,
}

/// <summary>Thin WebSocket transport for the read stream; parsing and state live in the session.</summary>
public sealed class ViewerReadStreamClient : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly byte[] _buffer = new byte[64 * 1024];

    public WebSocketState State => _socket.State;

    public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.Scheme is not ("ws" or "wss"))
        {
            throw new ArgumentException("The read stream requires a ws:// or wss:// endpoint.", nameof(uri));
        }

        await _socket.ConnectAsync(uri, cancellationToken);
    }

    public async IAsyncEnumerable<ViewerStreamEnvelope> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        while (_socket.State == WebSocketState.Open)
        {
            var result = await _socket.ReceiveAsync(_buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                yield break;
            }

            builder.Append(Encoding.UTF8.GetString(_buffer, 0, result.Count));
            if (!result.EndOfMessage)
            {
                continue;
            }

            var json = builder.ToString();
            builder.Clear();
            yield return ViewerStreamEnvelope.Parse(json);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        }

        _socket.Dispose();
    }
}
