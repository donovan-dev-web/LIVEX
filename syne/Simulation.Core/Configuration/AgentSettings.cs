// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (AgentSettings).

namespace Simulation.Core.Configuration;

public sealed class AgentSettings
{
    public int InitialCount { get; set; } = 100;
    public Dictionary<string, double> Traits { get; set; } = new()
    {
        ["bravery"] = 1.0,
        ["curiosity"] = 1.0,
        ["sociability"] = 1.0,
        ["greed"] = 1.0,
        ["pessimism"] = 1.0,
        ["aggressiveness"] = 1.0,
        ["strength"] = 1.0,
        ["speed"] = 1.0,
    };
    public NeedsSettings Needs { get; set; } = new();
    public PerceptionSettings Perception { get; set; } = new();
    public MemorySettings Memory { get; set; } = new();
    public BeliefSettings Beliefs { get; set; } = new();
    public TrustSettings Trust { get; set; } = new();
    public ActionSettings Actions { get; set; } = new();

    /// <summary>Héritage intergénérationnel (SYNE-063) : mécanismes fins configurables.</summary>
    public InheritanceSettings Inheritance { get; set; } = new();

    /// <summary>Cycle de vie — mortalité par épuisement (SYNE-074).</summary>
    public LifeSettings Life { get; set; } = new();

    /// <summary>Cheminement (SYNE-077) : A* déterministe sur grille rasterisée + cache LRU.</summary>
    public PathfindingSettings Pathfinding { get; set; } = new();
}
