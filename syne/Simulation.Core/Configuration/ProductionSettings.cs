// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Section de configuration (ProductionSettings) — désir de production (SYNE-223).

namespace Simulation.Core.Configuration;

/// <summary>
/// Désir de <b>production</b> (SYNE-223, étape 4) : au lieu de ne viser que des
/// besoins physiologiques, l'agent poursuit un <b>objet à fabriquer</b> et laisse
/// la décomposition recette → sous-recettes → prélèvements au planificateur.
/// </summary>
/// <remarks>
/// <para>
/// La section est <b>désactivée par défaut</b> : la trajectoire du scénario de
/// référence reste bit-à-bit identique (même doctrine que
/// <c>agents.desires.instrumental</c>, <c>agents.planner</c> ou
/// <c>agents.knowledge</c>).
/// </para>
/// <para>
/// Un objectif est un <b>identifiant d'objet</b> du pack de contenu (par exemple
/// <c>stone_pickaxe</c> ou <c>chest</c>) — jamais une suite d'étapes. Le plan est
/// dérivé des recettes connues : c'est un but, pas un script.
/// </para>
/// <para>
/// Quand plusieurs objectifs sont déclarés, ils sont répartis
/// <b>déterministement</b> entre les esprits (identifiant d'entité modulo le
/// nombre d'objectifs), comme les chaînes de la campagne R1 : un run peut ainsi
/// exercer plusieurs chaînes sans qu'un agent reçoive un plan écrit.
/// </para>
/// </remarks>
public sealed class ProductionSettings
{
    /// <summary>Active le désir de production (faux par défaut : pin de référence).</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Objets visés, en identifiants de contenu. Vide tant que la section est
    /// désactivée ; le validateur refuse une section active sans objectif.
    /// </summary>
    public List<string> Goals { get; set; } = [];

    /// <summary>
    /// Bénéfice d'utilité de l'objectif, du même ordre que les désirs
    /// instrumentaux (plafond 60) : assez haut pour l'emporter sur l'exploration
    /// oisive, assez bas pour ne jamais primer une faim installée.
    /// </summary>
    public double Benefit { get; set; } = 45.0;

    /// <summary>
    /// Borne de nœuds du graphe de recettes déplié. Une recette cyclique ou un
    /// pack trop profond s'arrête proprement au lieu de faire exploser le plan.
    /// </summary>
    public int MaxPlanNodes { get; set; } = 128;

    /// <summary>
    /// Délai minimal avant de redemander une étape refusée par l'hôte (ticks).
    /// Un refus est un fait : la même commande n'est pas renvoyée aussitôt.
    /// </summary>
    public int RetryTicks { get; set; } = 30;
}
