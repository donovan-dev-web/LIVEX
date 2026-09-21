# CHANGELOG.md

**Composant** : LIVEX (général)
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 septembre 2026
**Dépend de** : `VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`livex-vX.Y.Z` = triplet SYNE + ECHOS + PRISM).

## [Unreleased]

### Added
- Documentation technique V0.1 complète du monorepo (phases 0 à 5 du Plan documentation) :
  - générique racine : `VISION`, `ARCHITECTURE`, `COMMUNICATION`, `GLOSSARY`, `ROADMAP`, `FAQ`, `README` ;
  - gouvernance : `GITFLOW`, `CI_CD`, `VERSIONING`, `LICENSE`, `CONTRIBUTING`, `CODE_OF_CONDUCT`, `SECURITY` ;
  - `docs/governance/*` (ISSUES, PULL_REQUESTS, KANBAN) et templates `.github/` (issues, PR, CI `ci.yml`, release `release.yml`) ;
  - SYNE : 16 docs + ADR-001/002/005–011 ;
  - ECHOS : 14 docs + ADR-001/002 (stack FastAPI + électron, calcul causal) ;
  - PRISM : 12 docs + ADR-001 (choix Godot) ;
  - ADR transverses : ADR-003 (API HTTP REST 5181), ADR-004 (WebSocket 5180) + `0000-template`.
  - `docs/ETHICS_AND_SCOPE.md`.
- **Jalon U0 — Socle & gouvernance** :
  - `syne/` : solution .NET (`Simulation.Core`, `Simulation.Console`, `Simulation.Core.Tests`), `global.json` (SDK 10.0.400), modèle de configuration Annexe H + validation, flags CLI (`--seed`, `--max-ticks`, `--world-size`, `--config`, `--headless`) ;
  - `echos/` : monorepo `echos/` (API FastAPI :5000, package analyse, clients d'ingestion, `echos-ui` React/TS, tests pytest) ;
  - `.gitignore`, `.editorconfig`, branches Git Flow (`develop`), milestones dédupliqués, labels normalisés, CI remaniée (jobs filtrés `syne/**`, `echos/**`) ;
  - **ECHOS-1** : structure code ECHOS livrée — paquet `echos` (API FastAPI `create_app()`, registre des **7 moteurs de métriques**, placeholder ingestion), `echos-ui` (Vite + React + TS : lint, build, tests vitest/jsdom), 13 tests pytest couverture 100 %, jobs CI `echos-python` + `echos-ui` actifs ;
  - **ECHOS-2** : contrats d'ingestion SYNE livrés — modèles pydantic `WorldSnapshot`/`ExternalEvent` (JSON camelCase), clients `WsClient` :5180 + `ControlClient` :5181 testés de façon déterministe sur **golden files versionnés** (41 tests pytest, couverture 97 %).

### Changed
- Divergence ECHOS annoncée et documentée (prototype C#/.NET + Django → **FastAPI + Electron/React/TypeScript**, SQLite/Parquet).
- README et INSTALLATION reflètent l'état du socle U0 ; **conteneurisation Docker reportée** au-delà du Jalon U0.

### Deprecated
- (aucun)

## [0.0.0] — à venir

Première version consolidée (aucune).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | Documentation V0.1
| 21 septembre 2026 | ECHOS-1 : structure code livrée (echos + echos-ui) | Jalon U0 — socle ECHOS |
| 21 septembre 2026 | ECHOS-2 : contrats d'ingestion + golden files | Jalon U0 — clôture ECHOS |