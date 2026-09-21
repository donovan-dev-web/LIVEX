# ECHOS

Observation et pilotage de SYNE : calculs d'analyse, API REST, interface d'observation (React + TypeScript).

**Statut** : [DRAFT] — jalon U0 (socle). Documentation : `../docs/docs-echos/` (VISION, ARCHITECTURE, METRICS_SPEC, API_REST, TESTING).

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

## API local (jalon U0)

```bash
python -m venv .venv && .venv/bin/pip install -r requirements-dev.txt
.venv/bin/uvicorn echos.api.app:app --reload        # http://127.0.0.1:8000
curl http://127.0.0.1:8000/health                    # {"status":"ok", ...}
```

## Tests (U0)

```bash
.venv/bin/flake8 echos && .venv/bin/pytest           # 41 tests, couverture ≥ 80 %
```

## UI (echos-ui)

```bash
cd echos-ui && npm install && npm run dev            # Vite
npm run lint && npm run build && npm test -- --run   # vérification CI
```

## Jalons

- **U0 (ce socle)** : structure monorepo buildable/testable, API FastAPI + squelette des 7 moteurs, interface Vite/React, contrats d'ingestion (`WorldSnapshot`/`ExternalEvent` camelCase) + clients `WsClient` :5180 / `ControlClient` :5181 testés sur golden files.
- **U1+** : implémentation des moteurs de métriques, connexion E2E réelle à SYNE, stockage SQLite/Parquet.