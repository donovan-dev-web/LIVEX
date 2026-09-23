using System.Text.Json.Nodes;
using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Population;
using Simulation.Core.Prng;
using Simulation.Core.Social;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
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
        // Scénario du contrat (pas de la mortalité, SYNE-074) : population immortelle.
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(new Entity(new EntityId(1), "Entité A", null, new Position(50, 50), TraitSet.NeutralAll, bornAt: 0));
        world.AddEntity(new Entity(new EntityId(2), "Entité A", null, new Position(80, 50), TraitSet.NeutralAll, bornAt: 0));
        world.AddEntity(new Entity(new EntityId(3), "Entité A", null, new Position(300, 300), TraitSet.NeutralAll, bornAt: 0));
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Agents.Life.DeathEnabled = false;
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), options);
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
        Assert.NotNull((bool?)eventJson["value"]!["deliberated"]);
        Assert.NotNull((bool?)eventJson["value"]!["interrupted"]);
    }

    [Fact]
    public void Snapshot_Resources_ReflectStocksAfterActions()
    {
        // SYNE-042 : le snapshot porte les réserves (DATA_MODEL.md §8) —
        // les actions terminales les mettent à jour.
        (_, SimulationLoop loop) = BuildLoop();
        loop.Run(500);

        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 7);
        JsonObject message = ObservabilitySerializer.SnapshotMessage(snapshot);

        Assert.NotNull(message["resources"]);
        double water = message["resources"]!.AsArray().First(r => (string?)r!["type"] == "water")!["quantity"]!.GetValue<double>();
        double food = message["resources"]!.AsArray().First(r => (string?)r!["type"] == "food")!["quantity"]!.GetValue<double>();

        Assert.True(water >= 0.0 && water < 1000.0, "La réserve d'eau a été consommée par Drink.");
        Assert.True(food >= 0.0 && food < 100.0, "La réserve de nourriture a été consommée par Eat.");
    }

    [Fact]
    public void Event_ActionCompleted_RespectsContract()
    {
        // SYNE-080 : chaque exécution atomique produit un événement action_completed
        // (API_CONTRACTS.md §2.2) portant l'action exécutée, son issue et ses deltas.
        (_, SimulationLoop loop) = BuildLoop();
        for (int tick = 1; tick <= 12; tick++)
        {
            loop.AdvanceOneTick();
        }

        MindState mind = loop.Cognition.MindOf(1);
        Assert.NotNull(mind.LastActionResult);

        ExternalEvent completed = EventSensor.ActionCompleted(loop.CurrentTick, agentId: 1, mind.LastActionResult!);
        JsonObject eventJson = ObservabilitySerializer.EventMessage(completed);

        Assert.Equal(ObservabilityContract.ActionCompleted, (string?)eventJson["type"]);
        Assert.Equal("1", (string?)eventJson["agentId"]);
        Assert.Equal(mind.LastActionResult!.Kind.ToString(), (string?)eventJson["action"]);
        Assert.NotNull((string?)eventJson["value"]!["outcome"]);
    }

    [Fact]
    public void RunId_IsStableAndDerivedFromSeed()
    {
        Assert.Equal("run-42", ObservabilityContract.RunIdFor(42));
    }

    [Fact]
    public void Snapshot_CarriesEngineVersion()
    {
        // DETERMINISM.md §3.6.2 / VERSIONING.md §3 : la version moteur identifie le run.
        // Jalon SYNE ph7b → 0.6.0 : mortalité (074), naissance consentie fidèle (075),
        // décision collective → objectifs (076) et cheminement A* déterministe (077).
        Assert.Equal("0.6.0", ObservabilityContract.EngineVersion);

        (_, SimulationLoop loop) = BuildLoop();
        loop.Run(3);
        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 7);
        JsonObject message = ObservabilitySerializer.SnapshotMessage(snapshot);

        Assert.Equal(ObservabilityContract.EngineVersion, (string?)message["engineVersion"]);
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

    [Fact]
    public void Snapshot_ExposesGroups_AsCamelCaseArray()
    {
        // SYNE-060 : la photographie embarque les groupes émergents (API_CONTRACTS §2.1).
        SimulationOptions options = ConfigLoader.LoadDefaults();
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(new Entity(new EntityId(1), "Entité A", null, new Position(50, 50), TraitSet.NeutralAll, bornAt: 0));
        world.AddEntity(new Entity(new EntityId(2), "Entité A", null, new Position(80, 50), TraitSet.NeutralAll, bornAt: 0));
        world.AddEntity(new Entity(new EntityId(3), "Entité A", null, new Position(300, 300), TraitSet.NeutralAll, bornAt: 0));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), options);
        loop.Run(1); // crée les esprits (création paresseuse dans le pipeline).

        foreach (Simulation.Core.Entities.Entity entity in world.Entities)
        {
            MindState mind = loop.Cognition.MindOf(entity.Id.Value);
            foreach (Simulation.Core.Entities.Entity peer in world.Entities.Where(other => other.Id.Value != entity.Id.Value))
            {
                mind.Trust.Interact(peer.Id.Value);
            }

            mind.Beliefs.ApplyEvidence(
                new Fact("entity-9", "position", "10.00,10.00"),
                0.9,
                "perception-9",
                options.Agents.Beliefs,
                tick: 9);
        }

        loop.Cognition.Groups.Step(
            tick: 10,
            world.Entities.ToDictionary(entity => entity.Id.Value, entity => loop.Cognition.MindOf(entity.Id.Value)));

        WorldSnapshot snapshot = WorldSnapshot.Capture(loop, seed: 7);
        JsonObject message = ObservabilitySerializer.SnapshotMessage(snapshot);
        JsonArray groups = message["groups"]!.AsArray();

        GroupSnapshot group = Assert.Single(snapshot.Groups);
        Assert.Equal(1UL, group.GroupId);
        Assert.Equal(3, group.Size);
        Assert.Contains(group.LeaderId, new ulong?[] { 1, 2, 3 });
        Assert.Equal(10UL, group.BornTick);

        JsonObject json = (JsonObject)groups[0]!;
        Assert.DoesNotContain("\"GroupId\"", ObservabilitySerializer.ToJsonText(message));
        Assert.Equal(1UL, (ulong?)json["groupId"]);
        Assert.Equal(3, (int?)json["size"]);
        Assert.Equal(group.LeaderId, (ulong?)json["leaderId"]);
        Assert.Equal(10UL, (ulong?)json["bornTick"]);
    }

    [Fact]
    public void EventSensor_GroupEvents_CarryContractValues()
    {
        // SYNE-060/061 : événements typés group_formed / group_dissolved /
        // group_decision avec les schémas de valeur d'API_CONTRACTS §2.2.
        var group = new Social.Group(1, bornTick: 10, [1UL, 2UL, 3UL])
        {
            MeanCohesion = 0.42,
            LeaderId = 2,
        };

        ExternalEvent formed = EventSensor.GroupFormed(tick: 10, group);
        Assert.Equal(ObservabilityContract.GroupFormed, formed.Type);
        Assert.Equal("2", formed.AgentId);
        var formedValue = (JsonObject)formed.Value!;
        Assert.Equal(1UL, (ulong?)formedValue["groupId"]);
        Assert.Equal(3, (int?)formedValue["size"]);
        Assert.Equal(0.42, (double?)formedValue["cohesion"]);

        var dissolved = new GroupDissolution(1, 30, 10, [1UL, 2UL, 3UL], Success: true, 3, 3);
        ExternalEvent dissolvedEvent = EventSensor.GroupDissolved(tick: 30, dissolved);
        Assert.Equal(ObservabilityContract.GroupDissolved, dissolvedEvent.Type);
        var dissolvedValue = (JsonObject)dissolvedEvent.Value!;
        Assert.Equal(20L, (long?)dissolvedValue["lifetime"]);
        Assert.Equal(true, (bool?)dissolvedValue["success"]);
        Assert.Equal(3, (int?)dissolvedValue["membersOut"]);
        Assert.Equal(3, (int?)dissolvedValue["membersIn"]);

        var decision = new GroupDecision(1, 20, 2, DesireKind.SeekFood, 0.67);
        ExternalEvent decisionEvent = EventSensor.GroupDecision(tick: 20, decision);
        Assert.Equal(ObservabilityContract.GroupDecision, decisionEvent.Type);
        Assert.Equal("2", decisionEvent.AgentId);
        Assert.Equal("SeekFood", decisionEvent.Action);
        var decisionValue = (JsonObject)decisionEvent.Value!;
        Assert.Equal("SeekFood", (string?)decisionValue["decision"]);
    }

    [Fact]
    public void EventSensor_AgentSpawned_CarriesParentage()
    {
        // SYNE-062 : la naissance fournit la parenté de l'entité née.
        var birth = new BirthObservation(
            100,
            MotherId: 1,
            FatherId: 2,
            ChildId: 3,
            "Entité A",
            new Position(12.5, 20.75),
            TraitSet.NeutralAll);

        ExternalEvent spawned = EventSensor.AgentSpawned(tick: 100, birth);
        Assert.Equal(ObservabilityContract.AgentSpawned, spawned.Type);
        Assert.Equal("3", spawned.AgentId);

        var value = (JsonObject)spawned.Value!;
        Assert.Equal(3UL, (ulong?)value["childId"]);
        Assert.Equal(1UL, (ulong?)value["motherId"]);
        Assert.Equal(2UL, (ulong?)value["fatherId"]);
        Assert.Equal("Entité A", (string?)value["species"]);
    }

    [Fact]
    public void EventSensor_MessageSent_CarriesDeliveryContract()
    {
        // SYNE-050 : événement message_sent (envoi ou relais) avec identifiant,
        // type, sauts, confiance et taille du payload (API_CONTRACTS §2.2).
        var sent = new MessageSent(
            MessageId: 42,
            SenderId: 1,
            TargetId: 3,
            Type: MessageType.Information,
            Payload: "perceived-9#10.00,10.00",
            Hops: 1,
            Confidence: 0.81);

        ExternalEvent messageEvent = EventSensor.MessageSent(tick: 7, sent);
        JsonObject json = ObservabilitySerializer.EventMessage(messageEvent);

        Assert.Equal(ObservabilityContract.MessageSent, (string?)json["type"]);
        Assert.Equal("1", (string?)json["agentId"]);
        Assert.Equal("3", (string?)json["targetId"]);
        Assert.Equal("Information", (string?)json["action"]);
        var value = (JsonObject)messageEvent.Value!;
        Assert.Equal(42UL, (ulong?)value["messageId"]);
        Assert.Equal(1, (int?)value["hops"]);
        Assert.Equal(0.81, (double?)value["confidence"]);
        Assert.Equal(23, (int?)value["payloadLength"]);
    }

    [Fact]
    public void EventSensor_MessageReceived_CarriesReceptionContract()
    {
        // SYNE-051 : recevoir (y compris interception) — confiance ajustée par la
        // relation du récepteur + drapeau d'incompréhension (API_CONTRACTS §2.2).
        var received = new MessageReceived(
            MessageId: 42,
            ReceiverId: 2,
            SenderId: 1,
            Type: MessageType.Warning,
            Hops: 2,
            Confidence: 0.6,
            Understood: false);

        ExternalEvent messageEvent = EventSensor.MessageReceived(tick: 8, received);
        JsonObject json = ObservabilitySerializer.EventMessage(messageEvent);

        Assert.Equal(ObservabilityContract.MessageReceived, (string?)json["type"]);
        Assert.Equal("2", (string?)json["agentId"]);
        Assert.Equal("1", (string?)json["targetId"]);
        Assert.Equal("Warning", (string?)json["action"]);
        var value = (JsonObject)messageEvent.Value!;
        Assert.Equal(42UL, (ulong?)value["messageId"]);
        Assert.Equal(2, (int?)value["hops"]);
        Assert.Equal(0.6, (double?)value["confidence"]);
        Assert.False((bool?)value["understood"]);
    }

    [Fact]
    public void EventSensor_AgentDied_CarriesCauseAndSpecies()
    {
        // SYNE-074 : la mort par épuisement porte cause et espèce (API_CONTRACTS §2.2).
        var death = new DeathObservation(
            Tick: 200,
            EntityId: 9,
            Species: "Entité A",
            Cause: "energy_exhaustion");

        ExternalEvent died = EventSensor.AgentDied(tick: 200, death);
        JsonObject json = ObservabilitySerializer.EventMessage(died);

        Assert.Equal(ObservabilityContract.AgentDied, (string?)json["type"]);
        Assert.Equal("9", (string?)json["agentId"]);
        Assert.Equal("energy_exhaustion", (string?)json["cause"]);
        var value = (JsonObject)died.Value!;
        Assert.Equal("energy_exhaustion", (string?)value["cause"]);
        Assert.Equal("Entité A", (string?)value["species"]);
    }

    [Fact]
    public void Event_ActionCompleted_WithReserve_AddsReserveFields()
    {
        // SYNE-040 : une action qui consomme une réserve (Eat/Drink) porte les
        // champs reserve / reserveConsumed dans l'événement action_completed.
        var result = new ActionResult(
            DesireKind.Eat,
            ActionOutcome.Executed,
            Reason: null,
            EnergyDelta: -0.5,
            HungerDelta: -20.0,
            ThirstDelta: 0.0,
            FatigueDelta: 0.0,
            ReserveConsumed: ResourceKind.Food,
            ReserveConsumedAmount: 1.0);

        ExternalEvent completed = EventSensor.ActionCompleted(tick: 12, agentId: 4, result);
        JsonObject json = ObservabilitySerializer.EventMessage(completed);

        Assert.Equal(ObservabilityContract.ActionCompleted, (string?)json["type"]);
        Assert.Equal("4", (string?)json["agentId"]);
        Assert.Equal("Eat", (string?)json["action"]);
        var value = (JsonObject)completed.Value!;
        Assert.Equal("food", (string?)value["reserve"]);
        Assert.Equal(1.0, (double?)value["reserveConsumed"]);
        Assert.Equal(-20.0, (double?)value["hungerDelta"]);
    }
}