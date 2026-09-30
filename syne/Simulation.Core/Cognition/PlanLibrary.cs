using Simulation.Core.Actions;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Bibliothèque de plans candidats par objectif (ADR « Means-End Reasoning », D3,
/// engineVersion 0.14.0) : transforme le mapping historique « besoin → objectif →
/// action quasi unique » en « besoin → objectif → N plans candidats », chaque plan
/// étant scoré par la fonction d'utilité existante (§3.14, **inchangée**) — elle
/// reçoit simplement plus de candidats en entrée.
///
/// <para>
/// Périmètre du temps 2 de la décision (après la réintroduction d'`Attack`, temps 1,
/// portée par le catalogue D7) : pour un objectif de faim/soif, l'entité détentrice
/// d'un stock pertinent peut générer <see cref="DesireKind.Take"/> (constitution de
/// stock au-delà du besoin immédiat — modulé par le trait <c>greed</c>, cf. ADR
/// « Inventaire ») ; l'entité en manque peut générer <see cref="DesireKind.Give"/>
/// ou <see cref="DesireKind.Trade"/> vers un pair connu et de confiance. `Steal`
/// n'est PAS implémenté (l'ADR le conditionne à un arbitrage avec
/// <c>docs/ETHICS_AND_SCOPE.md</c>) ; `Buy` est couvert par Échanger (1 contre 1).
/// Les candidats ne sont générés que si <c>agents.actions.plans.enabled</c> et
/// <c>agents.inventory.enabled</c> sont actifs, et uniquement quand l'objectif est
/// réellement prioritaire (point 1 « à trancher » de l'ADR : coût de génération).
/// </para>
/// </summary>
public static class PlanLibrary
{
    /// <summary>
    /// Génère les plans candidats supplémentaires pour les objectifs actifs
    /// (candidats de besoin déjà résolus compris). Déterministe : aucun tirage
    /// PRNG, ordre d'énumération par identifiant croissant côté appelant.
    /// </summary>
    public static IReadOnlyList<Goal> GenerateCandidates(
        IReadOnlyList<Goal> activeGoals,
        MindState mind,
        Entity entity,
        ActionCatalog catalog,
        ResourceStocks stocks,
        InventorySettings inventorySettings,
        PlansSettings plansSettings,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(activeGoals);
        ArgumentNullException.ThrowIfNull(mind);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stocks);
        ArgumentNullException.ThrowIfNull(inventorySettings);
        ArgumentNullException.ThrowIfNull(plansSettings);

        if (!plansSettings.Enabled || !inventorySettings.Enabled || mind.Inventory is null)
        {
            return [];
        }

        var candidates = new List<Goal>();
        foreach (Goal goal in activeGoals)
        {
            bool foodGoal = goal.Kind is DesireKind.SeekFood or DesireKind.Eat;
            bool waterGoal = goal.Kind is DesireKind.SeekWater or DesireKind.Drink;
            if (!foodGoal && !waterGoal)
            {
                continue;
            }

            // Constitution de stock : l'entité avare (greed) prend au-delà de
            // son besoin immédiat tant que sa capacité le permet (ADR Inventaire :
            // « condition nécessaire à l'apparition spontanée d'échanges »).
            if (IsViable(catalog, DesireKind.Take, stocks)
                && entity.Traits["greed"] >= 1.0
                && mind.Inventory.FreeWeight >= inventorySettings.TakeAmount)
            {
                candidates.Add(new Goal(DesireKind.Take, tick));
            }

            // Aide/échange : l'entité en manque peut solliciter un pair connu
            // (confiance > 0.5 — seuil minimal de relation établie) via un échange
            // (recette fixe courte, pas de planificateur générique).
            if (HasTrustedPeer(mind))
            {
                candidates.Add(new Goal(DesireKind.Trade, tick));
            }
        }

        return candidates;
    }

    private static bool IsViable(ActionCatalog catalog, DesireKind kind, ResourceStocks stocks)
    {
        ActionDefinition definition = catalog[kind];
        return definition.RequiresReserve is not { } reserve || !stocks.IsEmpty(reserve);
    }

    private static bool HasTrustedPeer(MindState mind)
    {
        foreach ((ulong _, double trust) in mind.Trust.Snapshot())
        {
            if (trust > 0.5)
            {
                return true;
            }
        }

        return false;
    }
}
