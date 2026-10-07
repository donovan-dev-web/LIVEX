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

        // benefit = Min(80, 100) × 0.6 = 48 (calibration D1 : plafond monotone 60,
        // croissant avec le besoin — l'ancien plafond 30 écrasait Eat/Drink) ;
        // cost = moveEnergyCost = 0,03 (calibration B1, ADR-016) ; risk = 0.15 ;
        // confidence = 0.5 × (0.5 + 0.7×0.5) = 0.425 ; personality = 0.5 + greed(1.0) = 1.5 ;
        // urgency = sigmoid(0.1×(80−50)) × 20 ≈ 0.9526 × 20 ≈ 19.05.
        Assert.Equal(48.0, score.Benefit, 10);
        Assert.Equal(0.03, score.Cost, 10);
        Assert.Equal(0.15, score.Risk, 10);
        Assert.Equal(0.425, score.Confidence, 10);
        Assert.Equal(1.5, score.PersonalityModifier, 10);
        Assert.True(score.Urgency > 18.9 && score.Urgency < 19.2);
        Assert.Equal(((48.0 - 0.03 - 0.15) * 0.425 * 1.5) + score.Urgency, score.Utility, 10);
    }

    [Fact]
    public void Urgency_SigmoidIncreasesWithNeed()
    {
        var low = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 60), goalAge: 0, new ActionSettings());
        var high = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 95), goalAge: 0, new ActionSettings());

        Assert.True(high > low);
    }

    [Fact]
    public void Urgency_PenalizesStaleGoalsAndCriticalState()
    {
        var baseScore = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 80), goalAge: 0, new ActionSettings());
        var aged = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 90), goalAge: 200, new ActionSettings());
        var critical = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 95, energy: 5), goalAge: 0, new ActionSettings());

        Assert.True(aged > baseScore + 4.9);
        Assert.True(critical > baseScore + 9.9);
    }

    [Fact]
    public void Urgency_CriticalUsesConfigurableThresholds()
    {
        // Seuil de faim critique : 85 (COGNITIVE_ARCHITECTURE.md §6, agents.interruption.criticalHunger).
        var nonCritical = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 84), goalAge: 0, new ActionSettings());
        var critical = UtilityEvaluator.UrgencyOf(DesireKind.SeekFood, BodyNeeds.FromState(hunger: 86), goalAge: 0, new ActionSettings());

        Assert.True(critical > nonCritical + 9.9);
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

    [Fact]
    public void Evaluate_AlignBonusBoostsBenefitOfCurrentIntention()
    {
        // SYNE-030 : bénéfice × alignBonus (défaut 1.2) si l'action rejoint l'objectif courant.
        var needs = BodyNeeds.FromState(hunger: 80);
        var actions = new ActionSettings();

        UtilityScore aligned = UtilityEvaluator.Evaluate(
            DesireKind.SeekFood, needs, Neutral(), successRate: 0.7, goalAge: 0, actions, currentIntention: DesireKind.SeekFood);
        UtilityScore otherwise = UtilityEvaluator.Evaluate(
            DesireKind.SeekFood, needs, Neutral(), successRate: 0.7, goalAge: 0, actions, currentIntention: DesireKind.SeekWater);

        Assert.Equal(48.0 * 1.2, aligned.Benefit, 10);
        Assert.Equal(48.0, otherwise.Benefit, 10);
        Assert.True(aligned.Utility > otherwise.Utility);
    }

    [Fact]
    public void Evaluate_CollectiveObjective_ScalesAlignmentByConsensusAndLeaderTrust()
    {
        // SYNE-076 : le bénéfice d'une action conforme à l'objectif collectif est
        // bonifié de ×1,2 quand consensus = 1 et confiance leader = 1 ; pondéré
        // par le produit consensus × confiance sinon.
        var needs = BodyNeeds.FromState(hunger: 80, thirst: 70);
        var actions = new ActionSettings();
        var full = new GroupObjective(
            GroupId: 1, Kind: DesireKind.SeekFood, Consensus: 1.0, LeaderTrust: 1.0,
            AdoptedTick: 1, ExpiresTick: 11);
        var half = full with { Consensus = 0.5, LeaderTrust = 1.0 };

        UtilityScore aligned = UtilityEvaluator.Evaluate(
            DesireKind.SeekFood, needs, Neutral(), successRate: 0.7, goalAge: 0, actions,
            currentIntention: null, collectiveObjective: full);
        UtilityScore partial = UtilityEvaluator.Evaluate(
            DesireKind.SeekFood, needs, Neutral(), successRate: 0.7, goalAge: 0, actions,
            currentIntention: null, collectiveObjective: half);
        UtilityScore nonConforming = UtilityEvaluator.Evaluate(
            DesireKind.SeekWater, needs, Neutral(), successRate: 0.7, goalAge: 0, actions,
            currentIntention: null, collectiveObjective: full);

        // 48 × 1.2 (plein) ; 48 × (1 + 0.2 × 0.5) = 52,8 (moitié) ; 42 (non conforme :
        // SeekWater lit la soif 70 → min(70,100) × 0.6 = 42) — bénéfices recalés par
        // la calibration D1 (plafond monotone 60, croissant avec le besoin).
        Assert.Equal(57.6, aligned.Benefit, 10);
        Assert.Equal(52.8, partial.Benefit, 10);
        Assert.Equal(42.0, nonConforming.Benefit, 10);
        Assert.True(aligned.Utility > partial.Utility);
        Assert.True(partial.Utility > nonConforming.Utility);
    }

    [Fact]
    public void ApplyActionSwitchMargin_BlocksUndecisiveSwitch()
    {
        // SYNE-031 : hystérésis anti-oscillation (actionSwitchMargin défaut 0.05).
        var needs = BodyNeeds.FromState(hunger: 80, thirst: 70);
        var actions = new ActionSettings();
        var factor = Neutral();

        UtilityScore current = UtilityEvaluator.Evaluate(
            DesireKind.SeekWater, needs, factor, successRate: 0.7, goalAge: 0, actions, currentIntention: DesireKind.SeekWater);
        UtilityScore barelyBetter = UtilityEvaluator.Evaluate(
            DesireKind.SeekFood, needs, factor, successRate: 0.7, goalAge: 0, actions, currentIntention: DesireKind.SeekWater);

        UtilityScore blocked = UtilityEvaluator.ApplyActionSwitchMargin(
            barelyBetter, current.Kind, needs, factor, successRate: 0.7, goalAge: 0, actions);

        // Le candidat ne dépasse pas la marge → l'action courante est conservée.
        Assert.Equal(DesireKind.SeekWater, blocked.Kind);
    }

    [Fact]
    public void ApplyActionSwitchMargin_AllowsDecisiveSwitch_AndSameKind()
    {
        var needs = BodyNeeds.FromState(hunger: 95, thirst: 30);
        var actions = new ActionSettings();
        var factor = Neutral();

        UtilityScore dominant = UtilityEvaluator.Evaluate(
            DesireKind.SeekFood, needs, factor, successRate: 0.7, goalAge: 0, actions, currentIntention: DesireKind.SeekWater);

        UtilityScore selected = UtilityEvaluator.ApplyActionSwitchMargin(
            dominant, DesireKind.SeekWater, needs, factor, successRate: 0.7, goalAge: 0, actions);

        Assert.Equal(DesireKind.SeekFood, selected.Kind);
        Assert.Equal(dominant.Utility, selected.Utility, 10);

        UtilityScore sameKind = UtilityEvaluator.ApplyActionSwitchMargin(
            dominant, DesireKind.SeekFood, needs, factor, successRate: 0.7, goalAge: 0, actions);
        Assert.Equal(DesireKind.SeekFood, sameKind.Kind);

        UtilityScore noCurrent = UtilityEvaluator.ApplyActionSwitchMargin(
            dominant, currentKind: null, needs, factor, successRate: 0.7, goalAge: 0, actions);
        Assert.Equal(DesireKind.SeekFood, noCurrent.Kind);
    }
}