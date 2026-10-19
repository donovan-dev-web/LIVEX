// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests de la croyance et de sa révision bornée.

using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests;

public class BeliefTests
{
    private static BeliefSettings DefaultBeliefs() => new();

    [Fact]
    public void Revise_AppliesBoundedBlend_Decision12()
    {
        // belief = belief + (signal − belief) × strength
        double revised = BeliefRevision.Revise(
            current: 0.8,
            signal: 0.2,
            strength: 0.3,
            maxChangePerSnap: 1.0);

        Assert.Equal(0.62, revised, 10);
    }

    [Fact]
    public void Revise_EnforcesPerSnapCap()
    {
        // Un signal trompeur ne peut pas inverser une croyance forte en une seule fois.
        double revised = BeliefRevision.Revise(
            current: 0.9,
            signal: 0.0,
            strength: 1.0,
            maxChangePerSnap: 0.5);

        Assert.Equal(0.4, revised, 10);
    }

    [Fact]
    public void ApplyEvidence_CreatesBeliefFromSignal()
    {
        var set = new BeliefSet();
        var fact = new Fact("entity-2", "position", "80.00,50.25");

        set.ApplyEvidence(fact, signal: 0.85, source: "perception-1", DefaultBeliefs(), tick: 5);

        Assert.True(set.TryGet(fact, out Belief? belief));
        Assert.Equal(0.85, belief!.Confidence);
        Assert.Equal(5UL, belief.BornTick);
        Assert.Equal(105UL, belief.ExpiryTick);
    }

    [Fact]
    public void ApplyEvidence_SameSourceAlignsByBonus()
    {
        var set = new BeliefSet();
        var fact = new Fact("entity-2", "position", "80.00,50.25");
        set.ApplyEvidence(fact, signal: 0.8, source: "perception-1", DefaultBeliefs(), tick: 5);

        set.ApplyEvidence(fact, signal: 0.8, source: "perception-1", DefaultBeliefs(), tick: 6);

        Assert.True(set.TryGet(fact, out Belief? belief));
        Assert.Equal(1.0, belief!.Confidence, 10);
        Assert.Equal(6UL, belief.UpdatedTick);
    }

    [Fact]
    public void ApplyEvidence_DifferentSourcesAverage()
    {
        var set = new BeliefSet();
        var fact = new Fact("entity-2", "position", "80.00,50.25");
        set.ApplyEvidence(fact, signal: 0.8, source: "perception-1", DefaultBeliefs(), tick: 5);

        set.ApplyEvidence(fact, signal: 0.6, source: "rumour", DefaultBeliefs(), tick: 6);

        Assert.True(set.TryGet(fact, out Belief? belief));
        Assert.Equal(0.7, belief!.Confidence, 10);
    }

    [Fact]
    public void ApplyEvidence_ConflictingValuePenalizesThenReplaces_TheSingleCurrentBelief()
    {
        // S-09 : une seule croyance courante par (sujet, prédicat) — une position
        // contradictoire ne coexiste pas avec la précédente, elle la remplace.
        var set = new BeliefSet();
        var first = new Fact("entity-2", "position", "80.00,50.25");
        set.ApplyEvidence(first, signal: 0.9, source: "perception-1", DefaultBeliefs(), tick: 5);

        var second = new Fact("entity-2", "position", "40.00,10.00");
        set.ApplyEvidence(second, signal: 0.4, source: "perception-1", DefaultBeliefs(), tick: 6);

        Assert.False(set.TryGet(first, out _));
        Assert.Equal(1, set.Count);
        Assert.True(set.TryGetCurrent("entity-2", "position", out Belief? current));
        Assert.Equal("40.00,10.00", current!.Fact.Value);
        Assert.Equal(6UL, current.UpdatedTick);
        // La croyance reste celle du <b>sujet</b> : son âge (BornTick) court depuis la
        // première observation, seule la valeur et l'horodatage de mise à jour changent.
        Assert.Equal(5UL, current.BornTick);

        // Pénalité de conflit effective : le prior pénalisé (0,9 − 0,1) est révisé
        // vers le signal (0,4) — Revise(0,8 ; 0,4 ; 0,3) = 0,68 et non 0,4 (le
        // signal n'est pas installé tel quel) ni 0,8 (la croyance antérieure est
        // bel et bien pénalisée).
        Assert.Equal(0.68, current.Confidence, 10);
    }

    [Fact]
    public void Tick_ExpiryCapsConfidenceAtFortyPercent()
    {
        var settings = new BeliefSettings { ExpiryTicks = 10, ExpiredCap = 0.4, TimeDecayPerTick = 0.999 };
        var set = new BeliefSet();
        var fact = new Fact("entity-2", "alive", "true");
        set.ApplyEvidence(fact, signal: 0.9, source: "perception-1", settings, tick: 0);

        set.Tick(currentTick: 11, settings);

        Assert.True(set.TryGet(fact, out Belief? belief));
        Assert.True(belief!.Confidence <= 0.4);
    }

    [Fact]
    public void Tick_DecaysNonUpdatedBeliefs()
    {
        var settings = new BeliefSettings { TimeDecayPerTick = 0.9 };
        var set = new BeliefSet();
        var fact = new Fact("entity-2", "alive", "true");
        set.ApplyEvidence(fact, signal: 0.8, source: "perception-1", settings, tick: 0);

        set.Tick(currentTick: 1, settings);

        Assert.True(set.TryGet(fact, out Belief? belief));
        Assert.Equal(0.72, belief!.Confidence, 10);
    }
}
