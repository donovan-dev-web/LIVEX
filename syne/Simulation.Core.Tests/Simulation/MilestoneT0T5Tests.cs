using System.Globalization;
using System.Reflection;
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
/// Tests de jalon transverse T0–T5 (SYNE-122, ROADMAP.md §7 « Jalon U8 ») :
/// <list type="bullet">
/// <item><b>T0</b> — 50 entités, 1000 ticks : aucun crash, état valide et
/// déterministe à l'échelle ;</item>
/// <item><b>T1</b> — 50 entités, 2000 ticks : les croyances divergent entre les
/// entités (expériences différentes ⇒ croyances différentes, décision n°14) ;</item>
/// <item><b>T2</b> — des traits différents produisent des décisions différentes
/// dans une situation strictement identique (même seed, même position, mêmes
/// besoins) ;</item>
/// <item><b>T3</b> — l'information reste locale : une entité ne peut connaitre
/// que ce qu'elle perçoit ou reçoit dans son voisinage (jamais au-delà) ;</item>
/// <item><b>T4</b> — reproductibilité du benchmark à l'échelle du jalon (la
/// mesure des objectifs de débit est couverte par ScaleTargetsTests + le mode
/// CLI --benchmark, PERFORMANCE.md §9) ;</item>
/// <item><b>T5</b> — la suite dépasse la barre du jalon (&gt; 160 tests,
/// couverture ≥ 80 %, cf. TESTING.md).</item>
/// </list>
/// </summary>
public class MilestoneT0T5Tests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static SimulationOptions OptionsNoDeath()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Agents.Life.DeathEnabled = false;
        return options;
    }

    /// <summary>
    /// Monde à <paramref name="count"/> entités (positions/traits tirés PRNG
    /// déterministe, même philosophie que Ph10DeterminismBaselineTests).
    /// </summary>
    private static SimulationLoop Build(ulong seed, int count, EntityTemplate? template = null, SimulationOptions? options = null)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= (ulong)count; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(template ?? EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, options ?? ConfigLoader.LoadDefaults());
    }

    /// <summary>Template : tout trait neutre sauf <paramref name="trait"/> fixé exactement à <paramref name="value"/>.</summary>
    private static EntityTemplate TemplateWithFixedTrait(string trait, double value)
    {
        var ranges = new Dictionary<string, (double Min, double Max)>(StringComparer.Ordinal);
        foreach (string name in TraitSet.TraitNames)
        {
            ranges[name] = name == trait ? (value, value) : (TraitSet.Neutral, TraitSet.Neutral);
        }

        return new EntityTemplate("Entité A", ranges);
    }

    /// <summary>Empreinte d'un paquet de croyances (sujet, prédicat, valeur, confiance) en partie canonique.</summary>
    private static string BeliefFingerprint(MindState mind)
    {
        var builder = new StringBuilder();
        foreach (Belief belief in mind.Beliefs.OrderedByFact())
        {
            builder.Append(belief.Fact.Subject);
            builder.Append('|');
            builder.Append(belief.Fact.Predicate);
            builder.Append('|');
            builder.Append(belief.Fact.Value);
            builder.Append('|');
            builder.Append(belief.Confidence.ToString("R", CultureInfo.InvariantCulture));
            builder.Append(';');
        }

        return builder.ToString();
    }

    private static string PositionFingerprint(SimulationLoop loop)
    {
        var builder = new StringBuilder();
        foreach (Entity entity in loop.World.Entities.OrderBy(entity => entity.Id.Value))
        {
            builder.Append(entity.Id.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(';');
            builder.Append(entity.Position.X.ToString("R", CultureInfo.InvariantCulture));
            builder.Append(';');
            builder.Append(entity.Position.Y.ToString("R", CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    // ------------------------------------------------------------------
    // T0 — 50 entités, 1000 ticks : aucun crash, état valide, déterminisme.
    // ------------------------------------------------------------------

    [Fact]
    public void T0_FiftyEntitiesThousandTicks_NoCrashAndValidState()
    {
        SimulationLoop loop = Build(seed: 12347, count: 50, options: OptionsNoDeath());
        loop.Run(1000);

        Assert.Equal(1000UL, loop.CurrentTick);
        var seen = new HashSet<ulong>();
        foreach (Entity entity in loop.World.Entities)
        {
            Assert.True(seen.Add(entity.Id.Value), "Id d'entité dupliqué.");
            Assert.InRange(entity.Position.X, 0.0, 500.0);
            Assert.InRange(entity.Position.Y, 0.0, 500.0);
            MindState mind = loop.Cognition.MindOf(entity.Id.Value);
            Assert.False(double.IsNaN(mind.Needs.Energy), "Énergie NaN.");
            Assert.InRange(mind.Needs.Energy, 0.0, 100.0);
        }
    }

    [Fact]
    public void T0_FiftyEntitiesThousandTicks_StateIsDeterministic()
    {
        SimulationLoop first = Build(seed: 12347, count: 50, options: OptionsNoDeath());
        first.Run(1000);
        SimulationLoop second = Build(seed: 12347, count: 50, options: OptionsNoDeath());
        second.Run(1000);

        string firstFingerprint = PositionFingerprint(first);
        string secondFingerprint = PositionFingerprint(second);

        Assert.Equal(firstFingerprint, secondFingerprint);
        Assert.NotEqual(0UL, Fnv1a(firstFingerprint));
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

    // ------------------------------------------------------------------
    // T1 — 50 entités, 2000 ticks : croyances divergentes.
    // ------------------------------------------------------------------

    [Fact]
    public void T1_FiftyEntitiesTwoThousandTicks_BeliefsDiverge()
    {
        SimulationLoop loop = Build(seed: 94117, count: 50, options: OptionsNoDeath());
        loop.Run(2000);

        var fingerprints = new Dictionary<string, int>(StringComparer.Ordinal);
        int totalBeliefs = 0;
        foreach (Entity entity in loop.World.Entities)
        {
            MindState mind = loop.Cognition.MindOf(entity.Id.Value);
            totalBeliefs += mind.Beliefs.Count;
            string fingerprint = BeliefFingerprint(mind);
            fingerprints[fingerprint] = fingerprints.GetValueOrDefault(fingerprint) + 1;
        }

        Assert.True(totalBeliefs > 0, "Aucune croyance acquise après 2000 ticks : la perception n'aurait rien appris.");
        Assert.True(fingerprints.Count > 1,
            $"Les 50 entités partagent toutes la même carte de croyances après 2000 ticks ({totalBeliefs} croyances totales) : les expériences différentes auraient dû diverger.");
    }

    // ------------------------------------------------------------------
    // T2 — traits différents ⇒ décisions différentes (situation identique).
    // ------------------------------------------------------------------

    [Fact]
    public void T2_DifferentTraits_SameSeedSameSituation_ProduceDifferentDecisions()
    {
        // Situation strictement identique : même seed, même monde, même position,
        // mêmes autres traits — seule la curiosité (trait plage [2,2] contre [0,0])
        // diffère. L'expérience contrôlée isole donc l'effet du trait.
        SimulationLoop low = Build(seed: 777, count: 1, template: TemplateWithFixedTrait("curiosity", 0.0), options: OptionsNoDeath());
        SimulationLoop high = Build(seed: 777, count: 1, template: TemplateWithFixedTrait("curiosity", 2.0), options: OptionsNoDeath());

        Entity lowEntity = low.World.Entities.Single();
        Entity highEntity = high.World.Entities.Single();
        Assert.Equal(lowEntity.Position, highEntity.Position);

        foreach (string trait in TraitSet.TraitNames)
        {
            Assert.Equal(trait == "curiosity" ? 0.0 : TraitSet.Neutral, lowEntity.Traits[trait]);
            Assert.Equal(trait == "curiosity" ? 2.0 : TraitSet.Neutral, highEntity.Traits[trait]);
        }

        var lowDecisions = new List<DesireKind>();
        var highDecisions = new List<DesireKind>();

        const int ticks = 1000;
        for (int tick = 1; tick <= ticks; tick++)
        {
            low.AdvanceOneTick();
            high.AdvanceOneTick();
            lowDecisions.Add(low.Cognition.MindOf(lowEntity.Id.Value).Intention?.Kind ?? DesireKind.Idle);
            highDecisions.Add(high.Cognition.MindOf(highEntity.Id.Value).Intention?.Kind ?? DesireKind.Idle);
        }

        Assert.False(lowDecisions.SequenceEqual(highDecisions),
            "Les décisions sont identiques sur tout l'horizon : le trait de curiosité n'aurait eu aucune influence.");

        int firstDifference = Enumerable.Range(0, ticks).First(index => lowDecisions[index] != highDecisions[index]);
        bool oneExplores = lowDecisions[firstDifference] == DesireKind.Explore || highDecisions[firstDifference] == DesireKind.Explore;
        Assert.True(oneExplores,
            $"À la première divergence (tick {firstDifference + 1}), l'une des entités doit explorer (low={lowDecisions[firstDifference]}, high={highDecisions[firstDifference]}) : la divergence n'aurait pas été portée par le trait de curiosité.");
    }

    // ------------------------------------------------------------------
    // T3 — information locale : jamais au-delà du voisinage perceptible.
    // ------------------------------------------------------------------

    [Fact]
    public void T3_FarApartEntities_NeverKnowEachOther()
    {
        SimulationLoop loop = BuildTwoApart(seed: 3, positionA: new Position(10, 10), positionB: new Position(480, 480));
        loop.Run(300);

        Assert.False(HasPositionBelief(loop, observerId: 1, subjectId: 2), "L'entité 1 connait l'entité 2 hors de portée.");
        Assert.False(HasPositionBelief(loop, observerId: 2, subjectId: 1), "L'entité 2 connait l'entité 1 hors de portée.");
    }

    [Fact]
    public void T3_CloseEntities_LearnAboutEachOther()
    {
        SimulationLoop loop = BuildTwoApart(seed: 3, positionA: new Position(100, 100), positionB: new Position(110, 105));
        loop.Run(200);

        Assert.True(HasPositionBelief(loop, observerId: 1, subjectId: 2), "L'entité 1 n'a pas appris la position de l'entité 2 dans son voisinage.");
        Assert.True(HasPositionBelief(loop, observerId: 2, subjectId: 1), "L'entité 2 n'a pas appris la position de l'entité 1 dans son voisinage.");
    }

    private static SimulationLoop BuildTwoApart(ulong seed, Position positionA, Position positionB)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        world.AddEntity(new Entity(new EntityId(1), "Entité A", name: null, positionA, TraitSet.NeutralAll, bornAt: 0));
        world.AddEntity(new Entity(new EntityId(2), "Entité B", name: null, positionB, TraitSet.NeutralAll, bornAt: 0));
        return new SimulationLoop(world, Xoshiro256StarStar.Create(seed), OptionsNoDeath());
    }

    private static bool HasPositionBelief(SimulationLoop loop, ulong observerId, ulong subjectId)
    {
        MindState mind = loop.Cognition.MindOf(observerId);
        return mind.Beliefs.All.Any(belief =>
            belief.Fact.Subject == $"entity-{subjectId}" && belief.Fact.Predicate == "position") ;
    }

    // ------------------------------------------------------------------
    // T4 — reproductibilité du benchmark à l'échelle du jalon (les objectifs
    // de débit restent du ressort de ScaleTargetsTests + CLI --benchmark).
    // ------------------------------------------------------------------

    [Fact]
    public void T4_MilestoneScaleBenchmark_IsDeterministicAndRuns()
    {
        SimulationLoop first = Build(seed: 424242, count: 50, options: OptionsNoDeath());
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        first.Run(1000);
        stopwatch.Stop();

        SimulationLoop second = Build(seed: 424242, count: 50, options: OptionsNoDeath());
        second.Run(1000);

        Assert.Equal(1000UL, first.CurrentTick);
        Assert.True(stopwatch.Elapsed.TotalSeconds < 300.0, "L'échelle T0/T1 (50 entités × 1000 ticks) doit s'exécuter en un temps raisonnable.");
        Assert.Equal(PositionFingerprint(first), PositionFingerprint(second));
    }

    // ------------------------------------------------------------------
    // T5 — la suite dépasse la barre du jalon (&gt; 160 tests, TESTING.md).
    // ------------------------------------------------------------------

    [Fact]
    public void T5_MilestoneTestGate_SuiteExceedsOneHundredSixty()
    {
        int testMethods = typeof(MilestoneT0T5Tests).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.IsPublic)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Count(method => method.GetCustomAttributes(inherit: false).Any(attribute =>
                attribute.GetType().Name == nameof(FactAttribute) ||
                attribute.GetType().Name == nameof(TheoryAttribute)));

        Assert.True(testMethods >= 160,
            $"La suite de tests ({testMethods}) doit dépasser la barre du jalon U8 (≥ 160, TESTING.md §« Jalon »).");
    }
}