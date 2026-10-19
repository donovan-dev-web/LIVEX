// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (LifeSettings).

namespace Simulation.Core.Configuration;

public sealed class LifeSettings
{
    /// <summary>Mortalité active.</summary>
    public bool DeathEnabled { get; set; } = true;

    /// <summary>Energie fatale : une entité dont l'énergie est ≤ ce seuil meurt (défaut 0 — épuisement total).</summary>
    public double DeathEnergyThreshold { get; set; } = 0.0;

    /// <summary>Cause d'une mort par épuisement (événement <c>agent_died</c>).</summary>
    public string EnergyExhaustionCause { get; set; } = "energy_exhaustion";
}
