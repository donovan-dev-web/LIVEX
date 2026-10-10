# CHANGELOG — PRISM

**Composant** : PRISM
**Statut** : [DRAFT]
**Dernière mise à jour** : 7 octobre 2026
**Dépend de** : `../../VERSIONING.md`
**Format** : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`prism-vX.Y.Z`).

---

## [Unreleased]

### Added
- **Contrat d'échelle temporelle ADR-017 (spec « Profil gameplay PRISM », 10/10/2026)** : `world_initialized.world` (description **1.1**) porte `simulatedSecondsPerTick` (défaut 60 → 5 pour le profil `prism`) et `metersPerUnit` (défaut 1,0 — PRISM k = 100 uu/unité) ; chaque `snapshot` porte `simulatedTimeSeconds` (`tick × simulatedSecondsPerTick`) à côté de `simulatedTimeMinutes` (plancher entier, inchangé) ; `/api/control/status` expose `simulatedSecondsPerTick`. Exigences de reconstruction du plugin (§6 de la spec) documentées dans `TRANSPORT_API.md` : **aucune valeur temporelle/ spatiale en dur** — lire `cellSize`, `ticksPerSecond`, `simulatedSecondsPerTick`, `metersPerUnit` depuis `world_initialized`, `k = tuile_WP / cellSize`, `MaxWalkSpeed = speed × k × TPS`, horloge UI affichant `simulatedTimeSeconds` **et** le ratio R.
### Added
- Plugin Unreal **PRISM-LDK** (`prism/LDK/Plugins/PrismLdk/`, module `PrismLdk`) : `UPrismLdkSubsystem` (WebSocket `5180` + contrôle HTTP `5181`), types, fonctions et événements Blueprint (`OnWorldInitialized`, `OnSnapshot`, `OnWorldDelta`, `OnSyneEvent`, `OnControlResult`, `OnError`).
- Hôte technique de développement/build `prism/LDK/LDK.uproject` (Unreal 5.8, `EngineAssociation` `5.8`) avec sa configuration `Config/` et ses assets Blueprint de test.
- Guide d'intégration Blueprint du plugin : `PRISM_UNREAL_IMPLEMENTATION.md`.
- Règles de normalisation des fins de ligne et de traitement des binaires Unreal (`.uasset`, `.umap`) dans `.gitattributes`.
- **ADR-002** — Unreal Engine 5.8 et plugin PRISM-LDK pour PRISM ([Accepted]).
- **`SCALE_AND_CADENCE_SPEC.md`** — correspondance spatiale et temporelle SYNE ↔ Unreal : échelle `k = 100 uu/unité` (1 unité = 1 m), cadence `ticksPerSecond = 6` (parité vitesse joueur/agent aux défauts Unreal), monde `2 240 × 2 240` unités (≈ 5 km²) en cases de 32 (cellule World Partition 3200 uu par défaut), densité sociale, environnement complet (forêt/rivière/montagne/côte) et checklist de validation.
- **ADR-003** — Échelle et cadence SYNE ↔ Unreal (k = 100, TPS 6, monde ≈ 5 km²) ([Proposed]).

### Changed
- Dossier de documentation réaligné sur PRISM, projet Unreal final de LIVEX, et son plugin PRISM-LDK.
- Clarification du rôle de SYNE comme moteur décisionnel et autorité de l'état, et du périmètre mince C++/Blueprint du plugin.
- Contrats de transport documentés selon l'état courant : `world_initialized`, snapshot global par tick, deltas/événements, cycle de contrôle `prepare`/`ready`/`start`/`pause`/`resume`/`stop`/`reset` et lecture du statut.
- Anciennes instructions d'implémentation Godot remplacées par le périmètre du plugin et du projet PRISM ; les spécifications visuelles sont recadrées en objectifs de présentation, sans prétendre que les fonctions sont déjà implémentées.
- Rôle et limites de `syne-mock` documentés : outil de développement des contrats, sans équivalence avec le moteur SYNE.
- **ADR-001** (choix Godot) conservée comme décision historique et marquée [Superseded] par l'ADR-002.
- Feuille de route PRISM réécrite en étapes d'évolution orientées intégration et validation, avec une section de risques explicite.
- `PRISM_UNREAL_IMPLEMENTATION.md` : la conversion de positions et la grille de tuiles renvoient à `SCALE_AND_CADENCE_SPEC.md` (convention retenue `k = 100`, tuile 3200 uu) au lieu de l'échelle illustrative de 100 uu ; le tableau de cadence ajoute la ligne d'immersion `TPS = 6`.

### Historical
- Les versions antérieures de ces documents décrivaient un prototype Godot et des intentions de rendu. Elles ne décrivent plus la plateforme ni l'implémentation actuelles. Le prototype était archivé dans `../docs_prototype/` — répertoire retiré depuis par la revue documentaire V0.1 (PR #502).

## [0.0.0] — à venir

Version initiale (prototype Godot V1/V2 de la Monographie, [HÉRITÉ]).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 7 octobre 2026 | `SCALE_AND_CADENCE_SPEC.md` + ADR-003 ([Proposed]) ; conventions de conversion et de cadence dans `PRISM_UNREAL_IMPLEMENTATION.md` | Parité vitesse joueur/agent sur un monde ≈ 5 km² |
| 27 septembre 2026 | Dossier PRISM réaligné sur Unreal + PRISM-LDK ; ADR-002 créée | Adoption d'Unreal (étape 1 de la feuille de route) |
| 27 septembre 2026 | Plugin PRISM-LDK et hôte technique de build | Première implémentation exécutable |
| 17 septembre 2026 | Création | Documentation V0.1 |
