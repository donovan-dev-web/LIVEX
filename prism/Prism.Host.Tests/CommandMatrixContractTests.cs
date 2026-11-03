using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Prism.Content;
using Prism.Contracts;
using Prism.Host.Hosting;

namespace Prism.Host.Tests;

/// <summary>
/// PRISM-078: every command kind must produce the same motivated outcome on the embedded host, the
/// HTTP link and the WebSocket link. The matrix is data-driven over the whole command vocabulary so
/// a new kind cannot ship without a contract outcome on all three links.
/// </summary>
public sealed class CommandMatrixContractTests
{
    public static readonly string[] AllKinds = [.. Prism.Contracts.CommandKinds.All];

    // Preparing a world costs about 25 s on this machine; one shared world keeps the whole matrix
    // under a minute instead of ten.
    private static readonly Lazy<MatrixFixture> Shared = new(() => new MatrixFixture());

    public static TheoryData<string> CommandKinds => new(AllKinds);

    [Theory]
    [MemberData(nameof(CommandKinds))]
    public void EmbeddedHostReturnsAMotivatedOutcomeForEveryKind(string kind)
    {
        var outcome = Shared.Value.StepEmbedded(kind);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Reason));
        Assert.True(
            outcome.Reason is "invalid_payload" or "agent_not_found" or "nothing_to_cancel" or "unknown_command",
            $"Unexpected reason '{outcome.Reason}' for {kind}.");
    }

    [Fact]
    public async Task EveryCommandKindHasTheSameOutcomeOnEmbeddedHttpAndWebSocketLinks()
    {
        var fixture = Shared.Value;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var linkRuntime = fixture.CreateRuntime();
        await using var app = PrismHostServer.Build(port, linkRuntime, MatrixFixture.Token);
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        http.DefaultRequestHeaders.Add("X-LIVEX-Session-Token", MatrixFixture.Token);
        try
        {
            await MatrixFixture.PrepareOverHttp(http, fixture);
            await http.PostAsJsonAsync("/control/start", new StartWorldRequest());

            using var lockstep = new ClientWebSocket();
            lockstep.Options.SetRequestHeader("X-LIVEX-Session-Token", MatrixFixture.Token);
            await lockstep.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/data/lockstep"), CancellationToken.None);
            var buffer = new byte[16 * 1024];
            var tick = 0L;

            foreach (var kind in AllKinds)
            {
                var embedded = fixture.StepEmbedded(kind);

                var httpResponse = await http.PostAsJsonAsync(
                    "/data/tick",
                    MatrixFixture.TickRequest(tick++, kind));
                Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
                var httpOutcome = MatrixFixture.Outcome(
                    ProtocolJson.Deserialize<TickResult>(await httpResponse.Content.ReadAsStringAsync()));

                await lockstep.SendAsync(
                    Encoding.UTF8.GetBytes(ProtocolJson.Serialize(MatrixFixture.TickRequest(tick++, kind))),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
                var received = await lockstep.ReceiveAsync(buffer, CancellationToken.None);
                var socketOutcome = MatrixFixture.Outcome(
                    ProtocolJson.Deserialize<TickResult>(Encoding.UTF8.GetString(buffer, 0, received.Count)));

                Assert.Equal(embedded, httpOutcome);
                Assert.Equal(embedded, socketOutcome);
            }

            await lockstep.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task UnknownPayloadFieldsAreIgnoredOnBothLinks()
    {
        var fixture = Shared.Value;
        var payload = JsonSerializer.SerializeToElement(
            new { target = new { x = 0, y = 0, z = 0 }, unexpected = "ignored", extra = 42 },
            ProtocolJson.Options);

        var embeddedTick = fixture.EmbeddedTick++;
        var embedded = fixture.Runtime.Step(new TickRequest(
            ProtocolVersion.Current.ToString(),
            embeddedTick,
            [MatrixFixture.Command(embeddedTick, "Aller", payload)]));
        Assert.DoesNotContain(
            embedded.Events,
            entry => entry.Kind == "action_failed" &&
                     entry.Payload.GetProperty("reason").GetString() == "invalid_payload");

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var linkRuntime = fixture.CreateRuntime();
        await using var app = PrismHostServer.Build(port, linkRuntime, MatrixFixture.Token);
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        http.DefaultRequestHeaders.Add("X-LIVEX-Session-Token", MatrixFixture.Token);
        try
        {
            await MatrixFixture.PrepareOverHttp(http, fixture);
            await http.PostAsJsonAsync("/control/start", new StartWorldRequest());
            var response = await http.PostAsJsonAsync(
                "/data/tick",
                new TickRequest(
                    ProtocolVersion.Current.ToString(),
                    0,
                    [MatrixFixture.Command(0, "Aller", payload)]));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = ProtocolJson.Deserialize<TickResult>(await response.Content.ReadAsStringAsync());
            Assert.DoesNotContain(
                result.Events,
                entry => entry.Kind == "action_failed" &&
                         entry.Payload.GetProperty("reason").GetString() == "invalid_payload");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class MatrixFixture
    {
        public const string Token = "matrix-session-token";
        private const ulong AgentId = 1;
        private readonly string _contentDirectory;

        public MatrixFixture()
        {
            _contentDirectory = MinimalContentDirectory();
            var compilation = new ContentPackCompiler().CompileDirectory(_contentDirectory);
            Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
            ContentHash = compilation.Pack!.ContentHash;
            Runtime = CreateRuntime();
        }

        public string ContentHash { get; }

        public PrismHostRuntime Runtime { get; }

        public long EmbeddedTick { get; set; }

        public PrismHostRuntime CreateRuntime()
        {
            var runtime = new PrismHostRuntime();
            runtime.Prepare(new PrepareWorldRequest(4, ContentHash, _contentDirectory));
            runtime.Start(new StartWorldRequest());
            return runtime;
        }

        public static async Task PrepareOverHttp(HttpClient http, MatrixFixture fixture) =>
            _ = await http.PostAsJsonAsync(
                "/control/prepare",
                new PrepareWorldRequest(4, fixture.ContentHash, fixture._contentDirectory));

        public (string? Reason, bool Ok) StepEmbedded(string kind)
        {
            var tick = EmbeddedTick++;
            var result = Runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                tick,
                [Command(tick, kind, EmptyPayload())]));
            return Outcome(result);
        }

        public static CommandEnvelope Command(long tick, string kind, JsonElement payload) =>
            new(
                ProtocolVersion.Current.ToString(),
                tick,
                $"matrix:{kind}:{tick}",
                AgentId,
                kind,
                payload);

        public static TickRequest TickRequest(long tick, string kind) =>
            new(ProtocolVersion.Current.ToString(), tick, [Command(tick, kind, EmptyPayload())]);

        public static (string? Reason, bool Ok) Outcome(TickResult result)
        {
            var failure = result.Events.FirstOrDefault(entry => entry.Kind == "action_failed");
            if (failure is null)
            {
                return (null, true);
            }

            var reason = failure.Payload.TryGetProperty("reason", out var value)
                ? value.GetString()
                : "unmotivated_failure";
            return (reason, false);
        }

        private static JsonElement EmptyPayload() =>
            JsonSerializer.SerializeToElement(new { }, ProtocolJson.Options);
    }

    private static string MinimalContentDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Prism.sln")))
            {
                return Path.Combine(directory.FullName, "content", "minimal");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Prism.sln from the host test output directory.");
    }
}
