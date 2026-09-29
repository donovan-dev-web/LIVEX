using Simulation.Core.Cognition;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Xunit;

namespace Simulation.Core.Tests.Communication;

/// <summary>
/// Garde anti-boucle de la communication et borne de la file sortante
/// (jalon review/refactor, engineVersion 0.12.0).
///
/// <para>
/// <b>Anti-relay.</b> <c>CommunicationState</c> enregistrait l'ensemble « déjà
/// relayé » via <c>RecordRelayed</c>, mais <c>CommunicationSystem.CanRelay</c>
/// ne le consultait pas : le mécanisme était inerte. Un même message pouvait donc
/// être relayé autant de fois que l'agent le rencontrait, via des canaux
/// distincts, au tick suivant. La boucle de relais ne s'arrêtait qu'à
/// <c>MaxHops</c> — l'énergie étant débitée à chaque relais, la cascade
/// épuisait les agents et saturait le réseau. C'est la seule correction de ce
/// jalon à avoir modifié la trajectoire de référence (checksums dorés
/// 0x27fad50065d8c4a4 → 0xdb57f58566418f5d, voir
/// <c>DeterminismRegressionTests</c>).
/// </para>
///
/// <para>
/// <b>File bornée.</b> <c>Drain</c> ne retire que <c>MaxSendsPerTick</c>
/// messages par tick alors que l'esprit peut en produire davantage : sans borne,
/// la file croissait indéfiniment et diffusait des observations périmées des
/// centaines de ticks plus tard.
/// </para>
/// </summary>
public class CommunicationAntiRelayTests
{
    private static Message NewMessage(ulong id, ulong sender = 1, int hops = 0) =>
        new Message(id, sender, targetId: null, MessageType.Information, "contenu", confidence: 0.9, hops, tick: 10);

    [Fact]
    public void HasRelayed_TracksTheRecordedSet()
    {
        var state = new CommunicationState();

        Assert.False(state.HasRelayed(42));

        state.RecordRelayed(42);

        Assert.True(state.HasRelayed(42));
        Assert.False(state.HasRelayed(43));
    }

    [Fact]
    public void Enqueue_WithoutExplicitBound_KeepsEverything()
    {
        // Rétro-compatibilité : l'appel sans borne explicite reste non borné
        // (ceux qui sponge sur l'état de la file obtiennent le comportement
        // historique), la borne n'est appliquée que lorsqu'elle est fournie.
        var state = new CommunicationState();

        for (ulong i = 0; i < 1_000; i++)
        {
            state.Enqueue(NewMessage(i));
        }

        Assert.Equal(1_000, state.OutgoingCount);
    }

    [Fact]
    public void Enqueue_RespectsTheBound_AndDropsTheOldestFirst()
    {
        // Politique « le plus récent gagne » : un message périmé est moins utile
        // qu'une observation fraîche, et la file doit rester bornée.
        var state = new CommunicationState();

        state.Enqueue(NewMessage(1), maxPending: 3);
        state.Enqueue(NewMessage(2), maxPending: 3);
        state.Enqueue(NewMessage(3), maxPending: 3);
        Assert.Equal(3, state.OutgoingCount);

        state.Enqueue(NewMessage(4), maxPending: 3);
        Assert.Equal(3, state.OutgoingCount);

        IReadOnlyList<Message> drained = state.Drain(maxSends: 3).ToList();

        Assert.Equal(3, drained.Count);
        Assert.Equal(2UL, drained[0].MessageId);
        Assert.Equal(3UL, drained[1].MessageId);
        Assert.Equal(4UL, drained[2].MessageId);
    }

    [Fact]
    public void Enqueue_ZeroBound_DropsEverything()
    {
        var state = new CommunicationState();

        state.Enqueue(NewMessage(1), maxPending: 0);

        Assert.Equal(0, state.OutgoingCount);
    }

    [Fact]
    public void Enqueue_NegativeBound_IsRejected()
    {
        var state = new CommunicationState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Enqueue(NewMessage(1), maxPending: -1));
    }

    [Fact]
    public void Enqueue_BoundOfOne_KeepsOnlyTheLatest()
    {
        var state = new CommunicationState();

        state.Enqueue(NewMessage(1), maxPending: 1);
        state.Enqueue(NewMessage(2), maxPending: 1);

        Message only = Assert.Single(state.Drain(maxSends: 10));
        Assert.Equal(2UL, only.MessageId);
    }

    [Fact]
    public void Drain_RespectsMaxSends_AndKeepsTheRemainder()
    {
        // La borne de la file et le débit par tick sont deux contraintes
        // distinctes : la première borne la mémoire, la seconde le trafic.
        var state = new CommunicationState();
        for (ulong i = 0; i < 5; i++)
        {
            state.Enqueue(NewMessage(i + 1), maxPending: 5);
        }

        IReadOnlyList<Message> first = state.Drain(maxSends: 2).ToList();

        Assert.Equal(2, first.Count);
        Assert.Equal(3, state.OutgoingCount);
        Assert.Equal(1UL, first[0].MessageId);
        Assert.Equal(2UL, first[1].MessageId);
    }
}
