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
        var options = ConfigLoader.LoadDefaults();
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
        for (int i = 0; i < 100 && !condition(); i++)
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