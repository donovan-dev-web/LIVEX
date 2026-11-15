// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Désir de production (SYNE-223) : viser un OBJET à fabriquer, pas une suite d'étapes.

using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>Désir de production candidat : l'objet visé et son bénéfice d'utilité.</summary>
public readonly record struct ProductionDesire(string Goal, double Benefit);

/// <summary>
/// Production (SYNE-223, étape 4) : un désir <b>instrumental</b> qui vise un objet
/// du pack de contenu (« je veux une pioche en pierre »). Ce que l'agent ne reçoit
/// <b>pas</b>, c'est un plan : la décomposition en recettes, sous-recettes et
/// prélèvements est dérivée à l'exécution du graphe de recettes et de
/// l'inventaire observé.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un but, pas un script.</b> La configuration ne dit que <i>quoi</i> ; elle ne
/// dit jamais <i>comment</i>. Un agent qui reçoit <c>stone_pickaxe</c> doit
/// découvrir qu'il lui faut un manche, une tête, un établi et des bûches.
/// </para>
/// <para>
/// <b>Déterminisme.</b> Plusieurs objectifs sont répartis par
/// <c>entityId % goals.Count</c> : même population, mêmes affectations, aucun
/// tirage. Un objectif déjà porté n'est plus poursuivi — le désir s'éteint de
/// lui-même, sans qu'une étape « terminer » soit écrite quelque part.
/// </para>
/// <para>
/// <b>Désactivé par défaut.</b> Aucun candidat n'est produit sans la section
/// <c>agents.desires.production</c> : la trajectoire de référence reste
/// bit-à-bit identique.
/// </para>
/// </remarks>
public static class ProductionDesires
{
    /// <summary>Objectif poursuivi par une entité, ou <c>null</c> si le désir est éteint.</summary>
    public static ProductionDesire? Evaluate(MindState mind, ulong entityId, ProductionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(mind);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled || settings.Goals.Count == 0)
        {
            return null;
        }

        int index = (int)(entityId % (ulong)settings.Goals.Count);
        string goal = settings.Goals[index];
        if (string.IsNullOrWhiteSpace(goal) || mind.CarriedCount(goal) > 0)
        {
            return null;
        }

        return new ProductionDesire(goal, settings.Benefit);
    }

    /// <summary>
    /// Cible canonique d'un objectif de production : un <b>objet</b>, pas un lieu.
    /// La position est celle de l'agent (le plan décidera des déplacements) et
    /// l'identifiant est 0 — aucune entité du monde ne porte ce but.
    /// </summary>
    public static GoalTarget Target(string goal, Position self)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        return new GoalTarget("item", 0, goal, self, 0, 1.0);
    }
}
