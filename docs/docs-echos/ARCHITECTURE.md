# ARCHITECTURE.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 23 septembre 2026
**Dépend de** : `VISION.md`, `../COMMUNICATION.md`
**Source Monographie** : §4.2

---

## 1. Architecture cible

| Composant | Rôle (V0.1) | Implémentation V0.1 |
| :-- | :-- | :-- |
| **Analyse** | Calculs scientifiques, traitement des données, métriques | Python (FastAPI) |
| **Application** | Couche applicative, API REST, pilotage | FastAPI local |
| **Interface** | Vues d'observation, contrôle, calibration | **Retirée d'ECHOS** (ADR-007) : présentation native portée par le Launcher (consoles de logs + fenêtre d'analyse Avalonia) |
| **Stockage** | Données d'analyse (séparé de la donnée SYNE) | SQLite + Parquet |
| **Source** | SYNE — production d'événements | WebSocket :5180 |

### ⚠ Divergence annoncée vs prototype

L'architecture cible de la Monographie (§4.2.1) prévoit **Django** (Application) et un **shell Electron** avec interface web React (prototype C#/.NET). **Décision V0.1 (décision utilisateur, 23/09/2026)** : la stack ECHOS est **FastAPI local (PAS Django)**, avec **NumPy/Pandas/SciPy/NetworkX** pour le calcul et **SQLite/Parquet** pour le stockage d'analyse. **Décision du 5 octobre 2026 (ADR-007, utilisateur)** : ECHOS est un **moteur sans interface** — l'interface `echos-ui` (React/Vite) et le shell `echos-desktop` (Electron) ont été **supprimés**, de même que le montage statique de FastAPI. Les vues d'observation et d'analyse sont portées par le **Launcher** (Avalonia), qui consomme l'API REST. Cette divergence est assumée et documentée (cf. `../ARCHITECTURE.md` racine, `adr/ADR-001-stack-applicative.md`, `../docs-launcher/adr/ADR-007-consoles-et-fenetre-analyse-natives.md`).

## 2. Intégration avec SYNE

```mermaid
flowchart LR
    S[SYNE\nWebSocket :5180] -->|snapshot / event| E[ECHOS\nconsommateur d'événements]
    E -->|contrôle / calibration\nHTTP :5181| S
    E --> M[Métriques temps réel]
    E --> DB[(Stockage SQLite\nhistorique)]
    E --> API[API REST :5000]
    E --> WS2[Diffusion WebSocket\nmétriques]
```

- ECHOS **observe** SYNE (contrat de transport, WebSocket :5180) et **pilote** SYNE (contrôle, calibration) — Monographie §4.2.2.
- Le **présentatif est le Launcher** (ADR-007) : il sonde l'API REST :5000 (1 s) et affiche les séries dans sa fenêtre d'analyse. Aucune diffusion WebSocket de métriques n'est servie par ECHOS à ce jour.

## 3. Séparation des données

Les données d'analyse d'ECHOS sont **séparées** des données de persistance de SYNE :
- la base SYNE contient l'**état canonique** du monde simulé (schéma 11 tables, Annexe G) ;
- la base ECHOS contient les **agrégations, métriques et indices mesurés** (SQLite + Parquet pour les séries lourdes).

Cette séparation garantit que l'observation ne modifie pas la persistance de référence.

## 4. Systèmes internes

| Système | Rôle | Doc |
| :-- | :-- | :-- |
| Ingestion temps réel | Consommateur WebSocket 5180 → `snapshot`/`event` — clients `ws_client` (transport injectable) + `control_client` HTTP 5181 | `../docs-syne/API_CONTRACTS.md`, `echos/echos/ingestion/` |
| Stockage d'analyse | Agrégation incrémentale par tick (sans perte, sous-échantillonnage `sample_every`), SQLite `AnalyticsStore` (schéma stable versionné) + séries lourdes Parquet (jointure SQLite↔Parquet cohérente), pipeline `consume()` | `echos/echos/storage/` |
| 7 moteurs de métriques | Calculs d'analyse | `METRICS_SPEC.md` |
| Indicateurs d'émergence | Score composite, auto-détection | `EMERGENCE_INDICATORS.md` |
| Analyse causale | Reconstruction des chaînes | `CAUSAL_ANALYSIS.md` |
| Comparaison expérimentale | Runs contrôlés, reproductibilité | `EXPERIMENT_COMPARISON.md` |
| API REST | Endpoints d'interrogation lecture seule (port 5000) — `api/app.py` (factory + `ECHOS_ANALYTICS_DB`), `api/routes.py` (ECHOS-040→044), `api/series.py` (cache LRU) | `API_REST.md` |
| Shell de bureau | **Supprimé** (ADR-007) : plus d'Electron, plus de montage statique — `create_app()` est une API seule | `../docs-launcher/adr/ADR-007-consoles-et-fenetre-analyse-natives.md` |
| Logging & instrumentation | 3 niveaux (structuré, traces, texte) | `LOGGING_INSTRUMENTATION.md` |

---

## 5. Structure de code

Mise en œuvre découpée, chaque sous-composant buildable/testable séparément (ECHOS-002) :

```
echos/
├── pyproject.toml            # paquet echos, pytest --cov-fail-under=80
├── .flake8                   # flake8 (max-line-length=100)
├── echos/
│   ├── __init__.py           # __version__ (synchro pyproject)
│   ├── server.py             #   echos-serve : uvicorn API seule (aucune page servie)
│   ├── api/                  # API REST (ECHOS-040 → 045)
│   │   ├── app.py            #   create_app(store) FastAPI + ECHOS_ANALYTICS_DB, /health
│   │   ├── routes.py         #   /api/runs*, /metrics, /export, /beliefs, /relationships, /groups, /emergent-phenomena
│   │   └── series.py         #   SeriesCache LRU invalide par ingest_version
│   ├── analysis/             # 8 moteurs (7 métriques + EmergenceIndicators)
│   │   └── __init__.py       #   registre ENGINES + known_engines() + compute_all(snapshot)
│   └── ingestion/            # clients ws/control SYNE (API_CONTRACTS.md §2-3)
│       ├── models.py         #   WorldSnapshot / ExternalEvent (camelCase) + parse_message
│       ├── stream.py         #   TickSegment / aligned_ticks (flux aligné par tick)
│       ├── ws_client.py      #   WsClient :5180 (transport injectable, réception déterministe)
│       └── control_client.py #   ControlClient :5181 (start / pause / resume / reset)
│   └── storage/              # stockage d'analyse (ECHOS-011 → 013, v2 en ph4)
│       ├── aggregation.py    #   TickRecord.from_segment / summarize / downsample
│       ├── sqlite.py         #   AnalyticsStore (SCHEMA_VERSION "5", tables tick_metrics + tick_contexts, thread-safe)
│       ├── parquet.py        #   séries lourdes PyArrow + coherence_errors
│       └── pipeline.py       #   consume() flux → SQLite + Parquet + moteurs (compute_all)
├── tests/                    # pytest (api, registre moteurs, versionnage) + fixtures/golden
```

- `echos` est le composant **Application + Analyse** : il n'a **pas** de composant Interface (ADR-007).
- Les moteurs exposent le contrat `ENGINE_NAME` / `METRICS` / `compute(snapshot)` ; implémentation au jalon U1.
- Les contrats d'ingestion (`WorldSnapshot`/`ExternalEvent`) sont des modèles pydantic camelCase validés ; le client WebSocket et le client de contrôle sont testés de façon **déterministe sur fixtures** (E2E réel SYNE = jalon U1).

---

## Points restés ouverts dans ce document
- La divergence FastAPI/Django est tranchée et documentée. L'interface web et le
  shell Electron, livrés en V0.1, ont été **retirés le 5 octobre 2026** (ADR-007) :
  ECHOS est une API Python seule, et le Launcher porte les fenêtres natives de
  présentation (consoles de logs, fenêtre d'analyse).
- Choix des bibliothèques de visualisation : **sans objet pour ECHOS** — le
  rendu appartient au Launcher (LiveCharts2).