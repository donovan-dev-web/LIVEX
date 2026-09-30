using System.Globalization;
using System.Text;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Non-régression du jalon ADR cognitifs (engineVersion 0.14.0) : avec les
/// drapeaux D7/D8/D3/D5/D2 **désactivés** (défaut), la trajectoire bit-à-bit
/// du scénario de référence est inchangée — le checksum 0x46769cfb11c8b3a7
/// (pinné depuis la calibration D1, engineVersion 0.13.0) est conservé, seul
/// le pin de version a bougé. Puis vérification que les drapeaux **activés**
/// changent la trajectoire (les nouvelles mécaniques sont bien branchées).
/// </summary>
public class AdrDeterminismNeutralTests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static SimulationLoop BuildScenario(ulong seed, SimulationOptions? options = null)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        world.AddObstacle(new Obstacle("rocher-1", new Position(250, 150), radius: 15));
        world.AddObstacle(new Obstacle("rocher-2", new Position(420, 380), radius: 25));

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= 50; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options ?? ConfigLoader.LoadDefaults());
    }

    private static string RunLog(SimulationLoop loop, int ticks)
    {
        var log = new StringBuilder();
        for (int tick = 1; tick <= ticks; tick++)
        {
            loop.AdvanceOneTick();
            log.Append(CultureInfo.InvariantCulture, $"t={tick};pop={loop.World.Entities.Count()};");
            foreach (Simulation.Core.Entities.Entity entity in loop.World.Entities.OrderBy(e => e.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = loop.Cognition.MindOf(entity.Id.Value);
                log.Append(CultureInfo.InvariantCulture,
                    $"{entity.Id.Value}:{entity.Position.X:0.00},{entity.Position.Y:0.00};E={mind.Needs.Energy:0.00};");
                log.Append(CultureInfo.InvariantCulture, $"I={mind.Intention?.Kind.ToString() ?? "None"};");
                if (mind.Inventory is { } inventory)
                {
                    log.Append(CultureInfo.InvariantCulture, $"INV={inventory.CurrentWeight:0.00};");
                }
            }
        }

        return log.ToString();
    }

    private static ulong Fnv1a(string text)
    {
        ulong hash = FnvOffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash = (hash ^ b) * FnvPrime;
        }

        return hash;
    }

    [Fact]
    public void FlagsDisabled_NewMechanicsStayInert()
    {
        // Le checksum de référence du scénario (0x46769cfb11c8b3a7, pinné depuis
        // la calibration D1) est conservé — assertion portée par
        // DeterminismRegressionTests.GoldenChecksum_IsPinned. Ici : inertie
        // comportementale des nouvelles mécaniques quand les drapeaux sont éteints.
        SimulationLoop loop = BuildScenario(seed: 7);
        RunLog(loop, ticks: 200);

        foreach (Simulation.Core.Entities.Entity entity in loop.World.Entities)
        {
            Simulation.Core.Cognition.MindState mind = loop.Cognition.MindOf(entity.Id.Value);
            Assert.Null(mind.Inventory);                       // D8 éteint
            Assert.Empty(mind.Commitments);                    // D5 éteint
            Assert.Equal(0.0, mind.LastSalienceScore, 10);     // D2 éteint
            Assert.False(mind.SkippedBySalience);
            Assert.NotEqual(DesireKind.Take, mind.Intention?.Kind);   // D7 jamais choisi
            Assert.NotEqual(DesireKind.Give, mind.Intention?.Kind);
            Assert.NotEqual(DesireKind.Trade, mind.Intention?.Kind);
            Assert.NotEqual(DesireKind.Attack, mind.Intention?.Kind);
            Assert.NotEqual(DesireKind.Defend, mind.Intention?.Kind);
        }
    }

    [Fact]
    public void FlagsEnabled_ChangesTrajectory()
    {
        SimulationOptions enabled = ConfigLoader.LoadDefaults();
        enabled.Agents.Actions.Inventory.Enabled = true;
        enabled.Agents.Actions.Plans.Enabled = true;
        enabled.Agents.Actions.Salience.Enabled = true;
        enabled.Agents.Actions.Commitments.Enabled = true;

        string enabledLog = RunLog(BuildScenario(seed: 7, enabled), ticks: 120);
        string disabledLog = RunLog(BuildScenario(seed: 7), ticks: 120);

        Assert.NotEqual(Fnv1a(disabledLog), Fnv1a(enabledLog));
        // L'inventaire produit un état observable nouveau (poids porté > 0 dès
        // qu'une primitive Take est exécutée).
        Assert.Contains("INV=", enabledLog);
    }

    [Fact]
    public void SameSeedAndFlags_RunIdentically()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Agents.Actions.Inventory.Enabled = true;
        options.Agents.Actions.Plans.Enabled = true;

        string first = RunLog(BuildScenario(seed: 42, options), ticks: 100);
        string second = RunLog(BuildScenario(seed: 42, options), ticks: 100);
        Assert.Equal(first, second);
    }
}
