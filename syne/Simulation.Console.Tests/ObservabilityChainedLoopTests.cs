using System.Globalization;
using System.Text.Json.Nodes;
using Simulation.Console.Observability;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Intégration de la **boucle de simulation complète** (SYNE-101, jalon ph10) :
/// perception → mémoire/croyances → besoins/objectifs → décision utilitaire →
/// communication → actions → événements/groupes/population. Le run observé brûle
/// les 150 premiers ticks de bout en bout ; le flux chaîné (snapshot + événements)
/// doit être **sans perte** : contiguïté stricte des ticks, trame complète par tick,
/// et invariants de comptage vérifiés sur l'état interne.
/// </summary>
public class ObservabilityChainedLoopTests
{
    private sealed class RecordingSink : IObservabilitySink
    {
        public List<string> Frames { get; } = new();

        public Task BroadcastAsync(string text)
        {
            Frames.Add(text);
            return Task.CompletedTask;
        }
    }

    private static JsonNode ParseFrame(string frame) =>
        JsonNode.Parse(frame) ?? throw new InvalidOperationException("trame JSON invalide");

    private static SimulationLoop BuildScenario(ulong seed, ulong entityCount)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        world.AddObstacle(new Obstacle("rocher-1", new Position(250, 150), radius: 15));
        world.AddObstacle(new Obstacle("rocher-2", new Position(420, 380), radius: 25));

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= entityCount; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, Simulation.Core.Configuration.ConfigLoader.LoadDefaults());
    }

    [Fact]
    public async Task FullChainedLoop_OneFramePerTick_WithoutLoss()
    {
        const int maxTicks = 150;
        var sink = new RecordingSink();
        var emitter = new ObservabilityTickEmitter(BuildScenario(12345, entityCount: 25), seed: 12345, sink);

        await emitter.RunAsync(maxTicks);

        Assert.Equal((ulong)maxTicks, emitter.TicksEmitted);

        var nodes = sink.Frames.Select(ParseFrame).ToList();
        Assert.Equal(sink.Frames.Count, nodes.Count); // chaque trame est un JSON valide (aucune perte)

        var snapshots = nodes.Where(node => node!["type"]!.GetValue<string>() == "snapshot").ToList();
        var summaries = nodes.Where(node => node!["type"]!.GetValue<string>() == "tick_summary").ToList();

        Assert.Equal(maxTicks, snapshots.Count);           // 1 snapshot par tick
        Assert.Equal(maxTicks, summaries.Count);           // 1 tick_summary par tick
        Assert.True(sink.Frames.Count > maxTicks * 20, "le flux complet doit contenir les événements détaillés");

        ulong previousTick = 0;
        foreach (JsonNode? snapshot in snapshots)
        {
            ulong tick = snapshot!["tick"]!.GetValue<ulong>();
            Assert.True(tick > previousTick, "les snapshots doivent être ordonnés strictement (pas de trou ni de doublon)");
            previousTick = tick;
        }
        Assert.Equal((ulong)maxTicks, previousTick);

        var summaryTicks = summaries.Select(node => node!["tick"]!.GetValue<ulong>()).OrderBy(t => t).ToList();
        for (int tick = 1; tick <= maxTicks; tick++)
        {
            Assert.Equal((ulong)tick, summaryTicks[tick - 1]); // tick_summary contigus 1..N
        }
    }

    [Fact]
    public async Task FullChainedLoop_EnlistsEveryPipelineStage()
    {
        const int maxTicks = 180;
        var sink = new RecordingSink();
        var emitter = new ObservabilityTickEmitter(BuildScenario(424242, entityCount: 30), seed: 424242, sink);
        await emitter.RunAsync(maxTicks);

        var eventTypes = sink.Frames
            .Select(ParseFrame)
            .Where(node => node is not null)
            .Select(node => node!["type"]!.GetValue<string>())
            .ToHashSet();

        foreach (string stage in new[]
        {
            "snapshot",                    // perception / état du monde
            "decision_made",               // décision utilitaire
            "action_completed",            // actions
            "message_sent",                // communication
            "message_received",
            "tick_summary",
        })
        {
            Assert.Contains(stage, eventTypes);
        }

        Assert.Contains(eventTypes, type => type is "group_formed" or "group_dissolved" or "agent_spawned" or "agent_died");
    }

    [Fact]
    public async Task FullChainedLoop_CrossChecksDeadEntities_DisappearFromTrajectory()
    {
        const int maxTicks = 150;
        var sink = new RecordingSink();
        var emitter = new ObservabilityTickEmitter(BuildScenario(424242, entityCount: 30), seed: 424242, sink);
        await emitter.RunAsync(maxTicks);

        var parsed = sink.Frames.Select(ParseFrame).ToList();
        var deaths = parsed
            .Where(node => node!["type"]!.GetValue<string>() == "agent_died")
            .Select(node => (Id: ulong.Parse(node!["agentId"]!.GetValue<string>(), CultureInfo.InvariantCulture), Tick: node!["tick"]!.GetValue<ulong>()))
            .ToList();

        if (deaths.Count == 0)
        {
            return; // scénario sans mortalité dans cette fenêtre → invariant trivialement satisfait
        }

        var deathTickByAgent = deaths
            .GroupBy(entry => entry.Id)
            .ToDictionary(group => group.Key, group => group.Min(entry => entry.Tick));

        var snapshots = parsed
            .Where(node => node!["type"]!.GetValue<string>() == "snapshot")
            .Select(node => (Tick: node!["tick"]!.GetValue<ulong>(), Agents: node!["agents"]!.AsArray()
                .Select(agent => ulong.Parse(agent!["id"]!.GetValue<string>(), CultureInfo.InvariantCulture)).ToList()))
            .ToList();

        foreach ((ulong deadId, ulong deathTick) in deathTickByAgent)
        {
            foreach ((ulong tick, List<ulong> agents) in snapshots)
            {
                if (tick <= deathTick)
                {
                    continue; // avant le décès l'agent peut légitimement être présent
                }

                Assert.DoesNotContain(deadId, agents);
            }
        }
    }
}