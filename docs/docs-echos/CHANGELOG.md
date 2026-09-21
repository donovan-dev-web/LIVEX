# CHANGELOG — ECHOS

**Composant** : ECHOS
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 septembre 2026
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