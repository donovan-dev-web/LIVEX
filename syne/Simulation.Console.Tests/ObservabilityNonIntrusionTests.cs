using System.Globalization;
using System.Text;
using Simulation.Console.Observability;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Preuves de clôture de l'observabilité (SYNE-081 / SYNE-082, DETERMINISM.md §3) :
/// <list type="bullet">
/// <item>SYNE-081 — anti-triche : l'observation (capture snapshot + événements +
/// diffusion) ne modifie **rien** du monde simulé ; un run observé et un run
/// témoin identiques produisent la même trajectoire bit-à-bit.</item>
/// <item>SYNE-082 — le flux diffusé est déterministe : deux runs observés de même
/// seed produisent des trames JSON **textuellement identiques**, donc un flux
/// consommable par ECHOS de façon reproductible.</item>
/// </list>
/// Le sink en mémoire exerce le chemin complet de production (WorldSnapshot.Capture,
/// EventSensor, ObservabilitySerializer) sans réseau — la diffusion socket étant
/// triviale côté mutation (testée indépendamment par ObservabilityServerWireTests).
/// </summary>
public class ObservabilityNonIntrusionTests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>Sink de diffusion en mémoire (enregistre la séquence exacte des trames).</summary>
    private sealed class RecordingSink : IObservabilitySink
    {
        public List<string> Frames { get; } = new();

        public Task BroadcastAsync(string text)
        {
            Frames.Add(text);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Journal de trajectoire (déterministe) : mêmes lignes que
    /// <c>DeterminismRegressionTests.BuildPerceptionLog</c>, mais recueillies par
    /// incrément pour pouvoir intercaler l'émission sur le run observé.
    /// </summary>
    private sealed class TrajectoryRecorder
    {
        private readonly StringBuilder _log = new();

        public string Text => _log.ToString();

        /// <summary>Append une ligne par entité à partir de l'état courant.</summary>
        public void Record(SimulationLoop loop)
        {
            foreach (Entity entity in loop.World.Entities.OrderBy(e => e.Id.Value))
            {
                MindState mind = loop.Cognition.MindOf(entity.Id.Value);
                _log.Append(loop.CurrentTick.ToString(CultureInfo.InvariantCulture));
                _log.Append(';');
                _log.Append(entity.Id.Value);
                _log.Append(';');
                _log.Append(mind.Memory.Recall(loop.CurrentTick).Count.ToString(CultureInfo.InvariantCulture));
                _log.Append(';');
                _log.Append(mind.Intention?.Kind ?? DesireKind.Idle);
                _log.Append(';');
                foreach (MemoryRecall recall in mind.Memory.Recall(MemoryCategory.Observation, loop.CurrentTick).Take(8))
                {
                    _log.Append(recall.Entry.Content);
                    _log.Append('|');
                }

                _log.AppendLine();
            }
        }
    }

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
    public async Task ObservedRun_MatchesTwinPlainRun_TrajectoryUnchanged()
    {
        // SYNE-081 : brancher l'émetteur complet ne modifie pas la trajectoire.
        SimulationLoop plain = BuildScenario(12345, entityCount: 25);
        var plainRec = new TrajectoryRecorder();
        for (int tick = 1; tick <= 150; tick++)
        {
            plain.AdvanceOneTick();
            plainRec.Record(plain);
        }

        SimulationLoop observed = BuildScenario(12345, entityCount: 25);
        var sink = new RecordingSink();
        var emitter = new ObservabilityTickEmitter(observed, seed: 12345, sink);
        var observedRec = new TrajectoryRecorder();
        for (int tick = 1; tick <= 150; tick++)
        {
            observed.AdvanceOneTick();
            observedRec.Record(observed);
            await emitter.EmitCurrentTickAsync();
        }

        Assert.Equal(150ul, observed.CurrentTick);
        Assert.True(sink.Frames.Count > 10, "le flux observé doit contenir des trames diffusées");
        Assert.Equal(plainRec.Text, observedRec.Text);           // bit-à-bit
        Assert.Equal(Fnv1a(plainRec.Text), Fnv1a(observedRec.Text)); // hash épinglable
    }

    [Fact]
    public void PlainRun_StillReproducesThePinnedPerceptionChecksum()
    {
        // Garde-fou transverse : la preuve d'anti-intrusion s'appuie sur un journal
        // identique à celui de DeterminismRegressionTests ; vérifie la cohérence.
        SimulationLoop loop = BuildScenario(12345, entityCount: 25);
        var rec = new TrajectoryRecorder();
        for (int tick = 1; tick <= 200; tick++)
        {
            loop.AdvanceOneTick();
            rec.Record(loop);
        }

        Assert.Equal("0x27fad50065d8c4a4", $"0x{Fnv1a(rec.Text):x16}");
    }

    [Fact]
    public async Task ObservedRuns_SameSeed_ProduceIdenticalFrameStream()
    {
        // SYNE-082 : le flux (snapshot + événements) est déterministe → le même
        // seed produit des trames identiques, donc une ingestion ECHOS reproductible.
        var sinkA = new RecordingSink();
        var emitterA = new ObservabilityTickEmitter(BuildScenario(7, entityCount: 20), seed: 7, sinkA);
        await emitterA.RunAsync(120);

        var sinkB = new RecordingSink();
        var emitterB = new ObservabilityTickEmitter(BuildScenario(7, entityCount: 20), seed: 7, sinkB);
        await emitterB.RunAsync(120);

        Assert.Equal(120ul, emitterA.TicksEmitted);
        Assert.Equal(120ul, emitterB.TicksEmitted);
        Assert.Equal(sinkA.Frames.Count, sinkB.Frames.Count);
        Assert.True(sinkA.Frames.Count > 100, "un run 120 ticks doit produire un flux volumineux");

        for (int i = 0; i < sinkA.Frames.Count; i++)
        {
            Assert.Equal(sinkA.Frames[i], sinkB.Frames[i]);
        }

        Assert.Equal(Fnv1a(string.Concat(sinkA.Frames)), Fnv1a(string.Concat(sinkB.Frames)));
    }
}