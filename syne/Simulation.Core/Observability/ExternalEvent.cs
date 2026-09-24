using System.Globalization;
using System.Text.Json.Nodes;
using Simulation.Core.Communication;
using Simulation.Core.Population;
using Simulation.Core.Social;
using Simulation.Core.World;

namespace Simulation.Core.Observability;

/// <summary>
/// Événement discret typé (API_CONTRACTS.md §2.2 — ExternalEvent). V0.1 :
/// <c>tick_summary</c> (1 par tick) et <c>decision_made</c> (1 par entité par tick).
/// </summary>
public sealed record ExternalEvent(
    string Type,
    ulong Tick,
    string? AgentId = null,
    string? TargetId = null,
    string? Action = null,
    string? Cause = null,
    JsonNode? Value = null);

/// <summary>Production d'événements discrets à partir de la cognition (SYNE-080).</summary>
public static class EventSensor
{
    /// <summary>Événement <c>tick_summary</c> (voir API_CONTRACTS.md §2.2).</summary>
    public static ExternalEvent TickSummary(ulong tick, int aliveCount) =>
        new(ObservabilityContract.TickSummary, tick, Value: (JsonNode)new System.Text.Json.Nodes.JsonObject
        {
            ["aliveCount"] = aliveCount,
        });

    /// <summary>
    /// Événement <c>decision_made</c> par entité (intention, utilité du dernier
    /// score délibéré + drapeaux de délibération/interruption du tick courant,
    /// COGNITIVE_ARCHITECTURE.md §7).
    /// </summary>
    public static ExternalEvent DecisionMade(ulong tick, ulong agentId, Cognition.MindState mind)
    {
        ArgumentNullException.ThrowIfNull(mind);
        string action = mind.Intention?.Kind.ToString() ?? "Idle";
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["intention"] = action,
            ["utility"] = mind.LastDecision?.Utility ?? 0.0,
            ["deliberated"] = mind.DeliberatedThisTick,
            ["interrupted"] = mind.InterruptedThisTick,
        };
        return new ExternalEvent(
            ObservabilityContract.DecisionMade,
            tick,
            AgentId: agentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Action: action,
            Cause: $"hunger={mind.Needs.Hunger:0.#},thirst={mind.Needs.Thirst:0.#},fatigue={mind.Needs.Fatigue:0.#}",
            Value: value);
    }

    /// <summary>
    /// Événement <c>action_completed</c> par entité : issue de l'exécution atomique
    /// de l'action du tick (SYNE-040, API_CONTRACTS.md §2.2) avec les deltas d'effets.
    /// </summary>
    public static ExternalEvent ActionCompleted(ulong tick, ulong agentId, Actions.ActionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        string outcome = result.Outcome.ToString().ToLowerInvariant();
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["outcome"] = outcome,
            ["energyDelta"] = Math.Round(result.EnergyDelta, 4),
            ["hungerDelta"] = Math.Round(result.HungerDelta, 4),
            ["thirstDelta"] = Math.Round(result.ThirstDelta, 4),
            ["fatigueDelta"] = Math.Round(result.FatigueDelta, 4),
        };
        if (result.ReserveConsumed is { } reserve)
        {
            value["reserve"] = reserve.ToString().ToLowerInvariant();
            value["reserveConsumed"] = Math.Round(result.ReserveConsumedAmount, 4);
        }

        return new ExternalEvent(
            ObservabilityContract.ActionCompleted,
            tick,
            AgentId: agentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Action: result.Kind.ToString(),
            Cause: result.Reason,
            Value: value);
    }

    /// <summary>
    /// Événement <c>message_sent</c> (SYNE-050) : pulsation émise (envoi ou relais),
    /// avec identifiant, type, nombre de sauts, confiance de transmission et taille
    /// du payload. Traçable par ECHOS (heatmap de communication, NetworkCentrality...).
    /// </summary>
    public static ExternalEvent MessageSent(ulong tick, Communication.MessageSent sent)
    {
        ArgumentNullException.ThrowIfNull(sent);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["messageId"] = sent.MessageId,
            ["hops"] = sent.Hops,
            ["confidence"] = Math.Round(sent.Confidence, 4),
            ["payloadLength"] = sent.Payload.Length,
        };
        return new ExternalEvent(
            ObservabilityContract.MessageSent,
            tick,
            AgentId: sent.SenderId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TargetId: sent.TargetId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Action: sent.Type.ToString(),
            Value: value);
    }

    /// <summary>
    /// Événement <c>message_received</c> (SYNE-051) : pulsation reçue (y compris
    /// interception — le signal est public), avec confiance ajustée par la relation
    /// de confiance du récepteur envers l'émetteur et drapeau d'incompréhension.
    /// </summary>
    public static ExternalEvent MessageReceived(ulong tick, Communication.MessageReceived received)
    {
        ArgumentNullException.ThrowIfNull(received);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["messageId"] = received.MessageId,
            ["hops"] = received.Hops,
            ["confidence"] = Math.Round(received.Confidence, 4),
            ["understood"] = received.Understood,
        };
        return new ExternalEvent(
            ObservabilityContract.MessageReceived,
            tick,
            AgentId: received.ReceiverId.ToString(CultureInfo.InvariantCulture),
            TargetId: received.SenderId.ToString(CultureInfo.InvariantCulture),
            Action: received.Type.ToString(),
            Value: value);
    }

    /// <summary>Convertit une liste triée de membres en nœud JSON (ordre croissant, déterministe).</summary>
    private static JsonArray JsonMembers(IReadOnlyList<ulong> members)
    {
        var json = new JsonArray();
        foreach (ulong member in members)
        {
            json.Add(member);
        }

        return json;
    }

    /// <summary>
    /// Événement <c>group_formed</c> (SYNE-060) : apparition d'un groupe émergent.
    /// Porteur (agentId) : le leader émergent, sinon le plus petit membre.
    /// </summary>
    public static ExternalEvent GroupFormed(ulong tick, Group group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["groupId"] = group.Id,
            ["size"] = group.Members.Count,
            ["cohesion"] = Math.Round(group.MeanCohesion, 4),
            ["members"] = JsonMembers(group.Members),
        };
        ulong anchor = group.LeaderId ?? group.Members[0];
        return new ExternalEvent(
            ObservabilityContract.GroupFormed,
            tick,
            AgentId: anchor.ToString(CultureInfo.InvariantCulture),
            Value: value);
    }

    /// <summary>
    /// Événement <c>group_dissolved</c> (SYNE-060) : dissolution avec bilan de vie
    /// (durée, succès, turnover entrée/sortie). Sémantique consommée par ECHOS
    /// (group_dynamics : lifetime/success/membersOut/membersIn).
    /// </summary>
    public static ExternalEvent GroupDissolved(ulong tick, GroupDissolution dissolution)
    {
        ArgumentNullException.ThrowIfNull(dissolution);
        long lifetime = dissolution.FormedTick <= dissolution.Tick
            ? (long)(dissolution.Tick - dissolution.FormedTick)
            : 0;
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["groupId"] = dissolution.GroupId,
            ["lifetime"] = lifetime,
            ["success"] = dissolution.Success,
            ["membersOut"] = dissolution.MembersOut,
            ["membersIn"] = dissolution.MembersIn,
            ["members"] = JsonMembers(dissolution.Members),
        };
        return new ExternalEvent(
            ObservabilityContract.GroupDissolved,
            tick,
            Value: value);
    }

    /// <summary>
    /// Événement <c>group_decision</c> (SYNE-061) : décision collective adoptée —
    /// intention dominante + consensus pondéré par la confiance au leader.
    /// </summary>
    public static ExternalEvent GroupDecision(ulong tick, GroupDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["groupId"] = decision.GroupId,
            ["decision"] = decision.Decision.ToString(),
            ["consensus"] = Math.Round(decision.Consensus, 4),
        };
        return new ExternalEvent(
            ObservabilityContract.GroupDecision,
            tick,
            AgentId: decision.LeaderId.ToString(CultureInfo.InvariantCulture),
            Action: decision.Decision.ToString(),
            Value: value);
    }

    /// <summary>
    /// Événement <c>agent_spawned</c> (SYNE-062) : naissance d'une entité par la
    /// fusion consentie de deux parents.
    /// </summary>
    public static ExternalEvent AgentSpawned(ulong tick, BirthObservation birth)
    {
        ArgumentNullException.ThrowIfNull(birth);
        var position = new System.Text.Json.Nodes.JsonObject
        {
            ["x"] = Math.Round(birth.Position.X, 4),
            ["y"] = Math.Round(birth.Position.Y, 4),
        };
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["childId"] = birth.ChildId,
            ["motherId"] = birth.MotherId,
            ["fatherId"] = birth.FatherId,
            ["species"] = birth.Species,
            ["position"] = position,
        };
        return new ExternalEvent(
            ObservabilityContract.AgentSpawned,
            tick,
            AgentId: birth.ChildId.ToString(CultureInfo.InvariantCulture),
            Value: value);
    }

    /// <summary>
    /// Événement <c>agent_died</c> (SYNE-074) : mort d'une entité par épuisement
    /// (énergie ≤ seuil fatal). Consommé par ECHOS (population : courbe de survie).
    /// </summary>
    public static ExternalEvent AgentDied(ulong tick, DeathObservation death)
    {
        ArgumentNullException.ThrowIfNull(death);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["cause"] = death.Cause,
            ["species"] = death.Species,
        };
        return new ExternalEvent(
            ObservabilityContract.AgentDied,
            tick,
            AgentId: death.EntityId.ToString(CultureInfo.InvariantCulture),
            Cause: death.Cause,
            Value: value);
    }

    /// <summary>
    /// Événement <c>world.construction_placed</c> (SYNE-071) : pose d'une construction
    /// (= obstacle statique, décision n°20) — modification d'environnement tracée.
    /// Événement d'environnement (pas d'agent porteur) : identifiant, position et
    /// rayon de l'obstacle posé.
    /// </summary>
    public static ExternalEvent ConstructionPlaced(ulong tick, Obstacle obstacle)
    {
        ArgumentNullException.ThrowIfNull(obstacle);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = obstacle.Id,
            ["x"] = Math.Round(obstacle.Position.X, 4),
            ["y"] = Math.Round(obstacle.Position.Y, 4),
            ["radius"] = Math.Round(obstacle.Radius, 4),
        };
        return new ExternalEvent(
            ObservabilityContract.ConstructionPlaced,
            tick,
            TargetId: obstacle.Id,
            Value: value);
    }

    /// <summary>
    /// Événement <c>world.construction_removed</c> (SYNE-071) : retrait d'une
    /// construction — modification d'environnement tracée, même charge utile que
    /// la pose (identifiant, position, rayon de l'obstacle retiré).
    /// </summary>
    public static ExternalEvent ConstructionRemoved(ulong tick, Obstacle obstacle)
    {
        ArgumentNullException.ThrowIfNull(obstacle);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = obstacle.Id,
            ["x"] = Math.Round(obstacle.Position.X, 4),
            ["y"] = Math.Round(obstacle.Position.Y, 4),
            ["radius"] = Math.Round(obstacle.Radius, 4),
        };
        return new ExternalEvent(
            ObservabilityContract.ConstructionRemoved,
            tick,
            TargetId: obstacle.Id,
            Value: value);
    }

    /// <summary>
    /// Événement <c>world.season_changed</c> (SYNE-072) : basculement d'une saison à
    /// l'autre au tick du changement (cycle actif <c>world.seasons.enabled</c>) —
    /// déterminisme total (fonction pure du tick, 0 tirage PRNG, DETERMINISM.md §3).
    /// Événement d'environnement (pas d'agent porteur) : saisons précédente et courante
    /// en clé JSON camelCase.
    /// </summary>
    public static ExternalEvent SeasonChanged(ulong tick, Simulation.Core.Configuration.SeasonChange change)
    {
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["previous"] = World.Seasons.Name(change.Previous),
            ["current"] = World.Seasons.Name(change.Current),
        };
        return new ExternalEvent(
            ObservabilityContract.SeasonChanged,
            tick,
            TargetId: World.Seasons.Name(change.Current),
            Value: value);
    }
}