namespace Simulation.Core.Cognition;

/// <summary>Cycle de vie d'un engagement (ADR « Engagements Communicationnels », D5).</summary>
public enum CommitmentStatus
{
    /// <summary>En attente d'honoration (créé à la réception d'une Response positive).</summary>
    Pending = 0,

    /// <summary>Honoré — TrustLevel += CommitmentBonus chez le demandeur.</summary>
    Fulfilled = 1,

    /// <summary>Rompu volontairement — TrustLevel -= CommitmentPenalty chez le demandeur.</summary>
    Broken = 2,

    /// <summary>Expiré sans honororation — traité comme rompu (ADR : « non honoré au-delà → Broken »).</summary>
    Expired = 3,
}

/// <summary>
/// Engagement communicationnel (ADR « Engagements Communicationnels », D5,
/// engineVersion 0.14.0) : créé chez le <b>demandeur</b> quand il reçoit une
/// <c>Response</c> positive d'un pair à sa <c>Request</c>. Version minimale de
/// l'ADR : un seul type d'engagement (aide à la survie), pas de propagation
/// sociale de la rupture (visibilité privée A↔B uniquement).
///
/// <para>
/// Un engagement actif ajoute un objectif candidat <see cref="DesireKind.Socialize"/>
/// (aide au demandeur) pondéré par la confiance envers lui ; l'honororation
/// (<see cref="CommitmentStatus.Fulfilled"/>) et la rupture
/// (<see cref="CommitmentStatus.Broken"/>) impactent <c>TrustLevel</c> via
/// <c>agents.trust.commitmentBonus/commitmentPenalty</c> — une promesse rompue
/// n'est plus sans effet, et n'est pas confondue avec un mensonge factuel.
/// </para>
/// </summary>
public sealed record Commitment(
    ulong ToEntityId,
    string RequestType,
    ulong CreatedTick,
    ulong ExpiryTick)
{
    /// <summary>Statut courant (mutable via résolution de cycle).</summary>
    public CommitmentStatus Status { get; private set; } = CommitmentStatus.Pending;

    /// <summary>Tick de résolution (résolution ou expiration), 0 si encore actif.</summary>
    public ulong ResolvedTick { get; private set; }

    /// <summary>Encore actif (Pending) et non expiré au tick courant.</summary>
    public bool IsActive(ulong currentTick) => Status == CommitmentStatus.Pending && currentTick < ExpiryTick;

    /// <summary>Honore l'engagement (idempotent — seul un engagement actif peut être résolu).</summary>
    public bool Fulfill(ulong tick)
    {
        if (Status != CommitmentStatus.Pending)
        {
            return false;
        }

        Status = CommitmentStatus.Fulfilled;
        ResolvedTick = tick;
        return true;
    }

    /// <summary>Rompt l'engagement (abandon volontaire — idempotent).</summary>
    public bool Break(ulong tick)
    {
        if (Status != CommitmentStatus.Pending)
        {
            return false;
        }

        Status = CommitmentStatus.Broken;
        ResolvedTick = tick;
        return true;
    }

    /// <summary>Expire l'engagement (non honoré au-delà de expiryTick — idempotent).</summary>
    public bool Expire(ulong tick)
    {
        if (Status != CommitmentStatus.Pending)
        {
            return false;
        }

        Status = CommitmentStatus.Expired;
        ResolvedTick = tick;
        return true;
    }

    /// <summary>Restauration d'état (persistance bit-à-bit, PERSISTENCE.md §4).</summary>
    internal static Commitment Restore(
        ulong toEntityId,
        string requestType,
        ulong createdTick,
        ulong expiryTick,
        CommitmentStatus status,
        ulong resolvedTick)
        => new(toEntityId, requestType, createdTick, expiryTick)
        {
            Status = status,
            ResolvedTick = resolvedTick,
        };
}
