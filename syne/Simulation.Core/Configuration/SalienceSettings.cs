// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (SalienceSettings).

namespace Simulation.Core.Configuration;

public sealed class SalienceSettings
{
    /// <summary>Contrôle de saillance actif (D2).</summary>
    public bool Enabled { get; set; }

    /// <summary>Seuil de score déclenchant la délibération complète.</summary>
    public double ReconsiderThreshold { get; set; } = 1.0;

    /// <summary>Poids d'un franchissement de seuil de besoin depuis la dernière délibération.</summary>
    public double NeedThresholdWeight { get; set; } = 1.0;

    /// <summary>Poids d'une condition d'interruption remplie (généralisation).</summary>
    public double InterruptionConditionWeight { get; set; } = 2.0;

    /// <summary>Filet de sécurité : reconsidération forcée tous les N ticks sans saillance (0 = désactivé).</summary>
    public int ForcedReconsiderationTicks { get; set; } = 50;
}
