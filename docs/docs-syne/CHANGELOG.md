# CHANGELOG — SYNE

**Composant** : SYNE
**Statut** : [DRAFT]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`syne-vX.Y.Z`).

## [Unreleased]

### Added
- Documentation technique V0.1 complète du composant (VISION, ARCHITECTURE, DATA_MODEL, SIMULATION_LOOP, COGNITIVE_ARCHITECTURE, SYSTEMS_SPEC, COMMUNICATION_PROTOCOL, PERSISTENCE, DETERMINISM, CONFIGURATION, API_CONTRACTS, PERFORMANCE, TESTING, ROADMAP).
- Formalisation des ADR-001, ADR-002, ADR-005 à ADR-011 (Annexe F de la Monographie).
- **Socle U0 (SYNE-1)** : solution `Syne.sln`, bibliothèque `Simulation.Core` (configuration Annexe H, loader JSON générique, validation, flags CLI), `Simulation.Console` (conf résolue + sonde PRNG), tests xUnit (28). PRNG déterministe **xoshiro256\*\*** + **splitmix64** (vecteurs épinglés), `global.json` SDK 10.0.400. ADR-012 (config JSON + CLI).

### Changed
- ARCHITECTURE.md : §4 (couche applicative réelle, Dockerfile reporté) et §6 (PRNG défini) mis à jour.

### Deprecated
- (aucun)

## [0.0.0] — à venir

Version initiale (prototype V1/V2 de la Monographie référencé comme [HÉRITÉ]).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | Documentation V0.1 |
| 21 septembre 2026 | Socle U0 : solution, config, PRNG, ADR-012 | SYNE-001 / SYNE-005 / SYNE-006 |