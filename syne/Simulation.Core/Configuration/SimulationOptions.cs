// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Options de simulation : valeurs typées et défauts de toutes les sections de configuration.

namespace Simulation.Core.Configuration;

/// <summary>
/// Options de simulation. Structure parallèle au fichier config.json :
/// les propriétés absentes d'un fichier partiel conservent les défauts intégrés.
/// </summary>
public sealed class SimulationOptions
{
    public SimulationSettings Simulation { get; set; } = new();
    public AgentSettings Agents { get; set; } = new();
    public ResourceSettings Resources { get; set; } = new();
    public CommunicationSettings Communication { get; set; } = new();
    public WorldSettings World { get; set; } = new();
    public RandomSettings Random { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public GroupSettings Groups { get; set; } = new();
    public ReproductionSettings Reproduction { get; set; } = new();
}
