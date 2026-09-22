using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Simulation.Console.Observability;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using WorldType = Simulation.Core.World.World;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Console.Tests;

public class ObservabilityServerWireTests
{
    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task EmitCurrentTick_BroadcastsSnapshotOverWebSocket()
    {
        await using var server = new ObservabilityServer(FreePort());
        server.Start();

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/"), CancellationToken.None);
        await WaitForClient(server);

        var world = new WorldType(new WorldSize(100, 100));
        world.AddEntity(new Entity(new EntityId(1), "Entité A", null, new Position(10, 10), TraitSet.NeutralAll, bornAt: 0));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), ConfigLoader.LoadDefaults());
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, server);
        await emitter.RunAsync(1);

        JsonNode? json = JsonNode.Parse(await ReceiveTextAsync(client));
        Assert.NotNull(json);
        Assert.Equal("snapshot", (string?)json!["type"]);
        Assert.Equal(1u, (uint?)json!["tick"]);
        Assert.Equal(1, (int?)json!["aliveCount"]);
        Assert.Equal("Entité A", (string?)json!["agents"]![0]!["species"]);
    }

    [Fact]
    public async Task RunAsync_EmitsOneSnapshotPerTick()
    {
        await using var server = new ObservabilityServer(FreePort());
        server.Start();

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/"), CancellationToken.None);
        await WaitForClient(server);

        var world = new WorldType(new WorldSize(100, 100));
        world.AddEntity(new Entity(new EntityId(1), "Entité A", null, new Position(10, 10), TraitSet.NeutralAll, bornAt: 0));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), ConfigLoader.LoadDefaults());
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, server);
        await emitter.RunAsync(3);

        Assert.Equal(3ul, loop.CurrentTick);
        Assert.Equal(3ul, emitter.TicksEmitted);

        var snapshotTicks = new List<uint>();
        while (snapshotTicks.Count < 3)
        {
            JsonNode? frame = JsonNode.Parse(await ReceiveTextAsync(client));
            if ((string?)frame!["type"] == "snapshot")
            {
                snapshotTicks.Add((uint?)frame!["tick"] ?? 0);
            }
        }

        Assert.Equal([1u, 2u, 3u], snapshotTicks);
    }

    [Fact]
    public async Task RunAsync_EmitsActionCompletedEventAfterExecution()
    {
        // SYNE-040/080 : chaque tick émet l'événement action_completed de l'action
        // atomique exécutée (API_CONTRACTS.md §2.2).
        await using var server = new ObservabilityServer(FreePort());
        server.Start();

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/"), CancellationToken.None);
        await WaitForClient(server);

        var world = new WorldType(new WorldSize(100, 100));
        world.AddEntity(new Entity(new EntityId(1), "Entité A", null, new Position(10, 10), TraitSet.NeutralAll, bornAt: 0));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), ConfigLoader.LoadDefaults());
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, server);
        await emitter.RunAsync(12);

        string? outcome = null;
        while (outcome is null)
        {
            JsonNode? frame = JsonNode.Parse(await ReceiveTextAsync(client));
            if ((string?)frame!["type"] == "action_completed")
            {
                outcome = (string?)frame!["value"]!["outcome"];
            }
        }

        Assert.NotNull(outcome);
        Assert.True(outcome == "executed" || outcome == "blocked", $"outcome inattendu : {outcome}");
        Assert.NotNull(loop.Cognition.MindOf(1).LastActionResult);
    }

    private static async Task WaitForClient(ObservabilityServer server)
    {
        for (int i = 0; i < 50 && server.ClientCount == 0; i++)
        {
            await Task.Delay(50);
        }

        Assert.NotEqual(0, server.ClientCount);
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket client)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await client.ReceiveAsync(buffer, CancellationToken.None);
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}