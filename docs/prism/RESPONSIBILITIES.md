# RESPONSIBILITIES — Répartition des responsabilités et plan d'extraction

**Composant** : PRISM (transverse SYNE, ECHOS, Launcher, Installateur)
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 1er novembre 2026
**Dépend de** : [`adr/ADR-004-refonte-prism-monde-voxel-autoritaire.md`](adr/ADR-004-refonte-prism-monde-voxel-autoritaire.md), `../syne/ARCHITECTURE.md`, `../launcher/INTEGRATION_CONTRACT.md`
**Source Monographie** : —

---

## 1. Principe de partage

> **SYNE décide, PRISM exécute et constate.** SYNE sait ce que l'agent *veut*, *croit* et *se rappelle*. PRISM sait ce qui *est* et ce qui *se passe* physiquement.

## 2. Tableau de répartition

| Domaine | SYNE | PRISM | ECHOS |
| :-- | :-- | :-- | :-- |
| Besoins (faim, soif, fatigue, social…) | **Possède** et applique les effets | Fournit les effets d'un objet consommé (valeurs en données) | Observe |
| Croyances, mémoire, mémoire spatiale | **Possède** | Fournit des perceptions | Observe |
| Désirs, intentions, planification | **Possède** | — | Observe |
| Relations, groupes, confiance, accords, transferts décidés | **Possède** | Exécute atomiquement un transfert accepté | Observe |
| Communication entre agents (pulsations) | **Possède** (messages, rayon 50 blocs) | Fournit les positions de chaque tick | Observe |
| Livres / savoir figé | **Possède** | — | Observe |
| Espace, blocs, chunks, génération | — | **Possède** | Lit |
| Matière : objets, inventaires, conteneurs | — | **Possède** | Lit |
| Corps physique, position, mouvement, navigation | — | **Possède** | Lit |
| Perception géométrique (ce qui est visible) | Reçoit | **Produit** (trois niveaux) | — |
| Temps physique : jour, saison, croissance, péremption | Reçoit des événements | **Possède** | Lit |
| Recettes, chaînes, stations, outils | Connaît (savoir) | **Possède** (règles et exécution) | Lit |
| Structures, chantiers, accès | Décide quoi bâtir | **Possède** (état, effets, listes d'accès) | Lit |
| Groupes comme notion | **Possède** | **Ignore** : ne voit que des identifiants autorisés | Lit |
| Territoires (zones de présence) | Calcule sur positions observées | Fournit positions | Observe |
| Mesures d'analyse (indices, viabilité) | — | — | **Possède** |

## 3. Ce que PRISM ne fait jamais

- Décider du comportement d'un agent, choisir une cible à sa place, ou répondre par une connaissance globale (information locale).
- Connaître la notion de groupe, de confiance ou d'intention.
- Dépendre d'ECHOS ou du visualiseur pour avancer.

## 4. Plan d'extraction du monde hors de SYNE

Le relevé ci-dessous s'appuie sur l'arborescence de `syne/Simulation.Core` à la date de rédaction (noms de fichiers) ; les lignes exactes sont à relever en phase P9 (ISSUES `SYNE-2xx`).

| Élément actuel de SYNE | Devenir |
| :-- | :-- |
| `World/World.cs`, `WorldSize.cs`, `WorldDescription.cs`, `Position.cs` | Retirés. SYNE ne connaît plus que des positions **observées** et des identifiants de chunks |
| `World/Obstacle.cs`, `LineOfSight.cs`, `SpatialGrid.cs` | Retirés (géométrie → PRISM). La perception de proximité d'agents vient des positions de chaque tick ; un index spatial minimal reste côté SYNE pour la communication |
| `World/ResourceNode.cs`, `ResourceStocks.cs` | Retirés. Remplacés par objets, inventaires et conteneurs PRISM |
| `World/Season.cs` | Retiré. La saison est un événement PRISM (`season_changed`) |
| `World/Territory.cs` | Conservé comme **analyse sociale** sur positions observées (aucune géométrie de monde) |
| `World/Book.cs` | Conservé (savoir). Option : véhicule des recettes (variante riche) |
| `Navigation/AStarPathfinder.cs`, `PathCache.cs` | Retirés. Le pathfinding 3D est dans PRISM ; SYNE émet `Aller(cible)` |
| `Perception/Perception.cs` | Réduit à l'intégration des observations dans la mémoire/croyances |
| `Actions/ActionExecutor.Movement.cs`, `ActionExecutor.Primitives.cs` | Remplacés par l'émission de commandes/macro-actions et le traitement des événements |
| Configuration `world.*`, `resources.*` | Migrent vers la configuration PRISM ; SYNE conserve `agents.*`, `communication.*`, `social.*` |
| Persistance SQLite (tables liées au monde) | Les tables du monde migrent vers la persistance PRISM ; un **jeu de points de reprise** couple les deux |
| `Observability/WorldSnapshot.cs` | Allégé : plus de ressources globales ni d'obstacles ; porte état cognitif et référence de monde (empreinte) |
| Calibrations ADR-014/015/016 | **Invalidées** ; à refaire avec les chaînes de production |

Nouveaux éléments SYNE : mémoire spatiale par chunk, planificateur, croyances sur recettes/structures/chantiers, désirs instrumentaux, adaptateur de contrat (`PrismClient`). Détail : [`AGENT_INTERFACE.md`](AGENT_INTERFACE.md).

## 5. Impacts sur les autres composants

| Composant | Impact | Document à mettre à jour |
| :-- | :-- | :-- |
| **SYNE** | Version `0.18.0`, contrat d'observabilité `0.6.0` ; nouvelle boucle (étapes 1 et 11–13 de `SIMULATION_LOOP.md` réécrites) ; DETERMINISM, PERSISTENCE, CONFIGURATION, DATA_MODEL, SYSTEMS_SPEC, API_CONTRACTS, TESTING, PERFORMANCE | `docs/syne/*`, ADR-005 et ADR-017 (temporalité) à relire |
| **ECHOS** | Nouvelle source d'ingestion : flux de lecture PRISM. Le moteur 6 (`ResourceSustainabilityMetrics`) repose aujourd'hui sur `resources` et `resource_consumed` : à refondre sur stocks, production, consommation et usure. Nouveaux indicateurs (coopération, spécialisation, infrastructures) | `docs/echos/METRICS_SPEC.md`, `EMERGENCE_INDICATORS.md`, `DYNAMIC_VIABILITY_INDEX.md`, `API_REST.md` |
| **Launcher** | PRISM headless devient un composant **requis** de toute session (et non seulement du mode Immersion) ; ADR-006 du Launcher (verrou) à réviser ; manifeste PRISM conforme au schéma v1 ; empreinte du pack de contenu ajoutée au format de paquet `.livexp` et à la reproductibilité | `docs/launcher/INTEGRATION_CONTRACT.md` §11, `COMPONENTS.md`, `PACKAGE_FORMAT.md`, `ADR-005/006` |
| **Installateur** | Les paquets PRISM Windows/Linux (Unreal) sont remplacés par des publications .NET autonomes (host + viewer) ; la production « hors CI » d'Unreal (ADR-010 de l'installateur) disparaît | `docs/installer/COMPONENTS.md`, `adr/ADR-010` |
| **Gouvernance** | Phases 4 et 5 de `docs/governance/ROADMAP.md` ; `VERSIONING.md` (tag `prism-v2.0.0`) ; `THIRD-PARTY-NOTICES.md` ; `LIVEX-STATUS.md` | racine et `docs/governance/*` |
| **syne-mock** | Le mock Node.js de SYNE perd son objet (il servait à PRISM-LDK). Remplacé par `Prism.Bots` ; à retirer ou à réorienter | `syne-mock/`, `docs/prism/TESTING.md` |

## 6. Calendrier d'extraction

L'extraction n'est jamais « tout d'un coup » : la phase P9 de [`ROADMAP.md`](ROADMAP.md) procède en trois temps (contrat consommé en parallèle du monde existant → bascule par profil de configuration → suppression du code mort) afin qu'une régression reste attribuable.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
