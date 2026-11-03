using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Prism.Content;
using Prism.Contracts;
using Prism.Core.World;
using Prism.Host.Hosting;
using Prism.Host.Persistence;

namespace Prism.Host.Tests;

public sealed class ContractHardeningTests
{
    [Fact]
    public async Task ReadStreamPublishesMetricsWithStocksMatterAndFailures()
    {
        var root = MinimalContentDirectory();
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var runtime = new PrismHostRuntime(readStreamCapacity: 64, checksumIntervalTicks: 5);
        await using var stream = runtime.SubscribeReadStream();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await using var reader = stream.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal("world_snapshot", reader.Current.Kind);

        runtime.Prepare(new PrepareWorldRequest(123, compilation.Pack!.ContentHash, root));
        runtime.Start(new StartWorldRequest());
        for (var tick = 0; tick < 5; tick++)
        {
            runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                tick,
                tick == 0 ? [InvalidMiner(tick)] : []));
        }

        ReadStreamEnvelope? metrics = null;
        while (metrics is null && await reader.MoveNextAsync())
        {
            if (reader.Current.Kind == "metrics")
            {
                metrics = reader.Current;
                break;
            }
        }

        Assert.NotNull(metrics);
        Assert.Equal(4, metrics!.Tick);
        var payload = metrics.Payload;
        Assert.Equal(JsonValueKind.Array, payload.GetProperty("stocks").ValueKind);
        Assert.Equal(JsonValueKind.Array, payload.GetProperty("matter").ValueKind);
        Assert.Equal(JsonValueKind.Array, payload.GetProperty("structures").ValueKind);
        Assert.Equal(0, payload.GetProperty("reservedStations").GetInt32());
        var failures = payload.GetProperty("failures").EnumerateArray().ToArray();
        var invalidPayload = Assert.Single(failures);
        Assert.Equal("invalid_payload", invalidPayload.GetProperty("reason").GetString());
        Assert.Equal(1, invalidPayload.GetProperty("count").GetInt64());
        Assert.NotNull(runtime.CurrentMetrics);
        Assert.Equal(4, runtime.CurrentMetrics!.Tick);
    }

    [Fact]
    public void AllerCommandWalksTheAgentUntilTheTargetCellIsReached()
    {
        var root = MinimalContentDirectory();
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var runtime = new PrismHostRuntime();
        runtime.Prepare(new PrepareWorldRequest(123, compilation.Pack!.ContentHash, root));
        var (origin, target) = FindFlatRun(runtime.WorldSnapshot!, minimumLength: 3);
        runtime.Start(new StartWorldRequest());

        using var incarnate = JsonDocument.Parse(JsonSerializer.Serialize(
            new { position = new { x = origin.X, y = origin.Y, z = origin.Z } },
            ProtocolJson.Options));
        var first = runtime.Step(new TickRequest(
            ProtocolVersion.Current.ToString(),
            0,
            [new CommandEnvelope(
                ProtocolVersion.Current.ToString(),
                0,
                "incarnate",
                42,
                "Incarner",
                incarnate.RootElement.Clone())]));
        Assert.Equal("action_completed", Assert.Single(first.Events).Kind);

        using var move = JsonDocument.Parse(JsonSerializer.Serialize(
            new { target = new { x = target.X, y = target.Y, z = target.Z } },
            ProtocolJson.Options));
        var started = runtime.Step(new TickRequest(
            ProtocolVersion.Current.ToString(),
            1,
            [new CommandEnvelope(
                ProtocolVersion.Current.ToString(),
                1,
                "walk",
                42,
                "Aller",
                move.RootElement.Clone())]));
        var accepted = Assert.Single(started.Events);
        Assert.Equal("action_completed", accepted.Kind);
        Assert.True(accepted.Payload.GetProperty("result").GetProperty("accepted").GetBoolean());

        var agentX = origin.X;
        var completion = started;
        for (var tick = 2L; tick <= 1 + target.X - origin.X + 2 && completion.Events.All(
                 eventEnvelope => eventEnvelope.Payload.TryGetProperty("result", out var result) &&
                                  !result.TryGetProperty("completed", out _));
             tick++)
        {
            completion = runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), tick, []));
            agentX = Assert.Single(runtime.WorldSnapshot!.Agents!).Position.CellX;
        }

        Assert.Equal(target.X, agentX);
        var finished = Assert.Single(
            completion.Events,
            eventEnvelope => eventEnvelope.Kind == "action_completed");
        Assert.True(finished.Payload.GetProperty("result").GetProperty("completed").GetBoolean());
    }

    [Fact]
    public void ContractRejectsInvalidPayloadsBeforeTheHandlerRuns()
    {
        var root = MinimalContentDirectory();
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var runtime = new PrismHostRuntime();
        runtime.Prepare(new PrepareWorldRequest(1, compilation.Pack!.ContentHash, root));
        runtime.Start(new StartWorldRequest());

        var result = runtime.Step(new TickRequest(
            ProtocolVersion.Current.ToString(),
            0,
            [InvalidMiner(0), UnknownCommand(0)]));

        Assert.Collection(
            result.Events,
            eventEnvelope =>
            {
                Assert.Equal("action_failed", eventEnvelope.Kind);
                Assert.Equal("invalid_payload", eventEnvelope.Payload.GetProperty("reason").GetString());
            },
            eventEnvelope =>
            {
                Assert.Equal("action_failed", eventEnvelope.Kind);
                Assert.Equal("unknown_command", eventEnvelope.Payload.GetProperty("reason").GetString());
            });
    }

    [Fact]
    public async Task RestoredRunProducesTheSameTickResultsAsTheContinuousRun()
    {
        var root = MinimalContentDirectory();
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var contentHash = compilation.Pack!.ContentHash;
        var directory = Path.Combine(Path.GetTempPath(), $"prism-resume-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new WorldCheckpointStore(Path.Combine(directory, "world.sqlite"));
            var continuous = new PrismHostRuntime(checkpointStore: store, checkpointOnStop: false);
            continuous.Prepare(new PrepareWorldRequest(123, contentHash, root));
            var (origin, target) = FindFlatRun(continuous.WorldSnapshot!, minimumLength: 8);
            continuous.Start(new StartWorldRequest());
            var continuousResults = new List<string>();
            for (var tick = 0L; tick < 12; tick++)
            {
                var commands = tick switch
                {
                    0 => new[] { Incarnate(tick, origin) },
                    1 => new[] { Move(tick, target) },
                    _ => [],
                };
                var result = continuous.Step(new TickRequest(ProtocolVersion.Current.ToString(), tick, commands));
                if (tick >= 5)
                {
                    continuousResults.Add(ProtocolJson.Serialize(result));
                }

                if (tick == 4)
                {
                    continuous.SaveCheckpoint("mid");
                }
            }

            var continuousHash = continuous.WorldSnapshot!.WorldHash;
            var continuousAgents = JsonSerializer.Serialize(
                continuous.WorldSnapshot.Agents,
                ProtocolJson.Options);

            var restored = new PrismHostRuntime(checkpointStore: store, checkpointOnStop: false);
            restored.RestoreCheckpoint("mid", contentHash);
            restored.Start(new StartWorldRequest());
            var restoredResults = new List<string>();
            for (var tick = 5L; tick < 12; tick++)
            {
                var result = restored.Step(new TickRequest(ProtocolVersion.Current.ToString(), tick, []));
                restoredResults.Add(ProtocolJson.Serialize(result));
            }

            Assert.Equal(continuousResults, restoredResults);
            Assert.Equal(continuousHash, restored.WorldSnapshot!.WorldHash);
            Assert.Equal(
                continuousAgents,
                JsonSerializer.Serialize(restored.WorldSnapshot.Agents, ProtocolJson.Options));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task HttpAndWebSocketLinksReportTheSameContractErrors()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var runtime = new PrismHostRuntime();
        await using var app = PrismHostServer.Build(port, runtime, "session-secret");
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        http.DefaultRequestHeaders.Add("X-LIVEX-Session-Token", "session-secret");
        try
        {
            var root = MinimalContentDirectory();
            var compilation = new ContentPackCompiler().CompileDirectory(root);
            Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
            _ = await http.PostAsJsonAsync(
                "/control/prepare",
                new PrepareWorldRequest(4, compilation.Pack!.ContentHash, root));
            _ = await http.PostAsJsonAsync("/control/start", new StartWorldRequest());

            var httpResult = await http.PostAsJsonAsync(
                "/data/tick",
                new TickRequest(ProtocolVersion.Current.ToString(), 0, [InvalidMiner(0)]));
            Assert.Equal(HttpStatusCode.OK, httpResult.StatusCode);
            var httpTick = ProtocolJson.Deserialize<TickResult>(
                await httpResult.Content.ReadAsStringAsync());
            var httpFailure = Assert.Single(httpTick.Events);
            Assert.Equal("invalid_payload", httpFailure.Payload.GetProperty("reason").GetString());

            using var lockstep = new ClientWebSocket();
            lockstep.Options.SetRequestHeader("X-LIVEX-Session-Token", "session-secret");
            await lockstep.ConnectAsync(
                new Uri($"ws://127.0.0.1:{port}/data/lockstep"),
                CancellationToken.None);
            var buffer = new byte[8192];
            await SendAsync(lockstep, JsonSerializer.Serialize(
                new TickRequest("99.0.0", 1, []),
                ProtocolJson.Options));
            using (var error = JsonDocument.Parse(await ReceiveAsync(lockstep, buffer)))
            {
                Assert.Equal("protocol_unsupported", error.RootElement.GetProperty("error").GetString());
            }

            await SendAsync(lockstep, JsonSerializer.Serialize(
                new TickRequest(ProtocolVersion.Current.ToString(), 1, [UnknownCommand(1)]),
                ProtocolJson.Options));
            var socketTick = ProtocolJson.Deserialize<TickResult>(await ReceiveAsync(lockstep, buffer));
            var socketFailure = Assert.Single(socketTick.Events);
            Assert.Equal("unknown_command", socketFailure.Payload.GetProperty("reason").GetString());
            await lockstep.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);

            using var metricsResponse = await http.GetAsync("/metrics");
            var metricsText = await metricsResponse.Content.ReadAsStringAsync();
            Assert.Contains("prism_host_last_completed_tick 1", metricsText, StringComparison.Ordinal);
            Assert.Contains("prism_command_failures_total{reason=\"invalid_payload\"}", metricsText, StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static CommandEnvelope InvalidMiner(long tick)
    {
        using var payload = JsonDocument.Parse("{}");
        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            $"invalid-{tick}",
            1,
            "Miner",
            payload.RootElement.Clone());
    }

    private static CommandEnvelope UnknownCommand(long tick)
    {
        using var payload = JsonDocument.Parse("{}");
        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            $"unknown-{tick}",
            1,
            "Inconnue",
            payload.RootElement.Clone());
    }

    private static CommandEnvelope Incarnate(long tick, BlockCoordinate origin)
    {
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(
            new { position = new { x = origin.X, y = origin.Y, z = origin.Z } },
            ProtocolJson.Options));
        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            $"incarnate-{tick}",
            42,
            "Incarner",
            payload.RootElement.Clone());
    }

    private static CommandEnvelope Move(long tick, BlockCoordinate target)
    {
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(
            new { target = new { x = target.X, y = target.Y, z = target.Z } },
            ProtocolJson.Options));
        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            tick,
            $"move-{tick}",
            42,
            "Aller",
            payload.RootElement.Clone());
    }

    private static (BlockCoordinate Origin, BlockCoordinate Target) FindFlatRun(
        GeneratedWorldSnapshot world,
        int minimumLength)
    {
        var overrides = world.Overrides
            .Where(static entry => !entry.Value.IsAir)
            .Select(static entry => entry.Key)
            .ToHashSet();
        for (var y = 0; y < world.Height; y++)
        {
            var runStart = 0;
            var runZ = -1;
            for (var x = 0; x < world.Width; x++)
            {
                var column = world.SurfaceColumns[x + world.Width * y];
                var feetZ = column.SurfaceZ + 1;
                var blocked = feetZ >= world.VerticalSize ||
                              overrides.Contains(new BlockCoordinate(x, y, feetZ));
                if (blocked || (runZ >= 0 && column.SurfaceZ != runZ))
                {
                    runStart = x;
                    runZ = blocked ? -1 : column.SurfaceZ;
                    continue;
                }

                if (runZ < 0)
                {
                    runStart = x;
                    runZ = column.SurfaceZ;
                    continue;
                }

                if (x - runStart + 1 >= minimumLength)
                {
                    return (
                        new BlockCoordinate(runStart, y, runZ + 1),
                        new BlockCoordinate(x, y, feetZ));
                }
            }
        }

        throw new InvalidOperationException(
            "The generated test world has no flat surface run of the requested length.");
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

    private static Task SendAsync(WebSocket socket, string message) =>
        socket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);

    private static async Task<string> ReceiveAsync(WebSocket socket, byte[] buffer)
    {
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}
