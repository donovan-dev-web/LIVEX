// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (PathfindingSettings).

namespace Simulation.Core.Configuration;

public sealed class PathfindingSettings
{
    /// <summary>Cheminement actif (peut être neutralisé par l'option V0.1).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Taille de cellule de la grille rasterisée (unités monde).</summary>
    public double CellSize { get; set; } = 10.0;

    /// <summary>Plafond d'expansion d'A* (cellules) — repli « sur place » au-delà.</summary>
    public int MaxExpansionCells { get; set; } = 4096;

    /// <summary>Capacité du cache de chemins LRU (mémoïsation source → but).</summary>
    public int CacheCapacity { get; set; } = 256;
}
