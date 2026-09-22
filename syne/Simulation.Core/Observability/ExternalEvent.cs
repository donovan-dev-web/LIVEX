using System.Text.Json.Nodes;

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
}