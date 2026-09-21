using Simulation.Core.Entities;
using Simulation.Core.Prng;
using Xunit;

namespace Simulation.Core.Tests.Entities;

public class EntityTemplateTests
{
    [Fact]
    public void DefaultA_ProvidesAllRangesInDocBand()
    {
        var template = EntityTemplate.DefaultA;

        Assert.Equal("Entité A", template.Species);
        foreach (string name in TraitSet.TraitNames)
        {
            (double min, double max) = template.TraitRanges[name];
            Assert.Equal(0.5, min, precision: 12);
            Assert.Equal(1.5, max, precision: 12);
        }
    }

    [Fact]
    public void MissingRange_Throws()
    {
        var incomplete = new Dictionary<string, (double Min, double Max)> { ["bravery"] = (0.5, 1.5) };
        Assert.Throws<ArgumentException>(() => new EntityTemplate("Entité B", incomplete));
    }

    [Fact]
    public void InvalidRange_Throws()
    {
        var ranges = new Dictionary<string, (double Min, double Max)>(StringComparer.Ordinal);
        foreach (string name in TraitSet.TraitNames)
        {
            ranges[name] = name == "bravery" ? (1.5, 0.5) : (0.5, 1.5);
        }

        Assert.Throws<ArgumentException>(() => new EntityTemplate("Entité B", ranges));
    }

    [Fact]
    public void RangeOutsideTraitDomain_Throws()
    {
        var ranges = new Dictionary<string, (double Min, double Max)>(StringComparer.Ordinal);
        foreach (string name in TraitSet.TraitNames)
        {
            ranges[name] = name == "bravery" ? (0.0, 2.5) : (0.5, 1.5);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityTemplate("Entité B", ranges));
    }
}

public class EntityTests
{
    [Fact]
    public void Age_ComputesTicksSinceBirth()
    {
        var entity = new Entity(
            new EntityId(1),
            "Entité A",
            name: null,
            new Simulation.Core.World.Position(1, 1),
            TraitSet.NeutralAll,
            bornAt: 10);

        Assert.Equal(0UL, entity.Age(9));
        Assert.Equal(0UL, entity.Age(10));
        Assert.Equal(5UL, entity.Age(15));
    }

    [Fact]
    public void Constructor_RejectsBlankSpecies()
    {
        Assert.Throws<ArgumentException>(() => new Entity(
            new EntityId(1),
            " ",
            name: null,
            new Simulation.Core.World.Position(1, 1),
            TraitSet.NeutralAll,
            bornAt: 0));
    }
}