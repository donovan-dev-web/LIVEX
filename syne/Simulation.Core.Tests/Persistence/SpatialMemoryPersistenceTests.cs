// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : La mémoire spatiale par chunk (SYNE-221) est capturée et restaurée bit-à-bit.

using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Perception;
using Simulation.Core.Persistence;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

public class SpatialMemoryPersistenceTests
{
    private static SimulationLoop BuildScenario(ulong seed, int entityCount)
    {
        var world = new Simulation.Core.World.World(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (int i = 0; i < entityCount; i++)
        {
            (Entity entity, Xoshiro256StarStar next) =
                EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, (ulong)(i + 1), bornAt: 0);
            rng = next;
            world.AddEntity(entity);
        }

        SimulationOptions options = ConfigLoader.LoadDefaults();
        return new SimulationLoop(world, rng, options);
    }

    [Fact]
    public void Capture_ThenRestore_PreservesTheSpatialMemory()
    {
        SimulationLoop loop = BuildScenario(4321, 3);
        Entity observed = loop.World.Entities[0];
        loop.Cognition.ApplyExternalSpatialObservations(
            observed,
            [
                new SpatialChunkObservation(2, 1, 0b1010u),
                new SpatialChunkObservation(0, 4, 0b0001u),
            ],
            currentTick: 7);
        loop.Cognition.ApplyExternalSpatialObservations(
            observed,
            [new SpatialChunkObservation(2, 1, 0b0100u)],
            currentTick: 8);

        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);
        MindSnapshotDto captured = snapshot.Minds.Single(mind => mind.EntityId == observed.Id.Value);
        Assert.NotNull(captured.SpatialChunks);
        Assert.Equal(2, captured.SpatialChunks!.Count);

        SimulationLoop restored = SimulationSnapshotRestorer.Restore(snapshot, ConfigLoader.LoadDefaults());
        SimulationSnapshot roundTrip = SimulationSnapshotCodec.Capture(restored);
        MindSnapshotDto restoredMind = roundTrip.Minds.Single(mind => mind.EntityId == observed.Id.Value);

        Assert.NotNull(restoredMind.SpatialChunks);
        Assert.Equal(captured.SpatialChunks, restoredMind.SpatialChunks);
        Assert.Equal(0b1110u, restoredMind.SpatialChunks!.Single(chunk => chunk.ChunkX == 2 && chunk.ChunkY == 1).TagMask);
    }

    [Fact]
    public void Capture_LeavesSpatialChunksAbsent_WhenNothingWasObserved()
    {
        // Les runs sans PRISM ne doivent pas gagner de champ : l'empreinte
        // bit-à-bit des scénarios de référence reste identique.
        SimulationLoop loop = BuildScenario(11, 2);

        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);

        Assert.All(snapshot.Minds, mind => Assert.Null(mind.SpatialChunks));
    }
}
