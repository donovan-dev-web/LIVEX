using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests;

public class WorldTests
{
    [Fact]
    public void WorldSize_RejectsNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldSize(0, 500));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldSize(500, -1));
    }

    [Fact]
    public void Position_IsClampedToWorldBounds_NonToroidal()
    {
        var size = new WorldSize(500, 500);

        Assert.Equal(new Position(500, 0), Position.Clamp(new Position(700, -20), size));
        Assert.Equal(new Position(0, 0), Position.Clamp(new Position(-5, -5), size));
        Assert.Equal(new Position(250, 250), Position.Clamp(new Position(250, 250), size));
    }

    [Fact]
    public void AddEntity_RejectsOutOfBoundsPosition()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var entity = new Simulation.Core.Entities.Entity(
            new Simulation.Core.Entities.EntityId(1),
            "Entité A",
            name: null,
            new Position(600, 300),
            Simulation.Core.Entities.TraitSet.NeutralAll,
            bornAt: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => world.AddEntity(entity));
    }

    [Fact]
    public void SamplePosition_IsWithinWorld_AndDeterministicOrder()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var rng = Simulation.Core.Prng.Xoshiro256StarStar.Create(2026);

        var first = world.SamplePosition(rng);

        Assert.InRange(first.Position.X, 0.0, 500.0);
        Assert.InRange(first.Position.Y, 0.0, 500.0);

        var replay = world.SamplePosition(Simulation.Core.Prng.Xoshiro256StarStar.Create(2026));
        Assert.Equal(first.Position, replay.Position);
    }

    [Fact]
    public void DefaultCellSize_YieldsGridLargerThanOneCell()
    {
        var world = new WorldType(new WorldSize(500, 500));
        Assert.True(world.Grid.CellCountX > 1);
        Assert.True(world.Grid.CellCountY > 1);
    }
}