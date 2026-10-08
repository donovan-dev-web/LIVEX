using System.Diagnostics;
using System.Globalization;
using System.Text;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Cibles de débit aux échelles 50/500/1000 entités (SYNE-091, décision n°30,
/// PERFORMANCE.md §9). Le test CI n'asserte PAS les objectifs finaux (≥ 30/20/10
/// t/s, mesurés à la machine de référence via le mode CLI --benchmark) mais des
/// planchers anti-régression très en-deçà des mesures réelles (marge anti-
/// flakiness CI, même convention que PerceptionBenchmarkTests) : une régression
/// d'ordre de grandeur (retour à la perception naïve O(n²), etc.) les casse.
/// Déterminisme à l'échelle (SYNE-093) : même seed ⇒ même checksum, et le
/// pooling/collecte n'altèrent rien.
/// </summary>
public class ScaleTargetsTests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static SimulationLoop Build(ulong seed, ulong count)
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        var world = new Simulation.Core.World.World(WorldSize.From(options), spatialCellSize: options.Agents.Perception.Radius);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= count; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options);
    }

    private static double Throughput(ulong seed, ulong count, int ticks)
    {
        // Meilleur de 5 essais : les mesures de débit en CI sont perturbées par la
        // charge — le max (essai le moins contensionné, proche du taux machine)
        // sert de référence anti-régression (PERFORMANCE.md §9). Cinq essais
        // couvrent plusieurs fenêtres de charge : un run `windows-latest` où les
        // deux assemblées de tests tournaient en parallèle avait vu les trois
        // essais passer sous le plancher sans aucune régression du moteur.
        double best = 0.0;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            SimulationLoop loop = Build(seed, count);
            var sw = Stopwatch.StartNew();
            loop.Run(ticks);
            sw.Stop();
            best = Math.Max(best, ticks / (sw.Elapsed.TotalMilliseconds / 1000.0));
        }

        return best;
    }

    /// <summary>Contrôle de débit : message portant la valeur mesurée (diagnostic CI).</summary>
    private static void AssertAboveFloor(double floor, double measured, string message)
    {
        Assert.True(
            measured >= floor,
            $"{message} Mesuré {measured:F1} t/s, plancher {floor:F0} t/s.");
    }

    [Fact]
    public void FiftyEntities_StayAboveRegressionFloor()
    {
        AssertAboveFloor(120.0, Throughput(12345, 50, 100), "Débit à 50 entités sous le plancher anti-régression.");
    }

    [Fact]
    public void FiveHundredEntities_StayAboveRegressionFloor()
    {
        AssertAboveFloor(30.0, Throughput(12345, 500, 60), "Débit à 500 entités sous le plancher anti-régression.");
    }

    [Fact]
    public void OneThousandEntities_StayAboveRegressionFloor()
    {
        AssertAboveFloor(20.0, Throughput(12345, 1000, 40), "Débit à 1000 entités sous le plancher anti-régression.");
    }

    [Fact]
    public void ScaleChecksum_IsBitForBitReproducible()
    {
        // SYNE-093 : le benchmark est reproductible bit-à-bit à seed égale — ici à
        // l'échelle 500 (scénario à population réaliste V0.1).
        ulong first = ChecksumState(Build(67890, 500), ticks: 30);
        ulong second = ChecksumState(Build(67890, 500), ticks: 30);
        Assert.Equal(first, second);
        Assert.NotEqual(0UL, first);
    }

    private static ulong ChecksumState(SimulationLoop loop, int ticks)
    {
        loop.Run(ticks);
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
}