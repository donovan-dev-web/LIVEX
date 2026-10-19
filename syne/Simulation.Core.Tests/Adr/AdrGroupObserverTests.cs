// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Arbitrage A0-3 (17/10/2026, validate le 17/10) — loi des groupes en
//        OBSERVATEUR SEUL :
//        (1) la décision collective reste calculée et est exposée (état du
//            groupe, `LastDecisions`, snapshot, événement `group_decision`) mais
//            n'est JAMAIS injectée dans l'utilité individuelle — la propagation
//            SYNE-076 (`PropagateObjectives`, `GroupObjective`,
//            `collectiveAlignBonus`) a été supprimée ;
//        (2) la liaison de cohésion n'a plus aucune clause de proximité
//            explicite (`proximityRequired`/`proximityRadius` supprimés) ; la
//            distance des paires liées est instrumentée (max/moyenne par
//            groupe, `null` = non mesuré) et persistée dans le snapshot.

using System.Text.Json.Nodes;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Loop;
using Simulation.Core.Observability;
using Simulation.Core.Persistence;
using Simulation.Core.Prng;
using Simulation.Core.Social;
using Simulation.Core.World;
using Xunit;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Tests;

/// <summary>
/// Gates de l'arbitrage A0-3 : la décision collective est un CONSTAT exposé,
/// jamais une cause d'utilité ; les distances des paires liées sont mesurées et
/// portées jusqu'au snapshot ; aucune mécanisme d'injection n'est réintroduite.
/// </summary>
public class AdrGroupObserverTests
{
    private static SimulationLoop BuildLoop(ulong seed, ulong entityCount)
    {
        var world = new WorldType(new WorldSize(500, 500), spatialCellSize: 50);
        Xoshiro256StarStar rng = Xoshiro256StarStar.Create(seed);
        for (ulong i = 1; i <= entityCount; i++)
        {
            (Entity entity, rng) = EntityFactory.CreateNext(EntityTemplate.DefaultA, world, rng, i, bornAt: 0);
            world.AddEntity(entity);
        }

        return new SimulationLoop(world, rng, ConfigLoader.LoadDefaults());
    }

    [Fact]
    public void CollectiveInjection_Mechanisms_AreFullyRemoved()
    {
        // Garde de non-réintroduction (A0-3) : toute réintroduction d'un
        // mécanisme d'injection collective exige un nouvel arbitrage explicite.
        Assert.Null(typeof(MindState).GetProperty("CollectiveObjective"));
        Assert.Null(typeof(MindState).Assembly.GetType("Simulation.Core.Cognition.GroupObjective"));
        Assert.Null(typeof(GroupSystem).GetMethod("PropagateObjectives"));
        Assert.Null(typeof(Simulation.Core.Configuration.ActionEntrySettings).GetProperty("CollectiveAlignBonus"));
        Assert.Null(typeof(GroupSettings).GetProperty("ProximityRequired"));
        Assert.Null(typeof(GroupSettings).GetProperty("ProximityRadius"));
    }

    [Fact]
    public void GroupDecisionAndBondDistances_AreExposedInSnapshotAndEvents()
    {
        SimulationLoop loop = BuildLoop(seed: 12345, entityCount: 25);

        // Groupe contrôlé injecté via la restauration interne (mêmes esprits du
        // run) : décision collective adoptée + distances instrumentées.
        var minds = loop.Cognition.MindPairsSorted().ToDictionary(pair => pair.Id, pair => pair.Mind);
        var group = new Group(1, bornTick: 1, new List<ulong> { 1, 2, 3 })
        {
            MeanCohesion = 0.42,
            MaxBondDistance = 512.5,
            MeanBondDistance = 256.25,
            LeaderId = 1,
            Decision = DesireKind.SeekFood,
            Consensus = 0.9,
            HadDecision = true,
        };
        loop.Cognition.RestoreState(minds, new[] { group }, nextGroupId: 2);

        // (1) Snapshot d'état : la décision et les distances sont portées par le
        // groupe (jamais par un esprit).
        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);
        GroupSnapshotDto dto = Assert.Single(snapshot.Groups);
        Assert.Equal((int)DesireKind.SeekFood, dto.Decision);
        Assert.Equal(0.9, dto.Consensus, 10);
        Assert.Equal(512.5, dto.MaxBondDistance);
        Assert.Equal(256.25, dto.MeanBondDistance);

        // (2) Snapshot d'observabilité (PRISM/ECHOS) : mêmes champs.
        WorldSnapshot world = WorldSnapshot.Capture(loop, seed: 12345);
        GroupSnapshot worldGroup = Assert.Single(world.Groups);
        Assert.Equal("SeekFood", worldGroup.Decision);
        Assert.Equal(512.5, worldGroup.MaxBondDistance);
        Assert.Equal(256.25, worldGroup.MeanBondDistance);

        // (2bis) Émission JSON : les deux distances sont réellement PUBLIÉES dans
        // `groups[]` du snapshot (le contrat 0.5.0 les annonce). Vérifier l'objet
        // `GroupSnapshot` ne prouvait pas la sérialisation : les deux champs
        // manquaient dans le JSON avant le 18/10.
        JsonObject snapshotJson = ObservabilitySerializer.SnapshotMessage(world);
        JsonObject worldGroupJson = (JsonObject)snapshotJson["groups"]!.AsArray()[0]!;
        Assert.Equal(512.5, (double?)worldGroupJson["maxBondDistance"]);
        Assert.Equal(256.25, (double?)worldGroupJson["meanBondDistance"]);

        // (3) Événements : group_formed porte la distance instrumentée (null JSON
        // si non mesurée) et group_decision expose l'adoption.
        ExternalEvent formed = EventSensor.GroupFormed(tick: 1, group);
        Assert.Equal(512.5, (double?)formed.Value!["maxBondDistance"]);

        ExternalEvent decisionEvent = EventSensor.GroupDecision(tick: 1, new GroupDecision(1, 1, 1, DesireKind.SeekFood, 0.9));
        Assert.Equal(ObservabilityContract.GroupDecision, decisionEvent.Type);
    }

    [Fact]
    public void GroupSnapshotDto_WithBondDistances_RestoresThroughSnapshotRoundTrip()
    {
        // Persistance : les distances instrumentées survivent à la capture et à
        // la restauration (contrat de reprise du snapshot, schemaVersion 5).
        SimulationLoop loop = BuildLoop(seed: 12345, entityCount: 25);
        var minds = loop.Cognition.MindPairsSorted().ToDictionary(pair => pair.Id, pair => pair.Mind);
        var group = new Group(1, bornTick: 1, new List<ulong> { 1, 2, 3 })
        {
            MeanCohesion = 0.42,
            MaxBondDistance = 512.5,
            MeanBondDistance = 256.25,
            LeaderId = 1,
            Decision = DesireKind.SeekFood,
            Consensus = 0.9,
            HadDecision = true,
        };
        loop.Cognition.RestoreState(minds, new[] { group }, nextGroupId: 2);

        SimulationSnapshot snapshot = SimulationSnapshotCodec.Capture(loop);
        SimulationLoop restored = SimulationSnapshotRestorer.Restore(snapshot, ConfigLoader.LoadDefaults());

        Group restoredGroup = Assert.Single(restored.Cognition.Groups.Active);
        Assert.Equal(512.5, restoredGroup.MaxBondDistance);
        Assert.Equal(256.25, restoredGroup.MeanBondDistance);
        Assert.Equal(DesireKind.SeekFood, restoredGroup.Decision);
        Assert.Equal(0.9, restoredGroup.Consensus, 10);
    }

    [Fact]
    public void ObserverSnapshot_WithoutMeasuredBondDistances_SerializesNull_NotZero()
    {
        // Absence ≠ zéro : un groupe dont les distances n'ont pas été mesurées
        // publie `null` dans le snapshot (jamais 0.0, qui se lirait comme un
        // zéro observé).
        SimulationLoop loop = BuildLoop(seed: 12345, entityCount: 5);
        var minds = loop.Cognition.MindPairsSorted().ToDictionary(pair => pair.Id, pair => pair.Mind);
        var group = new Group(3, bornTick: 2, new List<ulong> { 2, 3 });
        loop.Cognition.RestoreState(minds, new[] { group }, nextGroupId: 4);

        WorldSnapshot world = WorldSnapshot.Capture(loop, seed: 12345);
        JsonObject json = (JsonObject)ObservabilitySerializer.SnapshotMessage(world)["groups"]!.AsArray()[0]!;

        Assert.True(json.ContainsKey("maxBondDistance"));
        Assert.True(json.ContainsKey("meanBondDistance"));
        Assert.Null(json["maxBondDistance"]);
        Assert.Null(json["meanBondDistance"]);
    }

    [Fact]
    public void GroupFormedEvent_WithoutPositions_CarriesNullDistance_NotZero()
    {
        // Distinction zéro observé / non mesuré côté événement : sans table de
        // positions à la revue, `maxBondDistance` est un zéro JSON (absence),
        // jamais 0.0 mesuré.
        var group = new Group(7, bornTick: 3, new List<ulong> { 4, 5 });

        ExternalEvent formed = EventSensor.GroupFormed(tick: 3, group);

        Assert.Null(formed.Value!["maxBondDistance"]);
    }
}
