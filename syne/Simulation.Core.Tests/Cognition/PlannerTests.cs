// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du planificateur intégré, de son cache et de la replanification (SYNE-223).

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests;

public class PlannerTests
{
    private static GoalTarget NodeTarget(double x = 250, double y = 100, string label = "resource-food-test") =>
        new("node", 7, label, new Position(x, y), ObservedTick: 0, Confidence: 0.9);

    private static Plan Build(DesireKind kind, GoalTarget? target, int maxSteps = 6) =>
        Planner.Build(kind, target, maxSteps);

    [Fact]
    public void Build_AppendsTheTerminalStepToAPursuitWithATarget()
    {
        Plan plan = Build(DesireKind.SeekFood, NodeTarget());

        Assert.Equal(2, plan.Count);
        Assert.Equal(DesireKind.SeekFood, plan.Steps[0].Kind);
        Assert.Equal(DesireKind.Eat, plan.Steps[1].Kind);
        Assert.Equal("node:resource-food-test", plan.Steps[0].Label);
        Assert.Equal("SeekFood@node:resource-food-test", plan.Key);
    }

    [Fact]
    public void Build_KeepsASingleStepWhenThereIsNothingToDecompose()
    {
        Plan pursuit = Build(DesireKind.SeekWater, target: null);
        Plan terminal = Build(DesireKind.Eat, NodeTarget());

        Assert.Equal(DesireKind.SeekWater, Assert.Single(pursuit.Steps).Kind);
        Assert.Equal("SeekWater@-", pursuit.Key);
        Assert.Equal(DesireKind.Eat, Assert.Single(terminal.Steps).Kind);
    }

    [Fact]
    public void Build_TruncatesToMaxSteps()
    {
        Plan plan = Build(DesireKind.SeekFood, NodeTarget(), maxSteps: 1);

        Assert.Equal(DesireKind.SeekFood, Assert.Single(plan.Steps).Kind);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Planner.Build(DesireKind.SeekFood, NodeTarget(), maxSteps: 0));
    }

    [Fact]
    public void Cache_ServesAPlanOnceStored()
    {
        var cache = new PlanCache(capacity: 4);
        Plan plan = Build(DesireKind.SeekFood, NodeTarget());

        Assert.False(cache.TryGet(plan.Key, out _));
        cache.Put(plan);

        Assert.True(cache.TryGet(plan.Key, out Plan cached));
        Assert.Same(plan, cached);
        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);
    }

    [Fact]
    public void Cache_EvictsTheLeastRecentlyUsedPlan()
    {
        var cache = new PlanCache(capacity: 2);
        Plan a = Build(DesireKind.SeekFood, NodeTarget(x: 10, label: "node-a"));
        Plan b = Build(DesireKind.SeekFood, NodeTarget(x: 20, label: "node-b"));
        Plan c = Build(DesireKind.SeekWater, NodeTarget(x: 30, label: "node-c"));
        cache.Put(a);
        cache.Put(b);

        // a redevient le plus récent : c'est b qui cède la place.
        Assert.True(cache.TryGet(a.Key, out _));
        cache.Put(c);

        Assert.True(cache.TryGet(a.Key, out _));
        Assert.False(cache.TryGet(b.Key, out _));
        Assert.Equal(1, cache.Evictions);
    }

    [Fact]
    public void Cache_InvalidateRemovesThePlan()
    {
        var cache = new PlanCache(capacity: 2);
        Plan plan = Build(DesireKind.SeekFood, NodeTarget());
        cache.Put(plan);

        Assert.True(cache.Invalidate(plan.Key));
        Assert.False(cache.Invalidate(plan.Key));
        Assert.False(cache.TryGet(plan.Key, out _));
        Assert.Equal(1, cache.Invalidations);
    }

    [Fact]
    public void Cache_WithZeroCapacityNeverStores()
    {
        var cache = new PlanCache(capacity: 0);
        Plan plan = Build(DesireKind.SeekFood, NodeTarget());

        cache.Put(plan);

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet(plan.Key, out _));
    }

    private static (SimulationLoop Loop, Entity Entity) LocalizedLoop(bool planner, WorldType world)
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Agents.Planner.Enabled = planner;
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(4242);
        (Entity entity, Xoshiro256StarStar next) =
            EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, 1, bornAt: 0);
        world.AddEntity(entity);
        return (new SimulationLoop(world, next, options), entity);
    }

    private static WorldType HungryWorld()
    {
        var world = new WorldType(new WorldSize(500, 500));
        world.AddResourceNode(new ResourceNode(
            "resource-food-test", ResourceKind.Food, new Position(250, 100),
            amount: 50.0, capacity: 100.0, regenerationRate: 0.0));
        world.AddResourceNode(new ResourceNode(
            "resource-water-test", ResourceKind.Water, new Position(250, 100),
            amount: 50.0, capacity: 100.0, regenerationRate: 0.0));
        return world;
    }

    [Fact]
    public void APursuitIsSatisfiedOnlyWithinInteractionReach()
    {
        GoalTarget target = NodeTarget(x: 250, y: 100);
        var pursuit = new PlanStep(DesireKind.SeekFood, target.Canonical);
        var terminal = new PlanStep(DesireKind.Eat, target.Canonical);

        Assert.True(Planner.IsSatisfied(pursuit, target, new Position(250, 100), interactionRadius: 2.0));
        Assert.True(Planner.IsSatisfied(pursuit, target, new Position(251, 100), interactionRadius: 2.0));
        Assert.False(Planner.IsSatisfied(pursuit, target, new Position(300, 100), interactionRadius: 2.0));
        // Une action terminale ne se franchit pas par la position : elle s'exécute.
        Assert.False(Planner.IsSatisfied(terminal, target, new Position(250, 100), interactionRadius: 2.0));
        // Sans cible, aucune poursuite n'est franchie (elle reste l'étape courante).
        Assert.False(Planner.IsSatisfied(pursuit, target: null, new Position(250, 100), interactionRadius: 2.0));
    }

    [Fact]
    public void EnabledPlannerBuildsAndCachesAPlan()
    {
        WorldType world = HungryWorld();
        (SimulationLoop loop, Entity entity) = LocalizedLoop(planner: true, world);
        // L'esprit est créé au premier pas de la boucle ; c'est ensuite que les
        // besoins du scénario sont posés (état initial identique dans les deux cas).
        loop.AdvanceOneTick();
        loop.Cognition.MindOf(entity.Id.Value).Needs.RestoreState(
            hunger: 85.0, thirst: 0.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 60.0);

        for (int tick = 0; tick < 30; tick++)
        {
            loop.AdvanceOneTick();
        }

        // Le planificateur produit et met en cache le plan de l'intention courante
        // (la consommation effective dépend de la chaîne de perception, couverte par
        // les tests d'actions localisées).
        Assert.NotNull(loop.Cognition.PlanCache);
        Assert.True(loop.Cognition.PlanCache!.Hits + loop.Cognition.PlanCache.Count > 0);
        Assert.Equal(0, loop.Cognition.PlanReplans);
    }

    [Fact]
    public void DisabledPlannerLeavesTheTrajectoryIdentical()
    {
        WorldType offWorld = HungryWorld();
        WorldType onWorld = HungryWorld();
        (SimulationLoop off, Entity offEntity) = LocalizedLoop(planner: false, offWorld);
        (SimulationLoop on, Entity onEntity) = LocalizedLoop(planner: true, onWorld);
        foreach (SimulationLoop loop in new[] { off, on })
        {
            loop.AdvanceOneTick();
            Entity target = loop == off ? offEntity : onEntity;
            loop.Cognition.MindOf(target.Id.Value).Needs.RestoreState(
                hunger: 85.0, thirst: 70.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 60.0);
        }

        for (int tick = 0; tick < 25; tick++)
        {
            off.AdvanceOneTick();
            on.AdvanceOneTick();

            Assert.Equal(offEntity.Position, onEntity.Position);
            Assert.Equal(
                off.Cognition.MindOf(offEntity.Id.Value).Needs.Hunger,
                on.Cognition.MindOf(onEntity.Id.Value).Needs.Hunger,
                10);
            Assert.Equal(
                off.Cognition.MindOf(offEntity.Id.Value).Needs.Thirst,
                on.Cognition.MindOf(onEntity.Id.Value).Needs.Thirst,
                10);
        }
    }

    [Fact]
    public void ValidationRejectsImpossiblePlannerSettings()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Agents.Planner.MaxPlanSteps = 0;
        options.Agents.Planner.CacheCapacity = -1;

        IReadOnlyList<string> errors = SimulationOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("agents.planner.maxPlanSteps", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("agents.planner.cacheCapacity", StringComparison.Ordinal));
    }
}
