// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Modèle de ressources localisées (arbitrage Phase 0 du réalignement) : la source d'une action
//        terminale est le GISEMENT où l'agent se tient — jamais une réserve mondiale. Couvre la fermeture
//        des fuites d'information (aucune cible non perçue) et l'atomicité du prélèvement sur un gisement.

using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests.Actions;

/// <summary>
/// Modèle de ressources <b>localisées</b> (arbitrage Phase 0, <c>resources.localized</c> —
/// défaut du run de référence depuis le réalignement). L'action terminale se sert
/// dans le gisement <b>sous la main</b> :
/// <list type="bullet">
/// <item>l'agent hors de portée d'un gisement ne peut pas se nourrir (aucune source
/// connue n'est un fait du monde) ;</item>
/// <item>un gisement épuisé n'est pas une source : l'objectif doit être révisé, pas
/// servi à crédit ;</item>
/// <item>le prélèvement est tout-ou-rien sur le gisement comme il l'était sur la
/// réserve globale (aucun bénéfice non payé).</item>
/// </list>
/// </summary>
public class LocalizedResourceActionTests
{
    private readonly SimulationOptions _options = ConfigLoader.LoadDefaults();
    private readonly ActionCatalog _catalog;
    private readonly ResourceStocks _stocks;
    private readonly WorldType _world;
    private readonly ActionExecutor _executor;
    private readonly Entity _entity;

    public LocalizedResourceActionTests()
    {
        Assert.True(_options.Resources.Localized, "Le modèle localisé est le défaut du réalignement.");
        _catalog = new ActionCatalog(_options.Agents.Actions);
        _stocks = new ResourceStocks(_options.Resources);
        _world = new WorldType(new WorldSize(500, 500));
        _entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(_entity);
        _executor = new ActionExecutor(_world, _catalog, _stocks, _options);
    }

    private ResourceNode PlaceNode(ResourceKind kind, double amount, double x = 250, double y = 100) =>
        Add(kind, amount, x, y);

    private ResourceNode Add(ResourceKind kind, double amount, double x, double y)
    {
        var node = new ResourceNode(
            $"resource-{kind.ToString().ToLowerInvariant()}-test",
            kind,
            new Position(x, y),
            amount,
            capacity: 100.0,
            regenerationRate: 0.0);
        _world.AddResourceNode(node);
        return node;
    }

    private MindState Hungry(double hunger = 80.0)
    {
        var mind = new MindState(_options);
        mind.Needs.RestoreState(hunger: hunger, thirst: 0.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 50.0);
        return mind;
    }

    [Fact]
    public void Eat_ConsumesTheNodeTheAgentStandsOn()
    {
        ResourceNode node = PlaceNode(ResourceKind.Food, amount: 5.0);
        MindState mind = Hungry();
        double hungerBefore = mind.Needs.Hunger;

        ActionResult result = _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(node.Id, result.SourceNodeId);
        Assert.Equal(5.0 - result.ReserveConsumedAmount, node.Amount, 10);
        Assert.Equal(hungerBefore - 30.0, mind.Needs.Hunger, 10);
        // La réserve globale publiée n'est pas la source : elle n'est pas débitée ici
        // (l'agrégat est recalculé par la boucle depuis les gisements).
        Assert.Equal(_options.Resources.Food.Initial, _stocks.Stock(ResourceKind.Food), 10);
    }

    [Fact]
    public void Eat_WithoutAnyNodeInReach_IsBlocked_AndGrantsNothing()
    {
        MindState mind = Hungry();
        double hungerBefore = mind.Needs.Hunger;
        double energyBefore = mind.Needs.Energy;

        ActionResult result = _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Contains("aucun gisement", result.Reason);
        Assert.Equal(hungerBefore, mind.Needs.Hunger, 10);
        Assert.Equal(energyBefore, mind.Needs.Energy, 10);
    }

    [Fact]
    public void Eat_OnADistantNode_IsBlocked()
    {
        // Hors du rayon d'interaction (2 u. par défaut) : le gisement existe dans le
        // monde, mais pas sous la main de l'agent.
        PlaceNode(ResourceKind.Food, amount: 5.0, x: 250.0 + (_options.Resources.InteractionRadius * 4.0), y: 100.0);
        MindState mind = Hungry();
        double hungerBefore = mind.Needs.Hunger;

        ActionResult result = _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Equal(hungerBefore, mind.Needs.Hunger, 10);
    }

    [Fact]
    public void Eat_OnADepletedNode_IsBlocked_AndTheObjectiveMustBeRevised()
    {
        ResourceNode node = PlaceNode(ResourceKind.Food, amount: 0.0);
        MindState mind = Hungry();
        double hungerBefore = mind.Needs.Hunger;

        ActionResult result = _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Contains(node.Id, result.Reason);
        Assert.Equal(0.0, node.Amount, 10);
        Assert.Equal(hungerBefore, mind.Needs.Hunger, 10);
    }

    [Fact]
    public void Eat_OnAnAlmostSufficientNode_IsBlocked_WithoutPartialConsumption()
    {
        double portion = _catalog[DesireKind.Eat].ReserveConsumption;
        ResourceNode node = PlaceNode(ResourceKind.Food, amount: portion * 0.5);
        MindState mind = Hungry();

        ActionResult result = _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Equal(portion * 0.5, node.Amount, 10);
    }

    [Fact]
    public void Take_IsBlocked_WhenTheLocalizedNodeCannotCoverTheAmount()
    {
        // Take (inventaire D8) prélève la source sous la main : un gisement plus
        // pauvre que la quantité demandée ne produit aucun effet partiel.
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Inventory.Enabled = true;
        var catalog = new ActionCatalog(options.Agents.Actions);
        var world = new WorldType(new WorldSize(500, 500));
        var entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        world.AddEntity(entity);
        var node = new ResourceNode("resource-food-test", ResourceKind.Food, new Position(250, 100), 0.25, 100.0, 0.0);
        world.AddResourceNode(node);
        var executor = new ActionExecutor(world, catalog, new ResourceStocks(options.Resources), options);
        var mind = new MindState(options);
        double takeAmount = options.Agents.Actions.Inventory.TakeAmount;
        Assert.True(0.25 < takeAmount);

        ActionResult result = executor.Execute(entity, mind, DesireKind.Take, currentTick: 10);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Contains("insuffisante", result.Reason);
        Assert.Equal(0.25, node.Amount, 10);
        Assert.Equal(0.0, mind.Inventory!.Amount(ResourceKind.Food), 10);
    }

    [Fact]
    public void Take_DrawsFromTheNodeUnderHand_WithinCapacity()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Inventory.Enabled = true;
        var catalog = new ActionCatalog(options.Agents.Actions);
        var world = new WorldType(new WorldSize(500, 500));
        var entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        world.AddEntity(entity);
        var node = new ResourceNode("resource-food-test", ResourceKind.Food, new Position(250, 100), 5.0, 100.0, 0.0);
        world.AddResourceNode(node);
        var executor = new ActionExecutor(world, catalog, new ResourceStocks(options.Resources), options);
        var mind = new MindState(options);
        double takeAmount = options.Agents.Actions.Inventory.TakeAmount;

        ActionResult result = executor.Execute(entity, mind, DesireKind.Take, currentTick: 10);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(5.0 - takeAmount, node.Amount, 10);
        Assert.Equal(takeAmount, mind.Inventory!.Amount(ResourceKind.Food), 10);
    }
}
