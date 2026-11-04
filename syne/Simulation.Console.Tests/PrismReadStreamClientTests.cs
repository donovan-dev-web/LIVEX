using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Simulation.Console.Integration;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismReadStreamClientTests
{
    [Fact]
    public async Task ReassemblesTextFramesAndAcceptsSnapshotResynchronization()
    {
        var transport = new FakeTransport(
        [
            Envelope(5, 2, "world_snapshot"),
            Envelope(7, 3, "world_snapshot"),
            Envelope(8, 4, "tick_result"),
        ],
        fragmentSize: 17);
        await using var client = new PrismReadStreamClient(
            new Uri("ws://127.0.0.1:5182/data/read"),
            transport);
        await client.ConnectAsync();

        var envelopes = new List<PrismReadStreamEnvelope>();
        await foreach (var envelope in client.ReadAllAsync())
        {
            envelopes.Add(envelope);
        }

        Assert.Equal([5, 7, 8], envelopes.Select(static envelope => envelope.Sequence));
        Assert.Equal(["world_snapshot", "world_snapshot", "tick_result"],
            envelopes.Select(static envelope => envelope.Kind));
        var observation = Assert.Single(envelopes[^1].GetAgentObservations());
        Assert.Equal(42UL, observation.AgentId);
        Assert.Equal(4L, observation.Tick);
        Assert.Equal(1, observation.Changes.GetProperty("changes").GetArrayLength());
        Assert.Equal(1, observation.Self.GetProperty("position").GetProperty("x").GetInt32());
        Assert.Equal(1, transport.CloseOutputCount);
        await client.DisposeAsync();
        Assert.True(transport.Disposed);
    }

    [Fact]
    public async Task RejectsDifferentialMessagesAfterSequenceGap()
    {
        var transport = new FakeTransport(
        [
            Envelope(0, -1, "world_snapshot"),
            Envelope(2, 0, "region_delta"),
        ]);
        await using var client = new PrismReadStreamClient(
            new Uri("ws://127.0.0.1:5182/data/read"),
            transport);
        await client.ConnectAsync();

        await using var reader = client.ReadAllAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        var exception = await Assert.ThrowsAsync<JsonException>(
            async () => await reader.MoveNextAsync());
        Assert.Contains("sequence gap", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsUnsupportedProtocolVersionAndBinaryFrames()
    {
        var modified = new
        {
            protocolVersion = "2.0.0",
            sequence = 0,
            tick = -1,
            kind = "world_snapshot",
            payload = new { state = "ready" },
        };
        var transport = new FakeTransport([JsonSerializer.Serialize(modified)]);
        await using var client = new PrismReadStreamClient(
            new Uri("ws://127.0.0.1:5182/data/read"),
            transport);
        await client.ConnectAsync();
        await using var reader = client.ReadAllAsync().GetAsyncEnumerator();

        var exception = await Assert.ThrowsAsync<JsonException>(
            async () => await reader.MoveNextAsync());
        Assert.Contains("unsupported", exception.Message, StringComparison.Ordinal);

        var binaryTransport = new FakeTransport(
            [Envelope(0, -1, "world_snapshot")],
            messageType: WebSocketMessageType.Binary);
        await using var binaryClient = new PrismReadStreamClient(
            new Uri("ws://127.0.0.1:5182/data/read"),
            binaryTransport);
        await binaryClient.ConnectAsync();
        await using var binaryReader = binaryClient.ReadAllAsync().GetAsyncEnumerator();
        await Assert.ThrowsAsync<JsonException>(async () => await binaryReader.MoveNextAsync());
    }

    [Fact]
    public async Task RequiresConnectionAndValidWebSocketEndpoint()
    {
        Assert.Throws<ArgumentException>(() =>
            new PrismReadStreamClient(new Uri("http://127.0.0.1:5182/data/read")));
        await using var client = new PrismReadStreamClient(
            new Uri("ws://127.0.0.1:5182/data/read"),
            new FakeTransport([]));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.ReadAllAsync())
            {
            }
        });
    }

    private static string Envelope(long sequence, long tick, string kind) =>
        JsonSerializer.Serialize(new
        {
            protocolVersion = "1.0.0",
            sequence,
            tick,
            kind,
            payload = kind == "tick_result"
                ? (object)new
                {
                    observations = new[]
                    {
                        new
                        {
                            protocolVersion = "1.0.0",
                            tick,
                            agentId = 42UL,
                            changes = new { changes = new[] { new { change = "appeared" } } },
                            self = new { position = new { x = 1, y = 2, z = 3 } },
                        },
                    },
                }
                : new { state = "ready" },
        });

    private sealed class FakeTransport(
        IReadOnlyList<string> messages,
        int fragmentSize = int.MaxValue,
        WebSocketMessageType messageType = WebSocketMessageType.Text)
        : IPrismReadStreamTransport
    {
        private readonly Queue<byte[]> _messages = new(
            messages.Select(Encoding.UTF8.GetBytes));
        private byte[]? _current;
        private int _offset;

        public int CloseOutputCount { get; private set; }
        public bool Disposed { get; private set; }

        public Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            if (_current is null)
            {
                if (_messages.Count == 0)
                {
                    return Task.FromResult(
                        new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
                }

                _current = _messages.Dequeue();
                _offset = 0;
            }

            var count = Math.Min(
                Math.Min(fragmentSize, buffer.Count),
                _current.Length - _offset);
            Array.Copy(_current, _offset, buffer.Array!, buffer.Offset, count);
            _offset += count;
            var endOfMessage = _offset == _current.Length;
            if (endOfMessage)
            {
                _current = null;
            }

            return Task.FromResult(
                new WebSocketReceiveResult(count, messageType, endOfMessage));
        }

        public Task CloseOutputAsync(CancellationToken cancellationToken)
        {
            CloseOutputCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
