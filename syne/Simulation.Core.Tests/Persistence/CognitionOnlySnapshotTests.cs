// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du découpage du snapshot (SYNE-220 étape 3a) : capture de cognition seule, refus du
//        chemin local, et format des snapshots complets inchangé.

using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Persistence;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests.Persistence;

public sealed class CognitionOnlySnapshotTests
{
    private static SimulationLoop Build()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        var world = new Simulation.Core.World.World(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(31);
        (Entity entity, Xoshiro256StarStar next) =
            EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, 1, bornAt: 0);
        world.AddEntity(entity);
        return new SimulationLoop(world, next, options);
    }

    [Fact]
    public void CaptureCognition_LeavesTheWorldOut_AndKeepsOnlyAReference()
    {
        SimulationLoop loop = Build();
        loop.AdvanceOneTick();

        SimulationSnapshot snapshot = SimulationSnapshotCodec.CaptureCognition(loop);

        // Le monde n'est pas dans le snapshot : il appartient à l'hôte.
        Assert.Null(snapshot.World);
        WorldReferenceDto reference = Assert.IsType<WorldReferenceDto>(snapshot.WorldReference);
        Assert.Equal(loop.World.Size.Width, reference.Width);
        Assert.Equal(loop.World.Size.Height, reference.Height);
        Assert.Equal(loop.Options.Agents.Perception.SpatialCellSize, reference.CellSize);
        Assert.Null(reference.ContentHash);

        // Ce que SYNE ne peut pas reconstruire, lui, est bien là.
        Assert.Equal(loop.CurrentTick, snapshot.Tick);
        Assert.Equal(loop.Cognition.MindPairsSorted().Count(), snapshot.Minds.Count);
        Assert.Equal(loop.Cognition.Groups.NextGroupId, snapshot.NextGroupId);
        Assert.Equal(loop.Rng.State, (snapshot.Rng.S0, snapshot.Rng.S1, snapshot.Rng.S2, snapshot.Rng.S3));
        Assert.Equal(RunIdentity.ConfigHash(loop.Options), snapshot.ConfigHash);
    }

    [Fact]
    public void CaptureCognition_KeepsTheContentHashWhenTheHostProvidesIt()
    {
        SimulationLoop loop = Build();

        SimulationSnapshot snapshot = SimulationSnapshotCodec.CaptureCognition(loop, contentHash: "449942918aec7c4c");

        Assert.Equal("449942918aec7c4c", snapshot.WorldReference!.ContentHash);
    }

    [Fact]
    public void Capture_StillCarriesTheWholeWorld_SoTheLocalFormatIsUnchanged()
    {
        SimulationLoop loop = Build();

        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);

        Assert.NotNull(snapshot.World);
        Assert.Single(snapshot.World.Entities);
        Assert.Null(snapshot.WorldReference);

        string json = SimulationSnapshotCodec.ToJson(snapshot);
        Assert.DoesNotContain("worldReference", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_RefusesACognitionOnlySnapshot()
    {
        SimulationLoop loop = Build();
        SimulationSnapshot snapshot = SimulationSnapshotCodec.CaptureCognition(loop);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => SimulationSnapshotRestorer.Restore(snapshot, loop.Options));

        Assert.Contains("cognition seule", error.Message, StringComparison.Ordinal);
        Assert.Contains("RestoreCognition", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreCognition_BringsTheMindsBack_WithoutTouchingTheLocalWorld()
    {
        SimulationLoop source = Build();
        // Un esprit n'existe qu'après la première délibération du tick.
        source.AdvanceOneTick();
        ulong entityId = source.World.Entities[0].Id.Value;
        source.Cognition.MindOf(entityId).Needs.ExertEnergy(11.5);
        double expectedEnergy = source.Cognition.MindOf(entityId).Needs.Energy;

        SimulationSnapshot snapshot = SimulationSnapshotCodec.CaptureCognition(source);

        SimulationLoop target = Build();
        (ulong Id, double X, double Y)[] positionsBefore = target.World.Entities
            .Select(entity => (entity.Id.Value, entity.Position.X, entity.Position.Y))
            .ToArray();

        SimulationSnapshotRestorer.RestoreCognition(target, snapshot);

        Assert.Equal(expectedEnergy, target.Cognition.MindOf(entityId).Needs.Energy, 10);
        Assert.Equal(
            positionsBefore,
            target.World.Entities.Select(entity => (entity.Id.Value, entity.Position.X, entity.Position.Y)).ToArray());
    }

    [Fact]
    public void CognitionOnlySnapshot_SurvivesAJsonRoundTrip()
    {
        SimulationLoop loop = Build();
        loop.AdvanceOneTick();

        SimulationSnapshot snapshot = SimulationSnapshotCodec.CaptureCognition(loop);
        SimulationSnapshot reloaded = SimulationSnapshotCodec.FromJson(SimulationSnapshotCodec.ToJson(snapshot));

        Assert.Null(reloaded.World);
        Assert.NotNull(reloaded.WorldReference);
        Assert.Equal(SimulationSnapshotCodec.Hash(snapshot), SimulationSnapshotCodec.Hash(reloaded));
        Assert.Equal(snapshot.Minds.Count, reloaded.Minds.Count);
    }
}
