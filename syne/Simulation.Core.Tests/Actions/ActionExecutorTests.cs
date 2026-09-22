using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests.Actions;

/// <summary>
/// Exécuteur d'actions (SYNE-040) : exécution atomique d'une action par entité
/// par tick, effets issus du catalogue déclaratif, itération par identifiant
/// croissant sans consommation du PRNG.
/// </summary>
public class ActionExecutorTests
{
    private readonly SimulationOptions _options = ConfigLoader.LoadDefaults();
    private readonly ActionCatalog _catalog;
    private readonly ResourceStocks _stocks;
    private readonly WorldType _world;
    private readonly ActionExecutor _executor;
    private readonly Entity _entity;
    private readonly MindState _mind;

    public ActionExecutorTests()
    {
        _catalog = new ActionCatalog(_options.Agents.Actions);
        _stocks = new ResourceStocks(_options.Resources);
        _world = new WorldType(new WorldSize(500, 500));
        _entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(_entity);
        _executor = new ActionExecutor(_world, _catalog, _stocks, _options);
        _mind = new MindState(_options);
    }

    [Fact]
    public void Eat_AppliesCatalogEffectsAndConsumesFoodReserve()
    {
        // SYNE-042 : Eat consomme la réserve Food et applique les effets du catalogue.
        double energyBefore = _mind.Needs.Energy;
        double foodBefore = _stocks.Stock(ResourceKind.Food);

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(-30.0, result.HungerDelta);
        Assert.Equal(-0.2, result.EnergyDelta);
        Assert.Equal(ResourceKind.Food, result.ReserveConsumed);
        Assert.Equal(1.0, result.ReserveConsumedAmount);
        Assert.Equal(foodBefore - 1.0, _stocks.Stock(ResourceKind.Food), 10);
        Assert.Equal(energyBefore - 0.2, _mind.Needs.Energy, 10);
        // L'eau n'est pas touchée par Eat.
        Assert.Equal(1000.0, _stocks.Stock(ResourceKind.Water));
    }

    [Fact]
    public void Drink_ConsumesWaterReserveWithoutMoving()
    {
        // SYNE-042 : Drink est une action terminale (pas de déplacement) qui
        // consomme la réserve Water.
        Position before = _entity.Position;
        double waterBefore = _stocks.Stock(ResourceKind.Water);

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Drink, currentTick: 10);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(-30.0, result.ThirstDelta);
        Assert.Equal(before, _entity.Position);
        Assert.Equal(waterBefore - 1.0, _stocks.Stock(ResourceKind.Water), 10);
    }

    [Fact]
    public void Eat_WithEmptyReserve_IsBlockedWithNoEffect()
    {
        // SYNE-042 : réserve vide → action bloquée, aucun effet appliqué.
        _stocks.TryConsume(ResourceKind.Food, 100.0);
        double energyBefore = _mind.Needs.Energy;

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal(0.0, result.EnergyDelta);
        Assert.Equal(0.0, result.HungerDelta);
        Assert.Equal(energyBefore, _mind.Needs.Energy, 10);
    }

    [Fact]
    public void SeekFood_MovesDeterministicallyAndAppliesEnergyCost()
    {
        // SYNE-041 : déplacement vers la cible déterministe du tick (id, souhait)
        // avec coût d'énergie déclaré, sans consommer de réserve.
        Position before = _entity.Position;
        double energyBefore = _mind.Needs.Energy;

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.SeekFood, currentTick: 10);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(-0.5, result.EnergyDelta);
        Assert.Equal(energyBefore - 0.5, _mind.Needs.Energy, 10);
        Assert.NotEqual(before, _entity.Position);
        Assert.Equal(100.0, _stocks.Stock(ResourceKind.Food), 10);
    }

    [Fact]
    public void Movement_DoesNotEnterObstacle()
    {
        // SYNE-041 : une entité évoluant près d'un obstacle n'entre jamais dans
        // son intérieur (pas bloqué sur un obstacle, DETERMINISM.md §5).
        var obstacle = new Obstacle("îlot", new Position(50, 50), radius: 20);
        _world.AddObstacle(obstacle);
        var rover = new Entity(new EntityId(9), "Entité B", name: null, new Position(100, 250), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(rover);
        MindState roverMind = new(_options);

        for (ulong tick = 1; tick <= 200; tick++)
        {
            _executor.Execute(rover, roverMind, DesireKind.Explore, tick);
        }

        Assert.True(rover.Position.DistanceTo(obstacle.Position) >= obstacle.Radius);
    }

    [Fact]
    public void Rest_RecoversFatigueAndEnergyWithoutMoving()
    {
        Position before = _entity.Position;
        _mind.Needs.ExertEnergy(20.0);

        ActionResult result = _executor.Execute(_entity, _mind, DesireKind.Rest, currentTick: 5);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(0.5, result.EnergyDelta);
        Assert.Equal(-1.0, result.FatigueDelta);
        Assert.Equal(before, _entity.Position);
    }
}