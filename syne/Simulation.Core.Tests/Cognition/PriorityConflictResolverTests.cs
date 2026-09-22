using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// SYNE-033 : résolution probabiliste des conflits de priorités (décision n°22) —
/// force × confiance, sans arbitraire d'ancienneté, tirage déterministe sans PRNG.
/// </summary>
public class PriorityConflictResolverTests
{
    private static UtilityScore Score(DesireKind kind, double confidence) =>
        new(kind, Benefit: 0, Cost: 0, Risk: 0, Confidence: confidence, PersonalityModifier: 1, Urgency: 0, Utility: 0);

    private static BodyNeeds Needs(double hunger = 0, double thirst = 0) =>
        BodyNeeds.FromState(hunger: hunger, thirst: thirst);

    [Fact]
    public void Resolve_ReturnsTheOnlyCandidate()
    {
        var scores = new[] { Score(DesireKind.SeekFood, 0.7) };

        UtilityScore winner = PriorityConflictResolver.Resolve(scores, Needs(hunger: 90), entityId: 1, tick: 1);

        Assert.Equal(DesireKind.SeekFood, winner.Kind);
    }

    [Fact]
    public void Resolve_IsDeterministicForIdenticalContext()
    {
        var scores = new[] { Score(DesireKind.SeekFood, 0.7), Score(DesireKind.SeekWater, 0.7) };

        var first = PriorityConflictResolver.Resolve(scores, Needs(hunger: 90, thirst: 90), entityId: 42, tick: 77);
        var second = PriorityConflictResolver.Resolve(scores, Needs(hunger: 90, thirst: 90), entityId: 42, tick: 77);

        Assert.Equal(first.Kind, second.Kind);
    }

    [Fact]
    public void Resolve_FavorsStrongerDriveAcrossContexts()
    {
        // même confiance → la force (élan du besoin) domine, sans exclure statistiquement le faible.
        var scores = new[] { Score(DesireKind.SeekWater, 0.7), Score(DesireKind.SeekFood, 0.7) };
        var needs = Needs(hunger: 90, thirst: 30);

        int foodWins = 0;
        int waterWins = 0;
        for (ulong id = 1; id <= 500; id++)
        {
            DesireKind winner = PriorityConflictResolver.Resolve(scores, needs, entityId: id, tick: id * 7).Kind;
            if (winner == DesireKind.SeekFood)
            {
                foodWins++;
            }
            else
            {
                waterWins++;
            }
        }

        Assert.True(foodWins > waterWins, $"attendu SeekFood dominant (obtenu {foodWins}/{waterWins}).");
        Assert.True(foodWins > 300, $"SeekFood devrait dominer (obtenu {foodWins}).");
    }

    [Fact]
    public void Resolve_HigherConfidenceBreaksNearTie()
    {
        // forces proches mais confiances différentes → la confiance × force départage.
        var lowTrust = Score(DesireKind.SeekFood, 0.3);
        var highTrust = Score(DesireKind.SeekWater, 0.9);
        var needs = Needs(hunger: 50, thirst: 50);

        int waterWins = 0;
        for (ulong id = 1; id <= 300; id++)
        {
            if (PriorityConflictResolver.Resolve(new[] { highTrust, lowTrust }, needs, entityId: id, tick: 3).Kind == DesireKind.SeekWater)
            {
                waterWins++;
            }
        }

        // p(SeekWater) = (50×0.9) / (50×0.9 + 50×0.3) = 0.75 → large majorité.
        Assert.True(waterWins > 180, $"attendu majorité SeekWater (obtenu {waterWins}).");
    }

    [Fact]
    public void Resolve_ZeroStrengths_FallsBackToStableKindOrder()
    {
        // force nulle des deux côtés : aucun arbitraire d'ancienneté — l'ordre du catalogue (stable) tranche.
        var scores = new[] { Score(DesireKind.SeekWater, 0.5), Score(DesireKind.SeekFood, 0.5) };

        DesireKind winner = PriorityConflictResolver.Resolve(scores, Needs(), entityId: 9, tick: 9).Kind;

        Assert.Equal(DesireKind.SeekFood, winner);
    }

    [Fact]
    public void Resolve_RejectsEmptyCandidates()
    {
        Assert.Throws<ArgumentException>(() => PriorityConflictResolver.Resolve(Array.Empty<UtilityScore>(), Needs(), entityId: 1, tick: 1));
    }
}