// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Gate de la Phase 2 du réalignement — la décision consomme la CONNAISSANCE de l'agent : deux agents à état
//        physiologique identique mais à connaissances différentes génèrent des candidats différents, et la chaîne
//        mémoire → croyance → cible → objectif est remontable.

using System.Globalization;
using Simulation.Core.Actions;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Gate de la Phase 2 (S-02/S-05/S-07/B-05) : « à état physique identique mais
/// connaissances différentes, des agents ont des candidats/évaluations différents
/// quand ces connaissances sont pertinentes, pour des raisons traçables ».
///
/// <para>
/// Les deux agents de chaque paire partagent strictement les mêmes besoins, les
/// mêmes traits et la même position ; seule diffère l'information qu'ils
/// détiennent (aucune, un souvenir, une croyance). Chaque différence de candidat
/// est donc imputable à l'information, et non à l'état physiologique.
/// </para>
/// </summary>
public class CognitionKnowledgeTests
{
    private static readonly Position Self = new(250.0, 250.0);

    private static SimulationOptions Options()
    {
        SimulationOptions options = ConfigLoader.LoadDefaults();
        options.Resources.Localized = true;
        return options;
    }

    /// <summary>Agent affamé et assoiffé, sans aucune connaissance du monde.</summary>
    private static MindState IgnorantAgent(SimulationOptions options)
    {
        var mind = new MindState(options);
        mind.Needs.RestoreState(hunger: 80.0, thirst: 80.0, fatigue: 0.0, safety: 1.0, social: 0.0, curiosity: 0.0, energy: 60.0);
        return mind;
    }

    private static void BelieveNode(MindState mind, SimulationOptions options, string label, double x, double y, double amount, ulong tick)
    {
        // Format canonique du pipeline (culture invariante) : un fait écrit avec le
        // séparateur décimal de la culture courante ne serait pas relu par
        // KnowledgeBuilder — le test mesurerait alors la culture, pas la cognition.
        mind.Beliefs.ApplyObservation(
            new Fact(
                $"node-{label}",
                KnowledgeBuilder.NodeStatePredicate,
                string.Create(CultureInfo.InvariantCulture, $"{x:0.00},{y:0.00},{amount:0.###}")),
            1.0,
            "perception-1",
            options.Agents.Beliefs,
            tick);
    }

    private static void RememberNode(
        MindState mind,
        SimulationOptions options,
        string label,
        double x,
        double y,
        double amount,
        ulong tick)
    {
        mind.Memory.Store(
            MemoryCategory.Observation,
            "perception-1",
            string.Create(CultureInfo.InvariantCulture, $"node|{label}|{x:0.00}|{y:0.00}|{amount:0.###}|1.000"),
            1.0,
            tick);
    }

    [Fact]
    public void IgnorantAgent_GeneratesNoTargetedGoal_AndCannotEat()
    {
        SimulationOptions options = Options();
        var catalog = new ActionCatalog(options.Agents.Actions);
        var stocks = new ResourceStocks(options.Resources);
        MindState mind = IgnorantAgent(options);
        CognitionKnowledge knowledge = KnowledgeBuilder.Build(mind, Self, 10, options.Agents.Beliefs, options.Resources.InteractionRadius);

        Assert.False(knowledge.KnowsFood);
        Assert.False(knowledge.KnowsWater);
        Assert.Null(knowledge.TargetFor(DesireKind.SeekFood));

        IReadOnlyList<Goal> goals = DesireFactory.Generate(mind.Needs, 10, [], catalog, stocks, knowledge);

        // Aucune source connue : la faim ne produit pas d'action terminale (Eat)
        // et pas davantage une quête ciblée — l'agent prospecte.
        Assert.DoesNotContain(goals, goal => goal.Kind == DesireKind.Eat);
        Assert.DoesNotContain(goals, goal => goal.Kind == DesireKind.SeekFood && goal.HasTarget);
        Assert.Contains(goals, goal => goal.Kind == DesireKind.Explore);
    }

    [Fact]
    public void BelievedSourceAtHand_ProducesTheTerminalAction_FromTheSameNeeds()
    {
        SimulationOptions options = Options();
        var catalog = new ActionCatalog(options.Agents.Actions);
        var stocks = new ResourceStocks(options.Resources);
        MindState mind = IgnorantAgent(options);
        BelieveNode(mind, options, "resource-food-1", Self.X, Self.Y, amount: 100.0, tick: 9);

        CognitionKnowledge knowledge = KnowledgeBuilder.Build(mind, Self, 10, options.Agents.Beliefs, options.Resources.InteractionRadius);

        Assert.True(knowledge.AtFoodSource);
        IReadOnlyList<Goal> goals = DesireFactory.Generate(mind.Needs, 10, [], catalog, stocks, knowledge);

        // Mêmes besoins, même position : la seule différence est la croyance, et
        // c'est elle qui produit l'action terminale (et sa cible, son âge, sa confiance).
        Goal eat = Assert.Single(goals, goal => goal.Kind == DesireKind.Eat);
        Assert.Equal("resource-food-1", eat.Target?.Label);
        Assert.Equal(9UL, eat.Target?.ObservedTick);
        Assert.Equal(1.0, eat.Target?.Confidence ?? 0.0, 10);
    }

    [Fact]
    public void SourceKnownButOutOfReach_ProducesATargetedPursuit()
    {
        SimulationOptions options = Options();
        var catalog = new ActionCatalog(options.Agents.Actions);
        var stocks = new ResourceStocks(options.Resources);
        MindState mind = IgnorantAgent(options);
        BelieveNode(mind, options, "resource-water-1", x: 320.0, y: 250.0, amount: 100.0, tick: 9);

        CognitionKnowledge knowledge = KnowledgeBuilder.Build(mind, Self, 10, options.Agents.Beliefs, options.Resources.InteractionRadius);

        Assert.False(knowledge.AtWaterSource);
        IReadOnlyList<Goal> goals = DesireFactory.Generate(mind.Needs, 10, [], catalog, stocks, knowledge);

        Goal seek = Assert.Single(goals, goal => goal.Kind == DesireKind.SeekWater);
        Assert.True(seek.HasTarget, "Une source connue hors de portée doit porter sa cible.");
        Assert.Equal(320.0, seek.Target!.Value.Position.X, 10);
        Assert.Equal(0UL, seek.Target.Value.Age(9));
        Assert.Equal(9UL, seek.Target.Value.ObservedTick);
    }

    [Fact]
    public void DepletedSource_IsNotATarget_TheObjectiveIsRevised()
    {
        SimulationOptions options = Options();
        var catalog = new ActionCatalog(options.Agents.Actions);
        var stocks = new ResourceStocks(options.Resources);
        MindState mind = IgnorantAgent(options);
        BelieveNode(mind, options, "resource-food-1", Self.X, Self.Y, amount: 0.0, tick: 9);

        CognitionKnowledge knowledge = KnowledgeBuilder.Build(mind, Self, 10, options.Agents.Beliefs, options.Resources.InteractionRadius);

        Assert.False(knowledge.KnowsFood);
        IReadOnlyList<Goal> goals = DesireFactory.Generate(mind.Needs, 10, [], catalog, stocks, knowledge);
        Assert.DoesNotContain(goals, goal => goal.Kind == DesireKind.Eat);
    }

    [Fact]
    public void RecalledMemory_SubstitutesForAnExpiredBelief()
    {
        // S-07 / B-05 : la mémoire n'est pas un stockage inerte — une observation
        // encore saillante fournit une cible quand la croyance n'a pas survécu.
        SimulationOptions options = Options();
        MindState mind = IgnorantAgent(options);
        RememberNode(mind, options, "resource-water-1", x: 400.0, y: 250.0, amount: 80.0, tick: 5);

        CognitionKnowledge knowledge = KnowledgeBuilder.Build(
            mind,
            Self,
            currentTick: 6,
            options.Agents.Beliefs,
            options.Resources.InteractionRadius);

        Assert.True(knowledge.KnowsWater);
        Assert.Equal(400.0, knowledge.WaterSource!.Value.Position.X, 10);
        Assert.Equal(5UL, knowledge.WaterSource.Value.ObservedTick);
        // La confiance d'un souvenir est bornée par sa salience : un souvenir qui
        // s'efface ne peut pas produire une croyance ferme.
        Assert.True(knowledge.WaterSource.Value.Confidence <= 1.0);
    }

    [Fact]
    public void Knowledge_DrivesUtility_NotOnlyTheCandidateSet()
    {
        // La confiance épistémique entre dans le score : la même action évaluée par
        // deux agents identiques diffère selon ce qu'ils savent de leur cible.
        SimulationOptions options = Options();
        var actions = options.Agents.Actions;
        MindState ignorant = IgnorantAgent(options);
        MindState informed = IgnorantAgent(options);
        BelieveNode(informed, options, "resource-water-1", x: 260.0, y: 250.0, amount: 100.0, tick: 9);

        CognitionKnowledge ignorantKnowledge = KnowledgeBuilder.Build(
            ignorant, Self, 10, options.Agents.Beliefs, options.Resources.InteractionRadius);
        CognitionKnowledge informedKnowledge = KnowledgeBuilder.Build(
            informed, Self, 10, options.Agents.Beliefs, options.Resources.InteractionRadius);

        var factors = new AgentFactors(TraitSet.NeutralAll);
        UtilityScore withoutKnowledge = UtilityEvaluator.Evaluate(
            DesireKind.SeekWater,
            ignorant.Needs,
            factors,
            ignorant.SuccessRate(DesireKind.SeekWater),
            goalAge: 0,
            actions,
            knowledgeConfidence: ignorantKnowledge.EpistemicFor(DesireKind.SeekWater));
        UtilityScore withKnowledge = UtilityEvaluator.Evaluate(
            DesireKind.SeekWater,
            informed.Needs,
            factors,
            informed.SuccessRate(DesireKind.SeekWater),
            goalAge: 0,
            actions,
            knowledgeConfidence: informedKnowledge.EpistemicFor(DesireKind.SeekWater));

        Assert.True(
            withKnowledge.Utility > withoutKnowledge.Utility,
            $"Une cible crue doit peser davantage qu'une absence de connaissance ({withKnowledge.Utility:F2} vs {withoutKnowledge.Utility:F2}).");
        Assert.True(withKnowledge.Confidence > withoutKnowledge.Confidence);
    }
}
