# Architecture

## 1. Vue générale

```text
+-------------------------+
|    Simulation Core      |
|        C# / .NET        |
+------------+------------+
             |
        State / Events
             |
        Transport Layer (WebSocket)
             |
       +-------+--------+
       |                |
       v                v
  3D UI (Godot)      Analyzer (.NET)
  WebSocket          WebSocket
                      |
                      | REST API
                      v
                   Web UI (React + TS)
        (double flux : WebSocket sim + API Analyzer)
```

### 1.1 Découpage du dépôt (mono-repo)

Le dépôt est organisé par composant, un dossier racine par partie :

| Dossier racine      | Composant            | Rôle                                                        | Statut       |
|---------------------|----------------------|-------------------------------------------------------------|--------------|
| `simulation-core/`  | Simulation Core      | Moteur de simulation, logique métier (C#)                   | Phases 1–4 OK|
| `analyzer/`         | Analyzer (.NET)      | Détection d'émergence, métriques, API REST                 | Phase 5 OK   |
| `web-ui/`           | Web UI (React+TS)    | Rapports, visualisation temps réel, comparaison de runs    | Phase 6 OK   |
| `godot-renderer/`   | 3D UI (Godot)        | Scène 3D, visualisation, caméra, debug                      | Phase 10     |
| `ci/`               | CI/CD                | Pipeline build/tests/coverage, artefact, release           | Phase 11     |

Contrats transverses :
- Le `Simulation.Core` **ne référence aucun** moteur graphique (principe §7).
- La communication vers les présentations/analyzer passe par un *Transport Layer*
  (Phase 4 : **WebSocket** retenu) indépendant du renderer.
- L'`Analyzer` (.NET, Phase 5) s'abonne au WebSocket de simulation, calcule les
  métriques d'émergence et les expose via une **API REST** consommée par le Web UI.
- Le `Web UI` (Phase 6) est en **double flux** : il s'abonne aussi au WebSocket de
  simulation (vue live) et consomme l'API REST de l'Analyzer (métriques).
- `simulation-core/` contient la solution `.NET` (`EmergentSimulation.slnx`) et les
  projets `Simulation.Core` (bibliothèque) + `Simulation.Console` (exécutable
  d'observabilité / harnais de validation).
- Les dossiers `godot-renderer/`, `web-ui/`, `analyzer/` sont créés lors des phases
  respectives ; leur emplacement est réservé ici pour éviter toute dérive de layout.

## 2. Simulation Core

Le Core contient toute la logique métier de la simulation.

Il ne doit pas référencer un moteur graphique.

Le monde logique est **2D** (`{x, y}`) ; le renderer 3D réutilisera ces coordonnées en `X/Z` le cas échéant. Voir `docs/V1/03-V1-SPECIFICATION.md` §2.

## 3. Modules

```text
Simulation.Core
├── World
├── Agents
├── Components / State
├── Needs
├── Perception
├── Memory
├── Decisions
├── Actions
├── Systems
├── Events
├── Persistence
└── Configuration
```

## 4. Systèmes

Systèmes V1 :

- WorldSystem
- PhysiologySystem
- PerceptionSystem
- MemorySystem
- NeedSystem
- DecisionSystem
- ActionSystem
- MovementSystem

## 5. Présentation

Le renderer possède :

- représentation des agents
- représentation des ressources
- caméra
- animations
- UI
- outils de debug

Il ne décide pas du comportement.

## 6. Communication

La communication externe est validée (Phase 4) : **WebSocket** est retenu
(universel, natif navigateur, client `.NET` partagé). Le protocole reste
indépendant du renderer (principe §7).

- **Simulation Core → présentations/analyzer** : WebSocket (snapshots + events,
  `WorldSnapshot` / `ExternalEvent`, cf. `docs/V1/09-EVENTS-API.md`).
- **Analyzer → Web UI** : **API REST** (Phase 6) — l'Analyzer expose les
  métriques et rapports calculés à partir du flux de simulation.
- **Web UI** : **double flux** — abonnement au WebSocket de simulation (vue live)
  **et** consommation de l'API REST de l'Analyzer (métriques d'émergence).

Le `gRPC` et l'IPC restent des alternatives possibles si le débit ou le typage
fort côté client le justifient ultérieurement.

## 7. Principe de dépendance

```text
Simulation.Core
      ^
      |
Presentation Adapter
```

Jamais :

```text
Simulation.Core -> Godot
Simulation.Core -> Unity
```

## 8. Performance

La performance sera mesurée avant toute optimisation structurelle.

Métriques :

- durée moyenne d'un tick
- ticks/seconde
- temps par système
- mémoire
- nombre d'agents
- nombre d'interactions
