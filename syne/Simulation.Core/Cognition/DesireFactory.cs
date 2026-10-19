// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Les 6 désirs V0.1 + Idle, puis les actions terminales de besoin Eat/Drink (SYNE-042).

using Simulation.Core.Actions;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Les 6 désirs V0.1 + Idle, puis les actions terminales de
/// besoin Eat/Drink (SYNE-042). Chaque désir découle d'un besoin
/// non satisfait (objectif = état recherché).
/// <list type="bullet">
///   <item>Les valeurs existantes (0-8) ne changent jamais : l'ordre du catalogue
/// est stable pour le départage déterministe.</item>
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

    /// <summary>Attaquer (D7/D3 temps 1) : inflige un dégât d'énergie (−5 × agressivité).</summary>
    Attack = 12,

    /// <summary>Se défendre (D7) : réduit de moitié le dégât d'une attaque subie.</summary>
    Defend = 13,
}

/// <summary>
/// État recherché (objectif) né d'un besoin non satisfait.
/// <para>
/// Depuis le réalignement (B-02), un objectif peut porter une <b>cible concrète</b>
/// (<see cref="GoalTarget"/>) : « de l'eau en P, connue à t ». Le but devient
/// falsifiable — arrivé en P sans ressource, l'objectif n'est pas accompli et la
/// cible est révisée ou abandonnée. Un objectif sans cible reste possible
/// (<c>Idle</c>, action purement physiologique), mais un objectif de recherche
/// sans cible connue n'est plus proposé : dans ce cas l'agent <b>explore</b>.
/// </para>
/// </summary>
public readonly record struct Goal(DesireKind Kind, ulong BornTick, GoalTarget? Target = null)
{
    public ulong Age(ulong currentTick) => currentTick >= BornTick ? currentTick - BornTick : 0;

    /// <summary>Vrai si l'objectif porte une cible du monde connue par l'agent.</summary>
    public bool HasTarget => Target is not null;
}

/// <summary>
/// Générateur d'objectifs : un désir est créé quand son besoin passe son seuil
/// (SYNE-042 : déclenchement dès « ≥ 50 ») et qu'aucun objectif du même type
/// n'est actif.
///
/// <para>
/// <b>Deux modèles</b> (arbitrage de la Phase 0) :
/// <list type="bullet">
/// <item><b>Localisé</b> (<c>knowledge</c> fourni) : la faisabilité et
/// la cible dépendent de ce que l'agent <b>sait</b>. Sur un gisement (cible à
/// portée d'interaction) → <c>Eat</c>/<c>Drink</c> ; gisement connu plus loin →
/// <c>SeekFood</c>/<c>SeekWater</c> <i>avec cible</i> ; aucun gisement connu →
/// <c>Explore</c> (recherche d'un repère) et non un déplacement aveugle.</item>
/// <item><b>Global</b> (<c>knowledge</c> nul, modèle historique) :
/// <c>Eat</c>/<c>Drink</c> dès que la réserve mondiale est viable, sinon
/// <c>SeekFood</c>/<c>SeekWater</c> — la localité de la décision n'est alors pas
/// revendiquée.</item>
/// </list>
/// </para>
/// </summary>
public static class DesireFactory
{
    public static IReadOnlyList<Goal> Generate(
        BodyNeeds needs,
        ulong tick,
        IEnumerable<DesireKind> activeKinds,
        ActionCatalog catalog,
        ResourceStocks stocks,
        CognitionKnowledge? knowledge = null)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stocks);

        var goals = new List<Goal>();
        var active = new HashSet<DesireKind>(activeKinds);
        var generated = new HashSet<DesireKind>();

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

            if (!needs.IsTriggered(kind))
            {
                continue;
            }

            DesireKind candidate = ResolveTerminal(kind, catalog, stocks, knowledge);
            if (!active.Contains(candidate) && generated.Add(candidate))
            {
                goals.Add(new Goal(candidate, tick, knowledge?.TargetFor(candidate)));
            }
        }

        return goals;
    }

    /// <summary>
    /// Faim/soif : <b>localisé</b>, l'action dépend de la connaissance — sur le
    /// gisement → action terminale ; gisement connu → poursuite ciblée ; rien de
    /// connu → exploration. <b>Global</b>, la viabilité de la réserve mondiale
    /// décide (modèle historique).
    /// </summary>
    private static DesireKind ResolveTerminal(
        DesireKind kind,
        ActionCatalog catalog,
        ResourceStocks stocks,
        CognitionKnowledge? knowledge)
    {
        if (kind is not (DesireKind.SeekFood or DesireKind.SeekWater))
        {
            return kind;
        }

        if (knowledge is { } known)
        {
            bool food = kind == DesireKind.SeekFood;
            bool atSource = food ? known.AtFoodSource : known.AtWaterSource;
            if (atSource)
            {
                return food ? DesireKind.Eat : DesireKind.Drink;
            }

            GoalTarget? target = food ? known.FoodSource : known.WaterSource;
            return target is not null
                ? kind
                : DesireKind.Explore;
        }

        return kind == DesireKind.SeekFood
            ? (catalog.IsViable(DesireKind.Eat, stocks) ? DesireKind.Eat : DesireKind.SeekFood)
            : (catalog.IsViable(DesireKind.Drink, stocks) ? DesireKind.Drink : DesireKind.SeekWater);
    }
}
