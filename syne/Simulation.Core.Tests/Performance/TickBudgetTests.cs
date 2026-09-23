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
/// Budgets par tick (SYNE-090, PERFORMANCE.md §3/§9) : la collecte mesure chaque
/// sous-système (perception, mémoire/croyances, besoins/objectifs, décision,
/// actions, communication, événements/groupe/population) et le temps total du
/// tick. La part de computation (Σ phases / tick) doit rester ≥ 30 % — le reste
/// est de l'allocation/GC/overhead. La collecte ne doit jamais altérer la
/// trajectoire (déterminisme, DETERMINISM.md §3).
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
            log.AppendLine();
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

    [Fact]
    public void GoldenChecksum_IsUnaffectedByInstrumentation()
    {
        // La collecte de budgets (et le pooling de perception) ne touchent pas la
        // trajectoire : le checksum doré épinglé reste 0x27fad50065d8c4a4 même
        // quand la boucle est instrumentée. Scénario = copie exacte de
        // DeterminismRegressionTests (rochers, 25 entités, 200 ticks, same log).
        SimulationLoop instrumented = BuildGolden(seed: 12345, TickBudgetCollector.CreateEnabled());

        ulong hash = FnvOffsetBasis;
        var log = new StringBuilder();
        for (int tick = 1; tick <= 200; tick++)
        {
            instrumented.AdvanceOneTick();
            foreach (Entity entity in instrumented.World.Entities.OrderBy(e => e.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = instrumented.Cognition.MindOf(entity.Id.Value);
                log.Append(tick.ToString(CultureInfo.InvariantCulture));
                log.Append(';');
                log.Append(entity.Id.Value);
                log.Append(';');
                log.Append(mind.Memory.Recall((ulong)tick).Count.ToString(CultureInfo.InvariantCulture));
                log.Append(';');
                log.Append(mind.Intention?.Kind ?? Simulation.Core.Cognition.DesireKind.Idle);
                log.Append(';');
                foreach (Simulation.Core.Cognition.MemoryRecall recall in mind.Memory.Recall(
                    Simulation.Core.Cognition.MemoryCategory.Observation,
                    (ulong)tick).Take(8))
                {
                    log.Append(recall.Entry.Content);
                    log.Append('|');
                }

                log.AppendLine();
            }

            hash = Fnv1a(log.ToString());
        }

        Assert.Equal("0x27fad50065d8c4a4", $"0x{hash:x16}");
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