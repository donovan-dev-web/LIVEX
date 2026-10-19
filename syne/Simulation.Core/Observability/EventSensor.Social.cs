// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : EventSensor - partie Social.

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
            // Instrumentation A0-3 : distance max des paires liées au moment de la
            // formation — null JSON = non mesurée (aucune table de positions),
            // jamais zéro observé.
            ["maxBondDistance"] = group.MaxBondDistance is { } distance ? Math.Round(distance, 4) : null,
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
}
