// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (ReproductionSettings).

namespace Simulation.Core.Configuration;

public sealed class ReproductionSettings
{
    /// <summary>Naissances par fusion actives.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Une tentative de fusion tous les N ticks (rareté V0.1 → V0.2).</summary>
    public int IntervalTicks { get; set; } = 100;

    /// <summary>Consentement : confiance réciproque minimale du couple pour fusionner.</summary>
    public double ConsentTrustThreshold { get; set; } = 0.6;

    /// <summary>Plafond de naissances par tick (0 = aucune, garde-fou de population).</summary>
    public int MaxBirthsPerTick { get; set; } = 1;

    /// <summary>Proximité physique : distance maximale entre les parents pour fusionner (SYNE-075).</summary>
    public double MergeRange { get; set; } = 40.0;

    /// <summary>Énergie minimale des deux parents pour fusionner (SYNE-075).</summary>
    public double MergeMinimumEnergy { get; set; } = 30.0;

    /// <summary>La ligne de vue entre les parents doit être dégagée (SYNE-075, SYNE-011).</summary>
    public bool RequireLineOfSight { get; set; } = true;

    /// <summary>Ni l'un ni l'autre des parents ne doit être en état critique (SYNE-075 : faim/énergie critiques).</summary>
    public bool RequireNoCriticalNeed { get; set; } = true;
}
