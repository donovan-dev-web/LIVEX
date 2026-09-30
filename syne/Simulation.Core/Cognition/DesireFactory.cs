using Simulation.Core.Actions;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Les 6 désirs V0.1 (DATA_MODEL.md §7) + Idle, puis les actions terminales de
/// besoin Eat/Drink (SYNE-042, décision n°4). Chaque désir découle d'un besoin
/// non satisfait (objectif = état recherché, COGNITIVE_ARCHITECTURE.md §5).
/// <list type="bullet">
///   <item>Les valeurs existantes (0-8) ne changent jamais : l'ordre du catalogue
///    est stable pour le départage déterministe (décision n°22, DETERMINISM.md §5).</item>
///   <item>Eat/Drink (7-8) : actions terminales immédiates, non persistantes
///    (exécutées tant que le besoin est déclenché, jamais en holdover).</item>
///   <item>Take…Defend (9-13, jalon primitives D7/D8 — engineVersion 0.14.0) :
///    primitives atomiques de manipulation de l'inventaire et d'interaction,
///    ajoutées EN QUEUE (jamais réordonnées). Elles ne sont générées que via la
///    bibliothèque de plans (agents.actions.plans.enabled, D3) et ne font rien
///    hors inventaire actif (agents.inventory.enabled, D8).</item>
/// </list>
/// </summary>
public enum DesireKind
{
    Idle = 0,
    SeekFood = 1,
    SeekWater = 2,
    Rest = 3,
    Flee = 4,
    Socialize = 5,
    Explore = 6,
    Eat = 7,
    Drink = 8,

    /// <summary>Prendre (D7) : transfert réserve mondiale → inventaire, sous capacité.</summary>
    Take = 9,

    /// <summary>Donner (D7) : transfert inventaire → autre entité, sans contrepartie.</summary>
    Give = 10,

    /// <summary>Échanger (D7) : transfert bidirectionnel conditionné (1 Food ↔ 1 Water V0.1).</summary>
    Trade = 11,

    /// <summary>Attaquer (D7/D3 temps 1) : inflige un dégât d'énergie (§3.15.4 : −5 × agressivité).</summary>
    Attack = 12,

    /// <summary>Se défendre (D7) : réduit de moitié le dégât d'une attaque subie.</summary>
    Defend = 13,
}

/// <summary>État recherché (objectif) né d'un besoin non satisfait (COGNITIVE_ARCHITECTURE.md §5).</summary>
public readonly record struct Goal(DesireKind Kind, ulong BornTick)
{
    public ulong Age(ulong currentTick) => currentTick >= BornTick ? currentTick - BornTick : 0;
}

/// <summary>
/// Générateur d'objectifs : un désir est créé quand son besoin passe son seuil
/// (SYNE-042, décision n°4 : déclenchement dès « ≥ 50 ») et qu'aucun objectif du
/// même type n'est actif. Pour faim/soif, le désir engendré est l'action terminale
/// <see cref="DesireKind.Eat"/>/<see cref="DesireKind.Drink"/> si la réserve globale
/// correspondante est disponible, sinon le <see cref="DesireKind.SeekFood"/>/
/// <see cref="DesireKind.SeekWater"/> de poursuite (catalogue, SYNE-040) — le
/// déclencheur est donc viable par construction.
/// </summary>
public static class DesireFactory
{
    public static IReadOnlyList<Goal> Generate(
        BodyNeeds needs,
        ulong tick,
        IEnumerable<DesireKind> activeKinds,
        ActionCatalog catalog,
        ResourceStocks stocks)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stocks);

        var goals = new List<Goal>();
        var active = new HashSet<DesireKind>(activeKinds);

        foreach (DesireKind kind in Enum.GetValues<DesireKind>())
        {
            // Les actions terminales Eat/Drink (SYNE-042) ne sont jamais générées
            // directement : elles sont résolues depuis SeekFood/SeekWater via
            // ResolveTerminal (sinon doublons d'objectifs). Les primitives D7
            // (Take/Give/Trade/Attack/Defend) ne sont jamais générées ici non plus :
            // elles proviennent uniquement de la bibliothèque de plans (D3,
            // PlanLibrary.GenerateCandidates) quand elle est activée.
            if (kind is DesireKind.Idle
                or DesireKind.Eat or DesireKind.Drink
                or DesireKind.Take or DesireKind.Give or DesireKind.Trade
                or DesireKind.Attack or DesireKind.Defend)
            {
                continue;
            }

            DesireKind candidate = ResolveTerminal(kind, catalog, stocks);
            if (!active.Contains(candidate) && needs.IsTriggered(kind))
            {
                goals.Add(new Goal(candidate, tick));
            }
        }

        return goals;
    }

    /// <summary>
    /// Faim/soif : préfère l'action terminale Eat/Drink si la réserve est
    /// disponible, sinon revient à la poursuite SeekFood/SeekWater.
    /// </summary>
    private static DesireKind ResolveTerminal(DesireKind kind, ActionCatalog catalog, ResourceStocks stocks)
    {
        if (kind == DesireKind.SeekFood)
        {
            return catalog.IsViable(DesireKind.Eat, stocks) ? DesireKind.Eat : DesireKind.SeekFood;
        }

        if (kind == DesireKind.SeekWater)
        {
            return catalog.IsViable(DesireKind.Drink, stocks) ? DesireKind.Drink : DesireKind.SeekWater;
        }

        return kind;
    }
}