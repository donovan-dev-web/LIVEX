// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : compteurs de recherche de chemin exposés au profil opt-in (PRISM-104).

using Prism.Core.Navigation;

namespace Prism.Host.Ticking;

/// <summary>
/// Phase qui tient les compteurs de recherche de chemin d'un tick. Le host les lit pour le
/// profilage textuel (<c>PRISM_TICK_PROFILE=1</c>) et le relevé JSON
/// (<c>PRISM_BENCHMARK_JSON</c>), sans connaître le système de cycle de vie ni l'ordre de ses
/// phases internes.
/// </summary>
public interface IPathfindingMetricsSource
{
    /// <summary>Compteurs cumulés depuis la création du monde ; le host en prend le delta du tick.</summary>
    PathfindingMetricsSnapshot PathfindingTotals { get; }
}
