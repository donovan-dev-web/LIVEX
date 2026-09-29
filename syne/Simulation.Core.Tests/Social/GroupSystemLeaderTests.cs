using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Social;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Leader émergent et cohésion des groupes (jalon review/refactor,
/// engineVersion 0.12.0).
///
/// <para>
/// <b>Identifiant 0.</b> Le leader était modélisé par un <c>ulong</c> dont la
/// sentinelle <c>0</c> signifiait « pas de leader ». Toute entité d'identifiant
/// <c>0</c> — autorisée par le modèle — ne pouvait donc jamais être élue, et son
/// groupe restait sans décision ni propagation collective. L'absence est
/// désormais <c>null</c> (<see cref="Group.LeaderId"/>).
/// </para>
///
/// <para>
/// <b>Cohésion.</b> La cohésion n'était pas recalculée après un changement de
/// composition : <c>MeanCohesion</c> conservait la valeur mesurée à la formation,
/// exposant une valeur périmée dans l'observabilité et les métriques.
/// </para>
/// </summary>
public class GroupSystemLeaderTests
{
    private static SimulationOptions Options() => ConfigLoader.LoadDefaults();

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

    /// <summary>
    /// Lie fortement chaque esprit aux autres et leur donne une croyance commune,
    /// ce qui fait monter la cohésion au-dessus du seuil de formation.
    /// </summary>
    private static Dictionary<ulong, MindState> LinkedMinds(SimulationOptions options, IEnumerable<ulong> ids, int bonds = 20)
    {
        var minds = new Dictionary<ulong, MindState>();
        foreach (ulong id in ids)
        {
            minds[id] = new MindState(options);
        }

        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys)
            {
                if (peer != self)
                {
                    Bond(mind, peer, bonds);
                }
            }

            ShareBelief(mind, options, target: 9, tick: 9);
        }

        return minds;
    }

    [Fact]
    public void EntityZero_IsNotMistakenForTheAbsenceOfALeader()
    {
        // Prérequis : l'identifiant 0 est un identifiant d'entité valide et ne
        // doit pas être confondu avec une sentinelle « aucun ».
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = LinkedMinds(options, [0, 1, 2]);

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Group group = Assert.Single(system.Active);
        Assert.Equal([0UL, 1UL, 2UL], group.Members);
        // Leader non nul et présent : la valeur n'est pas la sentinelle.
        Assert.NotNull(group.LeaderId);
    }

    [Fact]
    public void EntityZero_CanActuallyBeElectedLeader()
    {
        // Pour que 0 soit élu, sa confiance entrante doit être la plus forte.
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = LinkedMinds(options, [0, 1, 2], bonds: 20);

        Bond(minds[1], 0, 60);
        Bond(minds[2], 0, 60);
        Bond(minds[0], 1, 2);
        Bond(minds[0], 2, 2);
        Bond(minds[1], 2, 2);
        Bond(minds[2], 1, 2);

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Group group = Assert.Single(system.Active);
        Assert.Equal(0UL, group.LeaderId);
    }

    [Fact]
    public void Leader_IsNullOnlyWhenThereIsGenuinelyNoGroup()
    {
        // Aucun groupe actif ⇒ aucun leader. Le point est que l'absence se lit
        // comme null/absence de groupe, jamais comme « identifiant 0 ».
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> isolated = [];
        for (ulong id = 1; id <= 3; id++)
        {
            isolated[id] = new MindState(options);
        }

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, isolated);

        Assert.Empty(system.Active);
    }

    [Fact]
    public void MeanCohesion_IsRefreshed_WhenTrustEvolves_WithoutMembershipChange()
    {
        // La composition du groupe est identifiée par <b>égalité exacte</b> de
        // l'ensemble de membres : un groupe dont les membres ne changent pas
        // survit aux révisions. Or la cohésion dépend des confiances et des
        // croyances, qui évoluent d'un tick à l'autre (interactions, oubli). La
        /// Rafraîchir la cohésion était donc la seule façon de garder
        // elle, la valeur restait figée sur la dernière recomposition.
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = LinkedMinds(options, [1, 2, 3], bonds: 2);

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);

        Group formed = Assert.Single(system.Active);
        Assert.Equal([1UL, 2UL, 3UL], formed.Members);
        double cohesionAtFormation = formed.MeanCohesion;

        // Les trois membres se rencontrent fortement : la confiance mutuelle monte, donc
        // la cohésion doit monter avec elle.
        foreach ((ulong self, MindState mind) in minds)
        {
            foreach (ulong peer in minds.Keys)
            {
                if (peer != self)
                {
                    Bond(mind, peer, 40);
                }
            }
        }

        system.Step(tick: 20, minds);

        Group after = Assert.Single(system.Active);
        Assert.Equal(formed.Id, after.Id);
        Assert.Equal(formed.Members, after.Members);
        Assert.True(
            after.MeanCohesion > cohesionAtFormation,
            $"la cohésion doit suivre l'évolution des confiances "
            + $"({cohesionAtFormation} → {after.MeanCohesion}) et rester figée sinon.");
    }

    [Fact]
    public void MeanCohesion_IsRepublished_OnEveryReview()
    {
        // Deux révisions successives sans changement : la valeur doit rester
        // stable et strictement positive (elle n'est ni nulle ni périmée).
        SimulationOptions options = Options();
        Dictionary<ulong, MindState> minds = LinkedMinds(options, [1, 2, 3], bonds: 25);

        var system = new GroupSystem(options.Groups);
        system.Step(tick: 10, minds);
        double atTen = Assert.Single(system.Active).MeanCohesion;

        system.Step(tick: 20, minds);
        double atTwenty = Assert.Single(system.Active).MeanCohesion;

        Assert.True(atTen > 0.0);
        Assert.Equal(atTen, atTwenty, 10);
    }
}
