// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (ResourceSpec).

namespace Simulation.Core.Configuration;

public sealed class ResourceSpec
{
    public int Initial { get; set; }
    public double RegenerationRate { get; set; }
    public int? DegradationTick { get; set; }
}
