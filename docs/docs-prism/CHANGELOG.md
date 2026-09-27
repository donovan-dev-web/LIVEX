# CHANGELOG — PRISM

**Composant** : PRISM
**Statut** : [DRAFT]
**Dernière mise à jour** : 27 septembre 2026
**Dépend de** : `../../VERSIONING.md`
**Format** : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`prism-vX.Y.Z`).

---

## [Unreleased]

### Added
- Plugin Unreal **PRISM-LDK** (`prism/LDK/Plugins/PrismLdk/`, module `PrismLdk`) : `UPrismLdkSubsystem` (WebSocket `5180` + contrôle HTTP `5181`), types, fonctions et événements Blueprint (`OnWorldInitialized`, `OnSnapshot`, `OnWorldDelta`, `OnSyneEvent`, `OnControlResult`, `OnError`).
- Hôte technique de développement/build `prism/LDK/LDK.uproject` (Unreal 5.8, `EngineAssociation` `5.8`) avec sa configuration `Config/` et ses assets Blueprint de test.
- Guide d'intégration Blueprint du plugin : `PRISM_UNREAL_IMPLEMENTATION.md`.
- Règles de normalisation des fins de ligne et de traitement des binaires Unreal (`.uasset`, `.umap`) dans `.gitattributes`.
- **ADR-002** — Unreal Engine 5.8 et plugin PRISM-LDK pour PRISM ([Accepted]).

### Changed
- Dossier de documentation réaligné sur PRISM, projet Unreal final de LIVEX, et son plugin PRISM-LDK.
- Clarification du rôle de SYNE comme moteur décisionnel et autorité de l'état, et du périmètre mince C++/Blueprint du plugin.
- Contrats de transport documentés selon l'état courant : `world_initialized`, snapshot global par tick, deltas/événements, cycle de contrôle `prepare`/`ready`/`start`/`pause`/`resume`/`stop`/`reset` et lecture du statut.
- Anciennes instructions d'implémentation Godot remplacées par le périmètre du plugin et du projet PRISM ; les spécifications visuelles sont recadrées en objectifs de présentation, sans prétendre que les fonctions sont déjà implémentées.
- Rôle et limites de `syne-mock` documentés : outil de développement des contrats, sans équivalence avec le moteur SYNE.
- **ADR-001** (choix Godot) conservée comme décision historique et marquée [Superseded] par l'ADR-002.
- Feuille de route PRISM réécrite en étapes d'évolution orientées intégration et validation, avec une section de risques explicite.

### Historical
- Les versions antérieures de ces documents décrivaient un prototype Godot et des intentions de rendu. Elles ne décrivent plus la plateforme ni l'implémentation actuelles. Le prototype est archivé dans `../docs_prototype/`.

## [0.0.0] — à venir

Version initiale (prototype Godot V1/V2 de la Monographie, [HÉRITÉ]).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 27 septembre 2026 | Dossier PRISM réaligné sur Unreal + PRISM-LDK ; ADR-002 créée | Adoption d'Unreal (étape 1 de la feuille de route) |
| 27 septembre 2026 | Plugin PRISM-LDK et hôte technique de build | Première implémentation exécutable |
| 17 septembre 2026 | Création | Documentation V0.1 |
