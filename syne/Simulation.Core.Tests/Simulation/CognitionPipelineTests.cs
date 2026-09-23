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
    public void Deliberation_DrivesTerminalDrinkOnceThirstTriggered()
    {
        // SYNE-042 : soif déclenchée dès 50 (décision n°4) → l'entité boit quand
        // c'est viable ; la boisson consomme la réserve globale d'eau.
        // (mortalité désactivée : le scénario teste la soif, pas le cycle de vie)
        SimulationOptions options = Options();
        options.Agents.Life.DeathEnabled = false;
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(MakeEntity(1, new Position(50, 50)));
        world.AddEntity(MakeEntity(2, new Position(80, 50)));
        world.AddEntity(MakeEntity(3, new Position(300, 300)));
        world.AddEntity(MakeEntity(4, new Position(50, 90)));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), options);
        loop.Run(500);

        MindState mind = loop.Cognition.MindOf(1);
        Assert.NotNull(mind.LastDecision);

        // La soif a été éteinte par Drink (réserve mise à jour, décision n°4) :
        // la réserve globale d'eau a été consommée mais reste non vide.
        Assert.True(loop.Resources.Stock(World.ResourceKind.Water) >= 0.0);
        Assert.True(loop.Resources.Stock(World.ResourceKind.Water) < 1000.0);
    }

    [Fact]
    public void StopDrinking_WhenWaterReserveExhausts_FallsBackToSeek()
    {
        // SYNE-042 : réserve d'eau vide → plus de Drink viable → poursuite SeekWater.
        SimulationOptions options = Options();
        options.Resources.Water.Initial = 0;

        WorldType world = new(new WorldSize(500, 500));
        world.AddEntity(MakeEntity(1, new Position(50, 50)));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), options);

        loop.Run(120);

        MindState mind = loop.Cognition.MindOf(1);
        Assert.Equal(DesireKind.SeekWater, mind.Intention!.Value.Kind);
    }

    [Fact]
    public void PerceptionAndDecisions_AreDeterministicAcrossTwoRuns()
    {
        (_, SimulationLoop first) = BuildLoop();
        (_, SimulationLoop second) = BuildLoop();

        first.Run(50);
        second.Run(50);

        // Comparaison des survivants seulement (SYNE-074 : la mortalité est
        // déterministe elle aussi) — mêmes survivants, mêmes états.
        List<ulong> firstAlive = first.World.Entities.Select(e => e.Id.Value).OrderBy(id => id).ToList();
        List<ulong> secondAlive = second.World.Entities.Select(e => e.Id.Value).OrderBy(id => id).ToList();
        Assert.Equal(firstAlive, secondAlive);

        foreach (ulong id in firstAlive)
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

    private static (WorldType World, SimulationLoop Loop) BuildSingleLoop(SimulationOptions options)
    {
        var world = new WorldType(new WorldSize(500, 500));
        world.AddEntity(MakeEntity(1, new Position(50, 50)));
        var loop = new SimulationLoop(world, Xoshiro256StarStar.Create(7), options);
        return (world, loop);
    }

    [Fact]
    public void DeliberationFrequency_IsConfigurable()
    {
        // SYNE-031 : intervalle 2 → l'entité 1 délibère aux ticks impairs (décision n°14).
        SimulationOptions options = Options();
        options.Agents.Actions.Deliberation.IntervalTicks = 2;

        (_, SimulationLoop loop) = BuildSingleLoop(options);
        var booleans = new List<bool>();
        for (int tick = 1; tick <= 6; tick++)
        {
            loop.AdvanceOneTick();
            booleans.Add(loop.Cognition.MindOf(1).DeliberatedThisTick);
        }

        Assert.Equal([true, false, true, false, true, false], booleans);
    }

    [Fact]
    public void Holdover_KeepsIntentionBetweenDeliberations()
    {
        // SYNE-031 : entre deux délibérations (intervalle 10, entité 1), l'intention est conservée.
        (_, SimulationLoop loop) = BuildSingleLoop(Options());
        for (int tick = 1; tick <= 87; tick++)
        {
            loop.AdvanceOneTick();
        }

        MindState mind = loop.Cognition.MindOf(1);

        // tick 87–88 : hors délibération ((87+1) % 10 != 0) — intention Drink conservée
        // depuis la dernière délibération (soif déclenchée ≥ 50, décision n°4).
        Assert.False(mind.DeliberatedThisTick);
        Assert.Equal(DesireKind.Drink, mind.Intention?.Kind);

        loop.AdvanceOneTick();
        Assert.False(mind.DeliberatedThisTick);
        Assert.Equal(DesireKind.Drink, mind.Intention?.Kind);

        // tick 89 : délibération — soif toujours ≥ 50 → Drink conservé.
        loop.AdvanceOneTick();
        Assert.True(mind.DeliberatedThisTick);
        Assert.Equal(DesireKind.Drink, mind.Intention?.Kind);
    }

    [Fact]
    public void Interruption_OverridesHoldoverForCriticalNeed()
    {
        // SYNE-032/043 : faim critique (seuil 85) + utilité supérieure de > marge (10)
        // pendant une non-délibération → reprise par interruption. Réserve disponible →
        // le déclencheur centralisé répond par l'action terminale Eat (SYNE-042).
        SimulationOptions options = Options();
        options.Agents.Needs.HungerRate = 5;
        options.Agents.Needs.ThirstRate = 0;
        options.Agents.Needs.FatigueRate = 0;
        options.Agents.Needs.SafetyDriftRate = 0;
        options.Agents.Needs.SocialDriftRate = 0;
        options.Agents.Needs.CuriosityDriftRate = 0;

        (_, SimulationLoop loop) = BuildSingleLoop(options);
        for (int tick = 1; tick <= 17; tick++)
        {
            loop.AdvanceOneTick();
        }

        MindState mind = loop.Cognition.MindOf(1);

        // tick 17 : faim = 85, seuil STRICT (>) non atteint → pas d'interruption.
        Assert.False(mind.InterruptedThisTick);

        // tick 18 : faim = 90 > 85, hors délibération → interruption vers Eat (réserve pleine).
        loop.AdvanceOneTick();
        Assert.True(mind.InterruptedThisTick);
        Assert.False(mind.DeliberatedThisTick);
        Assert.Equal(DesireKind.Eat, mind.Intention?.Kind);
        Assert.NotNull(mind.LastDecisionRecord);
        Assert.True(mind.LastDecisionRecord!.Interrupted);
        Assert.False(mind.LastDecisionRecord.Deliberated);
    }

    [Fact]
    public void Interruption_FallsBackToSeekWhenNoReserve()
    {
        // SYNE-043 : réserve vide → le déclencheur centralisé répond à la faim
        // critique par la poursuite SeekFood (action terminale non viable).
        SimulationOptions options = Options();
        options.Agents.Needs.HungerRate = 5;
        options.Agents.Needs.ThirstRate = 0;
        options.Agents.Needs.FatigueRate = 0;
        options.Agents.Needs.SafetyDriftRate = 0;
        options.Agents.Needs.SocialDriftRate = 0;
        options.Agents.Needs.CuriosityDriftRate = 0;
        options.Resources.Food.Initial = 0;

        (_, SimulationLoop loop) = BuildSingleLoop(options);
        for (int tick = 1; tick <= 18; tick++)
        {
            loop.AdvanceOneTick();
        }

        MindState mind = loop.Cognition.MindOf(1);
        Assert.True(mind.InterruptedThisTick);
        Assert.Equal(DesireKind.SeekFood, mind.Intention?.Kind);
    }

    [Fact]
    public void DecisionRecord_ExposesFullTrace()
    {
        // SYNE-030/031 : chaque décision produit une trace complète (COGNITIVE_ARCHITECTURE.md §7).
        (_, SimulationLoop loop) = BuildSingleLoop(Options());
        loop.Run(100);

        MindState mind = loop.Cognition.MindOf(1);
        Assert.NotNull(mind.LastDecisionRecord);
        Assert.True(mind.LastDecisionRecord!.Tick > 0);
        Assert.Equal(1UL, mind.LastDecisionRecord.EntityId);
        Assert.True(mind.LastDecisionScores.Count > 0);
        Assert.NotNull(mind.LastDecision);
    }
}