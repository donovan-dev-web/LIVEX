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
}