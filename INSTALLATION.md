# Installation & Démarrage

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : —

---

> Les implémentations V0.1 du **Jalon U0** (socle & gouvernance) sont en place :
> `syne/` (moteur C#/.NET) et `echos/` (observatoire Python/FastAPI + UI React/TS).
> La conteneurisation Docker est **hors périmètre U0** (voir `ROADMAP.md`).

## Prérequis

- [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) ≥ 10.0.4xx (pinné `syne/global.json`)
- Python 3.11+ (ECHOS API/analyse) et Node 20+ (ECHOS UI)

## Lancer SYNE (moteur — CLI)

```bash
dotnet run --project syne/Simulation.Console -- --seed 12345 --max-ticks 1000
```

Flags : `--seed <s>`, `--max-ticks <n>`, `--world-size <w> <h>`, `--config <path>`, `--headless`.

## Lancer ECHOS (observatoire)

```bash
# API FastAPI (port 5000)
python3 -m venv echos/.venv
echos/.venv/bin/pip install -r echos/requirements-dev.txt
echos/.venv/bin/uvicorn echos.api.app:app --app-dir echos --host 127.0.0.1 --port 5000

# Interface (React + TypeScript)
cd echos/echos-ui && npm ci && npm run dev
```

## Prototype historique

Le prototype V1/V2 (validé par 98 tests, [`docs/docs_prototype/`](docs/docs_prototype/))
reste référencé comme socle d'héritage documenté (`[HÉRITÉ]`).

## Orchestration complète (cible future)

`docker compose up --build` est la cible d'orchestration des trois modules
(SYNE, ECHOS, PRISM) une fois leur conteneurisation définie — **non encore
actif** (hors périmètre U0, voir [`ROADMAP.md`](ROADMAP.md)).

## Points restés ouverts

- Contrats de transport réels SYNE (WebSocket 5180 / HTTP 5181) : jalons U1+.
- Persistance SQLite : jalon U8.
- PRISM : créé après U0 → U8 (condition ROADMAP).

## Aller plus loin

- Architecture détaillée : [`ARCHITECTURE.md`](ARCHITECTURE.md)
- Documentation par module : [`docs/docs-syne/`](docs/docs-syne/) ·
  [`docs/docs-echos/`](docs/docs-echos/) · [`docs/docs-prism/`](docs/docs-prism/)
- Contribuer : [`CONTRIBUTING.md`](CONTRIBUTING.md)
