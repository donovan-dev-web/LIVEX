# CHANGELOG — ECHOS

**Composant** : ECHOS
**Statut** : [DRAFT]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`echos-vX.Y.Z`).

## [Unreleased]

### Added
- Documentation technique V0.1 complète du composant (VISION, ARCHITECTURE, METRICS_SPEC, EMERGENCE_INDICATORS, CAUSAL_ANALYSIS, EXPERIMENT_COMPARISON, API_REST, LOGGING_INSTRUMENTATION, LIMITATIONS, TESTING, ROADMAP).
- ADR-001 (stack FastAPI + Electron/React) et ADR-002 (mode de calcul causal hors ligne).

### Changed
- Divergence assumée vs prototype/Monographie : application **FastAPI** (pas Django), interface **Electron + React** intégrée, analyse **Python** (pas C#/.NET), stockage **SQLite/Parquet**.

### Deprecated
- Shell Electron du prototype abandonné → désormais interface Electron **intégrée** à ECHOS.

## [0.0.0] — à venir

Version initiale (instrumentation prototype V1 : 18 tests xUnit, couverture 85 %, agrégation incrémentale validée — [HÉRITÉ]).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | Documentation V0.1 |