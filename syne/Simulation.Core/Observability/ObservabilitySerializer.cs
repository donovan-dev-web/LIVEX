using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Core.World;

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
            // Champ additif V0.2 : le seed voyage dans le snapshot, ECHOS n'a plus à
            // le dériver du runId (les runs pilotés portaient seed: "" côté ECHOS).
            ["seed"] = (ulong)snapshot.Seed,
            ["tick"] = (ulong)snapshot.Tick,
            ["simulatedTimeMinutes"] = snapshot.SimulatedTimeMinutes,
            ["aliveCount"] = snapshot.AliveCount,
            ["agents"] = agents,
            ["resources"] = ResourcesJson(snapshot.Resources),
            ["obstacles"] = ObstaclesJson(snapshot.Obstacles),
            ["groups"] = GroupsJson(snapshot.Groups),
            ["season"] = snapshot.Season,
            ["seasonIndex"] = snapshot.SeasonIndex,
            ["territories"] = TerritoriesJson(snapshot.Territories),
            ["books"] = BooksJson(snapshot.Books),
            ["worldChanges"] = WorldChangesJson(snapshot.WorldChanges),
            ["actions"] = ActionsJson(snapshot.Actions),
        };
        return message;
    }

    private static JsonArray WorldChangesJson(IReadOnlyList<WorldChangeSnapshot> changes)
    {
        var array = new JsonArray();
        foreach (WorldChangeSnapshot change in changes)
        {
            array.Add(new JsonObject
            {
                ["kind"] = change.Kind,
                ["id"] = change.Id,
                ["x"] = change.X,
                ["y"] = change.Y,
                ["radius"] = change.Radius,
            });
        }
        return array;
    }

    private static JsonArray ActionsJson(IReadOnlyList<ActionSnapshot> actions)
    {
        var array = new JsonArray();
        foreach (ActionSnapshot action in actions)
        {
            var item = new JsonObject
            {
                ["agentId"] = action.AgentId,
                ["action"] = action.Action,
                ["outcome"] = action.Outcome,
                ["cause"] = action.Cause,
                ["energyDelta"] = action.EnergyDelta,
                ["hungerDelta"] = action.HungerDelta,
                ["thirstDelta"] = action.ThirstDelta,
                ["fatigueDelta"] = action.FatigueDelta,
            };
            if (action.Reserve is not null)
            {
                item["reserve"] = action.Reserve;
                item["reserveConsumed"] = action.ReserveConsumed;
            }
            array.Add(item);
        }
        return array;
    }

    private static JsonArray TerritoriesJson(IReadOnlyList<TerritorySnapshot> territories)
    {
        var array = new JsonArray();
        foreach (TerritorySnapshot zone in territories)
        {
            var members = new JsonArray();
            foreach (ulong member in zone.Members)
            {
                members.Add(member);
            }

            array.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = zone.Id,
                ["x"] = zone.X,
                ["y"] = zone.Y,
                ["radius"] = zone.Radius,
                ["memberCount"] = zone.MemberCount,
                ["members"] = members,
            });
        }

        return array;
    }

    /// <summary>
    /// Livres au serializer (SYNE-121, décisions n°18/19, Monographie §3.18) :
    /// chaque livre est émis en <c>{id, authorId, title, writtenTick, readCount,
    /// readers[]}</c> (lecteurs distincts dans l'ordre de première consultation)
    /// — champ books[] additif, émis seulement quand world.books.enabled est actif
    /// (désactivé par défaut ⇒ trajectoire du scénario de référence inchangée,
    /// checksums dorés ré-épinglés inchangés, pin contractuel).
    /// </summary>
    private static JsonArray BooksJson(IReadOnlyList<BookSnapshot> books)
    {
        var array = new JsonArray();
        foreach (BookSnapshot book in books)
        {
            var readers = new JsonArray();
            foreach (ulong reader in book.Readers)
            {
                readers.Add(reader);
            }

            array.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = book.Id,
                ["authorId"] = book.AuthorId,
                ["title"] = book.Title,
                ["content"] = book.Content,
                ["writtenTick"] = book.WrittenTick,
                ["readCount"] = book.ReadCount,
                ["readers"] = readers,
            });
        }

        return array;
    }

    private static JsonArray ObstaclesJson(IReadOnlyList<ObstacleSnapshot> obstacles)
    {
        var array = new JsonArray();
        foreach (ObstacleSnapshot obstacle in obstacles)
        {
            array.Add(new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = obstacle.Id,
                ["x"] = obstacle.X,
                ["y"] = obstacle.Y,
                ["radius"] = obstacle.Radius,
            });
        }

        return array;
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

    private static JsonArray GroupsJson(IReadOnlyList<GroupSnapshot> groups)
    {
        var array = new JsonArray();
        foreach (GroupSnapshot group in groups)
        {
            var members = new JsonArray();
            foreach (ulong member in group.Members)
            {
                members.Add(member);
            }

            var json = new System.Text.Json.Nodes.JsonObject
            {
                ["groupId"] = group.GroupId,
                ["members"] = members,
                ["size"] = group.Size,
                ["bornTick"] = group.BornTick,
                ["cohesion"] = group.Cohesion,
                ["consensus"] = group.Consensus,
            };
            if (group.LeaderId is { } leaderId)
            {
                json["leaderId"] = leaderId;
            }

            if (group.Decision is { } decision)
            {
                json["decision"] = decision;
            }

            array.Add(json);
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

    public static JsonObject WorldDeltaMessage(
        ulong tick, string runId, IReadOnlyList<EnvironmentChange> changes)
    {
        var array = new JsonArray();
        foreach (EnvironmentChange change in changes)
        {
            array.Add(new JsonObject
            {
                ["kind"] = change.Kind == EnvironmentChangeKind.Added ? "added" : "removed",
                ["id"] = change.Obstacle.Id,
                ["x"] = change.Obstacle.Position.X,
                ["y"] = change.Obstacle.Position.Y,
                ["radius"] = change.Obstacle.Radius,
            });
        }

        return new JsonObject
        {
            ["type"] = ObservabilityContract.WorldDelta,
            ["runId"] = runId,
            ["tick"] = tick,
            ["changes"] = array,
        };
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

        var json = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = agent.Id,
            ["species"] = agent.Species,
            ["position"] = position,
            ["energy"] = agent.Energy,
            ["hunger"] = agent.Hunger,
            ["thirst"] = agent.Thirst,
            ["fatigue"] = agent.Fatigue,
            ["currentAction"] = agent.CurrentAction,
            ["currentIntention"] = agent.CurrentIntention,
            ["traits"] = traits,
            ["beliefs"] = beliefs,
            ["goals"] = goals,
            ["trust"] = trust,
            ["memoryCount"] = agent.MemoryCount,
        };

        // Champs additifs du contrat 0.3.0 (D7/D8/D5) : émis seulement quand les
        // drapeaux correspondants sont actifs — sortie bit-à-bit identique sinon.
        if (agent.Inventory.Count > 0)
        {
            var inventory = new System.Text.Json.Nodes.JsonObject();
            foreach (System.Collections.Generic.KeyValuePair<string, double> entry in
                     agent.Inventory.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                inventory[entry.Key] = entry.Value;
            }

            json["inventory"] = inventory;
        }

        if (agent.Commitments.Count > 0)
        {
            var commitments = new JsonArray();
            foreach (CommitmentObservation commitment in agent.Commitments)
            {
                commitments.Add(new System.Text.Json.Nodes.JsonObject
                {
                    ["toEntityId"] = commitment.ToEntityId,
                    ["requestType"] = commitment.RequestType,
                    ["status"] = commitment.Status,
                    ["createdTick"] = commitment.CreatedTick,
                    ["expiryTick"] = commitment.ExpiryTick,
                });
            }

            json["commitments"] = commitments;
        }

        return json;
    }
}