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
public sealed class ActionExecutor
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

    /// <summary>
    /// Vrai si l'action peut appliquer sa satisfaction : action sans cible (effet
    /// immédiat) ou cible connue atteinte à portée d'interaction.
    /// </summary>
    private bool ArrivedAtTarget(Entity entity, MindState mind, DesireKind kind)
    {
        if (mind.Intention is not { } goal || goal.Kind != kind || goal.Target is not { } target)
        {
            return true;
        }

        return entity.Position.DistanceTo(target.Position) <= _options.Resources.InteractionRadius;
    }

    /// <summary>
    /// Exécute une primitive D7 si <paramref name="kind"/> en est une, sinon null.
    /// <list type="bullet">
    /// <item><b>Take</b> (D7+D8) : prélève <c>agents.inventory.takeAmount</c> de la
    /// réserve Food (Water si faim faible) vers l'inventaire — échoue si l'inventaire
    /// est inactif, si la réserve est vide ou si la capacité ne couvre pas la quantité.</item>
    /// <item><b>Give</b> (D7) : transfert <c>giveAmount</c> vers l'entité vivante la
    /// plus proche (même sémantique de cible que Socialize) — échoue sans cible,
    /// sans stock ou si la capacité du destinataire est insuffisante.</item>
    /// <item><b>Trade</b> (D7+D8) : échange fixe 1 Food ↔ 1 Water (× takeAmount) avec
    /// l'entité la plus proche — échoue si l'un des deux inventaires ne peut pas payer.
    /// La confiance des deux parties est renforcée (interaction positive réciproque).</item>
    /// <item><b>Attack</b> (D7/D3 temps 1) : inflige <c>5 × aggressivité</c> de dégât
    /// d'énergie à la cible la plus proche — attaque neutre, jamais portée
    /// par un besoin (doctrine de progressivité), réduite de moitié si la cible se
    /// défend ce tick.</item>
    /// <item><b>Defend</b> (D7) : pose le drapeau de défense du tick (réduction de
    /// moitié d'une attaque subie).</item>
    /// </list>
    /// Toutes ces primitives consomment l'énergie du catalogue et échouent en
    /// <see cref="ActionOutcome.Blocked"/> sans aucun effet partiel.
    /// </summary>
    private ActionResult? ExecutePrimitive(
        Entity entity,
        MindState mind,
        DesireKind kind,
        ulong currentTick,
        Func<ulong, MindState?>? mindLookup)
    {
        switch (kind)
        {
            case DesireKind.Take:
                return ExecuteTake(entity, mind);
            case DesireKind.Give:
                return ExecuteGive(entity, mind, mindLookup);
            case DesireKind.Trade:
                return ExecuteTrade(entity, mind, mindLookup);
            case DesireKind.Attack:
                return ExecuteAttack(entity, mind, currentTick, mindLookup);
            case DesireKind.Defend:
                return ExecuteDefend(entity, mind);
            default:
                return null;
        }
    }

    private ActionResult ExecuteTake(Entity entity, MindState mind)
    {
        if (mind.Inventory is not { } inventory)
        {
            return ActionResult.Blocked(DesireKind.Take, "inventaire inactif (agents.inventory.enabled = false)");
        }

        // Réserve ciblée : Food par défaut, Water si la soif domine la faim
        // (recette fixe courte — pas de planificateur).
        ResourceKind reserve = mind.Needs.Hunger >= mind.Needs.Thirst ? ResourceKind.Food : ResourceKind.Water;
        double amount = Math.Min(_options.Agents.Actions.Inventory.TakeAmount, inventory.FreeWeight);
        double available = AvailableAtHand(entity, reserve);
        if (amount <= 0.0 || available <= 0.0)
        {
            return ActionResult.Blocked(
                DesireKind.Take,
                available <= 0.0
                    ? (_options.Resources.Localized ? $"aucun gisement {reserve} à portée" : $"réserve {reserve} vide")
                    : "capacité d'inventaire saturée");
        }

        // Atomicité : prélèvement source d'abord (échec = aucun effet), stockage ensuite.
        if (available < amount)
        {
            return ActionResult.Blocked(DesireKind.Take, $"source {reserve} insuffisante ({available:0.###} < {amount:0.###})");
        }

        ConsumeAtHand(entity, reserve, amount);

        double? stored = inventory.TryTake(reserve, amount);
        if (stored is null)
        {
            // Invariant : amount est plafonné par FreeWeight avant le prélèvement
            // monde, donc TryTake ne peut pas échouer ici. Défensive : signaler
            // sans effet partiel (la réserve monde reste débitée d'un montant
            // stocké nulle part — cas impossible par construction).
            return ActionResult.Blocked(DesireKind.Take, "capacité d'inventaire saturée");
        }

        return MechanicalResult(DesireKind.Take, entity, mind);
    }

    /// <summary>Quantité disponible <b>sous la main</b> pour une primitive d'inventaire (gisement ou réserve mondiale).</summary>
    private double AvailableAtHand(Entity entity, ResourceKind kind)
    {
        if (!_options.Resources.Localized)
        {
            return _stocks.Stock(kind);
        }

        return _world.NearestResourceNode(entity.Position, kind, _options.Resources.InteractionRadius)?.Amount ?? 0.0;
    }

    /// <summary>Prélève la source sous la main (gisement localisé ou réserve mondiale).</summary>
    private void ConsumeAtHand(Entity entity, ResourceKind kind, double amount)
    {
        if (!_options.Resources.Localized)
        {
            _stocks.TryConsume(kind, amount);
            return;
        }

        _world.NearestResourceNode(entity.Position, kind, _options.Resources.InteractionRadius)?.TryConsume(amount);
    }

    private ActionResult ExecuteGive(Entity entity, MindState mind, Func<ulong, MindState?>? mindLookup)
    {
        if (mind.Inventory is not { } inventory)
        {
            return ActionResult.Blocked(DesireKind.Give, "inventaire inactif (agents.inventory.enabled = false)");
        }

        Entity? recipient = NearestKnownEntity(entity, mind);
        if (recipient is null)
        {
            return ActionResult.Blocked(DesireKind.Give, "aucune entité connue à portée");
        }

        // Don de la ressource détenue en plus grande quantité (recette fixe ;
        // départage par ordre stable du type — déterminisme).
        ResourceKind held = inventory.Snapshot()
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => (int)pair.Key)
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (inventory.Amount(held) <= 0.0)
        {
            return ActionResult.Blocked(DesireKind.Give, "inventaire vide");
        }

        MindState? recipientMind = mindLookup?.Invoke(recipient.Id.Value);
        Inventory? recipientInventory = recipientMind?.Inventory;
        if (recipientInventory is null)
        {
            return ActionResult.Blocked(DesireKind.Give, "destinataire sans inventaire");
        }

        double amount = Math.Min(_options.Agents.Actions.Inventory.GiveAmount, inventory.Amount(held));
        if (!inventory.TryGive(held, amount, recipientInventory))
        {
            return ActionResult.Blocked(DesireKind.Give, "capacité du destinataire insuffisante");
        }

        // Le don est une interaction sociale positive réciproque.
        mind.Trust.Interact(recipient.Id.Value);
        recipientMind!.Trust.Interact(entity.Id.Value);
        return MechanicalResult(DesireKind.Give, entity, mind);
    }

    private ActionResult ExecuteTrade(Entity entity, MindState mind, Func<ulong, MindState?>? mindLookup)
    {
        if (mind.Inventory is not { } inventory)
        {
            return ActionResult.Blocked(DesireKind.Trade, "inventaire inactif (agents.inventory.enabled = false)");
        }

        Entity? partner = NearestKnownEntity(entity, mind);
        if (partner is null)
        {
            return ActionResult.Blocked(DesireKind.Trade, "aucun partenaire connu à portée");
        }

        MindState? partnerMind = mindLookup?.Invoke(partner.Id.Value);
        if (partnerMind?.Inventory is not { } partnerInventory)
        {
            return ActionResult.Blocked(DesireKind.Trade, "partenaire sans inventaire");
        }

        // Échange fixe 1 Food ↔ 1 Water (× takeAmount, recette V0.1).
        double amount = _options.Agents.Actions.Inventory.TradeAmount;
        (ResourceKind mine, ResourceKind theirs) = mind.Needs.Hunger >= mind.Needs.Thirst
            ? (ResourceKind.Water, ResourceKind.Food)
            : (ResourceKind.Food, ResourceKind.Water);
        if (inventory.Amount(mine) < amount || partnerInventory.Amount(theirs) < amount)
        {
            return ActionResult.Blocked(DesireKind.Trade, "stock insuffisant pour l'échange");
        }

        // Atomicité bidirectionnelle : chaque transfert tout-ou-rien, vérifié avant
        // application — si l'un échoue, aucun des deux n'est exécuté.
        if (partnerInventory.FreeWeight < amount || inventory.FreeWeight < amount)
        {
            return ActionResult.Blocked(DesireKind.Trade, "capacité d'inventaire insuffisante");
        }

        if (!inventory.TryGive(mine, amount, partnerInventory)
            || !partnerInventory.TryGive(theirs, amount, inventory))
        {
            return ActionResult.Blocked(DesireKind.Trade, "échec du transfert bidirectionnel");
        }

        // L'échange est une interaction positive réciproque.
        mind.Trust.Interact(partner.Id.Value);
        partnerMind.Trust.Interact(entity.Id.Value);
        return MechanicalResult(DesireKind.Trade, entity, mind);
    }

    private ActionResult ExecuteAttack(
        Entity entity,
        MindState mind,
        ulong currentTick,
        Func<ulong, MindState?>? mindLookup)
    {
        Entity? target = NearestKnownEntity(entity, mind);
        if (target is null)
        {
            return ActionResult.Blocked(DesireKind.Attack, "aucune cible connue à portée");
        }

        MindState? targetMind = mindLookup?.Invoke(target.Id.Value);
        if (targetMind is null)
        {
            return ActionResult.Blocked(DesireKind.Attack, "cible sans état cognitif");
        }

        double damage = 5.0 * Math.Max(0.0, mind.Factors?.Aggressiveness ?? 1.0);
        if (_commit is { } buffer)
        {
            // Fenêtre causale fermée (ch. III) : le dégât est une conséquence du
            // tick, appliquée en fin d'itération — la défense de la cible est
            // résolue à ce moment, sur le drapeau du tick courant, et l'agent
            // suivant ne voit jamais un état à moitié mis à jour.
            buffer.RequestEnergyDamage(target.Id.Value, targetMind, damage);
            buffer.RequestSafetyThreat(target.Id.Value, targetMind, 0.10);
        }
        else
        {
            targetMind.Needs.ExertEnergy(targetMind.DefendingThisTick ? damage * 0.5 : damage);
            targetMind.Needs.ThreatenSafety(0.10);
        }

        return MechanicalResult(DesireKind.Attack, entity, mind);
    }

    private ActionResult ExecuteDefend(Entity entity, MindState mind)
    {
        mind.DefendingThisTick = true;
        return MechanicalResult(DesireKind.Defend, entity, mind);
    }

    /// <summary>Résultat mécanique standard d'une primitive (coût énergétique du catalogue).</summary>
    private ActionResult MechanicalResult(DesireKind kind, Entity entity, MindState mind)
    {
        ActionDefinition definition = _catalog[kind];
        double energyDelta = 0.0;
        if (definition.EnergyCost > 0.0)
        {
            energyDelta = -definition.EnergyCost;
            mind.Needs.ExertEnergy(definition.EnergyCost);
        }

        return new ActionResult(kind, ActionOutcome.Executed, null, energyDelta, 0.0, 0.0, 0.0, null, 0.0);
    }

    /// <summary>
    /// Entité vivante la plus proche <b>parmi celles que l'agent connaît</b> : la
    /// cible est choisie dans les <b>croyances de position</b> (issues de la
    /// perception ou d'un message), filtrées par la portée perceptuelle et la ligne
    /// de vue. Aucune lecture de la collection globale des entités n'intervient dans
    /// le <i>choix</i> (S-20 / D-09 / B-11) : un pair hors rayon ou masqué n'est pas
    /// candidat.
    /// <para>
    /// La résolution « identifiant connu → entité » est une simple résolution de
    /// référence sur un identifiant déjà cru (et non un parcours de recherche de
    /// cible) ; elle est bornée par le nombre de croyances de l'agent.
    /// </para>
    /// </summary>
    private Entity? NearestKnownEntity(Entity self, MindState mind)
    {
        Entity? nearest = null;
        double bestDistance = double.MaxValue;
        foreach (Belief belief in mind.Beliefs.AllInPlace)
        {
            if (!string.Equals(belief.Fact.Predicate, KnowledgeBuilder.EntityPositionPredicate, StringComparison.Ordinal) ||
                !belief.Fact.Subject.StartsWith("entity-", StringComparison.Ordinal) ||
                !KnowledgeBuilder.TryParsePosition(belief.Fact.Value, out Position position))
            {
                continue;
            }

            if (!ulong.TryParse(
                    belief.Fact.Subject["entity-".Length..],
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out ulong knownId) ||
                knownId == self.Id.Value)
            {
                continue;
            }

            double distance = self.Position.DistanceTo(position);
            if (distance > _options.Agents.Perception.Radius ||
                !LineOfSight.IsClear(self.Position, position, _world.Obstacles))
            {
                continue;
            }

            if (distance > bestDistance || (distance == bestDistance && nearest is not null && knownId >= nearest.Id.Value))
            {
                continue;
            }

            Entity? candidate = FindEntity(knownId);
            if (candidate is null)
            {
                continue;
            }

            nearest = candidate;
            bestDistance = distance;
        }

        return nearest;
    }

    /// <summary>Résolution d'une entité par identifiant <b>déjà cru</b> (jamais un critère de choix).</summary>
    private Entity? FindEntity(ulong id)
    {
        foreach (Entity entity in _world.Entities)
        {
            if (entity.Id.Value == id)
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>
    /// Pas de déplacement vers la <b>destination de l'intention</b> : cible connue
    /// (« eau en P, connue à t »), stable d'un tick à l'autre — c'est le contrat
    /// <c>B-01/B-02</c> : une destination persistante, pas un offset recalculé.
    ///
    /// <para>
    /// Si l'intention ne porte pas de cible (aucun repère connu), le pas retombe
    /// sur l'offset pseudo-aléatoire déterministe historique — <b>prospection
    /// déclarée</b>, et non « quête » : aucun nom d'action ne doit laisser croire à
    /// une navigation vers un objet que l'agent ignore.
    /// </para>
    ///
    /// Si le pas direct est bloqué par un obstacle, le chemin A* contournant
    /// l'obstacle est emprunté (SYNE-077) ; en l'absence de chemin, repli
    /// « sur place ».
    /// </summary>
    private void MoveTowardDestination(Entity entity, MindState mind, DesireKind kind, ulong currentTick)
    {
        // SYNE-071 : une construction posée/retirée en cours de run re-rasterise la
        // grille A* et purge le cache (no-op si aucune modification — déterminisme).
        _pathfinder.Refresh();

        GoalTarget? destination = mind.Intention is { Kind: var intentionKind, Target: { } intended } && intentionKind == kind
            ? intended
            : null;

        Position target;
        if (destination is { } aimed)
        {
            if (entity.Position.DistanceTo(aimed.Position) <= _options.Resources.InteractionRadius)
            {
                return; // arrivé : plus de pas (la satisfaction est décidée par le catalogue)
            }

            target = Position.Clamp(aimed.Position, _world.Size);
        }
        else
        {
            (double dx, double dy) = DeterministicOffset(entity.Id.Value, currentTick, kind);
            target = Position.Clamp(
                new Position(entity.Position.X + dx, entity.Position.Y + dy),
                _world.Size);
        }

        double speed = Math.Max(0.0, entity.Traits["speed"]);
        Position step = StepToward(entity.Position, target, speed);
        if (IsBlocked(step))
        {
            step = PathStep(entity, target, speed);
        }

        if (step == entity.Position)
        {
            return;
        }

        if (_commit is { } buffer)
        {
            buffer.RequestMove(entity, step);
        }
        else
        {
            _world.Grid.Move(entity, step);
        }
    }

    /// <summary>
    /// Prend le pas suivant sur le chemin A* (SYNE-077) quand le pas direct vers
    /// la cible est bloqué. Dépasse la cellule cible si elle est la dernière.
    /// </summary>
    private Position PathStep(Entity entity, Position target, double speed)
    {
        IReadOnlyList<Position> path = _pathfinder.FindPath(entity.Position, target);
        if (path.Count == 0)
        {
            return entity.Position; // repli « sur place »
        }

        Position waypoint = path[0];
        Position step = StepToward(entity.Position, waypoint, speed);
        return IsBlocked(step) ? entity.Position : step;
    }

    /// <summary>Pas de déplacement vers la cible (au plus <paramref name="speed"/> unités).</summary>
    private static Position StepToward(Position from, Position target, double speed)
    {
        double dx = target.X - from.X;
        double dy = target.Y - from.Y;
        double distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= 1e-12)
        {
            return from;
        }

        double step = Math.Min(distance, Math.Max(0.0, speed));
        return new Position(
            from.X + ((dx / distance) * step),
            from.Y + ((dy / distance) * step));
    }

    private bool IsBlocked(Position position) =>
        _world.Obstacles.Any(obstacle => obstacle.Position.DistanceTo(position) <= obstacle.Radius);

    /// <summary>
    /// Cible de déplacement pseudo-aléatoire déterminée par (id, tick, désir) —
    /// finaliseur SplitMix64/avalanche stable, aucune dépendance au PRNG global.
    /// </summary>
    private (double Dx, double Dy) DeterministicOffset(ulong id, ulong tick, DesireKind kind)
    {
        ulong h = id;
        h ^= tick * SplitMix64.Gamma;
        h ^= (ulong)kind * 0xBF58476D1CE4E5B9UL;
        h = SplitMix64.Avalanche(h);

        double angle = ((h % 10000) / 10000.0) * 2.0 * Math.PI;
        double radius = ((h >> 17) % 100) / 100.0 * _options.Agents.Perception.Radius * 0.5;
        return (Math.Cos(angle) * radius, Math.Sin(angle) * radius);
    }
}