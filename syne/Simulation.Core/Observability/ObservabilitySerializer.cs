using System.Text.Json;
using System.Text.Json.Nodes;

namespace Simulation.Core.Observability;

/// <summary>
/// Sérialisation déterministe camelCase des messages d'observabilité
/// (API_CONTRACTS.md §2). Les objets <c>JsonObject</c> préservent leur ordre
/// d'insertion → sortie stable et testable (checksum d'émission).
/// </summary>
public static class ObservabilitySerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static JsonObject SnapshotMessage(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var agents = new JsonArray();
        foreach (AgentSnapshot agent in snapshot.Agents)
        {
            agents.Add(AgentJson(agent));
        }

        var message = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = ObservabilityContract.SnapshotType,
            ["version"] = snapshot.Version,
            ["engineVersion"] = ObservabilityContract.EngineVersion,
            ["runId"] = snapshot.RunId,
            ["tick"] = (ulong)snapshot.Tick,
            ["simulatedTimeMinutes"] = snapshot.SimulatedTimeMinutes,
            ["aliveCount"] = snapshot.AliveCount,
            ["agents"] = agents,
            ["resources"] = ResourcesJson(snapshot.Resources),
        };
        return message;
    }

    private static JsonArray ResourcesJson(IReadOnlyList<ResourceSnapshot> resources)
    {
        var array = new JsonArray();
        foreach (ResourceSnapshot resource in resources)
        {
            array.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = resource.Type,
                ["quantity"] = resource.Quantity,
            });
        }

        return array;
    }

    public static JsonObject EventMessage(ExternalEvent externalEvent)
    {
        ArgumentNullException.ThrowIfNull(externalEvent);
        var message = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = externalEvent.Type,
            ["tick"] = externalEvent.Tick,
        };
        if (externalEvent.AgentId is not null)
        {
            message["agentId"] = externalEvent.AgentId;
        }

        if (externalEvent.TargetId is not null)
        {
            message["targetId"] = externalEvent.TargetId;
        }

        if (externalEvent.Action is not null)
        {
            message["action"] = externalEvent.Action;
        }

        if (externalEvent.Cause is not null)
        {
            message["cause"] = externalEvent.Cause;
        }

        if (externalEvent.Value is not null)
        {
            message["value"] = externalEvent.Value;
        }

        return message;
    }

    public static string ToJsonText(JsonObject message) => message.ToJsonString(Options);

    private static JsonObject AgentJson(AgentSnapshot agent)
    {
        var position = new System.Text.Json.Nodes.JsonObject
        {
            ["x"] = agent.PositionX,
            ["y"] = agent.PositionY,
        };
        var traits = new System.Text.Json.Nodes.JsonObject();
        foreach ((string name, double value) in agent.Traits.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            traits[name] = value;
        }

        var beliefs = new JsonArray();
        foreach (BeliefObservation belief in agent.Beliefs)
        {
            beliefs.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["subject"] = belief.Subject,
                ["predicate"] = belief.Predicate,
                ["value"] = belief.Value,
                ["confidence"] = belief.Confidence,
            });
        }

        var goals = new JsonArray();
        foreach (GoalObservation goal in agent.Goals)
        {
            goals.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["kind"] = goal.Kind,
                ["age"] = goal.Age,
            });
        }

        var trust = new JsonArray();
        foreach (TrustObservation relation in agent.Trust)
        {
            trust.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["peerId"] = relation.PeerId,
                ["trust"] = relation.Trust,
            });
        }

        return new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = agent.Id,
            ["species"] = agent.Species,
            ["position"] = position,
            ["energy"] = agent.Energy,
            ["hunger"] = agent.Hunger,
            ["thirst"] = agent.Thirst,
            ["fatigue"] = agent.Fatigue,
            ["currentAction"] = agent.CurrentIntention,
            ["traits"] = traits,
            ["beliefs"] = beliefs,
            ["goals"] = goals,
            ["trust"] = trust,
            ["memoryCount"] = agent.MemoryCount,
        };
    }
}