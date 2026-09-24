# CHANGELOG — SYNE

**Composant** : SYNE
**Statut** : [DRAFT]
**Dernière mise à jour** : 24 septembre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`syne-vX.Y.Z`).

## [Unreleased]

### Added
- **Jalon SYNE ph11c — Cycle des ressources (SYNE-070, jalon U8)** :
  - **`4ᵉ TYPE MINÉRAL`** : `ResourceKind.Mineral` (défaut `resources.mineral.initial = 0`),
    initialisé via `ResourceSettings` (enum-driven), exposé dans l'observabilité (`resources[]`
    — 4 types, API_CONTRACTS §2.1) et persisté (SQLite `resources`/`resource_snapshots`,
    `WriteResources`).
  - **`RÉGÉNÉRATION & DÉGRADATION PÉRIODIQUE`** : `ResourceStocks.ApplyLifecycle(tick, settings)`
    appliquée en **fin de tick** par `SimulationLoop.AdvanceOneTick` (ordre causal strict
    DETERMINISM.md §5, mesurée sous `TickPhase.EventsGroupsPopulation`) — régénération
    `+ regenerationRate` par tick, dégradation à chaque `degradationTick` de
    `− regenerationRate × degradationTick` (clamp ≥ 0 ; inerte sans taux) ; **0 tirage PRNG**.
  - **`VALIDATION`** : bornes `resources.{food,water,wood,mineral}.{initial,regenerationRate,
    degradationTick}` dans `SimulationOptionsValidator` (CONFIGURATION.md §6/6.7).
  - **`DÉTERMINISME & VERSION`** : `engineVersion` **0.6.0 → 0.7.0** ; checksum doré de perception
    et baseline ph10 **inchangés** (l'altération porte sur les réserves, pas sur la cognition du
    scénario de référence) — ré-épinglés pour pin contractuel (DETERMINISM.md §7).
  - Tests : `ResourceStocksTests` (+5 : minéral, régénération bornée, dégradation périodique,
    non-négativité, déterminisme du cycle) + `SimulationOptionsValidatorTests` (+1) +
    `PersistenceTests` (+1 : 4 réserves dont mineral persistées, régénération restaurée) ;
    assertions existantes `CognitionPipelineTests`/`ObservabilitySensorTests` adaptées aux
    bornes de régénération.
  - **Suite totale : 342 tests** (327 Core + 15 Console).
- **Jalon SYNE ph11d — Constructions (SYNE-071, jalon U8)** :
  - **`MODÈLE & CONFIGURATION`** : `world.obstacles` réactivé (défaut `false`,
    CONFIGURATION.md §6.8) — flag vivant + **layout initial** `world.obstacleLayout[]`
    (`StaticObstacleSettings {id, x, y, radius}`) ; carte `Obstacle` (disque) inchangée ;
    `WorldSettings` enrichi (enum-driven), défauts de layout = liste vide.
  - **`MUTATION DYNAMIQUE`** : `World.AddObstacle` validé (position bornée au monde,
    id unique, révision `ObstacleRevision` incrémentée) ; **constructions tracées** :
    `PlaceConstruction` / `RemoveConstruction` enregistrent un `EnvironmentChange`
    (`Added`/`Removed`) consommé par l'observabilité — AddObstacle reste **non tracé**
    (init/restauration de config) ; `ClearEnvironmentChanges()` pour le drain.
    `ApplyConfiguredLayout` → posé au build des mondes Console (CLI + serveur de contrôle).
  - **`GRILLE A* DYNAMIQUE`** : `AStarPathfinder.Refresh()` re-rasterise la grille
    (`_rasterizedRevision` vs `World.ObstacleRevision`) et purge le cache LRU ;
    no-op déterministe quand rien n'a changé ; câblé en tête de
    `MoveTowardDeterministicTarget` (ActionExecutor).
  - **`OBSERVABILITÉ`** : événements d'environnement **`world.construction_placed`** /
    **`world.construction_removed`** (value `{id, x, y, radius}`, `targetId`) drainés par
    `ObservabilityTickEmitter` après les boucles entités ; snapshot embarque
    **`obstacles[]`** (`{id, x, y, radius}`, ordre d'insertion — API_CONTRACTS §2.1) ;
    `engineVersion` **0.7.0 → 0.8.0**. Checksums dorés **inchangés** (scénario de référence
    sans modification d'environnement en cours de run → Refresh no-op ; 0 tirage PRNG).
  - **`VALIDATION`** : `world.obstacleLayout[]` borné (`id` non vide, `radius` > 0, `x`/`y`
    dans le monde) et rejeté si `world.obstacles` est `false`.
  - Tests : `WorldTests` (+8 : bornes, id unique, trace/révision, drain), A*
    (+4 : purge cache, re-routage après pose, no-op, re-liberation après retrait),
    `ConfigLoaderTests` (+2 : défauts + layout JSON), `SimulationOptionsValidatorTests`
    (+3 : layout valide/appel-flag/bornes), `ObservabilitySensorTests` (+3 : obstacles
    snapshot + contrats construction_placed/removed, champ obstacles additif),
    `ObservabilityConstructionTests` (+2, Console : pose/retrait drainés + déterminisme
    non-altéré). Anciennes assertions adaptées (engineVersion 0.8.0).
  - Suite totale : **362 tests** (345 Core + 17 Console).
- **Jalon U8 — Validation moteur T0–T5 (SYNE-122)** :
  - **`MilestoneT0T5Tests`** (8 tests) verrouille les jalons transverses de validation
    (ROADMAP.md §3, ROADMAP §7) :
    - **T0** — 50 entités / 1000 ticks : aucun crash, état valide (ids uniques, positions
      dans le monde, énergie ∈ [0, 100], non-NaN) et **déterminisme à l'échelle**
      (empreinte bit-à-bit des positions identique à seed égale).
    - **T1** — 50 entités / 2000 ticks : les **croyances divergent** (&gt; 1 carte de
      croyances distincte parmi les entités — expériences différentes ⇒ croyances
      différentes, décision n°14).
    - **T2** — traits différents ⇒ décisions différentes en situation **strictement
      identique** : même seed, même monde, même position, mêmes autres traits ; seule la
      curiosité (plage [0,0] vs [2,2]) change — la première divergence de décision est
      **portée par Explore** (modificateur de personnalité 0.5 vs 2.5).
    - **T3** — information locale : deux entités hors de portée ne se connaissent jamais
      (aucune croyance croisée), deux entités proches apprennent mutuellement leur position.
    - **T4** — reproductibilité du benchmark à l'échelle du jalon (débits objectifs =
      `ScaleTargetsTests` + CLI `--benchmark`, PERFORMANCE.md §9).
    - **T5** — la suite dépasse la barre du jalon (&gt; 160 tests, vérifié par réflexion
      sur l'assembly de test ; couverture mesurée **94,01 %**, ≥ 80 %).
  - **Carte purement test** : aucun changement moteur — **engineVersion inchangé (0.8.0)**,
    **0 tirage PRNG ajouté** ; checksums dorés (perception `0x27fad50065d8c4a4`, baseline
    ph10 `0x072a488aa18c05eb`) non rejoués.
  - Suite totale : **370 tests** (353 Core + 17 Console).
- **Jalon U8 — Saisons (SYNE-072)** :
  - **`CONFIGURATION`** : `world.seasons` passe d'un booléen **mort** (jamais consommé) à un
    **objet** actif (CONFIGURATION.md §6.9) — `enabled` (défaut `false`), `seasonLengthTicks`
    (360), `initialSeason` (spring), `cycle[]` = 4 définitions × facteurs par resource
    (`SeasonFactor` Food/Water/Wood/Mineral ; V0.1 : spring ×1, summer eau ×1.2, autumn
    bois ×1.2 + nourriture ×1.1, winter nourriture ×0.8 eau ×0.9 — calibrés en SYNE-120,
    décision n°4).
  - **`MODÈLE`** : `Season` (enum) + statique `Seasons` (`Name`/`TryParse`/`Count`) ;
    saison courante = **fonction pure du tick** `(initialSeasonIndex + tick /
    seasonLengthTicks) mod 4` — **0 tirage PRNG** (DETERMINISM.md §3) ; `SeasonDefinition`
    configurable, `SeasonChange {Previous, Current}` tracé par le monde.
  - **`RÉGÉNÉRATION`** : `ResourceStocks.ApplyLifecycle(tick, settings, seasonFactors?)` —
    les facteurs multiplient régénération et dégradation périodique (SYNE-070) en **fin de
    tick** (fauche causale SYNE-070, clamp ≥ 0), facteurs neutres si saisons désactivées.
  - **`OBSERVABILITÉ`** : événement du monde **`world.season_changed`** (`targetId` = saison
    courante, `value = {previous, current}` — API_CONTRACTS §2.2) drainé par
    `ObservabilityTickEmitter` au **tick exact** du basculement, avant le snapshot ;
    snapshot embarque **`season`**/**`seasonIndex`** (**additifs**, §2.1) ;
    `engineVersion` **0.8.0 → 0.9.0**.
  - **`VALIDATION`** : `seasonLengthTicks` &gt; 0, `initialSeason` valide, `cycle` = les 4
    saisons exactement une fois, facteurs ≥ 0.
  - Tests : `SeasonTests` (+16, Core : marche `At` (cycle/types/longueur 1), facteurs par
    saison/resource, pureté/déterminisme, `ApplyLifecycle` factorisé + dégradation bornée,
    traçage au tick exact, drain, désactivé ⇒ aucun événement, déterminisme bit-à-bit
    (FNV `ResourceLog`), snapshot `season`/`seasonIndex`, sérialiseur, événement typé,
    validateur défauts + 6 branches invalides) + `ObservabilitySeasonTests` (+3, Console :
    émission au tick du basculement, snapshot traversant l'émetteur, désactivé ⇒ silence) ;
    `ConfigLoaderTests` adapté (objet vs booléen) ; anciennes assertions adaptées
    (engineVersion 0.9.0). Checksums dorés **inchangés** (saisons désactivées par défaut
    ⇒ scénario de référence intact ; 0 tirage PRNG).
  - Suite totale : **389 tests** (369 Core + 20 Console).
- **Jalon U8 — Territoires (SYNE-073)** :
  - **`CONFIGURATION`** : `world.territories` = bloc `{enabled, zones[]}` (CONFIGURATION.md
    §6.10) — zones « points de survie » = disques `{id, centerX, centerY, radius}` (défaut 20,
    décision n°21) ; `enabled` défaut `false` ; layout posé à l'init (`ApplyConfiguredLayout`).
  - **`MODÈLE`** : `Territory {Id, Center, Radius}` porté par le monde (ordre de pose stable) ;
    l'appartenance d'une entité à une zone = **function pure des positions** (`distance ≤
    radius`, bord inclus) — **0 tirage PRNG** (DETERMINISM.md §3) ; `TrackTerritoryMembership`
    recalcule en **fin de tick** (même fenêtre causale fermée que saisons/constructions).
  - **`OBSERVABILITÉ`** : bascules tracées `TerritoryMembershipChange {Kind, Zone, EntityId}`
    (par zone telle que posée, identifiant croissant, **sorties avant entrées**), accumulées
    (`LastTerritoryChanges`/`MembersOfTerritory`) et **drainées** en événement du monde
    **`world.territory_membership_changed`** (`agentId` = entité, `targetId` = zone, `value =
    {kind}` — API_CONTRACTS §2.2) ; snapshot embarque **`territories[]`** `{id, x, y, radius,
    memberCount, members[]}` (**additif**, sérialisé seulement si suivi actif, §2.1) ;
    `engineVersion` **0.9.0 → 0.10.0**.
  - **`VALIDATION`** : `zones` rejetée si `world.territories.enabled` est `false` ; par zone :
    `id` non vide et unique, `radius` &gt; 0, `centerX`/`centerY` dans le monde.
  - Tests : `TerritoryTests` (+16, Core : layout/ordre de pose, `AddTerritory` bornes/id, bord
    inclus, Entered puis Left (ordre), accumulation + drain, désactivé ⇒ aucun suivi,
    déterminisme bit-à-bit (FNV `MembershipLog`), snapshot territoires + vide si désactivé,
    sérialiseur JSON, événement typé, validateur défauts + 6 branches invalides) +
    `ObservabilityTerritoryTests` (+2, Console : événements émis + snapshot `territories[]`
    traversant l'émetteur, désactivé ⇒ silence). Checksums dorés **inchangés** (suivi
    désactivé par défaut ⇒ scénario de référence intact ; 0 tirage PRNG).
  - Suite totale : **407 tests** (385 Core + 22 Console).
- **Jalon SYNE ph11 — Persistance & Contrôle (SYNE-110 → SYNE-113, jalon U8)** :
  - **`MODÈLE & REPRISE BIT-À-BIT` (SYNE-110/111/112, PR2 PR SYNE)** : persistance SQLite
    **11 tables** (`PRAGMA user_version=2`) — `SqlitePersistenceStore` : sauvegarde atomique de l'état
    complet (monde, entités, croyances/mémoire/relations, RNG 4×64) sur hooks `autoSaveEveryNTicks`
    (défaut 1000) / `maxBackups` (défaut 5) branchés sur `SimulationLoop.AdvanceOneTick` (rotation
    implémentée) ; reprise exacte post-crash au dernier `tick_states` ; snapshot JSON déterministe
    à côté (`SimulationSnapshotCodec`) ; **`PersistenceTests`** (5).
  - **`SERVEUR DE CONTRÔLE HTTP :5181` (SYNE-113, PR3 PR SYNE)** : contrôle **non intrusif** du
    moteur — `Simulation.Console/Control/` : `ControlServer` (`HttpListener` BCL, zéro dépendance
    ADR-002/003, option CLI `--serve` + `--serve-port` défaut 5181, contrat API_CONTRACTS §3) +
    `SimulationController` (machine à états `idle→running⇋paused→finished`, boucle `RunLoopAsync`
    avec gate `ManualResetEventSlim` + `CancellationToken` par run, **0 tirage PRNG ajouté** ⇒
    trajectoire bit-à-bit identique au run ininterrompu) ; routes `POST /api/control/start {seed?, config?}`
    / `pause` / `resume` / `reset {seed?, runId?}` + `GET /api/control/status` (réponses
    `{ok, action, runId, state, tick, aliveCount, seed}`) ; finalise **ECHOS-085** (interop vérifiée
    contre le vrai `ControlClient` ECHOS) ; **`ControlServerWireTests`** (6).
  - **Suite totale : 335 tests** (320 Core + 15 Console) → **342** (327 Core + 15 Console) après le cycle des ressources (SYNE-070, bloc ph11c ci-dessus).
- **Jalon SYNE ph10 — Tests & Couverture (SYNE-100 → SYNE-102, jalon ph10, U7)** :
  - **`SUITE UNITAIRE & COUVERTURE` (SYNE-100)** : couverture lignes mesurée **96,19 %** (≥ 80 % requis ;
    Coverlet XPlat) — les deux fichiers sous le seuil passent à **100 %** : `SimulationOptionsValidator.cs`
    (278/278) via **`Ph10ValidationCoverageTests`** (27 tests / 35 exécutions — toutes les branches de
    validation : monde, sauvegarde, débits, perception, communication, besoins, croyances, catalogue
    d'actions, merge, mortalité, A*, options nulles) et `ExternalEvent.cs` (314/314) via
    **`ObservabilitySensorTests`** étendu (`message_sent` contrat de livraison, `message_received`
    contrat de réception, `agent_died` {cause, species}, `agent_spawned` parentage,
    `action_completed` réserves).
  - **`TESTS D'INTÉGRATION BOUCLE COMPLÈTE` (SYNE-101)** : **`ObservabilityChainedLoopTests`** —
    150 ticks chaînés observés **sans perte** : 1 snapshot + 1 `tick_summary` par tick, ticks contigus
    1..N (aucun trou ni doublon), toutes les trames JSON valides, 6 sous-systèmes engagés
    (`decision_made`, `action_completed`, `message_sent`/`message_received`…) + au moins un événement
    social/population ; invariant cross-check : les agents `agent_died` n'apparaissent dans aucun
    snapshot post-mortem.
  - **`NON-RÉGRESSION DÉTERMINISME` (SYNE-102)** : **`Ph10DeterminismBaselineTests`** — journal
    d'état **complet** (population, envois, groupes, naissances, décès, puis id/position/énergie/
    besoins/intention/mémoire/confiance par entité) en partie canonique — bit-à-bit identique pour
    seeds {12345, 7, 999}, divergent pour seed différent ; **nouvelle baseline épinglée
    `0x072a488aa18c05eb`** (25 ent., 200 ticks, seed 12345) documentée DETERMINISM.md §7. Golden de
    perception **`0x27fad50065d8c4a4` inchangé**, `engineVersion` reste **0.6.0** (aucune altération
    de trajectoire — tests seuls).
  - Tests : +45 (279 → **324** : **315 Core** + **9 Console**). Seuils annexe J.1 largement dépassés
    (160+ requis).
- **Jalon SYNE ph9 — Performance & Scalabilité (SYNE-090 → SYNE-093, jalon ph9, U7)** :
  - **`BUDGETS PAR TICK` (SYNE-090)** : `TickBudgetCollector` (opt-in, `SimulationLoop.Budgets`) — mesure
    du temps par sous-système (perception, mémoire/croyances, besoins/objectifs, décision/utilité,
    actions/mouvement, communication, événements/groupe/population — PERFORMANCE.md §3) + total tick ;
    part de computation = Σ phases / tick **≥ 30 %** (PERFORMANCE.md §9). Aucun coût sur le chemin
    nominal (collecteur nul par défaut), **aucun tirage PRNG** — checksum doré inchangé
    **0x27fad50065d8c4a4** même sous instrumentation.
  - **`Cibles 50/500/1000` (SYNE-091)** : mode CLI **`--benchmark`** (`--benchmark-ticks`,
    `--benchmark-populations`) — 50/500/1000 entités × seeds {12345, 999, 7} × 300 ticks :
    **≥ 2720 / ≥ 1187 / ≥ 505 t/s** mesurés (cibles 30/20/10 largement dépassées, machine de
    référence PERFORMANCE.md §9) + table des budgets par sous-système ; tests CI `ScaleTargetsTests`
    (planchers anti-régression × ~7, convention PERFORMANCE.md §9).
  - **`GRILLE SPATIALE + POOLING` (SYNE-092)** : `ObjectPool<T>` (Rent/Return, vidage au retour,
    compteurs) appliqué au buffer de tri des candidats de perception (`PerceptionSystem.SortBuffers`) —
    réduction de la charge GC sans toucher la trajectoire ; grille spatiale déjà en production
    (`SpatialGrid`, perception O(fenêtre 3×3), SYNE-012).
  - **`DÉTERMINISME PERFORMANCE` (SYNE-093)** : checksum d'état FNV-1a (id;x;y;énergie) reproductible
    bit-à-bit à seed égale — tests d'échelle (500 entités) + `TickBudgetTests` (collecte n'altère pas
    la trajectoire, golden épinglé) + checksums affichés par `--benchmark`.
  - Tests : +12 (261 → **273** Core ; total **279** avec 6 Console). Aucune altération contractuelle —
    `engineVersion` reste **0.6.0**, checksum doré **0x27fad50065d8c4a4**.
- **Jalon SYNE ph6 — Groupes & Naissance (SYNE-060 → SYNE-063, issues #29–#32, milestone ph6)** :
  - **`GROUPES` (élément de réseau social, SYNE-060/061)** : `GroupSystem` (propriété `Cognition.Groups`).
    Cohésion = min trust réciproque × affinité (1 + `sharedBeliefBonus` + `goalAlignmentBonus`),
    lien = confiance ≥ `trustThreshold` **et** affinité > 1 (décisions n°23/24) ; composantes
    union-find (racine = id min), révision LOD déterministe (`reviewIntervalTicks` = 10),
    cycle de vie par correspondance exacte des membres (turnover ⇒ dissolution + refonte),
    leader émergent = somme de confiance entrante max (tie-break id min), décisions collectives
    pondérées par la confiance au leader (quorum `consensusThreshold` = 0.5).
  - **`NAISSANCE` (fusion consentie, SYNE-062)** : `BirthSystem` — passe `reproduction.intervalTicks`
    = 100, consentement = min trust réciproque ≥ `consentTrustThreshold` 0.6 (décision n°17),
    première paire qualifiante en ordre d'id (mère = moindre), enfant au point médian clampé
    (id = max+1), `MindState.Born` → mémoire + buts + `Born` ; `NewbornMinds` fusionnées dans le
    pipeline **après** la révision des groupes (ordre causal).
  - **`HERITAGE` (mécanismes fins, SYNE-063)** : `FuseTraits(a, b, settings, seed)` — parent
    exprimant sous dominance ∈ [0, 1], mutation déterministe `SplitMix64(newTraitId, seed, tick)`
    (+ clamps [0, 2]) ; `InheritMemory(double? salienceThreshold = null)` → défaut
    `agents.inheritance.salienceThreshold` (0.01).
  - **`Observabilité (additif, contrat 0.5.0 → engineVersion 0.5.0)`** : événements
    `group_formed`/`group_dissolved`/`group_decision` (bilan de vie `lifetime`/`success`,
    turnover `membersOut`/`membersIn`) et `agent_spawned` ({childId, motherId, fatherId, species, x, y});
    snapshot `groups[]` (`WorldSnapshot.GroupSnapshot`, camelCase).
  - **`Calibration`** : `communication.transmissionRange` défaut 20 → **55** (bornes [1, 70]
    indépendantes de la perception) — le scénario défaut forme un tapis de confiance.
  - Tests : +32 (210 → **242**), dont `GroupSystemTests` (9), `BirthSystemTests` (7),
    `GroupBirthDeterminismTests` (3), mécanismes fins d'héritage (7), validations de config (3),
    observabilité (3). Document `SOCIAL_NETWORK.md` créé ; checksum doré re-épinglé **0x864e72f57e1fe0d0**.
- **Jalon SYNE ph7b — Fidélités V0.1 (SYNE-074 → SYNE-077, jalon ph7b, U6, engineVersion 0.6.0)** :
  - **`MORTALITÉ` (SYNE-074)** : `DeathSystem` — entité à énergie ≤ seuil fatal (défaut 0, `life.deathEnabled`)
    meurt ; corps retiré du monde (`World.RemoveEntity`, `SpatialGrid.Remove`), esprit purgé de la
    cognition et des groupes (`GroupSystem.PurgeDeceased`) **après** boucles entités, communication et
    naissances (ordre causal, DETERMINISM §5) ; événement `agent_died` ({cause, species}).
  - **`NAISSANCE CONSENTIE FIDÈLE` (SYNE-075)** : `BirthSystem.Qualifies` — distance ≤ `mergeRange` 40,
    ligne de vue claire (`LineOfSight.IsClear`), énergie ≥ `mergeMinimumEnergy` 30 chacune, aucun besoin
    critique (seuil `CriticalEnergy`) ; 4 tests fidélité (trop loin / LOS masquée / énergie épuisée /
    état critique) + test pipeline (gates neutralisés, naissance observée).
  - **`DÉCISION COLLECTIVE → OBJECTIFS` (SYNE-076)** : `GroupObjective {groupId, kind, consensus,
    leaderTrust, adoptedTick, expiresTick}` propagé à chaque révision (`GroupSystem.PropagateObjectives`,
    TTL = `reviewIntervalTicks`, confiance au leader, leader auto-aligné à 1.0) ; bonus d'alignement
    `CollectiveAlignBonus` 1.2 appliqué dans `UtilityEvaluator.Evaluate`/`ApplyActionSwitchMargin`
    (alignement = consensus × confiance au leader).
  - **`CHEMINEMENT A* DÉTERMINISTE` (SYNE-077)** : `AStarPathfinder` — grille rasterisée (`PathfindingSettings`,
    `CellSize` 10, disques + marge demi-cellule → cellules bloquées), voisinage ordonné, tie-break
    (f, g, x, y), heuristique octile, expansion plafonnée (`MaxExpansionCells` 4096), repli « sur place » ;
    `PathCache` LRU (`CacheCapacity` 256) ; intégré dans `ActionExecutor` quand le pas direct est bloqué ;
    aucune consommation PRNG. Tests : `AStarPathfinderTests` (8) + `ActionExecutorTests` (+2).
  - `engineVersion` → **0.6.0** ; checksum doré ré-épinglé (inchangé) **0x27fad50065d8c4a4** ;
    tests : **261 Core + 6 Console**, couverture ≥ 80 %.
- Documentation technique V0.1 complète du composant (VISION, ARCHITECTURE, DATA_MODEL, SIMULATION_LOOP, COGNITIVE_ARCHITECTURE, SYSTEMS_SPEC, COMMUNICATION_PROTOCOL, PERSISTENCE, DETERMINISM, CONFIGURATION, API_CONTRACTS, PERFORMANCE, TESTING, ROADMAP).
- Formalisation des ADR-001, ADR-002, ADR-005 à ADR-011 (Annexe F de la Monographie).
- **Socle U0 (SYNE-1)** : solution `Syne.sln`, bibliothèque `Simulation.Core` (configuration Annexe H, loader JSON générique, validation, flags CLI), `Simulation.Console` (conf résolue + sonde PRNG), tests xUnit (28). PRNG déterministe **xoshiro256\*\*** + **splitmix64** (vecteurs épinglés), `global.json` SDK 10.0.400. ADR-012 (config JSON + CLI).
- **Noyau U0 (SYNE-2)** : boucle minimale (1 tick = 1 min simulée, `maxTicks` respecté, tête/queue affichées), monde continu 500×500 non-toroidal (positions clampées), **grille spatiale uniforme** (requêtes par rayon déterministes, cellule configurable), entités typées (identité séquentielle, espèce, position, traits **8 traits [0, 2]** hérités d'un **paramétrage** plages de traits), fabrique déterministe épinglée (référence indépendante), tests xUnit (62).
- **Jalon SYNE ph1 — BDI + Perception (SYNE-010 à SYNE-015, issues #7–#12)** :
  - **Perception (SYNE-011/012)** : rayon par défaut **50** (décision n°6), confiance `1 − (d/r)×0.3` clampée [0.7, 1.0], perception **étagée** (`id % 4`), **ligne de vue** obstacle cercle (ADR-013), obstacles observés (id FNV-1a), requête `QueryCircle` bornée (fenêtre 3×3) avec **micro-benchmark CI** (< 10 ms/requête).
  - **Mémoire (SYNE-013)** : salience exponentielle (decay 0.01/0.005/0.002), seuil d'oubli 0.01, capacité 1000 avec **éviction du moins saillant**, rappel ordonné (StoredAt puis Sequence).
  - **Croyances (SYNE-014)** : faits `(subject, predicate, value)`, révision (alignement +0.2 / moyenne / **conflit** −0.1 avec création), plafond par snap, expiration (plafond 0.4) et décroissance `timeDecayPerTick`.
  - **BDI + utilité (SYNE-010)** : pipeline 10/15 étapes branché dans la boucle (perception → mémoire → croyances → besoins → désirs → délibération → intention → action), utilité `U = (benefit − cost − risk) × confidence × personalityModifier + urgency`, mouvements déterministes sans consommation PRNG.
  - **Déterminisme (SYNE-015)** : `DeterminismRegressionTests` — hash FNV-1a **épinglé** de la trajectoire perception+décision, égalité bit-à-bit entre 2 runs identiques, divergence entre seeds.
  - Tests : +60 (62 → **122**). ADR-013 (ligne de vue en V1).

### Added
- **Observabilité (SYNE-080, issue #37, milestone ph8)** : émetteur WebSocket **BCL minimal**
  (HttpListener + `AcceptWebSocketAsync`, zéro dépendance) dans `Simulation.Console` activé par
  `--observe` (`--observe-port`, défaut 5180, bind `127.0.0.1`). Contrat API_CONTRACTS §2 :
  **1 snapshot/tick** (version, runId `run-<seed>`, tick, simulatedTimeMinutes, aliveCount,
  agents[{id, species, position{x,y}, energy, hunger, thirst, fatigue, currentAction}], resources[])
  + **1 `tick_summary`/tick** + **1 `decision_made`/entité/tick** ({intention, utility}) en **JSON
  camelCase déterministe**. Aucun tirage PRNG ajouté (déterminisme préservé). Diffusion à tous les
  consommateurs connectés. Tests : `ObservabilitySensorTests` (Core, format/épinglage camelCase) +
  **`Simulation.Console.Tests`** (tests de fil WebSocket réels, 2). Suite : **129 tests**.
- **Jalon SYNE ph2 — Mémoire intergénérationnelle + Croyances + Confiance (SYNE-020 → SYNE-022, issues #13/#14/#15, milestone ph2)** :
  - **Confiance inter-entités (SYNE-021)** : `Relationships` (Interact +bonus, ObserveDeception −sanction, Tick décroissance ×`TrustDecayFactorPerTick`, défaut **0.9** — COMMUNICATION_PROTOCOL §3), confiance initiale 0.5, bonus de vérité 0.05, sanction de mensonge 0.2 ; `MindState.Trust` intégré au pipeline (Tick décroissance à chaque step).
  - **Mémoire intergénérationnelle (SYNE-020)** : `Inheritance.FuseTraits` (moyenne), `InheritMemory` (union, seuil de salience, ré-horodatage `birthTick`), `InheritBeliefs` (union, confiance max sur fait identique, source « héritage », expiration restampée) ; naissance par fusion consentie `MindState.Born(options, parentA, parentB, birthTick)` (décision n°16, COGNITIVE_ARCHITECTURE §6.6).
  - **Éviction mémoire (SYNE-022)** : `Memory.AllEntries` ; stress test — capacité 1000 **jamais dépassée** (catégories mixtes, éviction du moins saillant).
  - **Observabilité étendue (additif, contrat V0.1 inchangé)** : `AgentSnapshot.From(entity, mind, currentTick)` émet `traits`, `beliefs` (±confiance), `goals` (kind/age), `trust` (peerId/level), `memoryCount` (camelCase) — consommé par les 7 moteurs ECHOS au jalon U2 ECHOS.
  - Tests : +17 (129 → **146**). Build Release 0 warning / 0 erreur.

### Added
- **Jalon SYNE ph4 — Actions (SYNE-040 → SYNE-043, issues #20/#21/#22/#23, milestone ph4, engineVersion 0.3.0)** :
  - **Sous-système d'actions déclaratif (SYNE-040)** : nouveau `Simulation.Core/Actions/` — `ActionCatalog` (définitions issues de `agents.actions.catalog`, ordre stable de l'enum, viabilité contre les réserves), `ActionExecutor` (**une action atomique par entité par tick**, itération par identifiant croissant, aucun tirage PRNG), `ActionResult`/`ActionOutcome` (Executed/Blocked + deltas d'effets). Les effets (coûts énergie, récupérations, consommation de réserve) proviennent du catalogue déclaratif (CONFIGURATION §6.2).
  - **Déplacement + obstacles (SYNE-041)** : cible pseudo-aléatoire déterministe par (id, tick, désir) via finaliseur SplitMix64 (reproductible, 0 PRNG), **pas borné par la vitesse**, clamp aux limites du monde, **jamais de pas dans un obstacle** (rejet → sur place) ; coût par défaut = `actions.moveEnergyCost` pour les actions de déplacement.
  - **Besoins déclenchés ≥ 50 + réserves (SYNE-042)** : seuils par défaut **50/50/70** (`needs.hungerTriggerThreshold`/`thirstTriggerThreshold`/`fatigueTriggerThreshold`, décision n°4) ; nouvelles actions terminales **Eat/Drink** (`DesireKind.Eat = 7`, `Drink = 8`, append) résolues depuis SeekFood/SeekWater quand la réserve est disponible ; **réserves globales** `ResourceStocks` (`Food` 100 / `Water` 1000 / `Wood` 50, régénération décision n°4), consommées par Eat/Drink et exposées `SimulationLoop.Resources` ; « instruire » reporté (jalon ph7, ROADMAP).
  - **Déclencheur d'interruption centralisé (SYNE-043)** : `InterruptionTrigger` — unique point « action en cours interrompue ? » (faim critique > 85 → Eat si réserve, sinon SeekFood ; énergie < 10 → Rest), évalué à tout tick hors délibération, utilité + marge `utilityExcessMargin`, aucune consommation PRNG.
  - **Observabilité (additif, contrat V0.1 compatible)** : événement **`action_completed`** (1/entité/tick — action, `outcome`, deltas, réserve consommée) ; snapshot ajoute **`resources`** peuplées (type/quantity, DATA_MODEL §8) ; `engineVersion` → **0.3.0**.
  - **Config** : `SimulationOptionsValidator` chemins corrigés (`agents.actions.deliberation.*`/`interruption.*`, défaut ph3) + validation `needs.*TriggerThreshold` et catalogue complet.
  - **Déterminisme** : checksum de trajectoire **recalculé** (0xe8d69e462fc22df7 → **0xdfbc9a6c4a1d8122**, DETERMINISM.md §6) ; contrat « 0 tirage PRNG » préservé.
  - Tests : +30 (166 → **197**, dont 3 `Simulation.Console.Tests`). Build Release 0 warning / 0 erreur.

### Added
- **Jalon SYNE ph5 — Communication (SYNE-050 → SYNE-054, issues #24/#25/#26/#27/#28, milestone ph5, engineVersion 0.4.0)** :
  - **Sous-système de communication (SYNE-050)** : nouveau `Simulation.Core/Communication/` — `Message` (enum `MessageType` des 7 types, id `ulong` **SplitMix64 déterministe** — 0 PRNG global, `Relayed()` applique `hops+1` et `confidence × hopConfidenceDecay`), `CommunicationState` (file sortante bornée, file entrante bornée, `SentThisTick` partagé envois+relais, ensemble `RelaySeen` anti-boucle), `CommunicationSystem.Step` (**une passe par tick** : *diffusion* par identifiant croissant puis *relais*, `PERFORMANCE.md` — `batchCommunication`). Pulsation « Information » émise par toute entité sociable (`sociability ≥ 0.5`) percevant une entité vivante (`perceived-{id}#{x},{y}`) — partage public.
  - **Publicité + interception (SYNE-051)** : toute entité dans la portée (`transmissionRange`, **20 u. héritée — décision n°7**) et en **ligne de vue** (`LineOfSight.IsClear`) reçoit, **quelle que soit la cible** (décision n°8) ; réception capée `maxReceivesPerTick` (une réception écartée ne coûte rien).
  - **Coûts hérités configurables (SYNE-052)** : décision n°9 — envoi **0.5 + p×0.1**, réception **0.2 + p×0.05** (`sendEnergyCost`/`sendEnergyPayloadFactor`/`receiveEnergyCost`/`receiveEnergyPayloadFactor`), appliqués via `BodyNeeds.ExertEnergy`.
  - **Protocole de confiance (SYNE-053)** : décision n°10 — relais **× 0.9/hop** (`hopConfidenceDecay`), borné `maxHops` (défaut 2, câble public), `SenderId` d'origine préservé ; confiance effective = confiance du message × confiance du récepteur envers l'émetteur (`Relationships.TrustWith`, inconnue 0.0) + marquage `Interact` ; incompréhension (5 %) par tirage déterministe `SplitMix64(receiverId, messageId)`.
  - **Observabilité (additif, contrat V0.1 compatible)** : événements **`message_sent`**/**`message_received`** (`EventSensor`, `ObservabilityContract` 0.4.0) diffusés par `ObservabilityTickEmitter` après les boucles entités ; `engineVersion` → **0.4.0**.
  - **Config** : clés `communication.*` (transmissionRange, relayEnabled, maxHops, coûts, facteurs, hopConfidenceDecay) + validation (`SimulationOptionsValidator`, CONFIGURATION §6.3).
  - **Déterminisme** : checksum de trajectoire **recalculé** (0xdfbc9a6c4a1d8122 → **0x6aa2b2d87b32a8a5**, DETERMINISM.md §6) ; contrat « 0 tirage PRNG » préservé (id + incompréhension via SplitMix64 stable).
  - Tests : +13 (197 → **210**, dont 3 `Simulation.Console.Tests` ; `CommunicationSystemTests` : portée/LOS, interception, coûts n°9, confiance récepteur, caps, relais × 0.9/hop + maxHops, déterminisme). Build Release 0 warning / 0 erreur.

### Added
- **Jalon SYNE ph3 — Décision + Utilité (SYNE-030 → SYNE-033, issues #16/#17/#18/#19, milestone ph3, engineVersion 0.2.0)** :
  - **Formule d'utilité complète (SYNE-030)** : `U = (benefit − cost − risk) × confidence × personalityModifier + urgency` ; **bonus d'alignement ×1.2** (`deliberation.alignBonus`) quand l'action rejoint l'objectif courant ; seuils critiques **configurables** faim > 85 / énergie < 10 (`interruption.criticalHunger`/`criticalEnergy`, COGNITIVE_ARCHITECTURE §6) ; **hystérésis anti-oscillation** `actionSwitchMargin` (défaut 0.05, `ApplyActionSwitchMargin`) — ne changer d'action que si elle surpasse l'action courante de la marge.
  - **Sélecteur d'action + fréquence de délibération (SYNE-031)** : sélection déterministe par utilité maximale ; **fréquence configurable** (`deliberation.intervalTicks`, défaut 10 — décision n°14, LOD « 1 tick tous les 10 ») avec **holdover** de l'intention entre deux délibérations, décalée par entité (lissage de charge) ; **trace `DecisionRecord`** complète (tick, entité, scores par action, délibéré/interrompu — COGNITIVE_ARCHITECTURE §7).
  - **Interruptions d'actions (SYNE-032)** : `TryInterrupt` — besoin critique dont l'utilité surpasse de > `utilityExcessMargin` (10) l'action en cours reprend la main, y compris entre deux délibérations (décision n°15) ; décision interrompue tracée.
  - **Conflits de priorités (SYNE-033)** : `PriorityConflictResolver` — candidats à moins de `conflictTieMargin` (0.5) du maximum → **résolution probabiliste `p = drive × confidence / Σ`**, tirage **SplitMix64 déterministe sans PRNG** (décision n°22, aucun arbitraire d'ancienneté ; à force nulle, ordre du catalogue stable).
  - **Observabilité (additif, contrat V0.1 compatible)** : `decision_made` ajoute `deliberated`/`interrupted` (bool) par tick ; snapshot ajoute `engineVersion` (déterminsime : `0.2.0`, épinglé).
  - **Déterminisme** : checksum de trajectoire **recalculé** (0xab56603aedd578af → 0xe8d69e462fc22df7, DETERMINISM.md §6) — délibération à fréquence + interruptions altèrent volontairement la trajectoire ; contrat « 0 tirage PRNG » préservé.
  - Tests : +20 (146 → **166**, dont 2 `Simulation.Console.Tests`). Build Release 0 warning / 0 erreur.

### Changed
- ARCHITECTURE.md : §4 (couche applicative réelle, Dockerfile reporté) et §6 (PRNG défini) mis à jour.
- (SYNE-2) ARCHITECTURE.md : §4 précise la couche implémentée (mondes, entités, grille, boucle).
- (SYNE ph1) DATA_MODEL, COGNITIVE_ARCHITECTURE, SYSTEMS_SPEC : rayon défaut 50, ligne de vue V1 (ADR-013), obstacles cercle V0.1.
- (SYNE ph1) CONFIGURATION : clés perception/mémoire/croyances/actions/dérives de besoins alignées sur l'implémentation.
- (SYNE ph1) DETERMINISM : contrat « pipeline = 0 tirage PRNG », tests de régression.
- (SYNE ph1) PERFORMANCE : méthodologie micro-benchmark grille (SYNE-012).
- (SYNE ph1) TESTING : périmètre 122 tests, commandes par filtre.

### Deprecated
- (aucun)

## [0.0.0] — à venir

Version initiale (prototype V1/V2 de la Monographie référencé comme [HÉRITÉ]).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | Documentation V0.1 |
| 21 septembre 2026 | Socle U0 : solution, config, PRNG, ADR-012 | SYNE-001 / SYNE-005 / SYNE-006 |
| 21 septembre 2026 | Noyau U0 : boucle, monde + grille, entités + traits | SYNE-002 / SYNE-003 / SYNE-004 |
| 21 septembre 2026 | Jalon SYNE ph1 : BDI + Perception | SYNE-010 → SYNE-015 |
| 21 septembre 2026 | Jalon SYNE ph2 : Mémoire intergénérationnelle + Croyances + Confiance | SYNE-020 → SYNE-022 |
| 21 septembre 2026 | Jalon SYNE ph3 : Décision + Utilité (engineVersion 0.2.0) | SYNE-030 → SYNE-033 |
| 22 septembre 2026 | Jalon SYNE ph4 : Actions déclaratives + réserves (engineVersion 0.3.0) | SYNE-040 → SYNE-043 |