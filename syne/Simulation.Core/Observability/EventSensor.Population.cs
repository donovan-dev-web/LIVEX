// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : EventSensor - partie Population.

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
}
