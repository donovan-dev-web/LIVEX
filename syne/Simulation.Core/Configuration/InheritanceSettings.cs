// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (InheritanceSettings).

namespace Simulation.Core.Configuration;

public sealed class InheritanceSettings
{
    /// <summary>
    /// Dominance dans [0, 1] : 0 = fusion égalitaire (moyenne arithmétique, V0.1) ;
    /// &gt; 0 = le trait du parent « exprimant » (écart au neutre le plus grand)
    /// pèse d'autant plus (dominance pleine à 1).
    /// </summary>
    public double Dominance { get; set; } = 0.0;

    /// <summary>Probabilité de mutation par trait (0 = aucun bruit, V0.1).</summary>
    public double MutationRate { get; set; } = 0.0;

    /// <summary>Amplitude d'une mutation (± <c>mutationMagnitude</c>, borné à [0, 2]).</summary>
    public double MutationMagnitude { get; set; } = 0.1;

    /// <summary>Seuil de salience d'un souvenir parental pour être transmis (ex-V0.1 <c>DefaultSalienceThreshold</c>).</summary>
    public double SalienceThreshold { get; set; } = 0.01;
}
