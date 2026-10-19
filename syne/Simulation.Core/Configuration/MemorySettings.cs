// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (MemorySettings).

namespace Simulation.Core.Configuration;

public sealed class MemorySettings
{
    public int MaxCapacity { get; set; } = 1000;
    public double RecallThreshold { get; set; } = 0.01;
    /// <summary>Décroissance des souvenirs d'observation.</summary>
    public double ObservationDecayRate { get; set; } = 0.01;
    public double EventDecayRate { get; set; } = 0.005;
    public double InteractionDecayRate { get; set; } = 0.002;
}
