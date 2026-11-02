# CHANGELOG — PRISM (documentation V2)

**Composant** : PRISM
**Statut** : [STABLE]
**Dernière mise à jour** : 1er novembre 2026

Format : Keep a Changelog. Ce fichier retrace la **documentation** de la V2 ; l'historique de PRISM V1 (`1.0.0`) est archivé dans `legacy-unreal/CHANGELOG.md`.

## [Unreleased] — Documentation V2

### Added
- Spécification complète de PRISM V2 (monde voxel autoritaire) : vision, architecture, répartition des responsabilités, modèle de données, génération, boucle de simulation, contenu, systèmes, interface agent, contrat SYNE↔PRISM, déterminisme, persistance, configuration, performance, rendu, tests, packaging.
- ADR-004 à ADR-010 ; registre de questions ouvertes (Q-01 à Q-12).
- Feuille de route de bout en bout et backlog (`ROADMAP.md`, `ISSUES.md`).

### Changed
- ADR-002 et ADR-003 passent à `[Superseded]` (au moment de la fusion de la V2).
- Le contrat PRISM cesse d'être une consommation de `syne/API_CONTRACTS.md` 0.5.0.

### Removed
- Rien tant que la V2 n'est pas fusionnée ; l'archivage de la documentation V1 est un jalon (ISSUES `PRISM-0xx`, phase P13).
