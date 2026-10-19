// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (InventorySettings).

namespace Simulation.Core.Configuration;

public sealed class InventorySettings
{
    /// <summary>Inventaire actif (D8) — les primitives Take/Give/Trade opèrent dessus.</summary>
    public bool Enabled { get; set; }

    /// <summary>Capacité de poids par entité (capacitePoids — défaut posé, calibrable).</summary>
    public double CapacityWeight { get; set; } = 20.0;

    /// <summary>Quantité par primitive Prendre (transfert réserve → inventaire).</summary>
    public double TakeAmount { get; set; } = 5.0;

    /// <summary>Quantité par primitive Donner (transfert inventaire → cible, don sans contrepartie).</summary>
    public double GiveAmount { get; set; } = 1.0;

    /// <summary>Quantité par primitive Échanger (1 contre 1, Food ↔ Water V0.1).</summary>
    public double TradeAmount { get; set; } = 1.0;
}
