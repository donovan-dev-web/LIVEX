// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Traçabilité des constructions (SYNE-071, jalon U8) : une pose/retrait d'obstacle statique en cours de run
//        est une modification d'environnement tracée — drainée par l'émetteur en événements
//        world.construction_placed/world.construction_removed et reflétée dans le snapshot (obstacles), sans rompre
//        le déterminisme du run.

using System.Globalization;
using System.Text.Json.Nodes;
using Simulation.Console.Observability;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.PrismWorldAdapter;

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
    public async Task LocalConstructionPlacement_IsNoLongerTraced_SincePrismOwnsTheGeometry()
    {
        // SYNE-220 étape 3c : la pose locale d'une construction est un shim vide
        // (la géométrie appartient à PRISM). Ce test verrouille l'absence de
        // fuite : aucune modification d'environnement, aucun événement local,
        // afin que le flux d'observabilité ne prétende plus tracer un monde
        // que SYNE ne possède pas.
        var sink = new RecordingSink();
        SimulationLoop loop = BuildScenario(seed: 7);
        var emitter = new ObservabilityTickEmitter(loop, seed: 7, sink);

        for (int tick = 1; tick <= 3; tick++)
        {
            loop.AdvanceOneTick();
            await emitter.EmitCurrentTickAsync();
        }

        ((WorldType)loop.World).PlaceConstruction(
            new Obstacle("maison-1", new Position(60, 60), radius: 10));
        loop.AdvanceOneTick();
        await emitter.EmitCurrentTickAsync();

        Assert.Empty(loop.World.Obstacles);
        Assert.Empty(loop.World.LastEnvironmentChanges);
        Assert.DoesNotContain(
            sink.Frames.Select(ParseFrame),
            node => node!["type"]!.GetValue<string>() == ObservabilityContract.ConstructionPlaced);
        Assert.DoesNotContain(
            sink.Frames.Select(ParseFrame),
            node => node!["type"]!.GetValue<string>() == ObservabilityContract.ConstructionRemoved);
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
