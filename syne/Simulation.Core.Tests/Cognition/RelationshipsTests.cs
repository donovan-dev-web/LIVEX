using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests;

public class RelationshipsTests
{
    private static TrustSettings DefaultTrust() => new();

    [Fact]
    public void Interact_NewPeer_StartsAtInitialTrust()
    {
        var trust = new Relationships(DefaultTrust());

        Assert.Equal(0.5, trust.Interact(peerId: 7));
        Assert.True(trust.Knows(7));
        Assert.Equal(0.5, trust.TrustWith(7));
    }

    [Fact]
    public void Interact_UnknownPeer_ReturnsZeroTrust()
    {
        var trust = new Relationships(DefaultTrust());

        Assert.Equal(0.0, trust.TrustWith(99));
        Assert.False(trust.Knows(99));
    }

    [Fact]
    public void Interact_KnownPeer_ReinforcesWithTruthBonus_AndCapsAtOne()
    {
        var trust = new Relationships(DefaultTrust());
        trust.Interact(7);

        Assert.Equal(0.55, trust.Interact(7), 12);

        for (int i = 0; i < 50; i++)
        {
            trust.Interact(7);
        }

        Assert.Equal(1.0, trust.TrustWith(7), 12);
    }

    [Fact]
    public void Tick_DecaysRelationshipsWithoutInteraction_ByDecayFactor()
    {
        var trust = new Relationships(DefaultTrust());
        trust.Interact(7);
        trust.Interact(7); // renforcement → 0.55

        // Interaction au cycle courant → pas de décroissance ce tick.
        trust.Tick();
        Assert.Equal(0.55, trust.TrustWith(7), 12);

        // Cycle sans interaction → trust × 0.9.
        trust.Tick();
        Assert.Equal(0.55 * 0.9, trust.TrustWith(7), 12);
    }

    [Fact]
    public void ObserveDeception_PenalisesTrust_AndFloorsAtZero()
    {
        var trust = new Relationships(DefaultTrust());
        trust.Interact(7);

        Assert.Equal(0.3, trust.ObserveDeception(7), 12);
        Assert.Equal(0.1, trust.ObserveDeception(7), 12);
        Assert.Equal(0.0, trust.ObserveDeception(7), 12);
        Assert.Equal(0.0, trust.TrustWith(7), 12);
    }

    [Fact]
    public void ObserveDeception_OnUnknownPeer_CreatesZeroTrust()
    {
        var trust = new Relationships(DefaultTrust());

        Assert.Equal(0.0, trust.ObserveDeception(42));
        Assert.True(trust.Knows(42));
    }

    [Fact]
    public void Snapshot_IsOrderedByPeerId()
    {
        var trust = new Relationships(DefaultTrust());
        trust.Interact(9);
        trust.Interact(2);
        trust.Interact(5);

        IReadOnlyList<(ulong PeerId, double Trust)> snapshot = trust.Snapshot();

        Assert.Equal([2UL, 5UL, 9UL], snapshot.Select(relation => relation.PeerId));
    }

    [Fact]
    public void Tick_Multipeer_IsCommutativeAndDeterministic()
    {
        var trust = new Relationships(DefaultTrust());
        trust.Interact(3);
        trust.Interact(3); // établit 0.55
        trust.Interact(8);

        trust.Tick();
        trust.Tick();

        Assert.Equal(
            [0.55 * 0.9, 0.5 * 0.9],
            trust.Snapshot().Select(relation => relation.Trust));
    }
}