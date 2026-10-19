// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (SeasonDefinition).

namespace Simulation.Core.Configuration;

public sealed class SeasonDefinition
{
    /// <summary>Nom de la saison (clé JSON camelCase) : <c>spring</c>, <c>summer</c>, <c>autumn</c>, <c>winter</c>.</summary>
    public string Name { get; set; } = string.Empty;

    public double FoodFactor { get; set; } = 1.0;
    public double WaterFactor { get; set; } = 1.0;
    public double WoodFactor { get; set; } = 1.0;
    public double MineralFactor { get; set; } = 1.0;
}
