// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (TerritorySettings).

namespace Simulation.Core.Configuration;

public sealed class TerritorySettings
{
    /// <summary>Suivi du territoire actif (appartenance + événements + snapshot <c>territories[]</c>).</summary>
    public bool Enabled { get; set; }

    /// <summary>Les zones (disques) du territoire, posées à l'init dans cet ordre (déterministe).</summary>
    public List<TerritoryZoneDefinition> Zones { get; set; } = [];
}
