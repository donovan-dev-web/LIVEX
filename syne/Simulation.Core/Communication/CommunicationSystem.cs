using System.Globalization;
using Simulation.Core.Cognition;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.World;

namespace Simulation.Core.Communication;

/// <summary>
/// Système de communication par pulsations lumineuses publiques (SYNE-050 → 054,
/// COMMUNICATION_PROTOCOL.md, décisions n°7-10). Exécuté en **une passe par tick**
/// (<c>performance.batchCommunication</c>, PERFORMANCE.md) après le pipeline
/// cognitif, dans un ordre causal strict :
///
/// <list type="number">
/// <item><b>Diffusion</b> : chaque entité (identifiant croissant) émet ses
///  messages sortants (cap <c>maxSendsPerTick</c>) — portée
///  <c>transmissionRange</c> + ligne de vue ; toute entité dans la portée reçoit,
///  quelle que soit la cible (publicité, interception — décision n°8).</item>
/// <item><b>Relais</b> : chaque entité relaye les messages compris (cap partagé
///  avec les envois) tant que <c>hops &lt; maxHops</c>, avec dégradation de
///  confiance <c>× hopConfidenceDecay</c> (10 %/hop — décision n°10).</item>
/// </list>
///
/// Coûts énergétiques (décision n°9) : envoi <c>sendEnergyCost + payload ×
/// sendEnergyPayloadFactor</c>, réception <c>receiveEnergyCost + payload ×
/// receiveEnergyPayloadFactor</c>, appliqués via <c>BodyNeeds.ExertEnergy</c>.
///
/// Déterminisme : aucun tirage du PRNG global ; l'incompréhension (5 %) et les
/// identifiants de message dérivent de hash SplitMix64 stables (DETERMINISM.md §3).
/// </summary>
public sealed class CommunicationSystem
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    private const ulong Mix1 = 0xBF58476D1CE4E5B9UL;
    private const ulong Mix2 = 0x94D049BB133111EBUL;

    private readonly Simulation.Core.World.World _world;
    private readonly CommunicationSettings _settings;
    private readonly List<MessageSent> _lastSent = new();
    private readonly List<MessageReceived> _lastReceived = new();

    public CommunicationSystem(Simulation.Core.World.World world, CommunicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.TransmissionRange <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "La portée de transmission doit être &gt; 0.");
        }

        _world = world;
        _settings = settings;
    }

    /// <summary>Messages émis au tick courant (envois + relais), ordre d'émission.</summary>
    public IReadOnlyList<MessageSent> LastSent => _lastSent;

    /// <summary>Messages reçus au tick courant (y compris interceptions), ordre de réception.</summary>
    public IReadOnlyList<MessageReceived> LastReceived => _lastReceived;

    public CommunicationSettings Settings => _settings;

    /// <summary>Exécute le cycle de communication du tick (diffusion puis relais).</summary>
    public void Step(ulong tick, IReadOnlyDictionary<ulong, MindState> minds)
    {
        ArgumentNullException.ThrowIfNull(minds);
        _lastSent.Clear();
        _lastReceived.Clear();

        List<Entity> ordered = _world.Entities.OrderBy(entity => entity.Id.Value).ToList();
        foreach (Entity entity in ordered)
        {
            if (minds.TryGetValue(entity.Id.Value, out MindState? mind))
            {
                mind.Communication.BeginTick();
            }
        }

        DispatchPass(tick, ordered, minds);
        RelayPass(tick, ordered, minds);
    }

    /// <summary>Crée un message sortant avec identifiant déterministe (hash stable, pas de PRNG).</summary>
    public static Message CreateMessage(ulong senderId, ulong? targetId, MessageType type, string payload, ulong tick, int sequence)
    {
        ulong messageId = SplitMix(senderId ^ (tick * GoldenGamma) ^ ((ulong)(sequence + 1) * Mix1));
        return new Message(messageId, senderId, targetId, type, payload, confidence: 1.0, hops: 0, tick);
    }

    private void DispatchPass(ulong tick, IReadOnlyList<Entity> ordered, IReadOnlyDictionary<ulong, MindState> minds)
    {
        foreach (Entity entity in ordered)
        {
            if (!minds.TryGetValue(entity.Id.Value, out MindState? mind))
            {
                continue;
            }

            IReadOnlyList<Message> outgoing = mind.Communication.Drain(_settings.MaxSendsPerTick);
            foreach (Message message in outgoing)
            {
                DispatchSingle(tick, entity, message, minds);
            }
        }
    }

    private void RelayPass(ulong tick, IReadOnlyList<Entity> ordered, IReadOnlyDictionary<ulong, MindState> minds)
    {
        if (!_settings.RelayEnabled)
        {
            return;
        }

        foreach (Entity entity in ordered)
        {
            if (!minds.TryGetValue(entity.Id.Value, out MindState? mind))
            {
                continue;
            }

            int relayBudget = Math.Max(0, _settings.MaxSendsPerTick - mind.Communication.SentThisTick);
            int relayed = 0;

            foreach ((Message message, bool understood) in mind.Communication.ReceivedThisTick())
            {
                if (relayed >= relayBudget)
                {
                    break;
                }

                if (!CanRelay(entity.Id.Value, message, understood))
                {
                    continue;
                }

                Message relayedMessage = message.Relayed(_settings.HopConfidenceDecay, tick);
                DispatchSingle(tick, entity, relayedMessage, minds);
                mind.Communication.RecordRelayed(message.MessageId);
                relayed++;
            }
        }
    }

    private bool CanRelay(ulong entityId, Message message, bool understood)
    {
        if (!understood)
        {
            return false;
        }

        if (message.SenderId == entityId)
        {
            return false;
        }

        if (message.Hops >= _settings.MaxHops)
        {
            return false;
        }

        return true;
    }

    private void DispatchSingle(ulong tick, Entity sender, Message message, IReadOnlyDictionary<ulong, MindState> minds)
    {
        if (minds.TryGetValue(sender.Id.Value, out MindState? senderMind))
        {
            senderMind.Needs.ExertEnergy(SendCost(message));
            senderMind.Communication.RecordSent();
        }

        var candidates = _world.Grid.QueryCircle(sender.Position, _settings.TransmissionRange, sender.Id.Value)
            .Select(candidate => (Candidate: candidate, Distance: sender.Position.DistanceTo(candidate.Position)))
            .ToList();
        candidates.Sort(static (a, b) =>
        {
            int byDistance = a.Distance.CompareTo(b.Distance);
            return byDistance != 0 ? byDistance : a.Candidate.Id.Value.CompareTo(b.Candidate.Id.Value);
        });

        var deliverable = new List<(Entity Entity, double Distance)>(candidates.Count);
        foreach ((Entity candidate, double distance) in candidates)
        {
            if (LineOfSight.IsClear(sender.Position, candidate.Position, _world.Obstacles))
            {
                deliverable.Add((candidate, distance));
            }
        }

        _lastSent.Add(new MessageSent(
            message.MessageId,
            sender.Id.Value,
            message.TargetId,
            message.Type,
            message.Payload,
            message.Hops,
            message.Confidence));

        foreach ((Entity receiver, _) in deliverable)
        {
            if (!minds.TryGetValue(receiver.Id.Value, out MindState? receiverMind))
            {
                continue;
            }

            if (receiverMind.Communication.RecentIncomingCount >= _settings.MaxReceivesPerTick)
            {
                continue;
            }

            Receive(receiverMind, message, receiver.Id.Value);
        }
    }

    private void Receive(MindState receiverMind, Message message, ulong receiverId)
    {
        receiverMind.Needs.ExertEnergy(ReceiveCost(message));

        // COMMUNICATION_PROTOCOL.md §7.2 : la confiance du message est ajustée par
        // la confiance du récepteur envers l'émetteur (matrice de relations).
        double effective = Math.Clamp(message.Confidence * receiverMind.Trust.TrustWith(message.SenderId), 0.0, 1.0);
        receiverMind.Trust.Interact(message.SenderId);

        bool understood = DeterministicDraw(receiverId, message.MessageId) >= _settings.IncomprehensionRate;
        receiverMind.Communication.RecordReceived(message, understood);

        _lastReceived.Add(new MessageReceived(
            message.MessageId,
            receiverId,
            message.SenderId,
            message.Type,
            message.Hops,
            effective,
            understood));
    }

    private double SendCost(Message message) =>
        _settings.SendEnergyCost + (message.Payload.Length * _settings.SendEnergyPayloadFactor);

    private double ReceiveCost(Message message) =>
        _settings.ReceiveEnergyCost + (message.Payload.Length * _settings.ReceiveEnergyPayloadFactor);

    /// <summary>Tirage déterministe SplitMix64 (doublex) dans [0, 1) — aucun PRNG global.</summary>
    private static double DeterministicDraw(ulong a, ulong b)
    {
        ulong h = SplitMix(a ^ (b * GoldenGamma));
        return (h % 10000) / 10000.0;
    }

    private static ulong SplitMix(ulong z)
    {
        z = (z ^ (z >> 30)) * Mix1;
        z = (z ^ (z >> 27)) * Mix2;
        return z ^ (z >> 31);
    }
}