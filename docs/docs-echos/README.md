# ECHOS — Emergent Cognition & Holistic Observation System

[![Statut: STABLE](https://img.shields.io/badge/Statut-STABLE-00d4a0.svg)](README.md)
[![7 moteurs](https://img.shields.io/badge/Moteurs-7-1f7f6f.svg)](METRICS_SPEC.md)
[![API: REST 5000](https://img.shields.io/badge/API-REST%205000-1f7f6f.svg)](API_REST.md)
[![Tests: 365](https://img.shields.io/badge/Tests-365-1f7f6f.svg)](TESTING.md)

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : la documentation transversale (../), SYNE ≥ 0.13.0 (contrat d'observabilité 0.2.1)
**Source Monographie** : Partie 4

---

## Rôle

Observatoire de LIVEX : il transforme l'exécution de SYNE en données compréhensibles (états, événements, métriques, graphes, historiques, comparaisons, contrôles). **Il observe, il n'influence pas le phénomène mesuré.** ECHOS est un **moteur sans interface** (ADR-007) : il publie une API, le **Launcher présente**.

## Lancement seul

```console
# API FastAPI (aucune page servie — ADR-007)
uvicorn echos.api.app:app --host 127.0.0.1 --port 5000

# Observation : fenêtres natives du Launcher (consoles de logs, fenêtre d'analyse)
```

## Dépendances

- **Python** : FastAPI, NumPy, Pandas, SciPy, NetworkX.
- **Présentation** : **aucune** — pas de framework JS, pas de navigateur, pas de shell de bureau ; voir `../docs-launcher/adr/ADR-007-consoles-et-fenetre-analyse-natives.md`.
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
| `adr/` | Décisions d'architecture |
| `FRONTEND_VISION.md` | **Historique** : spécification de l'interface retirée d'ECHOS (ADR-007) |
| `USER_STORIES.md` | **Historique** : personas + user stories de l'interface retirée (ADR-007) |
| `UI_DESIGN.md` | **Historique** : design system, écrans, layout de l'interface retirée (ADR-007) |
| `METRICS_DICTIONARY.md` | Dictionnaire des unités, conventions d'échelle, matrice métrique → API → vue |
| `REFERENCE_SCENARIOS.md` | Cas étalons, baselines et exemples de rapports interprétés |