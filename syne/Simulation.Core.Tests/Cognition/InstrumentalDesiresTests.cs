// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests des désirs instrumentaux et de la sécurité alimentaire (SYNE-222).

using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests;

public class InstrumentalDesiresTests
{
    private static GoalTarget FoodSource(double x = 250, double y = 100) =>
        new("node", 7, "resource-food-test", new Position(x, y), ObservedTick: 0, Confidence: 0.9);

    private static CognitionKnowledge KnownFood(bool atSource = false) =>
        new(FoodSource(), null, null, null, atSource, false);

    /// <summary>Options avec inventaire et désirs instrumentaux actifs.</summary>
    private static SimulationOptions Options(bool instrumental = true, bool inventory = true)
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Inventory.Enabled = inventory;
        options.Agents.Desires.Instrumental.Enabled = instrumental;
        return options;
    }

    private static MindState Mind(SimulationOptions options, double hunger, double stocked = 0.0)
    {
        var mind = new MindState(options);
        if (hunger > 0.0)
        {
            // RecoverHunger(amount) fait Hunger −= amount : un montant négatif pose
            // la faim, sans exposer de mutateur de test supplémentaire.
            mind.Needs.RecoverHunger(-hunger);
        }

        if (stocked > 0.0 && mind.Inventory is { } inventory)
        {
            inventory.TryTake(ResourceKind.Food, stocked);
        }

        return mind;
    }

    private static NeedsSettings Needs => new();

    [Fact]
    public void Evaluate_IsSilentWhenTheSectionIsDisabled()
    {
        var options = Options(instrumental: false);
        MindState mind = Mind(options, hunger: 20.0);

        Assert.Null(InstrumentalDesires.Evaluate(mind, KnownFood(), Needs, options.Agents.Desires.Instrumental));
    }

    [Fact]
    public void Evaluate_IsSilentWithoutInventory()
    {
        var options = Options(inventory: false);
        MindState mind = Mind(options, hunger: 20.0);

        Assert.Null(InstrumentalDesires.Evaluate(mind, KnownFood(), Needs, options.Agents.Desires.Instrumental));
    }

    [Fact]
    public void Evaluate_IsSilentWhenTheReserveIsAlreadySufficient()
    {
        var options = Options();
        MindState mind = Mind(options, hunger: 20.0, stocked: options.Agents.Desires.Instrumental.ReserveTarget);

        Assert.Null(InstrumentalDesires.Evaluate(mind, KnownFood(), Needs, options.Agents.Desires.Instrumental));
        Assert.True(InstrumentalDesires.IsSecure(mind, options.Agents.Desires.Instrumental));
    }

    [Fact]
    public void Evaluate_WaitsForTheAnticipationHorizon()
    {
        var options = Options();
        InstrumentalSettings settings = options.Agents.Desires.Instrumental;
        // Faim 10 : à 0,5/tick, il faut 80 ticks pour atteindre 50 — sous un
        // horizon de 240 ticks, le seuil est projeté, donc le désir paraît.
        MindState mind = Mind(options, hunger: 10.0);

        Assert.NotNull(InstrumentalDesires.Evaluate(mind, KnownFood(), Needs, settings));

        // Faim au seuil : la faim physiologique prend la main (SeekFood/Eat),
        // l'instrumental s'efface — son horizon est l'avant-seuil, pas le seuil.
        MindState hungry = Mind(options, hunger: Needs.HungerTriggerThreshold);
        Assert.Null(InstrumentalDesires.Evaluate(hungry, KnownFood(), Needs, settings));

        // Horizon nul : plus d'anticipation possible.
        settings.AnticipateTicks = 1;
        MindState calm = Mind(options, hunger: 10.0);
        Assert.Null(InstrumentalDesires.Evaluate(calm, KnownFood(), Needs, settings));
    }

    [Fact]
    public void Evaluate_RequiresAKnownFoodSource()
    {
        var options = Options();
        MindState mind = Mind(options, hunger: 10.0);

        Assert.Null(InstrumentalDesires.Evaluate(mind, CognitionKnowledge.Empty, Needs, options.Agents.Desires.Instrumental));
        Assert.Null(InstrumentalDesires.Evaluate(mind, knowledge: null, Needs, options.Agents.Desires.Instrumental));
    }

    [Fact]
    public void Evaluate_PursuesTheKnownSourceWithABenefitProportionalToTheGap()
    {
        var options = Options();
        InstrumentalSettings settings = options.Agents.Desires.Instrumental;
        MindState mind = Mind(options, hunger: 10.0, stocked: 1.0);

        InstrumentalDesire? desire = InstrumentalDesires.Evaluate(mind, KnownFood(), Needs, settings);

        Assert.NotNull(desire);
        Assert.Equal(DesireKind.SecureFood, desire!.Value.Kind);
        Assert.Equal("resource-food-test", desire.Value.Target!.Value.Label);
        // Réserve 1/3 : deux tiers du bénéfice maximal.
        Assert.Equal(InstrumentalDesires.MaxBenefit * 2.0 / 3.0, desire.Value.Benefit, precision: 9);
    }

    [Fact]
    public void Evaluate_TakesFromTheSourceWhenAlreadyNextToIt()
    {
        var options = Options();
        MindState mind = Mind(options, hunger: 10.0);

        InstrumentalDesire? desire = InstrumentalDesires.Evaluate(
            mind, KnownFood(atSource: true), Needs, options.Agents.Desires.Instrumental);

        Assert.NotNull(desire);
        Assert.Equal(DesireKind.Take, desire!.Value.Kind);
        Assert.Null(desire.Value.Target);
    }

    [Fact]
    public void IsSecure_HoldsWithoutInventory()
    {
        var options = Options(inventory: false);
        MindState mind = Mind(options, hunger: 10.0);

        Assert.True(InstrumentalDesires.IsSecure(mind, options.Agents.Desires.Instrumental));
    }

    [Fact]
    public void Plan_RunsToTheSourceThenTakes()
    {
        GoalTarget target = FoodSource();
        Plan plan = Planner.Build(DesireKind.SecureFood, target, maxSteps: 6);

        Assert.True(Planner.IsPursuit(DesireKind.SecureFood));
        Assert.Equal(2, plan.Count);
        Assert.Equal(DesireKind.SecureFood, plan.Steps[0].Kind);
        Assert.Equal(DesireKind.Take, plan.Steps[1].Kind);
    }

    [Fact]
    public void Validator_RefusesInstrumentalDesiresWithoutInventory()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Desires.Instrumental.Enabled = true;

        Assert.Contains(
            SimulationOptionsValidator.Validate(options),
            error => error.Contains("agents.desires.instrumental.enabled"));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(240, 0)]
    public void Validator_RefusesDegenerateInstrumentalBounds(int anticipateTicks, int reserveTarget)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Inventory.Enabled = true;
        options.Agents.Desires.Instrumental.Enabled = true;
        options.Agents.Desires.Instrumental.AnticipateTicks = anticipateTicks;
        options.Agents.Desires.Instrumental.ReserveTarget = reserveTarget;

        Assert.NotEmpty(SimulationOptionsValidator.Validate(options));
    }

    /// <summary>
    /// Bout en bout dans la boucle : le désir instrumental <b>entre dans la
    /// délibération</b> avant que la faim n'ait franchi son seuil — c'est la
    /// propriété demandée (« ne pas vivre au jour le jour »). On observe les
    /// candidats du tick, pas seulement le choix retenu : la victoire du stock
    /// relève du calibrage (SYNE-227).
    /// </summary>

    /// <summary>
    /// Bout en bout côté exécution : hors de portée d'un gisement, la réserve
    /// portée permet de manger — sans elle, le stock constitué ne servirait à rien.
    /// </summary>
    [Fact]
    public void Executor_EatsFromTheCarriedStockWhenNoNodeIsAtHand()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Inventory.Enabled = true;
        var world = new WorldType(new WorldSize(500, 500));
        var entity = new Entity(new EntityId(1), "Entité A", name: null, new Position(250, 250), TraitSet.NeutralAll, bornAt: 0);
        world.AddEntity(entity);
        var mind = new MindState(options);
        mind.Inventory!.TryTake(ResourceKind.Food, 5.0);
        var executor = new ActionExecutor(world, new ActionCatalog(options.Agents.Actions),
            new ResourceStocks(options.Resources),
            options);

        ActionResult result = executor.Execute(entity, mind, DesireKind.Eat, currentTick: 0);

        Assert.Equal(ActionOutcome.Executed, result.Outcome);
        Assert.Equal(4.0, mind.Inventory.Amount(ResourceKind.Food), precision: 9);
    }

    /// <summary>
    /// Sans inventaire (profil de référence), manger hors de portée d'un gisement
    /// reste refusé : le repli sur le stock est bien une conséquence du stock.
    /// </summary>

    /// <summary>
    /// Prendre alimente la <b>réserve visée</b> : quand la section instrumentale est
    /// active et la réserve sous son niveau, c'est de la nourriture qui est prise,
    /// même si la soif domine — c'est le moyen du désir, pas son substitut.
    /// </summary>

    [Fact]
    public void TemporalScale_ScalesTheAnticipationHorizonOnly()
    {
        var settings = new DesireSettings
        {
            Instrumental = new InstrumentalSettings { Enabled = true, AnticipateTicks = 240, ReserveTarget = 3 },
        };

        // dt = 1 : instance rendue telle quelle (pin bit-à-bit).
        Assert.Same(settings, TemporalScale.ScaleDesires(settings, 1.0));

        DesireSettings scaled = TemporalScale.ScaleDesires(settings, 2.0);
        Assert.True(scaled.Instrumental.Enabled);
        Assert.Equal(120, scaled.Instrumental.AnticipateTicks);
        Assert.Equal(3, scaled.Instrumental.ReserveTarget);
    }

    [Fact]
    public void AgentSettings_PreserveTheDesireSectionThroughTemporalScaling()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Desires.Instrumental.Enabled = true;
        options.Agents.Desires.Instrumental.ReserveTarget = 4;

        SimulationOptions scaled = TemporalScale.WithScaledAgentSettings(
            options,
            TemporalScale.ScaleNeeds(options.Agents.Needs, 2.0),
            TemporalScale.ScaleMemory(options.Agents.Memory, 2.0),
            TemporalScale.ScaleBeliefs(options.Agents.Beliefs, 2.0),
            TemporalScale.ScaleTrust(options.Agents.Trust, 2.0),
            TemporalScale.ScaleActions(options.Agents.Actions, 2.0),
            TemporalScale.ScaleDesires(options.Agents.Desires, 2.0));

        Assert.True(scaled.Agents.Desires.Instrumental.Enabled);
        Assert.Equal(4, scaled.Agents.Desires.Instrumental.ReserveTarget);
        Assert.Equal(120, scaled.Agents.Desires.Instrumental.AnticipateTicks);
    }
}



