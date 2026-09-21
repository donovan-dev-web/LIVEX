using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
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
    [InlineData(59.0, false)]
    [InlineData(60.0, true)]
    public void IsTriggered_FoodThresholdAtSixty(double hunger, bool expected)
    {
        var needs = BodyNeeds.FromState(hunger: hunger);
        Assert.Equal(expected, needs.IsTriggered(DesireKind.SeekFood));
    }

    [Fact]
    public void IsTriggered_AfterEnoughTicksThirstDrivesGoal()
    {
        var needs = new BodyNeeds();
        var settings = new NeedsSettings();
        for (int i = 0; i < 86; i++)
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
        Assert.Equal(60.0, needs.Drive(DesireKind.SeekWater));
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
}

public class DesireFactoryTests
{
    [Fact]
    public void Generate_CreatesGoalWhenTriggered()
    {
        var needs = BodyNeeds.FromState(thirst: 65.0);

        IReadOnlyList<Goal> goals = DesireFactory.Generate(needs, tick: 10, activeKinds: []);

        Goal goal = Assert.Single(goals);
        Assert.Equal(DesireKind.SeekWater, goal.Kind);
        Assert.Equal(10UL, goal.BornTick);
    }

    [Fact]
    public void Generate_NoGoalBelowThreshold()
    {
        var needs = new BodyNeeds();

        IReadOnlyList<Goal> goals = DesireFactory.Generate(needs, tick: 10, activeKinds: []);

        Assert.Empty(goals);
    }

    [Fact]
    public void Generate_SkipsAlreadyActiveKinds()
    {
        var needs = BodyNeeds.FromState(thirst: 80.0, hunger: 70.0);

        IReadOnlyList<Goal> goals = DesireFactory.Generate(needs, tick: 10, activeKinds: [DesireKind.SeekWater]);

        Assert.Single(goals);
        Assert.Equal(DesireKind.SeekFood, goals[0].Kind);
    }

    [Fact]
    public void Goal_AgeMapsTickDelta()
    {
        var goal = new Goal(DesireKind.Explore, BornTick: 7);
        Assert.Equal(3UL, goal.Age(10));
    }
}