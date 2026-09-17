# Emergent Simulation Engine

Moteur de simulation systémique émergente, persistante et temps réel. Des agents
prennent des décisions à partir de leur état interne, de leurs besoins, de leur
perception, de leur mémoire et de leurs capacités — **aucun scénario global
n'impose les comportements émergents**.

Le moteur est découplé de toute représentation graphique : il expose son état via
un *Transport Layer* (WebSocket) consommé par un analyzer (métriques d'émergence)
et des interfaces (Web, 3D).

Le projet est organisé en **deux générations** :

| Génération | Statut | Cible | Docs |
|-----------|--------|-------|------|
| **V1** | ✅ **Implémentée** (Phases 0–11) | ~50 agents, Utility AI, persistance JSON | `docs/V1/` |
| **V2** | 📐 **Spécifiée** (réfonte BDI) | 500–1000 agents, cognition BDI complète, SQLite | `docs/V2/` |

La **V1** est l'implémentation actuelle, exécutable et testée. La **V2** est la
refonte majeure vers une plateforme de simulation persistante multi-agent BDI,
spécifiée en détail (docs `/V2`) mais **non encore implémentée** — voir la
section [V2 / évolutions](#v2--évolutions) plus bas.

---

# V1 — Implémentation courante

## Architecture

```text
Simulation Core (C# / .NET)
        |
   Transport Layer (WebSocket)
        |
        +---- WebSocket ----> 3D Renderer (Godot, Phase 10)
        |
        +---- WebSocket ----> Analyzer (.NET) --(REST API)--> Web UI (React + TS, Phase 6)
                                       |
                                       +-- la Web UI est aussi abonnée au WebSocket (double flux)
```

| Composant            | Dossier            | Rôle                                                        | État        |
|----------------------|--------------------|-------------------------------------------------------------|-------------|
| Simulation Core      | `simulation-core/` | Moteur de simulation, logique métier (C#)                   | Phases 1–4, 7, 9 |
| Analyzer             | `analyzer/`        | Détection d'émergence, métriques, API REST                  | Phases 5, 8, 9 |
| Web UI               | `web-ui/`          | Rapports, visualisation temps réel, comparaison de runs     | Phase 6     |
| 3D Renderer (Godot)  | `godot-renderer/`  | Scène 3D, visualisation, caméra, debug                      | Phase 10    |
| CI/CD                | `ci/`              | Pipeline build/tests/coverage, artefact, release            | Phase 11    |

## Fonctionnalités (V1)

- Simulation autonome déterministe (PRNG `xoshiro256**`, reprise exacte).
- Agents : besoins (faim/soif/énergie/santé/sécurité), perception locale, mémoire, Utility AI.
- Actions exécutables et interruptibles, monde persistant.
- Transport réseau (WebSocket) et persistance (save/load JSON versionné).
- Analyzer : métriques de population, ressources, spatial, comportement, et mesures d'émergence.
- Web UI : vue live (canvas) + rapports (graphes) en double flux.

## Stack technique

- **C# / .NET 10** — Simulation Core et Analyzer (mono-repo 100 % .NET).
- **WebSocket** — Transport Layer (serveur `TcpListener` maison, sans privilège admin).
- **React 18 + TypeScript (Vite)** — Web UI, charting via Recharts.
- **Godot 4.7.2 (édition .NET) + C#** — renderer 3D procédural sans asset (Phase 10, ADR-006).
- Python non retenu (cohérence de toolchain) ; réservé aux calculs scientifiques lourds.

## Structure du dépôt

```text
simulation-core/               # Simulation Core (C# / .NET)
├── EmergentSimulation.slnx    # solution .NET 10
├── Simulation.Core/           # moteur (bibliothèque)
└── Simulation.Console/        # exécutable de test / observabilité + modes serve/wsclient
analyzer/                      # Analyzer (.NET) : Analyzer.Core + Analyzer.Service (API REST)
web-ui/                        # Web UI (React + TypeScript)
godot-renderer/                # Renderer 3D (Godot 4.7.2 édition .NET, C#) — Phase 10
docs/                          # documentation (spécification V1 centralisée)
start-all.ps1 / start-all.sh   # lanceur unique (sim + analyzer + web ui [+ godot optionnel])
```

Les artefacts de build (`bin/`, `obj/`, `node_modules/`, `dist/`, `analyzer/data/`)
sont ignorés via `.gitignore`.

## Tests & couverture

Les tests automatisés (Phase 9) couvrent le déterminisme, la persistance, le transport et
l'analyzer. Couverture cible ≥ 80 % (atteinte sur les deux cœurs métier).

```bash
# Simulation Core (Rng, moteur, persistance, transport WebSocket, grille spatiale)
dotnet test simulation-core/EmergentSimulation.slnx -c Release

# Analyzer (métriques + agrégation incrémentale)
dotnet test analyzer/Analyzer.slnx -c Release

# Couverture (Cobertura)
dotnet test simulation-core/EmergentSimulation.slnx -c Release --collect:"XPlat Code Coverage"
dotnet test analyzer/Analyzer.slnx -c Release --collect:"XPlat Code Coverage"
```

## Démarrage rapide

### Prérequis

- **.NET 10 SDK**
- **Node.js 18+** (pour la Web UI)

### Lancer tout en une commande

Le lanceur démarre la simulation (serveur WebSocket), l'Analyzer (client WebSocket +
API REST) et la Web UI (dev server) en parallèle, puis affiche les URLs :

```bash
# Windows
.\start-all.ps1

# Linux / macOS
./start-all.sh
```

Ports par défaut : simulation `5180`, analyzer `5000`, Web UI `5173`.
Run id de l'analyzer : `demo`. Ouvrez **http://localhost:5173**, saisissez
`demo` dans le champ *Run id*, et la vue live + les rapports s'affichent.

Options (PowerShell) : `-Ticks 100000 -SimPort 5180 -AnalyzerPort 5000 -WebPort 5173 -RunId demo`.
Pour lancer aussi le **renderer 3D Godot** (optionnel) : `-Godot` , avec éventuellement
`-GodotPath "C:\...\Godot_v4.7.2-stable_mono_win64.exe"` (si le binaire n'est pas sur le
PATH). Voir `godot-renderer/README.md`.
Linux/macOS : variables d'environnement `SIM_PORT`, `ANALYZER_PORT`, `WEB_PORT`, `RUN_ID`
ou argument positionnel `./start-all.sh 100000`.

### Démarrage manuel

```bash
# 1) Serveur de simulation (WebSocket sur :5180)
dotnet run -c Release --project simulation-core/Simulation.Console -- config.json 2000 serve 5180

# 2) Analyzer (client WS + API REST sur :5000)
dotnet run -c Release --project analyzer/Analyzer.Service -- --sim=ws://127.0.0.1:5180/ --rest=http://localhost:5000 --run-id=demo

# 3) Web UI (Vite dev server sur :5173)
cd web-ui && npm install && npm run dev
```

Interroger l'Analyzer :

```bash
curl http://localhost:5000/api/runs
curl http://localhost:5000/api/runs/demo
curl "http://localhost:5000/api/compare?a=demo&b=demo"
```

### Configuration

Tous les paramètres sont dans `simulation-core/Simulation.Console/config.json` :
`simulation.seed`, `simulation.ticks`, `world`, `population`, `agent`, `biology`,
`resources`, `decision` (voir `docs/V1/03-V1-SPECIFICATION.md`).

### Persistance (reprise exacte, PRNG inclus)

```bash
# Valider save/load (reprise bit-à-bit identique)
dotnet run -c Release --project simulation-core/Simulation.Console -- config.json 2000 persist save.json
# En code : engine.Save("save.json") / SimulationEngine.Load("save.json")
```

### Transport & contrat

La simulation diffuse, via WebSocket, deux messages JSON (`camelCase`) :
`{"kind":"snapshot", "tick":N, "simulatedTimeMinutes":M, "aliveCount":N, "agents":[...], "resources":[...]}`
et `{"kind":"event", "type":"...", "tick":N, "agentId":..., ...}`.
Voir `docs/V1/09-EVENTS-API.md` et `simulation-core/Simulation.Core/Transport/`.

## Roadmap

| Phase | Sujet                                 | État        |
|-------|---------------------------------------|-------------|
| 0     | Conception                            | ✅          |
| 1     | Simulation Core                       | ✅          |
| 2     | Validation (benchmark)                | ✅          |
| 3     | Persistance                           | ✅          |
| 4     | Transport (WebSocket)                 | ✅          |
| 5     | Analyzer (C#/.NET)                    | ✅          |
| 6     | Web UI (React + TS, double flux)      | ✅          |
| 7     | Optimisation de la simulation         | ✅          |
| 8     | Optimisation de l'Analyzer            | ✅          |
| 9     | Refactor / Tests / Coverage           | ✅          |
| 10    | Application 3D (Godot)                | ✅          |
| 11    | CI/CD & Déploiement                   | ✅          |

Détails : `docs/V1/12-ROADMAP.md`.

## Documentation (V1)

- [Vision et principes](docs/V1/01-VISION.md)
- [Modèle conceptuel](docs/V1/02-CONCEPTUAL-MODEL.md)
- [Spécification fonctionnelle V1](docs/V1/03-V1-SPECIFICATION.md)
- [Architecture](docs/V1/04-ARCHITECTURE.md)
- [Agents](docs/V1/05-AGENTS.md) · [Actions](docs/V1/06-ACTIONS.md) · [Système de décision](docs/V1/07-DECISION-SYSTEM.md)
- [Boucle de simulation](docs/V1/08-SIMULATION-LOOP.md) · [Événements et API](docs/V1/09-EVENTS-API.md)
- [Persistance](docs/V1/10-PERSISTANCE.md) · [Analyse de l'émergence](docs/V1/11-EMERGENCE-ANALYSIS.md)
- [Roadmap](docs/V1/12-ROADMAP.md) · [Analyzer (Phase 5)](docs/V1/13-ANALYZER.md) · [Web UI (Phase 6)](docs/V1/14-WEB-UI.md)
- [Renderer 3D (Phase 10)](docs/V1/17-RENDERER-3D.md) · [Contribution](CONTRIBUTING.md)

---

# V2 / évolutions

La **V2** est une **refonte majeure, spécifiée mais non encore implémentée**, vers une
plateforme de simulation persistante de type jeu vidéo 3D, supportant **500–1000 agents
autonomes** avec une **cognition BDI complète** (perception, mémoire, croyances, besoins,
objectifs, délibération, intention) et une **observabilité partielle** (les agents ne
perçoivent pas l'état complet du monde).

Changements structurants V1 → V2 :

| Aspect | V1 | V2 |
|--------|----|----|
| Agents | ~50 agents simples | 500–1000 agents BDI |
| Prise de décision | Utility AI (règles) | BDI + scoring d'utilité multidimensionnel |
| Modèle du monde | Agents omniscients | Observabilité partielle |
| Communication inter-agents | Absente | Messages structurés, rayon local |
| Persistance | Fichiers JSON | SQLite (ACID) |
| Émergence mesurée | Ressources / population | 7 métriques (cognitives, sociales, informationnelles) |
| Groupes | Absents | Coalitions explicites + rôles |
| Performance | 50 agents @ 25 ticks/sec | 1000 agents @ 10+ ticks/sec |

Calendrier cible : **~24 semaines, 2–3 développeurs, 12 phases**.

## Documentation (V2)

Pointez d'abord vers le [README V2](docs/V2/README.md) et la
[vue d'ensemble des phases](docs/V2/00-PHASES-OVERVIEW.md), puis :

- [Vision V2](docs/V2/01-VISION.md) · [Modèle conceptuel](docs/V2/02-CONCEPTUAL-MODEL.md) · [Spécification V2](docs/V2/03-V2-SPECIFICATION.md)
- [Architecture](docs/V2/04-ARCHITECTURE.md) · [Agents BDI](docs/V2/05-AGENTS-BDI.md) · [Système de décision](docs/V2/06-DECISION-SYSTEM.md)
- [Protocole de communication](docs/V2/07-COMMUNICATION-PROTOCOL.md) · [Persistance](docs/V2/08-PERSISTENCE.md) · [Analyzer V2](docs/V2/09-ANALYZER-V2.md)
- [Web UI V2](docs/V2/10-WEB-UI-V2.md) · [Renderer 3D V2](docs/V2/11-RENDERER-3D-V2.md) · [Obstacles statiques](docs/V2/12-OBSTACLES-STATIC.md)
- [Logging & instrumentation](docs/V2/13-LOGGING-INSTRUMENTATION.md) · [Scaling](docs/V2/14-SCALING-STRATEGY.md) · [Plan de test](docs/V2/15-TEST-PLAN.md)
- [Roadmap V2](docs/V2/16-ROADMAP.md) · [ADR V2](docs/V2/17-ADR.md) · [Glossaire](docs/V2/GLOSSAIRE.md)
- [Feuille de route source](docs/V2/V2_feuille_de_route_simulation_emergente.md)
