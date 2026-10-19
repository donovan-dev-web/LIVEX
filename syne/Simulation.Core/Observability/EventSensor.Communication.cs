// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : EventSensor - partie Communication.

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
}
