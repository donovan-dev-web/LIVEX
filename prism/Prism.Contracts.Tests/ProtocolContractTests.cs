using System.Text.Json;
using Prism.Contracts;

namespace Prism.Contracts.Tests;

public sealed class ProtocolContractTests
{
    [Fact]
    public void SerializesTickRequestUsingCamelCaseAndRoundTripsEnvelope()
    {
        using var payloadDocument = JsonDocument.Parse("""{"target":{"x":4,"y":2,"z":1}}""");
        var request = new TickRequest(
            "1.0.0",
            12,
            [
                new CommandEnvelope(
                    "1.0.0",
                    12,
                    "cmd-1",
                    7,
                    "Miner",
                    payloadDocument.RootElement.Clone()),
            ]);

        var json = ProtocolJson.Serialize(request);
        var roundTrip = ProtocolJson.Deserialize<TickRequest>(json);

        Assert.Contains("\"protocolVersion\"", json, StringComparison.Ordinal);
        Assert.Contains("\"commandId\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ProtocolVersion\"", json, StringComparison.Ordinal);
        Assert.Equal("cmd-1", Assert.Single(roundTrip.Commands).CommandId);
        Assert.Equal(4, roundTrip.Commands[0].Payload.GetProperty("target").GetProperty("x").GetInt32());
    }

    [Fact]
    public void IgnoresUnknownFieldsButRejectsUnsupportedMajorProtocolVersion()
    {
        const string json = """
            {
              "protocolVersion": "1.4.0",
              "tick": 4,
              "commands": [],
              "futureField": true
            }
            """;

        var request = ProtocolJson.Deserialize<TickRequest>(json);
        Assert.Equal(4, request.Tick);
        ProtocolJson.ValidateEnvelopeVersion(request.ProtocolVersion);

        var exception = Assert.Throws<UnsupportedProtocolVersionException>(
            () => ProtocolJson.ValidateEnvelopeVersion("2.0.0"));
        Assert.Equal("protocol_unsupported", exception.ErrorCode);
    }

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("0.9.4", true)]
    [InlineData("1.01.0", false)]
    [InlineData("1.0", false)]
    [InlineData("1.0.0-preview", false)]
    [InlineData("-1.0.0", false)]
    public void ParsesStrictThreePartProtocolVersions(string text, bool succeeds)
    {
        Assert.Equal(succeeds, ProtocolVersion.TryParse(text, out _));
    }

    [Fact]
    public void EventEnvelopeSupportsOptionalAgentAndPayload()
    {
        using var payloadDocument = JsonDocument.Parse("""{"reason":"item_missing"}""");
        var json = ProtocolJson.Serialize(new EventEnvelope(
            "1.0.0",
            3,
            "evt-1",
            "action_failed",
            null,
            payloadDocument.RootElement.Clone()));

        var parsed = ProtocolJson.Deserialize<EventEnvelope>(json);

        Assert.Null(parsed.AgentId);
        Assert.Equal("item_missing", parsed.Payload.GetProperty("reason").GetString());
    }

    [Fact]
    public void RejectsNestedCommandsOnADifferentTickAndInvalidRequiredPayload()
    {
        using var payloadDocument = JsonDocument.Parse("{}");
        var mismatched = new TickRequest(
            "1.0.0",
            4,
            [
                new CommandEnvelope("1.0.0", 3, "cmd", 1, "Miner", payloadDocument.RootElement.Clone()),
            ]);

        Assert.Throws<JsonException>(() => ProtocolJson.Serialize(mismatched));
        Assert.Throws<JsonException>(() => ProtocolJson.Deserialize<CommandEnvelope>(
            """{"protocolVersion":"1.0.0","tick":0,"commandId":"x","agentId":1,"kind":"Miner"}"""));
    }

    [Fact]
    public void PublishedSchemasAreValidJsonDocuments()
    {
        var schemaFiles = Directory.GetFiles(FindSchemasDirectory(), "*.schema.json");
        Assert.Equal(5, schemaFiles.Length);
        foreach (var schemaFile in schemaFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(schemaFile));
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                document.RootElement.GetProperty("$schema").GetString());
        }
    }

    [Fact]
    public void PublishedCommandSchemaMatchesTheCanonicalCommandVocabulary()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FindSchemasDirectory(), "command-envelope.schema.json")));
        var declared = document.RootElement
            .GetProperty("properties")
            .GetProperty("kind")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(element => element.GetString())
            .OfType<string>()
            .ToArray();

        Assert.Equal(CommandKinds.All.Count, declared.Length);
        Assert.Equal(
            CommandKinds.All.OrderBy(kind => kind, StringComparer.Ordinal),
            declared.OrderBy(kind => kind, StringComparer.Ordinal));
    }

    private static string FindSchemasDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "docs", "prism", "schemas")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "docs", "prism", "schemas");
    }

    [Fact]
    public void IdempotencyWindowReplaysResultsRejectsIdReuseAndEvictsOldestEntry()
    {
        var window = new CommandIdempotencyWindow<string>(capacity: 2);
        var executionCount = 0;
        CommandEnvelope Command(string id, int target)
        {
            using var payload = JsonDocument.Parse($$"""{"target":{{target}}}""");
            return new CommandEnvelope(
                "1.0.0",
                1,
                id,
                7,
                "Miner",
                payload.RootElement.Clone());
        }

        var original = Command("one", 1);
        Assert.Equal("result-1", window.ExecuteOnce(original, () => $"result-{++executionCount}"));
        Assert.Equal("result-1", window.ExecuteOnce(original, () => $"result-{++executionCount}"));
        Assert.Equal(1, executionCount);

        var conflict = Assert.Throws<CommandIdConflictException>(
            () => window.ExecuteOnce(Command("one", 2), () => $"result-{++executionCount}"));
        Assert.Equal("command_id_conflict", conflict.ErrorCode);
        Assert.Equal(1, executionCount);

        _ = window.ExecuteOnce(Command("two", 2), () => $"result-{++executionCount}");
        _ = window.ExecuteOnce(Command("three", 3), () => $"result-{++executionCount}");
        Assert.Equal(2, window.Count);
        Assert.Equal("result-4", window.ExecuteOnce(original, () => $"result-{++executionCount}"));
        Assert.Equal(4, executionCount);
    }

    [Fact]
    public void FailedCommandExecutionIsNotCached()
    {
        using var payloadDocument = JsonDocument.Parse("{}");
        var command = new CommandEnvelope("1.0.0", 1, "retry", 1, "Miner", payloadDocument.RootElement.Clone());
        var window = new CommandIdempotencyWindow<int>();
        Assert.Throws<InvalidOperationException>(
            () => window.ExecuteOnce(command, () => throw new InvalidOperationException("temporary")));

        Assert.Equal(42, window.ExecuteOnce(command, () => 42));
    }

    [Fact]
    public void IdempotencyWindowSnapshotsMutableResults()
    {
        using var payloadDocument = JsonDocument.Parse("{}");
        var command = new CommandEnvelope("1.0.0", 1, "snapshot", 1, "Miner", payloadDocument.RootElement.Clone());
        var window = new CommandIdempotencyWindow<List<int>>();
        var executionCount = 0;
        var firstResult = window.ExecuteOnce(command, () =>
        {
            executionCount++;
            return new List<int> { 1 };
        });
        firstResult.Add(2);

        var replayedResult = window.ExecuteOnce(command, () =>
        {
            executionCount++;
            return new List<int> { 3 };
        });

        Assert.Equal([1], replayedResult);
        Assert.Equal(1, executionCount);
    }
}
