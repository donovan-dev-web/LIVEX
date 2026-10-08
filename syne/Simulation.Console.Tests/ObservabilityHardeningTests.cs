using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Simulation.Console.Observability;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using World = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Cible d'observabilité (jalon review/refactor, engineVersion 0.12.0).
///
/// <para>
/// Invariant : l'observabilité ne doit ni bloquer, ni faire échouer la boucle de
/// simulation (DETERMINISM.md §3). Avant correction, un client lent gelait le
/// run (aucun timeout sur l'envoi), deux diffusions concurrentes sur le même
/// <see cref="WebSocket"/> levaient <c>InvalidOperationException</c>, un groupe
/// formé puis dissous dans le même tick faisait échouer l'émission, et les
/// tampons du tick n'étaient jamais vidés sans abonné.
/// </para>
/// </summary>
public sealed class ObservabilityHardeningTests
{
    private static SimulationLoop NewLoop(ulong seed = 1234)
    {
        var world = new World(new WorldSize(250, 250), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= 4; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        SimulationOptions options = SimulationProfiles.Reference();
        options.Simulation.TicksPerSecond = 1000;
        return new SimulationLoop(world, rng, options);
    }

    /// <summary>Sink qui bloque indéfiniment : ne doit jamais bloquer l'émetteur.</summary>
    private sealed class StalledSink : IObservabilitySink, IObservabilityDemand
    {
        public bool HasSubscribers => true;

        public Task BroadcastAsync(string text) => Task.Delay(Timeout.Infinite);
    }

    private sealed class RecordingSink : IObservabilitySink, IObservabilityDemand
    {
        public bool HasSubscribers { get; set; } = true;

        public ConcurrentQueue<string> Frames { get; } = new();

        public Task BroadcastAsync(string text)
        {
            Frames.Enqueue(text);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Broadcast_ToAStalledSink_DoesNotHangTheSimulation()
    {
        var loop = NewLoop();
        var emitter = new ObservabilityTickEmitter(loop, 1234, new StalledSink(), "run-stalled");

        // Le sink ne rend jamais la main : l'émission doit tout de même finir,
        // faute de quoi la boucle de simulation reste figée.
        Task emit = emitter.EmitCurrentTickAsync();
        Task completed = await Task.WhenAny(emit, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.Same(emit, completed);
        await emit;
    }

    [Fact]
    public async Task NoSubscribers_SkipsSnapshotButStillDrainsTickBuffers()
    {
        var loop = NewLoop();
        var sink = new RecordingSink { HasSubscribers = false };
        var emitter = new ObservabilityTickEmitter(loop, 1234, sink, "run-nosub");

        // Tampon réellement alimenté : sans le vidage, il stagnerait et
        // rejouerait un événement obsolète à la première diffusion.
        loop.World.PlaceConstruction(new Obstacle("maison-1", new Position(60, 60), radius: 10));
        Assert.Single(loop.World.LastEnvironmentChanges);

        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        // Aucun work inutile quand personne n'écoute...
        Assert.Empty(sink.Frames);

        // ...mais les tampons ne doivent pas grossir indéfiniment.
        Assert.Empty(loop.World.LastEnvironmentChanges);
    }

    [Fact]
    public async Task ConstructionPlacedWhileSilentlyDriven_IsNotReplayedLater()
    {
        var loop = NewLoop();
        var sink = new RecordingSink { HasSubscribers = false };
        var emitter = new ObservabilityTickEmitter(loop, 1234, sink, "run-replay");

        loop.World.PlaceConstruction(new Obstacle("maison-fantome", new Position(80, 80), radius: 10));
        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        sink.HasSubscribers = true;
        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        // Le snapshot, lui, doit au contraire refléter l'obstacle réel du monde.
        // C'est l'ÉVÉNEMENT « construction placée » du tick sans abonné qui ne
        // doit pas être rejoué plus tard comme s'il venait d'arriver.
        List<string> events = sink.Frames
            .Where(frame => !frame.Contains("\"type\":\"snapshot\"", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(events, frame => frame.Contains("construction_placed", StringComparison.Ordinal));
        Assert.DoesNotContain(events, frame => frame.Contains("world_delta", StringComparison.Ordinal));
        Assert.Contains(sink.Frames, frame => frame.Contains("maison-fantome", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SubscriberAppearingAfterSilentTicks_ReceivesOnlyCurrentTick()
    {
        var loop = NewLoop();
        var sink = new RecordingSink { HasSubscribers = false };
        var emitter = new ObservabilityTickEmitter(loop, 1234, sink, "run-late");

        for (int i = 0; i < 5; i++)
        {
            loop.AdvanceOneTick();
            await emitter.EmitCurrentTickAsync();
        }

        Assert.Empty(sink.Frames);

        sink.HasSubscribers = true;
        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        // Aucun événement fantôme des 5 ticks précédents.
        Assert.NotEmpty(sink.Frames);
        Assert.Contains(sink.Frames, frame => frame.Contains("\"tick\":6", StringComparison.Ordinal));
        Assert.DoesNotContain(sink.Frames, frame => frame.Contains("\"tick\":1,", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConcurrentBroadcasts_DoNotThrow()
    {
        var loop = NewLoop();
        var sink = new RecordingSink();
        var emitter = new ObservabilityTickEmitter(loop, 1234, sink, "run-concurrent");

        // Deux émissions imbriquées sur le même sink : le serveur WebSocket réel
        // n'accepte qu'un SendAsync à la fois par socket.
        Task first = emitter.EmitCurrentTickAsync();
        Task second = emitter.EmitCurrentTickAsync();
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task ObservabilityServer_ReportsNoSubscribersBeforeAndAfterLifecycle()
    {
        int port = FreePort();
        await using var server = new ObservabilityServer(port);

        Assert.False(server.HasSubscribers);
        Assert.Equal(0, server.ClientCount);

        server.Start();
        Assert.True(server.IsListening);
        Assert.False(server.HasSubscribers);

        await server.DisposeAsync();

        // Les clients sont purgés : sinon ClientCount mentait après l'arrêt.
        Assert.Equal(0, server.ClientCount);
        Assert.False(server.HasSubscribers);
    }

    [Fact]
    public async Task ObservabilityServer_DisposeIsIdempotent()
    {
        var server = new ObservabilityServer(FreePort());
        server.Start();

        await server.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task ObservabilityServer_StartTwice_DoesNotLeakAListener()
    {
        var server = new ObservabilityServer(FreePort());
        server.Start();
        server.Start();

        Assert.True(server.IsListening);
        await server.DisposeAsync();
    }

    [Fact]
    public async Task ObservabilityServer_BroadcastAfterDispose_IsANoOp()
    {
        var server = new ObservabilityServer(FreePort());
        server.Start();
        await server.DisposeAsync();

        // Doit être silencieux plutôt que de lever sur une trame de fin de run.
        await server.BroadcastAsync("{\"type\":\"post_dispose\"}");
    }

    [Fact]
    public async Task ObservabilityServer_RealClient_ReceivesFrames_AndIsDroppedOnDisconnect()
    {
        int port = FreePort();
        await using var server = new ObservabilityServer(port);
        server.Start();

        using var client = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), cts.Token);

        await WaitUntilAsync(() => server.ClientCount == 1, TimeSpan.FromSeconds(10));
        Assert.True(server.HasSubscribers);

        await server.BroadcastAsync("{\"type\":\"ping\"}");

        byte[] buffer = new byte[4096];
        WebSocketReceiveResult result = await client.ReceiveAsync(buffer, cts.Token);
        string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Assert.Contains("ping", text, StringComparison.Ordinal);

        client.Abort();

        // Le retrait doit être effectif, pas seulement noticed à la diffusion
        // suivante (le drain le faisait déjà avant).
        await WaitUntilAsync(() => server.ClientCount == 0, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ObservabilityServer_ParallelBroadcasts_ToRealClient_AreSerialized()
    {
        int port = FreePort();
        await using var server = new ObservabilityServer(port);
        server.Start();

        using var client = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), cts.Token);
        await WaitUntilAsync(() => server.ClientCount == 1, TimeSpan.FromSeconds(10));

        // Sans sémaphore par client, ces envois concurrents lèvent
        // InvalidOperationException côté serveur.
        await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(i => server.BroadcastAsync($"{{\"n\":{i}}}")));

        byte[] buffer = new byte[4096];
        var received = new HashSet<string>();
        while (received.Count < 40)
        {
            WebSocketReceiveResult result = await client.ReceiveAsync(buffer, cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            received.Add(Encoding.UTF8.GetString(buffer, 0, result.Count));
        }

        Assert.Equal(40, received.Count);
        Assert.Equal(1, server.ClientCount);
    }

    [Fact]
    public async Task ObservabilityServer_ClientThatStopsReading_IsRemovedAndBroadcastKeepsWorking()
    {
        int port = FreePort();
        await using var server = new ObservabilityServer(port);
        server.Start();

        using var client = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), cts.Token);
        await WaitUntilAsync(() => server.ClientCount == 1, TimeSpan.FromSeconds(10));

        // Le client ne lit plus : les tampons TCP se remplissent et l'envoi
        // serveur finit par se bloquer jusqu'à l'expiration de SendTimeout.
        // V3 (08/10/2026) : TimeoutException échappait à SendTextAsync — le
        // client n'était jamais retiré, chaque trame rebloquait 2 s et la
        // cadence moteur se couplait au consommateur. Il doit être abandonné,
        // et l'observabilité rester utilisable juste après.
        const int frameSize = 512 * 1024;
        string payload = new('x', frameSize);
        Task broadcast = Task.Run(async () =>
        {
            for (var i = 0; i < 64 && server.ClientCount > 0; i++)
            {
                await server.BroadcastAsync(payload);
            }
        });

        Task finished = await Task.WhenAny(broadcast, Task.Delay(TimeSpan.FromSeconds(45)));
        Assert.Same(broadcast, finished);
        await broadcast;

        // Client retiré malgré l'envoi bloqué, pas seulement « noticed ».
        await WaitUntilAsync(() => server.ClientCount == 0, TimeSpan.FromSeconds(10));
        Assert.False(server.HasSubscribers);

        // La diffusion suivante revient immédiatement : personne ne doit
        // hériter du verrou laissé par le client abandonné.
        var resend = server.BroadcastAsync("{\"type\":\"ping\"}");
        finished = await Task.WhenAny(resend, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(resend, finished);
        await resend;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "condition non satisfaite avant l'échéance.");
    }

    private static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
