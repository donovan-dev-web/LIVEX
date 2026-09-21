# ISSUES.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `ISSUES.md` (racine, conventions), `KANBAN.md` (governance), `DECISIONS_ECHOS.md`, `ROADMAP.md` ECHOS
**Source Monographie** : Annexe K (feuille de route V2), §4.10.3 (règle d'or), §9.6.4 (issues ADR)

---

## 1. Objectif

Ce document est le **backlog complet des issues du composant ECHOS**, conçu pour être la **source unique de création des cartes du tableau Kanban** (`docs/governance/KANBAN.md`). Il couvre la réalisation **de A à Z** d'ECHOS : consommation des contrats SYNE, ingestion, les 7 moteurs de métriques, indicateurs d'émergence, API REST, logging, analyse causale, comparaison expérimentale, interface Écrans (Electron/React) et tests.

Chaque issue est **prête à être copiée** dans un système d'issues (GitHub/GitLab) avec son **titre**, son **label**, son **milestone**, sa **priorité**, ses **dépendances** et son **critère d'acceptation** — conformément aux règles de `docs/governance/ISSUES.md` (types, cycle de vie) et aux colonnes du Kanban.

## 2. Règle d'application (cohérence avec SYNE)

ECHOS **observe et analyse** ; il ne **modifie pas** le phénomène mesuré (Monographie §4.10.3 — règle d'or : *« l'instrument d'observation ne doit pas transformer le phénomène étudié »*). En conséquence :

- Toute issue ECHOS qui lit l'état SYNE via WebSocket :5180 / HTTP :5181 doit rester **non intrusive** : aucune écriture dans le monde observé.
- Les contrats de transport sont **hérités** de SYNE (`API_CONTRACTS.md` SYNE, `COMMUNICATION_PROTOCOL.md` §2.4) : ECHOS se cale dessus, il ne les redéfinit pas.
- La **persistance d'analyse** (SQLite + Parquet) est **séparée** de la persistance de référence SYNE (Annexe L).

---

## 3. Backlog — réalisation A → Z d'ECHOS

Chaque sous-section = un milestone (aligné sur `ROADMAP.md` ECHOS, jalons J1–J5). Colonnes : ID · Titre · Labels · Priorité · Dépend de · Critère d'acceptation.

### Milestone ph0 (echos) — Fondation & stack

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-001 | Décision applicative ECHOS (ADR-001 ECHOS) : FastAPI + Electron/React | `type/governance`, `component/echos`, `type/docs` | P0 | ADR-001 ECHOS, `VISION.md`, Monographie §4.2.1 | ADR-001 accepté : stack = FastAPI (PAS Django) + Electron/React/TS ; divergence vs Monographie documentée dans `ARCHITECTURE.md` ECHOS |
| ECHOS-002 | Structure monorepo `echos/` (API, analyse, moteurs, UI, tests) | `type/feature`, `component/echos` | P0 | ECHOS-001 | Monorepo découpé ; chaque sous-composant buildable et testable séparément |
| ECHOS-003 | Outillage CI/tests (pytest, couverture, golden files) | `type/test`, `component/echos` | P0 | ECHOS-002, `TESTING.md` ECHOS | Pipeline pytest vert ; couverture instrumentée ; golden files versionnés |
| ECHOS-004 | Contrats d'ingestion hérités (WebSocket 5180 / HTTP 5181 SYNE) | `type/feature`, `component/echos`, `component/syne` | P0 | `../docs-syne/API_CONTRACTS.md`, `COMMUNICATION_PROTOCOL.md` §2.4 (SYNE) | Client WebSocket 5180 + contrôle HTTP 5181 opérationnels ; déterminisme de réception |

### Milestone ph1 (echos) — Ingestion & stockage

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-010 | Consommateur WebSocket 5180 (snapshot + événements) — **LIVRÉ (issue #367, PR E1, U1)** | `type/feature`, `component/echos`, `component/syne` | P0 | ECHOS-004, ADR-001 ECHOS | Consommation des `snapshot`/`event` ; tick aligné sur la boucle SYNE — ✓ `aligned_ticks`/`TickSegment` (1 snapshot + événements par tick, refus des désalignements), modèles alignés sur l'émetteur V0.1, tests serveur WebSocket réel in-process, smoke E2E SYNE→ECHOS documenté |
| ECHOS-011 | Agrégation incrémentale (séries temporelles) — **LIVRÉ (issue #368, PR E2, U1)** | `type/feature`, `component/echos` | P0 | ECHOS-010 | Agrégation par tick sans perte ; sous-échantillonnage paramétrable — ✓ `TickRecord.from_segment`/`summarize` (1 résumé par tick, `sample_every`), `downsample(records, every)` en lecture, réductions déterministes testées |
| ECHOS-012 | Schéma SQLite d'analyse (Annexe G / Annexe L ECHOS) — **LIVRÉ (issue #369, PR E2, U1)** | `type/persistance`, `component/echos` | P0 | ECHOS-010, `ARCHITECTURE.md` ECHOS §3 | Schéma stable ; tables d'analyse distinctes des tables SYNE — ✓ `AnalyticsStore` SQLite (tables `runs`/`tick_summaries`/`events_log` + `_meta`), `SCHEMA_VERSION`, FK activées, dump du schéma versionné et testé |
| ECHOS-013 | Série Parquet (séries lourdes) — **LIVRÉ (issue #370, PR E2, U1)** | `type/persistance`, `component/echos` | P1 | ECHOS-012 | Séries lourdes en Parquet ; jointure SQLite↔Parquet cohérente — ✓ `agent_rows`/`write_agent_series`/`read_agent_series` (pyarrow snappy), `coherence_errors` sur la clé `(run_id, tick)` jointe à `tick_summaries` |

### Milestone ph2 (echos) — Moteurs de métriques (7 moteurs)

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-020 | Moteur 1 — CognitiveDiversityMetrics (diversité cognitive) — **LIVRÉ (issue #371, PR ECHOS, U2)** | `type/feature`, `component/echos` | P0 | ECHOS-011, `METRICS_SPEC.md` §2 | 8 métriques (§2) calculées → croyances/objectifs/intentions — ✓ entropie de Shannon (faits), désaccord majoritaire, variance de confiance, convergence des objectifs, expression des traits |
| ECHOS-021 | Moteur 2 — InformationPropagationMetrics (propagation) — **LIVRÉ (issue #372, PR ECHOS, U2)** | `type/feature`, `component/echos` | P0 | ECHOS-011, `METRICS_SPEC.md` §3 | 5 métriques : volume, diffusion, dégradation, sauts, centralité — ✓ événements `message_sent` (ADR-004), diffusion 80 %, perte 10 %/saut |
| ECHOS-022 | Moteur 3 — SocialComplexityMetrics (complexité sociale) — **LIVRÉ (issue #373, PR ECHOS, U2)** | `type/feature`, `component/echos` | P0 | ECHOS-011, `METRICS_SPEC.md` §4 | 7 métriques : confiance, variance, densité, clustering, centralité, communautés — ✓ graphe de confiance, communautés par **propagation d'étiquettes déterministe** (§4, écart Louvain documenté) |
| ECHOS-023 | Moteur 4 — GoalConvergenceMetrics (convergence objectifs) — **LIVRÉ (issue #374, PR ECHOS, U2)** | `type/feature`, `component/echos` | P1 | ECHOS-011, `METRICS_SPEC.md` §5 | Alignement/divergence des objectifs ; entropie de distribution — ✓ alignement global, entropie, potentiel de coopération Σp², `GoalTypeCounts` |
| ECHOS-024 | Moteur 5 — FeedbackLoopDetector (boucles de rétroaction) — **LIVRÉ (issue #375, PR ECHOS, U2)** | `type/feature`, `component/echos` | P1 | ECHOS-011, `METRICS_SPEC.md` §6 | Cycles action→conséquence→décision détectés (fenêtre 100 ticks + fréquence > 2) — ✓ heuristique fenêtre glissante `history` (défaut 100 ticks), seuil > 2, facteur d'amplification, boucles critiques > 1,5× |
| ECHOS-025 | Moteur 6 — ResourceSustainabilityMetrics (durabilité ressources) — **LIVRÉ (issue #376, PR ECHOS, U2)** | `type/feature`, `component/echos` | P2 | ECHOS-011, `METRICS_SPEC.md` §7 | Surexploitation / épuisement / régénération mesurés — ✓ ratio dispo/consommation (`resource_consumed`), `CriticalityPoints` < 20 %, `RecoveryTime` sur historique |
| ECHOS-026 | Moteur 7 — GroupDynamicsMetrics (dynamique de groupes) — **LIVRÉ (issue #377, PR ECHOS, U2)** | `type/feature`, `component/echos` | P2 | ECHOS-011, `METRICS_SPEC.md` §8 | Formation/dissolution de groupes ; stabilité des coalitions — ✓ communautés du graphe de confiance, `group_formed`/`group_dissolved`, taux par 1000 ticks |
| ECHOS-027 | Golden files des 7 moteurs (jalon J2) — **LIVRÉ (issue #378, PR ECHOS, U2)** | `type/test`, `component/echos` | P0 | ECHOS-020→026, `METRICS_SPEC.md` | Scores bit-à-bit identiques entre runs à seed égale ; golden files 18+ tests — ✓ `fixtures/snapshot_analysis.json` + `golden/analysis_golden.json`, `test_j2_determinism.py` (rejeu bit-à-bit de 2 runs, dernier tick == golden), **115 tests**, couverture 98,2 % |

### Milestone ph3 (echos) — Indicateurs d'émergence

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-030 | Score d'émergence composite [0,1] | `type/feature`, `component/echos` | P0 | ECHOS-027, `EMERGENCE_INDICATORS.md` | Score dans [0,1] ; stable entre runs identiques (jalon J3) |
| ECHOS-031 | Auto-détection de phénomènes émergents | `type/feature`, `component/echos` | P1 | ECHOS-030, `EMERGENCE_INDICATORS.md` | Détection automatique ; trace des signaux déclencheurs |
| ECHOS-032 | Mesures d'émergence vs règle d'or §4.10.3 | `type/obs`, `component/echos` | P1 | ECHOS-030, Monographie §4.10.3 | Les scores ne sont jamais présentés comme preuve d'intelligence ; disclaimer documenté |
| ECHOS-033 | Indicateurs de complexité & d'imprévisibilité | `type/feature`, `component/echos` | P2 | ECHOS-030, `EMERGENCE_INDICATORS.md` | Complexité + imprévisibilité mesurées ; non régression |

### Milestone ph4 (echos) — API REST

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-040 | API REST de base (FastAPI, port 5000) | `type/feature`, `component/echos` | P0 | ECHOS-012, `API_REST.md` ECHOS | Endpoints `/api/runs`, `/api/runs/{id}` opérationnels |
| ECHOS-041 | Métriques & export (`/metrics`, `/export`) | `type/feature`, `component/echos` | P0 | ECHOS-040, ECHOS-012, `API_REST.md` | Métriques JSON/CSV ; export reproductible |
| ECHOS-042 | Croyances & relations observées (`/beliefs`, `/relationships`) | `type/feature`, `component/echos`, `component/syne` | P1 | ECHOS-040 | Accès lecture des croyances/confiance par entité, sans intrusion |
| ECHOS-043 | Groupes & phénomènes émergents exposés | `type/feature`, `component/echos` | P1 | ECHOS-040, ECHOS-031 | Groupes actifs + phénomènes détectés via API |
| ECHOS-044 | Sous-échantillonnage & cache de séries | `type/perf`, `component/echos` | P1 | ECHOS-041, `PERFORMANCE.md` ECHOS | Séries longues servies sans mémoire explosive ; cache validé |
| ECHOS-045 | Couverture API ≥ 80 % (jalon J5) | `type/test`, `component/echos` | P0 | ECHOS-040→044, `TESTING.md` ECHOS | 80 %+ des endpoints testés ; golden files maintenus |

### Milestone ph5 (echos) — Logging & instrumentation

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-050 | Logging structuré (JSON) des métriques | `type/feature`, `component/echos` | P0 | ECHOS-011, `LOGGING_INSTRUMENTATION.md` ECHOS | Événements structurés (3 niveaux : structuré/traces/texte) |
| ECHOS-051 | Trace des décisions SYNE consommées | `type/obs`, `component/echos`, `component/syne` | P1 | ECHOS-050, `../docs-syne/COGNITIVE_ARCHITECTURE.md` §3.4.10 | Traces `decision_traces` disponibles pour analyse causale |
| ECHOS-052 | Profilage des 7 moteurs | `type/perf`, `component/echos` | P1 | ECHOS-027, `LOGGING_INSTRUMENTATION.md` | Coût par moteur tracé ; budgets respectés (§performance) |

### Milestone ph6 (echos) — Analyse causale

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-060 | Décision calcul causal (ADR-002 ECHOS) : hors ligne sur traces | `type/governance`, `component/echos`, `type/docs` | P0 | ADR-002 ECHOS, `CAUSAL_ANALYSIS.md` | ADR-002 accepté : calcul déterministe hors ligne (PAS temps réel) |
| ECHOS-061 | Reconstruction de chaînes causales | `type/feature`, `component/echos` | P1 | ECHOS-060, ECHOS-051, `CAUSAL_ANALYSIS.md` | Chaîne Action←Intention←Objectif←Besoin←Croyance←Mémoire←Perception reconstruite |
| ECHOS-062 | Détection de boucles causales (cycles) | `type/feature`, `component/echos` | P2 | ECHOS-061, `CAUSAL_ANALYSIS.md` §4.5.3 | Cycles marqués ; profondeur d'affichage limitée |
| ECHOS-063 | Cache & invalidation des analyses causales | `type/perf`, `component/echos` | P2 | ECHOS-061 | Cache des résultats ; invalidation après re-run (déterminisme) |

### Milestone ph7 (echos) — Comparaison expérimentale

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-070 | Comparaison de runs (`/api/compare`) | `type/feature`, `component/echos` | P1 | ECHOS-041, `EXPERIMENT_COMPARISON.md` | Comparaison bit-à-bit / seed différente reproductible |
| ECHOS-071 | Métriques de reproductibilité | `type/feature`, `component/echos` | P1 | ECHOS-070, `METRICS_SPEC.md` | Méta-métriques stables entre runs (déterminisme ECHOS) |
| ECHOS-072 | Format d'export comparatif (CSV/JSON) | `type/feature`, `component/echos` | P2 | ECHOS-070 | Export uniforme pour analyse hors ligne |

### Milestone ph8 (echos) — Interface intégrée (Écrans)

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-080 | Shell Electron + React/TS (V0.1) | `type/feature`, `component/echos` | P0 | ADR-001 ECHOS, `FRONTEND_VISION.md`, `UI_DESIGN.md` | Shell démarre ; communique avec FastAPI local |
| ECHOS-081 | Vue métriques temps réel (WebSocket 5180) | `type/obs`, `component/echos` | P0 | ECHOS-080, ECHOS-011 | Métriques à jour en continu, sans figer l'exécution SYNE |
| ECHOS-082 | Vue croyances & confiance | `type/feat`, `component/echos` | P1 | ECHOS-080, ECHOS-042 | Croyances de l'entité + confiance inter-entités visualisées |
| ECHOS-083 | Vue réseaux sociaux (graphe) | `type/feature`, `component/echos` | P1 | ECHOS-080, ECHOS-022 | Graphe de confiance mis à jour ; communautés (Louvain) affichées |
| ECHOS-084 | Vue calibration / budget | `type/feature`, `component/echos` | P1 | ECHOS-080 | Calibration des valeurs ECHOS paramétrable via interface |
| ECHOS-085 | Contrôle du moteur depuis Écrans | `type/feature`, `component/echos`, `component/syne` | P2 | ECHOS-080, HTTP :5181 SYNE | Pilotage non intrusif via HTTP :5181 (démarrage/arrêt, éviter d'écrire dans le monde) |

### Milestone ph9 (echos) — Tests & couverture

| ID | Titre | Labels | Prio | Dépend de | Critère d'acceptation |
| :-- | :-- | :-- | :-- | :-- | :-- |
| ECHOS-090 | Suite de tests ECHOS (→ 18+ jalons jeu) | `type/test`, `component/echos` | P0 | ECHOS-045, ECHOS-027 | Tests unitaires + golden files ; ≥ 80 % de couverture |
| ECHOS-091 | Tests d'intégration (SYNE↔ECHOS↔PRISM) | `type/test`, `component/echos`, `component/syne`, `component/prism` | P1 | ECHOS-090, `../docs-prism/TESTING.md` | Flux complet sans perte ; déterminisme cross-composants |
| ECHOS-092 | Non-régression des indicateurs (golden files V0.1) | `type/test`, `component/echos` | P0 | ECHOS-030 | Scores inchangés entre versions ; baseline V0.1 figée |

## 4. Règles de suivi (Kanban)

- Toute carte `ECHOS-*` doit avoir **exactement 1 milestone** (`milestone/echos-ph0`…`ph9`, `milestone/v0.1`, `milestone/v1`) et **≥ 1 label de type** (`type/*`).
- Chaque carte porte la colonne Kanban correspondante à son état (Backlog → Todo → In Progress → In Review → Done, `KANBAN.md` §2).
- Une carte ne passe en **In Review** qu'avec une PR référençant l'issue (`Closes #ECHOS-*`).
- Une carte ne passe en **Done** qu'avec **golden files verts** (déterminisme) et couverture respectée.
- Une carte `status/blocked` doit référencer le blocage (ADR, décision `[OUVERT]`, donnée manquante).
- **Conformité** : toute carte doit tracer sa **décision** (`DECISIONS_ECHOS.md` n°) quand elle existe — une valeur non tranchée reste `[OUVERTE]` et calibrée plus tard, jamais figée par accident (Monographie §9.6.4).

---

## 5. Maintenance de ce document

- Document régénéré/actualisé à chaque **jalon** atteint (`ROADMAP.md` ECHOS §3) et à chaque décision tranchée.
- Les IDs `ECHOS-###` sont **stables** : aucune réindexation rétroactive ; un ID consommé est conservé (trace), jamais réutilisé.

---

## Points restés ouverts dans ce document

- Le périmètre exact des **écrans Electron** reste `[OUVERT]` au-delà des besoins définis par les parties 4 et 5 de la Monographie (§ROADMAP ECHOS, LIMITATIONS).
- Les **valeurs chiffrées** (seuils d'émergence, fenêtres, budgets moteur) sont calibrées après les premiers runs valides — cartes de calibration positionnées en fin de cycle, conformément à la règle §9.6.4 (déterminisme bit-à-bit conservé).