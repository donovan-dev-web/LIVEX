// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Empreinte canonique de l'état SYNE en fin de run couplé (PRISM-082, run R2).

using System.Globalization;
using System.Text;
using Simulation.Core.Cognition;
using Simulation.Core.Entities;
using Simulation.Core.Loop;

namespace Simulation.Console.Integration;

/// <summary>
/// Empreinte de l'état SYNE à la fin d'un run couplé (PRISM-082, run R2) : suite
/// canonique des entités (identifiant croissant) avec position et besoins en
/// <b>représentation binaire exacte</b>, hachée en FNV-1a 64 bits.
/// </summary>
/// <remarks>
/// Deux raisons de hasher les bits et non une chaîne formatée : un arrondi
/// masquerait une divergence réelle, et l'ordre canonique par identifiant rend
/// l'empreinte indépendante de l'ordre d'énumération du monde. L'empreinte ne
/// dépend que de l'état observé et du dernier tick — aucune horloge, aucun tirage.
/// </remarks>
public static class CoupledFingerprint
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>Empreinte hexadécimale (16 caractères) de l'état SYNE.</summary>
    public static string Compute(SimulationLoop loop, ulong lastTick)
    {
        ArgumentNullException.ThrowIfNull(loop);

        var builder = new StringBuilder(64);
        builder.Append(lastTick.ToString(CultureInfo.InvariantCulture));

        foreach (Entity entity in loop.World.Entities.OrderBy(candidate => candidate.Id.Value))
        {
            builder.Append('|')
                .Append(entity.Id.Value.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(Bits(entity.Position.X))
                .Append(',')
                .Append(Bits(entity.Position.Y));

            if (!loop.Cognition.HasMind(entity.Id.Value))
            {
                builder.Append(",nomind");
                continue;
            }

            MindState mind = loop.Cognition.MindOf(entity.Id.Value);
            builder.Append(',')
                .Append(Bits(mind.Needs.Hunger))
                .Append(',')
                .Append(Bits(mind.Needs.Thirst))
                .Append(',')
                .Append(Bits(mind.Needs.Fatigue))
                .Append(',')
                .Append(Bits(mind.Needs.Energy));
        }

        return Hash(builder.ToString());
    }

    private static string Bits(double value) =>
        BitConverter.DoubleToUInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);

    private static string Hash(string canonical)
    {
        ulong hash = FnvOffsetBasis;
        foreach (byte octet in Encoding.UTF8.GetBytes(canonical))
        {
            hash ^= octet;
            hash *= FnvPrime;
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }
}
