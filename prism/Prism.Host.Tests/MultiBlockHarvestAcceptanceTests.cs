using System.Text.Json;
using Prism.Content;
using Prism.Contracts;
using Prism.Host.Hosting;

namespace Prism.Host.Tests;

/// <summary>
/// Acceptance of A2 (gate G4): one tree yields more than one block. The trunk block above the base
/// is out of reach as long as the reach rule stops at the feet level, so every
/// <c>ObtenirObjet(log, 2)</c> ended after its first log and the storage chain never got the two
/// logs its eight planks need. This test plays the macro exactly as the planner does — on a
/// generated world, next to a tree — and asks for two logs.
/// </summary>
public sealed class MultiBlockHarvestAcceptanceTests
{
    private const ulong AgentId = 42;

    [Fact]
    public void OneTreeYieldsTwoLogsThroughTheObtainMacro()
    {
        var contentRoot = Path.Combine(FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(contentRoot);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var pack = compilation.Pack!;
        var runtime = new PrismHostRuntime(checkpointOnStop: false);
        runtime.Prepare(new PrepareWorldRequest(1, pack.ContentHash, contentRoot));

        var logId = pack.Registries.Materials.GetId("log");
        var waterId = pack.Registries.Materials.GetId("water");
        var (standing, baseLog) = FindTreeWithStandingCell(runtime.WorldSnapshot!, logId, waterId);
        runtime.Start(new StartWorldRequest());

        var tick = 0L;
        CommandEnvelope Command(string commandId, string kind, JsonElement payload) =>
            new(ProtocolVersion.Current.ToString(), tick, commandId, AgentId, kind, payload.Clone());
        TickResult Step(params CommandEnvelope[] commands) =>
            runtime.Step(new TickRequest(ProtocolVersion.Current.ToString(), tick++, commands));

        using var incarnate = JsonDocument.Parse(JsonSerializer.Serialize(
            new { position = new { x = standing.X, y = standing.Y, z = standing.Z } },
            ProtocolJson.Options));
        Assert.Equal(
            "action_completed",
            Assert.Single(Step(Command("incarnate-harvest", "Incarner", incarnate.RootElement)).Events).Kind);

        // One tick without a command: a macro-action selects its sources from what the agent has
        // already perceived, and the observation of the incarnation tick is published by the
        // perception phase (a deferred action such as Miner validates its reach later).
        Step();

        // The hint keeps both sources inside the chosen tree: its base log and the log above it.
        // Neither can be replaced by a log of a neighbouring tree, so the second block really is
        // the one the old reach rule could not touch.
        using var obtain = JsonDocument.Parse(JsonSerializer.Serialize(
            new
            {
                item = "log",
                quantity = 2,
                hint = new { x = baseLog.X, y = baseLog.Y, z = baseLog.Z },
                hintRadius = 1,
            },
            ProtocolJson.Options));
        var started = Step(Command("obtain-two-logs", "ObtenirObjet", obtain.RootElement));
        Assert.DoesNotContain(started.Events, static entry => entry.Kind == "action_failed");
        Assert.Contains(
            started.Events,
            static entry =>
                entry.Kind == "action_completed" &&
                entry.Payload.GetProperty("result").GetProperty("started").GetBoolean());

        EventEnvelope? completed = null;
        for (var attempt = 0; attempt < 400 && completed is null; attempt++)
        {
            foreach (var entry in Step().Events)
            {
                if (entry.Payload.ValueKind != JsonValueKind.Object ||
                    !entry.Payload.TryGetProperty("commandId", out var commandId) ||
                    commandId.GetString() != "obtain-two-logs")
                {
                    continue;
                }

                if (entry.Kind == "action_failed")
                {
                    throw new InvalidOperationException(
                        $"The obtain of two logs failed with '{entry.Payload.GetProperty("reason").GetString()}'.");
                }

                if (entry.Kind == "action_completed" &&
                    entry.Payload.TryGetProperty("result", out var payloadResult) &&
                    payloadResult.ValueKind == JsonValueKind.Object &&
                    payloadResult.TryGetProperty("completed", out var done) &&
                    done.ValueKind == JsonValueKind.True)
                {
                    completed = entry;
                }
            }
        }

        Assert.NotNull(completed);
        var result = completed!.Payload.GetProperty("result");
        Assert.True(result.GetProperty("completed").GetBoolean());
        Assert.Equal(2, result.GetProperty("quantityAcquired").GetInt32());

        // The case really is one tree: the agent stands beside the base log, at its height.
        Assert.Equal(1, Math.Abs(standing.X - baseLog.X) + Math.Abs(standing.Y - baseLog.Y));
        Assert.Equal(baseLog.Z, standing.Z);
    }

    /// <summary>
    /// First tree (deterministic scan) whose base log has a second log on top and a neighbouring
    /// column where an agent can stand at the base log's height: same surface, not water, and both
    /// body cells free. Returns the standing cell and the base log.
    /// </summary>
    private static (Core.World.BlockCoordinate Standing, Core.World.BlockCoordinate BaseLog)
        FindTreeWithStandingCell(
            GeneratedWorldSnapshot world,
            ushort logId,
            ushort waterId)
    {
        var occupied = world.Overrides
            .Where(static entry => !entry.Value.IsAir)
            .ToDictionary(static entry => entry.Key, static entry => entry.Value);

        foreach (var candidate in occupied.Keys
                     .OrderBy(static position => position.Y)
                     .ThenBy(static position => position.X)
                     .ThenBy(static position => position.Z))
        {
            if (occupied[candidate].MaterialId != logId ||
                !occupied.TryGetValue(
                    new Core.World.BlockCoordinate(candidate.X, candidate.Y, candidate.Z + 1),
                    out var above) ||
                above.MaterialId != logId)
            {
                continue;
            }

            foreach (var (dx, dy) in NeighbourOffsets)
            {
                var x = candidate.X + dx;
                var y = candidate.Y + dy;
                if ((uint)x >= (uint)world.Width || (uint)y >= (uint)world.Height)
                {
                    continue;
                }

                var surface = world.SurfaceColumns[x + world.Width * y];
                if (surface.SurfaceZ != candidate.Z - 1 || surface.MaterialId == waterId)
                {
                    continue;
                }

                var feet = new Core.World.BlockCoordinate(x, y, candidate.Z);
                var head = new Core.World.BlockCoordinate(x, y, candidate.Z + 1);
                if (occupied.ContainsKey(feet) || occupied.ContainsKey(head))
                {
                    continue;
                }

                return (feet, candidate);
            }
        }

        throw new InvalidOperationException("The generated test world has no reachable two-block tree.");
    }

    private static readonly (int Dx, int Dy)[] NeighbourOffsets =
    [
        (0, -1), (-1, 0), (1, 0), (0, 1),
    ];

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
}
