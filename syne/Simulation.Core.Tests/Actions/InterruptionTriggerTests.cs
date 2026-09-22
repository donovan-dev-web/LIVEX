using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests.Actions;

/// <summary>
/// Déclencheur d'interruption centralisé (SYNE-043) : unique point de décision
/// « l'action en cours doit-elle être interrompue ? », évaluée à tout tick.
/// </summary>
public class InterruptionTriggerTests
{
    private readonly SimulationOptions _options = ConfigLoader.LoadDefaults();
    private readonly ActionCatalog _catalog;
    private readonly ResourceStocks _stocks;
    private readonly InterruptionTrigger _trigger;
    private readonly AgentFactors _factors = new(TraitSet.NeutralAll);

    public InterruptionTriggerTests()
    {
        _catalog = new ActionCatalog(_options.Agents.Actions);
        _stocks = new ResourceStocks(_options.Resources);
        _trigger = new InterruptionTrigger(_catalog, _stocks);
    }

    private (IReadOnlyList<UtilityScore> Scores, UtilityScore? Chosen) Evaluate(BodyNeeds needs, DesireKind currentKind)
        => _trigger.Evaluate(
            _options.Agents.Actions.Interruption,
            _options.Agents.Actions,
            needs,
            _factors,
            successRate: _ => 0.8,
            currentKind,
            currentIntention: null,
            currentTick: 1,
            entityId: 1);

    [Fact]
    public void NoUrgency_WhenNeedsAreNominal()
    {
        (IReadOnlyList<UtilityScore> scores, UtilityScore? chosen) = Evaluate(new BodyNeeds(), DesireKind.SeekFood);

        Assert.Empty(scores);
        Assert.Null(chosen);
    }

    [Fact]
    public void CriticalHunger_WithReserve_ResolvesToTerminalEat()
    {
        // SYNE-042/043 : faim critique + réserve disponible → Eat.
        BodyNeeds needs = BodyNeeds.FromState(hunger: 90.0);

        (IReadOnlyList<UtilityScore> scores, UtilityScore? chosen) = Evaluate(needs, DesireKind.SeekWater);

        Assert.NotNull(chosen);
        Assert.Equal(DesireKind.Eat, chosen.Value.Kind);
        Assert.Equal(2, scores.Count);
    }

    [Fact]
    public void CriticalHunger_EmptyReserve_FallsBackToSeekFood()
    {
        // SYNE-042/043 : réserve vide → la poursuite SeekFood répond à la faim.
        var empty = ResourceStocks.FromState(food: 0.0, water: 1000.0, wood: 50.0);
        var trigger = new InterruptionTrigger(_catalog, empty);
        BodyNeeds needs = BodyNeeds.FromState(hunger: 90.0);

        (_, UtilityScore? chosen) = trigger.Evaluate(
            _options.Agents.Actions.Interruption,
            _options.Agents.Actions,
            needs,
            _factors,
            successRate: _ => 0.8,
            currentKind: DesireKind.SeekWater,
            currentIntention: null,
            currentTick: 1,
            entityId: 1);

        Assert.NotNull(chosen);
        Assert.Equal(DesireKind.SeekFood, chosen.Value.Kind);
    }

    [Fact]
    public void CriticalEnergy_ResolvesToRest()
    {
        BodyNeeds needs = BodyNeeds.FromState(energy: 5.0, fatigue: 90.0);

        (IReadOnlyList<UtilityScore> scores, UtilityScore? chosen) = Evaluate(needs, DesireKind.SeekFood);

        Assert.NotNull(chosen);
        Assert.Equal(DesireKind.Rest, chosen.Value.Kind);
    }

    [Fact]
    public void CurrentHungerAction_IsNotReInterrupted()
    {
        // Le déclencheur n'interrompt pas Eat/SeekFood par la faim déjà prise en charge.
        BodyNeeds needs = BodyNeeds.FromState(hunger: 90.0);

        (IReadOnlyList<UtilityScore> scores, UtilityScore? chosen) = Evaluate(needs, DesireKind.Eat);

        Assert.Empty(scores);
        Assert.Null(chosen);
    }

    [Fact]
    public void MarginNotMet_NoInterruption()
    {
        // La candidature ne doit pas passer si l'utilité ne dépasse pas la marge.
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Interruption.UtilityExcessMargin = 1000.0;
        BodyNeeds needs = BodyNeeds.FromState(hunger: 90.0);

        (IReadOnlyList<UtilityScore> scores, UtilityScore? chosen) = _trigger.Evaluate(
            options.Agents.Actions.Interruption,
            options.Agents.Actions,
            needs,
            _factors,
            successRate: _ => 0.8,
            DesireKind.SeekWater,
            currentIntention: null,
            currentTick: 1,
            entityId: 1);

        Assert.Empty(scores);
        Assert.Null(chosen);
    }
}