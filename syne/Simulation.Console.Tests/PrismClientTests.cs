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
            // `Drink` est le seul kind terminal traduisible : boire à une source
            // se dit par coordonnée, sans identifiant d'objet (contrairement à Eat,
            // qui exigerait un id d'objet que SYNE ne connaît pas).
            new DecisionRecord(0, 2, true, false, DesireKind.Drink, 0.7, [], Target: target),
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
        // Contrat PRISM : la coordonnée est directement sous `target`.
        Assert.True(CommandPayloadValidator.TryValidate(
            "Aller",
            commands[1].GetProperty("payload"),
            out string? detail), detail);
        Assert.Equal(4, commands[1].GetProperty("payload").GetProperty("target")
            .GetProperty("x").GetInt32());
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

    [Fact]
    public async Task DrainsPaginatedTickResponsesAndRebuildsTheSameStream()
    {
        var pages = new[]
        {
            Page(index: 0, pageCount: 2, agents: [1UL, 2UL], events: 1),
            Page(index: 1, pageCount: 2, agents: [3UL], events: 0),
        };
        var handler = new PagedTickHandler(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/data/tick"] = ProtocolJson.Serialize(pages[0]),
            [$"/data/tick/result?cursor={Uri.EscapeDataString(pages[0].Page.Cursor!)}"] =
                ProtocolJson.Serialize(pages[1]),
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5182/"),
        };
        var client = new PrismClient(httpClient, "session-token");

        var result = await client.StepAsync(new TickRequest(ProtocolVersion.Current.ToString(), 0, []));

        // Le flux reconstruit est celui du monde, dans l'ordre annoncé.
        Assert.Equal(0, result.Tick);
        Assert.Equal([1UL, 2UL, 3UL], result.Observations.Select(static observation => observation.AgentId));
        Assert.Single(result.Events);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/data/tick", handler.Requests[0].Path);
        Assert.StartsWith("/data/tick/result?cursor=", handler.Requests[1].Path, StringComparison.Ordinal);
        Assert.All(handler.Requests, request => Assert.Equal("session-token", request.SessionToken));
    }

    [Fact]
    public async Task RefusesAPageSequenceWithAGapInsteadOfApplyingAPartialTick()
    {
        var first = Page(index: 0, pageCount: 3, agents: [1UL], events: 0);
        // La page annoncée suivante saute de 0 à 2 : le tick est incomplet.
        var skipped = Page(index: 2, pageCount: 3, agents: [2UL], events: 0);
        var handler = new PagedTickHandler(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/data/tick"] = ProtocolJson.Serialize(first),
            [$"/data/tick/result?cursor={Uri.EscapeDataString(first.Page.Cursor!)}"] =
                ProtocolJson.Serialize(skipped),
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5182/"),
        };
        var client = new PrismClient(httpClient, "session-token");

        var exception = await Assert.ThrowsAsync<PrismTickPageSequenceException>(
            () => client.StepAsync(new TickRequest(ProtocolVersion.Current.ToString(), 0, [])));

        Assert.Contains("Expected page", exception.Message, StringComparison.Ordinal);
    }

    private static TickPageEnvelope Page(int index, int pageCount, ulong[] agents, int events)
    {
        var complete = index == pageCount - 1;
        return new TickPageEnvelope(
            ProtocolVersion.Current.ToString(),
            0,
            new TickPageInfo(index, pageCount, complete, complete ? null : $"0.{index + 1}.{pageCount}.0123456789abcdef"),
            new TickPageChunk(
                agents.Select(agentId => new ObservationEnvelope(
                    ProtocolVersion.Current.ToString(),
                    0,
                    agentId,
                    JsonSerializer.SerializeToElement(new { changes = Array.Empty<object>() }, ProtocolJson.Options),
                    JsonSerializer.SerializeToElement(new { position = new { x = 1, y = 2, z = 3 } }, ProtocolJson.Options))).ToArray(),
                Enumerable.Range(0, events).Select(eventIndex => new EventEnvelope(
                    ProtocolVersion.Current.ToString(),
                    0,
                    $"evt-{index}-{eventIndex}",
                    "action_completed",
                    (ulong)eventIndex,
                    JsonSerializer.SerializeToElement(new { commandId = $"cmd-{index}" }, ProtocolJson.Options))).ToArray()));
    }

    private sealed class PagedTickHandler(IReadOnlyDictionary<string, string> responses) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var endpoint = request.RequestUri!.AbsolutePath + request.RequestUri.Query;
            Requests.Add(new RecordedRequest(
                endpoint,
                string.Empty,
                request.Headers.GetValues("X-LIVEX-Session-Token").Single()));
            if (!responses.TryGetValue(endpoint, out var body))
            {
                throw new InvalidOperationException($"Unexpected PRISM endpoint '{endpoint}'.");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
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
