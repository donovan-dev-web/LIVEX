using Simulation.Core.Entities;
using Xunit;

namespace Simulation.Core.Tests.Entities;

public class TraitSetTests
{
    [Fact]
    public void NeutralAll_HasEightTraits_AllAtOne()
    {
        var traits = TraitSet.NeutralAll;

        Assert.Equal(8, traits.Values.Count());
        Assert.All(TraitSet.TraitNames, name => Assert.Equal(1.0, traits[name]));
    }

    [Fact]
    public void MissingTrait_Throws()
    {
        var incomplete = new Dictionary<string, double> { ["bravery"] = 1.0 };
        Assert.Throws<ArgumentException>(() => new TraitSet(incomplete));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(2.01)]
    [InlineData(double.NaN)]
    public void OutOfRangeTrait_Throws(double value)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string name in TraitSet.TraitNames)
        {
            values[name] = name == "bravery" ? value : 1.0;
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new TraitSet(values));
    }

    [Fact]
    public void Mean_IsAverageOfTraits()
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string name in TraitSet.TraitNames)
        {
            values[name] = 2.0;
        }

        var traits = new TraitSet(values);
        Assert.Equal(2.0, traits.Mean, precision: 12);
    }

    [Fact]
    public void UnknownTraitName_Throws()
    {
        var traits = TraitSet.NeutralAll;
        Assert.Throws<KeyNotFoundException>(() => _ = traits["inconnu"]);
    }
}