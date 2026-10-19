// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (SeasonFactors).

namespace Simulation.Core.Configuration;

public readonly record struct SeasonFactors(double Food, double Water, double Wood, double Mineral)
{
    /// <summary>Facteur applicable à une ressource donnée (repli 1.0 si inconnu).</summary>
    public double For(World.ResourceKind kind) => kind switch
    {
        World.ResourceKind.Food => Food,
        World.ResourceKind.Water => Water,
        World.ResourceKind.Wood => Wood,
        World.ResourceKind.Mineral => Mineral,
        _ => 1.0,
    };
}
