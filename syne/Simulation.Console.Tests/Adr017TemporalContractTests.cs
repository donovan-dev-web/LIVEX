using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Simulation.Console.Control;
using Simulation.Console.Observability;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Contrats additifs de l'échelle temporelle (ADR-017, spec PRISM §4.3) :
/// la préparation du profil <c>prism</c> porte <c>simulatedSecondsPerTick</c>
/// et <c>metersPerUnit</c> dans la description <c>world_initialized</c>
/// (version 1.1) et dans <c>/api/control/status</c>, sans rien changer au
/// cycle de contrôle.
/// </summary>
public sealed class Adr017TemporalContractTests : IAsyncLifetime
{
    private readonly ControlServer _server = new(FreePort());
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public Task InitializeAsync()
    {
        _server.Start();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task PreparePrism_CarriesTheScaleOnWorldAndStatus()
    {
        using var prepare = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/prepare"))
        {
            // Le profil prism est complet (§4.4) : passé comme surcouche, il
            // s'applique sur les défauts avec la sémantique de fusion usuelle.
            Content = JsonBody($$"""{ "seed": 7, "config": {{SimulationProfiles.PrismJson()}} }"""),
        };
        using HttpResponseMessage prepared = await _http.SendAsync(prepare);
        Assert.Equal(System.Net.HttpStatusCode.OK, prepared.StatusCode);

        using HttpResponseMessage worldResponse = await _http.GetAsync(Url("/api/world"));
        JsonNode? world = JsonNode.Parse(await worldResponse.Content.ReadAsStringAsync());
        Assert.Equal("1.1", (string?)world?["version"]);
        Assert.Equal(5, (int?)world?["simulatedSecondsPerTick"]);
        Assert.Equal(1.0, (double?)world?["metersPerUnit"]);
        Assert.Equal(6, (int?)world?["ticksPerSecond"]);
        Assert.Equal(2240, (int?)world?["width"]);
        Assert.Equal(70, (int?)world?["cellCountX"]);
        Assert.Equal(4_900, ((JsonArray?)world?["cells"])?.Count);

        using HttpResponseMessage statusResponse = await _http.GetAsync(Url("/api/control/status"));
        JsonNode? status = JsonNode.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal("ready", (string?)status?["state"]);
        Assert.Equal(5, (int?)status?["simulatedSecondsPerTick"]);
        Assert.Equal(6, (int?)status?["ticksPerSecond"]);
    }

    [Fact]
    public async Task Status_WithoutPreparedWorld_KeepsTheScaleNull()
    {
        using HttpResponseMessage statusResponse = await _http.GetAsync(Url("/api/control/status"));
        JsonNode? status = JsonNode.Parse(await statusResponse.Content.ReadAsStringAsync());
        Assert.Equal("idle", (string?)status?["state"]);
        Assert.Null(status?["simulatedSecondsPerTick"]);
        Assert.Null(status?["ticksPerSecond"]);
    }

    [Fact]
    public async Task WorldInitializedFrame_CarriesTheScale()
    {
        var sink = new RecordingSink();
        await using var controller = new SimulationController(sink);

        await controller.PrepareAsync(7, SimulationProfiles.PrismJson());

        string frame = Assert.Single(sink.Messages);
        JsonNode? parsed = JsonNode.Parse(frame);
        Assert.Equal("world_initialized", (string?)parsed?["type"]);
        Assert.Equal("1.1", (string?)parsed?["version"]);
        Assert.Equal(5, (int?)parsed?["world"]?["simulatedSecondsPerTick"]);
        Assert.Equal(1.0, (double?)parsed?["world"]?["metersPerUnit"]);
        Assert.Equal(6, (int?)parsed?["world"]?["ticksPerSecond"]);
    }

    [Fact]
    public async Task StatusJson_TicksPerSecondAndScale_AreNullableUntilPrepared()
    {
        // Même sémantique que ticksPerSecond : null tant qu'aucun monde n'est
        // préparé, puis la valeur du monde préparé.
        await using var controller = new SimulationController();
        Assert.Null(controller.WorldDescription);

        await controller.PrepareAsync(9, SimulationProfiles.ReferenceJson());
        Assert.Equal(60, controller.WorldDescription?.SimulatedSecondsPerTick);
        Assert.Equal(1.0, controller.WorldDescription?.MetersPerUnit);
    }

    private string Url(string path) => $"http://127.0.0.1:{_server.Port}{path}";

    private static HttpContent? JsonBody(string json) => new StringContent(json, Encoding.UTF8, "application/json");

    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class RecordingSink : IObservabilitySink
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public Task BroadcastAsync(string text)
        {
            Messages.Enqueue(text);
            return Task.CompletedTask;
        }
    }
}
