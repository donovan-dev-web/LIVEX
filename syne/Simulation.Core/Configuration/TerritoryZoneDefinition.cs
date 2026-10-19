// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (TerritoryZoneDefinition).

namespace Simulation.Core.Configuration;

public sealed class TerritoryZoneDefinition
{
    public string Id { get; set; } = string.Empty;
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Radius { get; set; } = 20.0;
}
