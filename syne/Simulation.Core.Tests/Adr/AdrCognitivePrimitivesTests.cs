using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests.Adr;

/// <summary>
/// Primitives atomiques D7 + inventaire D8 (ADR acceptés du 30/09/2026,
/// engineVersion 0.14.0) : manipulation d'inventaire sous capacité, échange
/// fixe 1↔1, dégât d'attaque §3.15.4 (réduit de moitié si défense), drapeaux
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
        Dictionary<ulong, MindState?> lookup = new() { [1] = _mind, [2] = targetMind };

        _executor.Execute(_entity, _mind, DesireKind.Attack, currentTick: 5, id => lookup[id]);

        Assert.Equal(energyBefore - 2.5, targetMind.Needs.Energy, 10);
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
