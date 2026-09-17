# README.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : —

---

# LIVEX

**Systems & Emergent Network EXperiment** — une plateforme de simulation multi-agents à émergence, persistante et temps réel.

[![Licence: MIT](https://img.shields.io/badge/Licence-MIT-00d4a0.svg)](LICENSE)
[![Version](https://img.shields.io/badge/Version-V0.1-9cf.svg)](VERSIONING.md)
[![Langue: FR](https://img.shields.io/badge/Langue-Fran%C3%A7ais-949494.svg)](GLOSSARY.md)

Des entités autonomes — dotées de besoins, de mémoire, de croyances et de capacités — interagissent dans un monde persistant et partiellement observable. Les structures collectives (groupes, échanges, conflits) **émergent**, elles ne sont jamais scriptées.

## Les trois modules

| Module | Rôle | Docs |
| :-- | :-- | :-- |
| **SYNE** | Moteur de simulation — vérité du monde | [`docs/docs-syne/README.md`](docs/docs-syne/README.md) |
| **ECHOS** | Observation, analyse, pilotage | [`docs/docs-echos/README.md`](docs/docs-echos/README.md) |
| **PRISM** | Représentation 3D & interaction | [`docs/docs-prism/README.md`](docs/docs-prism/README.md) |

## Pourquoi LIVEX

- **Déterminisme bit-à-bit** : reproductibilité exacte avec même seed + config + version moteur.
- **Cognition explicite** : architecture BDI, décisions traçables (DecisionRecord).
- **Anti-hallucination scientifique** : un score d'émergence est une mesure, pas une preuve.

## Démarrage rapide

> Les implémentations V0.1 sont en cours de conception (documentation d'abord). Les commandes ci-dessous reflètent la cible ; l'état courant du dépôt contient la documentation et le prototype historique (`docs/docs_prototype/`).

```bash
# Lancer le moteur (simulation-core du prototype, tête-à-tête)
dotnet run --project simulation-core/Simulation.Console -- --seed 12345 --max-ticks 1000

# Orchestration complète (cible)
docker compose up --build
```

## Documentation

- **Vision** : [`VISION.md`](VISION.md)
- **Architecture** : [`ARCHITECTURE.md`](ARCHITECTURE.md)
- **Communication inter-composants** : [`COMMUNICATION.md`](COMMUNICATION.md)
- **Glossaire** : [`GLOSSARY.md`](GLOSSARY.md)
- **Road map** : [`ROADMAP.md`](ROADMAP.md)
- **FAQ** : [`FAQ.md`](FAQ.md)
- **Éthique & portée** : [`docs/ETHICS_AND_SCOPE.md`](docs/ETHICS_AND_SCOPE.md)
- **Composants** : [`docs/docs-syne/`](docs/docs-syne/) · [`docs/docs-echos/`](docs/docs-echos/) · [`docs/docs-prism/`](docs/docs-prism/)
- **Gouvernance** : [`GITFLOW.md`](GITFLOW.md) · [`CI_CD.md`](CI_CD.md) · [`VERSIONING.md`](VERSIONING.md) · [`docs/governance/`](docs/governance/)

## État du projet

- Phases 0–5 de documentation : ✅ **terminées** (socle, cadrage, SYNE, ECHOS, PRISM, consolidation).
- Statut global : **exploration V0.1** — documentation consolidée ; implémentation des composants à venir, prototype historique fonctionnel (98 tests).

## Contribuer

Voir [`CONTRIBUTING.md`](CONTRIBUTING.md) et [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md).

## Licence

Distribué sous licence MIT — voir [`LICENSE`](LICENSE).

---

## Points restés ouverts dans ce document
- Commandes cibles (compose V0.1) à revalider lors de l'implémentation réelle ; les réferences au prototype restent [HÉRITÉ].