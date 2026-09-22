using Simulation.Core.Cognition;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;
using Xunit;

namespace Simulation.Core.Tests;

/// <summary>
/// Tests du sous-système de communication par pulsations (SYNE-050 → 054,
/// COMMUNICATION_PROTOCOL.md, décisions n°7-10).
/// </summary>
public class CommunicationSystemTests
{
    private static CommunicationSettings Settings(Action<CommunicationSettings>? configure = null)
    {
        var settings = new CommunicationSettings();
        configure?.Invoke(settings);
        return settings;
    }

    private static Entity MakeEntity(ulong id, Position position, double sociability = 1.0)
    {
        var traits = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["bravery"] = 1.0,
            ["curiosity"] = 1.0,
            ["sociability"] = sociability,
            ["greed"] = 1.0,
            ["pessimism"] = 1.0,
            ["aggression"] = 1.0,
            ["strength"] = 1.0,
            ["speed"] = 1.0,
        };
        return new Entity(new EntityId(id), "Entité", null, position, new TraitSet(traits), bornAt: 0);
    }

    private static (Simulation.Core.World.World World, CommunicationSystem System, Dictionary<ulong, MindState> Minds) Build(
        CommunicationSettings settings,
        params (ulong Id, Position Position, double Sociability)[] entities)
    {
        var world = new Simulation.Core.World.World(new WorldSize(500, 500));
        var minds = new Dictionary<ulong, MindState>();
        var options = ConfigLoader.LoadDefaults();
        options.Communication = settings;
        foreach ((ulong id, Position position, double sociability) in entities)
        {
            Entity entity = MakeEntity(id, position, sociability);
            world.AddEntity(entity);
            minds[id] = new MindState(options);
        }

        var system = new CommunicationSystem(world, settings);
        return (world, system, minds);
    }

    private static Message Message(ulong senderId, string payload, ulong? targetId = null, ulong tick = 1, int sequence = 1) =>
        CommunicationSystem.CreateMessage(senderId, targetId, MessageType.Information, payload, tick, sequence);

    [Fact]
    public void Broadcast_ReachesEntitiesInRangeWithLineOfSight()
    {
        (_, CommunicationSystem system, Dictionary<ulong, MindState> minds) = Build(
            Settings(s => s.TransmissionRange = 30),
            (1, new Position(10, 10), 1.0),
            (2, new Position(20, 10), 1.0),  // dans la portée
            (3, new Position(100, 100), 1.0)); // hors portée

        minds[1].Communication.Enqueue(Message(1, "hello", sequence: 1));
        brains(system, minds, tick: 1);

        Assert.Contains(system.LastReceived, r => r.ReceiverId == 2 && r.SenderId == 1);
        Assert.DoesNotContain(system.LastReceived, r => r.ReceiverId == 3);
    }

    private static void brains(CommunicationSystem system, Dictionary<ulong, MindState> minds, ulong tick)
    {
        system.Step(tick, minds); // Step invoque BeginTick pour chaque entité
    }

    [Fact]
    public void Interception_DeliversRegardlessOfTarget()
    {
        // Décision n°8 : le signal est public — toute entité dans la portée reçoit,
        // même si le message ne lui était pas destiné.
        (_, CommunicationSystem system, Dictionary<ulong, MindState> minds) = Build(
            Settings(s => s.TransmissionRange = 30),
            (1, new Position(10, 10), 1.0),
            (2, new Position(15, 10), 1.0), // tiers (intercepteur)
            (3, new Position(12, 10), 1.0)); // destinataire nominal

        minds[1].Communication.Enqueue(Message(1, "private", targetId: 3, tick: 1));
        brains(system, minds, tick: 1);

        Assert.Contains(system.LastReceived, r => r.ReceiverId == 2 && r.MessageId != 0);
        Assert.Contains(system.LastReceived, r => r.ReceiverId == 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    public void SendAndReceiveCosts_FollowDecision9(int payloadLength)
    {
        // Décision n°9 : envoi 0.5 + p×0.1 ; réception 0.2 + p×0.05.
        // Relais désactivé : pas de rebond du message vers l'émetteur.
        string payload = new string('x', payloadLength);
        double expectedSend = 0.5 + (payloadLength * 0.1);
        double expectedReceive = 0.2 + (payloadLength * 0.05);

        (_, CommunicationSystem system, Dictionary<ulong, MindState> minds) = Build(
            Settings(s =>
            {
                s.TransmissionRange = 30;
                s.RelayEnabled = false;
            }),
            (1, new Position(10, 10), 1.0),
            (2, new Position(12, 10), 1.0));

        minds[1].Communication.Enqueue(Message(1, payload));
        brains(system, minds, tick: 1);

        Assert.Equal(Math.Round(100.0 - expectedSend, 6), Math.Round(minds[1].Needs.Energy, 6));
        Assert.Equal(Math.Round(100.0 - expectedReceive, 6), Math.Round(minds[2].Needs.Energy, 6));
    }

    [Fact]
    public void Confidence_IsAdjustedByReceiverTrustTowardsSender()
    {
        (_, CommunicationSystem system, Dictionary<ulong, MindState> minds) = Build(
            Settings(s =>
            {
                s.TransmissionRange = 30;
                s.RelayEnabled = false;
            }),
            (1, new Position(10, 10), 1.0),
            (2, new Position(12, 10), 1.0));

        minds[2].Trust.Interact(1); // première rencontre → confiance initiale 0.5
        minds[1].Communication.Enqueue(Message(1, "x"));
        brains(system, minds, tick: 1);

        MessageReceived received = Assert.Single(system.LastReceived);
        Assert.Equal(Math.Round(1.0 * 0.5, 4), Math.Round(received.Confidence, 4));
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(1.0, false)]
    public void Incomprehension_RateBoundsAreRespected(double rate, bool expectedUnderstood)
    {
        (_, CommunicationSystem system, Dictionary<ulong, MindState> minds) = Build(
            Settings(s =>
            {
                s.TransmissionRange = 30;
                s.IncomprehensionRate = rate;
                s.RelayEnabled = false;
            }),
            (1, new Position(10, 10), 1.0),
            (2, new Position(12, 10), 1.0));

        minds[1].Communication.Enqueue(Message(1, "x"));
        brains(system, minds, tick: 1);

        MessageReceived received = Assert.Single(system.LastReceived);
        Assert.Equal(expectedUnderstood, received.Understood);
    }

    [Fact]
    public void Incomprehension_IsDeterministicForIntermediateRate()
    {
        // La cible d'incompréhension dérive d'un hash stable (receiver, messageId) :
        // deux exécutions identiques produisent les mêmes drapeaux (DETERMINISM.md §3).
        (_, CommunicationSystem systemA, Dictionary<ulong, MindState> mindsA) = Build(
            Settings(s =>
            {
                s.TransmissionRange = 30;
                s.IncomprehensionRate = 0.05;
                s.RelayEnabled = false;
            }),
            (1, new Position(10, 10), 1.0),
            (2, new Position(12, 10), 1.0),
            (3, new Position(20, 15), 1.0),
            (4, new Position(22, 12), 1.0));

        (_, CommunicationSystem systemB, Dictionary<ulong, MindState> mindsB) = Build(
            Settings(s =>
            {
                s.TransmissionRange = 30;
                s.IncomprehensionRate = 0.05;
                s.RelayEnabled = false;
            }),
            (1, new Position(10, 10), 1.0),
            (2, new Position(12, 10), 1.0),
            (3, new Position(20, 15), 1.0),
            (4, new Position(22, 12), 1.0));

        foreach ((Dictionary<ulong, MindState> minds, Message msg) in
                 new[] { (mindsA, Message(2, "alpha", tick: 1, sequence: 1)), (mindsB, Message(2, "alpha", tick: 1, sequence: 1)) })
        {
            minds[2].Communication.Enqueue(msg);
        }

        mindsA[4].Communication.Enqueue(Message(4, "beta", tick: 1, sequence: 1));
        mindsB[4].Communication.Enqueue(Message(4, "beta", tick: 1, sequence: 1));

        systemA.Step(1, mindsA);
        systemB.Step(1, mindsB);

        Assert.Equal(systemA.LastReceived.Count, systemB.LastReceived.Count);
        for (int i = 0; i < systemA.LastReceived.Count; i++)
        {
            Assert.Equal(systemA.LastReceived[i].Understood, systemB.LastReceived[i].Understood);
        }

        Assert.Contains(systemA.LastReceived, r => r.Understood);
    }

    [Fact]
    public void MaxReceivesPerTick_CapsDelivery()
    {
        (_, CommunicationSystem system, Dictionary<ulong, MindState> minds) = Build(
            Settings(s =>
            {
                s.TransmissionRange = 30;
                s.MaxReceivesPerTick = 1;
                s.RelayEnabled = false;
            }),
            (1, new Position(10, 10), 1.0),
            (2, new Position(40, 10), 1.0),
            (3, new Position(12, 12), 1.0)); // entité 2 reçoit l'un des deux

        minds[1].Communication.Enqueue(Message(1, "a", tick: 1, sequence: 1));
        minds[3].Communication.Enqueue(Message(3, "b", tick: 1, sequence: 1));
        brains(system, minds, tick: 1);

        int receivedBy2 = system.LastReceived.Count(r => r.ReceiverId == 2);
        Assert.Equal(1, receivedBy2);
    }

    [Fact]
    public void Relay_DegradesConfidenceAndStopsAtMaxHops()
    {
        // Décision n°10 : × 0.9 par saut (10 %/hop). Les relais préservent
        // l'identifiant de l'émetteur d'origine et se voient dans LastSent avec
        // Hops+1 et Confidence×0.9 ; MaxHops=2 borne la chaîne.
        (_, CommunicationSystem system, Dictionary<ulong, MindState> minds) = Build(
            Settings(s => s.TransmissionRange = 15),
            (1, new Position(10, 10), 1.0),  // A
            (2, new Position(25, 10), 1.0),  // B (relais)
            (3, new Position(40, 10), 1.0),  // C (relais secondaire)
            (4, new Position(55, 10), 1.0)); // D (hors portée, dernier saut)

        minds[1].Communication.Enqueue(Message(1, "chain", tick: 1));
        brains(system, minds, tick: 1);

        // Réception à chaque saut : A→B (hops 0), B→C (hops 1), C→D (hops 2).
        Assert.Single(system.LastReceived, r => r.ReceiverId == 2 && r.Hops == 0);
        Assert.Single(system.LastReceived, r => r.ReceiverId == 3 && r.Hops == 1);
        Assert.Single(system.LastReceived, r => r.ReceiverId == 4 && r.Hops == 2);

        // Décroissance 10 %/hop au niveau du message relayé.
        MessageSent relay1 = Assert.Single(system.LastSent, s => s.SenderId == 2 && s.Hops == 1);
        MessageSent relay2 = Assert.Single(system.LastSent, s => s.SenderId == 3 && s.Hops == 2);
        Assert.Equal(Math.Round(0.9, 4), Math.Round(relay1.Confidence, 4));
        Assert.Equal(Math.Round(0.81, 4), Math.Round(relay2.Confidence, 4));

        // Soulignement public : aucune livraison au-delà de MaxHops.
        Assert.DoesNotContain(system.LastReceived, r => r.Hops >= 3);
    }

    [Fact]
    public void SameInput_ProducesIdenticalActivity()
    {
        (_, CommunicationSystem systemA, Dictionary<ulong, MindState> mindsA) =
            Build(Settings(s => s.TransmissionRange = 30), (1, new Position(10, 10), 1.0), (2, new Position(12, 10), 1.0));
        (_, CommunicationSystem systemB, Dictionary<ulong, MindState> mindsB) =
            Build(Settings(s => s.TransmissionRange = 30), (1, new Position(10, 10), 1.0), (2, new Position(12, 10), 1.0));

        brains(systemA, mindsA, tick: 1);
        brains(systemB, mindsB, tick: 1);

        Assert.Equal(systemA.LastSent.Count, systemB.LastSent.Count);
        Assert.Equal(systemA.LastReceived.Count, systemB.LastReceived.Count);
        for (int i = 0; i < systemA.LastReceived.Count; i++)
        {
            MessageReceived a = systemA.LastReceived[i];
            MessageReceived b = systemB.LastReceived[i];
            Assert.Equal(a.MessageId, b.MessageId);
            Assert.Equal(a.Hops, b.Hops);
            Assert.Equal(a.Confidence, b.Confidence);
            Assert.Equal(a.Understood, b.Understood);
        }
    }

    [Fact]
    public void Validation_RequiresPositiveTransmissionRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CommunicationSystem(new Simulation.Core.World.World(new WorldSize(100, 100)),
                new CommunicationSettings { TransmissionRange = 0 }));
    }
}