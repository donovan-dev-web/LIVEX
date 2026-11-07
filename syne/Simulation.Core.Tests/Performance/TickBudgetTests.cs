// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Budgets par tick (SYNE-090) : la collecte mesure chaque sous-système (perception, mémoire/croyances,
//        besoins/objectifs, décision, actions, événements/groupe/population) et le temps total du tick.

using System.Globalization;
using System.Text;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Performance;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Budgets par tick (SYNE-090) : la collecte mesure chaque
/// sous-système (perception, mémoire/croyances, besoins/objectifs, décision,
/// actions, communication, événements/groupe/population) et le temps total du
/// tick. La part de computation (Σ phases / tick) doit rester ≥ 30 % — le reste
/// est de l'allocation/GC/overhead. La collecte ne doit jamais altérer la
/// trajectoire (déterminisme).
/// </summary>
public class TickBudgetTests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static SimulationOptions Options(ulong seed) => ConfigLoader.LoadDefaults();

    private static SimulationLoop Build(ulong seed, ulong count, TickBudgetCollector? budget)
    {
        SimulationOptions options = Options(seed);
        var world = new Simulation.Core.World.World(WorldSize.From(options), spatialCellSize: options.Agents.Perception.Radius);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= count; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options, budget);
    }

    private static ulong ChecksumState(SimulationLoop loop)
    {
        var log = new StringBuilder();
        foreach (Entity entity in loop.World.Entities.OrderBy(e => e.Id.Value))
        {
            log.Append(entity.Id.Value.ToString(CultureInfo.InvariantCulture));
            log.Append(';');
            log.Append(entity.Position.X.ToString("0.00", CultureInfo.InvariantCulture));
            log.Append(';');
            log.Append(entity.Position.Y.ToString("0.00", CultureInfo.InvariantCulture));
            log.Append(';');
            log.Append((loop.Cognition.HasMind(entity.Id.Value)
                    ? loop.Cognition.MindOf(entity.Id.Value).Needs.Energy
                    : 0.0).ToString("0.00", CultureInfo.InvariantCulture));
            log.Append('\n');
        }

        ulong hash = FnvOffsetBasis;
        foreach (byte value in Encoding.UTF8.GetBytes(log.ToString()))
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        return hash;
    }

    [Fact]
    public void BudgetCollection_MeasuresEveryPhaseAndComputesShare()
    {
        TickBudgetCollector budget = TickBudgetCollector.CreateEnabled();
        SimulationLoop loop = Build(seed: 12345, count: 250, budget);
        loop.Run(50);

        TickBudgetSnapshot snap = budget.Snapshot();
        Assert.Equal(50, snap.PipelineSamples);
        Assert.True(snap.MeanPipelineMs > 0.0, "Le tick moyen doit être mesuré.");
        Assert.True(snap.Samples(TickPhase.Communication) >= 50, "La passe de communication a lieu à chaque tick.");
        Assert.True(snap.Samples(TickPhase.Perception) > 0, "La perception doit être mesurée.");
        Assert.True(snap.MeanMs(TickPhase.Perception) >= 0.0);
        Assert.True(
            snap.ComputationShare() >= 0.30,
            $"Part de computation {snap.ComputationShare():P1} < 30 % (PERFORMANCE.md §9).");
    }

    [Fact]
    public void BudgetCollection_DoesNotAlterTrajectory()
    {
        ulong withBudget = ChecksumState(Build(seed: 67890, count: 200, TickBudgetCollector.CreateEnabled()));
        ulong withoutBudget = ChecksumState(Build(seed: 67890, count: 200, budget: null));

        Assert.Equal(withBudget, withoutBudget);
    }

    private static SimulationLoop BuildGolden(ulong seed, TickBudgetCollector? budget)
    {
        SimulationOptions options = Options(seed);
        var world = new Simulation.Core.World.World(WorldSize.From(options), spatialCellSize: 50);
        world.AddObstacle(new Obstacle("rocher-1", new Position(250, 150), radius: 15));
        world.AddObstacle(new Obstacle("rocher-2", new Position(420, 380), radius: 25));

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= 25; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options, budget);
    }

    private static ulong Fnv1a(string text)
    {
        ulong hash = FnvOffsetBasis;
        foreach (byte value in Encoding.UTF8.GetBytes(text))
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        return hash;
    }
}

