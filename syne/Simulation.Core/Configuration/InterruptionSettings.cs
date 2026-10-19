// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (InterruptionSettings).

namespace Simulation.Core.Configuration;

public sealed class InterruptionSettings
{
    /// <summary>Interruptions actives.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Marge : interruption seulement si l'utilité du besoin critique surpasse l'action courante de cette marge (défaut 10).</summary>
    public double UtilityExcessMargin { get; set; } = 10.0;

    /// <summary>Seuil de faim critique (défaut 85).</summary>
    public double CriticalHunger { get; set; } = 85.0;

    /// <summary>Seuil d'énergie critique (défaut 10).</summary>
    public double CriticalEnergy { get; set; } = 10.0;
}
