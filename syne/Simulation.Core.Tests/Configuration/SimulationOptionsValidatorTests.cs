using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

public class SimulationOptionsValidatorTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        Assert.Empty(SimulationOptionsValidator.Validate(ConfigLoader.LoadDefaults()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void InvalidWorldWidth_IsRejected(int width)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Simulation.WorldWidth = width;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("worldWidth"));
    }

    [Fact]
    public void InvalidMaxTicks_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Simulation.MaxTicks = 0;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("maxTicks"));
    }

    [Fact]
    public void UnknownEngine_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Random.Engine = "mt19937";

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("engine"));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(2.01)]
    public void TraitOutOfRange_IsRejected(double value)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Traits["bravery"] = value;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("bravery"));
    }

    [Fact]
    public void MultipleErrors_AreAllReported()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Simulation.WorldWidth = 0;
        options.Random.Engine = "nope";

        Assert.True(SimulationOptionsValidator.Validate(options).Count >= 2);
    }

    [Fact]
    public void InvalidDeliberationInterval_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Deliberation.IntervalTicks = 0;

        Assert.Contains(SimulationOptionsValidator.Validate(options), e => e.Contains("intervalTicks"));
    }

    [Fact]
    public void InvalidDeliberationMargins_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Deliberation.AlignBonus = 0;
        options.Agents.Actions.Deliberation.ActionSwitchMargin = -0.1;
        options.Agents.Actions.Deliberation.ConflictTieMargin = -1;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("alignBonus"));
        Assert.Contains(errors, e => e.Contains("actionSwitchMargin"));
        Assert.Contains(errors, e => e.Contains("conflictTieMargin"));
    }

    [Fact]
    public void InvalidInterruptionThresholds_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Interruption.UtilityExcessMargin = -1;
        options.Agents.Actions.Interruption.CriticalHunger = 101;
        options.Agents.Actions.Interruption.CriticalEnergy = 100;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("utilityExcessMargin"));
        Assert.Contains(errors, e => e.Contains("criticalHunger"));
        Assert.Contains(errors, e => e.Contains("criticalEnergy"));
    }

    [Fact]
    public void InvalidResourceSettings_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Resources.Food.Initial = -1;
        options.Resources.Water.RegenerationRate = -0.5;
        options.Resources.Wood.DegradationTick = 0;
        options.Resources.Mineral.Initial = -10;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("resources.food.initial"));
        Assert.Contains(errors, e => e.Contains("resources.water.regenerationRate"));
        Assert.Contains(errors, e => e.Contains("resources.wood.degradationTick"));
        Assert.Contains(errors, e => e.Contains("resources.mineral.initial"));
    }

    [Fact]
    public void InvalidGroupSettings_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Groups.ReviewIntervalTicks = 0;
        options.Groups.TrustThreshold = 1.1;
        options.Groups.MinGroupSize = 1;
        options.Groups.SharedBeliefBonus = -0.1;
        options.Groups.GoalAlignmentBonus = -1;
        options.Groups.ConsensusThreshold = 2.0;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("groups.reviewIntervalTicks"));
        Assert.Contains(errors, e => e.Contains("groups.trustThreshold"));
        Assert.Contains(errors, e => e.Contains("groups.minGroupSize"));
        Assert.Contains(errors, e => e.Contains("groups.sharedBeliefBonus"));
        Assert.Contains(errors, e => e.Contains("groups.goalAlignmentBonus"));
        Assert.Contains(errors, e => e.Contains("groups.consensusThreshold"));
    }

    [Fact]
    public void InvalidReproductionSettings_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Reproduction.IntervalTicks = 0;
        options.Reproduction.ConsentTrustThreshold = -1;
        options.Reproduction.MaxBirthsPerTick = -1;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("reproduction.intervalTicks"));
        Assert.Contains(errors, e => e.Contains("reproduction.consentTrustThreshold"));
        Assert.Contains(errors, e => e.Contains("reproduction.maxBirthsPerTick"));
    }

    [Fact]
    public void InvalidInheritanceSettings_AreRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Inheritance.Dominance = 1.5;
        options.Agents.Inheritance.MutationRate = -0.5;
        options.Agents.Inheritance.MutationMagnitude = -1;
        options.Agents.Inheritance.SalienceThreshold = 0.0;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("agents.inheritance.dominance"));
        Assert.Contains(errors, e => e.Contains("agents.inheritance.mutationRate"));
        Assert.Contains(errors, e => e.Contains("agents.inheritance.mutationMagnitude"));
        Assert.Contains(errors, e => e.Contains("agents.inheritance.salienceThreshold"));
    }
}