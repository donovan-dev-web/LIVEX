using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Social;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Tests du système de groupes émergents (SYNE-060/061, SYSTEM_SPEC.md §5) :
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
    public void PropagateObjectives_AdoptsCollectiveDecision_InMembersTTL()
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
        Assert.Single(system.Active);

        system.PropagateObjectives(currentTick: 10, ttlTicks: 10, minds);

        foreach (ulong member in new ulong[] { 1, 2, 3 })
        {
            GroupObjective? objective = minds[member].CollectiveObjective;
            Assert.NotNull(objective);
            Assert.Equal(1UL, objective.Value.GroupId);
            Assert.Equal(DesireKind.SeekFood, objective.Value.Kind);
            // Consensus 1.0 × confiance leader > 0 → bonus maximal ×1.2.
            Assert.Equal(1.0, objective.Value.Consensus, 12);
            Assert.True(objective.Value.LeaderTrust > 0.0);
            Assert.Equal(10UL, objective.Value.AdoptedTick);
            Assert.Equal(20UL, objective.Value.ExpiresTick);
        }
    }

    [Fact]
    public void PropagateObjectives_WithoutDecision_ClearsObjective()
    {
        SimulationOptions options = Options();
        options.Groups.ConsensusThreshold = 1.0; // empêche l'adoption (divergence)

        Dictionary<ulong, MindState> minds = Minds(options, 3);
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys.Where(id => id != self))
            {
                Bond(mind, peer);
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        // Entités 1 et 2 sur SeekWater, entité 3 en divergence (Rest) :
        // consensus SeekWater = 0.5 < quorum 1.0 → aucune décision adoptée.
        minds[1].Intention = new Goal(DesireKind.SeekWater, BornTick: 8);
        minds[2].Intention = new Goal(DesireKind.SeekWater, BornTick: 8);
        minds[3].Intention = new Goal(DesireKind.Rest, BornTick: 8);

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);
        Assert.Null(Assert.Single(system.Active).Decision);

        system.PropagateObjectives(currentTick: 10, ttlTicks: 10, minds);

        foreach (ulong member in new ulong[] { 1, 2, 3 })
        {
            Assert.Null(minds[member].CollectiveObjective);
        }
    }

    [Fact]
    public void PropagateObjectives_WithoutLeaderTrust_DoesNotAlign()
    {
        // Chaîne 1-2-3-4 : leader émergent = 2 (confiance entrante max, départage
        // par id min). Le membre 4 n'a aucune confiance envers le leader 2 (pas de
        // lien direct) : il est dans le groupe sans s'aligner sur son objectif.
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = Minds(options, 4);
        foreach ((ulong self, MindState mind) in minds)
        {
            ShareBelief(mind, options, target: 9, tick: 9);
            mind.Intention = new Goal(DesireKind.SeekFood, BornTick: 8);
        }

        foreach ((ulong a, ulong b) in new[] { (1UL, 2UL), (2UL, 3UL), (3UL, 4UL) })
        {
            Bond(minds[a], b);
            Bond(minds[b], a);
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);
        Assert.Equal(2UL, Assert.Single(system.Active).LeaderId);

        system.PropagateObjectives(currentTick: 10, ttlTicks: 10, minds);

        Assert.NotNull(minds[1].CollectiveObjective);
        Assert.NotNull(minds[2].CollectiveObjective);
        Assert.NotNull(minds[3].CollectiveObjective);
        // Sans confiance envers le leader, le membre 4 ne s'aligne pas.
        Assert.Null(minds[4].CollectiveObjective);
    }
}