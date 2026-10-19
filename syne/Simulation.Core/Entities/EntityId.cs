// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Identifiant d'entité (ulong séquentiel déterministe).

namespace Simulation.Core.Entities;

/// <summary>
/// Identifiant d'entité (ulong séquentiel déterministe).
/// </summary>
public readonly record struct EntityId(ulong Value)
{
    public override string ToString() => Value.ToString("D5");
}
