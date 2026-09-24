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

    [Fact]
    public void AddObstacle_RejectsOutOfBoundsPosition()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var obstacle = new Obstacle("hors-bord", new Position(700, 300), radius: 10);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => world.AddObstacle(obstacle));
        Assert.Contains("sort du monde", ex.Message);
        Assert.Empty(world.Obstacles);
    }

    [Fact]
    public void AddObstacle_RejectsDuplicateId()
    {
        var world = new WorldType(new WorldSize(500, 500));
        world.AddObstacle(new Obstacle("rocher", new Position(250, 250), radius: 15));

        Assert.Throws<ArgumentException>(() => world.AddObstacle(new Obstacle("rocher", new Position(260, 260), radius: 10)));
        Assert.Single(world.Obstacles);
    }

    [Fact]
    public void AddObstacle_IncrementsRevision_WithoutTracingEnvironmentChange()
    {
        var world = new WorldType(new WorldSize(500, 500));
        ulong initial = world.ObstacleRevision;

        world.AddObstacle(new Obstacle("rocher", new Position(250, 250), radius: 15));

        Assert.True(world.ObstacleRevision > initial);
        Assert.Empty(world.LastEnvironmentChanges);
    }

    [Fact]
    public void PlaceConstruction_TracesAddAndBumpsRevision()
    {
        var world = new WorldType(new WorldSize(500, 500));
        ulong initial = world.ObstacleRevision;

        world.PlaceConstruction(new Obstacle("maison", new Position(150, 150), radius: 12));

        Assert.Equal(EnvironmentChangeKind.Added, Assert.Single(world.LastEnvironmentChanges).Kind);
        Assert.Equal("maison", Assert.Single(world.LastEnvironmentChanges).Obstacle.Id);
        Assert.True(world.ObstacleRevision > initial);
        Assert.Single(world.Obstacles);
    }

    [Fact]
    public void RemoveConstruction_TracesRemoval_AndReturnsFalseWhenAbsent()
    {
        var world = new WorldType(new WorldSize(500, 500));
        world.PlaceConstruction(new Obstacle("maison", new Position(150, 150), radius: 12));
        world.ClearEnvironmentChanges();
        ulong initial = world.ObstacleRevision;

        Assert.True(world.RemoveConstruction("maison"));
        Assert.Empty(world.Obstacles);
        Assert.Equal(EnvironmentChangeKind.Removed, Assert.Single(world.LastEnvironmentChanges).Kind);
        Assert.True(world.ObstacleRevision > initial);

        world.ClearEnvironmentChanges();
        Assert.False(world.RemoveConstruction("maison"));
        Assert.Empty(world.LastEnvironmentChanges);
    }

    [Fact]
    public void ClearEnvironmentChanges_ConsumesTracedModifications()
    {
        var world = new WorldType(new WorldSize(500, 500));
        world.PlaceConstruction(new Obstacle("maison", new Position(150, 150), radius: 12));

        world.ClearEnvironmentChanges();

        Assert.Empty(world.LastEnvironmentChanges);
        Assert.Single(world.Obstacles);
    }

    [Fact]
    public void ApplyConfiguredLayout_PlacesObstaclesOnlyWhenEnabled()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var layout = new Simulation.Core.Configuration.WorldSettings
        {
            Obstacles = true,
            ObstacleLayout =
            [
                new() { Id = "maison-1", X = 100, Y = 100, Radius = 10 },
                new() { Id = "maison-2", X = 400, Y = 400, Radius = 20 },
            ],
        };

        world.ApplyConfiguredLayout(layout);

        Assert.Equal(2, world.Obstacles.Count);
        Assert.Equal(20.0, world.Obstacles[1].Radius);
        Assert.Empty(world.LastEnvironmentChanges);

        var disabled = new Simulation.Core.Configuration.WorldSettings { Obstacles = false };
        var emptyWorld = new WorldType(new WorldSize(500, 500));
        emptyWorld.ApplyConfiguredLayout(disabled);
        Assert.Empty(emptyWorld.Obstacles);
    }
}