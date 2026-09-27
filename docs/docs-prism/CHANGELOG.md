# CHANGELOG — PRISM

**Composant** : PRISM
**Statut** : [DRAFT]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`prism-vX.Y.Z`).

## [Unreleased]

### Added
- Plugin Unreal **PRISM-LDK** (`prism/LDK/Plugins/PrismLdk/`, module `PrismLdk`) : `UPrismLdkSubsystem` (WebSocket `5180` + contrôle HTTP `5181`), types, fonctions et événements exposés à Blueprint (`OnWorldInitialized`, `OnSnapshot`, `OnWorldDelta`, `OnSyneEvent`, `OnControlResult`, `OnError`).
- Hôte technique de développement/build `prism/LDK/LDK.uproject` (Unreal 5.8, `EngineAssociation` `5.8`) avec sa configuration `Config/` et ses assets Blueprint de test.
- Guide d'intégration Blueprint du plugin : `docs/docs-prism/PRISM_UNREAL_IMPLEMENTATION.md`.
- Règles de normalisation des fins de ligne et de traitement des binaires Unreal (`.uasset`, `.umap`) dans `.gitattributes`.
- Documentation technique V0.1 complète du composant (VISION, ARCHITECTURE, SCENE_SPEC, TRANSPORT_API, RENDERING_SPEC, VISUALIZATION_SPEC, UX_INTERACTION, ASSETS_CONVENTIONS, TESTING, ROADMAP).
- ADR-001 (choix Godot édition .NET) + index référencant les ADR transverses 003/004.

### Changed
- L'interface d'analyse, autrefois une app web séparée (React + TypeScript), est désormais **intégrée à ECHOS** en V0.1 et consommée en complément par PRISM.

### Deprecated
- (aucun)

## [0.0.0] — à venir

Version initiale (prototype Godot V1/V2 de la Monographie, [HÉRITÉ]).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 27 septembre 2026 | Plugin Unreal PRISM-LDK + hôte technique + guide d'intégration | Première implémentation exécutable de PRISM |
| 17 septembre 2026 | Création | Documentation V0.1 |