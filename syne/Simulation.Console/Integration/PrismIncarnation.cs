// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using Prism.Contracts;

namespace Simulation.Console.Integration;

/// <summary>
/// Incarnation des agents SYNE dans le monde PRISM : chaque entité reçoit la
/// commande <c>Incarner</c> une seule fois, au premier tick du run, sur la case
/// que PRISM a déclarée praticable.
/// </summary>
/// <remarks>
/// Les coordonnées de la commande sont des <b>blocs entiers</b> (ADR-013),
/// identiques aux cases de l'instantané de monde : aucune conversion.
/// <para>
/// Sans cette commande, PRISM ne connaît aucun agent, ne publie donc aucune
/// observation par agent, et la boucle couplée tourne à vide — c'est exactement
/// ce que le run R2 du 6 novembre a révélé (<c>minds = 0</c>).
/// </para>
/// </remarks>
public static class PrismIncarnation
{
    /// <summary>
    /// Construit les commandes d'incarnation d'un tick. L'ordre est celui des
    /// identifiants d'agent : la même affectation produit le même flux.
    /// </summary>
    public static IReadOnlyList<CommandEnvelope> Build(
        long tick,
        IReadOnlyList<(ulong AgentId, PrismSpawnCell Cell)> assignments)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentNullException.ThrowIfNull(assignments);

        var commands = new List<CommandEnvelope>(assignments.Count);
        foreach ((ulong agentId, PrismSpawnCell cell) in
                 assignments.OrderBy(static assignment => assignment.AgentId))
        {
            JsonElement payload = JsonSerializer.SerializeToElement(
                new { position = new { x = cell.X, y = cell.Y, z = cell.FeetZ } },
                ProtocolJson.Options);
            commands.Add(new CommandEnvelope(
                ProtocolVersion.Current.ToString(),
                tick,
                string.Concat(
                    "syne:",
                    tick.ToString(CultureInfo.InvariantCulture),
                    ":",
                    agentId.ToString(CultureInfo.InvariantCulture),
                    ":Incarner"),
                agentId,
                "Incarner",
                payload));
        }

        return commands;
    }
}
