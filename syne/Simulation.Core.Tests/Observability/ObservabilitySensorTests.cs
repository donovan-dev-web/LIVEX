using System.Text.Json.Nodes;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Prng;
using WorldType = Simulation.Core.World.World;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class ObservabilitySensorTests
{
    private static (WorldType World, SimulationLoop Loop) BuildLoop()
    {
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(new Entity(new EntityId(1), "Entité A", null, new Position(50, 50), TraitSet.NeutralAll, bornAt: 0));
        world.AddEntity(new Entity(new EntityId(2), "Entité A", null, new Position(80, 50), TraitSet.NeutralAll, bornAt: 0));
        world.AddEntity(new Entity(new EntityId(3), "Entité A", null, new Position(300, 300), TraitSet.NeutralAll, bornAt: 0));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), ConfigLoader.LoadDefaults());
        return (world, loop);
    }

    [Fact]
    public void Snapshot_Message_RespectsCamelCaseContract()
    {
        (_, SimulationLoop loop) = BuildLoop();
        loop.Run(3);

        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 7);
        JsonObject message = ObservabilitySerializer.SnapshotMessage(snapshot);
        string json = ObservabilitySerializer.ToJsonText(message);

        Assert.Equal("snapshot", (string?)message["type"]);
        Assert.Equal(ObservabilityContract.Version, (string?)message["version"]);
        Assert.Equal("run-7", (string?)message["runId"]);
        Assert.Equal(3UL, (ulong?)message["tick"]);
        Assert.Equal(3L, (long?)message["simulatedTimeMinutes"]);
        Assert.Equal(3, (int?)message["aliveCount"]);
        Assert.Equal(3, message["agents"]!.AsArray().Count);
        Assert.NotNull(message["resources"]);

        // Agents triés par id croissant (déterminisme d'émission).
        Assert.Equal("1", (string?)message["agents"]![0]!["id"]);
        Assert.Equal("3", (string?)message["agents"]![2]!["id"]);

        JsonNode agent = message["agents"]![0]!;
        Assert.Equal("Entité A", (string?)agent["species"]);
        Assert.Equal(50, (double?)agent["position"]!["x"]);
        Assert.Equal(50, (double?)agent["position"]!["y"]);
        Assert.InRange((double?)agent["energy"] ?? 0, 0, 100);
        Assert.Equal("Idle", (string?)agent["currentAction"]);

        // Aucune clé PascalCase (contrat API_CONTRACTS.md §2).
        Assert.DoesNotContain("\"AliveCount\"", json);
        Assert.DoesNotContain("\"SimulatedTimeMinutes\"", json);
        Assert.DoesNotContain("\"RunId\"", json);
        Assert.DoesNotContain("\"PositionX\"", json);
    }

    [Fact]
    public void Snapshot_Emission_IsDeterministicForSameSeed()
    {
        string Emit(ulong seed)
        {
            var world = new WorldType(new WorldSize(100, 100));
            world.AddEntity(new Entity(new EntityId(1), "Entité A", null, new Position(10, 10), TraitSet.NeutralAll, bornAt: 0));
            var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(seed), ConfigLoader.LoadDefaults());
            loop.Run(5);
            return ObservabilitySerializer.ToJsonText(
                ObservabilitySerializer.SnapshotMessage(WorldSnapshot.Capture(loop, seed)));
        }

        string first = Emit(7);
        Assert.Equal(first, Emit(7));
        Assert.NotEqual(first, Emit(8));
    }

    [Fact]
    public void Events_DecisionMade_And_TickSummary_RespectContract()
    {
        (_, SimulationLoop loop) = BuildLoop();
        loop.Run(200);

        var summary = EventSensor.TickSummary(loop.CurrentTick, aliveCount: 3);
        JsonObject summaryJson = ObservabilitySerializer.EventMessage(summary);
        Assert.Equal("tick_summary", (string?)summaryJson["type"]);
        Assert.Equal(3, (int?)summaryJson["value"]!["aliveCount"]);

        MindState mind = loop.Cognition.MindOf(1);
        ExternalEvent decision = EventSensor.DecisionMade(loop.CurrentTick, agentId: 1, mind);
        JsonObject eventJson = ObservabilitySerializer.EventMessage(decision);
        Assert.Equal("decision_made", (string?)eventJson["type"]);
        Assert.Equal("1", (string?)eventJson["agentId"]);
        Assert.Equal(mind.Intention?.Kind.ToString() ?? "Idle", (string?)eventJson["action"]);
        Assert.NotNull(eventJson["cause"]);
        Assert.NotNull(eventJson["value"]);
        Assert.Equal(mind.Intention?.Kind.ToString() ?? "Idle", (string?)eventJson["value"]!["intention"]);
        Assert.True((double?)eventJson["value"]!["utility"] >= 0);
    }

    [Fact]
    public void RunId_IsStableAndDerivedFromSeed()
    {
        Assert.Equal("run-42", ObservabilityContract.RunIdFor(42));
    }

    [Fact]
    public void Snapshot_ExposesCognitiveFieldsDeterministically()
    {
        (_, SimulationLoop loop) = BuildLoop();
        loop.Run(50);

        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 7);
        JsonObject message = ObservabilitySerializer.SnapshotMessage(snapshot);
        JsonObject agent = message["agents"]![0]!.AsObject();

        Assert.NotNull(agent["traits"]);
        Assert.NotNull(agent["beliefs"]);
        Assert.NotNull(agent["goals"]);
        Assert.NotNull(agent["trust"]);
        Assert.True((int?)agent["memoryCount"] >= 0);
        Assert.InRange((int?)agent["memoryCount"] ?? 0, 0, 51);

        // Traits exposés par nom (déterminisme : ordre inchangé pour une même seed).
        JsonObject traits = agent["traits"]!.AsObject();
        Assert.True(traits.Count > 0);
        Assert.Contains("bravery", traits.Select(node => node.Key));

        // Pas de clé PascalCase pour les nouveaux champs (contrat camelCase).
        string json = ObservabilitySerializer.ToJsonText(message);
        Assert.DoesNotContain("\"MemoryCount\"", json);
        Assert.DoesNotContain("\"BeliefObservation\"", json);
    }
}