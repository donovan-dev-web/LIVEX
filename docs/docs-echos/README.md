# ECHOS — Emergent Cognition & Holistic Observation System

[![Statut: STABLE](https://img.shields.io/badge/Statut-STABLE-00d4a0.svg)](README.md)
[![7 moteurs](https://img.shields.io/badge/Moteurs-7-1f7f6f.svg)](METRICS_SPEC.md)
[![API: REST 5000](https://img.shields.io/badge/API-REST%205000-1f7f6f.svg)](API_REST.md)

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : la documentation transversale (../)
**Source Monographie** : Partie 4

---

## Rôle

Observatoire de LIVEX : il transforme l'exécution de SYNE en données compréhensibles (états, événements, métriques, graphes, historiques, comparaisons, contrôles). **Il observe, il n'influence pas le phénomène mesuré.**

## Lancement seul

```console
# API FastAPI (V0.1)
uvicorn echos.api.app:app --host 127.0.0.1 --port 5000

# Interface (Electron + React)
cd echos/echos-ui && npm run start
```

## Dépendances

- **Python** : FastAPI, NumPy, Pandas, SciPy, NetworkX.
- **Interface** : Electron + React/TypeScript, ECharts/Plotly.
- **Stockage** : SQLite (agrégations) + Parquet (séries lourdes).
- Se connecte à **SYNE** (WebSocket :5180 pour observer, HTTP :5181 pour piloter).

## Interfaces

- `API_REST.md` — API REST locale, port 5000.
- `../docs-syne/API_CONTRACTS.md` — contrat d'entrée (snapshot/event de SYNE).

## Documentation du composant

| Document | Rôle |
| :-- | :-- |
| `VISION.md` | Rôle scientifique, dimensions, interdits |
| `ARCHITECTURE.md` | Composants, intégration SYNE, séparation des données |
| `METRICS_SPEC.md` | Les 7 moteurs de métriques |
| `EMERGENCE_INDICATORS.md` | Score d'émergence, auto-détection |
| `CAUSAL_ANALYSIS.md` | Reconstruction causale et limites |
| `EXPERIMENT_COMPARISON.md` | Comparaison de runs, reproductibilité |
| `API_REST.md` | Endpoints |
| `LOGGING_INSTRUMENTATION.md` | 3 niveaux de journalisation, profilage |
| `LIMITATIONS.md` | Limites méthodologiques et techniques |
| `TESTING.md` | Tests, golden files, ≥ 80 % |
| `ROADMAP.md` | Roadmap ECHOS |
| `CHANGELOG.md` | Versions |
| `adr/` | Décisions d'architecture |