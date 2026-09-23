# TESTING.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `DETERMINISM.md`, `ARCHITECTURE.md`
**Source Monographie** : §7.1 (xUnit + Moq), Annexe J (jalons de validation, 160+ tests), Annexe I (benchmarks)

---

## 1. Objectif

Garantir — par des tests automatisés — la **correction**, le **déterminisme** et la **performance** de SYNE. Jalon : **160+ tests** (Annexe J.1) et **couverture ≥ 80 %** (Annexe I.3). État V0.1 : **279 tests** (suite Core + Console : baseline U0 62 → +60 au jalon SYNE ph1 → +5 observabilité SYNE-080 → +2 tests de fil WebSocket → +17 au jalon SYNE ph2 → +20 au jalon SYNE ph3 → +30 au jalon SYNE ph4 → +13 au jalon SYNE ph5, dont 3 `Simulation.Console.Tests` → +32 au jalon SYNE ph6 : 9 `GroupSystemTests`, 7 `BirthSystemTests`, 7 mécanismes fins d'héritage, 3 validations de configuration, 3 observabilité, 3 `GroupBirthDeterminismTests` → **+19** au jalon SYNE ph7b : 4 `BirthSystemTests` fidélités (SYNE-075), 3 propagation + 1 bonus `GroupObjective` (SYNE-076), 8 `AStarPathfinderTests` + 2 `ActionExecutorTests` (SYNE-077), 1 observabilité preuve SYNE-081/082 ; +1 `test_health.py` ECHOS — registre `/api/runs/{id}/decisions` → **+12** au jalon SYNE ph9 : 5 `ObjectPoolTests`, 3 `TickBudgetTests` (part computation ≥ 30 % + non-altération trajectoire + golden), 4 `ScaleTargetsTests` (planchers 50/500/1000 + checksum à l'échelle)). Checksum doré ré-épinglé `0x27fad50065d8c4a4` (engineVersion 0.6.0), inchangé au ph9 (instrumentation/pooling déterministes).

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
| Observabilité (SYNE-080) | format camelCase des messages (snapshot/event), épinglage et déterminisme d'émission, contrat `decision_made` + `action_completed` + `message_sent`/`message_received`, `resources` peuplées, **engineVersion 0.5.0** ; **tests de fil WebSocket réels** (`Simulation.Console.Tests`) |
| Groupes (SYNE ph6) | cohésion confiance × affinité (lien = confiance **et** part commune), composantes union-find, **cycle de vie par correspondance exacte des membres** (turnover ⇒ dissolution [+refonte]), taille minimale, leader par confiance entrante (tie-break id), décisions pondérées + quorum, formation/dissolution/décision événements + snapshot `groups[]` — `GroupSystemTests` |
| Naissance & Héritage (SYNE ph6) | fusion consentie (min confiance réciproque ≥ seuil, paire d'id minimal, enfant médian clampé), allocation d'id croissante, naissances fusionnées post-boucle, `agent_spawned` (parentage), **dominance [0,1] parent exprimant**, **mutation déterministe** + bornes [0,2], seuil de salience configuré — `BirthSystemTests`, `InheritanceTests` |
| Groupes/Births (déterminisme ph6) | mêmes options + seed ⇒ même séquence de groupes/naissances en 200 ticks, seeds différents ⇒ divergences, événements groupes via contrat — `GroupBirthDeterminismTests` |
| Ressources | régénération, épuisement |
| Persistance | sauvegarde/charge JSON et SQLite |
| Déterminisme | `DeterminismRegressionTests` (hash épinglé SYNE-015), auto-égalité, checksums |
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
| Tests | 160+ tests, ≥ 80 % | `dotnet test --collect:"XPlat Code Coverage"` |

## 7. Convention d'écriture

- Test unitaire = comportement observable d'un sous-système avec données explicites (pas de mock hasardeux).
- Chaque test de décision fournit le `DecisionRecord` attendu (SYNE-030 à 033 : `MindState.LastDecisionRecord`).
- Les tests qui dépendent du hasard utilisent une **seed fixe** ; le tirage de conflit de priorités est **déterministe sans PRNG** (hash SplitMix64, `PriorityConflictResolver`).

---

## Points restés ouverts dans ce document
- Répartition numérique exacte des tests (par système) à établir lors de l'implémentation.
- Outillage exact de tests Godot PRISM (hors SYNE) et outillage de tests front (Vitest) — voir `CI_CD.md`.