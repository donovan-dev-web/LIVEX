using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Perception;
using WorldType = Simulation.Core.World.World;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class PerceptionTests
{
    private static PerceptionSettings Settings(int radius = 50, bool lineOfSight = true, int rotation = 4) =>
        new() { Radius = radius, LineOfSight = lineOfSight, RotationInterval = rotation };

    private static Entity MakeEntity(ulong id, Position position) =>
        new(new EntityId(id), "Entité A", name: null, position, TraitSet.NeutralAll, bornAt: 0);

    private static PerceptionSystem PerceptionFor(WorldType world, PerceptionSettings? settings = null) =>
        new(world, settings ?? Settings());

    [Fact]
    public void Perceive_LimitsToPerceptionRadius()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var self = MakeEntity(1, new Position(200, 200));
        var inside = MakeEntity(2, new Position(240, 200));
        var outside = MakeEntity(3, new Position(260, 200));
        world.AddEntity(self);
        world.AddEntity(inside);
        world.AddEntity(outside);

        IReadOnlyList<Observation> observations = PerceptionFor(world).Perceive(self, tick: 1);

        Assert.Contains(observations, o => o.EntityId == 2);
        Assert.DoesNotContain(3UL, observations.Select(o => o.EntityId));
    }

    [Fact]
    public void Perceive_ConfidenceDecreasesWithDistance()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var self = MakeEntity(1, new Position(200, 200));
        var near = MakeEntity(2, new Position(225, 200));
        var atEdge = MakeEntity(3, new Position(250, 200));
        world.AddEntity(self);
        world.AddEntity(near);
        world.AddEntity(atEdge);

        IReadOnlyList<Observation> observations = PerceptionFor(world).Perceive(self, tick: 1);

        // Confiance = 1 − (distance/50) × 0.3 : 25 u → 0.85 ; 50 u → 0.7.
        Assert.Equal(0.85, observations.Single(o => o.EntityId == 2).Confidence, 10);
        Assert.Equal(0.7, observations.Single(o => o.EntityId == 3).Confidence, 10);
        Assert.Equal(2, observations.Count(o => o.EntityType == "entity"));
    }

    [Fact]
    public void Perceive_ObstacleMasksLineOfSight()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var self = MakeEntity(1, new Position(200, 200));
        // Les deux à 40 u : maské (segmente traversé par le rocher) vs dégagé.
        var masked = MakeEntity(2, new Position(240, 200));
        var clear = MakeEntity(3, new Position(200, 240));
        world.AddEntity(self);
        world.AddEntity(masked);
        world.AddEntity(clear);
        world.AddObstacle(new Obstacle("rocher", new Position(230, 200), radius: 10));

        IReadOnlyList<Observation> observations = PerceptionFor(world).Perceive(self, tick: 1);

        Assert.DoesNotContain(2UL, observations.Select(o => o.EntityId));
        Assert.Contains(observations, o => o.EntityId == 3);
    }

    [Fact]
    public void Perceive_WithoutObstacleTheSameEntityIsPerceived()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var self = MakeEntity(1, new Position(200, 200));
        var behind = MakeEntity(2, new Position(240, 200));
        world.AddEntity(self);
        world.AddEntity(behind);

        IReadOnlyList<Observation> observations = PerceptionFor(world, Settings(lineOfSight: false)).Perceive(self, tick: 1);

        Assert.Contains(observations, o => o.EntityId == 2);
    }

    [Fact]
    public void Perceive_ReportsObstaclesAsObservations()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var self = MakeEntity(1, new Position(200, 200));
        world.AddEntity(self);
        world.AddObstacle(new Obstacle("muraille", new Position(240, 200), radius: 5));

        IReadOnlyList<Observation> observations = PerceptionFor(world).Perceive(self, tick: 1);

        Observation obstacle = Assert.Single(observations, o => o.EntityType == "obstacle");
        Assert.Equal("5.00", obstacle.Attributes["radius"]);
    }

    [Fact]
    public void Rotation_PerceptionStaggeredAcrossGroups()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var great = new PerceptionSettings { Radius = 200, RotationInterval = 4 };
        var a = MakeEntity(1, new Position(100, 100));
        var b = MakeEntity(2, new Position(100, 100));
        var c = MakeEntity(3, new Position(100, 100));
        world.AddEntity(a);
        world.AddEntity(b);
        world.AddEntity(c);

        var perception = PerceptionFor(world, great);

        // Groupe = id % 4 : 1→1, 2→2, 3→3, 0→(absent ici). Au tick 1 seul id1 perçoit.
        Assert.True(perception.ShouldPerceive(a, tick: 1));
        Assert.False(perception.ShouldPerceive(b, tick: 1));
        Assert.Empty(perception.Perceive(b, tick: 1));
        Assert.NotEmpty(perception.Perceive(a, tick: 1));
        Assert.Empty(perception.Perceive(a, tick: 2));
    }

    [Fact]
    public void Perceive_OrdersByDistanceThenId()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var self = MakeEntity(1, new Position(200, 200));
        var near = MakeEntity(2, new Position(210, 200));
        var farA = MakeEntity(3, new Position(250, 200));
        var farB = MakeEntity(4, new Position(150, 200));
        world.AddEntity(self);
        world.AddEntity(near);
        world.AddEntity(farA);
        world.AddEntity(farB);

        IReadOnlyList<Observation> observations = PerceptionFor(world).Perceive(self, tick: 1);

        // near (10 u) puis les deux à 50,00 u départagées par identifiant croissant.
        Assert.Equal(
            new ulong[] { 2, 3, 4 },
            observations.Where(o => o.EntityType == "entity").Select(o => o.EntityId));
    }
}