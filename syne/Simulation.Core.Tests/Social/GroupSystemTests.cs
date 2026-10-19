// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests du système de groupes émergents (SYNE-060/061) : cohésion (confiance réciproque × affinité), cycle
//        de vie formation/dissolution, leader émergent et décisions collectives.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Social;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Tests du système de groupes émergents (SYNE-060/061) :
/// cohésion (confiance réciproque × affinité), cycle de vie formation/dissolution,
/// leader émergent et décisions collectives. Aucun tirage du PRNG global.
/// </summary>
public class GroupSystemTests
{
    private static SimulationOptions Options() => ConfigLoader.LoadDefaults();

    private static MindState Mind(SimulationOptions options) => new(options);

    private static void Bond(MindState self, ulong peer, int interactions = 1)
    {
        for (int i = 0; i < interactions; i++)
        {
            self.Trust.Interact(peer);
        }
    }

    private static void ShareBelief(MindState mind, SimulationOptions options, ulong target, ulong tick)
    {
        mind.Beliefs.ApplyEvidence(
            new Fact($"entity-{target}", "position", "10.00,10.00"),
            0.9,
            $"perception-{target}",
            options.Agents.Beliefs,
            tick);
    }

    private static Dictionary<ulong, MindState> Minds(SimulationOptions options, int count)
    {
        var minds = new Dictionary<ulong, MindState>();
        for (ulong id = 1; id <= (ulong)count; id++)
        {
            minds[id] = Mind(options);
        }

        return minds;
    }

    [Fact]
    public void Step_FormsGroup_WhenCohesionReached()
    {
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        GroupFormation formed = Assert.Single(system.LastFormed);
        Assert.Equal(1UL, formed.GroupId);
        Assert.Equal(3, formed.Members.Count);
        Assert.Equal(new ulong[] { 1, 2, 3 }, formed.Members);
        Assert.True(formed.MeanCohesion > 0.0);

        Group group = Assert.Single(system.Active);
        Assert.Equal(10UL, group.BornTick);
        Assert.Equal(1UL, group.LeaderId);
        Assert.Equal(3, group.Members.Count);
    }

    [Fact]
    public void Step_ReviewsOnlyOnInterval()
    {
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 3);
        }

        var system = new GroupSystem(options.Groups);

        system.Step(tick: 5, minds);
        Assert.Empty(system.LastFormed);

        system.Step(tick: 10, minds);
        Assert.Single(system.LastFormed);
    }

    [Fact]
    public void Step_NoGroup_BelowMinGroupSize()
    {
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 2);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Assert.Empty(system.LastFormed);
        Assert.Empty(system.Active);
    }

    [Fact]
    public void Step_NoGroup_WithoutSharedBeliefsOrGoals()
    {
        // Décision n°24 : la confiance seule ne suffit pas — il faut au moins une
        // part commune (croyance ou but) au-delà du seuil de confiance.
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Assert.Empty(system.LastFormed);
    }

    [Fact]
    public void Step_DissolvesWhenMembershipChanges_AndSuccessIsFalse()
    {
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 4);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);
        Group first = Assert.Single(system.Active);
        Assert.Equal(4, first.Members.Count);

        // Détache l'entité 4 du noyau {1,2,3} : disparition de tous ses liens sociaux.
        for (ulong peer = 1; peer <= 3; peer++)
        {
            for (int i = 0; i < 4; i++)
            {
                minds[peer].Trust.ObserveDeception(4);
                minds[4].Trust.ObserveDeception(peer);
            }
        }

        system.Step(tick: 20, minds);

        GroupDissolution dissolution = Assert.Single(system.LastDissolved);
        Assert.Equal(1UL, dissolution.GroupId);
        Assert.Equal(4, dissolution.Members.Count);
        Assert.False(dissolution.Success);
        Assert.Equal(4, dissolution.MembersOut);

        // Le noyau {1,2,3} reforme un nouveau groupe (id 2).
        Group reformed = Assert.Single(system.Active);
        Assert.Equal(2UL, reformed.Id);
        Assert.Equal(new ulong[] { 1, 2, 3 }, reformed.Members);
    }

    [Fact]
    public void Step_EmergentLeader_IsBestIncomingTrust_TieBreakMinId()
    {
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        // L'entité 2 gagne toute la confiance des autres (plus d'interactions).
        foreach (ulong peer in new ulong[] { 1, 3 })
        {
            Bond(minds[peer], 2, interactions: 8);
            Bond(minds[2], peer, interactions: 8);
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Group group = Assert.Single(system.Active);
        Assert.Equal(2UL, group.LeaderId);
    }

    [Fact]
    public void Step_CollectiveDecision_WhenConsensusReached()
    {
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
            mind.Intention = new Goal(DesireKind.SeekFood, BornTick: 8);
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        GroupDecision decision = Assert.Single(system.LastDecisions);
        Assert.Equal(DesireKind.SeekFood, decision.Decision);
        Assert.Equal(1.0, decision.Consensus, 12);
        Assert.True(Assert.Single(system.Active).HadDecision);
    }

    [Fact]
    public void Step_NoDecision_WithoutQuorum()
    {
        SimulationOptions options = Options();
        options.Groups.ConsensusThreshold = 1.0;

        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
            mind.Intention = new Goal(DesireKind.SeekWater, BornTick: 8);
        }

        // L'entité 3 diverge : pas de consensus unanime.
        minds[3].Intention = new Goal(DesireKind.Rest, BornTick: 8);

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Assert.Empty(system.LastDecisions);
        Group group = Assert.Single(system.Active);
        Assert.Null(group.Decision);
        Assert.False(group.HadDecision);
    }

    [Fact]
    public void Step_Disabled_ProducesNoGroups()
    {
        SimulationOptions options = Options();
        options.Groups.Enabled = false;

        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Assert.Empty(system.LastFormed);
        Assert.Empty(system.Active);
    }

    [Fact]
    public void Step_WithPositions_DistantMembers_StayBonded_AndDistanceIsInstrumented()
    {
        // A0-3 (17/10/2026) : la liaison de cohésion n'a PLUS de clause de
        // proximité explicite (l'ancienne clause D3 `proximityRequired` a été
        // retirée) : deux membres distants restent liés par confiance réciproque
        // et affinité, et leur distance est INSTRUMENTÉE (max/moyenne par
        // groupe) au lieu d'être filtrante. La confiance décroissante éteint de
        // lui-même un lien distants (0,9/tick sans interaction).
        SimulationOptions options = Options();
        options.Groups.MinGroupSize = 2;

        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        // Entités 1 et 2 proches (10) ; entité 3 très loin (1000 ≫ 60).
        var positions = new Dictionary<ulong, Position>
        {
            [1] = new Position(0.0, 0.0),
            [2] = new Position(10.0, 0.0),
            [3] = new Position(1000.0, 0.0),
        };

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds, positions);

        // Le trio entier est lié (aucun filtre spatial) et les distances des
        // paires liées sont mesurées : max = 1000, moyenne = (10 + 1000 + 990)/3.
        Group group = Assert.Single(system.Active);
        Assert.Equal(new ulong[] { 1, 2, 3 }, group.Members);
        Assert.Equal(1000.0, group.MaxBondDistance);
        Assert.Equal(2000.0 / 3.0, group.MeanBondDistance!.Value, 10);
        Assert.True(group.MaxBondDistance > 60.0,
            "La distance instrumentée dépasse l'ancien rayon de proximité : la liaison n'est plus filtrante.");
    }

    [Fact]
    public void Step_WithPositions_NearbyMembers_Bond_WithSmallInstrumentedDistance()
    {
        SimulationOptions options = Options();
        options.Groups.MinGroupSize = 3;

        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        var positions = new Dictionary<ulong, Position>
        {
            [1] = new Position(0.0, 0.0),
            [2] = new Position(10.0, 0.0),
            [3] = new Position(20.0, 0.0),
        };

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds, positions);

        Group group = Assert.Single(system.Active);
        Assert.Equal(new ulong[] { 1, 2, 3 }, group.Members);
        Assert.Equal(20.0, group.MaxBondDistance);
        Assert.Equal(40.0 / 3.0, group.MeanBondDistance!.Value, 10);
    }

    [Fact]
    public void Step_WithoutPositions_LeavesDistancesUnmeasured_NotZero()
    {
        // Distinction zéro observé / non mesuré (A0-3) : sans table de positions
        // (appel hors moteur), les distances sont `null` — jamais 0.
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Group group = Assert.Single(system.Active);
        Assert.Null(group.MaxBondDistance);
        Assert.Null(group.MeanBondDistance);
    }

    [Fact]
    public void Step_CollectiveDecision_IsExposedWithoutTouchingMinds()
    {
        // A0-3 : la décision collective reste CALCULÉE et EXPOSÉE (état du groupe,
        // LastDecisions), mais la revue ne MUTATE plus aucun esprit (la propagation
        // SYNE-076 et `CollectiveObjective` ont été supprimées).
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
            mind.Intention = new Goal(DesireKind.SeekFood, BornTick: 8);
        }

        var intentionsBefore = minds.ToDictionary(pair => pair.Key, pair => pair.Value.Intention);

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Group group = Assert.Single(system.Active);
        Assert.Equal(DesireKind.SeekFood, group.Decision);
        GroupDecision decision = Assert.Single(system.LastDecisions);
        Assert.Equal(group.Id, decision.GroupId);
        Assert.Equal(DesireKind.SeekFood, decision.Decision);
        Assert.True(decision.Consensus > 0.0);

        foreach ((ulong id, MindState mind) in minds)
        {
            Assert.Equal(intentionsBefore[id], mind.Intention);
        }
    }
}
