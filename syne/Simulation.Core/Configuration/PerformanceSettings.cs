// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (PerformanceSettings).

namespace Simulation.Core.Configuration;

public sealed class PerformanceSettings
{
    public bool ParallelPerception { get; set; } = true;
    public bool SpatialGrid { get; set; } = true;
    public bool DecisionCaching { get; set; } = true;
    public bool BatchCommunication { get; set; } = true;
}
