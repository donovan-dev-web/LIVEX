using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Prism.Content;
using Prism.Contracts;
using Prism.Core.World;
using Prism.Host.Hosting;
using Prism.Host.Ticking;

namespace Prism.Host.Tests;

/// <summary>
/// Plafond de payload par tick (PRISM-142, ADR-014) : aucune réponse de tick ne
/// dépasse <see cref="TickTransportLimits.MaximumTickResponseBytes"/>, le tick est
/// découpé en pages quand il le faut, les pages se recollent à l'octet près, et un
/// élément qui ne peut pas tenir est refusé franchement — jamais tronqué.
/// </summary>
public sealed class TickPayloadCeilingTests
{
    private const int TestCeiling = 64 * 1024;

    [Fact]
    public async Task TickAboveTheCeilingIsPagedAndReassemblesExactly()
    {
        var host = new SyntheticWorldHost(agents: 24, changesPerAgent: 64, events: 8);
        var runtime = new PrismHostRuntime(() => host);
        runtime.Prepare(new PrepareWorldRequest(1, "synthetic"));
        runtime.Start(new StartWorldRequest());

        await using var app = await StartAsync(runtime, TestCeiling);
        using var client = NewClient(app.Port);

        var response = await client.PostAsync(
            "/data/tick",
            TickRequestBody(new TickRequest(ProtocolVersion.Current.ToString(), 0, [])));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Content.Headers.ContentLength <= TestCeiling,
            $"A tick response of {response.Content.Headers.ContentLength} bytes exceeds the {TestCeiling}-byte ceiling.");
        var firstBody = await response.Content.ReadAsStringAsync();
        using var firstDocument = JsonDocument.Parse(firstBody);
        Assert.Equal(0, firstDocument.RootElement.GetProperty("page").GetProperty("index").GetInt32());
        Assert.False(firstDocument.RootElement.GetProperty("page").GetProperty("complete").GetBoolean());
        var announced = firstDocument.RootElement.GetProperty("page").GetProperty("pageCount").GetInt32();
        Assert.True(announced > 1, "The synthetic tick should not fit in a single page.");

        var largest = response.Content.Headers.ContentLength ?? 0;
        var drained = await DrainAsync(
            firstBody,
            async cursor =>
            {
                var page = await client.GetAsync(
                    $"/data/tick/result?cursor={Uri.EscapeDataString(cursor)}");
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                largest = Math.Max(largest, page.Content.Headers.ContentLength ?? 0);
                return await page.Content.ReadAsStringAsync();
            });

        Assert.Equal(announced, drained.PageCount);
        Assert.True(largest <= TestCeiling, $"Largest response was {largest} bytes, above the {TestCeiling}-byte ceiling.");

        // La pagination est transparente : les pages recollées sont le tick complet,
        // octet pour octet — c'est le critère de contenu identique (PRISM-107).
        var expected = ProtocolJson.Serialize(SyntheticWorldHost.BuildTick(0, 24, 64, 8));
        Assert.Equal(expected, ProtocolJson.Serialize(drained.Result));
    }

    [Fact]
    public async Task AnElementThatCannotFitTheCeilingIsRefusedInsteadOfTruncated()
    {
        // Une seule observation, mais son auto-observation porte 200 Ko : aucune
        // découpe n'est possible, donc l'hôte doit refuser le tick.
        var host = new SyntheticWorldHost(agents: 1, changesPerAgent: 1, events: 0, oversizedSelfBytes: 200_000);
        var runtime = new PrismHostRuntime(() => host);
        runtime.Prepare(new PrepareWorldRequest(1, "synthetic"));
        runtime.Start(new StartWorldRequest());

        await using var app = await StartAsync(runtime, TestCeiling);
        using var client = NewClient(app.Port);

        var response = await client.PostAsync(
            "/data/tick",
            TickRequestBody(new TickRequest(ProtocolVersion.Current.ToString(), 0, [])));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("payload_too_large", error.RootElement.GetProperty("error").GetString());
        var detail = error.RootElement.GetProperty("detail").GetString()!;
        Assert.Contains("cannot be split", detail, StringComparison.Ordinal);
        Assert.Contains("No partial response was sent", detail, StringComparison.Ordinal);

        // Rien n'est resté en attente : le run ne peut pas continuer sur un tick partiel.
        var page = await client.GetAsync("/data/tick/result?cursor=0.1.1.0");
        Assert.Equal(HttpStatusCode.Conflict, page.StatusCode);
    }

    [Fact]
    public async Task PagesOfAPreviousTickAreRefused()
    {
        var host = new SyntheticWorldHost(agents: 24, changesPerAgent: 64, events: 8);
        var runtime = new PrismHostRuntime(() => host);
        runtime.Prepare(new PrepareWorldRequest(1, "synthetic"));
        runtime.Start(new StartWorldRequest());

        await using var app = await StartAsync(runtime, TestCeiling);
        using var client = NewClient(app.Port);

        var first = await client.PostAsync(
            "/data/tick",
            TickRequestBody(new TickRequest(ProtocolVersion.Current.ToString(), 0, [])));
        using var firstDocument = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var staleCursor = firstDocument.RootElement.GetProperty("page").GetProperty("cursor").GetString()!;

        var second = await client.PostAsync(
            "/data/tick",
            TickRequestBody(new TickRequest(ProtocolVersion.Current.ToString(), 1, [])));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var stale = await client.GetAsync($"/data/tick/result?cursor={Uri.EscapeDataString(staleCursor)}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var error = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        Assert.Equal("page_expired", error.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task AnUnreadableCursorIsRefused()
    {
        var host = new SyntheticWorldHost(agents: 24, changesPerAgent: 64, events: 8);
        var runtime = new PrismHostRuntime(() => host);
        runtime.Prepare(new PrepareWorldRequest(1, "synthetic"));
        runtime.Start(new StartWorldRequest());

        await using var app = await StartAsync(runtime, TestCeiling);
        using var client = NewClient(app.Port);

        _ = await client.PostAsync(
            "/data/tick",
            TickRequestBody(new TickRequest(ProtocolVersion.Current.ToString(), 0, [])));

        var response = await client.GetAsync("/data/tick/result?cursor=not-a-cursor");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("page_invalid", error.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void PagingDoesNotChangeTheWorldNorTheEmittedStream()
    {
        // Même monde, mêmes commandes, deux plafonds : un tick servi d'un bloc et le
        // même tick découpé en pages doivent produire le même flux et le même monde.
        // Une observation pèse ≈ 40 Ko à saturation (256 entrées) : le plafond de test
        // doit tomber entre une observation et le tick complet, sinon la pagination
        // n'a rien à découper — et un élément insécable est justement refusé.
        var reference = RunRealWorld(maximumPageBytes: TickTransportLimits.MaximumTickResponseBytes);
        var paged = RunRealWorld(maximumPageBytes: 96 * 1024);

        Assert.True(paged.PageCounts.Max() > 1, "The 96 KiB ceiling did not force any page.");
        Assert.Equal(reference.WorldHash, paged.WorldHash);
        Assert.Equal(reference.Streams, paged.Streams);
        Assert.Equal(reference.Agents, paged.Agents);
    }

    [Fact]
    public async Task ThousandAgentsOverTwentyFiveTicksStayUnderTheCeilingAndMatchTheEightTickReference()
    {
        // Critère d'acceptation de PRISM-142 : 1 000 agents × 25 ticks aboutit, aucune
        // réponse ne dépasse 8 Mio, et le contenu est identique au run de référence de
        // 8 ticks (les huit premiers ticks sont comparés octet pour octet).
        var reference = RunRealWorld(
            maximumPageBytes: TickTransportLimits.MaximumTickResponseBytes,
            agents: 1_000,
            ticks: 8);
        var extended = RunRealWorld(
            maximumPageBytes: TickTransportLimits.MaximumTickResponseBytes,
            agents: 1_000,
            ticks: 25);

        Assert.Equal(25, extended.Streams.Count);
        Assert.Equal(reference.Streams, extended.Streams.Take(8).ToArray());
        Assert.Equal(reference.WorldHash, extended.WorldHashes[7]);
        Assert.Equal(reference.Agents, extended.Agents);
        Assert.True(
            extended.LargestResponseBytes <= TickTransportLimits.MaximumTickResponseBytes,
            $"A tick response reached {extended.LargestResponseBytes} bytes, above the " +
            $"{TickTransportLimits.MaximumTickResponseBytes}-byte ceiling.");
        Assert.True(
            extended.PageCounts.Max() > 1,
            "The 1 000-agent first tick should not fit in a single 8 MiB response.");
    }

    private static RealWorldRun RunRealWorld(int maximumPageBytes, int agents = 4, int ticks = 5)
    {
        var root = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));

        var runtime = new PrismHostRuntime(checkpointOnStop: false, checksumIntervalTicks: 10_000);
        runtime.Prepare(new PrepareWorldRequest(7, compilation.Pack!.ContentHash, root));
        var snapshot = runtime.WorldSnapshot!;
        var positions = FindIncarnationCells(snapshot, agents);
        runtime.Start(new StartWorldRequest());

        var streams = new List<string>(ticks);
        var worldHashes = new List<ulong>(ticks);
        var pageCounts = new List<int>(ticks);
        var largest = 0;
        var incarnated = 0;
        for (var tick = 0; tick < ticks; tick++)
        {
            var commands = tick == 0
                ? positions.Select((position, index) => IncarnateCommand((ulong)(index + 1), position)).ToArray()
                : Array.Empty<CommandEnvelope>();
            var result = runtime.Step(new TickRequest(
                ProtocolVersion.Current.ToString(),
                tick,
                commands));

            var plan = runtime.CreateTickPagePlan(result, maximumPageBytes);
            runtime.HoldTickPagePlan(plan);

            // Même parcours qu'un lecteur : page 1 par la réponse de tick, suivantes
            // par le curseur, jusqu'à la page qui s'annonce complète.
            var drained = DrainWithoutHttp(plan, runtime);
            streams.Add(ProtocolJson.Serialize(drained.Result));
            pageCounts.Add(drained.PageCount);
            largest = Math.Max(largest, drained.LargestBodyBytes);
            worldHashes.Add(runtime.WorldSnapshot!.WorldHash);
            incarnated = runtime.WorldSnapshot!.Agents!.Length;
        }

        return new RealWorldRun(streams, worldHashes, pageCounts, largest, incarnated);
    }

    private static DrainedTick DrainWithoutHttp(TickPagePlan plan, PrismHostRuntime runtime)
    {
        var largest = plan.FirstPage.Length;
        var pages = new List<TickPageEnvelope>();
        var body = Encoding.UTF8.GetString(plan.FirstPage);
        while (true)
        {
            TickPageEnvelope page;
            using (var document = JsonDocument.Parse(body))
            {
                if (!document.RootElement.TryGetProperty("page", out _))
                {
                    // Tick sous le plafond : la réponse reste un TickResult, on n'y touche pas.
                    return new DrainedTick(
                        ProtocolJson.Deserialize<TickResult>(document.RootElement),
                        1,
                        largest);
                }

                page = ProtocolJson.Deserialize<TickPageEnvelope>(document.RootElement);
            }

            pages.Add(page);
            if (page.Page.Complete)
            {
                break;
            }

            var next = runtime.RenderTickPage(page.Page.Cursor!);
            largest = Math.Max(largest, next.Length);
            body = Encoding.UTF8.GetString(next);
        }

        return new DrainedTick(Assemble(pages), pages.Count, largest);
    }

    private static TickResult Assemble(List<TickPageEnvelope> pages)
    {
        var observations = new List<ObservationEnvelope>();
        var events = new List<EventEnvelope>();
        foreach (var page in pages)
        {
            observations.AddRange(page.Chunk.Observations);
            events.AddRange(page.Chunk.Events);
        }

        return new TickResult(pages[0].ProtocolVersion, pages[0].Tick, observations, events);
    }

    private static async Task<DrainedTick> DrainAsync(string firstBody, Func<string, Task<string>> fetchPage)
    {
        var pages = new List<TickPageEnvelope>();
        var largest = 0;
        var body = firstBody;
        while (true)
        {
            largest = Math.Max(largest, Encoding.UTF8.GetByteCount(body));
            TickPageEnvelope page;
            using (var document = JsonDocument.Parse(body))
            {
                page = ProtocolJson.Deserialize<TickPageEnvelope>(document.RootElement);
            }

            pages.Add(page);
            if (page.Page.Complete)
            {
                break;
            }

            body = await fetchPage(page.Page.Cursor!);
        }

        return new DrainedTick(Assemble(pages), pages.Count, largest);
    }

    private static CommandEnvelope IncarnateCommand(ulong agentId, BlockCoordinate position)
    {
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(
            new { position = new { x = position.X, y = position.Y, z = position.Z } },
            ProtocolJson.Options));
        return new CommandEnvelope(
            ProtocolVersion.Current.ToString(),
            0,
            $"incarnate-{agentId}",
            agentId,
            "Incarner",
            payload.RootElement.Clone());
    }

    private static BlockCoordinate[] FindIncarnationCells(GeneratedWorldSnapshot world, int agents)
    {
        var occupied = world.Overrides
            .Where(static entry => !entry.Value.IsAir)
            .Select(static entry => entry.Key)
            .ToHashSet();
        var cells = new List<BlockCoordinate>(agents);
        for (var y = 0; y < world.Height && cells.Count < agents; y++)
        {
            for (var x = 0; x < world.Width && cells.Count < agents; x++)
            {
                var surface = world.SurfaceColumns[x + world.Width * y].SurfaceZ;
                var origin = new BlockCoordinate(x, y, surface + 1);
                if (origin.Z < world.VerticalSize && !occupied.Contains(origin))
                {
                    cells.Add(origin);
                }
            }
        }

        if (cells.Count < agents)
        {
            throw new InvalidOperationException(
                $"The generated test world only holds {cells.Count} clear cells for {agents} agents.");
        }

        return cells.ToArray();
    }

    private static async Task<RunningHost> StartAsync(PrismHostRuntime runtime, int ceiling)
    {
        var port = FreePort();
        var app = PrismHostServer.Build(port, runtime, "session-secret", maximumTickResponseBytes: ceiling);
        await app.StartAsync();
        return new RunningHost(app, port);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static HttpClient NewClient(int port)
    {
        var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        client.DefaultRequestHeaders.Add("X-LIVEX-Session-Token", "session-secret");
        return client;
    }

    private static StringContent TickRequestBody<T>(T value) =>
        new(ProtocolJson.Serialize(value), Encoding.UTF8, "application/json");

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

    private sealed record DrainedTick(TickResult Result, int PageCount, int LargestBodyBytes);

    private sealed record RealWorldRun(
        IReadOnlyList<string> Streams,
        IReadOnlyList<ulong> WorldHashes,
        IReadOnlyList<int> PageCounts,
        int LargestResponseBytes,
        int Agents)
    {
        public ulong WorldHash => WorldHashes[^1];
    }

    private sealed class RunningHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public RunningHost(WebApplication app, int port)
        {
            _app = app;
            Port = port;
        }

        public int Port { get; }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// Hôte de monde synthétique : il ne simule rien, il produit le volume d'un tick
    /// à 1 000 agents (observations saturées et événements) pour éprouver le transport
    /// sans payer une vraie perception.
    /// </summary>
    private sealed class SyntheticWorldHost(
        int agents,
        int changesPerAgent,
        int events,
        int oversizedSelfBytes = 0) : IWorldHost
    {
        public TickResult Step(TickRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return BuildTick(request.Tick, agents, changesPerAgent, events, oversizedSelfBytes);
        }

        public static TickResult BuildTick(
            long tick,
            int agents,
            int changesPerAgent,
            int events,
            int oversizedSelfBytes = 0)
        {
            var protocolVersion = ProtocolVersion.Current.ToString();
            var observations = new ObservationEnvelope[agents];
            for (var agent = 0; agent < agents; agent++)
            {
                observations[agent] = BuildObservation(tick, (ulong)agent, changesPerAgent, oversizedSelfBytes);
            }

            var eventEnvelopes = new EventEnvelope[events];
            for (var index = 0; index < events; index++)
            {
                eventEnvelopes[index] = new EventEnvelope(
                    protocolVersion,
                    tick,
                    $"evt-{tick}-{index}",
                    "action_completed",
                    (ulong)(index % Math.Max(1, agents)),
                    JsonSerializer.SerializeToElement(
                        new { commandId = $"cmd-{tick}-{index}", result = "completed" },
                        ProtocolJson.Options));
            }

            return new TickResult(protocolVersion, tick, observations, eventEnvelopes);
        }

        private static ObservationEnvelope BuildObservation(
            long tick,
            ulong agentId,
            int changesPerAgent,
            int oversizedSelfBytes)
        {
            var changes = new object[changesPerAgent];
            for (var index = 0; index < changesPerAgent; index++)
            {
                changes[index] = new
                {
                    change = 1,
                    entry = new
                    {
                        kind = 3,
                        x = index,
                        y = (int)(agentId % 64),
                        z = 56,
                        id = 0,
                        materialId = 4,
                        state = 0,
                        itemTypeId = 0,
                        quantity = 0,
                        tagMask = 0,
                        siteId = (string?)null,
                        workRequired = 0,
                    },
                };
            }

            var self = oversizedSelfBytes > 0
                ? JsonSerializer.SerializeToElement(
                    new
                    {
                        position = new { x = 0, y = 0, z = 0 },
                        note = new string('x', oversizedSelfBytes),
                    },
                    ProtocolJson.Options)
                : JsonSerializer.SerializeToElement(
                    new
                    {
                        position = new
                        {
                            x = (int)(agentId % 256) * 1000 + 500,
                            y = (int)(agentId / 256) * 1000 + 500,
                            z = 57000,
                        },
                        inventory = Array.Empty<object>(),
                        sheltered = false,
                    },
                    ProtocolJson.Options);

            return new ObservationEnvelope(
                ProtocolVersion.Current.ToString(),
                tick,
                agentId,
                JsonSerializer.SerializeToElement(new { changes }, ProtocolJson.Options),
                self);
        }
    }
}
