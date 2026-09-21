---
title: LIVEX
---

# LIVEX — Systems & Emergent Network Engine

[![Statut: develop](https://img.shields.io/badge/Statut-develop-1f7f6f.svg)](https://github.com/anomalyco/LIVEX/tree/develop)
[![Monorepo](https://img.shields.io/badge/Monorepo-dotnet%20%2B%20python%20%2B%20typescript-512bd4.svg)](articles/transverse/ARCHITECTURE.md)
[![Déterminisme](https://img.shields.io/badge/D%C3%A9terminisme-bit--%C3%A0--bit-00d4a0.svg)](articles/syne/DETERMINISM.md)
[![Tests: 160+](https://img.shields.io/badge/Tests-160+-1f7f6f.svg)](articles/syne/TESTING.md)
[![Licence](https://img.shields.io/badge/Licence-propri%C3%A9taire-303030.svg)](articles/transverse/LICENSE)

LIVEX est un **moteur de simulation sociale** : une population d'entités
autonomes (des « besoins », une perception partielle, des décisions BDI)
évolue dans un monde spatial continu, et la plateforme **mesure
l'émergence** — structures, conventions, phénomènes collectifs — en
respectant une **règle d'or** : l'observation ne transforme jamais le
phénomène étudié.

Construit autour de trois composants :

| Composant | Rôle | Doc |
| :-- | :-- | :-- |
| **SYNE** — Simulation.Core | Cœur C#/.NET : boucle de simulation, entités BDI, perception, décisions, **déterminisme bit-à-bit**. Émet l'état via WebSocket :5180. | [Docs SYNE](articles/syne/README.md) · API dans le menu |
| **ECHOS** — analyse | Stack Python/FastAPI : ingestion en temps réel (WebSocket), séries agrégées (SQLite + Parquet), métriques & indices d'émergence, API REST. | [Docs ECHOS](articles/echos/README.md) |
| **PRISM** — rendu | Engine de rendu (TypeScript/React) : visualisation du monde, des croyances et des réseaux observés. | [Docs PRISM](articles/prism/README.md) |

## Ce qui fait LIVEX

- **Déterminisme strict** : à seed égale, deux runs produisent exactement la
  même suite d'événements — bit-à-bit (vérifié par des golden files).
- **Observation non intrusive** : ECHOS et PRISM consomment les contrats
  publiés par SYNE sans jamais écrire dans le monde simulé.
- **Documentation exhaustive** : specifications, ADRs, contrats d'ingestion,
  tests (160+ côté .NET, 73+ côté ECHOS) et **référence API DocFX** générée
  depuis les sources.

## Documentation

- [Transversal](articles/transverse/VISION.md) — vision, architecture, roadmap, glossaire, installation.
- [SYNE](articles/syne/README.md) — moteur de simulation (contrats, config, tests).
- [ECHOS](articles/echos/README.md) — analyse, agrégation, métriques d'émergence.
- [PRISM](articles/prism/README.md) — rendu et scènes.

La **référence API** de `Simulation.Core` est générée par DocFX et accessible
depuis le menu **« API · Simulation.Core »**.

## Principes de gouvernance

- Flux Git : `feature` → `develop` (intégration, CI) → `main` (releases SemVer).
- CI sur chaque PR : build warnaserror, tests, couverture ≥ 80 %, lint.
- Chaque décision d'architecture est tracée dans un **ADR** (Architecture
  Decision Record).

---

*Site généré par [DocFX](https://dotnet.github.io/docfx/) à partir de la
documentation du monorepo LIVEX.*