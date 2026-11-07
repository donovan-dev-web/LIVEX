// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Prism.Contracts;
using Simulation.Core.World;

namespace Simulation.Console.Integration;

/// <summary>
/// Alimente <see cref="PrismWorldAdapter"/> depuis le flux de lecture PRISM
/// (SYNE-220 étape 2, bascule par profil).
/// </summary>
/// <remarks>
/// PRISM est autoritaire sur les positions : chaque enveloppe d'observation
/// porte l'auto-observation de l'agent (<c>self.position</c>), qui est la seule
/// source fiable pour le déplacement — les entrées <c>Agent</c> de
/// <c>changes</c> ne concernent que les voisins perçus et restent plafonnées par
/// la perception.
/// <para>
/// Une enveloppe illisible ou sans <c>self.position</c> est ignorée (le contrat
/// tolère les champs optionnels) : la synchronisation ne doit jamais faire
/// échouer un tick qui a réussi côté PRISM.
/// </para>
/// </remarks>
public sealed class PrismWorldSynchronizer
{
    private readonly PrismWorldAdapter _world;

    public PrismWorldSynchronizer(PrismWorldAdapter world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    /// <summary>Nombre cumulé d'entités effectivement déplacées.</summary>
    public int MovedTotal { get; private set; }

    /// <summary>Nombre d'enveloppes appliquées avec une position exploitable.</summary>
    public int AppliedTotal { get; private set; }

    /// <summary>
    /// Applique une enveloppe d'observation au monde externe.
    /// </summary>
    /// <returns><c>true</c> quand une position a été lue et transmise au monde.</returns>
    public bool Apply(ObservationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!TryReadSelfPosition(envelope.Self, out Position position))
        {
            return false;
        }

        MovedTotal += _world.ApplyAgentStates([(envelope.AgentId, position)]);
        AppliedTotal++;
        return true;
    }

    /// <summary>
    /// Applique un lot d'enveloppes en une seule écriture du monde (ordre
    /// canonique garanti par l'adaptateur).
    /// </summary>
    /// <returns>Nombre de positions appliquées.</returns>
    public int Apply(IReadOnlyList<ObservationEnvelope> envelopes)
    {
        ArgumentNullException.ThrowIfNull(envelopes);

        var states = new List<(ulong AgentId, Position Position)>(envelopes.Count);
        foreach (ObservationEnvelope envelope in envelopes)
        {
            if (envelope is not null && TryReadSelfPosition(envelope.Self, out Position position))
            {
                states.Add((envelope.AgentId, position));
            }
        }

        if (states.Count == 0)
        {
            return 0;
        }

        MovedTotal += _world.ApplyAgentStates(states);
        AppliedTotal += states.Count;
        return states.Count;
    }

    /// <summary>
    /// Lit <c>self.position.{x,y}</c> dans une auto-observation PRISM et la
    /// convertit en unités de monde SYNE selon ADR-013 : les coordonnées publiées
    /// sont des <b>milli-blocs</b> (1 000 par bloc), donc 1 unité SYNE = 1 bloc =
    /// 1 m. Aucun arrondi : la position reste continue (<c>128 500</c> milli-blocs
    /// → <c>128,5</c> blocs).
    /// </summary>
    public static bool TryReadSelfPosition(JsonElement self, out Position position)
    {
        position = default;
        if (self.ValueKind != JsonValueKind.Object ||
            !self.TryGetProperty("position", out JsonElement positionElement) ||
            positionElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryReadCoordinate(positionElement, "x", out double x) ||
            !TryReadCoordinate(positionElement, "y", out double y))
        {
            return false;
        }

        position = WorldSpace.FromMilliBlocks(x, y);
        return true;
    }

    private static bool TryReadCoordinate(JsonElement positionElement, string property, out double value)
    {
        value = 0;
        if (!positionElement.TryGetProperty(property, out JsonElement coordinate))
        {
            return false;
        }

        switch (coordinate.ValueKind)
        {
            case JsonValueKind.Number:
                if (!coordinate.TryGetDouble(out value) || !double.IsFinite(value))
                {
                    return false;
                }

                return true;
            default:
                return false;
        }
    }
}
