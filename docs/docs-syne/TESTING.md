# TESTING.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 23 septembre 2026
**Dépend de** : `DETERMINISM.md`, `ARCHITECTURE.md`
**Source Monographie** : §7.1 (xUnit + Moq), Annexe J (jalons de validation, 160+ tests), Annexe I (benchmarks)

---

## 1. Objectif

Garantir — par des tests automatisés — la **correction**, le **déterminisme** et la **performance** de SYNE. Jalon : **160+ tests** (Annexe J.1) et **couverture ≥ 80 %** (Annexe I.3). État V0.1 : **362 tests** (345 Core + 17 Console ; baseline U0 62 → +60 au jalon SYNE ph1 → +5 observabilité SYNE-080 → +2 tests de fil WebSocket → +17 au jalon SYNE ph2 → +20 au jalon SYNE ph3 → +30 au jalon SYNE ph4 → +13 au jalon SYNE ph5, dont 3 `Simulation.Console.Tests` → +32 au jalon SYNE ph6 : 9 `GroupSystemTests`, 7 `BirthSystemTests`, 7 mécanismes fins d'héritage, 3 validations de configuration, 3 observabilité, 3 `GroupBirthDeterminismTests` → **+19** au jalon SYNE ph7b : 4 `BirthSystemTests` fidélités (SYNE-075), 3 propagation + 1 bonus `GroupObjective` (SYNE-076), 8 `AStarPathfinderTests` + 2 `ActionExecutorTests` (SYNE-077), 1 observabilité preuve SYNE-081/082 ; +1 `test_health.py` ECHOS — registre `/api/runs/{id}/decisions` → **+12** au jalon SYNE ph9 : 5 `ObjectPoolTests`, 3 `TickBudgetTests` (part computation ≥ 30 % + non-altération trajectoire + golden), 4 `ScaleTargetsTests` (planchers 50/500/1000 + checksum à l'échelle) → **+45** au jalon SYNE ph10 : 35 `Ph10ValidationCoverageTests` (toutes les branches du `SimulationOptionsValidator`), 4 `ObservabilitySensorTests` (`message_sent/received`, `agent_died`, `agent_spawned`, `action_completed` réserves), 3 `Ph10DeterminismBaselineTests` (SYNE-102, baseline d'état épinglée), 3 `ObservabilityChainedLoopTests` (SYNE-101, boucle complète sans perte — Console) → **+5** au jalon U8 persistance : 5 `PersistenceTests` (SYNE-110/111/112 — reprise bit-à-bit après SQLite, état RNG 4×64, reprise post-crash, schéma 11 tables, rotation `maxBackups`) → **+6** au jalon U8 contrôle : 6 `ControlServerWireTests` (SYNE-113 — start/status, pause gèle le tick, resume reprend, reset, **contrôle non intrusif** = run piloté bit-à-bit identique au run ininterrompu, 404 JSON) → **+7** au jalon U8 cycle des ressources : 5 `ResourceStocksTests` (minéral 4ᵉ réserve, régénération bornée, dégradation périodique, non-négativité, déterminisme du cycle), 1 `SimulationOptionsValidatorTests` (bornes `resources.*`), 1 `PersistenceTests` (4 réserves dont mineral persistées + régénération restaurée) ; assertions existantes `CognitionPipelineTests`/`ObservabilitySensorTests` adaptées aux bornes de régénération → **+20** au jalon U8 constructions : 8 `WorldTests` (bornes + id unique de `AddObstacle`, trace/révision `PlaceConstruction`/`RemoveConstruction`, drain `ClearEnvironmentChanges`, layout appliqué), 4 `AStarPathfinderTests` (purge cache, re-routage après pose, no-op, route rétablie après retrait), 2 `ConfigLoaderTests` (défauts layout + chargement JSON), 3 `SimulationOptionsValidatorTests` (layout valide / sans flag / bornes), 3 `ObservabilitySensorTests` (snapshot `obstacles[]`, contrats construction_placed/removed, champ additif), 2 `ObservabilityConstructionTests` Console (pose/retrait drainés + déterminisme non altéré) ; épingles `engineVersion` passées à 0.8.0. **Couverture lignes mesurée 96,19 %** (≥ 80 % requis par SYNE-100) — `SimulationOptionsValidator.cs` 100 %, `ExternalEvent.cs` 100 %. Checksum doré ré-épinglé `0x27fad50065d8c4a4` (engineVersion 0.8.0), inchangé au ph9/ph10/U8 (instrumentation/pooling/persistance/tests/contrôle + cycle ressources + constructions seuls, 0 tirage PRNG et Refresh no-op sur le scénario de référence) ; baseline d'état complète ph10 `0x072a488aa18c05eb`. **Suite totale : 362 tests** (345 Core + 17 Console).

## 2. Stack de tests (Monographie §7.1)

- **xUnit** — framework de test.
- **Moq** — isolation des dépendances.
- **XPlat Code Coverage** (Coverlet) — mesurer la couverture.
- CI : exécuté dans `ci.yml` GitHub Actions.

## 3. Périmètre des tests par système

| Système | Tests ciblés |
| :-- | :-- |
| Perception | rayon, plage de confiance, grille spatiale, perception étagée, **ligne de vue obstacle (ADR-013)**, ordre distance/id, obstacles observés |
| Pipeline BDI | boucle croyance→désir→intention à chaque tick, rotation, déterminisme inter-runs, blocage mouvement |
| Mémoire | décroissance exponentielle, purge au seuil 0.01, capacité 1000, éviction épinglée |
| Croyances | révision (alignement/conflit/sources différentes), expiration, plafond par snap |
| Besoins & Objectifs | seuils, filtrage de faisabilité, priorisation |
| Décision / Utilité (SYNE ph3) | formule complète, **bonus d'alignement ×1.2**, hystérésis (`actionSwitchMargin`), interruptions par besoin critique, **fréquence de délibération configurable**, **conflits de priorités force × confiance**, **DecisionRecord** |
| Actions (SYNE ph4) | catalogue déclaratif complet + échec déclaratif, exécution atomique (Eat/Drink/rest/mouvement), déplacement déterministe sans obstacle, **réserves globales** (consommation, clamp, copie), **déclencheur d'interruption centralisé** (faim→Eat/SeekFood, énergie→Rest, marge, cas nominal), seuils de besoins **≥ 50**, terminal Eat/Drink en pipeline, événement `action_completed` (API_CONTRACTS §2.2) |
| Communications (SYNE ph5) | portée + **ligne de vue**, **interception publique** (décision n°8), **coûts hérités configurables** (0.5+p×0.1 / 0.2+p×0.05, décision n°9), **confiance ajustée par le récepteur**, caps `maxSends`/`maxReceives`, **relais × 0.9/hop** + borne `maxHops` + anti-boucle, **déterminisme** (id SplitMix64, incompréhension, égalité inter-runs) — `CommunicationSystemTests` |
| Observabilité (SYNE-080) | format camelCase des messages (snapshot/event), épinglage et déterminisme d'émission, contrat `decision_made` + `action_completed` (avec réserves) + `message_sent`/`message_received` (contrats de livraison/réception) + `agent_died` ({cause, species}) + `agent_spawned` (parentage), `resources` peuplées (4 types dep. SYNE-070), `obstacles[]` (dep. SYNE-071), **engineVersion** ; **tests de fil WebSocket réels** (`Simulation.Console.Tests`) |
| Groupes (SYNE ph6) | cohésion confiance × affinité (lien = confiance **et** part commune), composantes union-find, **cycle de vie par correspondance exacte des membres** (turnover ⇒ dissolution [+refonte]), taille minimale, leader par confiance entrante (tie-break id), décisions pondérées + quorum, formation/dissolution/décision événements + snapshot `groups[]` — `GroupSystemTests` |
| Naissance & Héritage (SYNE ph6) | fusion consentie (min confiance réciproque ≥ seuil, paire d'id minimal, enfant médian clampé), allocation d'id croissante, naissances fusionnées post-boucle, `agent_spawned` (parentage), **dominance [0,1] parent exprimant**, **mutation déterministe** + bornes [0,2], seuil de salience configuré — `BirthSystemTests`, `InheritanceTests` |
| Groupes/Births (déterminisme ph6) | mêmes options + seed ⇒ même séquence de groupes/naissances en 200 ticks, seeds différents ⇒ divergences, événements groupes via contrat — `GroupBirthDeterminismTests` |
| Ressources | réserves globales SYNE-042 (initialisation, consommation, clamp, copie snapshot), épuisement |
| Cycle des ressources (SYNE-070, jalon U8) | minéral 4ᵉ réserve (défaut 0), régénération bornée par tick, **dégradation périodique** (`− rate × degradationTick` tous les `degradationTick`), non-négativité, déterminisme du cycle (deux répliques ⇒ mêmes niveaux), bornes `resources.*` validées — `ResourceStocksTests`, `SimulationOptionsValidatorTests` |
| Constructions / obstacles statiques (SYNE-071, U8) | config `world.obstacles` + `world.obstacleLayout[]` (défauts, chargement JSON, bornes `id`/`radius`/`x`/`y`, layout rejeté quand flag `false`) ; mutation dynamique : `AddObstacle` hors bornes / id dupliqué rejetés, révision incrémentée, trace `PlaceConstruction`/`RemoveConstruction` + drain `ClearEnvironmentChanges` ; **grille A\* re-rasterisable** : `Refresh()` purge le cache et re-rasterise (re-routage après pose, route rétablie après retrait, **no-op** quand rien n'a changé) ; observabilité : `world.construction_placed`/`world.construction_removed` drainés (Console end-to-end), snapshot `obstacles[]` additif, déterminisme non altéré — `WorldTests`, `AStarPathfinderTests`, `PathCacheTest`(s), `ConfigLoaderTests`, `SimulationOptionsValidatorTests`, `ObservabilitySensorTests`, `ObservabilityConstructionTests` |
| Persistance (jalon U8) | reprise bit-à-bit après SQLite (état monde+cognition+RNG), sauvegarde atomique des **11 tables** (PERSISTENCE.md §3, `PRAGMA user_version=2`), rotation `maxBackups`, reprise après crash au dernier `tick_states` — `PersistenceTests` |
| Contrôle HTTP (SYNE-113, U8) | serveur de contrôle :5181 piloté par API (_state machine_ `idle→running⇋paused→finished`) — **`ControlServerWireTests`** : start (`seed`/`maxTicks`) + `state=Running`, pause gèle le tick / resume reprend, reset régénère un `runId`, **non-intrusion** (run piloté tick-à-tick == run ininterrompu, bit-à-bit), 404 JSON sur action inconnue |
| Configuration / Validation (SYNE-100) | toutes les branches du `SimulationOptionsValidator` : bornes monde/débits, sauvegarde (`autoSaveEveryNTicks`, `maxBackups`), perception, communication (caps, portée, `maxHops`, coûts, incompréhension), besoins (rates/triggers/drifts), croyances (plafonds, décroissance), groupe (`CollectiveAlignBonus`, catalogue eat/drink/rest, merge, mortalité), A* (`cellSize`, `maxExpansionCells`, `cacheCapacity`), `null` — `Ph10ValidationCoverageTests` (**`SimulationOptionsValidator.cs` 100 %**) |
| Déterminisme | `DeterminismRegressionTests` (hash épinglé SYNE-015), auto-égalité, checksums ; **`Ph10DeterminismBaselineTests` (SYNE-102)** — journal d'état **complet** (positions, énergie, besoins, mémoire, confiance, groupes, naissances, décès) bit-à-bit pour seeds {12345, 7, 999}, divergence seed différente, baseline épinglée `0x072a488aa18c05eb` |
| Intégration boucle complète (SYNE-101) | **`ObservabilityChainedLoopTests`** — 150 ticks chaînés sans perte : 1 snapshot + 1 `tick_summary`/tick, ticks contigus 1..N, trames JSON valides, 6 sous-systèmes engagés (`decision_made`, `action_completed`, `message_sent`/`message_received`…) + au moins un événement social/population ; agents décédés absents des snapshots post-mortem |
| Performance | `PerceptionBenchmarkTests` (SYNE-012) — budget 10 ms/requête en CI ; **`ObjectPoolTests` + `TickBudgetTests` + `ScaleTargetsTests` (SYNE ph9)** — pooling actif, part computation ≥ 30 %, trajectoire non altérée, planchers anti-régression 50/500/1000, checksum bit-à-bit à l'échelle |

## 4. Tests de déterminisme (critiques)

- **Reproductibilité** : exécuter deux runs identiques → checksums identiques.
- **Reprise** : sauvegarder au tick N, charger, poursuivre → même trajectoire qu'un run ininterrompu (état RNG inclus).
- **Anti-triche** : vérifier que les entités ne voient jamais plus que leur rayon (observabilité partielle).

## 5. Tests de performance

- Les benchmarks (Annexe I) sont des **tests intégrés** : débit, mémoire, CPU.
- Seuil d'échec = objectifs de ticks/s (Annexe I.3).
- **Jalon ph9 (SYNE-090…093)** : `TickBudgetTests` (part computation ≥ 30 % @250 entités/50 ticks, collecte n'altère pas la trajectoire, golden épinglé `0x27fad50065d8c4a4`), `ObjectPoolTests` (Rent/Return, équilibre des buffers de tri), `ScaleTargetsTests` (planchers anti-régression **50 → ≥ 120 t/s, 500 → ≥ 30 t/s, 1000 → ≥ 20 t/s**, meilleur de 3 ; checksum d'état reproductible à 500 entités × 30 ticks). Les planchers retiennent une **marge × ~7 en-deçà des mesures réelles** (PERFORMANCE.md §9.4) pour absorber la contention CI.

## 6. Jalons de validation (Annexe J.2)

| Phase | Critère | Commande indicative |
| :-- | :-- | :-- |
| BDI+Perception | 50 ent., 1000 ticks, pas de crash ; **pipeline BDI testé (SYNE ph1)** | `dotnet test -c Release` ≥ 122 tests |
| Mémoire+Croyances | 50 ent., 2000 ticks, croyances divergentes | `dotnet test --filter "MemoryTests|BeliefTests"` |
| Décision+Utilité | traits différents → décisions différentes | `dotnet test --filter "UtilityEvaluatorTests|CognitionPipelineTests"` |
| Communication (SYNE ph5) | information locale (rayon), interception, relais ≤ 2 sauts | `dotnet test --filter "CommunicationSystemTests"` |
| Performance | micro-benchmark grille < budget CI ; **débits 50/500/1000 ≥ 120/30/20 t/s (ph9)** | `dotnet test --filter "PerceptionBenchmarkTests|ScaleTargetsTests"` |
| Tests | **370 tests (353 Core + 17 Console), 94,01 % de couverture** (≥ 80 % requis, SYNE-100) ; jalons T0–T5 activés (SYNE-122) | `dotnet test --collect:"XPlat Code Coverage"` |
| Déterminisme (SYNE-102) | golden `0x27fad50065d8c4a4` + baseline d'état `0x072a488aa18c05eb` inchangés | `dotnet test --filter "DeterminismRegressionTests|Ph10DeterminismBaselineTests"` |
| Intégration (SYNE-101) | 150 ticks chaînés sans perte (contiguïté 1..N) | `dotnet test --filter "ObservabilityChainedLoopTests"` |
| Contrôle (SYNE-113) | pilotage HTTP :5181 non intrusif (run piloté == ininterrompu) | `dotnet test --filter "ControlServerWireTests"` |
| Jalons T0–T5 (SYNE-122) | T0 50/1000 sans crash + état valide déterministe ; T1 50/2000 croyances divergentes ; T2 traits → décisions différentes (situation identique) ; T3 info locale ; T4 reproductibilité benchmark ; T5 suite ≥ 160 tests | `dotnet test --filter "MilestoneT0T5Tests"` |

## 7. Convention d'écriture

- Test unitaire = comportement observable d'un sous-système avec données explicites (pas de mock hasardeux).
- Chaque test de décision fournit le `DecisionRecord` attendu (SYNE-030 à 033 : `MindState.LastDecisionRecord`).
- Les tests qui dépendent du hasard utilisent une **seed fixe** ; le tirage de conflit de priorités est **déterministe sans PRNG** (hash SplitMix64, `PriorityConflictResolver`).

---

## Points restés ouverts dans ce document
- Répartition numérique exacte des tests (par système) à établir lors de l'implémentation.
- Outillage exact de tests Godot PRISM (hors SYNE) et outillage de tests front (Vitest) — voir `CI_CD.md`.