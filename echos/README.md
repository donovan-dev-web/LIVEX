# ECHOS

Observation et pilotage de SYNE : calculs d'analyse, API REST, interface d'observation (React + TypeScript).

**Statut** : [DRAFT] — jalon U4 (API REST). Documentation : `../docs/docs-echos/` (VISION, ARCHITECTURE, METRICS_SPEC, API_REST, TESTING).

## Structure

```
echos/
├── pyproject.toml            # paquet Python echos (API + analyse + ingestion)
├── requirements.txt          # dépendances runtime (dont httpx, websockets)
├── requirements-dev.txt      # dépendances test/lint
├── echos/
│   ├── __init__.py           # __version__
│   ├── api/app.py            # application FastAPI (create_app, /health)
│   ├── analysis/             # 7 moteurs de métriques (METRICS_SPEC §1)
│   └── ingestion/            # contrats + clients SYNE (API_CONTRACTS §2-3)
└── echos-ui/                 # interface React + TypeScript (Vite)
```

## API local (jalon U4 — API REST)

```bash
python -m venv .venv && .venv/bin/pip install -r requirements-dev.txt
ECHOS_ANALYTICS_DB=/chemin/analyse.db .venv/bin/uvicorn echos.api.app:app  # 127.0.0.1:5000
curl http://127.0.0.1:5000/health                    # {"status":"ok", ...}
curl http://127.0.0.1:5000/api/runs                   # runs enregistrés
curl "http://127.0.0.1:5000/api/runs/{id}/metrics?every=10"  # séries sous-échantillonnées
```

Sans `ECHOS_ANALYTICS_DB`, les routes de donnée répondent 503 (contrat publié).

## Intégration au Launcher LIVEX

`component.json` déclare l'adaptateur Linux `echos-launcher`, qui utilise
`.venv/bin/python` lorsqu'il existe et sinon `python3`. Il accepte les arguments
communs du Launcher, mappe `--control-port` sur `ECHOS_PORT`, et lie le serveur
à `127.0.0.1`. `/health` mesure la vivacité HTTP ; la sonde déclarée
`/health/ready` ne réussit que si une base `ECHOS_ANALYTICS_DB` est configurée,
afin de ne pas annoncer comme prêt un service dont les API d'analyse répondent
503. L'arrêt propre `POST /control/shutdown` exige
`Authorization: Bearer $LIVEX_SESSION_TOKEN` et demande la fermeture d'Uvicorn.

Le Launcher transmet le jeton par variable d'environnement. Pour une session
avec ECHOS, configurer `ECHOS_ANALYTICS_DB` dans l'environnement de lancement.
L'API `echos-serve` et `python -m echos.server` conservent leur interface
existante ; l'adaptateur n'accepte que les arguments communs explicitement
déclarés et refuse les autres.

## Tests

```bash
.venv/bin/flake8 echos && .venv/bin/pytest           # 167 tests, couverture 98,2 % ≥ 80 %
```

## UI (echos-ui)

```bash
cd echos-ui && npm install && npm run dev            # Vite
npm run lint && npm run build && npm test -- --run   # vérification CI
```

## Application de bureau (echos-desktop)

Shell **Electron** (`echos-desktop/`, `ADR-003`) : il lance le backend Python
(empaqueté PyInstaller *onedir*) comme processus enfant, attend `/health`, puis
ouvre une fenêtre sur l'origine locale. FastAPI sert **aussi** le build `echos-ui`
à la racine : interface et API partagent la même origine, donc **pas de CORS**.

```bash
cd echos-ui     && npm install && npm run build      # build de l'interface (dist/)
cd ../echos-desktop && npm install
ECHOS_PYTHON=../.venv/bin/python npm run start        # dev : lance Electron + backend
npm run dist:linux                                    # .deb (Linux) ; dist:win → NSIS .exe
```

- Le binaire backend (`resources/backend/echos-server/`) et l'interface
  (`resources/ui/`) sont produits par `scripts/build-backend.mjs` et `build:ui`.
- La CI `echos-desktop.yml` construit chaque plateforme sur son runner (matrice
  `ubuntu`/`windows`) et attache les paquets à la release sur tag `echos-v*`.

## Jalons

- **U0** : structure monorepo buildable/testable, API FastAPI + squelette des 8 moteurs, interface Vite/React, contrats d'ingestion (`WorldSnapshot`/`ExternalEvent` camelCase) + clients `WsClient` :5180 / `ControlClient` :5181 testés sur golden files.
- **U1** : ingestion alignée par tick (`TickSegment`/`aligned_ticks`), agrégation incrémentale, stockage SQLite (`AnalyticsStore`, `SCHEMA_VERSION`) + Parquet, pipeline `consume()`.
- **U2** : les 7 moteurs de métriques (purs, déterministes, données absentes → neutres) + golden files + preuve J2.
- **U3** : indicateurs d'émergence (`EmergenceIndicators`, score composite, auto-détection des phénomènes, disclaimer §4.10.3) + preuve J3.
- **U4 (API REST)** : endpoints lecture seule (runs, métriques/séries `?every=N`, export JSON/CSV reproductible, croyances, relations, groupes, phénomènes), cache de séries validé, métriques calculées à l'ingestion (`compute_all`), preuve J5.