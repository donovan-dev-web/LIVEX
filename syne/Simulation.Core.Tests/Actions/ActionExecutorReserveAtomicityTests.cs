using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests.Actions;

/// <summary>
/// Atomicité de la réservation de ressources dans l'exécution d'action
/// (jalon review/refactor, engineVersion 0.12.0).
///
/// <para>
/// Avant correction, <see cref="ActionExecutor.Execute"/> appliquait d'abord les
/// effets puis tentait la consommation : un agent dont la réserve était
/// insuffisante obtenait quand même le bénéfice complet de l'action (faim, soif,
/// énergie), la consommation échouant ensuite sans conséquence. La réserve du
/// monde n'était donc pas contraignante — chaque agent affamé se servait
/// gratuitement — ce qui rendait la rareté d'un stock inopérante et faussait la
/// dynamique de population (mortalité, naissances, pression sur les zones).
/// </para>
/// </summary>
public class ActionExecutorReserveAtomicityTests
{
    private readonly SimulationOptions _options = ConfigLoader.LoadDefaults();
    private readonly ActionCatalog _catalog;
    private readonly ResourceStocks _stocks;
    private readonly WorldType _world;
    private readonly ActionExecutor _executor;
    private readonly Entity _entity;

    public ActionExecutorReserveAtomicityTests()
    {
        _catalog = new ActionCatalog(_options.Agents.Actions);
        _stocks = new ResourceStocks(_options.Resources);
        _world = new WorldType(new WorldSize(500, 500));
        _entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 100), TraitSet.NeutralAll, bornAt: 0);
        _world.AddEntity(_entity);
        _executor = new ActionExecutor(_world, _catalog, _stocks, _options);
    }

    [Fact]
    public void Eat_WithEmptyReserve_IsBlocked_AndGrantsNoBenefit()
    {
        _stocks.TryConsume(ResourceKind.Food, _stocks.Stock(ResourceKind.Food));
        Assert.True(_stocks.IsEmpty(ResourceKind.Food));

        var mind = new MindState(_options);
        mind.Needs.RestoreState(hunger: 80.0, thirst: 0.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 50.0);
        double hungerBefore = mind.Needs.Hunger;
        double energyBefore = mind.Needs.Energy;

        ActionResult result = _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Equal(hungerBefore, mind.Needs.Hunger, 10);
        Assert.Equal(energyBefore, mind.Needs.Energy, 10);
        Assert.Equal(0.0, _stocks.Stock(ResourceKind.Food), 10);
    }

    [Fact]
    public void Drink_WithEmptyReserve_IsBlocked_AndGrantsNoBenefit()
    {
        _stocks.TryConsume(ResourceKind.Water, _stocks.Stock(ResourceKind.Water));
        Assert.True(_stocks.IsEmpty(ResourceKind.Water));

        var mind = new MindState(_options);
        mind.Needs.RestoreState(hunger: 0.0, thirst: 90.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 50.0);
        double thirstBefore = mind.Needs.Thirst;
        double energyBefore = mind.Needs.Energy;

        ActionResult result = _executor.Execute(_entity, mind, DesireKind.Drink, currentTick: 10);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Equal(thirstBefore, mind.Needs.Thirst, 10);
        Assert.Equal(energyBefore, mind.Needs.Energy, 10);
    }

    [Fact]
    public void Eat_WithAlmostEnoughReserve_IsBlocked_AndLeavesTheStockUntouched()
    {
        // Le cas le plus important : « presque assez » ne doit jamais produire un
        // bénéfice partiel. Avant correction, l'agent mangeait « pour de vrai » et
        // la réserve restait à 0.999.
        double portion = _catalog[DesireKind.Eat].ReserveConsumption;
        _stocks.RestoreState(food: portion * 0.5, water: 1000.0, wood: 50.0);

        var mind = new MindState(_options);
        mind.Needs.RestoreState(hunger: 80.0, thirst: 0.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 50.0);
        double hungerBefore = mind.Needs.Hunger;

        ActionResult result = _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Blocked, result.Outcome);
        Assert.Equal(hungerBefore, mind.Needs.Hunger, 10);
        // Tout-ou-rien : la fraction disponible n'est pas prélevée.
        Assert.Equal(portion * 0.5, _stocks.Stock(ResourceKind.Food), 10);
    }

    [Fact]
    public void Eat_WithExactlyEnoughReserve_Succeeds_AndEmptiesTheStock()
    {
        // Frontière exacte : la sémantique est « couvre exactement », donc
        // l'égalité doit réussir et laisser la réserve à zéro.
        double portion = _catalog[DesireKind.Eat].ReserveConsumption;
        _stocks.RestoreState(food: portion, water: 1000.0, wood: 50.0);

        var mind = new MindState(_options);
        mind.Needs.RestoreState(hunger: 80.0, thirst: 0.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 50.0);
        double hungerBefore = mind.Needs.Hunger;

        ActionResult result = _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(hungerBefore - 30.0, mind.Needs.Hunger, 10);
        Assert.Equal(0.0, _stocks.Stock(ResourceKind.Food), 10);
        Assert.True(_stocks.IsEmpty(ResourceKind.Food));
    }

    [Fact]
    public void ReserveExhaustion_StopsServing_ExactlyWhenTheStockRunsOut()
    {
        // Le basculement doit être net : le dernier agent servi vide la réserve,
        // le suivant est bloqué. Aucune décroissance partielle, aucun starvation
        // « avec effet gratuit ».
        var mind = new MindState(_options);
        double portion = _catalog[DesireKind.Eat].ReserveConsumption;
        double initial = _options.Resources.Food.Initial;

        int served = 0;
        for (int attempt = 0; attempt < (int)initial + 50; attempt++)
        {
            ActionResult result = _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: (ulong)attempt);
            if (result.Outcome != ActionOutcome.Executed)
            {
                break;
            }

            served++;
        }

        Assert.Equal((int)Math.Floor(initial / portion), served);
        Assert.Equal(0.0, _stocks.Stock(ResourceKind.Food), 10);
    }

    [Fact]
    public void BlockedReserve_DoesNotConsumeEnergy()
    {
        // L'énergie d'action est un coût : un agent qui n'a pas mangé ne doit pas
        // non plus avoir payé le coût de l'action.
        _stocks.TryConsume(ResourceKind.Food, _stocks.Stock(ResourceKind.Food));

        var mind = new MindState(_options);
        mind.Needs.RestoreState(hunger: 80.0, thirst: 0.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 60.0);
        double energyAfterSetup = mind.Needs.Energy;

        _executor.Execute(_entity, mind, DesireKind.Eat, currentTick: 10);

        Assert.Equal(energyAfterSetup, mind.Needs.Energy, 10);
    }
}
