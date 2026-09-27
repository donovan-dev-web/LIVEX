# CHANGELOG.md

**Composant** : LIVEX (général)
**Statut** : [DRAFT]
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : `VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`livex-vX.Y.Z` = triplet SYNE + ECHOS + PRISM).

## [Unreleased]

### Added
- Portail documentaire `docs/README.md` : sépare les documents de référence courants des archives (prototype, Monographie), et pose la hiérarchie des sources de vérité — en cas de divergence, la documentation détaillée et l'implémentation testée du composant priment.
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
- Job CI **`Tests (SYNE-MOCK Node)`** (`ci.yml`) : `syne-mock/` rejoint la détection de composants modifiés (sortie `syne_mock`) et la suite `npm test` s'exécute sur le runner à chaque PR touchant le composant, avec `timeout-minutes: 5` en garde-fou contre une régression de type fuite de descripteurs. `CI_CD.md` §2 annonçait déjà ce job.
- `docs/site/sync-docs.sh` publie `PRISM_UNREAL_IMPLEMENTATION.md` dans `articles/prism/`, et `docs/site/toc.yml` l'expose sous « Intégration Unreal (PRISM-LDK) ». Le guide d'intégration Blueprint du plugin n'était pas accessible sur le site DocFX alors qu'il est référencé par `ARCHITECTURE.md`.

### Changed
- `docs-pages.yml` se déclenche aussi sur `prism/**` : une modification du plugin ou du projet Unreal ne peut plus laisser le site sans rebuild, alors que la documentation contractuelle est écrite contre ce code.
- Dossier de documentation PRISM réécrit pour le projet Unreal final et son plugin PRISM-LDK : `world_initialized`, snapshot global par tick, deltas/événements et cycle de contrôle `prepare`/`ready`/`start`/`pause`/`resume`/`stop`/`reset` deviennent la référence du composant ; les instructions d'implémentation Godot sont remplacées par le périmètre réel du plugin, et les spécifications visuelles sont recadrées en objectifs de présentation (elles ne décrivent pas des fonctions déjà livrées).
- Décision d'architecture actée : **PRISM est le projet Unreal final de LIVEX** et intègre le plugin Unreal **PRISM-LDK** (*LIVEX Development Kit*, module technique `PrismLdk`). L'ADR-002 formalise ce choix et supersède l'ADR-001 (prototype Godot), désormais conservée comme décision historique. SYNE reste le seul moteur décisionnel et l'autorité de l'état simulé ; `prism/LDK/LDK.uproject` est un hôte technique de développement/build du plugin, pas un second produit.
- ADR-003 (API HTTP) et ADR-004 (WebSocket temps réel) précisent désormais que la liste des routes et des formats qu'elles portaient est celle de la proposition d'origine de la Monographie, et renvoient vers `docs/docs-syne/API_CONTRACTS.md` et `COMMUNICATION.md` pour le contrat courant (diffusion multi-consommateur, cycle `prepare/ready/start/pause/resume/stop/reset`).

- Documentation transverse réalignée sur PRISM, projet Unreal final de LIVEX : `README.md`, `ARCHITECTURE.md` (§2.4 `syne-mock`, §6 arborescence), `VISION.md`, `ROADMAP.md`, `FAQ.md`, `INSTALLATION.md`, `COMMUNICATION.md`, `SECURITY.md`, `CI_CD.md`, `CONTRIBUTING.md` et la Monographie. Les références à Godot et à `godot-renderer/` ne décrivent plus l'implémentation actuelle ; `ARCHITECTURE.md` devient la source de vérité pour les responsabilités, le plugin PRISM, LDK et les projets Unreal.
- Portée de `syne-mock` cadrée explicitement comme **outil de développement des contrats Unreal, pas un moteur de simulation** : `README.md`, `ARCHITECTURE.md` §2.4, `CONTRIBUTING.md` §4 (checklist `npm test --prefix syne-mock`), `CI_CD.md` §2 et §5, `docs/ETHICS_AND_SCOPE.md` §4.1 et `docs/docs-prism/`. Ses résultats ne doivent pas être présentés comme ceux de SYNE.
- `docs/docs-echos/{ISSUES,ROADMAP,TESTING}.md` : la validation `ECHOS-091` ne couvre plus « le runtime PRISM qui commence après U8 » mais est explicitement restreinte à SYNE→ECHOS. La validation du plugin PRISM-LDK et du projet Unreal PRISM relève d'un chantier distinct.
- `docs/docs_prototype/README.md` porte désormais un bandeau d'archive pointant vers `docs-prism/` et `docs/README.md` : les références à Godot qu'il contient sont historiques.
- `docs/site/index.md` : la carte PRISM annonce « Visualisation Unreal (plugin PRISM PrismLdk) » au lieu de « Visualisation (Godot) ».
- `CONTRIBUTING.md` : ajout de la validation de compilation du plugin PRISM-LDK dans l'hôte Unreal de développement **et** dans le projet PRISM final.

### Fixed
- `CHANGELOG.md` : la section `## [Unreleased]` cumulait des sous-sections dupliquées (`### Changed` ×3, `### Added` ×2) et une ligne blanche parasite coupant une liste en deux, ce qui cassait le rendu de la section et la comparaison d'un release. Sections regroupées et ordonnées `Added` → `Changed` → `Fixed` → `Deprecated`, avec correction de la coquille « L'ADR-002 *formality* ce choix » → « *formalise* ce choix ».
- Cadrage Jalon U8 (PR cadrage docs) : planche U8 du `ROADMAP` racine recalée sur le backlog réel — plage ECHOS `080…093` fictive remplacée par `080…085` (ph8, livrés U7) + `090…092` (ph9) ;
- Ajout de la carte manquante **SYNE-113** (serveur de contrôle HTTP :5181, cible réelle du relais `controlClient` ECHOS — absente du backlog) ;
- `SYNE-110` précisée : hooks `autoSaveEveryNTicks` (défaut 1000) / `maxBackups` (défaut 5) à brancher sur la boucle (aucun consommateur à ce jour) ;
- ECHOS-085 annotée : livrée côté UI, finalisation en U8 contre SYNE-113.
- `PERSISTENCE.md` §3 recalé sur le modèle réel V0.1 : `agent_snapshots` sans `health` fictive (besoins = `energy, hunger, thirst, fatigue`), implémentation du snapshot bit-à-bit et de la rotation documentée.
- `syne-mock` : `npm test` ne se terminait plus. `createServer().close()` arrêtait l'écoute sans libérer les ressources : les clients WebSocket restaient connectés et les connexions HTTP en keep-alive n'étaient pas fermées, ce qui laissait 2 serveurs TCP et 3 sockets ouverts et empêchait `node --test` de rendre la main. `close()` termine désormais les clients WebSocket, purge les connexions HTTP et chaîne les rappels de fermeture ; les tests libèrent leur serveur via `t.after()` et attendent la fermeture du client.
- `syne-mock` : les assertions d'obstacles attendaient un objet `{id, x, y, radius}` alors que `WorldDescription` publie aussi `type`. Les attentes sont alignées sur le contrat réel.
- `syne-mock` : les agents initiaux pouvaient apparaître sur une case non franchissable. `createInitialAgents` ne testait que les obstacles et ignorait le relief, donc un agent pouvait se poser sur une cellule `sea` (`walkable: false`) ; il contrôlait aussi la position non arrondie alors que le monde publie la position arrondie. Le placement s'appuie désormais sur `cells[].walkable`, l'indicateur que `WorldDescription` expose lui-même, ce qui traite obstacles et mer par la même règle que celle publiée et supprime la divergence entre le test de placement (`isPositionBlocked`, point contre cercle) et la grille (`isCellBlocked`, centre de case). Sur 300 graines × 5 densités × 2 configurations d'obstacles (324 600 positions), 3 452 agents sur case non franchissable avant, 0 après ; la génération reste déterministe pour une graine donnée. Test de non-régression ajouté sur 5 graines.

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
