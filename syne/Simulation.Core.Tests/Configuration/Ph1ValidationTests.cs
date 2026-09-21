using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Configuration;

public class Ph1ValidationTests
{
    [Theory]
    [InlineData(19)]
    [InlineData(71)]
    public void PerceptionRadiusOutOfDecision6Range_IsRejected(int radius)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Perception.Radius = radius;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("perception.radius"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void RotationIntervalOutOfRange_IsRejected(int interval)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Perception.RotationInterval = interval;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("rotationInterval"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(0.0)]
    [InlineData(1.5)]
    public void BeliefUpdateStrengthOutOfRange_IsRejected(double strength)
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Beliefs.UpdateStrength = strength;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("updateStrength"));
    }

    [Fact]
    public void NegativeDecayRate_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Memory.EventDecayRate = -0.01;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("DecayRate"));
    }

    [Fact]
    public void ZeroCapacity_IsRejected()
    {
        var options = ConfigLoader.LoadDefaults();
        options.Agents.Memory.MaxCapacity = 0;

        var errors = SimulationOptionsValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("maxCapacity"));
    }
}