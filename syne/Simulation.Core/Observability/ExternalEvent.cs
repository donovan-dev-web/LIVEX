using System.Text.Json.Nodes;
using Simulation.Core.Communication;

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
            AgentId: received.ReceiverId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TargetId: received.SenderId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Action: received.Type.ToString(),
            Value: value);
    }
}