# CHANGELOG.md

**Composant** : LIVEX (général)
**Statut** : [DRAFT]
**Dernière mise à jour** : 24 septembre 2026
**Dépend de** : `VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`livex-vX.Y.Z` = triplet SYNE + ECHOS + PRISM).

## [Unreleased]

### Added
- **Jalon U8 — Constructions = obstacles statiques (SYNE-071)** :
  - `world.obstacles` réactivé (défaut `false`) + layout initial `world.obstacleLayout[]` (`{id, x, y, radius}`) posé au build des mondes (CLI + serveur de contrôle) ;
  - mutation dynamique validée : `AddObstacle` borné/id unique avec révision, constructions tracées `PlaceConstruction`/`RemoveConstruction` (modification d'environnement) ;
  - grille A\* re-rasterisable : `AStarPathfinder.Refresh()` purge le cache LRU et re-rasterise si la révision du monde a changé (no-op sinon), câblé en tête du déplacement déterministe ;
  - traçabilité : événements `world.construction_placed`/`world.construction_removed` + champ `obstacles[]` dans le snapshot (API_CONTRACTS §2) ; bornes `world.obstacleLayout[]` validées ;
  - `engineVersion` **0.7.0 → 0.8.0** ; checksums dorés inchangés (0 tirage PRNG, Refresh no-op sur le scénario de référence) ;
  - tests : +20 (8 World, 4 A\*, 2 ConfigLoader, 3 Validator, 3 ObservabilitySensor, 2 Console end-to-end) — **345 Core + 17 Console = 362 tests** au total.
- **Jalon U8 — Cycle des ressources (SYNE-070)** :
  - 4ᵉ type de réserve **`Mineral`** (`ResourceKind.Mineral`, défaut 0) — initialisation enum-driven, exposé dans l'observabilité `resources[]` (4 types) et persisté SQLite (`resources`/`resource_snapshots`) ;
  - **régénération & dégradation périodique** : `ResourceStocks.ApplyLifecycle(tick, settings)` appliquée en fin de tick (ordre causal strict, 0 tirage PRNG) — `+ regenerationRate`/tick, dégradation `− regenerationRate × degradationTick` à chaque période, clamp ≥ 0 ;
  - bornes `resources.*.{initial,regenerationRate,degradationTick}` ajoutées à la validation (CONFIGURATION.md §6.7) ;
  - `engineVersion` **0.6.0 → 0.7.0** ; checksums dorés inchangés (pin contractuel, DETERMINISM.md §7) ;
  - tests : +7 Core (cycle, minéral, validation, persistance 4 réserves) — **327 Core + 15 Console = 342 tests** au total.
- **Jalon U8 — Persistance SQLite bit-à-bit (SYNE-110/111/112)** :
  - `SimulationSnapshot`/`SimulationSnapshotCodec` : capture déterministe (JSON `System.Text.Json`) de l'état complet du monde + cognition + PRNG 4×64, avec hash stable ;
  - `SimulationSnapshotRestorer` : reprise exacte au tick N sans re-jouage (kill states dérivés reconstruits déterministiquement, holdover `LastDecision` + réserves restaurées en place) ;
  - `SqlitePersistenceStore` : schéma V2.0 des **11 tables** (PERSISTENCE.md §3, `PRAGMA user_version = 2`), transactions atomiques, **rotation `maxBackups`** (défaut 5), reprise au dernier `tick_states` (SYNE-112 post-crash) ;
  - Hook d'autosave câblé dans `SimulationLoop.AdvanceOneTick` (`autoSaveEveryNTicks`, défaut 1000) — purement en écriture, aucun tirage du PRNG ⇒ checksums épinglés inchangés ;
  - 5 `PersistenceTests` (315 → **320 tests Core**, 329 au total avec Console : reprise bit-à-bit identique au run ininterrompu, état RNG, reprise post-crash, schéma 11 tables, rotation).
- **Jalon U8 — Serveur de contrôle HTTP :5181 (SYNE-113)** :
  - `ControlServer` (`HttpListener` BCL, zéro dépendance ADR-002/003) + `SimulationController` (machine à états `idle→running⇋paused→finished`, gate `ManualResetEventSlim` + `CancellationToken` par run) dans `Simulation.Console/Control/`, activés par `--serve` (`--serve-port`, défaut 5181) ;
  - routes `POST /api/control/start {seed?, config?}` / `pause` / `resume` / `reset {seed?, runId?}` + `GET /status` (contrat API_CONTRACTS §3) — réponses `{ok, action, runId, state, tick, aliveCount, seed}` ;
  - **non-intrusif** (SYNE-081/DETERMINISM §3) : 0 tirage PRNG ajouté ⇒ run piloté == run ininterrompu, vérifié bit-à-bit ; finalise l'interop ECHOS-085 (testé contre le vrai `ControlClient` ECHOS) ;
  - 6 `ControlServerWireTests` (320 Core + **15 Console = 335 tests** au total).

### Fixed
- Cadrage Jalon U8 (PR cadrage docs) : planche U8 du `ROADMAP` racine recalée sur le backlog réel — plage ECHOS `080…093` fictive remplacée par `080…085` (ph8, livrés U7) + `090…092` (ph9) ;
- Ajout de la carte manquante **SYNE-113** (serveur de contrôle HTTP :5181, cible réelle du relais `controlClient` ECHOS — absente du backlog) ;
- `SYNE-110` précisée : hooks `autoSaveEveryNTicks` (défaut 1000) / `maxBackups` (défaut 5) à brancher sur la boucle (aucun consommateur à ce jour) ;
- ECHOS-085 annotée : livrée côté UI, finalisation en U8 contre SYNE-113.
- `PERSISTENCE.md` §3 recalé sur le modèle réel V0.1 : `agent_snapshots` sans `health` fictive (besoins = `energy, hunger, thirst, fatigue`), implémentation du snapshot bit-à-bit et de la rotation documentée.

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
| 24 septembre 2026 | SYNE ph11 : persistance SQLite bit-à-bit (SYNE-110→112) + serveur de contrôle HTTP :5181 (SYNE-113) | Jalon U8 — tests & persistance |
| 24 septembre 2026 | SYNE ph11c : cycle des ressources — minéraux + régénération/dégradation (SYNE-070), engineVersion 0.7.0 | Jalon U8 — tests & persistance |
