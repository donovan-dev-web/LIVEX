using System.Globalization;
using System.Text.Json.Nodes;
using Simulation.Console.Observability;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Console.Tests;

/// <summary>
/// Traçabilité des constructions (SYNE-071, jalon U8) : une pose/retrait
/// d'obstacle statique en cours de run est une modification d'environnement
/// tracée — drainée par l'émetteur en événements
/// <c>world.construction_placed</c>/<c>world.construction_removed</c> et reflétée
/// dans le snapshot (<c>obstacles</c>), sans rompre le déterminisme du run.
/// </summary>
public class ObservabilityConstructionTests
{
    private sealed class RecordingSink : IObservabilitySink
    {
        public List<string> Frames { get; } = new();

        public Task BroadcastAsync(string text)
        {
            Frames.Add(text);
            return Task.CompletedTask;
        }
    }

    private static SimulationLoop BuildScenario(ulong seed)
    {
        var world = new WorldType(new WorldSize(250, 250), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);

        for (ulong i = 1; i <= 3; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, ConfigLoader.LoadDefaults());
    }

    private static JsonNode ParseFrame(string frame) =>
        JsonNode.Parse(frame) ?? throw new InvalidOperationException("trame JSON invalide");

    [Fact]
    public async Task PlacementAndRemoval_AreEmittedAndDrained_WithoutBreakingTheRun()
    {
        const int warmup = 3;
        var sink = new RecordingSink();
        SimulationLoop loop = BuildScenario(seed: 7);
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, sink);

        for (int tick = 1; tick <= warmup; tick++)
        {
            loop.AdvanceOneTick();
            await emitter.EmitCurrentTickAsync();
        }

        Assert.Empty(loop.World.Obstacles);
        Assert.Empty(loop.World.LastEnvironmentChanges);
        ulong revisionBefore = loop.World.ObstacleRevision;

        loop.World.PlaceConstruction(new Obstacle("maison-1", new Position(60, 60), radius: 10));
        Assert.Single(loop.World.LastEnvironmentChanges);
        Assert.True(loop.World.ObstacleRevision > revisionBefore);

        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        var placed = sink.Frames.Select(ParseFrame)
            .Where(node => node!["type"]!.GetValue<string>() == ObservabilityContract.ConstructionPlaced)
            .ToList();
        JsonNode placedFrame = Assert.Single(placed);
        Assert.Equal("maison-1", (string?)placedFrame["targetId"]);
        Assert.Equal((ulong)warmup + 1, placedFrame["tick"]!.GetValue<ulong>());
        Assert.Equal(60.0, (double?)placedFrame["value"]!["x"]);
        Assert.Equal(10.0, (double?)placedFrame["value"]!["radius"]);

        // Le drain consomme les modifications tracées (événement émis une seule fois).
        Assert.Empty(loop.World.LastEnvironmentChanges);
        Assert.DoesNotContain(sink.Frames.Select(ParseFrame),
            node => node!["type"]!.GetValue<string>() == ObservabilityContract.ConstructionRemoved);

        // Le snapshot porte l'obstacle (même tick d'émission).
        JsonNode snapshot = sink.Frames.Select(ParseFrame)
            .Last(node => node!["type"]!.GetValue<string>() == "snapshot");
        JsonArray obstacles = snapshot["obstacles"]!.AsArray();
        Assert.Single(obstacles);
        Assert.Equal("maison-1", (string?)obstacles[0]!["id"]);

        loop.World.RemoveConstruction("maison-1");
        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        var removed = sink.Frames.Select(ParseFrame)
            .Where(node => node!["type"]!.GetValue<string>() == ObservabilityContract.ConstructionRemoved)
            .ToList();
        Assert.Single(removed);
        Assert.Equal("maison-1", (string?)removed[0]!["targetId"]);
        Assert.Equal((ulong)warmup + 2, removed[0]!["tick"]!.GetValue<ulong>());
        Assert.Empty(loop.World.LastEnvironmentChanges);
    }

    [Fact]
    public async Task ConstructionPlacement_DoesNotAlterPlainTrajectory()
    {
        // La pose d'une construction hors-portée ne consomme aucun tirage PRNG :
        // le run observé reste déterministe (même journal cognitif qu'un run témoin).
        SimulationLoop plain = BuildScenario(seed: 9);
        var plainRec = new TrajectoryMeter();
        for (int tick = 1; tick <= 5; tick++)
        {
            plain.AdvanceOneTick();
            plainRec.Record(plain);
        }

        var sink = new RecordingSink();
        SimulationLoop observed = BuildScenario(seed: 9);
        var observedRec = new TrajectoryMeter();
        var emitter = new ObservabilityTickEmitter(observed, seed: 9, sink);
        for (int tick = 1; tick <= 5; tick++)
        {
            observed.AdvanceOneTick();
            observedRec.Record(observed);
            await emitter.EmitCurrentTickAsync();
        }

        Assert.Equal(plainRec.Text, observedRec.Text);
    }

    private sealed class TrajectoryMeter
    {
        private readonly System.Text.StringBuilder _log = new();

        public string Text => _log.ToString();

        public void Record(SimulationLoop loop)
        {
            foreach (Entity entity in loop.World.Entities.OrderBy(e => e.Id.Value))
            {
                Simulation.Core.Cognition.MindState mind = loop.Cognition.MindOf(entity.Id.Value);
                _log.Append(loop.CurrentTick.ToString(CultureInfo.InvariantCulture));
                _log.Append(';');
                _log.Append(entity.Id.Value);
                _log.Append(';');
                _log.Append(mind.Intention?.Kind ?? Simulation.Core.Cognition.DesireKind.Idle);
                _log.Append(';');
                _log.Append(entity.Position.X.ToString("R", CultureInfo.InvariantCulture));
                _log.Append(',');
                _log.Append(entity.Position.Y.ToString("R", CultureInfo.InvariantCulture));
                _log.AppendLine();
            }
        }
    }
}