using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Simulation.Console.Control;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Console.Tests;

public class ControlServerWireTests : IAsyncLifetime
{
    private readonly ControlServer _server = new(FreePort());
    private readonly HttpClient _http = new()
    {
        BaseAddress = null,
        Timeout = TimeSpan.FromSeconds(15),
    };

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
    public async Task ExplicitPrepareRequiresReadyBeforeStart()
    {
        using var invalidPrepare = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/prepare"))
        {
            Content = JsonBody("{ \"seed\": 123, \"ticksPerSecond\": 0 }"),
        };
        using HttpResponseMessage invalidPrepared = await _http.SendAsync(invalidPrepare);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalidPrepared.StatusCode);

        using var prepare = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/prepare"))
        {
            Content = JsonBody("""
                {
                  "seed": 123,
                  "ticksPerSecond": 24,
                  "config": {
                    "world": {
                      "obstacles": true,
                      "obstacleLayout": [
                        { "id": "initial-rock", "x": 15, "y": 25, "radius": 4 }
                      ]
                    }
                  }
                }
                """),
        };
        using HttpResponseMessage prepared = await _http.SendAsync(prepare);
        Assert.Equal(System.Net.HttpStatusCode.OK, prepared.StatusCode);

        using HttpResponseMessage worldResponse = await _http.GetAsync(Url("/api/world"));
        Assert.Equal(System.Net.HttpStatusCode.OK, worldResponse.StatusCode);
        JsonNode? worldBody = await ReadJsonAsync(worldResponse);
        Assert.Equal(24, (int?)worldBody?["ticksPerSecond"]);
        JsonArray? obstacles = worldBody?["obstacles"]?.AsArray();
        JsonNode? initialObstacle = Assert.Single(obstacles!);
        Assert.Equal("initial-rock", (string?)initialObstacle?["id"]);
        Assert.Equal(15, (double?)initialObstacle?["x"]);
        Assert.Equal(25, (double?)initialObstacle?["y"]);
        Assert.Equal(4, (double?)initialObstacle?["radius"]);
        JsonArray? initialAgents = worldBody?["agents"]?.AsArray();
        Assert.NotNull(initialAgents);
        Assert.NotEmpty(initialAgents);
        Assert.All(initialAgents!, agent =>
        {
            Assert.NotNull(agent?["id"]);
            Assert.NotNull(agent?["species"]);
            Assert.NotNull(agent?["position"]?["x"]);
            Assert.NotNull(agent?["position"]?["y"]);
        });

        using var start = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("{ \"maxTicks\": 1 }"),
        };
        using HttpResponseMessage rejected = await _http.SendAsync(start);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, rejected.StatusCode);
        JsonNode? rejectedBody = await ReadJsonAsync(rejected);
        Assert.Equal("world_not_ready", (string?)rejectedBody!["error"]);

        using var ready = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/ready"))
        {
            Content = JsonBody("{ \"worldVersion\": \"1.0\" }"),
        };
        using HttpResponseMessage acknowledged = await _http.SendAsync(ready);
        Assert.Equal(System.Net.HttpStatusCode.OK, acknowledged.StatusCode);

        using var startAfterReady = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("{ \"seed\": 123, \"maxTicks\": 1 }"),
        };
        using var mismatchedStart = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("{ \"seed\": 124, \"maxTicks\": 1 }"),
        };
        using HttpResponseMessage mismatch = await _http.SendAsync(mismatchedStart);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, mismatch.StatusCode);
        JsonNode? mismatchBody = await ReadJsonAsync(mismatch);
        Assert.Equal("prepared_seed_mismatch", (string?)mismatchBody?["error"]);

        using HttpResponseMessage started = await _http.SendAsync(startAfterReady);
        Assert.Equal(System.Net.HttpStatusCode.OK, started.StatusCode);
        using HttpResponseMessage statusAfterStart = await _http.GetAsync(Url("/api/control/status"));
        JsonNode? runningStatus = await ReadJsonAsync(statusAfterStart);
        Assert.Equal(24, (int?)runningStatus?["ticksPerSecond"]);
    }

    [Fact]
    public async Task Start_AfterFinished_ReturnsRunFinishedInsteadOfWorldNotReady()
    {
        // D2 : après un run terminé naturellement, le monde est prêt mais le run
        // n'avancera plus. Le 409 générique world_not_ready orientait le client
        // vers /prepare au lieu de /reset.
        using var start = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("{ \"seed\": 21, \"maxTicks\": 1 }"),
        };
        using var _ = await _http.SendAsync(start);
        await WaitUntilAsync(() => _server.Controller.State == SimulationControlState.Finished);

        using var restart = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("{ \"seed\": 21, \"maxTicks\": 1 }"),
        };
        using HttpResponseMessage response = await _http.SendAsync(restart);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
        JsonNode? body = await ReadJsonAsync(response);
        Assert.Equal("run_finished", (string?)body!["error"]);
    }

    [Fact]
    public async Task Start_ReturnsRunIdAndStatusShowsRunning()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("{ \"seed\": 42, \"maxTicks\": 5000 }"),
        };

        using HttpResponseMessage response = await _http.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        JsonNode? body = await ReadJsonAsync(response);
        Assert.True((bool?)body!["ok"]);
        Assert.Equal("started", (string?)body["action"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)body["runId"]));
        Assert.Equal(42ul, (ulong?)body["seed"]);
        Assert.True(_server.Controller.HasRun);
    }

    [Fact]
    public async Task Pause_FreezesTick_Resume_Resumes()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("{ \"seed\": 7, \"maxTicks\": 1000000 }"),
        };
        using var _ = await _http.SendAsync(request);

        // Laisse la boucle avancer.
        await WaitUntilAsync(() => _server.Controller.Status().Tick > 0);

        using var pause = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/pause"));
        using var pauseResponse = await _http.SendAsync(pause);
        Assert.Equal(System.Net.HttpStatusCode.OK, pauseResponse.StatusCode);

        await WaitUntilAsync(() => _server.Controller.State == SimulationControlState.Paused);

        ulong frozen = _server.Controller.Status().Tick;
        await Task.Delay(150);
        Assert.Equal(frozen, _server.Controller.Status().Tick);

        using var resume = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/resume"));
        using var resumeResponse = await _http.SendAsync(resume);
        Assert.Equal(System.Net.HttpStatusCode.OK, resumeResponse.StatusCode);

        await WaitUntilAsync(() => _server.Controller.Status().Tick > frozen);
    }

    [Fact]
    public async Task Status_ExposesFullState()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("{ \"seed\": 12345, \"maxTicks\": 100 }"),
        };
        using var _ = await _http.SendAsync(request);

        await WaitUntilAsync(() => _server.Controller.Status().Tick > 0);

        using HttpResponseMessage response = await _http.GetAsync(Url("/api/control/status"));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        JsonNode? status = await ReadJsonAsync(response);
        string state = (string?)status!["state"] ?? "";
        Assert.True(state is "running" or "finished", $"État inattendu : {state}");
        Assert.NotNull(status["runId"]);
        Assert.True((ulong?)status["tick"] >= 1);
        Assert.Equal(12345ul, (ulong?)status["seed"]);
        Assert.Equal(100, (int?)status["maxTicks"]);
    }

    [Fact]
    public async Task Reset_RestartsWithNewRunId()
    {
        using var start = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody("{ \"seed\": 11, \"maxTicks\": 50000 }"),
        };
        using HttpResponseMessage startResponse = await _http.SendAsync(start);
        JsonNode? firstBody = await ReadJsonAsync(startResponse);
        string firstRunId = (string?)firstBody!["runId"] ?? "";
        await WaitUntilAsync(() => _server.Controller.Status().Tick > 5);

        using var reset = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/reset"))
        {
            Content = JsonBody("{ \"seed\": 99, \"maxTicks\": 50000 }"),
        };
        using HttpResponseMessage resetResponse = await _http.SendAsync(reset);
        Assert.Equal(System.Net.HttpStatusCode.OK, resetResponse.StatusCode);

        JsonNode? resetBody = await ReadJsonAsync(resetResponse);
        Assert.Equal("reset", (string?)resetBody!["action"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)resetBody["runId"]));

        await WaitUntilAsync(() => _server.Controller.Status().Tick > 0);
        Assert.Equal(99ul, _server.Controller.Status().Seed);
    }

    [Fact]
    public async Task ControlledRun_MatchesBatchRun_TickForTick()
    {
        // SYNE-113/081 : le contrôle HTTP ne doit pas altérer la trajectoire.
        // Un run piloté (start + pause + resume arbitraires) doit produire un
        // état identique à un run ininterrompu, à seed et population égales.
        const ulong seed = 4242;
        const int maxTicks = 120;

        using var request = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/start"))
        {
            Content = JsonBody($"{{\"seed\":{seed},\"maxTicks\":{maxTicks}}}"),
        };
        using var _ = await _http.SendAsync(request);

        // Interrompt plusieurs fois la boucle (la trajectoire ne doit pas changer).
        for (int i = 0; i < 3; i++)
        {
            await WaitUntilAsync(() => _server.Controller.Status().Tick >= (ulong)(10 * (i + 1)));
            using var pause = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/pause"));
            using var pauseResponse = await _http.SendAsync(pause);
            await Task.Delay(30);
            using var resume = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/resume"));
            using var resumeResponse = await _http.SendAsync(resume);
        }

        await WaitUntilAsync(() => _server.Controller.State == SimulationControlState.Finished);
        Assert.Equal((ulong)maxTicks, _server.Controller.Status().Tick);

        // Rendu de référence ininterrompu (même construction que SimulationFactory).
        var options = SimulationProfiles.Reference();
        var (_, referenceLoop) = SimulationFactory.Build(options, seed);
        referenceLoop.Run(maxTicks);

        Assert.Equal(referenceLoop.CurrentTick, _server.Controller.Status().Tick);
        Assert.Equal(referenceLoop.World.Entities.Count, _server.Controller.Status().AliveCount);
    }

    [Fact]
    public async Task UnknownAction_Returns404WithErrorJson()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Url("/api/control/bogus"));
        using HttpResponseMessage response = await _http.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);

        JsonNode? body = await ReadJsonAsync(response);
        Assert.False((bool?)body!["ok"]);
        Assert.Equal("not_found", (string?)body["error"]);
    }

    private string Url(string path) => $"http://127.0.0.1:{_server.Port}{path}";

    private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response)
    {
        string text = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(text);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        // À 10 ticks/s, un run de 120 ticks dure au moins 12 secondes.
        for (int i = 0; i < 800 && !condition(); i++)
        {
            await Task.Delay(25);
        }

        Assert.True(condition(), "Condition non remplie dans le délai imparti.");
    }

    private static HttpContent? JsonBody(string json) => new StringContent(json, Encoding.UTF8, "application/json");

    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}