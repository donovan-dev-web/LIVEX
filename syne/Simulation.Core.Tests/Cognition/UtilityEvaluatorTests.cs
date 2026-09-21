using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Xunit;

namespace Simulation.Core.Tests;

public class UtilityEvaluatorTests
{
    private static AgentFactors Neutral() => new(TraitSet.NeutralAll);

    [Fact]
    public void Evaluate_AppliesDecision13Formula()
    {
        var needs = BodyNeeds.FromState(hunger: 80);
        var actions = new ActionSettings();

        UtilityScore score = UtilityEvaluator.Evaluate(
            DesireKind.SeekFood,
            needs,
            Neutral(),
            successRate: 0.7,
            goalAge: 0,
            actions);

        // benefit = Min(80, 30) = 30 ; cost = 0.5 ; risk = 0.15 ;
        // confidence = 0.5 × (0.5 + 0.7×0.5) = 0.425 ; personality = 0.5 + greed(1.0) = 1.5 ;
        // urgency = sigmoid(0.1×(80−50)) × 20 ≈ 0.9526 × 20 ≈ 19.05.
        Assert.Equal(30.0, score.Benefit, 10);
        Assert.Equal(0.5, score.Cost, 10);
        Assert.Equal(0.15, score.Risk, 10);
        Assert.Equal(0.425, score.Confidence, 10);
        Assert.Equal(1.5, score.PersonalityModifier, 10);
        Assert.True(score.Urgency > 18.9 && score.Urgency < 19.2);
        Assert.Equal(((30.0 - 0.5 - 0.15) * 0.425 * 1.5) + score.Urgency, score.Utility, 10);
    }

    [Fact]
    public void Urgency_SigmoidIncreasesWithNeed()
    {
        var low = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 60), goalAge: 0);
        var high = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 95), goalAge: 0);

        Assert.True(high > low);
    }

    [Fact]
    public void Urgency_PenalizesStaleGoalsAndCriticalState()
    {
        var baseScore = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 90), goalAge: 0);
        var aged = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 90), goalAge: 200);
        var critical = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 95, energy: 5), goalAge: 0);

        Assert.True(aged > baseScore + 4.9);
        Assert.True(critical > baseScore + 9.9);
    }

    [Fact]
    public void PersonalityModifier_StaysAboveMinimum()
    {
        var factors = new AgentFactors(new TraitSet(
            TraitSet.TraitNames.ToDictionary(name => name, _ => 0.0, StringComparer.Ordinal)));

        double modifier = UtilityEvaluator.PersonalityModifierOf(DesireKind.Explore, factors);

        Assert.Equal(0.5, modifier, 10);
    }

    [Fact]
    public void CuriosityTrait_BoostsExploreUtility()
    {
        var traits = TraitSet.TraitNames.ToDictionary(name => name, _ => 1.0, StringComparer.Ordinal);
        traits["curiosity"] = 1.8;
        double high = UtilityEvaluator.PersonalityModifierOf(DesireKind.Explore, new AgentFactors(new TraitSet(traits)));
        double neutral = UtilityEvaluator.PersonalityModifierOf(DesireKind.Explore, Neutral());

        Assert.True(high > neutral);
    }

    [Fact]
    public void Best_SelectsMaxUtility_AndBreaksTieByKindOrder()
    {
        var first = new UtilityScore(DesireKind.SeekWater, 1, 0, 0, 0.5, 1, 0, 10.0);
        var second = new UtilityScore(DesireKind.SeekFood, 1, 0, 0, 0.5, 1, 0, 12.0);
        var tie = new UtilityScore(DesireKind.Explore, 1, 0, 0, 0.5, 1, 0, 12.0);

        Assert.Equal(DesireKind.SeekFood, UtilityEvaluator.Best([first, second]).Kind);
        Assert.Equal(DesireKind.SeekFood, UtilityEvaluator.Best([second, tie]).Kind);
    }
}