namespace Simulation.Core.Observability;

/// <summary>
/// Contrat d'observabilité (SYNE-080, API_CONTRACTS.md §2) : messages JSON
/// camelCase sur WebSocket, versionnés. Chaque message est un objet avec un
/// champ <c>type</c> ("snapshot" ou "event").
/// </summary>
public static class ObservabilityContract
{
    /// <summary>Version du contrat d'observabilité (API_CONTRACTS.md §2).</summary>
    public const string Version = "0.1.0";

    /// <summary>
    /// Version du moteur (DETERMINISM.md §3.6.2, VERSIONING.md §3) : identifie les
    /// règles de simulation ; incrémentée MINOR à toute altération de la trajectoire
    /// bit-à-bit (jalon SYNE ph6 → 0.5.0 : groupes émergents, décisions collectives,
    /// naissance par fusion ; jalon SYNE ph7b → 0.6.0 : mortalité, naissance consentie
    /// fidèle, décision collective → objectifs des membres, cheminement A* déterministe).
    /// Émise dans chaque snapshot.
    /// </summary>
    public const string EngineVersion = "0.6.0";

    public const string SnapshotType = "snapshot";
    public const string EventType = "event";

    /// <summary>Types d'événements typés émis (nomenclature V0.1, API_CONTRACTS.md §2.2).</summary>
    public const string DecisionMade = "decision_made";
    public const string TickSummary = "tick_summary";

    /// <summary>Exécution d'action terminée (SYNE-040, API_CONTRACTS.md §2.2).</summary>
    public const string ActionCompleted = "action_completed";

    /// <summary>Pulsation émise (envoi ou relais) — SYNE-050, API_CONTRACTS.md §2.2.</summary>
    public const string MessageSent = "message_sent";

    /// <summary>Pulsation reçue (y compris interception) — SYNE-051, API_CONTRACTS.md §2.2.</summary>
    public const string MessageReceived = "message_received";

    /// <summary>Formation d'un groupe émergent — SYNE-060, API_CONTRACTS.md §2.2.</summary>
    public const string GroupFormed = "group_formed";

    /// <summary>Dissolution d'un groupe (bilan + turnover) — SYNE-060, API_CONTRACTS.md §2.2.</summary>
    public const string GroupDissolved = "group_dissolved";

    /// <summary>Décision collective adoptée par consenssus — SYNE-061, API_CONTRACTS.md §2.2.</summary>
    public const string GroupDecision = "group_decision";

    /// <summary>Naissance par fusion consentie — SYNE-062, API_CONTRACTS.md §2.2.</summary>
    public const string AgentSpawned = "agent_spawned";

    /// <summary>Mort par épuisement — SYNE-074, API_CONTRACTS.md §2.2.</summary>
    public const string AgentDied = "agent_died";

    public static string RunIdFor(ulong seed) => $"run-{seed}";
}