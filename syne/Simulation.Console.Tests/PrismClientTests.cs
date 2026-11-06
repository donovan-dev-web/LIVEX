using System.Net;
using System.Text;
using System.Text.Json;
using Prism.Contracts;
using Simulation.Core.Cognition;
using Simulation.Core.World;
using Simulation.Console.Integration;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class PrismClientTests
{
    [Fact]
    public async Task SendsAuthenticatedControlCallsAndValidatesLockstepResponses()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5182/"),
        };
        var client = new PrismClient(httpClient, "session-token");

        var prepared = await client.PrepareAsync(123, "content-hash", "/content/minimal");
        var started = await client.StartAsync(maxTicks: 1);
        var tick = await client.StepAsync(new TickRequest(ProtocolVersion.Current.ToString(), 0, []));
        var stopped = await client.StopAsync();

        Assert.Equal("ready", prepared.State);
        Assert.Equal("running", started.State);
        Assert.Equal(0, tick.Tick);
        Assert.Equal("stopped", stopped.State);
        Assert.Equal(
            ["/control/prepare", "/control/start", "/data/tick", "/control/stop"],
            handler.Requests.Select(static request => request.Path));
        Assert.All(handler.Requests, static request => Assert.Equal("session-token", request.SessionToken));
        Assert.Contains("\"contentHash\":\"content-hash\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"maxTicks\":1", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"protocolVersion\":\"1.0.0\"", handler.Requests[2].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreservesServerErrorStatusAndBody()
    {
        var handler = new RecordingHandler(HttpStatusCode.Conflict, """{"error":"invalid_state"}""");
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5182/"),
        };
        var client = new PrismClient(httpClient, "session-token");

        var exception = await Assert.ThrowsAsync<PrismClientException>(
            () => client.StartAsync());

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("""{"error":"invalid_state"}""", exception.ResponseBody);
    }

    [Fact]
    public async Task MapsBdiMovementAndConsumptionDecisionsToLockstepCommands()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5182/"),
        };
        var client = new PrismClient(httpClient, "session-token");
        var target = new GoalTarget(
            "node",
            7,
            "resource-food-7",
            new Position(4.5, 8.25),
            3,
            0.9);
        var decisions = new[]
        {
            new DecisionRecord(0, 9, true, false, DesireKind.SeekFood, 0.8, [], Target: target),
            new DecisionRecord(0, 2, true, false, DesireKind.Eat, 0.7, [], Target: target),
            new DecisionRecord(0, 4, true, false, DesireKind.Rest, 0.6, []),
        };

        var result = await client.StepDecisionsAsync(
            0,
            decisions,
            position => new PrismBlockPosition((int)position.X, (int)position.Y, 5));

        Assert.Equal(0, result.Tick);
        using var request = JsonDocument.Parse(handler.Requests.Single().Body);
        var commands = request.RootElement.GetProperty("commands");
        Assert.Equal(2, commands.GetArrayLength());
        Assert.Equal(2UL, commands[0].GetProperty("agentId").GetUInt64());
        Assert.Equal("Consommer", commands[0].GetProperty("kind").GetString());
        Assert.Equal(9UL, commands[1].GetProperty("agentId").GetUInt64());
        Assert.Equal("Aller", commands[1].GetProperty("kind").GetString());
        Assert.Equal(4, commands[1].GetProperty("payload").GetProperty("target")
            .GetProperty("position").GetProperty("x").GetInt32());
    }

    [Fact]
    public async Task SavesAndRestoresCheckpointsThroughControlEndpoints()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5182/"),
        };
        var client = new PrismClient(httpClient, "session-token");

        var saved = await client.SaveCheckpointAsync("cp-1");
        var restored = await client.RestoreCheckpointAsync("cp-1", "content-hash");

        Assert.Equal("ready", saved.State);
        Assert.Equal("ready", restored.State);
        Assert.Equal(
            ["/control/checkpoint", "/control/restore"],
            handler.Requests.Select(static request => request.Path));
        Assert.Contains("\"checkpointId\":\"cp-1\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"contentHash\":\"content-hash\"", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? errorBody = null) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(new RecordedRequest(
                path,
                body,
                request.Headers.GetValues("X-LIVEX-Session-Token").Single()));

            var responseBody = errorBody ?? (path switch
            {
                "/control/prepare" => """{"state":"ready","lastCompletedTick":-1,"seed":123,"contentHash":"content-hash","error":null}""",
                "/control/start" => """{"state":"running","lastCompletedTick":-1,"seed":123,"contentHash":"content-hash","error":null}""",
                "/data/tick" => """{"protocolVersion":"1.0.0","tick":0,"observations":[],"events":[]}""",
                "/control/stop" => """{"state":"stopped","lastCompletedTick":0,"seed":123,"contentHash":"content-hash","error":null}""",
                "/control/checkpoint" => """{"state":"ready","lastCompletedTick":0,"seed":123,"contentHash":"content-hash","error":null}""",
                "/control/restore" => """{"state":"ready","lastCompletedTick":0,"seed":123,"contentHash":"content-hash","error":null}""",
                _ => throw new InvalidOperationException($"Unexpected PRISM endpoint '{path}'."),
            });
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed record RecordedRequest(string Path, string Body, string SessionToken);
}
