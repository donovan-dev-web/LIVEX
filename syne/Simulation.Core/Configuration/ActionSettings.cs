// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (ActionSettings).

namespace Simulation.Core.Configuration;

public sealed class ActionSettings
{
    /// <summary>
    /// Coût énergétique d'un déplacement — <b>calibration B1 </b> :
    /// 0,5 → 0,03. Le mouvement est l'action majoritaire (75 à 98 % des ticks en
    /// régime établi) : à 0,5 (puis 0,05 en D1) son coût dépasse le revenu
    /// amorti de Eat/Drink (≈ 0,057/tick) et l'énergie moyenne décroît sans borne
    /// jusqu'à l'épuisement (extinction lente).
    /// </summary>
    public double MoveEnergyCost { get; set; } = 0.03;

    /// <summary>Énergie regagnée par Repos — calibration B1 (hérité de D1).</summary>
    public double RestEnergyGain { get; set; } = 1.5;

    /// <summary>Fatigue récupérée par Repos — calibration B1 (hérité de D1).</summary>
    public double RestFatigueRecovery { get; set; } = 2.0;
    public DeliberationSettings Deliberation { get; set; } = new();
    public InterruptionSettings Interruption { get; set; } = new();

    /// <summary>Bibliothèque de plans candidats par objectif (D3, — désactivée par défaut, trajectoire de référence inchangée).</summary>
    public PlansSettings Plans { get; set; } = new();

    /// <summary>Inventaire des entités (D8, — désactivé par défaut).</summary>
    public InventorySettings Inventory { get; set; } = new();

    /// <summary>Contrôle de saillance (D2, — désactivé par défaut).</summary>
    public SalienceSettings Salience { get; set; } = new();

    /// <summary>Engagements communicationnels (D5, — désactivés par défaut).</summary>
    public CommitmentSettings Commitments { get; set; } = new();

    /// <summary>Catalogue déclaratif des actions (SYNE-040).</summary>
    public ActionCatalogSettings Catalog { get; set; } = new();
}
