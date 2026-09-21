# TESTING.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 21 septembre 2026
**Dépend de** : `DETERMINISM.md`, `ARCHITECTURE.md`
**Source Monographie** : §7.1 (xUnit + Moq), Annexe J (jalons de validation, 160+ tests), Annexe I (benchmarks)

---

## 1. Objectif

Garantir — par des tests automatisés — la **correction**, le **déterminisme** et la **performance** de SYNE. Jalon : **160+ tests** (Annexe J.1) et **couverture ≥ 80 %** (Annexe I.3). État V0.1 : **129 tests** (baseline U0 62 → +60 au jalon SYNE ph1 → +5 observabilité SYNE-080 → +2 tests de fil WebSocket).

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
| Décision / Utilité | formule complète, hystérésis, interruptions, cache |
| Actions | déclaratives, pool d'actions, coûts |
| Communications | rayon, incompréhension, dégradation par hop, bande passante |
| Observabilité (SYNE-080) | format camelCase des messages (snapshot/event), épinglage et déterminisme d'émission, contrat `decision_made` ; **tests de fil WebSocket réels** (`Simulation.Console.Tests`) |
| Groupes | formation, cohésion, leader, dissolution |
| Ressources | régénération, épuisement |
| Persistance | sauvegarde/charge JSON et SQLite |
| Déterminisme | `DeterminismRegressionTests` (hash épinglé SYNE-015), auto-égalité, checksums |
| Performance | `PerceptionBenchmarkTests` (SYNE-012) — budget 10 ms/requête en CI |

## 4. Tests de déterminisme (critiques)

- **Reproductibilité** : exécuter deux runs identiques → checksums identiques.
- **Reprise** : sauvegarder au tick N, charger, poursuivre → même trajectoire qu'un run ininterrompu (état RNG inclus).
- **Anti-triche** : vérifier que les entités ne voient jamais plus que leur rayon (observabilité partielle).

## 5. Tests de performance

- Les benchmarks (Annexe I) sont des **tests intégrés** : débit, mémoire, CPU.
- Seuil d'échec = objectifs de ticks/s (Annexe I.3).

## 6. Jalons de validation (Annexe J.2)

| Phase | Critère | Commande indicative |
| :-- | :-- | :-- |
| BDI+Perception | 50 ent., 1000 ticks, pas de crash ; **pipeline BDI testé (SYNE ph1)** | `dotnet test -c Release` ≥ 122 tests |
| Mémoire+Croyances | 50 ent., 2000 ticks, croyances divergentes | `dotnet test --filter "MemoryTests|BeliefTests"` |
| Décision+Utilité | traits différents → décisions différentes | `dotnet test --filter "UtilityEvaluatorTests|CognitionPipelineTests"` |
| Communication | information locale (rayon) | — |
| Performance | micro-benchmark grille < budget CI | `dotnet test --filter "PerceptionBenchmarkTests"` |
| Tests | 160+ tests, ≥ 80 % | `dotnet test --collect:"XPlat Code Coverage"` |

## 7. Convention d'écriture

- Test unitaire = comportement observable d'un sous-système avec données explicites (pas de mock hasardeux).
- Chaque test de décision fournit le `DecisionRecord` attendu.
- Les tests qui dépendent du hasard utilisent une **seed fixe**.

---

## Points restés ouverts dans ce document
- Répartition numérique exacte des tests (par système) à établir lors de l'implémentation.
- Outillage exact de tests Godot PRISM (hors SYNE) et outillage de tests front (Vitest) — voir `CI_CD.md`.