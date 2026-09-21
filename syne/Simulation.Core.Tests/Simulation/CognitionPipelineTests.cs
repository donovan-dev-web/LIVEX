using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using WorldType = Simulation.Core.World.World;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class CognitionPipelineTests
{
    private static SimulationOptions Options() => ConfigLoader.LoadDefaults();

    private static Entity MakeEntity(ulong id, Position position) =>
        new(new EntityId(id), "Entité A", name: null, position, TraitSet.NeutralAll, bornAt: 0);

    private static (WorldType World, SimulationLoop Loop) BuildLoop()
    {
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(MakeEntity(1, new Position(50, 50)));
        world.AddEntity(MakeEntity(2, new Position(80, 50)));
        world.AddEntity(MakeEntity(3, new Position(300, 300)));
        world.AddEntity(MakeEntity(4, new Position(50, 90)));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), Options());
        return (world, loop);
    }

    [Fact]
    public void Step_BuildsMindForEveryEntity_AndStoresObservations()
    {
        (_, SimulationLoop loop) = BuildLoop();
        loop.AdvanceOneTick();

        for (ulong id = 1; id <= 4; id++)
        {
            Assert.True(loop.Cognition.HasMind(id));
        }

        MindState mind = loop.Cognition.MindOf(1);
        // id1 (groupe 1) perçoit au tick 1 : id2 (dist 30) et id4 (dist 40) dans le rayon 50.
        IReadOnlyList<MemoryRecall> recalled = mind.Memory.Recall(1);
        Assert.True(recalled.Count >= 2);
        Assert.Contains(recalled, r => r.Entry.Content.StartsWith("entity|2|", StringComparison.Ordinal));
        Assert.Contains(recalled, r => r.Entry.Content.StartsWith("entity|4|", StringComparison.Ordinal));

        Assert.True(mind.Beliefs.TryGet(new Fact("entity-2", "position", "80.00,50.00"), out _));
    }

    [Fact]
    public void Step_RotationMeansNotEveryEntityPerceivesEveryTick()
    {
        (_, SimulationLoop loop) = BuildLoop();
        loop.AdvanceOneTick();

        // id2 (groupe 2) ne perçoit pas au tick 1.
        MindState mind2 = loop.Cognition.MindOf(2);
        Assert.Empty(mind2.Memory.Recall(1));

        // … mais il perçoit au tick 2.
        loop.AdvanceOneTick();
        Assert.NotEmpty(loop.Cognition.MindOf(2).Memory.Recall(2));
    }

    [Fact]
    public void Deliberation_SelectsThirstOnceTriggered()
    {
        (_, SimulationLoop loop) = BuildLoop();
        loop.Run(100);

        // Soif = 0.7 × 100 = 70 ≥ 60 → désir SeekWater dominant.
        MindState mind = loop.Cognition.MindOf(1);
        Assert.Equal(DesireKind.SeekWater, mind.Intention!.Value.Kind);
        Assert.NotNull(mind.LastDecision);
    }

    [Fact]
    public void PerceptionAndDecisions_AreDeterministicAcrossTwoRuns()
    {
        (_, SimulationLoop first) = BuildLoop();
        (_, SimulationLoop second) = BuildLoop();

        first.Run(50);
        second.Run(50);

        for (ulong id = 1; id <= 4; id++)
        {
            MindState a = first.Cognition.MindOf(id);
            MindState b = second.Cognition.MindOf(id);
            Assert.Equal(a.Needs.Hunger, b.Needs.Hunger, 10);
            Assert.Equal(a.Needs.Thirst, b.Needs.Thirst, 10);
            Assert.Equal(a.Intention, b.Intention);
            Assert.Equal(a.Memory.Count, b.Memory.Count);
        }
    }

    [Fact]
    public void Movement_NeverEntersObstacleInterior()
    {
        var world = new WorldType(new WorldSize(500, 500));
        var obstacle = new Obstacle("îlot", new Position(250, 250), radius: 20);
        world.AddObstacle(obstacle);

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(99);
        for (ulong i = 1; i <= 30; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            if (entity.Position.DistanceTo(obstacle.Position) <= obstacle.Radius)
            {
                continue;
            }

            world.AddEntity(entity);
        }

        var loop = new SimulationLoop(world, rng, Options());
        loop.Run(300);

        foreach (Entity entity in world.Entities)
        {
            Assert.True(entity.Position.DistanceTo(obstacle.Position) >= obstacle.Radius);
        }
    }
}