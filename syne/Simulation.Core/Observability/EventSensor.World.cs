// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : EventSensor - partie World.

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
    /// Événement <c>world.construction_placed</c> (SYNE-071) : pose d'une construction
    /// (= obstacle statique) — modification d'environnement tracée.
    /// Événement d'environnement (pas d'agent porteur) : identifiant, position et
    /// rayon de l'obstacle posé.
    /// </summary>
    public static ExternalEvent ConstructionPlaced(ulong tick, Simulation.Core.World.Obstacle obstacle)
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
    public static ExternalEvent ConstructionRemoved(ulong tick, Simulation.Core.World.Obstacle obstacle)
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
    /// déterminisme total (fonction pure du tick, 0 tirage PRNG).
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


    /// <summary>
    /// Événement <c>world.territory_membership_changed</c> (SYNE-073) :
    /// une entité est **entrée dans** ou **sortie de** la zone d'un « point de survie »
    /// au tick du franchissement (suivi actif <c>world.territories.enabled</c>) —
    /// déterminisme total (appartenance = fonction pure des positions, 0 tirage PRNG,
    /// Porteur : l'entité (AgentId), cible : la zone (TargetId),
    /// charge utile : le sens du changement {kind} (camelCase).
    /// </summary>
    public static ExternalEvent TerritoryMembershipChanged(ulong tick, Simulation.Core.World.TerritoryMembershipChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["kind"] = change.Kind == Simulation.Core.World.TerritoryMembershipChangeKind.Entered ? "entered" : "left",
        };
        return new ExternalEvent(
            ObservabilityContract.TerritoryMembershipChanged,
            tick,
            AgentId: change.EntityId.ToString(CultureInfo.InvariantCulture),
            TargetId: change.Zone.Id,
            Value: value);
    }

    /// <summary>Écriture d'un livre (SYNE-121) : coût énergétique payé par l'auteur.</summary>
    public static ExternalEvent BookWritten(ulong tick, Simulation.Core.World.BookChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = change.Book.Id,
            ["title"] = change.Book.Title,
            ["writtenTick"] = change.Book.WrittenTick,
            ["cost"] = Math.Abs(change.Value),
        };
        return new ExternalEvent(ObservabilityContract.BookWritten, tick,
            AgentId: change.Book.AuthorId.ToString(CultureInfo.InvariantCulture),
            TargetId: change.Book.Id, Value: value);
    }


    /// <summary>Consultation d'un livre (SYNE-121) : bénéfice de principe configuré.</summary>
    public static ExternalEvent BookRead(ulong tick, Simulation.Core.World.BookChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var value = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = change.Book.Id,
            ["readBenefit"] = change.Value,
        };
        return new ExternalEvent(ObservabilityContract.BookRead, tick,
            AgentId: change.ReaderId?.ToString(CultureInfo.InvariantCulture),
            TargetId: change.Book.Id, Value: value);
    }

}
