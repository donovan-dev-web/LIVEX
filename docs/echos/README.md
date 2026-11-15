# ECHOS — Emergent Cognition & Holistic Observation System

[![Version: 0.1.0](https://img.shields.io/badge/Version-0.1.0-1f7f6f.svg)](CHANGELOG.md)
[![Statut: STABLE](https://img.shields.io/badge/Statut-STABLE-00d4a0.svg)](README.md)
[![7 moteurs](https://img.shields.io/badge/Moteurs-7-1f7f6f.svg)](METRICS_SPEC.md)
[![API: REST 5000](https://img.shields.io/badge/API-REST%205000-1f7f6f.svg)](API_REST.md)
[![Tests: 393](https://img.shields.io/badge/Tests-393-1f7f6f.svg)](TESTING.md)
[![Couverture: 91,14 %](https://img.shields.io/badge/Couverture-91%2C14%25-1f7f6f.svg)](TESTING.md)

**Composant** : ECHOS
**Statut** : [STABLE]
**Version** : 0.1.0
**Dernière mise à jour** : 15 novembre 2026
**Dépend de** : la documentation transversale (../), SYNE 0.18.0 (contrat d'observabilité 0.6.0)
**Source Monographie** : Partie 4

Dernier résultat de suite connu : **419 réussis, 14 ignorés et 91,14 %** de
couverture au **15 novembre 2026**.

---

## Rôle

Observatoire de LIVEX : ECHOS ingère les flux, calcule des métriques, conserve des données d'analyse et publie une API ; il n'écrit pas dans l'état simulé. Il demeure un moteur sans interface (ADR-007), présenté par le Launcher. Pour PRISM V2, un chemin dédié valide, persiste et reprend les enveloppes du flux de lecture PRISM (avec `contentHash`, séquence et reprise après coupure), les rejoue en métriques de stocks/production/usure et en quatre indicateurs, et compare deux runs par `contentHash` et premier tick de divergence.

## Lancement seul

```console
# API FastAPI (aucune page servie — ADR-007)
uvicorn echos.api.app:app --host 127.0.0.1 --port 5000

# Observation : fenêtres natives du Launcher (consoles de logs, fenêtre d'analyse)
```

## Dépendances

- **Python** : FastAPI, NumPy, Pandas, SciPy, NetworkX.
- **Présentation** : **aucune** — pas de framework JS, pas de navigateur, pas de shell de bureau ; voir `../launcher/adr/ADR-007-consoles-et-fenetre-analyse-natives.md`.
- **Stockage** : SQLite (agrégations) + Parquet (séries lourdes).
- Connexion historique à **SYNE** (WebSocket :5180, HTTP :5181). Le flux de lecture PRISM V2 utilise un adaptateur d'ingestion distinct ; il ne remplace pas le chemin SYNE existant.

## Interfaces

- `API_REST.md` — API REST locale, port 5000.
- `../syne/API_CONTRACTS.md` — contrat d'entrée (snapshot/event de SYNE).
- `GET /api/runs/{id}/prism-stream` — enveloppes PRISM V2 validées, persistées et reprises après coupure (ECHOS-120).

## Documentation du composant

| Document | Rôle |
| :-- | :-- |
| `VISION.md` | Rôle scientifique, dimensions, interdits |
| `ARCHITECTURE.md` | Composants, intégration SYNE, séparation des données |
| `METRICS_SPEC.md` | Les 7 moteurs de métriques |
| `ANALYSIS_FOUNDATIONS.md` | Fondements scientifiques et mathématiques : équations, justifications, interprétations (tout le cœur analytique) |
| `DYNAMIC_VIABILITY_INDEX.md` | **[DRAFT]** Indice de viabilité dynamique (DVI) : spécification mathématique du régime dynamique viable (composantes, noyau géométrique, régimes, post-run) — successeur conceptuel d'`EmergenceScore`, non implémenté |
| `EMERGENCE_INDICATORS.md` | Score d'émergence, auto-détection |
| `CAUSAL_ANALYSIS.md` | Reconstruction causale et limites |
| `EXPERIMENT_COMPARISON.md` | Comparaison de runs, reproductibilité |
| `API_REST.md` | Endpoints |
| `LOGGING_INSTRUMENTATION.md` | 3 niveaux de journalisation, profilage |
| `LIMITATIONS.md` | Limites méthodologiques et techniques |
| `TESTING.md` | Tests, golden files, ≥ 80 % |
| `ROADMAP.md` | Roadmap ECHOS |
| `CHANGELOG.md` | Versions |
| `adr` | Décisions d'architecture |
| `METRICS_DICTIONARY.md` | Dictionnaire des unités, conventions d'échelle, matrice métrique → API → vue |
| `REFERENCE_SCENARIOS.md` | Cas étalons, baselines et exemples de rapports interprétés |
