// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (SpatialMemorySettings) — mémoire par chunk.

namespace Simulation.Core.Configuration;

/// <summary>
/// Mémoire spatiale par chunk (SYNE-221), sous <c>agents.beliefs.spatialMemory</c>.
/// </summary>
/// <remarks>
/// Une entité ne conserve qu'un nombre borné de chunks crus, et leur confiance
/// décroît avec le temps : sans ces bornes, une longue exploration ferait croître
/// la mémoire indéfiniment (coût par tick et taille de sauvegarde).
/// </remarks>
public sealed class SpatialMemorySettings
{
    /// <summary>Nombre maximal de chunks crus par entité (au-delà, éviction du moins cru).</summary>
    public int MaxChunksPerAgent { get; set; } = 512;

    /// <summary>Décroissance de confiance par tick d'un chunk non observé (dans (0, 1]).</summary>
    public double DecayPerTick { get; set; } = 0.999;

    /// <summary>Confiance sous laquelle un chunk est oublié.</summary>
    public double ForgetThreshold { get; set; } = 0.05;
}
