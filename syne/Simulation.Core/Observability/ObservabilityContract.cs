namespace Simulation.Core.Observability;

/// <summary>
/// Contrat d'observabilité (SYNE-080, API_CONTRACTS.md §2) : messages JSON
/// camelCase sur WebSocket, versionnés. Chaque message est un objet avec un
/// champ <c>type</c> ("snapshot" ou "event").
/// </summary>
public static class ObservabilityContract
{
    /// <summary>
    /// Version du contrat d'observabilité (API_CONTRACTS.md §2). 0.3.0 → 0.4.0 :
    /// champs additifs de l'échelle temporelle (ADR-017) — ``simulatedTimeSeconds``
    /// dans le snapshot, ``simulatedSecondsPerTick``/``metersPerUnit`` dans la
    /// description ``world_initialized`` (version de description 1.0 → 1.1) et
    /// dans ``/api/control/status``. Rétrocompatible : les champs existants
    /// (dont ``simulatedTimeMinutes``, plancher entier) sont inchangés.
    /// </summary>
    public const string Version = "0.4.0";

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
    /// inchangée, checksums dorés ré-épinglés inchangés (pin contractuel) ;
    /// jalon SYNE U8 → 0.11.0 : livres (SYNE-121, décisions n°18/19, Monographie
    /// §3.18) — <c>world.books</c> (connaissances matérialisées), événements
    /// world.book_written / world.book_read, champ snapshot books[] (additif) ;
    /// désactivé par défaut ⇒ trajectoire du scénario de référence inchangée,
    /// checksums dorés ré-épinglés inchangés (pin contractuel).
    /// jalon review/refactor → 0.12.0 : **altération volontaire de trajectoire** —
    /// activation du garde anti-relay de <c>CommunicationSystem.CanRelay</c> (le
    /// suivi « déjà relayé » était écrit mais jamais consulté : une même entité
    /// pouvait relayer indéfiniment un message reçu par des canaux distincts).
    /// Scénario de référence : 0x27fad50065d8c4a4 → 0xdb57f58566418f5d, cause isolée
    /// et vérifiée (voir DeterminismRegressionTests.GoldenChecksum_IsPinned) ; bump
    /// MINOR appliqué conformément à DETERMINISM.md §7. Les autres correctifs de ce
    /// jalon (atomicité des réserves, file de messages bornée, grille rectangulaire,
    /// IDs de naissance, persistance des territoires) sont mesurés neutres sur ce
    /// scénario. Champ snapshot territories[] (additif) et SchemaVersion 3 → 4.
    /// Émise dans chaque snapshot.
    /// jalon calibration D1 → 0.13.0 : **altération volontaire de trajectoire** —
    /// rééquilibrage de l'arbitrage utilitaire (bénéfice Eat/Drink déplafonné :
    /// min(need, 100) × 0.6, monotone jusqu'à 60) et bilan énergétique compensé
    /// (catalog eat.energyRecovery 2.0, drink 1.0) portés par le profil de
    /// référence (SimulationProfiles.Reference + configs/simulation/reference.json).
    /// Sans cela : mort lente (énergie moyenne 69 → 49 entre t800 et t1200,
    /// 2 extinctions sur 3) et Eat/Drink écrasés par Socialize/Explore faim
    /// saturée (utilité moyenne 13,9 vs 83,7). ADR de calibration en référence
    /// (docs/docs-syne/adr/), checksums dorés et fixtures analysis_golden.json
    /// re-calés dans le même commit (procédure DETERMINISM.md §7).
    /// jalon ADR cognitifs → 0.14.0 : implémentation des ADR acceptés du 30/09/2026,
    /// tous portés par des drapeaux désactivés par défaut (trajectoire du scénario
    /// de référence et checksums dorés inchangés — pin contractuel conservé) :
    /// D7 primitives d'actions (Take/Give/Trade/Attack/Defend, ajoutés EN QUEUE de
    /// DesireKind), D8 inventaire (capacité poids par entité), D3 bibliothèque de
    /// plans (candidats Take/Trade par objectif de besoin), D5 engagements
    /// communicationnels (Commitment + TrustLevel) et D2 contrôle de saillance
    /// (étape 3bis, reconsidération déclenchée + filet de sécurité). Contrat
    /// d'observabilité 0.2.1 → 0.3.0 (champs snapshot additifs émis sous drapeaux,
    /// rétro-compatibles à la lecture).
    /// jalon calibration B1 → 0.15.0 : **altération volontaire de trajectoire** —
    /// les défauts intégrés deviennent le profil calibré (ADR-016) : coût de
    /// déplacement 0,5 → 0,03, gains Eat/Drink 0/0 → 2,0/1,0, Rest 0,5/1 → 1,5/2,
    /// dérives social/curiosité 0,001/0,002 → 0,0002/0,0005 (un besoin sans
    /// satisfaction finissait par monopoliser Socialize/Explore), réserves
    /// nourriture/eau 100/1 000 → 20 000/20 000 avec régénération nette 20/10
    /// (dégradation de la nourriture neutralisée) et coûts de communication
    /// neutres (relais coupé, 1 envoi/1 réception). Objectif : 0 extinction et
    /// énergie stable sur 2500 ticks à 50 et 100 agents. Checksums dorés
    /// re-calés dans le même commit (DETERMINISM.md §7).
    /// </summary>
    public const string EngineVersion = "0.15.0";

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

    /// <summary>
    /// Livre écrit (SYNE-121, décisions n°18/19, Monographie §3.18.5) : une
    /// connaissance matérialisée à l'instant T — l'auteur en paie le coût
    /// (énergie <c>world.books.writeCostEnergy</c> ; la durée n'est pas modélisée en V0.1 ; aucun tirage
    /// PRNG — l'écriture est une API de la boucle). Charge utile {id, authorId,
    /// title, writtenTick, cost}.
    /// </summary>
    public const string BookWritten = "world.book_written";

    /// <summary>
    /// Livre lu (SYNE-121, décision n°19, Monographie §3.18.6) : le bénéfice de
    /// lecture est **posé en principe** (cognition, confiance, savoir) — le
    /// chiffrage appliqué au moteur de mémoire relève de SYNE-131 (décision n°11).
    /// V0.1 : trace le lecteur dans agentId, 0 tirage PRNG. Charge utile
    /// {id, readBenefit}.
    /// </summary>
    public const string BookRead = "world.book_read";
    public const string WorldDelta = "world_delta";

    public static string RunIdFor(ulong seed) => $"run-{seed}";
}