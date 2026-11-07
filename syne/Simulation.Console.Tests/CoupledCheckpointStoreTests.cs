// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du point de reprise couplé (SYNE-225) : snapshot SYNE aligné sur un checkpoint PRISM.

using System.Text.Json;
using Simulation.Console.Integration;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Persistence;
using Simulation.Core.Prng;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Console.Tests;

public sealed class CoupledCheckpointStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "livex-coupled-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static (SimulationLoop Loop, Entity Entity) Build()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        var world = new World(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(77);
        (Entity entity, Xoshiro256StarStar next) =
            EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, 1, bornAt: 0);
        world.AddEntity(entity);
        var loop = new SimulationLoop(world, next, options);
        loop.AdvanceOneTick();
        return (loop, entity);
    }

    [Fact]
    public void SaveThenLoadReturnsTheSameState()
    {
        (SimulationLoop loop, Entity entity) = Build();
        loop.Cognition.MindOf(entity.Id.Value).Needs.ExertEnergy(7.0);

        string path = CoupledCheckpointStore.Save(_directory, "r2-2499", tick: 2499, loop);

        Assert.True(File.Exists(path));
        CoupledCheckpointLoad load = CoupledCheckpointStore.Load(_directory, "r2-2499", expectedTick: 2499);
        Assert.True(load.Succeeded, load.Error);
        Assert.Equal("r2-2499", load.Checkpoint!.CheckpointId);
        Assert.Equal(2499, load.Checkpoint.Tick);
        Assert.Equal(CoupledFingerprint.Compute(loop, lastTick: 2499), load.Checkpoint.Fingerprint);
    }

    [Fact]
    public void ARestoredSnapshotBringsTheCognitionBackIntoAnotherLoop()
    {
        (SimulationLoop source, Entity entity) = Build();
        source.Cognition.MindOf(entity.Id.Value).Needs.ExertEnergy(9.0);
        double expectedEnergy = source.Cognition.MindOf(entity.Id.Value).Needs.Energy;
        CoupledCheckpointStore.Save(_directory, "point", tick: 5, source);
        CoupledCheckpointLoad load = CoupledCheckpointStore.Load(_directory, "point", expectedTick: 5);

        // Boucle "fraîche" du même profil : la cognition revient, le monde local n'est pas touché.
        (SimulationLoop target, Entity targetEntity) = Build();
        double beforeEnergy = target.Cognition.MindOf(targetEntity.Id.Value).Needs.Energy;
        Assert.NotEqual(expectedEnergy, beforeEnergy);

        SimulationSnapshot snapshot = SimulationSnapshotCodec.FromJson(load.Checkpoint!.SnapshotJson);
        SimulationSnapshotRestorer.RestoreCognition(target, snapshot);

        Assert.Equal(expectedEnergy, target.Cognition.MindOf(entity.Id.Value).Needs.Energy, 10);
    }

    [Fact]
    public void SavePersistsTheCognitionOnly_AndLeavesTheWorldToPrism()
    {
        (SimulationLoop loop, _) = Build();

        string path = CoupledCheckpointStore.Save(_directory, "point", tick: 2, loop);

        CoupledCheckpointFile file = JsonSerializer.Deserialize<CoupledCheckpointFile>(File.ReadAllText(path))!;
        SimulationSnapshot snapshot = SimulationSnapshotCodec.FromJson(file.SnapshotJson);

        // Étape 3a : le monde appartient à PRISM, la copie locale ne doit plus voyager.
        Assert.Null(snapshot.World);
        Assert.NotNull(snapshot.WorldReference);
        Assert.DoesNotContain("\"world\":", file.SnapshotJson, StringComparison.Ordinal);
        Assert.Single(snapshot.Minds);
    }

    [Fact]
    public void LoadRefusesATickThatDoesNotMatchThePrismCheckpoint()
    {
        (SimulationLoop loop, _) = Build();
        CoupledCheckpointStore.Save(_directory, "point", tick: 10, loop);

        CoupledCheckpointLoad load = CoupledCheckpointStore.Load(_directory, "point", expectedTick: 11);

        Assert.False(load.Succeeded);
        Assert.Contains("reprise refusée", load.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadReportsAMissingSnapshot()
    {
        CoupledCheckpointLoad load = CoupledCheckpointStore.Load(_directory, "absent", expectedTick: 0);

        Assert.False(load.Succeeded);
        Assert.Contains("aucun snapshot", load.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadRefusesACorruptedSnapshot()
    {
        (SimulationLoop loop, Entity entity) = Build();
        string path = CoupledCheckpointStore.Save(_directory, "point", tick: 3, loop);

        // On modifie le snapshot sans retoucher l'empreinte : le chargement doit refuser.
        string json = File.ReadAllText(path);
        int index = json.IndexOf("hunger", StringComparison.Ordinal);
        Assert.True(index > 0);
        File.WriteAllText(path, json.Replace("hunger", "hungen", StringComparison.Ordinal));

        CoupledCheckpointLoad load = CoupledCheckpointStore.Load(_directory, "point", expectedTick: 3);
        Assert.False(load.Succeeded);
        Assert.NotNull(load.Error);
    }

    [Fact]
    public void TheTickAfterARestoreIsTheOneFollowingTheLastCompletedTick()
    {
        // PRISM refuse de rejouer le tick achevé : la reprise commence au suivant.
        Assert.Equal(2500, CoupledCheckpointStore.NextTickAfterRestore(2499));
        Assert.Equal(1, CoupledCheckpointStore.NextTickAfterRestore(0));
    }

    [Fact]
    public void SaveDoesNotLeaveATemporaryFileBehind()
    {
        (SimulationLoop loop, _) = Build();
        CoupledCheckpointStore.Save(_directory, "point", tick: 1, loop);

        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }
}
