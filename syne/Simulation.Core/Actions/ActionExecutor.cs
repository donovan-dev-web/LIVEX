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
/// pas de déplacement dans un obstacle (DETERMINISM.md §5).
/// </summary>
public sealed class ActionExecutor
{
    private readonly World.World _world;
    private readonly ActionCatalog _catalog;
    private readonly ResourceStocks _stocks;
    private readonly SimulationOptions _options;
    private readonly AStarPathfinder _pathfinder;

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

        // Primitives D7 (ADR « Primitives d'actions ») : manipulation d'inventaire
        // et interaction inter-entités. Renvoie null si l'action n'est pas une
        // primitive (elle emprunte alors le chemin déclaratif historique).
        ActionResult? primitive = ExecutePrimitive(entity, mind, kind, currentTick, mindLookup);
        if (primitive is not null)
        {
            return primitive;
        }

        ActionDefinition definition = _catalog[kind];

        // 1. Prélèvement atomique de la réserve — avant tout effet observable.
        ResourceKind? reserveConsumed = null;
        double consumed = 0.0;
        if (definition.RequiresReserve is { } reserve)
        {
            consumed = definition.ReserveConsumption;
            if (!_stocks.TryConsume(reserve, consumed))
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

        if (definition.Movement)
        {
            MoveTowardDeterministicTarget(entity, kind, currentTick);
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

        return new ActionResult(
            kind,
            ActionOutcome.Executed,
            null,
            energyDelta,
            hungerDelta,
            thirstDelta,
            fatigueDelta,
            reserveConsumed,
            consumed);
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
    /// d'énergie (§3.15.4) à la cible la plus proche — attaque neutre, jamais portée
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
        // (recette fixe courte de l'ADR — pas de planificateur).
        ResourceKind reserve = mind.Needs.Hunger >= mind.Needs.Thirst ? ResourceKind.Food : ResourceKind.Water;
        double amount = Math.Min(_options.Agents.Actions.Inventory.TakeAmount, inventory.FreeWeight);
        if (amount <= 0.0 || _stocks.IsEmpty(reserve))
        {
            return ActionResult.Blocked(DesireKind.Take, _stocks.IsEmpty(reserve) ? $"réserve {reserve} vide" : "capacité d'inventaire saturée");
        }

        // Atomicité : prélèvement monde d'abord (échec = aucun effet), stockage ensuite.
        if (!_stocks.TryConsume(reserve, amount))
        {
            return ActionResult.Blocked(DesireKind.Take, $"réserve {reserve} insuffisante");
        }

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

    private ActionResult ExecuteGive(Entity entity, MindState mind, Func<ulong, MindState?>? mindLookup)
    {
        if (mind.Inventory is not { } inventory)
        {
            return ActionResult.Blocked(DesireKind.Give, "inventaire inactif (agents.inventory.enabled = false)");
        }

        Entity? recipient = NearestOtherEntity(entity);
        if (recipient is null)
        {
            return ActionResult.Blocked(DesireKind.Give, "aucune entité à portée");
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

        // Le don est une interaction sociale positive réciproque (§3.19.3).
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

        Entity? partner = NearestOtherEntity(entity);
        if (partner is null)
        {
            return ActionResult.Blocked(DesireKind.Trade, "aucun partenaire à portée");
        }

        MindState? partnerMind = mindLookup?.Invoke(partner.Id.Value);
        if (partnerMind?.Inventory is not { } partnerInventory)
        {
            return ActionResult.Blocked(DesireKind.Trade, "partenaire sans inventaire");
        }

        // Échange fixe 1 Food ↔ 1 Water (× takeAmount, recette V0.1 de l'ADR).
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

        // L'échange est une interaction positive réciproque (§3.19.3).
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
        Entity? target = NearestOtherEntity(entity);
        if (target is null)
        {
            return ActionResult.Blocked(DesireKind.Attack, "aucune cible à portée");
        }

        MindState? targetMind = mindLookup?.Invoke(target.Id.Value);
        if (targetMind is null)
        {
            return ActionResult.Blocked(DesireKind.Attack, "cible sans état cognitif");
        }

        double damage = 5.0 * Math.Max(0.0, mind.Factors?.Aggressiveness ?? 1.0);
        if (targetMind.DefendingThisTick)
        {
            damage *= 0.5;
        }

        targetMind.Needs.ExertEnergy(damage);
        targetMind.Needs.ThreatenSafety(0.10);
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

    /// <summary>Entité vivante la plus proche (toute autre que soi, hors murs), ou null.</summary>
    private Entity? NearestOtherEntity(Entity self)
    {
        Entity? nearest = null;
        double bestDistance = double.MaxValue;
        foreach (Entity other in _world.Entities)
        {
            if (other.Id.Value == self.Id.Value)
            {
                continue;
            }

            double distance = self.Position.DistanceTo(other.Position);
            if (distance < bestDistance || (distance == bestDistance && nearest is not null && other.Id.Value < nearest.Id.Value))
            {
                nearest = other;
                bestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Pas de déplacement vers la cible. Si le pas direct est bloqué par un
    /// obstacle, le chemin A* contournant l'obstacle est emprunté (SYNE-077 :
    /// le pas suit alors le premier centre de cellule du chemin) ; en l'absence de
    /// chemin (destination inaccessible ou expansion plafonnée), repli « sur place ».
    /// </summary>
    private void MoveTowardDeterministicTarget(Entity entity, DesireKind kind, ulong currentTick)
    {
        // SYNE-071 : une construction posée/retirée en cours de run re-rasterise la
        // grille A* et purge le cache (no-op si aucune modification — déterminisme).
        _pathfinder.Refresh();

        ActionDefinition definition = _catalog[kind];
        (double dx, double dy) = DeterministicOffset(entity.Id.Value, currentTick, kind);
        double targetX = entity.Position.X + dx;
        double targetY = entity.Position.Y + dy;

        Position target = Position.Clamp(
            new Position(targetX, targetY),
            _world.Size);

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

        _world.Grid.Move(entity, step);
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