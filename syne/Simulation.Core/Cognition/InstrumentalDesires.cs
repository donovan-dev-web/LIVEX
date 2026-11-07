// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Désirs instrumentaux et sécurité alimentaire (SYNE-222) : viser un STOCK de nourriture avant d'avoir faim.

using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Désir instrumental candidat (SYNE-222) : un <b>moyen</b> vers un besoin, avec
/// sa cible connue et le bénéfice qui en dérive (rempli de la réserve manquante,
/// et non d'un niveau de faim).
/// </summary>
/// <param name="Kind">
/// <see cref="DesireKind.SecureFood"/> (aller vers la source connue) ou
/// <see cref="DesireKind.Take"/> (la source est à portée : prendre).
/// </param>
/// <param name="Target">Source de nourriture visée (nulle si l'agent y est déjà).</param>
/// <param name="Benefit">Bénéfice d'utilité, proportionnel au manque de réserve.</param>
public readonly record struct InstrumentalDesire(DesireKind Kind, GoalTarget? Target, double Benefit);

/// <summary>
/// Sécurité alimentaire (SYNE-222) : le besoin de nourriture reste physiologique,
/// mais l'agent ne l'attend plus pour agir. Quand la faim <b>projetée</b> à
/// l'horizon d'anticipation franchira son seuil et que sa réserve est sous le
/// niveau visé, il forme un désir <b>instrumental</b> : constituer un stock.
/// </summary>
/// <remarks>
/// <para>
/// <b>Épistémique d'abord</b> : le moyen n'existe que si l'agent <b>sait</b> où
/// trouver de la nourriture (source crue, à portée ou non). Sinon rien n'est
/// proposé : la prospection reste le rôle d'<c>Explore</c>, et un stock qu'on ne
/// sait pas remplir ne serait qu'une intention creuse.
/// </para>
/// <para>
/// <b>Aucun vocabulaire nouveau</b> : aller vers la source est
/// <see cref="DesireKind.SecureFood"/> (poursuite), prendre est
/// <see cref="DesireKind.Take"/> (primitive d'inventaire existante), consommer
/// reste <see cref="DesireKind.Eat"/>. Le désir instrumental ne remplace jamais
/// le désir physiologique : il s'efface dès que la faim franchit son seuil.
/// </para>
/// <para>
/// <b>Déterminisme</b> : aucune décision aléatoire, uniquement des comparaisons
/// sur l'état des besoins, l'inventaire et la connaissance active — désactivé,
/// ce module ne produit aucun candidat et la trajectoire de référence est
/// bit-à-bit identique.
/// </para>
/// </remarks>
public static class InstrumentalDesires
{
    /// <summary>
    /// Bénéfice maximal d'un stock complet manquant. Calibré sur l'échelle des
    /// bénéfices existants (SeekFood/Eat : <c>faim × 0,6</c>, plafond 60) pour que
    /// la constitution du stock puisse l'emporter sur <c>Idle</c>/<c>Explore</c>
    /// sans jamais valoir une faim installée.
    /// </summary>
    public const double MaxBenefit = 60.0;

    /// <summary>
    /// Réserve suffisante (l'agent est en sécurité alimentaire : aucun désir
    /// instrumental). Sans inventaire, il n'y a rien à constituer → vrai.
    /// </summary>
    public static bool IsSecure(MindState mind, InstrumentalSettings settings)
    {
        ArgumentNullException.ThrowIfNull(mind);
        ArgumentNullException.ThrowIfNull(settings);
        return mind.Inventory is not { } inventory
            || inventory.Amount(ResourceKind.Food) >= settings.ReserveTarget;
    }

    /// <summary>
    /// Désir instrumental du tick, ou <c>null</c>. Conditions cumulatives :
    /// section active, inventaire présent, réserve sous le niveau visé, faim
    /// encore <b>sous</b> son seuil mais qui l'atteindra à l'horizon, et source de
    /// nourriture <b>connue</b>.
    /// </summary>
    public static InstrumentalDesire? Evaluate(
        MindState mind,
        CognitionKnowledge? knowledge,
        NeedsSettings needs,
        InstrumentalSettings settings)
    {
        ArgumentNullException.ThrowIfNull(mind);
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled || mind.Inventory is not { } inventory)
        {
            return null;
        }

        double stocked = inventory.Amount(ResourceKind.Food);
        if (stocked >= settings.ReserveTarget)
        {
            return null;
        }

        // Le désir physiologique garde la main dès que son seuil est franchi :
        // l'instrumental est une anticipation, pas un doublon de SeekFood.
        if (mind.Needs.Hunger >= needs.HungerTriggerThreshold)
        {
            return null;
        }

        double projected = mind.Needs.Hunger + (needs.HungerRate * settings.AnticipateTicks);
        if (projected < needs.HungerTriggerThreshold)
        {
            return null;
        }

        if (knowledge is not { } known || !known.KnowsFood)
        {
            return null;
        }

        double gap = (settings.ReserveTarget - stocked) / settings.ReserveTarget;
        double benefit = MaxBenefit * Math.Clamp(gap, 0.0, 1.0);

        // À portée : le moyen est l'acte (prendre). Plus loin : le moyen est la
        // poursuite de la source connue.
        return known.AtFoodSource
            ? new InstrumentalDesire(DesireKind.Take, null, benefit)
            : new InstrumentalDesire(DesireKind.SecureFood, known.FoodSource, benefit);
    }
}
