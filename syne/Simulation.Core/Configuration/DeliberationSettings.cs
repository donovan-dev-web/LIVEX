// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (DeliberationSettings).

namespace Simulation.Core.Configuration;

public sealed class DeliberationSettings
{
    /// <summary>Fréquence de délibération : ticks entre deux délibérations (défaut 10).</summary>
    public int IntervalTicks { get; set; } = 10;

    /// <summary>Bonus d'alignement avec l'objectif courant (×1.2).</summary>
    public double AlignBonus { get; set; } = 1.2;

    // Arbitrage A0-3 (17/10/2026) : `collectiveAlignBonus` SUPPRIMÉ avec la
    // propagation SYNE-076 — l'objectif collectif d'un groupe ne modifie plus
    // aucun score individuel ; il est exposé (état du groupe, événement
    // group_decision, snapshot) et n'entre dans la délibération d'un membre
    // qu'en tant que candidat individuel tracé s'il le choisit.

    /// <summary>Marge anti-oscillation (hystérésis) : passer à une nouvelle action seulement si elle dépasse l'action courante de cette marge (défaut 0.05).</summary>
    public double ActionSwitchMargin { get; set; } = 0.05;

    /// <summary>Marge de conflit de priorités : candidats à moins de cette marge du maximum → résolution probabiliste confiance×force.</summary>
    public double ConflictTieMargin { get; set; } = 0.5;
}
