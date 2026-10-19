// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (GroupSettings).

namespace Simulation.Core.Configuration;

public sealed class GroupSettings
{
    /// <summary>Formation/dissolution des groupes active.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Fiducial : révision des groupes tous les N ticks (fréquence LOD déterministe).</summary>
    public int ReviewIntervalTicks { get; set; } = 10;

    /// <summary>Confiance réciproque minimale (min des deux sens) pour un lien social.</summary>
    public double TrustThreshold { get; set; } = 0.3;

    /// <summary>Taille minimale d'un groupe (en dessous : pas de formation / dissolution).</summary>
    public int MinGroupSize { get; set; } = 3;

    /// <summary>Bonus d'affinité par croyance partagée (même fait, confiances ≥ 0.5).</summary>
    public double SharedBeliefBonus { get; set; } = 0.1;

    /// <summary>Bonus d'affinité quand deux entités partagent leur objectif courant (buts partagés).</summary>
    public double GoalAlignmentBonus { get; set; } = 0.2;

    /// <summary>Quorum : une décision collective est adoptée quand ≥ cette fraction des membres partage la même intention.</summary>
    public double ConsensusThreshold { get; set; } = 0.5;

    // Arbitrage A0-3 (17/10/2026) : les réglages `proximityRequired` et
    // `proximityRadius` ont été SUPPRIMÉS — la liaison de cohésion n'a plus
    // aucune clause de proximité explicite (la décroissance de confiance
    // 0,9/tick sans interaction éteint de lui-même un lien distant), et la
    // distance des paires liées est instrumentée (max/moyenne par groupe,
    // exposée en snapshot) au lieu d'être filtrante.
}
