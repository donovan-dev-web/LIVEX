// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : EventSensor - partie Cognition.

using System.Globalization;
using System.Text.Json.Nodes;
using Simulation.Core.Communication;
using Simulation.Core.Population;
using Simulation.Core.Social;

namespace Simulation.Core.Observability;

using System.Globalization;
using System.Text.Json.Nodes;
using Simulation.Core.Communication;
using Simulation.Core.Population;
using Simulation.Core.Social;

public static partial class EventSensor
{

    /// <summary>
    /// Événement <c>decision_made</c> par entité (intention, utilité du dernier
    /// score délibéré + drapeaux de délibération/interruption du tick courant).
    /// </summary>
    public static ExternalEvent DecisionMade(ulong tick, ulong agentId, Cognition.MindState mind)
    {
        ArgumentNullException.ThrowIfNull(mind);
        string action = mind.Intention?.Kind.ToString() ?? "Idle";
        Cognition.DecisionRecord? record = mind.LastDecisionRecord;
        Cognition.DecisionState state = mind.DecisionStateThisTick;
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["intention"] = action,
            ["decisionState"] = StateName(state),
            ["deliberated"] = mind.DeliberatedThisTick,
            ["interrupted"] = mind.InterruptedThisTick,
        };

        // Honnêteté des données (S-28 / D-03 / B-10) : l'utilité n'est publiée que
        // si une décision a réellement eu lieu au tick courant. Un report (holdover)
        // publie le tick de sa décision d'origine ET son score sous un nom distinct
        // — jamais un score ancien sous le nom « utility » du tick courant ; une
        // absence publie explicitement <c>null</c> (jamais 0.0 : zéro observé et
        // absence de mesure ne doivent pas se confondre en amont d'ECHOS).
        if (record is null || record.Missing)
        {
            value["utility"] = null;
            value["decisionTick"] = null;
        }
        else if (state is Cognition.DecisionState.New or Cognition.DecisionState.Interrupted)
        {
            value["utility"] = record.Utility;
            value["decisionTick"] = record.Tick;
        }
        else
        {
            value["utility"] = null;
            value["decisionTick"] = record.Tick;
            value["heldUtility"] = record.Utility;
        }

        if (record is { Missing: false } decided)
        {
            if (decided.Target is { } target)
            {
                value["target"] = target.Label;
                value["targetType"] = target.TargetType;
                value["targetConfidence"] = Math.Round(target.Confidence, 4);
                value["targetAge"] = target.Age(tick);
            }

            value["rejected"] = new System.Text.Json.Nodes.JsonArray(
                decided.RejectedKinds.Select(kind => (System.Text.Json.Nodes.JsonNode)System.Text.Json.Nodes.JsonValue.Create(kind.ToString())).ToArray());

            // Trace causale complète (D-06 / AC8) : les faits justificatifs du choix
            // (provenance, sujet, confiance, tick d'observation) sont publiés eux aussi,
            // et non seulement l'arithmétique du score — ECHOS peut ainsi remonter
            // action ← but ← croyance ← mémoire ← perception sans relire le snapshot.
            if (decided.Evidence is { Count: > 0 } evidence)
            {
                var evidenceJson = new System.Text.Json.Nodes.JsonArray();
                foreach (Cognition.DecisionEvidence fact in evidence)
                {
                    evidenceJson.Add((System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject
                    {
                        ["source"] = fact.Source,
                        ["subject"] = fact.Subject,
                        ["confidence"] = Math.Round(fact.Confidence, 4),
                        ["observedTick"] = fact.ObservedTick,
                    });
                }

                value["evidence"] = evidenceJson;
            }

            if (decided.ResultOutcome is { } outcome)
            {
                value["result"] = outcome;
            }

            if (decided.ResultReason is { } reason)
            {
                value["resultReason"] = reason;
            }
        }
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
    /// de l'action du tick (SYNE-040) avec les deltas d'effets.
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
}
