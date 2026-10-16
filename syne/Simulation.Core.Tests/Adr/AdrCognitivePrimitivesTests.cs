// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Primitives atomiques D7 + inventaire D8 (acceptés du 30/09/2026, engineVersion 0.14.0) : manipulation
//        d'inventaire sous capacité, échange fixe 1↔1, dégât d'attaque (réduit de moitié si défense), drapeaux de
//        traçabilité et de trajectoire (désactivés par défaut).

using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests.Adr;

/// <summary>
/// Primitives atomiques D7 + inventaire D8 (acceptés du 30/09/2026,
/// engineVersion 0.14.0) : manipulation d'inventaire sous capacité, échange
/// fixe 1↔1, dégât d'attaque (réduit de moitié si défense), drapeaux
/// de traçabilité et de trajectoire (désactivés par défaut).
/// </summary>
public class AdrCognitivePrimitivesTests
{
    private readonly SimulationOptions _options = ConfigLoader.LoadDefaults();
    private readonly ActionCatalog _catalog;
    private readonly ResourceStocks _stocks;
    private readonly World.World _world;
    private readonly ActionExecutor _executor;
    private readonly Entity _entity;
    private readonly MindState _mind;

    public AdrCognitivePrimitivesTests()
    {
        // Modèle de ressources global (arbitrage Phase 0) : Take prélève la réserve
        // mondiale. Les primitives inter-entités, elles, ne peuvent plus choisir leur
        // cible par un balayage global (S-20/D-09/B-11) — chaque cas déclare donc ce
        // que l'agent <b>sait</b> de son pair (<see cref="KnowPeer(bool)"/>).
        _options.Resources.Localized = false;
        _options.Agents.Actions.Inventory.Enabled = true;
        _options.Agents.Actions.Plans.Enabled = true;
        _catalog = new ActionCatalog(_options.Agents.Actions);
        _stocks = new ResourceStocks(_options.Resources);
        _world = new World.World(new WorldSize(500, 500));
        _entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(_entity);
        _executor = new ActionExecutor(_world, _catalog, _stocks, _options);
        _mind = new MindState(_options);
    }

    /// <summary>
    /// Déclare la croyance de position du pair (identifiant 2) — la seule voie par
    /// laquelle une primitive inter-entités peut désigner sa cible : perception ou
    /// message reçu. Un pair présent dans le monde mais jamais perçu n'est pas un
    /// candidat.
    /// </summary>
    private void KnowPeer(bool believed = true, double confidence = 1.0)
    {
        if (!believed)
        {
            return;
        }

        _mind.Beliefs.ApplyObservation(
            new Fact(
                "entity-2",
                KnowledgeBuilder.EntityPositionPredicate,
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{250.0:0.00},{100.0:0.00}")),
            confidence,
            "test-perception-1",
            _options.Agents.Beliefs,
            tick: 4);
    }

    [Fact]
    public void Take_TransfersFromWorldReserveIntoInventory_WithinCapacity()
    {
        double foodBefore = _stocks.Stock(ResourceKind.Food);
        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Take, currentTick: 5);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(foodBefore - _options.Agents.Actions.Inventory.TakeAmount, _stocks.Stock(ResourceKind.Food), 10);
        Assert.Equal(_options.Agents.Actions.Inventory.TakeAmount, _mind.Inventory!.Amount(ResourceKind.Food), 10);
    }

    [Fact]
    public void Take_FailsWithoutPartialEffect_WhenCapacityIsExhausted()
    {
        _mind.Inventory!.TryTake(ResourceKind.Food, _options.Agents.Actions.Inventory.CapacityWeight);
        double foodBefore = _stocks.Stock(ResourceKind.Food);
        double weightBefore = _mind.Inventory.CurrentWeight;

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Take, currentTick: 5);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Equal(foodBefore, _stocks.Stock(ResourceKind.Food), 10);
        Assert.Equal(weightBefore, _mind.Inventory.CurrentWeight, 10);
    }

    [Fact]
    public void Give_TransfersToNearestRecipient_AndRewardsTrust()
    {
        var neighbor = new Entity(new EntityId(2), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(neighbor);
        var neighborMind = new MindState(_options);
        _mind.Inventory!.TryTake(ResourceKind.Food, 3.0);
        KnowPeer();

        Dictionary<ulong, MindState?> lookup = new()
        {
            [1] = _mind,
            [2] = neighborMind,
        };

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Give, currentTick: 5, id => lookup[id]);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(3.0 - _options.Agents.Actions.Inventory.GiveAmount, _mind.Inventory.Amount(ResourceKind.Food), 10);
        Assert.Equal(_options.Agents.Actions.Inventory.GiveAmount, neighborMind.Inventory!.Amount(ResourceKind.Food), 10);
        Assert.True(_mind.Trust.Knows(2));
        Assert.True(neighborMind.Trust.Knows(1));
    }

    [Fact]
    public void Trade_RequiresBothStocks_AndExchangesAtomically()
    {
        var partner = new Entity(new EntityId(2), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(partner);
        var partnerMind = new MindState(_options);
        _mind.Inventory!.TryTake(ResourceKind.Water, 2.0);
        partnerMind.Inventory!.TryTake(ResourceKind.Food, 2.0);
        KnowPeer();

        Dictionary<ulong, MindState?> lookup = new() { [1] = _mind, [2] = partnerMind };

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Trade, currentTick: 5, id => lookup[id]);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        // Thirst dominante → l'initiateur donne Water et reçoit Food.
        Assert.Equal(1.0, _mind.Inventory.Amount(ResourceKind.Food), 10);
        Assert.Equal(1.0, _mind.Inventory.Amount(ResourceKind.Water), 10);
        Assert.Equal(1.0, partnerMind.Inventory!.Amount(ResourceKind.Food), 10);
        Assert.Equal(1.0, partnerMind.Inventory.Amount(ResourceKind.Water), 10);
    }

    [Fact]
    public void Trade_FailsWithoutAnyTransfer_WhenOneSideCannotPay()
    {
        var partner = new Entity(new EntityId(2), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(partner);
        var partnerMind = new MindState(_options);
        _mind.Inventory!.TryTake(ResourceKind.Water, 2.0);
        KnowPeer();
        // Le partenaire n'a pas de Food : l'échange doit échouer des deux côtés.

        Dictionary<ulong, MindState?> lookup = new() { [1] = _mind, [2] = partnerMind };

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Trade, currentTick: 5, id => lookup[id]);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Equal(2.0, _mind.Inventory.Amount(ResourceKind.Water), 10);
        Assert.Equal(0.0, partnerMind.Inventory!.Amount(ResourceKind.Water), 10);
    }

    [Fact]
    public void Attack_DealsFiveTimesAggressiveness_AndThreatensSafety()
    {
        var target = new Entity(new EntityId(2), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(target);
        var targetMind = new MindState(_options);
        double energyBefore = targetMind.Needs.Energy;
        double safetyBefore = targetMind.Needs.Safety;
        KnowPeer();

        Dictionary<ulong, MindState?> lookup = new() { [1] = _mind, [2] = targetMind };

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Attack, currentTick: 5, id => lookup[id]);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(energyBefore - 5.0, targetMind.Needs.Energy, 10);
        Assert.True(targetMind.Needs.Safety < safetyBefore);
    }

    [Fact]
    public void Defend_HalvesIncomingDamage_ForTheSameTick()
    {
        var target = new Entity(new EntityId(2), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(target);
        var targetMind = new MindState(_options);
        double energyBefore = targetMind.Needs.Energy;

        targetMind.DefendingThisTick = true;
        KnowPeer();
        Dictionary<ulong, MindState?> lookup = new() { [1] = _mind, [2] = targetMind };

        _executor.Execute(_entity, _mind, DesireKind.Attack, currentTick: 5, id => lookup[id]);

        Assert.Equal(energyBefore - 2.5, targetMind.Needs.Energy, 10);
    }

    /// <summary>
    /// Gate de réalignement (Phase 3, S-20/D-09/B-11) : une entité présente dans le
    /// monde mais <b>jamais perçue</b> n'est pas une cible — les primitives
    /// inter-entités ne lisent plus la collection globale des entités pour choisir
    /// leur cible.
    /// </summary>
    [Fact]
    public void InterEntityPrimitives_AreBlocked_WithoutBelievedTarget()
    {
        var stranger = new Entity(new EntityId(2), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(stranger);
        var strangerMind = new MindState(_options);
        _mind.Inventory!.TryTake(ResourceKind.Food, 3.0);
        strangerMind.Inventory!.TryTake(ResourceKind.Water, 3.0);
        Assert.False(_mind.Beliefs.TryGet(
            new Fact("entity-2", KnowledgeBuilder.EntityPositionPredicate, "250.00,100.00"),
            out _));

        Dictionary<ulong, MindState?> lookup = new() { [1] = _mind, [2] = strangerMind };
        ActionResult attack = _executor.Execute(_entity, _mind, DesireKind.Attack, currentTick: 5, id => lookup[id]);
        ActionResult trade = _executor.Execute(_entity, _mind, DesireKind.Trade, currentTick: 5, id => lookup[id]);

        Assert.Equal(ActionOutcome.Blocked, attack.Outcome);
        Assert.Equal(ActionOutcome.Blocked, trade.Outcome);
        Assert.Equal("aucune cible connue à portée", attack.Reason);
    }

    /// <summary>
    /// Gate Phase 3 (AC7, S-20/D-09/B-11) : une cible crue <b>hors rayon de
    /// perception</b> n'est pas candidat, même si elle est présente dans le monde
    /// et connue de l'agent. Le choix ne lit jamais la collection globale au-delà
    /// de la portée perceptuelle.
    /// </summary>
    [Fact]
    public void InterEntityPrimitives_AreBlocked_WhenBelievedTargetIsOutOfRadius()
    {
        var distant = new Entity(new EntityId(2), "Entité A", name: null, new Position(400, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(distant);
        var distantMind = new MindState(_options);
        _mind.Inventory!.TryTake(ResourceKind.Food, 3.0);
        _mind.Inventory!.TryTake(ResourceKind.Water, 3.0);
        distantMind.Inventory!.TryTake(ResourceKind.Food, 3.0);
        distantMind.Inventory!.TryTake(ResourceKind.Water, 3.0);

        // Croyance de position du pair, hors rayon (|400 - 250| = 150 > 50).
        _mind.Beliefs.ApplyObservation(
            new Fact("entity-2", KnowledgeBuilder.EntityPositionPredicate, "400.00,100.00"),
            1.0,
            "test-perception-1",
            _options.Agents.Beliefs,
            tick: 4);

        Dictionary<ulong, MindState?> lookup = new() { [1] = _mind, [2] = distantMind };
        ActionResult attack = _executor.Execute(_entity, _mind, DesireKind.Attack, currentTick: 5, id => lookup[id]);
        ActionResult give = _executor.Execute(_entity, _mind, DesireKind.Give, currentTick: 5, id => lookup[id]);
        ActionResult trade = _executor.Execute(_entity, _mind, DesireKind.Trade, currentTick: 5, id => lookup[id]);

        Assert.Equal(ActionOutcome.Blocked, attack.Outcome);
        Assert.Equal(ActionOutcome.Blocked, give.Outcome);
        Assert.Equal(ActionOutcome.Blocked, trade.Outcome);
        Assert.Equal("aucune cible connue à portée", attack.Reason);
    }

    /// <summary>
    /// Gate Phase 3 (AC7, S-20/D-09/B-11) : une cible crue <b>masquée par un
    /// obstacle</b> (ligne de vue coupée) n'est pas candidat, même si elle est
    /// dans le rayon de perception et connue de l'agent.
    /// </summary>
    [Fact]
    public void InterEntityPrimitives_AreBlocked_WhenBelievedTargetIsMaskedByObstacle()
    {
        // Mur entre l'entité 1 (250,100) et le pair cru (290,100) : la ligne de
        // vue est coupée alors que la distance (40) reste dans le rayon (50).
        _world.AddObstacle(new Obstacle("mur", new Position(270, 100), radius: 15));
        var hidden = new Entity(new EntityId(2), "Entité A", name: null, new Position(290, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(hidden);
        var hiddenMind = new MindState(_options);
        _mind.Inventory!.TryTake(ResourceKind.Food, 3.0);
        _mind.Inventory!.TryTake(ResourceKind.Water, 3.0);
        hiddenMind.Inventory!.TryTake(ResourceKind.Food, 3.0);
        hiddenMind.Inventory!.TryTake(ResourceKind.Water, 3.0);

        _mind.Beliefs.ApplyObservation(
            new Fact("entity-2", KnowledgeBuilder.EntityPositionPredicate, "290.00,100.00"),
            1.0,
            "test-perception-1",
            _options.Agents.Beliefs,
            tick: 4);

        Dictionary<ulong, MindState?> lookup = new() { [1] = _mind, [2] = hiddenMind };
        ActionResult attack = _executor.Execute(_entity, _mind, DesireKind.Attack, currentTick: 5, id => lookup[id]);
        ActionResult give = _executor.Execute(_entity, _mind, DesireKind.Give, currentTick: 5, id => lookup[id]);
        ActionResult trade = _executor.Execute(_entity, _mind, DesireKind.Trade, currentTick: 5, id => lookup[id]);

        Assert.Equal(ActionOutcome.Blocked, attack.Outcome);
        Assert.Equal(ActionOutcome.Blocked, give.Outcome);
        Assert.Equal(ActionOutcome.Blocked, trade.Outcome);
        Assert.Equal("aucune cible connue à portée", attack.Reason);
    }

    [Fact]
    public void Primitives_AreBlocked_WhenInventoryIsDisabled()
    {
        var disabledOptions = ConfigLoader.LoadDefaults();
        var catalog = new ActionCatalog(disabledOptions.Agents.Actions);
        var executor = new ActionExecutor(_world, catalog, _stocks, disabledOptions);
        var disabledMind = new MindState(disabledOptions);
        Assert.Null(disabledMind.Inventory);

        ActionResult take = executor.Execute(_entity, disabledMind, DesireKind.Take, currentTick: 5);
        ActionResult give = executor.Execute(_entity, disabledMind, DesireKind.Give, currentTick: 5);

        Assert.Equal(ActionOutcome.Blocked, take.Outcome);
        Assert.Equal(ActionOutcome.Blocked, give.Outcome);
    }

    [Fact]
    public void PlanLibrary_OnlyAddsCandidates_WhenEnabled_WithStockAndGreed()
    {
        var goal = new Goal(DesireKind.SeekFood, BornTick: 1);
        var plansOn = _options.Agents.Actions.Plans;
        var inventorySettings = _options.Agents.Actions.Inventory;
        plansOn.Enabled = true;
        inventorySettings.Enabled = true;

        var candidates = PlanLibrary.GenerateCandidates([goal], _mind, _entity, _catalog, _stocks, inventorySettings, plansOn, 2);
        // greed neutre = 1.0 → Take candidat ; pas de pair connu → pas de Trade.
        Assert.Contains(candidates, candidate => candidate.Kind == DesireKind.Take);
        Assert.DoesNotContain(candidates, candidate => candidate.Kind == DesireKind.Trade);

        plansOn.Enabled = false;
        var none = PlanLibrary.GenerateCandidates([goal], _mind, _entity, _catalog, _stocks, inventorySettings, plansOn, 2);
        Assert.Empty(none);
    }

    [Fact]
    public void Commitment_Fulfills_Expires_AndImpactsTrust()
    {
        var commitment = new Commitment(2, "survival", 0, 10);
        Assert.True(commitment.IsActive(5));

        // Expiration = pénalité de confiance (distincte du mensonge factuel).
        Assert.True(commitment.Expire(12));
        Assert.Equal(CommitmentStatus.Expired, commitment.Status);

        var held = new Commitment(3, "survival", 0, 100);
        Assert.True(held.Fulfill(20));
        Assert.Equal(CommitmentStatus.Fulfilled, held.Status);
        Assert.False(held.Fulfill(21)); // idempotent

        // Récompense et pénalité génériques (D5) — pair inconnu : part de 0.0,
        // récompense plafonnée à 1.0, pénalité planchée à 0.0.
        var trust = new Relationships(_options.Agents.Trust);
        double rewarded = trust.Reward(peerId: 2, _options.Agents.Trust.CommitmentBonus);
        Assert.Equal(_options.Agents.Trust.CommitmentBonus, rewarded, 10);
        double penalized = trust.Penalize(peerId: 2, _options.Agents.Trust.CommitmentPenalty);
        Assert.Equal(0.0, penalized, 10);
    }
}
