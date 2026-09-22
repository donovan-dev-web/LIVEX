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

## Tests

```bash
.venv/bin/flake8 echos && .venv/bin/pytest           # 167 tests, couverture 98,2 % ≥ 80 %
```

## UI (echos-ui)

```bash
cd echos-ui && npm install && npm run dev            # Vite
npm run lint && npm run build && npm test -- --run   # vérification CI
```

## Jalons

- **U0** : structure monorepo buildable/testable, API FastAPI + squelette des 8 moteurs, interface Vite/React, contrats d'ingestion (`WorldSnapshot`/`ExternalEvent` camelCase) + clients `WsClient` :5180 / `ControlClient` :5181 testés sur golden files.
- **U1** : ingestion alignée par tick (`TickSegment`/`aligned_ticks`), agrégation incrémentale, stockage SQLite (`AnalyticsStore`, `SCHEMA_VERSION`) + Parquet, pipeline `consume()`.
- **U2** : les 7 moteurs de métriques (purs, déterministes, données absentes → neutres) + golden files + preuve J2.
- **U3** : indicateurs d'émergence (`EmergenceIndicators`, score composite, auto-détection des phénomènes, disclaimer §4.10.3) + preuve J3.
- **U4 (API REST)** : endpoints lecture seule (runs, métriques/séries `?every=N`, export JSON/CSV reproductible, croyances, relations, groupes, phénomènes), cache de séries validé, métriques calculées à l'ingestion (`compute_all`), preuve J5.