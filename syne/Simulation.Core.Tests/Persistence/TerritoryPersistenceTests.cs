using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Persistence;
using Simulation.Core.Prng;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Persistance des territoires (jalon review/refactor, engineVersion 0.12.0).
///
/// <para>
/// Le snapshot ne capturait ni les zones de territoire ni l'appartenance
/// effective. Une restauration depuis un snapshot produisait donc un monde
/// <b>sans aucune zone</b> : le suivi s'éteignait silencieusement et, pire, toute
/// entité se retrouvait implicitement rattachée à la terre nominale « Communes ».
/// Le DTO <c>territories[]</c> et l'amorçage de l'appartenance au tick restauré
/// corrigent les deux moitiés.
/// </para>
/// </summary>
public class TerritoryPersistenceTests
{
    private static SimulationOptions Options()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.World.Territories.Enabled = true;
        options.World.Territories.Zones =
        [
            new TerritoryZoneDefinition { Id = "nord", CenterX = 250.0, CenterY = 100.0, Radius = 120.0 },
            new TerritoryZoneDefinition { Id = "sud", CenterX = 250.0, CenterY = 400.0, Radius = 120.0 },
        ];
        return options;
    }

    private static (SimulationLoop Loop, Xoshiro256StarStar Rng) Build(SimulationOptions options, params (ulong Id, Position At)[] agents)
    {
        var world = new WorldType(WorldSize.From(options));
        foreach (TerritoryZoneDefinition zone in options.World.Territories.Zones)
        {
            world.AddTerritory(new Territory(zone.Id, new Position(zone.CenterX, zone.CenterY), zone.Radius));
        }

        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(42);
        foreach ((ulong id, Position at) in agents)
        {
            Entity entity = new Entity(new EntityId(id), $"E{id}", name: null, at, TraitSet.NeutralAll, bornAt: 0);
            world.AddEntity(entity);
        }

        return (new SimulationLoop(world, rng, options), rng);
    }

    [Fact]
    public void Capture_RecordsEveryZone_WithItsMembership()
    {
        SimulationOptions options = Options();
        (SimulationLoop loop, _) = Build(options, (1, new Position(250, 100)), (2, new Position(250, 400)), (3, new Position(400, 250)));

        loop.AdvanceOneTick();
        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);

        Assert.NotNull(snapshot.World.Territories);
        Assert.Equal(2, snapshot.World.Territories.Count);

        TerritorySnapshotDto nord = snapshot.World.Territories.Single(t => t.Id == "nord");
        TerritorySnapshotDto sud = snapshot.World.Territories.Single(t => t.Id == "sud");

        Assert.Equal(100.0, nord.CenterY, 10);
        Assert.Equal(120.0, nord.Radius, 10);
        // E1 est dans « nord », E2 dans « sud », E3 dans aucune zone.
        Assert.Equal([1UL], nord.Members);
        Assert.Equal([2UL], sud.Members);
    }

    [Fact]
    public void RoundTrip_RestoresZonesAndMembership()
    {
        SimulationOptions options = Options();
        (SimulationLoop loop, _) = Build(options, (1, new Position(250, 100)), (2, new Position(250, 400)));
        loop.AdvanceOneTick();

        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);
        string json = SimulationSnapshotCodec.ToJson(snapshot);
        SimulationLoop restored = SimulationSnapshotRestorer.Restore(
            SimulationSnapshotCodec.FromJson(json), options);

        Assert.Equal(2, restored.World.Territories.Count);
        Assert.Equal(["nord", "sud"], restored.World.Territories.Select(t => t.Id).ToArray());

        // L'appartenance est réamorcée : le tick suivant ne doit réémettre aucun
        // événement d'entrée pour des entités déjà dans la zone.
        Assert.Empty(restored.LastTerritoryChanges);
        restored.AdvanceOneTick();
        Assert.Empty(restored.LastTerritoryChanges);
    }

    [Fact]
    public void RestoredMembership_StillDetectsGenuineMovement()
    {
        // L'amorçage ne doit pas « figer » l'appartenance : un déplacement réel
        // doit toujours produire un événement.
        SimulationOptions options = Options();
        (SimulationLoop loop, _) = Build(options, (1, new Position(250, 100)));
        loop.AdvanceOneTick();

        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);
        SimulationLoop restored = SimulationSnapshotRestorer.Restore(SimulationSnapshotCodec.FromJson(
            SimulationSnapshotCodec.ToJson(snapshot)), options);

        restored.World.Entities.Single(e => e.Id.Value == 1).SetPosition(new Position(250, 400));
        restored.AdvanceOneTick();

        Assert.Contains(
            restored.LastTerritoryChanges,
            c => c.Kind == TerritoryMembershipChangeKind.Left && c.EntityId == 1UL && c.Zone.Id == "nord");
        Assert.Contains(
            restored.LastTerritoryChanges,
            c => c.Kind == TerritoryMembershipChangeKind.Entered && c.EntityId == 1UL && c.Zone.Id == "sud");
    }

    [Fact]
    public void SnapshotHash_IsStable_AcrossACaptureRestoreCaptureCycle()
    {
        // La capture doit être idempotente : capturer, restaurer, recapturer
        // redonne le même contenu (garant que rien n'est perdu par le aller-retour).
        SimulationOptions options = Options();
        (SimulationLoop loop, _) = Build(options, (1, new Position(250, 100)), (2, new Position(250, 400)));
        loop.AdvanceOneTick();

        SimulationSnapshot first = SimulationSnapshotCodec.Capture(loop);
        SimulationLoop restored = SimulationSnapshotRestorer.Restore(
            SimulationSnapshotCodec.FromJson(SimulationSnapshotCodec.ToJson(first)), options);
        SimulationSnapshot second = SimulationSnapshotCodec.Capture(restored);

        Assert.Equal(SimulationSnapshotCodec.Hash(first), SimulationSnapshotCodec.Hash(second));
    }

    [Fact]
    public void SchemaVersion_IsFour_AndLegacySnapshotsWithoutTerritoriesStillLoad()
    {
        // Rétro-compatibilité : un snapshot v3 (sans champ territories) doit se
        // restaurer avec des listes vides plutôt que de lever.
        Assert.Equal(4, SimulationSnapshotCodec.SchemaVersion);

        string legacy = """
        {
          "schemaVersion": 3,
          "tick": 5,
          "rngState": { "s0": 1, "s1": 2, "s2": 3, "s3": 4 },
          "world": { "width": 500, "height": 500, "entities": [], "obstacles": [], "books": [] },
          "agents": {},
          "cognition": {}
        }
        """;

        SimulationSnapshot snapshot = SimulationSnapshotCodec.FromJson(legacy);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.World.Territories is null || snapshot.World.Territories.Count == 0);
    }
}
