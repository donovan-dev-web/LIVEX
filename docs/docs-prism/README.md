# PRISM — Perceptual Rendering & Interactive Simulation Module

**Composant** : PRISM
**Implémentation actuelle** : projet Unreal PRISM intégrant le plugin PRISM-LDK (`PrismLdk`)
**Dernière mise à jour** : 27 septembre 2026

---

## Rôle et intégration

**LIVEX** (*Living Intelligent Virtual Ecosystem eXperience*) est le projet
complet. **PRISM** en est le projet Unreal final pour la représentation et
l'interaction visuelle. PRISM intègre **PRISM-LDK** (*LIVEX Development Kit*),
le plugin Unreal situé actuellement dans `prism/LDK/Plugins/PrismLdk/`
(nom de module Unreal : `PrismLdk`). Il expose les types, fonctions et
événements Blueprint pour se connecter à SYNE et consommer ses contrats.
LIVEX n'est pas un projet séparé qui viendrait après PRISM.

Dans ce checkout, `prism/LDK/LDK.uproject` est un hôte Unreal technique fourni
pour compiler et tester PRISM-LDK. Le nom du fichier et du dossier ne désigne
pas le projet complet LIVEX ni un second projet produit.

**SYNE reste l'unique moteur décisionnel et l'autorité de l'état simulé.**
PrismLdk est un adaptateur d'intégration : il ne décide pas du comportement des
entités et ne doit pas devenir un moteur de simulation parallèle. Le code C++
du plugin reste mince et se limite aux contrats, au transport, à leur
conversion en types Blueprint et à la diffusion d'événements/résultats.

## Flux en bref

- WebSocket `ws://127.0.0.1:5180/` : réception de `world_initialized`, du
  snapshot global de chaque tick, de `world_delta` et des événements.
- HTTP `http://127.0.0.1:5181` : commandes de préparation, confirmation,
  démarrage, pause, reprise, arrêt, réinitialisation et lecture du statut.
- Les snapshots sont la source de vérité de l'état dynamique ; les événements
  servent aux notifications et effets ponctuels. Éviter de traiter deux fois
  une mutation présente dans les deux flux.

Voir [TRANSPORT_API.md](TRANSPORT_API.md) pour les contrats et
[PRISM_UNREAL_IMPLEMENTATION.md](PRISM_UNREAL_IMPLEMENTATION.md) pour le guide
d'intégration Blueprint et les conventions de projection du monde.

## Documents

| Document | Rôle |
| :-- | :-- |
| `VISION.md` | Rôle, frontières et responsabilité de SYNE |
| `ARCHITECTURE.md` | Projet PRISM, plugin PRISM-LDK et flux d'intégration |
| `SCENE_SPEC.md` | Responsabilités de PRISM pour le monde présenté |
| `TRANSPORT_API.md` | WebSocket et HTTP SYNE |
| `PRISM_UNREAL_IMPLEMENTATION.md` | Guide d'intégration PRISM-LDK et Blueprint |
| `RENDERING_SPEC.md` | Objectifs de représentation visuelle |
| `VISUALIZATION_SPEC.md` | Vues d'inspection, sociales et communication |
| `UX_INTERACTION.md` | Intentions d'interaction et d'interface |
| `ASSETS_CONVENTIONS.md` | Principes de présentation des éléments |
| `TESTING.md` | Validation du plugin et de l'intégration |
| `ROADMAP.md` | Étapes d'évolution |
| `CHANGELOG.md` | Historique de cette documentation |
| `adr/` | Décisions historiques et références transverses |

Les spécifications visuelles décrivent des objectifs de rendu, pas des
classes Unreal imposées ni des fonctionnalités générées automatiquement par
le plugin.
