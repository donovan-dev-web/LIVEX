using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class BodyNeedsTests
{
    [Fact]
    public void Advance_AppliesDecision3Rates()
    {
        var needs = new BodyNeeds();
        var settings = new NeedsSettings();

        needs.Advance(settings);

        Assert.Equal(0.5, needs.Hunger, 10);
        Assert.Equal(0.7, needs.Thirst, 10);
        Assert.Equal(0.3, needs.Fatigue, 10);
        Assert.Equal(100.0, needs.Energy);
    }

    [Fact]
    public void Advance_ClampsAtHundred()
    {
        var needs = BodyNeeds.FromState(hunger: 99.9);
        needs.Advance(new NeedsSettings());

        Assert.Equal(100.0, needs.Hunger);
    }

    [Theory]
    [InlineData(49.0, false)]
    [InlineData(50.0, true)]
    public void IsTriggered_FoodThresholdAtFifty(double hunger, bool expected)
    {
        var needs = BodyNeeds.FromState(hunger: hunger);
        Assert.Equal(expected, needs.IsTriggered(DesireKind.SeekFood));
        Assert.Equal(expected, needs.IsTriggered(DesireKind.Eat));
    }

    [Fact]
    public void IsTriggered_UsesConfigurableThresholds()
    {
        var needs = BodyNeeds.FromState(hunger: 60.0);
        var settings = new NeedsSettings { HungerTriggerThreshold = 65.0 };

        Assert.False(needs.IsTriggered(DesireKind.Eat, settings));

        settings.HungerTriggerThreshold = 55.0;
        Assert.True(needs.IsTriggered(DesireKind.Eat, settings));
    }

    [Fact]
    public void IsTriggered_AfterEnoughTicksThirstDrivesGoal()
    {
        var needs = new BodyNeeds();
        var settings = new NeedsSettings();
        for (int i = 0; i < 72; i++)
        {
            needs.Advance(settings);
        }

        Assert.True(needs.IsTriggered(DesireKind.SeekWater));
        Assert.True(needs.Thirst >= BodyNeeds.ThirstThreshold);
    }

    [Fact]
    public void Drive_MapsNeedsToHomogenizedScale()
    {
        var needs = BodyNeeds.FromState(hunger: 80, thirst: 60, fatigue: 50, safety: 0.3, social: 0.8, curiosity: 0.5);

        Assert.Equal(80.0, needs.Drive(DesireKind.SeekFood));
        Assert.Equal(80.0, needs.Drive(DesireKind.Eat));
        Assert.Equal(60.0, needs.Drive(DesireKind.SeekWater));
        Assert.Equal(60.0, needs.Drive(DesireKind.Drink));
        Assert.Equal(50.0, needs.Drive(DesireKind.Rest));
        Assert.Equal(70.0, needs.Drive(DesireKind.Flee), 10);
        Assert.Equal(80.0, needs.Drive(DesireKind.Socialize));
        Assert.Equal(50.0, needs.Drive(DesireKind.Explore));
    }

    [Fact]
    public void IsCritical_DetectsLowEnergyOrHighHunger()
    {
        Assert.False(new BodyNeeds().IsCritical);
        Assert.True(BodyNeeds.FromState(hunger: 95.0).IsCritical);
        Assert.True(BodyNeeds.FromState(energy: 5.0).IsCritical);
    }

    [Fact]
    public void RecoverHungerAndThirst_ReduceNeedsAndClamp()
    {
        var needs = BodyNeeds.FromState(hunger: 80.0, thirst: 60.0);

        needs.RecoverHunger(100.0);
        needs.RecoverThirst(25.0);

        Assert.Equal(0.0, needs.Hunger);
        Assert.Equal(35.0, needs.Thirst, 10);
    }
}

public class DesireFactoryTests
{
    private static ActionCatalog Catalog() => new(ConfigLoader.LoadDefaults().Agents.Actions);

    private static ResourceStocks Stocks(double food = 100.0, double water = 1000.0) =>
        ResourceStocks.FromState(food, water, wood: 50.0);

    [Fact]
    public void Generate_TriggeredThirstResolvesToTerminalDrinkWhenReserveAvailable()
    {
        var needs = BodyNeeds.FromState(thirst: 65.0);

        IReadOnlyList<Goal> goals = DesireFactory.Generate(needs, tick: 10, activeKinds: [], Catalog(), Stocks());

        Goal goal = Assert.Single(goals);
        Assert.Equal(DesireKind.Drink, goal.Kind);
        Assert.Equal(10UL, goal.BornTick);
    }

    [Fact]
    public void Generate_TriggeredHungerFallsBackToSeekWhenReserveEmpty()
    {
        var needs = BodyNeeds.FromState(hunger: 65.0);

        IReadOnlyList<Goal> goals = DesireFactory.Generate(needs, tick: 10, activeKinds: [], Catalog(), Stocks(food: 0.0));

        Goal goal = Assert.Single(goals);
        Assert.Equal(DesireKind.SeekFood, goal.Kind);
    }

    [Fact]
    public void Generate_NoGoalBelowThreshold()
    {
        var needs = new BodyNeeds();

        IReadOnlyList<Goal> goals = DesireFactory.Generate(needs, tick: 10, activeKinds: [], Catalog(), Stocks());

        Assert.Empty(goals);
    }

    [Fact]
    public void Generate_SkipsAlreadyActiveKinds()
    {
        var needs = BodyNeeds.FromState(thirst: 80.0, hunger: 70.0);

        IReadOnlyList<Goal> goals = DesireFactory.Generate(needs, tick: 10, activeKinds: [DesireKind.Drink], Catalog(), Stocks());

        Assert.Single(goals);
        Assert.Equal(DesireKind.Eat, goals[0].Kind);
    }

    [Fact]
    public void Goal_AgeMapsTickDelta()
    {
        var goal = new Goal(DesireKind.Explore, BornTick: 7);
        Assert.Equal(3UL, goal.Age(10));
    }
}