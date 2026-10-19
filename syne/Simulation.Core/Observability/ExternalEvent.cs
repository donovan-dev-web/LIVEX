// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Événement discret typé (ExternalEvent).

using System.Globalization;
using System.Text.Json.Nodes;
using Simulation.Core.Communication;
using Simulation.Core.Population;
using Simulation.Core.Social;
using Simulation.Core.World;

namespace Simulation.Core.Observability;

/// <summary>
/// Événement discret typé (ExternalEvent). V0.1 :
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
public static partial class EventSensor
{
    /// <summary>Nom canonique d'un état de décision (nomenclature du flux d'événements).</summary>
    private static string StateName(Cognition.DecisionState state) => state switch
    {
        Cognition.DecisionState.New => "fresh",
        Cognition.DecisionState.Holdover => "holdover",
        Cognition.DecisionState.Interrupted => "interrupted",
        _ => "none",
    };
}
