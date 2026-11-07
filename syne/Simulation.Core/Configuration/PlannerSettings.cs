// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (PlannerSettings).

namespace Simulation.Core.Configuration;

/// <summary>
/// Planificateur intégré (SYNE-223) : l'intention choisie est <b>décomposée</b> en
/// une suite d'étapes (aller vers, puis consommer), mise en cache par clé
/// d'intention, et reconstruite quand une étape échoue.
/// Désactivé par défaut : la trajectoire de référence reste bit-à-bit identique.
/// </summary>
public sealed class PlannerSettings
{
    /// <summary>Planificateur actif (SYNE-223).</summary>
    public bool Enabled { get; set; }

    /// <summary>Nombre maximal d'étapes retenues (les suivantes sont tronquées).</summary>
    public int MaxPlanSteps { get; set; } = 6;

    /// <summary>Taille du cache de plans (0 = aucun cache, les plans sont reconstruits).</summary>
    public int CacheCapacity { get; set; } = 64;

    /// <summary>
    /// Une étape bloquée invalide le plan et force une replanification à la
    /// délibération suivante. Faux = l'échec est seulement compté (diagnostic).
    /// </summary>
    public bool ReplanOnFailure { get; set; } = true;
}
