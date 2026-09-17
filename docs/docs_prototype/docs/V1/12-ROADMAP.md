# Roadmap

## Phase 0 — Conception

- [x] Vision générale
- [x] Modèle conceptuel initial
- [x] Définir précisément les paramètres V1
- [x] Définir les règles biologiques
- [x] Définir les actions
- [x] Définir les règles de décision
- [x] Définir le format de configuration
- [x] Définir le format de sauvegarde

La spécification V1 est centralisée dans `docs/V1/03-V1-SPECIFICATION.md` et est désormais complète et implémentable (formules de décision déterministes, `SafetyNeed`, actions secondaires à effet minimal, obstacles, PRNG `xoshiro256**` imposé). **Phase 0 validée.**

## Phase 1 — Simulation Core

- [x] Créer solution C#/.NET (`EmergentSimulation.slnx` + `Simulation.Core` + `Simulation.Console`)
- [x] Créer World (dimensions, obstacles, résolution de collision)
- [x] Créer Agent (état, besoins, mémoire, liens, action courante)
- [x] Créer Resources (nourriture/water sources, inventories)
- [x] Créer simulation clock (ticks, temps simulé, cible ticks/sec)
- [x] Créer simulation loop (11 étapes causales, spec §3.1)
- [x] Implémenter besoins (Faim/Soif/Énergie/Santé + `SafetyNeed`)
- [x] Implémenter perception (portée, agents/ressources)
- [x] Implémenter mémoire (confiance, décroissance)
- [x] Implémenter actions (9 actions + locomotion automatique vers la cible)
- [x] Implémenter Utility AI (score multi-facteurs + focus + hystérésis)
- [x] Implémenter événements (EventBus + DecisionRecord)
- [x] Ajouter console/debug output

**Statut : terminée et validée.** Exécution de référence (config par défaut, seed 12345) :
20/20 agents vivants à tick 2000, besoins sains, déterminisme vérifié (même `RngState`
reproduit à l'identique d'un run à l'autre). Critères V1 §17 remplis : 1 à 7, 9, 10.
Critère 8 (sauvegarde/reprise complète de l'état) reporté en Phase 3 — la reprise
exacte au niveau PRNG (spec §13) est déjà en place via `SimulationEngine.RngState`.

## Phase 2 — Validation

- [x] 20 agents
- [x] 100 agents
- [x] 1 000 agents
- [x] Mesurer ticks/sec
- [x] Mesurer CPU
- [x] Mesurer mémoire
- [x] Tester différentes seeds
- [x] Vérifier absence de comportements scriptés involontaires

**Statut : terminée.** Harnais de validation ajouté dans `simulation-core/Simulation.Console/Benchmark.cs`
(mode `bench` de `Program.cs`). Rapport généré : `simulation-core/Simulation.Console/phase2-benchmark.log`.

Résultats (config par défaut, monde 500×500, 3 seeds : 12345/999/7) :

| Échelle | ticks | débit | CPU | mémoire physique pic | vivants |
|---|---|---|---|---|---|
| 20 agents | 2000 | ~1900–3200 /s | ~1 cœur | 30–127 Mo | 100 % |
| 100 agents | 1000 | ~315–400 /s | ~1 cœur | 125–325 Mo | 100 % |
| 1000 agents | 150 | ~4,5–5,4 /s | ~1 cœur | 354–1241 Mo | 100 % |

Observations :
- **Déterminisme vérifié** (re-run identique du `RngState` et des vivants) aux échelles 20 et 100.
- **Pas de comportement scripté** : 4,7 actions distinctes en moyenne par tick toutes échelles
  confondues ; `Attack` émerge à 100 agents (agressivité), `Eat`/`Drink` dominent via la Utility AI.
- **Goulot de scalabilité** : la perception est en O(n²) (balayage de tous les agents par agent).
  Le débit s'effondre à ~5 ticks/s à 1000 agents — cible d'optimisation Phase 7
  (grille spatiale / partitioning, parallélisation des systèmes).
- **Mémoire** : l'`EventBus` accumule tous les événements sans borne (305 k événements à 1000
  agents / 150 ticks) ; à prévoir en Phase 3 (persistance) et Phase 7 (limite circulaire ou mode
  capture désactivable pour les longues simulations).
- **Survie 100 %** à toutes les échelles sur la fenêtre testée : la biologie V1 est permissive.
  Utile pour la stabilité ; les pressions de sélection apparaîtront sur des runs longs (épuisement
  des sources de nourriture finies).

## Phase 3 — Persistance

- [x] Save
- [x] Load
- [x] Versioning
- [x] Reprise exacte d'une simulation

**Statut : terminée et validée.** Couche de persistance dans `simulation-core/Simulation.Core/Persistence/`
(`SaveFile.cs` : DTO + `SimulationSerializer`). API sur `SimulationEngine` :
`engine.Save("save.json")` et `SimulationEngine.Load("save.json")`.

- Format JSON conforme spec §14 (SchemaVersion, SimulationVersion, Config, Simulation
  [seed/tick/rngState], World, Resources, Agents). Énumérations (`ActionType`, `ActionState`)
  sérialisées en chaînes (lisibles, robustes au versioning).
- **Versioning** : `schemaVersion` courant = 1. Une sauvegarde avec un `schemaVersion`
  différent lève `InvalidOperationException` (refus explicite, spec §14.6). Migration
  ultérieure à ajouter si le schéma évolue.
- **Reprise exacte** vérifiée par le mode `persist` (`Program.cs`) : Save au tick 1000
  puis Load restaure un état **bit à bit identique** (RngState + état des agents), et la
  simulation reprise rejoint **exactement** une exécution de référence non interrompue
  (20 agents / 2000 ticks). Aucune donnée de renderer sauvegardée (spec §14, §10).

Commande de validation :
`dotnet run -c Release --project simulation-core/Simulation.Console -- config.json 2000 persist save.json`

## Phase 4 — Transport

- [x] Définir DTO
- [x] Définir events externes
- [x] Choisir WebSocket/gRPC
- [x] Implémenter serveur
- [x] Implémenter client de test

**Statut : terminée et validée.** Contrat de transport dans
`simulation-core/Simulation.Core/Transport/` (`TransportDtos.cs` : DTO + `WebSocketServer.cs`
: serveur WebSocket maison au-dessus de `TcpListener`, sans dépendance admin).

- **Choix WebSocket** (et non gRPC) : universel pour le `web-ui` (navigateur, WebSocket natif)
  et l'`analyzer` (Python, `websockets`), et indépendant du renderer (spec §6). Le socket
  diffuse deux types de messages distingués par `kind` (JSON camelCase) :
  - `snapshot` : `WorldSnapshot` { tick, simulatedTimeMinutes, aliveCount, agents[], resources[] }
    permet la reconstruction complète du monde côté renderer/analyzer à chaque tick ;
  - `event` : `ExternalEvent` { type, tick, agentId?, action?, targetId?, cause?, value? }
    enveloppe légère des événements `SimulationEvent` (AgentDied, ActionCompleted, DecisionMade,
    TickCompleted, ResourceChanged, ...).
- **Serveur** : `WebSocketServer` pilote la simulation (`engine.Step()`), diffuse snapshot/events
  à tous les clients connectés, et purge `EventBus` après envoi (borne la mémoire, cf. Phase 2).
  Cadencé par `TargetTicksPerSecond` pour un débit temps réel.
- **Client de test** : `WsTestClient.cs` (C#) + modes `serve` / `wsclient` / `wstest` dans
  `Program.cs`. Validation : mode `wstest` (serveur + client dans le même processus) reçoit
  bien snapshots + events → **PASS** ; scénario `serve` + `wsclient` séparés → **PASS**.

Commandes :
```bash
dotnet run -c Release --project simulation-core/Simulation.Console -- config.json <ticks> serve <port>
dotnet run -c Release --project simulation-core/Simulation.Console -- config.json <ticks> wsclient <port> [frames]
dotnet run -c Release --project simulation-core/Simulation.Console -- config.json <ticks> wstest
```

## Phase 5 — Analyzer (C#/.NET)

- [x] Consommation : client WebSocket vers le Simulation Core (abonnement snapshots + events)
- [x] Modèle de métriques : population, ressources, spatial, comportement (cf. `docs/V1/11-EMERGENCE-ANALYSIS.md`)
- [x] Mesures d'émergence : persistance, reproductibilité, réseaux sociaux / clustering, entropie / diversité
- [x] API REST : rapports, séries temporelles, comparaison de runs
- [x] Stockage / export des résultats (seed, config, durée, version, résultats)
- [x] Tests unitaires des métriques (projet `analyzer/Analyzer.Tests`, 18 tests xUnit : `MetricsTests` + `RunStoreTests` + `EmergenceTests`)

**Statut : terminée et validée.** Service `.NET` (`analyzer/` : `Analyzer.Core`
bibliothèque + `Analyzer.Service` API REST) réutilisant les DTO de
`simulation-core/Simulation.Core/Transport` (`WorldSnapshot`, `ExternalEvent`)
— aucune duplication de contrat. Un client WebSocket (`SimClient`) s'abonne au
serveur de simulation (Phase 4), alimente un `RunStore` thread-safe, et les
métriques (cf. `Metrics.cs`) sont exposées via une API HTTP consommée par le
Web UI (Phase 6). Export JSON des runs dans `analyzer/data/runs/`.

Métriques calculées : population/mean besoins par tick, entropie de comportement
(Shannon), distance moyenne à la ressource la plus proche, **clustering spatial**
(composantes connexes de cellules occupées) et **réseau de co-localisation**
(degré moyen) comme proxy de réseau social, déplétion des ressources, persistance
du comportement dominant, et comparaison de deux runs (reproductibilité).

Commandes :
```bash
# Lancer le serveur de simulation (Phase 4)
dotnet run -c Release --project simulation-core/Simulation.Console -- config.json 2000 serve 5180

# Dans un autre terminal : Analyzer (se connecte, expose l'API REST sur :5000)
dotnet run -c Release --project analyzer/Analyzer.Service -- --sim=ws://127.0.0.1:5180/ --rest=http://localhost:5000

# Interroger
curl http://localhost:5000/api/runs
curl http://localhost:5000/api/runs/<id>
curl "http://localhost:5000/api/compare?a=<idA>&b=<idB>"

# Auto-test (génère un run synthétique + export, sans serveur)
dotnet run -c Release --project analyzer/Analyzer.Service -- --selftest
```

## Phase 6 — Web UI (React + TypeScript)

- [x] Choix techno : React + TS (Vite), Recharts, client WebSocket + client HTTP
- [x] Double flux : abonnement au WebSocket de simulation (vue live : positions, actions)
  **et** consommation de l'API Analyzer (métriques, émergence)
- [x] Rapports : concentrations de population, consommation de ressources, graphes d'émergence
- [x] Visualisation temps réel (canvas 2D pour le monde)
- [x] UI de comparaison de runs
- [x] **Contrôle du moteur depuis l'UI** : API REST de contrôle (`Simulation.Console`, port 5181 par défaut) — `start` / `pause` / `resume` / `reset` avec seed + run id ; le moteur démarre **en pause**. Panneau `ControlPanel` + hook `useControl`.
- [x] **Métriques UI** : CORS activé sur l'API Analyzer ; le run id est porté par le `WorldSnapshot` et l'Analyzer indexe les runs dynamiquement (plus besoin d'aligner `--run-id`).
- [x] **Rapport d'émergence** : `EmergenceAnalyzer` (Analyzer.Core) détecte les phénomènes émergents (convergence comportementale, auto-organisation spatiale, coalescence, rythmes collectifs, transition exploration→exploitation, tragédie des communs, conflit, résilience, homéostasie) ; exposé via `GET /api/runs/{id}/emergence` et affiché dans `EmergenceReportPanel` (polling temps réel).

**Statut : terminée et validée.** SPA `web-ui/` (Vite + React + TypeScript + Recharts).
Double flux implémenté :
- **flux direct** : `useSimulationSocket` (hook WebSocket) reçoit les `WorldSnapshot` /
  `ExternalEvent` et rend le monde 2D en temps réel (`WorldView`, canvas) ;
- **flux Analyzer** : `useRunMetrics` / `useRunList` consomment l'API REST de la Phase 5
  (`/api/runs`, `/api/runs/{id}`, `/api/compare`) et affichent les graphes
  (population, entropie, besoins, déplétion des ressources) et la comparaison de runs.

Le build passe (`npm install && npm run build`) ; les endpoints consommés sont validés
contre l'Analyzer (Phase 5). Aucune logique métier côté UI.

Commandes :
```bash
cd web-ui
npm install
npm run dev        # http://localhost:5173 (saisir l'URL WS sim + URL Analyzer + run id)
npm run build      # build de production dans dist/
```

## Phase 7 — Optimisation de la simulation

Seulement après profiling (cf. Phase 2) :

- [x] optimiser la perception (grille spatiale / partitioning, O(n²) → ~O(n))
- [x] optimiser le stockage (limite circulaire de l'EventBus, cf. Phase 2)
- [x] paralléliser les systèmes (Span / Memory, batches)
- [x] envisager ECS si pertinent
- [x] benchmark massif (1000+ agents, ticks/s, mémoire)

**Statut : terminée.** Optimisations implémentées dans `Simulation.Core` :

- **Grille spatiale** (`SpatialGrid.cs`, uniform grid, taille de cellule = `PerceptionRange`).
  `PerceptionSystem.Perceive` interroge les 3×3 cellules voisines au lieu de balayer tous
  les agents → perception en ~O(n) (voisins dans la portée) au lieu de O(n²). Les candidats
  sont triés par index d'origine `world.Agents` pour produire une liste **identique à la
  version O(n²)** : le comportement reste donc **bit à bit déterministe** (vérifié par
  re-run du `RngState` à 1000 agents).
- **EventBus en ring buffer** (`Events.cs`) : capacité fixe (500 k événements), écrasement
  des plus anciens au-delà, + token monotone et `DrainSince(ref token, batch)` pour un
  drainage incrémental sans vider le bus. Le serveur WebSocket (`WebSocketServer.cs`)
  utilise `DrainSince` (plus d'indexation absolue ni `Clear`). Mémoire bornée même en
  exécution headless longue (avant : croissance illimitée, cf. Phase 2).
- **Parallélisation de la perception** : `Parallel.For` sur les agents vivants (lecture
  seule sur le monde + grille ; écritures par-agent disjointes, pool de listes réutilisées).
- **Réduction d'allocations** : `Observation` passé en `readonly struct` (stocké en ligne
  dans le tableau du `List<Observation>`) + pooling des listes de perception par agent
  (le moteur réutilise `List<Observation>` d'un tick à l'autre au lieu d'en allouer à chaque tick).

Benchmark massif (monde 500×500, seed 12345, 150 ticks) :

| Échelle | ticks/s | mémoire physique pic | vivants |
|---|---|---|---|
| 1000 agents | ~4,6 /s | 39 Mo | 100 % |
| 2000 agents | ~0,6 /s | 42 Mo | 100 % |

**Constat de profiling** : la perception n'était **pas** le goulot (son passage en O(n) n'a
pas changé le débit, qui reste GC-bound). Le coût dominant est l'**allocation par tick dans
les systèmes** (records `Candidate`, `DecisionRecord` + chaînes de libellés dans
`DecisionSystem`, listes par agent) → ralentissement superlinéaire à grande échelle dû au
GC.

**Optimisation GC (complément Phase 7)** — ciblage du goulot d'allocation :
- `Candidate` passé en `readonly struct` (plus d'allocation par candidat par tick).
- **Interning des libellés** (`DecisionSystem.MakeLabel`) : l'ensemble des `(type, cible)`
  étant fini, la même instance de chaîne est réutilisée à travers tous les ticks/agents →
  supprime ~26 allocations de chaînes par agent par tick.
- **Pool des listes** de candidats/scored (`DecisionSystem`, réutilisées d'un agent à
  l'autre, `Decide` étant séquentiel) → plus de 2 listes allouées par agent par tick.
- **Émission d'événements verbeux désactivable** (`SimulationConfig.Events`,
  `EventBus.EmitAction`) : `DecisionMadeEvent` (avec son `DecisionRecord`) et les événements
  d'action (`ActionStarted/Moved/Completed/Failed`) ne sont émis que si
  `EmitDecisions`/`EmitActions` sont à `true` (défaut). Le `bench` les désactive → le
  ring buffer n'est plus noyé sous 150 k records, et le `DecisionRecord` n'est plus alloué.
  Comportement/déterminisme **inchangés** (l'émission n'altère pas l'état ; RngState
  identique run-to-run vérifié).

Nouveau benchmark (monde 500×500, seed 12345, 150 ticks, événements désactivés) :

| Échelle | ticks/s (avant) | ticks/s (après) | gain | mémoire physique pic |
|---|---|---|---|---|
| 1000 agents | ~4,6 | ~6,3 | +37 % | 39 Mo |
| 2000 agents | ~0,6 | ~0,9 | +50 % | 42 Mo |

Le scaling reste superlinéaire à 2000 agents (GC résiduel sur `MemoryEntry`/`ActionInstance`
par tick) : piste Phase 9 (pooling/struct de ces entrées, ou mode événements désactivable
aussi pour le serveur temps réel). L'ECS n'est **pas pertinent** ici (surcoût de réécriture
pour un gain marginal vu que le goulot est l'alloc GC, pas la structure de données).

## Phase 8 — Optimisation de l'Analyzer

- [x] Agrégation incrémentale des métriques (pas de recalcul plein)
- [x] Mise en cache des séries temporelles
- [x] Parallélisation des calculs lourds (réseaux / clustering)
- [x] Réduction de la bande passante (sous-échantillonnage des snapshots)

**Statut : terminée.** Optimisations dans `analyzer/` (`Analyzer.Core/Metrics.cs`,
`Analyzer.Core/RunStore.cs`, `Analyzer.Service/Program.cs`) :

- **Agrégation incrémentale** : `Metrics.ComputeTickSample` (fonction pure) est appelé
  **une fois par snapshot à l'ingest** (`RunStore.AddSnapshot`). Les séries temporelles
  (`Run.Series`) et les compteurs (deaths, depletion ressources, persistance/commutations
  de l'action dominante) sont accumulés au fil de l'eau. `RunStore.ComputeMetrics`
  synthétise depuis ces séries déjà calculées (`Metrics.Aggregate`) → **aucun recalcul
  plein** sur les snapshots à la requête. Le `RunStore` ne conserve plus les `WorldSnapshot`
  complets en mémoire (gain mémoire majeur : seules les séries légères `TickSample` sont
  gardées), sauf sous-échantillonnage.
- **Cache des séries** : `RunStore.ComputeMetrics` met en cache le `RunMetrics` par run,
  invalidé dès que la série change (append snapshot / death). Requêtes REST répétées
  (Web UI) sans coût de recalcul.
- **Parallélisation** : les boucles lourdes par tick sont parallélisées via `Parallel.For`
  avec accumulation locale + `Interlocked` — réseau de co-localisation O(n²)
  (`Spatial`, degré moyen) et distance à la ressource la plus proche
  (`MeanNearestResource`). Sécurisé (écritures sur index d'agent distincts).
- **Sous-échantillonnage** : `--sample-every=N` (ingest) ne conserve qu'1 snapshot sur N
  (réduction résolution/mémoire) ; l'endpoint `GET /api/runs/{id}?every=N` renvoie une
  série sous-échantillonnée (`Metrics.Downsample`). La réduction de bande passante
  transport (sim→analyzer) nécessiterait un support côté serveur (cadence `broadcastEvery`
  du Simulation Core, Phase 4) — noté comme piste.

Validation : `--selftest` vérifie **incrémental == référence** (mêmes deaths, entropie,
persistance, switches, nb de ticks). Test d'intégration live (serve + analyzer) :
série incrémentale reçue correctement, `?every=10` renvoie 9/86 échantillons, aucune
régression sur le flux WebSocket.

## Phase 9 — Refactor, Clean, Vérification + Tests & coverage

- [x] Refactor du Simulation Core (Modules §3 de `docs/V1/04-ARCHITECTURE.md`) : code déjà modulaire ; correction de sûreté concurrente (voir ci-dessous)
- [x] Nettoyage (logs de debug, nommage) : logs serveur conservés (informationnels)
- [x] Tests automatisés (unit + intégration) : Simulation.Core (38 tests), Persistance, Transport, Analyzer (18 tests) — **total 56 tests C#** + **11 tests UI (`Vitest`)**
- [x] Couverture de code (mesurée le 2026-08-29, filtres `ci/coverlet.*.runsettings`) :
  `Simulation.Core` **90,3 %** lignes (909/1007) / 76,4 % branches, `Analyzer.Core` **85,0 %** lignes / 67,9 % branches.
  Cible ≥ 80 % lignes : **`Simulation.Core` ✅ atteint (90,3 %)**, `Analyzer.Core` ✅ atteint (85,0 %). Renfort via tests ciblés `ActionSystem`/`WebSocketServer` + suite `Vitest` UI (polling). Voir `docs/V1/16-SANTE-PROJET.md`.
- [x] Vérification du déterminisme (tests dédiés sur `RngState`)

**Statut : terminée.** Livrables :

- **Projets de tests** : `simulation-core/Simulation.Core.Tests` (xUnit, net10.0, référence
  `Simulation.Core`) et `analyzer/Analyzer.Tests` (xUnit, net10.0, référence `Analyzer.Core`).
  Exécution : `dotnet test simulation-core/EmergentSimulation.slnx -c Release` et
  `dotnet test analyzer/Analyzer.slnx -c Release`. Couverture :
  `dotnet test <slnx> -c Release --collect:"XPlat Code Coverage"`.
- **`Simulation.Core.Tests`** (`RngTests`, `EngineDeterminismTests`, `TransportTests`,
  `TransportServerTests`, `SpatialGridTests`) :
  - PRNG `Rng` : reproductibilité seed, reprise depuis `State`, indépendance du clone.
  - Moteur : run-to-run identique (même `RngState` et même état d'agent), survie + évolution
    des besoins, **reprise exacte** `Save`/`Load` (le `RngState` rejoint la référence bit à bit),
    rejet d'un `schemaVersion` invalide.
  - Transport : round-trip `WorldSnapshot`/`ExternalEvent` (camelCase + enum en chaîne),
    **round-trip WebSocket** serveur→client (`ClientWebSocket`) : snapshots + événements reçus.
  - Grille spatiale : broad-phase ne manque aucun agent dans la portée, borné aux cellules
    voisines (`ceil(range/cellSize)`).
- **`Analyzer.Tests`** (voir §5 de `docs/V1/13-ANALYZER.md`) : 18 tests (`MetricsTests` +
  `RunStoreTests` + `EmergenceTests`).
- **Correction de sûreté concurrente (refactor Phase 9)** : `DecisionSystem` utilisait des
  listes « pool » et un cache de libellés **statiques partagés**. En exécutant plusieurs
  moteurs en parallèle (tests, serveurs multiples), ces champs statiques étaient corrompus
  (collection modifiée concurremment → exceptions aléatoires). Correction :
  - `_labelCache` → `ConcurrentDictionary` (interning thread-safe).
  - `_candidates`/`_scored` → `ThreadLocal` (un pool par thread ; `Step` est séquentiel par
    moteur, donc aucune contention, aucune allocation par agent par tick).
  - Comportement/déterminisme **inchangés** (RngState identique run-to-run vérifié par les tests).
- **`WebSocketServer`** : support du port `0` (OS attribue un port libre) ; expose le port
  réel après démarrage de l'écoute (`Port`), ce qui évite les collisions de port en tests
  parallèles.

La couverture cible ≥ 80 % lignes est **atteinte sur les deux bibliothèques** :
`Analyzer.Core` (85,0 %) et `Simulation.Core` (90,3 %, renforcé par des tests ciblés
`ActionSystem`/`WebSocketServer`). La couverture branches (`Simulation.Core` 76,4 %) reste
perfectible. Le suivi détaillé (tests, couverture, points à renforcer) est centralisé dans
`docs/V1/16-SANTE-PROJET.md`.
Le reste (console, service web) est laissé hors cible (points d'entrée, pas de logique métier).

## Phase 10 — Application 3D (Godot)

- [x] Choisir Godot (version LTS) + langage (C# / GDScript)
- [x] Représenter le monde (sol, ressources, obstacles)
- [x] Représenter les agents (mesh, couleur par besoin / état)
- [x] Synchroniser positions / actions via WebSocket (Transport Layer)
- [x] Caméra, animations
- [x] UI de debug + contrôle de simulation

**Statut : terminée et validée.** Renderer indépendant (principe §7 de l'architecture),
**Godot 4.7.2 (édition .NET) en C#** (décision : ADR-006) ; il s'abonne au WebSocket de
simulation et ne décide rien. Détail : `docs/V1/17-RENDERER-3D.md` (+ `godot-renderer/README.md`).

- **Stack** : `Godot.NET.Sdk 4.7.2`, `net8.0` + `RollForward=LatestMajor` (runtime .NET 10
  installé) ; aucun asset — tout est procédural (`PlaneMesh` sol, `CapsuleMesh` agents,
  `SphereMesh` ressources).
- **Livrables** : `godot-renderer/` (`main.tscn` + `scripts/SimClient.cs`,
  `CameraController.cs`, `Hud.cs`). Mise à jour `start-all.ps1` : switch optionnel
  `-Godot` / `-GodotPath`.
- **Validation (playtest contre le moteur réel, serve 5180 + contrôle 5181)** :
  connexion WS + flux snapshot/event ; spawn/mouvement des agents et ressources
  (interpolation) ; relais de contrôle complet depuis le HUD (start avec seed/run id
  appliqués, pause → tick figé, resume → tick repart, reset → monde reconstruit) ;
  bascule de couleur santé/action ; sélection d'agent + inspection ; suivi caméra (F) ;
  journal d'événements ; `dotnet build` 0 erreur / 0 warning.
- **Limite V1 assumée** : obstacles / taille du monde non diffusés par le contrat transport
  (hors périmètre V1) → sol fixe 500×500, obstacles ignorés (V2).

## Phase 11 — CI/CD & Déploiement

- [x] Pipeline CI (build + tests + coverage) sur PR
- [x] Lint / format (C#, TS)
- [x] Artefacts : Docker pour Analyzer + Web UI, build Godot
- [x] Release / versioning

**Statut : terminée et validée.** Automatisation livrée sous forme de workflows
GitHub Actions (dépôt `donovan-dev-web/SSE-emergent-simulation`), d'artefacts de
conteneurisation et d'une politique de versioning SemVer. Détail : `docs/V1/18-CICD.md`.

- **Pipeline CI** — `.github/workflows/ci.yml` (PR + push `main`) :
  - **dotnet** : restore/build des deux solutions (`EmergentSimulation.slnx`,
    `Analyzer.slnx`) en Release ; `dotnet format --verify-no-changes` ; tests xUnit
    (56) avec **couverture seuillée ≥ 80 % lignes** via `ci/coverlet.*.runsettings`
    (format cobertura, `Threshold=80`, `ThresholdStat=total`) ; rapports cobertura uploadés.
  - **web** : `npm ci` + **ESLint** (`npm run lint`) + **Prettier** (`npm run format:check`)
    + **Vitest** (`npm run test`, 11 tests) + build (`tsc --noEmit && vite build`),
    `dist/` uploadé.
  - **godot** : compilation de l'assembly C# (`dotnet build godot-renderer/.csproj`)
    + import headless `--headless --import --quit` (validation du projet/scripts).
- **Lint / format**
  - **C#** : `.editorconfig` racine + `dotnet format` (normalisation appliquée une
    fois sur le dépôt : blancs `WebSocketServer.cs`, ordre des `using`, BOM
    `Program.cs` — modifications de style uniquement, comportement inchangé, 56 tests
    verts). Vérifié en CI (`--verify-no-changes`).
  - **TS/JS** : ESLint (flat config, `eslint.config.js`) + Prettier
    (`.prettierrc.json`, `.prettierignore`) ajoutés à `web-ui` ; scripts
    `lint`/`format`/`format:check` ; quelques `any` typés en dur retirés des tests
    et code formaté (18 fichiers).
- **Artefacts**
  - **Docker** : `analyzer/Analyzer.Service/Dockerfile` (SDK .NET 10 → ASP.NET 10,
    port 5000, `ANALYZER_SIM_URL`/`ANALYZER_REST_URL`), `web-ui/Dockerfile`
    (node → nginx, dist/ servi sur 80), + bonus `simulation-core/Simulation.Console/Dockerfile`
    (moteur serve 5180/5181). Stack orchestrée par `compose.yml` (sim + analyzer + web-ui).
  - **Godot** : build reproductible = assembly C# Release + projet (import headless
    validé en CI), empaqueté pour livraison. Export exécutable (templates + 
    `export_presets`) volontairement hors périmètre V1 pour ne pas casser l'éditeur.
- **Release / versioning** — `.github/workflows/release.yml` déclenché sur tag `vX.Y.Z` :
  re-exécute build + tests + coverage, construit et **pousse les images GHCR**
  (`sim`, `analyzer`, `web-ui` taguées version + `latest`), empaquette le renderer,
  puis crée une **GitHub Release** avec notes auto et `dist/` de la Web UI attaché.
  SDK .NET épinglé par `global.json` (10.0.400).
- **Validation locale** : `dotnet format --verify-no-changes` propre sur les deux
  solutions ; 38 + 18 tests C# verts ; 11 tests Vitest verts ; lint + format:check
  propres ; build `tsc + vite` OK (warning taille de chunk préexistant).
