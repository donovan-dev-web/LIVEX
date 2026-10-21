# ISSUES.md — Governance

**Composant** : LIVEX (général) 
**Statut** : [STABLE] 
**Dernière mise à jour** : 21 octobre 2026 

---

## 1. Objectif

Ce document définit les règles et conventions pour la création et le suivi des issues GitHub du projet LIVEX. Il sert de référence unique pour :
- La création des issues dans les composants SYNE, ECHOS, PRISM et Launcher.
- L'alignement sur la roadmap et les jalons du projet.
- La traçabilité entre issues, ADRs et décisions de calibration.
- Le cycle de vie des issues (de la création à la fermeture).

---

## 2. Taxonomy des labels

### 2.1 Labels de type

| Label | Usage |
| :-- | :-- |
| `type/feature` | Nouvelle fonctionnalité ou comportement |
| `type/bug` | Correction d'anomalie constatée |
| `type/docs` | Documentation / ADR / décision |
| `type/perf` | Performance, budgets, benchmarks |
| `type/test` | Tests, couverture, fiabilisation |
| `type/calibration` | Calibration des valeurs (décisions ouvertes/fermées) |
| `type/persistence` | Persistance, sauvegarde/chargement, reprise |
| `type/obs` | Observabilité / événements / traces |
| `type/governance` | Process, kanban, jalons |

### 2.2 Labels de composant

| Label | Usage |
| :-- | :-- |
| `component/syne` | Issue portant sur le composant SYNE |
| `component/echos` | Issue portant sur le composant ECHOS |
| `component/prism` | Issue portant sur le composant PRISM / projet Unreal |
| `component/launcher` | Issue portant sur le composant Launcher / installateur |

### 2.3 Labels de statut / priorité / milestone

- **Statut** (complété au fil de l'eau, selon `KANBAN.md`) : `status/blocked`, `status/needs-input`, `status/good-first-issue`.
- **Priorité** : `priority/P0`, `priority/P1`, `priority/P2`, `priority/P3` (mêmes conventions que la Monographie Annexe J / `ROADMAP.md`).
- **Milestone** : `milestone/ph0` … `milestone/ph11`, plus `milestone/v0.1`, `milestone/v1`.

---

## 3. Règle de traçabilité

- Toute valeur **non chiffrée** reste un point **hors spec** : les issues de calibration (décisions n°4, 5, 6, 9, 18, 19, 20…) sont **volontairement** positionnées en fin de cycle (après les premiers runs valides) (bilan : 30 tranchées, 0 ouverte ; les 7 anciennes `[OUVERTES]` devenues `[TRANCHÉES]` restent **configurables**, pas figées par accident).
- Toute issue doit tracer sa **décision** d'origine quand elle existe — une valeur non tranchée reste calibrée plus tard, jamais figée par accident (Monographie §9.6.4).

---

## 4. Cycle de vie des issues

1. **Ouverture** : Issue créée avec labels appropriés, milestone désigné, dépendances notées.
2. **Planification** : Issue déplacée en "Todo" du Kanban, travail commencé.
3. **En cours** : Pull request ouverte, issue déplacée en "In Progress".
4. **Revue** : Revue de la PR, changements demandés, issue déplacée en "In Review".
5. **Fermeture** : PR fusionnée, issue fermée avec référence au commit.
6. **Bloquée** : Issue marquée `status/blocked` avec référence au blocage (ADR, décision `[OUVERTE]`, donnée manquante).

---

## 5. Maintenance de ce document

- Document actualisé lors de chaque décision tranchée et de chaque jalon atteint (`ROADMAP.md` §4 fin de phase).
- Les IDs et conventions sont **stables** : aucune réindexation rétroactive ; un ID consommé est conservé (trace), jamais réutilisé.
- Alignement avec `KANBAN.md` §2 pour les colonnes du tableau.