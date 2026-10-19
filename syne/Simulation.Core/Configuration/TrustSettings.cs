// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (TrustSettings).

namespace Simulation.Core.Configuration;

public sealed class TrustSettings
{
    /// <summary>Confiance d'une première rencontre.</summary>
    public double InitialTrust { get; set; } = 0.5;

    /// <summary>Décroissance de confiance par tick sans interaction (trustDecay).</summary>
    public double DecayFactorPerTick { get; set; } = 0.9;

    /// <summary>Bonus de confiance après une vérité constatée (plafond 1.0).</summary>
    public double TruthBonus { get; set; } = 0.05;

    /// <summary>Pénalité de confiance après un mensonge constaté (plancher 0.0).</summary>
    public double LiePenalty { get; set; } = 0.2;

    /// <summary>Bonus de confiance quand un engagement est tenu (D5, CommitmentStatus.Fulfilled).</summary>
    public double CommitmentBonus { get; set; } = 0.10;

    /// <summary>Pénalité de confiance quand un engagement est rompu (D5, CommitmentStatus.Broken) — distincte du mensonge factuel.</summary>
    public double CommitmentPenalty { get; set; } = 0.15;
}
