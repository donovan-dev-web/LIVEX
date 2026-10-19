// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (BeliefSettings).

namespace Simulation.Core.Configuration;

public sealed class BeliefSettings
{
    /// <summary>Force de révision : <c>belief = belief + (signal − belief) × strength</c>.</summary>
    public double UpdateStrength { get; set; } = 0.3;
    /// <summary>Plafond de variation de confiance par snap (« plafond par snap »).</summary>
    public double MaxChangePerSnap { get; set; } = 0.5;
    public double AlignBonus { get; set; } = 0.2;
    public double ConflictPenalty { get; set; } = 0.1;
    public ulong ExpiryTicks { get; set; } = 100;
    public double ExpiredCap { get; set; } = 0.4;
    public double TimeDecayPerTick { get; set; } = 0.999;
}
