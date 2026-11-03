using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Prism.Content;
using Prism.Contracts;
using Prism.Core.World;
using Prism.Host.Hosting;
using Prism.Host.Persistence;

namespace Prism.Host.Tests;

public sealed class PrismHostRuntimeTests
{
    [Fact]
    public async Task LifecycleProducesReadStreamAndStopsAtTickLimit()
    {
        var runtime = new PrismHostRuntime(readStreamCapacity: 8);
        await using var stream = runtime.SubscribeReadStream();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var reader = stream.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();

        Assert.True(await reader.MoveNextAsync());
        Assert.Equal("world_snapshot", reader.Current.Kind);
        Assert.Equal("idle", reader.Current.Payload.GetProperty("state").GetString());

        runtime.Prepare(new PrepareWorldRequest(123, "content-hash"));
        Assert.True(runtime.IsReady);
        runtime.Start(new StartWorldRequest(MaxTicks: 1));
        var result = runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), 0, []));

        Assert.Equal(0, result.Tick);
        Assert.Equal("stopped", runtime.Status.State);
        Assert.Equal(0, runtime.Status.LastCompletedTick);

        var messageKinds = new List<string>();
        while (messageKinds.Count < 3 && await reader.MoveNextAsync())
        {
            messageKinds.Add(reader.Current.Kind);
        }

        Assert.Equal(["world_ready", "tick_result", "run_stopped"], messageKinds);
    }

    [Fact]
    public void InvalidTickDoesNotFailOrAdvanceReadyRun()
    {
        var runtime = new PrismHostRuntime();
        runtime.Prepare(new PrepareWorldRequest(1, "content-hash"));
        runtime.Start(new StartWorldRequest());

        var exception = Assert.Throws<HostLifecycleException>(
            () => runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), 1, [])));

        Assert.Equal("unexpected_tick", exception.ErrorCode);
        Assert.Equal("running", runtime.Status.State);
        Assert.Equal(-1, runtime.Status.LastCompletedTick);
        Assert.Equal(0, runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), 0, [])).Tick);
    }

    [Fact]
    public async Task AgentLifecycleCommandsPublishEntitiesAndSurviveHostRestore()
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var directory = Path.Combine(Path.GetTempPath(), $"prism-agent-checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new WorldCheckpointStore(Path.Combine(directory, "world.sqlite"));
            var runtime = new PrismHostRuntime(checkpointStore: store, checkpointOnStop: false);
            runtime.Prepare(new PrepareWorldRequest(
                123,
                compilation.Pack!.ContentHash,
                root));
            var origin = FindClearSurfaceOrigin(runtime.WorldSnapshot!);
            await using var stream = runtime.SubscribeReadStream();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await using var reader = stream.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();
            Assert.True(await reader.MoveNextAsync());
            Assert.Equal("world_snapshot", reader.Current.Kind);

            runtime.Start(new StartWorldRequest());
            using var payload = JsonDocument.Parse(JsonSerializer.Serialize(
                new
                {
                    position = new { x = origin.X, y = origin.Y, z = origin.Z },
                    profile = new { carrySlots = 4 },
                },
                ProtocolJson.Options));
            var result = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                0,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        0,
                        "incarnate-agent",
                        42,
                        "Incarner",
                        payload.RootElement.Clone()),
                ]));

            Assert.Equal("action_completed", Assert.Single(result.Events).Kind);
            var observation = Assert.Single(result.Observations);
            Assert.Equal(42UL, observation.AgentId);
            Assert.Equal(origin.X * 1000 + 500, observation.Self
                .GetProperty("position").GetProperty("x").GetInt32());
            Assert.Contains(
                observation.Changes.GetProperty("changes").EnumerateArray(),
                change => change.GetProperty("change").GetInt32() == 0);
            Assert.Equal(42UL, Assert.Single(runtime.WorldSnapshot!.Agents!).AgentId);
            Assert.True(await reader.MoveNextAsync());
            Assert.Equal("entity_snapshot", reader.Current.Kind);
            Assert.Equal(42UL, reader.Current.Payload.GetProperty("agents")[0].GetProperty("agentId").GetUInt64());

            runtime.SaveCheckpoint("agent-checkpoint");
            var restored = new PrismHostRuntime(checkpointStore: store, checkpointOnStop: false);
            restored.RestoreCheckpoint("agent-checkpoint", compilation.Pack.ContentHash);
            Assert.Equal(42UL, Assert.Single(restored.WorldSnapshot!.Agents!).AgentId);
            restored.Start(new StartWorldRequest());
            using var emptyPayload = JsonDocument.Parse("{}");
            var retire = restored.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                1,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        1,
                        "retire-agent",
                        42,
                        "Retirer",
                        emptyPayload.RootElement.Clone()),
                ]));

            Assert.Equal("action_completed", Assert.Single(retire.Events).Kind);
            Assert.Empty(restored.WorldSnapshot!.Agents!);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ContentActionsAreDispatchedAndContentRulesSurviveCheckpointRestore()
    {
        var sourceRoot = Path.Combine(FindPrismRoot(), "content", "minimal");
        var directory = Path.Combine(Path.GetTempPath(), $"prism-action-checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var contentRoot = Path.Combine(directory, "content");
            CopyContentDirectory(sourceRoot, contentRoot);
            ConfigureActionTestContent(contentRoot);
            var compilation = new ContentPackCompiler().CompileDirectory(contentRoot);
            Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));

            var store = new WorldCheckpointStore(Path.Combine(directory, "world.sqlite"));
            var runtime = new PrismHostRuntime(checkpointStore: store, checkpointOnStop: false);
            runtime.Prepare(new PrepareWorldRequest(
                123,
                compilation.Pack!.ContentHash,
                contentRoot));

            var mineableMaterials = compilation.Pack.Materials
                .Where(static material => material.Solid)
                .Select(material => compilation.Pack.Registries.Materials.GetId(material.Id))
                .ToHashSet();
            var (origin, target) = FindClearMineableSurfaceAndAdjacentTarget(
                runtime.WorldSnapshot!,
                mineableMaterials);
            runtime.Start(new StartWorldRequest());

            using var incarnatePayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { position = new { x = origin.X, y = origin.Y, z = origin.Z } },
                ProtocolJson.Options));
            var incarnate = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                0,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        0,
                        "incarnate-action-agent",
                        42,
                        "Incarner",
                        incarnatePayload.RootElement.Clone()),
                ]));
            Assert.Equal("action_completed", Assert.Single(incarnate.Events).Kind);

            using var obtainPayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { item = "stone", quantity = 1, hint = target, hintRadius = 0 },
                ProtocolJson.Options));
            var obtaining = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                1,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        1,
                        "obtain-nearby-stone",
                        42,
                        "ObtenirObjet",
                        obtainPayload.RootElement.Clone()),
                ]));
            Assert.Contains(obtaining.Events, static entry => entry.Kind == "macro_step_completed");

            var obtained = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                2,
                []));
            Assert.Contains(
                obtained.Events,
                static entry => entry.Kind == "action_completed" &&
                                entry.Payload.GetProperty("commandId").GetString() == "obtain-nearby-stone");

            using var consumePayload = JsonDocument.Parse("""{"item":"stone"}""");
            var consumed = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                3,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        3,
                        "consume-mined-stone",
                        42,
                        "Consommer",
                        consumePayload.RootElement.Clone()),
                ]));
            Assert.Contains(consumed.Events, static entry => entry.Kind == "action_completed");
            var consumedEvent = Assert.Single(
                consumed.Events,
                static entry => entry.Kind == "item_consumed");
            Assert.Equal("item", consumedEvent.Payload.GetProperty("source").GetString());
            Assert.NotEmpty(consumedEvent.Payload.GetProperty("effects").EnumerateObject());

            var containerPosition = FindAdjacentClearCell(runtime.WorldSnapshot!, origin);
            using var placeContainerPayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { target = containerPosition, item = "chest", material = "chest" },
                ProtocolJson.Options));
            var placed = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                4,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        4,
                        "place-test-container",
                        42,
                        "Poser",
                        placeContainerPayload.RootElement.Clone()),
                ]));
            Assert.Equal("action_completed", Assert.Single(placed.Events).Kind);

            using var depositPayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { container = containerPosition, item = "stone", quantity = 1 },
                ProtocolJson.Options));
            var deposited = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                5,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        5,
                        "deposit-in-test-container",
                        42,
                        "Deposer",
                        depositPayload.RootElement.Clone()),
                ]));
            Assert.Equal("action_completed", Assert.Single(deposited.Events).Kind);

            using var mineContainerPayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { target = containerPosition },
                ProtocolJson.Options));
            var protectedContainer = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                6,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        6,
                        "mine-nonempty-container",
                        42,
                        "Miner",
                        mineContainerPayload.RootElement.Clone()),
                ]));
            Assert.Equal("action_failed", Assert.Single(protectedContainer.Events).Kind);
            Assert.Equal(
                "container_not_empty",
                protectedContainer.Events[0].Payload.GetProperty("reason").GetString());

            runtime.SaveCheckpoint("action-checkpoint");
            var restored = new PrismHostRuntime(checkpointStore: store, checkpointOnStop: false);
            restored.RestoreCheckpoint("action-checkpoint", compilation.Pack.ContentHash);
            Assert.Equal(runtime.WorldSnapshot!.WorldHash, restored.WorldSnapshot!.WorldHash);

            restored.Start(new StartWorldRequest());
            using var takePayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { container = containerPosition, item = "stone", quantity = 1 },
                ProtocolJson.Options));
            var taken = restored.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                7,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        7,
                        "take-from-restored-container",
                        42,
                        "Prendre",
                        takePayload.RootElement.Clone()),
                ]));
            Assert.Equal("action_completed", Assert.Single(taken.Events).Kind);
            Assert.Equal(
                1,
                taken.Events[0].Payload.GetProperty("result").GetProperty("quantity").GetInt32());

            var distantTarget = FindDistantMineableSurfaceTarget(
                restored.WorldSnapshot!,
                mineableMaterials,
                origin);
            using var distantObtainPayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new { item = "stone", quantity = 1, hint = distantTarget, hintRadius = 0 },
                ProtocolJson.Options));
            var travelling = restored.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                8,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        8,
                        "obtain-distant-stone",
                        42,
                        "ObtenirObjet",
                        distantObtainPayload.RootElement.Clone()),
                ]));
            Assert.Contains(travelling.Events, static entry => entry.Kind == "macro_step_completed");
            restored.SaveCheckpoint("macro-checkpoint");

            var resumed = new PrismHostRuntime(checkpointStore: store, checkpointOnStop: false);
            resumed.RestoreCheckpoint("macro-checkpoint", compilation.Pack.ContentHash);
            resumed.Start(new StartWorldRequest());
            var macroCompleted = false;
            for (var tick = 9; tick <= 20 && !macroCompleted; tick++)
            {
                var result = resumed.Step(new TickRequest(
                    ProtocolVersion.Current.ToString(),
                    tick,
                    []));
                macroCompleted = result.Events.Any(entry =>
                    entry.Kind == "action_completed" &&
                    entry.Payload.GetProperty("commandId").GetString() == "obtain-distant-stone");
            }

            Assert.True(macroCompleted, "The restored obtain-object macro did not complete.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareWithContentGeneratesAndPublishesWorldSnapshot()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"prism-world-checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var root = Path.Combine(directory, "content");
            CopyContentDirectory(Path.Combine(FindPrismRoot(), "content", "minimal"), root);
            var templatesPath = Path.Combine(root, "templates", "structures.json");
            var templates = File.ReadAllText(templatesPath);
            var costedPathTemplate = "\"materials\": [{ \"item\": \"plank\", \"qty\": 1 }],";
            Assert.Contains(costedPathTemplate, templates, StringComparison.Ordinal);
            File.WriteAllText(
                templatesPath,
                templates.Replace(costedPathTemplate, "\"materials\": [],", StringComparison.Ordinal));

            var compilation = new ContentPackCompiler().CompileDirectory(root);
            Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
            var store = new WorldCheckpointStore(Path.Combine(directory, "world.sqlite"));
            var runtime = new PrismHostRuntime(checkpointStore: store, checksumIntervalTicks: 4);
            await using var stream = runtime.SubscribeReadStream();

            runtime.Prepare(new PrepareWorldRequest(
                123,
                compilation.Pack!.ContentHash,
                root));

            var generatedWorld = runtime.WorldSnapshot;
            Assert.NotNull(generatedWorld);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var reader = stream.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();
            Assert.True(await reader.MoveNextAsync());
            Assert.Equal("world_snapshot", reader.Current.Kind);
            Assert.True(await reader.MoveNextAsync());
            Assert.Equal("world_ready", reader.Current.Kind);
            var world = reader.Current.Payload.GetProperty("world");
            Assert.Equal(256, world.GetProperty("width").GetInt32());
            Assert.Equal(256 * 256, world.GetProperty("surfaceColumns").GetArrayLength());
            Assert.NotEmpty(world.GetProperty("overrides").EnumerateArray());
            Assert.Equal(
                generatedWorld!.WorldHash,
                world.GetProperty("worldHash").GetUInt64());

            var origin = FindClearSurfaceOrigin(generatedWorld);
            runtime.Start(new StartWorldRequest());
            using var startPayload = JsonDocument.Parse(JsonSerializer.Serialize(
                new
                {
                    siteId = "path-checkpoint",
                    templateId = "path_segment",
                    origin,
                },
                ProtocolJson.Options));
            var startResult = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                0,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        0,
                        "start-path",
                        42,
                        "DemarrerChantier",
                        startPayload.RootElement.Clone()),
                ]));
            Assert.Equal(
                "action_completed",
                Assert.Single(startResult.Events).Kind);
            using var contributionPayload = JsonDocument.Parse("""{"siteId":"path-checkpoint"}""");
            using var accessPayload = JsonDocument.Parse(
                """{"siteId":"path-checkpoint","rules":[{"kind":"agent","id":99,"rights":["build"]}]}""");
            var firstContribution = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                1,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        1,
                        "contribute-path-1",
                        42,
                        "Contribuer",
                        contributionPayload.RootElement.Clone()),
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        1,
                        "set-path-access",
                        42,
                        "DefinirAcces",
                        accessPayload.RootElement.Clone()),
                ]));
            Assert.All(firstContribution.Events, eventEnvelope => Assert.Equal("action_completed", eventEnvelope.Kind));
            var worldWithSite = runtime.WorldSnapshot!;
            Assert.Equal(generatedWorld.WorldHash, worldWithSite.WorldHash);
            Assert.Equal(
                StructureConstructionState.InProgress,
                Assert.Single(worldWithSite.ConstructionSites).State);
            Assert.False(Assert.Single(worldWithSite.StructureIntegrity).EffectsActive);
            Assert.Empty(worldWithSite.ActiveStructureEffects);

            runtime.SaveCheckpoint("generated-world");
            var restoredRuntime = new PrismHostRuntime(checkpointStore: store, checksumIntervalTicks: 4);
            restoredRuntime.RestoreCheckpoint("generated-world", compilation.Pack.ContentHash);

            var restoredWorld = restoredRuntime.WorldSnapshot;
            Assert.NotNull(restoredWorld);
            Assert.Equal(worldWithSite.WorldHash, restoredWorld.WorldHash);
            Assert.Equal(worldWithSite.Overrides.Length, restoredWorld.Overrides.Length);
            Assert.Equal(worldWithSite.SurfaceColumns, restoredWorld.SurfaceColumns);

            restoredRuntime.Start(new StartWorldRequest());
            var deniedContribution = restoredRuntime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                2,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        2,
                        "contribute-path-denied",
                        43,
                        "Contribuer",
                        contributionPayload.RootElement.Clone()),
                ]));
            Assert.Equal("action_failed", Assert.Single(deniedContribution.Events).Kind);
            Assert.Equal(
                "access_denied",
                deniedContribution.Events.Single().Payload.GetProperty("reason").GetString());

            await using var deltaSubscription = restoredRuntime.SubscribeReadStream();
            using var deltaCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var deltaReader = deltaSubscription.ReadAllAsync(deltaCancellation.Token).GetAsyncEnumerator();
            Assert.True(await deltaReader.MoveNextAsync());
            Assert.Equal("world_snapshot", deltaReader.Current.Kind);
            Assert.Single(
                deltaReader.Current.Payload
                    .GetProperty("world")
                    .GetProperty("constructionSites")
                    .EnumerateArray());

            var secondContribution = restoredRuntime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                3,
                [
                    new CommandEnvelope(
                        ProtocolVersion.Current.ToString(),
                        3,
                        "contribute-path-2",
                        99,
                        "Contribuer",
                        contributionPayload.RootElement.Clone()),
                ]));
            Assert.Equal("action_completed", Assert.Single(secondContribution.Events).Kind);
            var completedSite = secondContribution.Events.Single().Payload
                .GetProperty("result")
                .GetProperty("site");
            Assert.Equal(2, completedSite.GetProperty("workCompleted").GetInt32());
            Assert.Equal(1, completedSite.GetProperty("blocksPlaced").GetInt32());
            var completedWorld = restoredRuntime.WorldSnapshot!;
            Assert.NotEqual(worldWithSite.WorldHash, completedWorld.WorldHash);
            Assert.True(Assert.Single(completedWorld.StructureIntegrity).EffectsActive);
            Assert.Equal("path_speed", Assert.Single(completedWorld.ActiveStructureEffects).Type);
            Assert.Contains(
                completedWorld.Overrides,
                entry => entry.Key == origin);
            Assert.True(await deltaReader.MoveNextAsync());
            Assert.Equal("region_delta", deltaReader.Current.Kind);
            Assert.Equal(completedWorld.WorldHash, deltaReader.Current.Payload.GetProperty("worldHash").GetUInt64());
            var changedRegion = Assert.Single(deltaReader.Current.Payload.GetProperty("regions").EnumerateArray());
            Assert.Contains(
                changedRegion.GetProperty("overrides").EnumerateArray(),
                entry => entry.GetProperty("key").GetProperty("x").GetInt32() == origin.X &&
                         entry.GetProperty("key").GetProperty("y").GetInt32() == origin.Y &&
                         entry.GetProperty("key").GetProperty("z").GetInt32() == origin.Z);
            Assert.True(await deltaReader.MoveNextAsync());
            Assert.Equal("tick_result", deltaReader.Current.Kind);
            Assert.True(await deltaReader.MoveNextAsync());
            Assert.Equal("checksum", deltaReader.Current.Kind);
            Assert.Equal(3, deltaReader.Current.Payload.GetProperty("tick").GetInt64());
            Assert.Equal(completedWorld.WorldHash, deltaReader.Current.Payload.GetProperty("worldHash").GetUInt64());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ControlHttpRequiresTokenAndExposesLifecycleAndReadiness()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var runtime = new PrismHostRuntime();
        await using var app = PrismHostServer.Build(port, runtime, "session-secret");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
                "/control/prepare",
                new PrepareWorldRequest(1, "content-hash"))).StatusCode);

            client.DefaultRequestHeaders.Add("X-LIVEX-Session-Token", "session-secret");
            var prepare = await client.PostAsJsonAsync(
                "/control/prepare",
                new PrepareWorldRequest(1, "content-hash"));
            Assert.Equal(HttpStatusCode.OK, prepare.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

            var invalidStart = await client.PostAsJsonAsync("/control/start", new StartWorldRequest(0));
            Assert.Equal(HttpStatusCode.Conflict, invalidStart.StatusCode);

            var start = await client.PostAsJsonAsync("/control/start", new StartWorldRequest(1));
            Assert.Equal(HttpStatusCode.OK, start.StatusCode);

            var unsupportedTick = new TickRequest("99.0.0", 0, []);
            var unsupportedResponse = await client.PostAsJsonAsync("/data/tick", unsupportedTick);
            Assert.Equal(HttpStatusCode.BadRequest, unsupportedResponse.StatusCode);
            using (var unsupportedError = JsonDocument.Parse(await unsupportedResponse.Content.ReadAsStringAsync()))
            {
                Assert.Equal("protocol_unsupported", unsupportedError.RootElement.GetProperty("error").GetString());
            }

            using var unknownFieldRequest = new StringContent(
                """{"protocolVersion":"1.0.0","tick":0,"commands":[],"futureField":{"value":true}}""",
                Encoding.UTF8,
                "application/json");
            var compatibleResponse = await client.PostAsync("/data/tick", unknownFieldRequest);
            Assert.Equal(HttpStatusCode.OK, compatibleResponse.StatusCode);
            using var result = JsonDocument.Parse(await compatibleResponse.Content.ReadAsStringAsync());
            Assert.Equal(0, result.RootElement.GetProperty("tick").GetInt64());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task LockstepWebSocketUsesTheSameTickContractAndReadStreamStartsWithSnapshot()
    {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var readListener = new TcpListener(IPAddress.Loopback, 0);
            readListener.Start();
            var readPort = ((IPEndPoint)readListener.LocalEndpoint).Port;
            listener.Stop();
            readListener.Stop();

            var runtime = new PrismHostRuntime();
            await using var app = PrismHostServer.Build(port, runtime, "session-secret", readPort);
            await app.StartAsync();
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            http.DefaultRequestHeaders.Add("X-LIVEX-Session-Token", "session-secret");
            try
            {
                _ = await http.PostAsJsonAsync("/control/prepare", new PrepareWorldRequest(4, "hash"));
                _ = await http.PostAsJsonAsync("/control/start", new StartWorldRequest());

                using var lockstep = new ClientWebSocket();
                lockstep.Options.SetRequestHeader("X-LIVEX-Session-Token", "session-secret");
                await lockstep.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/data/lockstep"), CancellationToken.None);
                var unsupportedRequest = new TickRequest("99.0.0", 0, []);
                var requestBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(unsupportedRequest, ProtocolJson.Options));
                await lockstep.SendAsync(requestBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                var responseBuffer = new byte[4096];
                var response = await lockstep.ReceiveAsync(responseBuffer, CancellationToken.None);
                using (var error = JsonDocument.Parse(Encoding.UTF8.GetString(responseBuffer, 0, response.Count)))
                {
                    Assert.Equal("protocol_unsupported", error.RootElement.GetProperty("error").GetString());
                }

                requestBytes = Encoding.UTF8.GetBytes(
                    """{"protocolVersion":"1.0.0","tick":0,"commands":[],"futureField":{"value":true}}""");
                await lockstep.SendAsync(requestBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                response = await lockstep.ReceiveAsync(responseBuffer, CancellationToken.None);
                var result = ProtocolJson.Deserialize<TickResult>(
                    Encoding.UTF8.GetString(responseBuffer, 0, response.Count));
                Assert.Equal(0, result.Tick);
                await lockstep.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);

                using var readStream = new ClientWebSocket();
                await readStream.ConnectAsync(new Uri($"ws://127.0.0.1:{readPort}/data/read"), CancellationToken.None);
                var readBuffer = new byte[4096];
                var readResponse = await readStream.ReceiveAsync(readBuffer, CancellationToken.None);
                using var message = JsonDocument.Parse(Encoding.UTF8.GetString(readBuffer, 0, readResponse.Count));
                Assert.Equal("world_snapshot", message.RootElement.GetProperty("kind").GetString());
                Assert.Equal(0, message.RootElement.GetProperty("tick").GetInt64());
                await readStream.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            }
            finally
            {
                await app.StopAsync();
            }
    }

    [Fact]
    public async Task SlowReadSubscriberReceivesFreshSnapshotInsteadOfPartialHistory()
    {
        var runtime = new PrismHostRuntime(readStreamCapacity: 1);
        await using var stream = runtime.SubscribeReadStream();
        runtime.Prepare(new PrepareWorldRequest(9, "hash"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var reader = stream.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();

        Assert.True(await reader.MoveNextAsync());
        Assert.Equal("world_snapshot", reader.Current.Kind);
        Assert.Equal("ready", reader.Current.Payload.GetProperty("state").GetString());
    }

    private static string FindPrismRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Prism.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Prism.sln from the host test output directory.");
    }

    private static void CopyContentDirectory(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destinationFile = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile);
        }
    }

    private static void ConfigureActionTestContent(string root)
    {
        var itemsPath = Path.Combine(root, "items", "base.json");
        var items = JsonNode.Parse(File.ReadAllText(itemsPath))!.AsArray();
        var stoneItem = items
            .Select(static item => item!.AsObject())
            .Single(static item => item["id"]!.GetValue<string>() == "stone");
        stoneItem["tags"]!.AsArray().Add("edible");
        stoneItem["effects"] = "eat_wheat";
        File.WriteAllText(itemsPath, JsonSerializer.Serialize(items, ProtocolJson.Options));

        var materialsPath = Path.Combine(root, "materials", "base.json");
        var materials = JsonNode.Parse(File.ReadAllText(materialsPath))!.AsArray();
        foreach (var material in materials.Select(static material => material!.AsObject()))
        {
            if (material["solid"]?.GetValue<bool>() != true ||
                material["perceivable"]?.GetValue<bool>() != true)
            {
                continue;
            }

            material["drops"] = new JsonArray(
                new JsonObject
                {
                    ["item"] = "stone",
                    ["qty"] = 2,
                },
                new JsonObject
                {
                    ["item"] = "chest",
                    ["qty"] = 1,
                });
        }

        File.WriteAllText(materialsPath, JsonSerializer.Serialize(materials, ProtocolJson.Options));
    }

    private static BlockCoordinate FindClearSurfaceOrigin(GeneratedWorldSnapshot world)
    {
        var overrides = world.Overrides
            .Where(static entry => !entry.Value.IsAir)
            .Select(static entry => entry.Key)
            .ToHashSet();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var surface = world.SurfaceColumns[x + world.Width * y].SurfaceZ;
                var origin = new BlockCoordinate(x, y, surface + 1);
                if (origin.Z < world.VerticalSize && !overrides.Contains(origin))
                {
                    return origin;
                }
            }
        }

        throw new InvalidOperationException("The generated test world has no clear surface block.");
    }

    private static (BlockCoordinate Origin, BlockCoordinate Target) FindClearMineableSurfaceAndAdjacentTarget(
        GeneratedWorldSnapshot world,
        IReadOnlySet<ushort> mineableMaterials)
    {
        var overrides = world.Overrides
            .Where(static entry => !entry.Value.IsAir)
            .Select(static entry => entry.Key)
            .ToHashSet();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var column = world.SurfaceColumns[x + world.Width * y];
                var origin = new BlockCoordinate(x, y, column.SurfaceZ + 1);
                if (!mineableMaterials.Contains(column.MaterialId) ||
                    origin.Z >= world.VerticalSize ||
                    overrides.Contains(origin))
                {
                    continue;
                }

                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var targetX = x + dx;
                    var targetY = y + dy;
                    if ((uint)targetX >= (uint)world.Width ||
                        (uint)targetY >= (uint)world.Height)
                    {
                        continue;
                    }

                    var targetColumn = world.SurfaceColumns[targetX + world.Width * targetY];
                    var target = new BlockCoordinate(targetX, targetY, targetColumn.SurfaceZ);
                    if (targetColumn.SurfaceZ == origin.Z &&
                        mineableMaterials.Contains(targetColumn.MaterialId) &&
                        !overrides.Contains(target))
                    {
                        return (origin, target);
                    }
                }
            }
        }

        throw new InvalidOperationException("The generated test world has no clear surface with an adjacent mineable block.");
    }

    private static BlockCoordinate FindAdjacentClearCell(
        GeneratedWorldSnapshot world,
        BlockCoordinate origin)
    {
        var overrides = world.Overrides
            .Where(static entry => !entry.Value.IsAir)
            .Select(static entry => entry.Key)
            .ToHashSet();
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var x = origin.X + dx;
            var y = origin.Y + dy;
            if ((uint)x >= (uint)world.Width ||
                (uint)y >= (uint)world.Height)
            {
                continue;
            }

            var surface = world.SurfaceColumns[x + world.Width * y].SurfaceZ;
            var target = new BlockCoordinate(x, y, surface + 1);
            if (target.Z == origin.Z && !overrides.Contains(target))
            {
                return target;
            }
        }

        throw new InvalidOperationException("The generated test world has no adjacent clear container cell.");
    }

    private static BlockCoordinate FindDistantMineableSurfaceTarget(
        GeneratedWorldSnapshot world,
        IReadOnlySet<ushort> mineableMaterials,
        BlockCoordinate origin)
    {
        var overrides = world.Overrides
            .Where(static entry => !entry.Value.IsAir)
            .Select(static entry => entry.Key)
            .ToHashSet();
        for (var radius = 3; radius <= 12; radius++)
        {
            for (var y = Math.Max(0, origin.Y - radius); y <= Math.Min(world.Height - 1, origin.Y + radius); y++)
            {
                for (var x = Math.Max(0, origin.X - radius); x <= Math.Min(world.Width - 1, origin.X + radius); x++)
                {
                    if (Math.Abs(x - origin.X) + Math.Abs(y - origin.Y) < 3)
                    {
                        continue;
                    }

                    var column = world.SurfaceColumns[x + world.Width * y];
                    var target = new BlockCoordinate(x, y, column.SurfaceZ);
                    if (column.SurfaceZ == origin.Z - 1 &&
                        mineableMaterials.Contains(column.MaterialId) &&
                        !overrides.Contains(target))
                    {
                        return target;
                    }
                }
            }
        }

        throw new InvalidOperationException("The generated test world has no distant mineable surface target.");
    }
}
