// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Exécuteur d'actions (SYNE-040) : applique atomiquement chaque tick l'action choisie par la délibération —
//        une action par entité par tick.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Navigation;
using Simulation.Core.Prng;
using Simulation.Core.World;

namespace Simulation.Core.Actions;

/// <summary>
/// Exécuteur d'actions (SYNE-040) : applique atomiquement chaque tick l'action
/// choisie par la délibération — une action par entité par tick. Les effets
/// (coût d'énergie, récupérations, consommation de réserve) proviennent du
/// catalogue déclaratif (SYNE-040). Le déplacement (SYNE-041) respecte les
/// obstacles et le coût énergie ; Eat/Drink mettent à jour les réserves (SYNE-042).
/// Depuis le jalon ph7b (SYNE-077), lorsqu'un obstacle barre le pas direct, le
/// déplacement emprunte le chemin A* déterministe (grille rasterisée, cache LRU,
/// repli « sur place »).
///
/// Déterminisme : aucune consommation du PRNG — cible pseudo-aléatoire stable
/// (hash SplitMix64 de (id, tick, désir)), itération par identifiant croissant,
/// pas de déplacement dans un obstacle.
/// </summary>
public sealed partial class ActionExecutor
{
    private readonly World.World _world;
    private readonly ActionCatalog _catalog;
    private readonly ResourceStocks _stocks;
    private readonly SimulationOptions _options;
    private readonly AStarPathfinder _pathfinder;
    private TickCommitBuffer? _commit;

    public ActionExecutor(World.World world, ActionCatalog catalog, ResourceStocks stocks, SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stocks);
        ArgumentNullException.ThrowIfNull(options);
        _world = world;
        _catalog = catalog;
        _stocks = stocks;
        _options = options;
        _pathfinder = new AStarPathfinder(world, options.Agents.Pathfinding);
    }

    public AStarPathfinder Pathfinder => _pathfinder;

    /// <summary>
    /// Ouvre le tampon de conséquences du tick (fenêtre causale fermée,
    /// ch. III) : les déplacements et les effets inter-entités sont différés à la
    /// fin de l'itération. <c>null</c> (hors pipeline, tests unitaires) = effets
    /// appliqués immédiatement, comportement historique.
    /// </summary>
    internal void BeginTick(TickCommitBuffer? buffer) => _commit = buffer;

    /// <summary>
    /// Exécute l'action <paramref name="kind"/> pour l'entité : applique les effets
    /// du catalogue et renvoie le résultat. Les actions à réserve sont
    /// <b>atomiques</b> : la consommation est tentée <i>avant</i> tout effet et
    /// échoue en <see cref="ActionOutcome.Blocked"/> (aucun effet, aucun mouvement)
    /// si la réserve ne couvre pas exactement la quantité configurée. Sans cela,
    /// un agent dont la réserve est inférieure à la consommation obtiendrait le
    /// bénéfice complet (faim/soif) pour une denrée jamais payée.
    /// <para>
    /// Depuis engineVersion 0.14.0, les primitives D7 (Take/Give/Trade/Attack/Defend)
    /// sont traitées avant le chemin déclaratif : elles opèrent sur l'inventaire (D8)
    /// et sur les entités cibles, avec la même atomicité (échec = aucun effet).
    /// </para>
    /// </summary>
    /// <param name="entity">Entité exécutante.</param>
    /// <param name="mind">État cognitif de l'entité exécutante.</param>
    /// <param name="kind">Action choisie par la délibération.</param>
    /// <param name="currentTick">Tick courant.</param>
    /// <param name="mindLookup">
    /// Accès à l'état cognitif d'une autre entité (fourni par le pipeline — les
    /// primitives Give/Trade/Attack ciblent un pair). <c>null</c> : les primitives
    /// inter-entités échouent en Blocked.
    /// </param>
    public ActionResult Execute(
        Entity entity,
        MindState mind,
        DesireKind kind,
        ulong currentTick,
        Func<ulong, MindState?>? mindLookup = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(mind);

        // Primitives D7 : manipulation d'inventaire
        // et interaction inter-entités. Renvoie null si l'action n'est pas une
        // primitive (elle emprunte alors le chemin déclaratif historique).
        ActionResult? primitive = ExecutePrimitive(entity, mind, kind, currentTick, mindLookup);
        if (primitive is not null)
        {
            return primitive;
        }

        ActionDefinition definition = _catalog[kind];

        // 1. Prélèvement atomique de la ressource — avant tout effet observable.
        //    Modèle localisé (Phase 0) : la source est le GISEMENT où l'agent se
        //    tient (cible crue atteinte), plus une réserve mondiale.
        ResourceKind? reserveConsumed = null;
        double consumed = 0.0;
        string? sourceNodeId = null;
        if (definition.RequiresReserve is { } reserve)
        {
            consumed = definition.ReserveConsumption;
            if (_options.Resources.Localized)
            {
                ResourceNode? node = _world.NearestResourceNode(entity.Position, reserve, _options.Resources.InteractionRadius);
                if (node is null)
                {
                    return ActionResult.Blocked(kind, $"aucun gisement {reserve} à portée (cible connue non atteinte)");
                }

                if (!node.TryConsume(consumed))
                {
                    return ActionResult.Blocked(kind, $"gisement {node.Id} épuisé ({node.Amount:0.###} < {consumed:0.###})");
                }

                sourceNodeId = node.Id;
            }
            else if (!_stocks.TryConsume(reserve, consumed))
            {
                return ActionResult.Blocked(
                    kind,
                    _stocks.IsEmpty(reserve)
                        ? $"réserve {reserve} vide"
                        : $"réserve {reserve} insuffisante ({_stocks.Stock(reserve):0.###} < {consumed:0.###})");
            }

            reserveConsumed = reserve;
        }

        // 2. Effets applicatifs.
        double energyDelta = 0.0;
        double hungerDelta = 0.0;
        double thirstDelta = 0.0;
        double fatigueDelta = 0.0;
        bool arrived = ArrivedAtTarget(entity, mind, kind);

        if (definition.Movement)
        {
            MoveTowardDestination(entity, mind, kind, currentTick);
            energyDelta = -definition.EnergyCost;
            if (energyDelta != 0.0)
            {
                mind.Needs.ExertEnergy(definition.EnergyCost);
            }
        }
        else if (definition.EnergyCost > 0.0)
        {
            energyDelta = -definition.EnergyCost;
            mind.Needs.ExertEnergy(definition.EnergyCost);
        }

        if (definition.EnergyRecovery > 0.0)
        {
            energyDelta += definition.EnergyRecovery;
            mind.Needs.RecoverEnergy(definition.EnergyRecovery);
        }

        if (definition.FatigueRecovery > 0.0)
        {
            fatigueDelta = -definition.FatigueRecovery;
            mind.Needs.RecoverFatigue(definition.FatigueRecovery);
        }

        if (definition.HungerRecovery > 0.0)
        {
            hungerDelta = -definition.HungerRecovery;
            mind.Needs.RecoverHunger(definition.HungerRecovery);
        }

        if (definition.ThirstRecovery > 0.0)
        {
            thirstDelta = -definition.ThirstRecovery;
            mind.Needs.RecoverThirst(definition.ThirstRecovery);
        }

        // Exutoires de besoin (S-13 / B-08) : la satisfaction d'un besoin social,
        // de curiosité ou de sûreté n'est accordée qu'à l'<b>arrivée</b> à la cible
        // connue (un déplacement n'est pas une satisfaction). Sans cible, l'effet
        // s'applique immédiatement (action non dirigée).
        if (arrived)
        {
            if (definition.SafetyRecovery > 0.0)
            {
                mind.Needs.RecoverSafety(definition.SafetyRecovery);
            }

            if (definition.SocialRecovery > 0.0)
            {
                mind.Needs.RecoverSocial(definition.SocialRecovery);
            }

            if (definition.CuriosityRecovery > 0.0)
            {
                mind.Needs.RecoverCuriosity(definition.CuriosityRecovery);
            }
        }

        return new ActionResult(
            kind,
            ActionOutcome.Executed,
            null,
            energyDelta,
            hungerDelta,
            thirstDelta,
            fatigueDelta,
            reserveConsumed,
            consumed,
            sourceNodeId);
    }
}
