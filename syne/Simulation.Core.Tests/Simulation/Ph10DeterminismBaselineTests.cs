using System.Globalization;
using System.Text;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Non-régression de déterminisme à l'échelle de la **boucle complète** (SYNE-102,
/// jalon ph10) : même seed + config ⇒ même trajectoire **bit-à-bit** sur l'état
/// observable complet (positions, énergie, besoins, intention, mémoire, relations,
/// communication, groupes, naissances, mortalité) — et divergence dès que la seed
/// change. Le journal d'état est construit en partie canonique (culture invariante,
/// tris par identifiant), cf. DETERMINISM.md §6/§7.
/// </summary>
public class Ph10DeterminismBaselineTests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static SimulationLoop BuildScenario(ulong seed, ulong entityCount)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        world.AddObstacle(new Obstacle("rocher-1", new Position(250, 150), radius: 15));
        world.AddObstacle(new Obstacle("rocher-2", new Position(420, 380), radius: 25));

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= entityCount; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, ConfigLoader.LoadDefaults());
    }

    private static string Rounded(double value) =>
        Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Journal canonique d'état complet : en-tête par tick (population, envois de
    /// messages, groupes actifs, naissances, décès) puis état de chaque entité
    /// vivante (id, position, énergie, besoins, intention, mémoire, confiance).
    /// </summary>
    private static string BuildStateLog(SimulationLoop loop, int ticks)
    {
        var log = new StringBuilder();
        for (int tick = 1; tick <= ticks; tick++)
        {
            loop.AdvanceOneTick();
            log.Append($"t={tick};pop={loop.World.Entities.Count()};");
            log.Append($"sent={loop.Cognition.Communication.LastSent.Count};");
            log.Append($"groups={loop.Cognition.Groups.Active.Count};");
            log.Append($"births={loop.Cognition.Birth.LastBirths.Count};");
            log.Append($"deaths={loop.Cognition.Death.LastDeaths.Count}");
            log.AppendLine();
            foreach (Entity entity in loop.World.Entities.OrderBy(entity => entity.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = loop.Cognition.MindOf(entity.Id.Value);
                log.Append(entity.Id.Value);
                log.Append(';');
                log.Append(Rounded(entity.Position.X));
                log.Append(';');
                log.Append(Rounded(entity.Position.Y));
                log.Append(';');
                log.Append(Rounded(mind.Needs.Energy));
                log.Append(';');
                log.Append(Rounded(mind.Needs.Hunger));
                log.Append(';');
                log.Append(Rounded(mind.Needs.Thirst));
                log.Append(';');
                log.Append(mind.Intention?.Kind ?? Simulation.Core.Cognition.DesireKind.Idle);
                log.Append(';');
                log.Append(mind.Memory.Recall((ulong)tick).Count.ToString(CultureInfo.InvariantCulture));
                log.Append(';');
                log.Append(mind.Trust.Count.ToString(CultureInfo.InvariantCulture));
                log.Append(';');
                log.Append(mind.Beliefs.Count.ToString(CultureInfo.InvariantCulture));
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
    public void FullPipeline_SameSeed_IsBitForBitReproducible()
    {
        foreach (ulong seed in new ulong[] { 12345, 7, 999 })
        {
            string first = BuildStateLog(BuildScenario(seed, entityCount: 25), ticks: 200);
            string second = BuildStateLog(BuildScenario(seed, entityCount: 25), ticks: 200);

            Assert.Equal(first, second);
            Assert.Equal(Fnv1a(first), Fnv1a(second));
        }
    }

    [Fact]
    public void FullPipeline_DifferentSeed_Diverges()
    {
        string seededA = BuildStateLog(BuildScenario(12345, entityCount: 25), ticks: 150);
        string seededB = BuildStateLog(BuildScenario(12346, entityCount: 25), ticks: 150);

        Assert.NotEqual(seededA, seededB);
    }

    [Fact]
    public void FullPipeline_StateBaseline_IsPinned()
    {
        // Épinglé au jalon ph10 (U7) : baseline d'état complet (boucle entière,
        // populations 25, 200 ticks, seed 12345 — même scénario que le checksum doré
        // de la perception, DETERMINISM.md §7) — toute altération bit-à-bit de la
        // trajectoire change ce checksum et impose un recalcul + bump MINOR.
        string log = BuildStateLog(BuildScenario(12345, entityCount: 25), ticks: 200);
        Assert.Equal("0x072a488aa18c05eb", $"0x{Fnv1a(log):x16}");
    }
}