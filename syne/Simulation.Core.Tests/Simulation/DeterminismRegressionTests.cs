using System.Globalization;
using System.Text;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Régression de déterminisme bit-à-bit de la perception (SYNE-015, DETERMINISM.md) :
/// même seed + config ⇒ même séquence de perception. Le hash de trajectoire est
/// épinglé (checksum de run, DETERMINISM.md §6).
/// </summary>
public class DeterminismRegressionTests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static SimulationOptions ScenarioOptions(ulong seed) => ConfigLoader.LoadDefaults();

    private static SimulationLoop BuildScenario(ulong populationSeed, ulong entityCount)
    {
        var world = new Simulation.Core.World.World(WorldSize.From(ScenarioOptions(42)), spatialCellSize: 50);
        world.AddObstacle(new Obstacle("rocher-1", new Position(250, 150), radius: 15));
        world.AddObstacle(new Obstacle("rocher-2", new Position(420, 380), radius: 25));

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(populationSeed);
        for (ulong i = 1; i <= entityCount; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, ScenarioOptions(populationSeed));
    }

    private static string BuildPerceptionLog(SimulationLoop loop, int ticks)
    {
        var log = new StringBuilder();
        for (int tick = 1; tick <= ticks; tick++)
        {
            loop.AdvanceOneTick();
            foreach (Entity entity in loop.World.Entities.OrderBy(e => e.Id.Value))
            {
                MindState mind = loop.Cognition.MindOf(entity.Id.Value);
                log.Append(tick.ToString(CultureInfo.InvariantCulture));
                log.Append(';');
                log.Append(entity.Id.Value);
                log.Append(';');
                log.Append(mind.Memory.Recall((ulong)tick).Count.ToString(CultureInfo.InvariantCulture));
                log.Append(';');
                log.Append(mind.Intention?.Kind ?? DesireKind.Idle);
                log.Append(';');
                foreach (MemoryRecall recall in mind.Memory.Recall(MemoryCategory.Observation, (ulong)tick).Take(8))
                {
                    log.Append(recall.Entry.Content);
                    log.Append('|');
                }

                log.AppendLine();
            }
        }

        return log.ToString();
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

    [Fact]
    public void SameSeedAndConfig_ProducesIdenticalPerceptionSequence()
    {
        foreach (ulong seed in new ulong[] { 12345, 42, 999 })
        {
            string first = BuildPerceptionLog(BuildScenario(seed, entityCount: 20), ticks: 150);
            string second = BuildPerceptionLog(BuildScenario(seed, entityCount: 20), ticks: 150);

            Assert.Equal(first, second);
            Assert.Equal(Fnv1a(first), Fnv1a(second));
        }
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentTrajectory()
    {
        string seededA = BuildPerceptionLog(BuildScenario(12345, entityCount: 20), ticks: 100);
        string seededB = BuildPerceptionLog(BuildScenario(12346, entityCount: 20), ticks: 100);

        Assert.NotEqual(seededA, seededB);
    }

    [Fact]
    public void GoldenChecksum_IsPinned()
    {
        // Épinglé au jalon SYNE ph5 (engineVersion 0.4.0) : pulsations publiques
        // (SYNE-050), coûts émission/réception (SYNE-052) et relais avec
        // dégradation de confiance × 0.9/hop (SYNE-053) altèrent la trajectoire —
        // DETERMINISM.md §7 impose recalcul + bump MINOR.
        string log = BuildPerceptionLog(BuildScenario(12345, entityCount: 25), ticks: 200);
        Assert.Equal("0x6aa2b2d87b32a8a5", $"0x{Fnv1a(log):x16}");
    }
}