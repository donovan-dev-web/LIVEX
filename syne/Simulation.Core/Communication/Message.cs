namespace Simulation.Core.Communication;

/// <summary>
/// Les 7 types de messages (COMMUNICATION_PROTOCOL.md §2, Monographie §3.16) :
/// Information, Request, Response, Announcement, Warning, Trading, Acknowledgement.
/// </summary>
public enum MessageType
{
    Information,
    Request,
    Response,
    Announcement,
    Warning,
    Trading,
    Acknowledgement,
}

/// <summary>
/// Message échangé par pulsation (COMMUNICATION_PROTOCOL.md §3) :
/// <c>MessageId | SenderId | Type | Payload | Confidence | Hops | Timestamp(tick)</c>.
///
/// <list type="bullet">
/// <item><b>Confidence</b> : confiance de transmission du message, dégradée de
///  10 %/hop à chaque relais (décision n°10). La confiance du récepteur envers
///  l'émetteur ajuste la confiance à la réception (COMMUNICATION_PROTOCOL.md §7).</item>
/// <item><b>Hops</b> : nombre de relais déjà effectués (bordures par <c>maxHops</c>).</item>
/// </list>
///
/// Déterminisme : le <c>MessageId</c> est un hash stable (SplitMix64) de
/// (senderId, tick, séquence) — pas de dépendance au PRNG global (DETERMINISM.md §3).
/// </summary>
public sealed record Message
{
    public Message(
        ulong messageId,
        ulong senderId,
        ulong? targetId,
        MessageType type,
        string payload,
        double confidence,
        int hops,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (confidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "La confiance doit être dans [0, 1].");
        }

        if (hops < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hops), "Le nombre de sauts doit être &gt;= 0.");
        }

        MessageId = messageId;
        SenderId = senderId;
        TargetId = targetId;
        Type = type;
        Payload = payload;
        Confidence = confidence;
        Hops = hops;
        Tick = tick;
    }

    public ulong MessageId { get; }

    public ulong SenderId { get; }

    /// <summary>Destinataire nominal (optionnel). La diffusion reste publique : toute entité dans la portée reçoit, indépendamment de la cible (interception, décision n°8).</summary>
    public ulong? TargetId { get; }

    public MessageType Type { get; }

    public string Payload { get; }

    public double Confidence { get; }

    public int Hops { get; }

    /// <summary>Tick d'émission (horodatage du message, COMMUNICATION_PROTOCOL.md §3).</summary>
    public ulong Tick { get; }

    /// <summary>Message relayé : saut <c>hops + 1</c> et confiance dégradée de
    /// <c>hopConfidenceDecay</c> (décision n°10 : <c>confidence *= 0.9^hops</c>).</summary>
    public Message Relayed(double hopConfidenceDecay, ulong tick)
    {
        if (hopConfidenceDecay is <= 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(hopConfidenceDecay), "La décroissance doit être dans (0, 1].");
        }

        return new Message(
            MessageId,
            SenderId,
            TargetId,
            Type,
            Payload,
            Confidence * hopConfidenceDecay,
            Hops + 1,
            tick);
    }

    public override string ToString() => string.Join(string.Empty, Type, "#", MessageId, "@", Hops);
}