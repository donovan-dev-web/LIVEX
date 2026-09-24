# CHANGELOG.md

**Composant** : LIVEX (général)
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 septembre 2026
**Dépend de** : `VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`livex-vX.Y.Z` = triplet SYNE + ECHOS + PRISM).

## [Unreleased]

### Fixed
- Cadrage Jalon U8 (PR cadrage docs) : planche U8 du `ROADMAP` racine recalée sur le backlog réel — plage ECHOS `080…093` fictive remplacée par `080…085` (ph8, livrés U7) + `090…092` (ph9) ;
- Ajout de la carte manquante **SYNE-113** (serveur de contrôle HTTP :5181, cible réelle du relais `controlClient` ECHOS — absente du backlog) ;
- `SYNE-110` précisée : hooks `autoSaveEveryNTicks` (défaut 1000) / `maxBackups` (défaut 5) à brancher sur la boucle (aucun consommateur à ce jour) ;
- ECHOS-085 annotée : livrée côté UI, finalisation en U8 contre SYNE-113.

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
- Divergence ECHOS annoncée et documentée (prototype C#/.NET + Django → **FastAPI + web local React/Vite servie par FastAPI pour V0.1**, SQLite/Parquet) ; le **shell Electron est conservé** (implémentation différée à un horizon ultérieur, correction 23/09/2026).
- **Correction Electron (23/09/2026)** : reformulation « PAS de shell Electron » → « shell Electron **conservé**, implémentation **différée post-V0.1** » dans ADR-001 ECHOS, `ARCHITECTURE.md` (ECHOS + racine), `FRONTEND_VISION.md`, `README.md`, `ROADMAP.md`, `ISSUES.md`, `CHANGELOG.md` (ECHOS), `TRANSPORT_API.md` (PRISM). Monographie non modifiée (snapshot figé).
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
| 21 septembre 2026 | Socle U0 : solution, config, PRNG, ADR-012 | SYNE-001 / SYNE-005 / SYNE-006 |
| 21 septembre 2026 | Noyau U0 : boucle, monde + grille, entités + traits | SYNE-002 / SYNE-003 / SYNE-004 |
| 21 septembre 2026 | Jalon SYNE ph1 : BDI + Perception | SYNE-010 → SYNE-015 |
| 21 septembre 2026 | ECHOS-1 : structure code livrée (echos + echos-ui) | Jalon U0 — socle ECHOS |
| 21 septembre 2026 | ECHOS-2 : contrats d'ingestion + golden files | Jalon U0 — clôture ECHOS |
| 21 septembre 2026 | ECHOS-3 : stockage ECHOS — agrégation, SQLite, Parquet, pipeline | Jalon U1 — ECHOS-011/012/013 |
| 21 septembre 2026 | Site de documentation GitHub Pages (DocFX) : landing + docs clés + API Simulation.Core, XML généré | U1 — documents |
| 23 septembre 2026 | ECHOS ph8 : échos-ui — les 6 Écrans (A→F), client REST typé + WS :5180, relais de pilotage :5181, design system | Jalon U7 — interface web |
