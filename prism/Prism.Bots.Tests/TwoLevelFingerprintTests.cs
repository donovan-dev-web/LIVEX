using System.Text.Json;
using System.Text.Json.Nodes;
using Prism.Bots;
using Prism.Content;
using Prism.Contracts;
using Prism.Host.Hosting;

namespace Prism.Bots.Tests;

/// <summary>
/// PRISM-107: the two fingerprint levels answer two different questions, and this file is the
/// proof that they do. A permutation of the emission order — events and observations reversed, the
/// stacks of an inventory reversed — is exactly what an internal refactor (a bounded search, a
/// region cache, a pooled buffer) may do; the trajectory fingerprint must not move, while the byte
/// fingerprint deliberately does.
/// </summary>
public sealed class TwoLevelFingerprintTests
{
    private const int Ticks = 120;

    /// <summary>Sampling period used here; the production default is 100 ticks.</summary>
    private const int SamplingInterval = 20;

    [Fact]
    public void AReorderedInternalEmissionLeavesTheTrajectoryFingerprintUnchanged()
    {
        var (run, recorded) = RunRecorded(Ticks, agents: 6);

        // The run is long enough for whole sampling windows, and the runner's own accumulation
        // agrees with a recomputation from the recorded ticks: the two paths hash the same frames.
        Assert.True(run.TrajectoryFrames > 0, "the run must span at least one sampling window.");
        Assert.Equal(run.TrajectoryFingerprint, RunTrajectory.Compute(recorded));
        Assert.Equal(run.Fingerprint, SerializedFingerprint(recorded));

        var permuted = recorded.Select(ReverseInternalOrder).ToArray();

        // Every reversal above must actually bite, or the invariance below would prove nothing:
        // the run needs ticks with several events, ticks with several observations, and at least
        // one inventory holding more than one stack.
        var multiEventTicks = recorded.Count(static result => result.Events.Count > 1);
        var multiObservationTicks = recorded.Count(static result => result.Observations.Count > 1);
        var multiStackObservations = recorded.Sum(
            static result => result.Observations.Count(static observation => InventoryLength(observation) > 1));
        Assert.True(multiEventTicks > 0, "no tick publishes two events, so reversing them would prove nothing.");
        Assert.True(
            multiObservationTicks > 0,
            "no tick publishes two observations, so reversing them would prove nothing.");
        Assert.True(
            multiStackObservations > 0,
            "no inventory holds two stacks, so reversing them would prove nothing.");

        // Trajectory level: identical. Observations are sorted by agent id and inventories are
        // aggregated by item type before hashing, so nothing about the internal order reaches it.
        Assert.Equal(
            RunTrajectory.Compute(recorded, SamplingInterval),
            RunTrajectory.Compute(permuted, SamplingInterval));

        // Byte level: different, on purpose. It hashes the published stream in order, so a
        // permutation moves it even though the simulation did exactly the same thing. That is the
        // confusion PRISM-107 removes: a performance change is judged on the trajectory level, and
        // a byte-level move is only a finding when the trajectory moved with it.
        Assert.NotEqual(run.Fingerprint, SerializedFingerprint(permuted));
    }

    [Fact]
    public void TheTrajectoryFingerprintMovesWhenAnAgentDoesNotEndWhereItDid()
    {
        var (_, recorded) = RunRecorded(Ticks, agents: 2);

        // The other half of the guard: a coarse fingerprint that never moves would be worthless.
        // One cell of drift on the last sampled tick is enough to break it.
        var shifted = recorded
            .Select((result, index) => ShiftLastAgentByOneCell(result, index, recorded.Length))
            .ToArray();

        Assert.NotEqual(
            RunTrajectory.Compute(recorded, SamplingInterval),
            RunTrajectory.Compute(shifted, SamplingInterval));
    }

    private static (BotRunResult Run, TickResult[] Recorded) RunRecorded(int ticks, int agents)
    {
        var root = Path.Combine(BotRunnerTests.FindPrismRoot(), "content", "minimal");
        var compilation = new ContentPackCompiler().CompileDirectory(root);
        Assert.True(compilation.Succeeded, string.Join(Environment.NewLine, compilation.Errors));
        var pack = compilation.Pack!;
        var content = BotContentMap.FromContent(pack);

        var runtime = new PrismHostRuntime(checkpointOnStop: false);
        runtime.Prepare(new PrepareWorldRequest(1UL, pack.ContentHash, root));
        runtime.Start(new StartWorldRequest());

        var host = new RecordingWorldHost(runtime);
        var bots = new Dictionary<ulong, IPrismBot>(agents);
        var spawns = new List<BotSpawn>(agents);
        var world = runtime.WorldSnapshot!;
        var sites = world.SpawnSites;
        // One agent plays the tools chain from a site that can actually play it: a chain agent
        // holds several item types at once, which is what makes the stack permutation bite
        // in the invariance test that uses this run.
        var toolsSpawn = R1Campaign.CandidateSpawnPointsFor(world, "tools", content).FirstOrDefault();
        for (var index = 0; index < agents; index++)
        {
            var agentId = (ulong)(index + 1);
            if (index == 0 && toolsSpawn is not null)
            {
                bots[agentId] = new PlannerBot(content, R1Campaign.GoalsFor("tools"));
                spawns.Add(new BotSpawn(agentId, toolsSpawn.Cell));
                continue;
            }

            var site = sites[index % sites.Length];
            bots[agentId] = new GreedyBot(content);
            spawns.Add(new BotSpawn(agentId, new BotCoordinate(site.X, site.Y, site.Z)));
        }

        var result = new BotRunner(host, bots).Run(0, spawns, ticks);
        return (result, host.Results.ToArray());
    }

    private static ulong SerializedFingerprint(IEnumerable<TickResult> results)
    {
        var hash = RunFingerprint.Empty;
        foreach (var result in results)
        {
            hash = RunFingerprint.AppendSerializedTick(hash, result);
        }

        return hash;
    }

    /// <summary>Same tick, same payloads, internal collections walked the other way round.</summary>
    private static TickResult ReverseInternalOrder(TickResult result) =>
        result with
        {
            Events = result.Events.Reverse().ToArray(),
            Observations = result.Observations.Select(ReverseInventory).Reverse().ToArray(),
        };

    private static int InventoryLength(ObservationEnvelope observation) =>
        JsonNode.Parse(observation.Self.GetRawText())!["inventory"] is JsonArray inventory
            ? inventory.Count
            : 0;

    private static ObservationEnvelope ReverseInventory(ObservationEnvelope observation)
    {
        var self = JsonNode.Parse(observation.Self.GetRawText())!.AsObject();
        if (self["inventory"] is JsonArray inventory && inventory.Count > 1)
        {
            self["inventory"] = new JsonArray(
                inventory.Reverse().Select(static node => node?.DeepClone()).ToArray());
        }

        return observation with { Self = ToElement(self) };
    }

    /// <summary>Moves one agent one cell along X on the last tick only.</summary>
    private static TickResult ShiftLastAgentByOneCell(TickResult result, int index, int count)
    {
        if (index != count - 1 || result.Observations.Count == 0)
        {
            return result;
        }

        var observations = result.Observations.ToArray();
        var self = JsonNode.Parse(observations[0].Self.GetRawText())!.AsObject();
        var position = self["position"]!.AsObject();
        position["x"] = position["x"]!.GetValue<int>() + 1000;
        observations[0] = observations[0] with { Self = ToElement(self) };
        return result with { Observations = observations };
    }

    private static JsonElement ToElement(JsonNode node) =>
        JsonDocument.Parse(node.ToJsonString(ProtocolJson.Options)).RootElement.Clone();

    private sealed class RecordingWorldHost(IWorldHost inner) : IWorldHost
    {
        public List<TickResult> Results { get; } = [];

        public TickResult Step(TickRequest request)
        {
            var result = inner.Step(request);
            Results.Add(result);
            return result;
        }
    }
}
