# CHANGELOG — ECHOS

**Composant** : ECHOS
**Statut** : [DRAFT]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`echos-vX.Y.Z`).

## [Unreleased]

### Added
- Documentation technique V0.1 complète du composant (VISION, ARCHITECTURE, METRICS_SPEC, EMERGENCE_INDICATORS, CAUSAL_ANALYSIS, EXPERIMENT_COMPARISON, API_REST, LOGGING_INSTRUMENTATION, LIMITATIONS, TESTING, ROADMAP).
- ADR-001 (stack FastAPI + Electron/React) et ADR-002 (mode de calcul causal hors ligne).
- ECHOS-1 : structure du monorepo — `echos/` (paquet Python `echos` : API FastAPI `create_app()`, `/health`, squelette des **7 moteurs de métriques** avec registre `known_engines()`, placeholder `ingestion`), `echos-ui/` (Vite + React + TypeScript : lint, build, tests vitest/jsdom), tests pytest (13 tests, couverture 100 %), CI `echos-python`/`echos-ui` activées.
- ECHOS-2 : contrats d'ingestion — modèles pydantic `WorldSnapshot` / `ExternalEvent` (JSON **camelCase**, API_CONTRACTS.md §2, alias ingress + sortie `by_alias`), `parse_message()` déterministe, client WebSocket `WsClient` (:5180, transport injectable), client de contrôle `ControlClient` (:5181 : start/pause/resume/reset), golden files versionnés (`fixtures/` + `golden/`), tests ingestion (41 tests, couverture 97 %).
- **ECHOS-010 (issue #367, milestone ph1)** : consommation du flux **alignée sur les ticks** — `TickSegment` (snapshot + événements d'un tick) et `aligned_ticks()` (refus déterministe des séquences désalignées) ; **modèles alignés sur l'émetteur SYNE V0.1** (agents sans `health` mais avec `species`/`fatigue`, `health` conservée pour compat doc) ; itération du client réel propres sur fermeture (`ConnectionClosed` → fin de flux) ; tests **serveur WebSocket réel in-process** rejouant le contrat V0.1 (goldens `*_v01`). Tests : 43 → **57**, couverture 98.7 %.
- **ECHOS-011 → ECHOS-013 (issues #368/#369/#370, milestone ph1)** : paquet `echos/storage/` — **agrégation incrémentale** `TickRecord.from_segment`/`summarize` (1 résumé par tick, **sans perte**), sous-échantillonnage `sample_every`/`downsample` (1 sur N, API_REST §4) ; **schéma SQLite d'analyse stable** `AnalyticsStore` (`SCHEMA_VERSION`, tables `runs`/`tick_summaries`/`events_log` **distinctes des tables SYNE**, FK activées) ; **séries lourdes Parquet** `agent_rows`/`write_agent_series`/`read_agent_series` (pyarrow, snappy) ; pipeline `consume()` boucle flux → SQLite + Parquet, **jointure SQLite ↔ Parquet cohérente** (`coherence_errors`). Dépendance ajoutée : `pyarrow>=20,<26`. Tests : 57 → **73**, couverture 98.7 %.
- **ECHOS ph2 — Moteurs de métriques (ECHOS-020 → ECHOS-027, issues #371 → #378, milestone ph2)** :
  - **Contrat enrichi SYNE U2 (additif)** : modèles d'ingestion — `Agent` accepte désormais `traits` (dict), `beliefs` (`subject`/`predicate`/`value`/`confidence`), `goals` (`kind`/`age`), `trust` (`peerId`/`trust`) et `memoryCount` (camelCase, optionnels) — rétro-compatibilité transport V0.1 (fixture `world_snapshot_u2.json`, roundtrip testé).
  - **7 moteurs pur·s déterministes** (`echos/analysis/`) : `CognitiveDiversityMetrics` (8 métriques), `InformationPropagationMetrics` (5, événements `message_sent`), `SocialComplexityMetrics` (7, graphe de confiance + **communautés par propagation d'étiquettes** — écart vs Louvain documenté dans `METRICS_SPEC.md` §4), `GoalConvergenceMetrics` (4), `FeedbackLoopDetector` (5, heuristique fenêtre **100 ticks / fréquence > 2**), `ResourceSustainabilityMetrics` (3, `resource_consumed` + `RecoveryTime` sur historique), `GroupDynamicsMetrics` (7, `group_formed`/`group_dissolved`).
  - **Contrat** : `compute(snapshot: dict) -> dict` (transport camelCase), **données manquantes → valeurs neutres 0.0**, clés inconnues ignorées, aucune mutation d'entrée, registre `known_engines()` inchangé.
  - **Preuve J2 (ECHOS-027)** : golden files versionnés `fixtures/snapshot_analysis.json` + `golden/analysis_golden.json` (tous les moteurs, valeurs vérifiées à la main) ; `test_j2_determinism.py` — **rejeu bit-à-bit de 2 runs** (séries de métriques strictement identiques) et dernier tick == golden ; tests par moteur à valeurs attendues calculées à la main. Tests : 73 → **115**, couverture **98,2 %**. flake8 + pytest `--cov-fail-under=80` verts.
- **ECHOS ph3 — Indicateurs d'émergence (ECHOS-030 → ECHOS-033, issues #379 → #382, milestone ph3)** :
  - **Moteur composite `EmergenceIndicators`** (`echos/analysis/emergence.py`, 8ᵉ moteur du registre `known_engines()` qui devient contractuel) : `compute(snapshot)` exécute les 6 moteurs entrants puis compose ; `compute_from_metrics(metrics)` miroir du `Calculate` du prototype. Fonctions pures/déterministes (aucun PRNG).
  - **ECHOS-030 Score composite [0,1]** : `EmergenceScore = BeliefDiversity×0.15 + GoalDiversity×0.15 + DiffusionSpeed_Norm×0.10 + ClusteringCoefficient×0.15 + LoopStrength×0.20 + (ActiveGroups/100)×0.25`, **clampé [0,1]** (entropies pouvant excéder 1), `DiffusionSpeed_Norm = clamp(1 − InformationDiffusionSpeed/100, 0, 1)` avec **neutralité étendue** (vitesse non mesurée → 0.0, convention ECHOS-006). Valeur de référence : 0.7585336.
  - **ECHOS-031 Phénomènes auto-détectés** : `DetectedPhenomena` — 5 phénomènes (CommunityFormation, FeedbackLoops, CollectiveCoordination, InformationBottleneck, OrganizationalDynamics, ordre stable) avec **trace des signaux déclencheurs** `[{metric, value, threshold}]` ; sur la fixture, seul InformationBottleneck (centralité 1.0 > 0.3).
  - **ECHOS-033 Complexité & imprévisibilité** : `SystemComplexity = (BeliefDiversity + GoalDiversity + InformationDiffusionSpeed)/3` (formule littérale, non bornée — incohérence d'échelle documentée) ; `UnpredictabilityIndex = LoopStrength × DecisionDiversity` (**décision [OUVERTE] résolue** : `DecisionVariability` n'existe dans aucun moteur, cf. EMERGENCE_INDICATORS.md §5).
  - **ECHOS-032 Règle d'or §4.10.3** : constante `DISCLAIMER` émise par le moteur (« jamais une preuve de l'existence d'une intelligence ou d'une société »), invariante et testée.
  - **Preuve J3** : `test_j3_determinism.py` — rejeu bit-à-bit des indicateurs, score borné sur [0,1] à chaque cadre, dernier cadre == golden ; golden `analysis_golden.json` étendu avec `EmergenceIndicators`. Tests : 120 → **146**, couverture **98,3 %**.
- **ECHOS ph4 — API REST (ECHOS-040 → ECHOS-045, issues #383 → #388, milestone ph4)** :
  - **Schéma SQLite v2** (`SCHEMA_VERSION = "2"`) : tables **`tick_metrics`** (métriques numériques des 8 moteurs, 1 ligne par métrique et par tick, PK `(run_id, tick, engine, metric)`) et **`tick_contexts`** (JSON par tick : `agents`, `groups`, `phenomena` — alimente croyances/relations/groupes/phénomènes) ; connexion **thread-safe** (`check_same_thread=False` + verrou partagé) — l'API FastAPI sert plusieurs requêtes de front.
  - **Métriques calculées à l'ingestion** : le pipeline `consume()` exécute `analysis.compute_all(snapshot)` (nouveau : **8 moteurs agrégés**, le composite réutilise les 6 moteurs entrants sans double calcul) et écrit `tick_metrics` + contextes — aucun recalcul à la lecture (API_REST.md §4).
  - **Endpoints REST** (`echos/api/routes.py`) : `GET /api/runs` (liste + bornes), `GET /api/runs/{id}` (métriques complètes + phénomènes), `GET /api/runs/{id}/metrics` (séries moteur×métrique + `latest`, filtres `engine`/`metric`, **`?every=N`** sous-échantillonnage index-based), `GET /api/runs/{id}/export` (**JSON ou CSV RFC 4180**, export **reproductible** — aucun horodatage, tri stable), `GET /api/beliefs/{agentId}` et `GET /api/relationships/{agentId}` (**lecture seule** — règle d'or §4.10.3), `GET /api/groups` et `GET /api/emergent-phenomena` (communautés + phénomènes via contexte), résolution du run par défaut (le plus récent).
  - **Cache de séries (ECHOS-044)** : `SeriesCache` LRU **borné** (256) et thread-safe, invalidé par `AnalyticsStore.ingest_version` (écritures) — séries longues servies sans mémoire explosive.
  - **Config API** : `create_app(store=None)` lit `ECHOS_ANALYTICS_DB` (sinon 503 sur les routes de données, contrat publié), `app.state.series_cache`, `/` annonce les 9 endpoints.
  - **Preuve J5 (ECHOS-045)** : `test_api_routes.py` (15 tests) + `test_sqlite_store.py`/`test_pipeline.py` étendus — couverture de la couche API ≥ 80 % (totale **98,2 %**), exports reproductibles testés. Tests : 146 → **167**, `SCHEMA_VERSION = "2"`.
- **ECHOS ph6 — Analyse causale (ECHOS-060 → ECHOS-063, issues #215 → #218, milestone ph6)** :
  - **`echos/analysis/causal.py`** (ECHOS-060/061) — reconstruction **hors ligne** (ADR-002 [Accepted]) : `build_chain(store, run_id, agent_id, tick=None, depth=7, max_depth=12)` produit la chaîne `Action ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception` depuis `decision_traces` + `events_log` (`decision_made` → intention, `message_received` → perception) + contexte `agents` (buts, croyances, mémoire) ; 1 nœud/couche (multiples agrégés dans `detail`, couche vide → `—`) ; besoins triés, croyances triées (déterminisme ECHOS-041).
  - **Boucles de rétroaction (ECHOS-062)** — récurrence de l'action aux ticks précédents (portée 16 dernières traces, `cycles[].ticks`) ; duplicat intra-chaîne arrêté au seuil du retour ; `depth` borné `[1, 12]` (défaut 7), `truncated` signalé.
  - **Cache (ECHOS-063)** — `echos/api/causal_cache.py::CausalCache` (LRU 256, thread-safe) invalidé sur `AnalyticsStore.ingest_version` : re-run ⇒ re-analyse, réponse **reproductible**.
  - **Endpoint** — `GET /api/runs/{run_id}/causal-chains/{agent_id}?tick=&depth=` (`API_REST.md` §3.8) : 404 entité sans trace / run inconnu, 422 `depth`, 503 sans store.
  - **Store** — `latest_decision_tick(run_id, agent_id)` et `context_before(run_id, context_type, tick)` (lecture déterministe, aucune écriture).
  - **Preuve J6** : `test_causal_analysis.py` (16 tests : chaîne complète/défauts, classement besoins, troncature, cycles, cache LRU + invalidation, endpoint 200/404/422/503, déterminisme deux-magasins). Tests : 181 → **197**, couverture **97,9 %**.
- **ECHOS ph5 — Logging & instrumentation (ECHOS-050 → ECHOS-052, issues #389 → #391, milestone ph5)** :
  - **Package `echos/instrumentation/`** (`LOGGING_INSTRUMENTATION.md` §1–§9) :
    - **ECHOS-050 Logging structuré** — `EchosLogger` : `structured-<run>.jsonl` (métriques par tick), `profilage-<run>.jsonl`, `decision-traces-<run>.jsonl`, logs texte quotidiens taggés `[SSE-V2]` ; sérialisation **déterministe** (`sort_keys=True`, compact — export reproductible) ; répertoire `ECHOS_LOG_DIR` (défaut `logs/`).
    - **ECHOS-051 Traces de décision** — `build_decision_trace` : fusion d'un `decision_made` SYNE avec le **contexte BDI observé** (action/utilité/`deliberated`/`interrupted`/cause/besoins/croyances/objectifs/mémoire, aucune écriture dans le monde) ; **table `decision_traces` (schéma v3)**, ingestion dans `consume()` (`ConsumeResult.decision_traces_written`), export API `GET /api/runs/{run_id}/decisions` (tri `(tick, agent_id)`).
    - **ECHOS-052 Profilage** — `profile.Markers` (`time.perf_counter`) autour de **chacun des 8 moteurs** via paramètre `profile` de `compute_all` (duck-typing, sortie inchangée) ; `compute_all_profiled` **bit-à-bit identique** à `compute_all` (déterminisme ECHOS-027) ; contexte `profiling` par tick ; budgets V0.1 en garde-fou CI (cibles de calibration, pas des sims réelles).
  - **Pipeline branché** : `consume(client, store, ..., logger=EchosLogger|None)` écrit 4 contextes par tick (`agents`, `groups`, `phenomena`, **`profiling`**) — `contexts_written == 12` pour 3 ticks.
  - **Preuve J5 étendue** : `test_instrumentation.py` (14 tests : JSONL déterministe + tag SSE-V2 + fusion BDI + profilage bit-à-bit/couverture 8 moteurs/format §5), `test_api_routes.py` (endpoint décisions reproductible + 404), `test_pipeline.py` étendu. Tests : 167 → **181**, `SCHEMA_VERSION = "3"`.

### Changed
- Divergence assumée vs prototype/Monographie : application **FastAPI** (pas Django), interface **Electron + React** intégrée, analyse **Python** (pas C#/.NET), stockage **SQLite/Parquet**.
- `METRICS_SPEC.md` §4 : détection de communautés = **propagation d'étiquettes déterministe** (remplace la mention Louvain, non déterministe bit-à-bit ni disponible en stdlib Python pure) ; hygiène de déterminisme ECHOS préservée.

### Deprecated
- Shell Electron du prototype abandonné → désormais interface Electron **intégrée** à ECHOS.

## [0.0.0] — à venir

Version initiale (instrumentation prototype V1 : 18 tests xUnit, couverture 85 %, agrégation incrémentale validée — [HÉRITÉ]).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | Documentation V0.1 |
| 21 septembre 2026 | ECHOS-1 : structure code (echos + echos-ui) | Jalon U0 — socle ECHOS |
| 21 septembre 2026 | ECHOS-2 : contrats d'ingestion + golden files | Jalon U0 — ECHOS-003/004 |
| 21 septembre 2026 | ECHOS-010 : flux aligné par tick + serveur in-process | Jalon U1 — ECHOS-010 |
| 21 septembre 2026 | ECHOS-011 à 013 : stockage — agrégation, SQLite, Parquet (+ pipeline) | Jalon U1 — clôture ECHOS |
| 21 septembre 2026 | ECHOS-020 à 027 : 7 moteurs de métriques + golden files + preuve J2 | Jalon U2 — moteurs & déterminisme |
| 22 septembre 2026 | ECHOS-030 à 033 : indicateurs d'émergence (score composite, phénomènes, complexité/imprévisibilité) + preuve J3 | Jalon U3 — indicateurs d'émergence |
| 22 septembre 2026 | ECHOS-040 à 045 : API REST (runs, métriques/séries + `?every`, export JSON/CSV reproductible, croyances/relations, groupes, phénomènes, cache de séries) + preuve J5 | Jalon U4 — API REST |