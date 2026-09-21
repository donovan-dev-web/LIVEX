using Simulation.Core.Entities;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests.Entities;

public class EntityFactoryTests
{
    private static (World.World World, EntityTemplate Template) Setup() =>
        (new World.World(new WorldSize(500, 500), spatialCellSize: 30), EntityTemplate.DefaultA);

    [Fact]
    public void CreateNext_ProducesEntitiesInTraitDomain()
    {
        var (world, template) = Setup();
        var rng = Xoshiro256StarStar.Create(12345);

        for (ulong i = 1; i <= 50; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(template, world, rng, i, bornAt: 0);
            Assert.Equal(i, entity.Id.Value);
            Assert.Equal("Entité A", entity.Species);
            Assert.InRange(entity.Position.X, 0.0, 500.0);
            Assert.InRange(entity.Position.Y, 0.0, 500.0);
            Assert.All(TraitSet.TraitNames, name =>
            {
                Assert.InRange(entity.Traits[name], TraitSet.Min, TraitSet.Max);
                (double min, double max) = template.TraitRanges[name];
                Assert.InRange(entity.Traits[name], min, max);
            });
        }
    }

    [Fact]
    public void CreateNext_SameInputs_ReproducesIdentically()
    {
        var (worldA, template) = Setup();
        var (worldB, _) = Setup();

        var rngA = Xoshiro256StarStar.Create(12345);
        var rngB = Xoshiro256StarStar.Create(12345);

        for (ulong i = 1; i <= 20; i++)
        {
            (var a, rngA) = EntityFactory.CreateNext(template, worldA, rngA, i, bornAt: 0);
            (var b, rngB) = EntityFactory.CreateNext(template, worldB, rngB, i, bornAt: 0);

            Assert.Equal(a.Id, b.Id);
            Assert.Equal(a.Position, b.Position);
            foreach (string name in TraitSet.TraitNames)
            {
                Assert.Equal(a.Traits[name], b.Traits[name], precision: 12);
            }
        }
    }

    [Fact]
    public void CreateNext_StreamPinnedToIndependentReference()
    {
        // Valeurs de référence générées par une implémentation Python indépendante
        // (xoshiro_ref.py, seed 12345, paramétrage « Entité A » [0.5, 1.5]).
        var (world, template) = Setup();
        var rng = Xoshiro256StarStar.Create(12345);

        (var first, rng) = EntityFactory.CreateNext(template, world, rng, 1, bornAt: 0);
        (var second, _) = EntityFactory.CreateNext(template, world, rng, 2, bornAt: 0);

        Assert.Equal("00001", first.Id.ToString());
        Assert.Equal(1.2438081631565894, first.Traits["bravery"], precision: 12);
        Assert.Equal(0.54834011483634582, first.Traits["greed"], precision: 12);
        Assert.Equal(192.98287133867248, first.Position.X, precision: 10);
        Assert.Equal(455.30653777535434, first.Position.Y, precision: 10);

        Assert.Equal(1.2818442024444772, second.Traits["bravery"], precision: 12);
        Assert.Equal(270.16232000789995, second.Position.X, precision: 10);
    }

    [Fact]
    public void CreateNext_DifferentSeeds_ProduceDifferentEntities()
    {
        var (worldA, template) = Setup();
        var (worldB, _) = Setup();

        var rngA = Xoshiro256StarStar.Create(1);
        var rngB = Xoshiro256StarStar.Create(2);

        bool anyDifference = false;
        for (ulong i = 1; i <= 10; i++)
        {
            (var a, rngA) = EntityFactory.CreateNext(template, worldA, rngA, i, bornAt: 0);
            (var b, rngB) = EntityFactory.CreateNext(template, worldB, rngB, i, bornAt: 0);
            anyDifference |= !a.Traits["bravery"].Equals(b.Traits["bravery"]);
        }

        Assert.True(anyDifference);
    }
}