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
    /// fidèle, décision collective → objectifs des membres, cheminement A* déterministe ;
    /// jalon SYNE ph7c → 0.7.0 : cycle des ressources — minéraux + régénération/
    /// dégradation périodique appliquées en fin de tick (SYNE-070) — l'altération porte
    /// sur les réserves du monde, pas sur la cognition du scénario de référence (checksum
    /// doré ré-épinglé inchangé, pin contractuel) ;
    /// jalon SYNE U8 → 0.8.0 : constructions = obstacles statiques configurables,
    /// pose/retrait tracés (world.construction_placed/_removed) et réémis dans le
    /// snapshot (SYNE-071) — aucun obstacle du scénario de référence n'est modifié,
    /// checksums dorés ré-épinglés inchangés (pin contractuel ph7b) ;
    /// jalon SYNE U8 → 0.9.0 : cycle de saisons (SYNE-072) — <c>world.seasons</c>
    /// (l'ancien drapeau booléen homonyme, mort, devient un bloc actif), événement
    /// world.season_changed et champ snapshot season/seasonIndex (additif) ;
    /// désactivé par défaut ⇒ trajectoire du scénario de référence inchangée,
    /// checksums dorés ré-épinglés inchangés (pin contractuel) ;
    /// jalon SYNE U8 → 0.10.0 : territoires (SYNE-073, décision n°21) — <c>world.territories</c>
    /// (zones « point de survie », appartenance suivie par la présence des entités),
    /// événement world.territory_membership_changed et champ snapshot territories[]
    /// (additif) ; désactivé par défaut ⇒ trajectoire du scénario de référence
    /// inchangée, checksums dorés ré-épinglés inchangés (pin contractuel).
    /// Émise dans chaque snapshot.
    /// </summary>
    public const string EngineVersion = "0.10.0";

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

    /// <summary>Construction posée (obstacle statique, modification d'environnement) — SYNE-071, API_CONTRACTS.md §2.2.</summary>
    public const string ConstructionPlaced = "world.construction_placed";

    /// <summary>Construction retirée (modification d'environnement) — SYNE-071, API_CONTRACTS.md §2.2.</summary>
    public const string ConstructionRemoved = "world.construction_removed";

    /// <summary>
    /// Changement de saison du cycle environnemental (SYNE-072, API_CONTRACTS.md §2.2) :
    /// émis au tick exact du basculement quand <c>world.seasons.enabled</c> (0 tirage
    /// PRNG — la saison est une fonction pure du tick). Charge utile {previous, current}.
    /// </summary>
    public const string SeasonChanged = "world.season_changed";

    /// <summary>
    /// Changement d'appartenance à une zone de territoire (SYNE-073, décision n°21,
    /// API_CONTRACTS.md §2.2) : une entité est entrée dans ou sortie du disque d'un
    /// « point de survie » quand <c>world.territories.enabled</c> (0 tirage PRNG —
    /// l'appartenance est une fonction pure des positions). Charge utile {kind}.
    /// </summary>
    public const string TerritoryMembershipChanged = "world.territory_membership_changed";

    public static string RunIdFor(ulong seed) => $"run-{seed}";
}