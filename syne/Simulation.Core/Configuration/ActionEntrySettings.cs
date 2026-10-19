// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (ActionEntrySettings).

namespace Simulation.Core.Configuration;

public sealed class ActionEntrySettings
{
    /// <summary>Action de déplacement (pas déterministe + coût énergie du mouvement).</summary>
    public bool Movement { get; set; }

    /// <summary>Coût énergétique (défaut : <c>actions.moveEnergyCost</c>).</summary>
    public double? EnergyCost { get; set; }

    /// <summary>Énergie récupérée (défaut : <c>actions.restEnergyGain</c>).</summary>
    public double? EnergyRecovery { get; set; }

    /// <summary>Fatigue récupérée (défaut : <c>actions.restFatigueRecovery</c>).</summary>
    public double? FatigueRecovery { get; set; }

    /// <summary>Réduction de la faim (ex. Eat).</summary>
    public double? HungerRecovery { get; set; }

    /// <summary>Réduction de la soif (ex. Drink).</summary>
    public double? ThirstRecovery { get; set; }

    /// <summary>Rétablissement de la sûreté (0-1) — exutoire de <c>Flee</c> (réalignement, S-13).</summary>
    public double? SafetyRecovery { get; set; }

    /// <summary>Réduction du besoin social (0-1) — exutoire de <c>Socialize</c> (réalignement, B-08).</summary>
    public double? SocialRecovery { get; set; }

    /// <summary>Réduction du besoin de curiosité (0-1) — exutoire de <c>Explore</c> (réalignement, B-08).</summary>
    public double? CuriosityRecovery { get; set; }

    /// <summary>Réserve globale requise/consommée (ex. Eat → Food).</summary>
    public World.ResourceKind? Reserve { get; set; }

    /// <summary>Quantité consommée de la réserve à chaque exécution (défaut 1.0).</summary>
    public double? ReserveConsumption { get; set; }
}
