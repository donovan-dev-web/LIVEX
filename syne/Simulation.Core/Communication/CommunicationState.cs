namespace Simulation.Core.Communication;

/// <summary>
/// Activité de communication d'un tick — alimente l'observabilité
/// (<c>message_sent</c> / <c>message_received</c>, API_CONTRACTS.md §2.2)
/// et les traces pour ECHOS (heatmap de communication, NetworkCentrality...).
/// </summary>
public sealed record MessageSent(
    ulong MessageId,
    ulong SenderId,
    ulong? TargetId,
    MessageType Type,
    string Payload,
    int Hops,
    double Confidence);

/// <summary>Réception d'un message (y compris interception — toute entité dans la portée reçoit).</summary>
public sealed record MessageReceived(
    ulong MessageId,
    ulong ReceiverId,
    ulong SenderId,
    MessageType Type,
    int Hops,
    double Confidence,
    bool Understood);

/// <summary>
/// État de communication d'une entité (COMMUNICATION_PROTOCOL.md §7) : file
/// sortante (bornée par <c>maxSendsPerTick</c>), file entrante (bornée par
/// <c>maxReceivesPerTick</c>) et ensemble "déjà relayé" anti-boucle.
///
/// V0.1 : la file entrante est consommée comme trace d'activité du tick (la
/// révision des croyances par messages arrive avec l'analyse/les besoins
/// sociaux) — la réception ajuste la confiance par la matrice de relations et
/// marque l'interaction (COMMUNICATION_PROTOCOL.md §7).
/// </summary>
public sealed class CommunicationState
{
    private readonly Queue<Message> _outgoing = new();
    private readonly List<Message> _recentIncoming = new();
    private readonly HashSet<ulong> _relayedMessageIds = new();
    private readonly Dictionary<ulong, bool> _understoodMap = new();

    /// <summary>Messages en attente d'émission au prochain cycle (borné par <c>maxSendsPerTick</c>).</summary>
    public int OutgoingCount => _outgoing.Count;

    /// <summary>Messages reçus au tick courant (borné par <c>maxReceivesPerTick</c>).</summary>
    public int RecentIncomingCount => _recentIncoming.Count;

    /// <summary>Messages émis au tick courant (envois + relais), partage le cap <c>maxSendsPerTick</c>.</summary>
    public int SentThisTick { get; private set; }

    /// <summary>Vrai si le message <paramref name="messageId"/> a déjà été relayé (anti-boucle).</summary>
    public bool HasRelayed(ulong messageId) => _relayedMessageIds.Contains(messageId);

    /// <summary>Enregistre une émission (envoi ou relais) au tick courant.</summary>
    public void RecordSent() => SentThisTick++;

    public void Enqueue(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _outgoing.Enqueue(message);
    }

    /// <summary>Défile jusqu'à <paramref name="maxSends"/> messages sortants (cap par tick).</summary>
    public IReadOnlyList<Message> Drain(int maxSends)
    {
        if (maxSends < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSends), "maxSends doit être &gt;= 0.");
        }

        var result = new List<Message>(Math.Min(_outgoing.Count, maxSends));
        while (result.Count < maxSends && _outgoing.Count > 0)
        {
            result.Add(_outgoing.Dequeue());
        }

        return result;
    }

    public void RecordReceived(Message message, bool understood)
    {
        ArgumentNullException.ThrowIfNull(message);
        _recentIncoming.Add(message);
        _understoodMap[message.MessageId] = understood;
    }

    /// <summary>Messages reçus au tick courant, en ordre de diffusion (sender croissant puis distance), avec le drapeau d'incompréhension.</summary>
    public IReadOnlyList<(Message Message, bool Understood)> ReceivedThisTick()
    {
        var result = new List<(Message, bool)>(_recentIncoming.Count);
        foreach (Message message in _recentIncoming)
        {
            result.Add((message, _understoodMap.TryGetValue(message.MessageId, out bool understood) && understood));
        }

        return result;
    }

    public void RecordRelayed(ulong messageId)
    {
        _relayedMessageIds.Add(messageId);
        TrimRelaySet();
    }

    /// <summary>Remise à zéro des buffers du tick courant (appelé en tête de cycle).</summary>
    public void BeginTick()
    {
        _recentIncoming.Clear();
        _understoodMap.Clear();
        SentThisTick = 0;
    }

    private const int RelaySetTarget = 128;

    private void TrimRelaySet()
    {
        if (_relayedMessageIds.Count <= RelaySetTarget)
        {
            return;
        }

        ulong[] sorted = _relayedMessageIds.ToArray();
        System.Array.Sort(sorted);
        _relayedMessageIds.RemoveWhere(_ => true);
        foreach (ulong keep in sorted.AsSpan(RelaySetTarget / 2))
        {
            _relayedMessageIds.Add(keep);
        }
    }
}