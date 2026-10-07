using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Simulation.Console.Control;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class LauncherSupervisionTests
{
    [Fact]
    public async Task ReadinessInfoAndAuthenticatedShutdownFollowLauncherContract()
    {
        int port = FreePort();
        await using var server = new ControlServer(
            port,
            sessionToken: "session-secret",
            instanceId: "syne-instance",
            initiallyReady: false);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };
        server.Start();

        using HttpResponseMessage live = await client.GetAsync("health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        using HttpResponseMessage notReady = await client.GetAsync("health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, notReady.StatusCode);
        server.MarkReady();

        using HttpResponseMessage ready = await client.GetAsync("health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        using JsonDocument info = JsonDocument.Parse(await client.GetStringAsync("info"));
        Assert.Equal("syne", info.RootElement.GetProperty("id").GetString());
        Assert.Equal("0.15.0", info.RootElement.GetProperty("version").GetString());
        Assert.Equal(1, info.RootElement.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("syne-instance", info.RootElement.GetProperty("instanceId").GetString());

        using var unauthenticatedMutation = new HttpRequestMessage(HttpMethod.Post, "api/control/pause");
        using HttpResponseMessage deniedMutation = await client.SendAsync(unauthenticatedMutation);
        Assert.Equal(HttpStatusCode.Unauthorized, deniedMutation.StatusCode);

        using var unauthorizedShutdown = new HttpRequestMessage(HttpMethod.Post, "control/shutdown");
        using HttpResponseMessage unauthorized = await client.SendAsync(unauthorizedShutdown);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var shutdown = new HttpRequestMessage(HttpMethod.Post, "control/shutdown");
        shutdown.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "session-secret");
        using HttpResponseMessage accepted = await client.SendAsync(shutdown);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await server.ShutdownRequested.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
