# DETERMINISM.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `PERSISTENCE.md`, `CONFIGURATION.md`
**Source Monographie** : §2.3.4, §3.6.2–3.6.4 (seed, PRNG, sérialisation), ADR-006, §7.5 (validation)

---

## 1. Définition

**Déterminisme bit-à-bit** : deux exécutions identiques (même seed, même config, même version moteur, même état initial) produisent **exactement** la même trajectoire — mêmes événements, mêmes décisions, mêmes états.

## 2. Les 4 piliers d'une exécution reproductible

| Pillier | Contenu | Source |
| :-- | :-- | :-- |
| **Seed** | Entier 64 bits initialisant le PRNG | §3.6.2 |
| **Configuration** | `config.json` : paramètres du monde | §3.6.2 |
| **Version moteur** | `engineVersion` — garantit des règles identiques | §3.6.2 |
| **État initial** | positions, ressources, entités | §3.6.2 |

## 3. Le PRNG : xoshiro256\*\*

- **Algorithme** : xoshiro256\*\* (`prng_engine = xoshiro256**`), rapide (l'un des plus rapides en 64 bits), qualité éprouvée (BigCrush de TestU01), état compact (4 × 64 bits = 256 bits).
- **Initialisation** : **splitmix64(seed)** — dérive une séquence reproductible à partir de la seed.
- **Règle stricte** : `System.Random` est **interdit** — pas stable entre versions .NET, état non directement sérialisable.
- **Reproductibilité bit-à-bit** garantie si seed identique.

(ADR-006, §3.6.3)

## 4. Sérialisation de l'état RNG

- L'état complet du PRNG (**4 × ulong**) est sérialisé dans `tick_states.rng_state`.
- Une simulation sauvegardée au tick 1000, rechargée puis poursuivie, produit exactement les mêmes événements qu'une exécution ininterrompue (§3.6.4).

## 5. Ordre causal strict

- Les sous-systèmes sont exécutés dans un **ordre causal strict** (boucle 15 étapes, `SIMULATION_LOOP.md`).
- Aucune exécution parallèle ne doit introduire de non-déterminisme : la parallélisation du prototype (`parallelPerception`, etc.) doit rester déterministe (agrégation d'ordre fixe).

**Jalon U8 — Livres (engineVersion 0.11.0)** : les écritures/lectures explicites ne consomment aucun tirage PRNG. Elles sont déterministes, enregistrées dans l’ordre d’appel et persistées avec le monde ; l’émission d’événements et du snapshot est additive. `world.books.enabled = false` par défaut, donc le run de référence est inchangé. La lecture n’altère pas encore la cognition (effet différé au futur moteur mémoire (hors U8)).

## 6. Vérification (tests)

| Test | Objectif |
| :-- | :-- |
| `BitIdenticalPersistenceTest` | Reprise bit-à-bit après sauvegarde/charge |
| Checksum de run | Hash de trajectoire pour détecter toute divergence |
| Matrices seeds × configs (Annexe I.4) | Pour chaque seed ∈ [12345, 67890, 99999, 42, 999], exécuter 1000 ticks, valider checksum |
| `DeterminismRegressionTests` (SYNE-015) | Hash **épinglé** de la trajectoire perception+décision (seed 12345, 25 ent., 200 ticks) + égalité bit-à-bit entre deux runs identiques et différence entre seeds |
| Auto-égalité (SYNE-015) | Deux exécutions (même seed/config) → chaîne de perception identique |

**Contrat V0.1 (jalon SYNE ph1)** : le pipeline cognitif (perception → décision) ne consomme **aucun** tirage du PRNG — l'avance du générateur reste **1 tirage/tick** ; les cibles de déplacement dérivent d'un déterminisme propre (hash SplitMix64 stable, sans passerelle RNG).

**Jalon SYNE ph3 (engineVersion 0.2.0)** : la délibération à fréquence configurable (décision n°14), les interruptions par besoin critique (décision n°15), la sélection avec hystérésis et le tirage de conflit de priorités (décision n°22) restent **0 tirage PRNG** — le tirage probabiliste de `PriorityConflictResolver` dérive d'un hash SplitMix64 de (entityId, tick, kinds). Checksum de la trajectoire recalculé (0xab56603aedd578af → **0xe8d69e462fc22df7**).

**Jalon SYNE ph4 (engineVersion 0.3.0)** : le catalogue d'actions déclaratif, l'exécuteur atomique et le déclencheur d'interruption centralisé restent **0 tirage PRNG** — la cible de déplacement dérive du hash SplitMix64 de (id, tick, désir) (ActionExecutor.DeterministicOffset), l'itération reste par identifiant croissant, le pas est clampé au monde et **jamais posé dans un obstacle** (rejet → sur place, SYNE-041). Checksum de la trajectoire recalculé (0xe8d69e462fc22df7 → **0xdfbc9a6c4a1d8122**).

**Jalon SYNE ph5 (engineVersion 0.4.0)** : le sous-système de communication reste **0 tirage PRNG** — les identifiants de message dérivent du hash SplitMix64 de (émetteur, tick, séquence) et l'incompréhension (5 %) de `SplitMix64(receiverId, messageId)` (CommunicationSystem, COMMUNICATION_PROTOCOL.md §3/4) ; la passe par tick (diffusion par identifiant croissant puis relais, borné à `maxHops`) préserve l'ordre causal. Checksum de la trajectoire recalculé (0xdfbc9a6c4a1d8122 → **0x6aa2b2d87b32a8a5**).

**Jalon SYNE ph6 (engineVersion 0.5.0)** : le réseau social reste **0 tirage PRNG** — les liens de cohésion découlent de l'ordre trié des paires d'entités (id croissant), les composantes d'un union-find à racine minimale, le leader du max de confiance entrante (tie-break id min), la mutation d'héritage de `SplitMix64(nouvelTraitId, seed, tick)` (Inheritance, DATA_MODEL §6.6.3) ; la révision des groupes s'intercale déterministement **après** les décréments croyances/confiance et **avant** le merge des naissances ; `transmissionRange` recalibré 20 → **55** (au défaut ph5, le scénario de référence n'échangeait aucune pulsation → trajectoire « silencieuse » ; la calibration ph6 produit des événements de groupe/naissance dans le run de référence). Checksum de la trajectoire recalculé (0x6aa2b2d87b32a8a5 → **0x864e72f57e1fe0d0**).

**Jalon SYNE ph7b (engineVersion 0.6.0)** : les 4 fidélités restent **0 tirage PRNG** — la mortalité itère par identifiant croissant après les boucles entités/communication/naissances (SYNE-074) ; la naissance consentie évalue les paires qualifiantes dans l'ordre d'id (SYNE-075) ; l'alignement sur objectif collectif est un produit déterministe consensus × confiance au leader (SYNE-076) ; le cheminement A* est **sans PRNG** : grille rasterisée, voisinage ordonné, départage (f, g, x, y), expansion plafonnée, repli « sur place » (SYNE-077), cache LRU à accès déterministe. Checksum de la trajectoire **inchangé** (0x27fad50065d8c4a4 — le scénario de référence ne déclenche aucun pas bloqué), ré-épinglé pour pin contractuel.

**Jalon SYNE ph7c (engineVersion 0.7.0)** : le **cycle des ressources** (SYNE-070) reste **0 tirage PRNG** — appliqué en **fin de tick** (ordre causal strict : entités → communication → groupes → naissances → mortalité → cycle ressources), il régénère de `regenerationRate` à chaque tick et se dégrade à chaque période `degradationTick` d'un montant `regenerationRate × degradationTick` (clamp ≥ 0) ; opérations purement additivo-subtractives, aucune passerelle PRNG. Quatre types désormais (Food, Water, Wood, **Mineral**). L'altération porte sur les **réserves du monde**, pas sur la cognition du scénario de référence : checksum doré de perception **inchangé** (0x27fad50065d8c4a4) et baseline ph10 **inchangée** (0x072a488aa18c05eb) — ré-épinglés identiques pour pin contractuel, `engineVersion` incrémenté 0.6.0 → 0.7.0.

**Jalon SYNE ph11d (engineVersion 0.8.0)** : les **constructions (SYNE-071)** restent **0 tirage PRNG** — `AddObstacle`/`PlaceConstruction`/`RemoveConstruction` sont purement déclaratifs (validations, révision ulong, journal `EnvironmentChange`) et **jamais branchés sur le RNG** ; la grille A* se re-rasterise via `AStarPathfinder.Refresh()` **en mode paresseux** : comparant `_rasterizedRevision` à `World.ObstacleRevision`, c'est un **no-op sans changement** — sur le scénario de référence (layout posé avant le ctor du pathfinder, aucune modification d'environnement en cours de run), aucun pas A* n'est rejoué et la purge du cache LRU n'altère pas la trajectoire des replis (aucun pas bloqué). Ordre causal : la pose/le retrait interviennent **hors boucle** (API de contrôle/consommateurs) et sont **réémis et vidés** par `ObservabilityTickEmitter` avant le snapshot du tick suivant (jamais intercalés dans les boucles entités). Checksum doré de perception **inchangé** (0x27fad50065d8c4a4) et baseline ph10 **inchangée** (0x072a488aa18c05eb) — ré-épinglés identiques pour pin contractuel, `engineVersion` incrémenté 0.7.0 → 0.8.0.
**Jalon U8 — Saisons (engineVersion 0.9.0)** : le **cycle de Saisons (SYNE-072)** reste **0 tirage PRNG** — la saison courante est une **fonction pure du tick** `(initialSeasonIndex + tick / seasonLengthTicks) mod 4` (aucune passerelle RNG) ; les facteurs saisonniers (`SeasonFactors`) multiplient la régénération et la dégradation périodique du cycle SYNE-070 en **fin de tick** (même fenêtre causale, opérations additivo-subtractives sur les réserves du monde, jamais sur la cognition). Le basculement est tracé par `_seasonChanges` et **drainé** par `ObservabilityTickEmitter` en `world.season_changed` **au tick exact** du changement, avant le snapshot. Saisons **désactivées par défaut** (`world.seasons.enabled = false`) : facteurs neutres ∗1, aucune modification de la trajectoire → checksum doré de perception **inchangé** (0x27fad50065d8c4a4) et baseline ph10 **inchangée** (0x072a488aa18c05eb) — ré-épinglés identiques pour pin contractuel, `engineVersion` incrémenté 0.8.0 → 0.9.0.

**Jalon U8 — Territoires (engineVersion 0.10.0)** : le **suivi de territoire (SYNE-073)** reste **0 tirage PRNG** — l'appartenance à une zone (`Territory`, disque autour d'un point de survie, décision n°21) est une **function pure des positions** (`distance ≤ radius`), **recalculée en fin de tick** par `SimulationLoop.TrackTerritoryMembership` (même fenêtre causale fermée que saisons/constructions, jamais intercalée dans les boucles entités) ; **aucun comportement agentique, aucune revendication, aucune passerelle RNG**. Ordre d'émission déterministe : par zone **telle que posée** (ordre de `world.territories.zones[]`), identifiant d'entité **croissant**, **sorties (Left) avant entrées (Entered)**. Les bascules sont tracées par `LastTerritoryChanges` (accumulées, vidables via `ClearTerritoryChanges`) et **drainées** par `ObservabilityTickEmitter` en `world.territory_membership_changed` — suivi **désactivé par défaut** (`world.territories.enabled = false`) : aucune altération de la trajectoire du scénario de référence → checksum doré de perception **inchangé** (0x27fad50065d8c4a4) et baseline ph10 **inchangée** (0x072a488aa18c05eb) — ré-épinglés identiques pour pin contractuel, `engineVersion` incrémenté 0.9.0 → 0.10.0. Activé, l'altération se borne à des champs **additifs** du snapshot (`territories[]`) et des événements typés, jamais à la cognition ni aux réserves.

**Jalon SYNE ph10 (engineVersion 0.7.0, inchangé)**: tests de non-régression de déterminisme (SYNE-102) **sans modification du moteur** — le golden de perception reste **0x27fad50065d8c4a4** et `engineVersion` reste 0.7.0. Une **nouvelle baseline d'état complet** est épinglée en complément (`Ph10DeterminismBaselineTests.FullPipeline_StateBaseline_IsPinned`) : journal canonique par tick (population, envois, groupes, naissances, décès, puis id/position/énergie/besoins/intention/mémoire/confiance de chaque entité) — scénario identique (25 entités, 200 ticks, seed 12345) → checksum FNV-1a **0x072a488aa18c05eb**. Toute altération bit-à-bit de la trajectoire change ce checksum ET le golden ; recalcul + bump MINOR requis (§7).

## 7. Impacts & contractuels

- Toute modification qui altère la trajectoire à seed identique impose :
  - incrément `MINOR`/`MAJOR` (cf. `../../VERSIONING.md`) ;
  - mise à jour de `engineVersion` (0.10.0 au jalon U8 — Saisons + Territoires ; 0.8.0 au jalon SYNE ph11d ; émise dans chaque snapshot, `ObservabilityContract.EngineVersion`).
- Les benchmarks (Annexe I) vérifient le déterminisme via checksum.

---

## Points restés ouverts dans ce document
- Aucun — le déterminisme est un contrat ferme et vérifié. Les implémentations parallèles devront maintenir l'ordre causal (détail à valider au moment du code).