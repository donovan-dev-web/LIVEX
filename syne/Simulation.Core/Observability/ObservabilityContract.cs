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
    /// bit-à-bit (jalon SYNE ph4 → 0.3.0 : catalogue d'actions, Eat/Drink, seuils de
    /// déclenchement 50, interruption centralisée). Emise dans chaque snapshot.
    /// </summary>
    public const string EngineVersion = "0.3.0";

    public const string SnapshotType = "snapshot";
    public const string EventType = "event";

    /// <summary>Types d'événements typés émis (nomenclature V0.1, API_CONTRACTS.md §2.2).</summary>
    public const string DecisionMade = "decision_made";
    public const string TickSummary = "tick_summary";

    /// <summary>Exécution d'action terminée (SYNE-040, API_CONTRACTS.md §2.2).</summary>
    public const string ActionCompleted = "action_completed";

    public static string RunIdFor(ulong seed) => $"run-{seed}";
}