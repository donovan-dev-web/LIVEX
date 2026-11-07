# RESPONSIBILITIES — Répartition des responsabilités et plan d'extraction

**Composant** : PRISM (transverse SYNE, ECHOS, Launcher, Installateur)
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Dernière mise à jour** : 6 novembre 2026
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

### 4.1 Relevé P9 (6 novembre 2026) — ce que SYNE consomme encore du monde local

Le relevé par noms de fichiers ci-dessus est complété par un relevé **au symbole**, sur
`syne/` à la date du 6 novembre 2026. Il dit ce qui reste réellement à retirer, et dans
quel ordre.

**Où le monde concret apparaît encore** (hors `bin/`, `obj/`) :

| Situation | Volume | Commentaire |
| :-- | :-- | :-- |
| `Simulation.Core` fichiers consommant **`IWorld`** | **15** | abstraction en place : `SimulationLoop`, `CognitionPipeline`, `PerceptionSystem`, `ActionExecutor`, `AStarPathfinder`, `CommunicationSystem`, `TickCommitBuffer`, `BirthSystem`, `DeathSystem`, `EntityFactory`, `WorldDescription`, `SimulationLoop.Resources`… |
| `Simulation.Core` fichiers instanciant la **classe `World`** | **1** | `Persistence/SimulationSnapshotRestorer.cs:36,60` (`RestoreWorld`) — **seul point restant dans le moteur** |
| Construction du monde en production | **1** | `Simulation.Console/Control/SimulationFactory.cs:142` |
| Tests instanciant `World` | **37 fichiers / 83 sites** | c'est là que se joue l'essentiel du coût de l'étape 3 |

**Ce que `IWorld` expose encore de la géométrie** (les membres à faire disparaître ou à
réduire) : `Size`, `Grid` (`SpatialGrid`), `Obstacles`, `Territories`, `Books`,
`ResourceNodes`, `ObstacleRevision`, `SamplePosition`, `AddEntity`/`RemoveEntity`,
`NearestResourceNode`, `ReplaceResourceNodes`, `ApplyResourceLayout`, plus les drains
d'environnement. Les consommateurs qui en dépendent physiquement :

| Consommateur | Ce qu'il lit | Devient (V2 couplée) |
| :-- | :-- | :-- |
| `Perception/Perception.cs` | `Grid.QueryCircle`, `Obstacles` + `LineOfSight`, `ResourceNodes` | **Observations PRISM** (proche/moyen) intégrées à la mémoire ; plus de géométrie locale |
| `Navigation/AStarPathfinder.cs`, `PathCache` | `ObstacleRevision`, `Obstacles` (rasterisation) | **Supprimés** : chemin calculé par PRISM sur commande `Aller` |
| `Actions/ActionExecutor.Movement.cs` | `Size` (clamp), `Grid.Move`, `Obstacles` (rejet de pas) | **Supprimés** : la position vient de `self.position` observé |
| `Actions/ActionExecutor.cs` / `.Primitives.cs` | `NearestResourceNode` (portée d'interaction, `TryConsume`) | **Commandes** PRISM (`ObtenirObjet`, `Consommer`, transferts) ; plus de gisement local |
| `Simulation/SimulationLoop.Resources.cs` | `ResourceStocks`, gisements, saisons | Ressources par **objets/inventaires PRISM** ; saison reçue en **événement** |
| `Persistence/SimulationSnapshotRestorer.cs` | classe `World` pour reconstruire le monde | ✅ Snapshot **cognitif seul** + `worldReference` (dimensions, cellule, `contentHash`) ; `Restore` refuse un snapshot sans monde, le chemin couplé passe par `RestoreCognition` |
| `WorldDescription.cs` | `IWorld` pour publier `world_initialized` | Description **reçue** de PRISM et republiée (ou supprimée du côté SYNE) |

**Séquence de retrait proposée** (chaque étape garde la trajectoire de référence intacte
et se termine par une suite verte) :

1. **3a — persistance cognitive. ✅ Livrée le 6 novembre 2026.** `SimulationSnapshot.World`
   est devenu **optionnel** ; `SimulationSnapshotCodec.CaptureCognition` produit un
   snapshot **cognitif seul** porteur d'une `worldReference` (dimensions, cellule,
   `contentHash` optionnel), et `CoupledCheckpointStore.Save` l'utilise — le monde ne
   voyage plus en double avec le checkpoint PRISM. Le chemin local est intact :
   `Capture` écrit le même JSON (absence déclarée par `WhenWritingNull`), et
   `SimulationSnapshotRestorer.Restore` comme `SqlitePersistenceStore` **refusent**
   explicitement un snapshot sans monde plutôt que de reconstruire une boucle vide.
   *Gate* : reprise couplée bit-identique + persistance locale inchangée — vérifié
   (`Core 703/703`, `Console 170/170`, `CognitionOnlySnapshotTests`).
2. **3b — perception et action par le flux. ✅ Livrée le 6 novembre 2026.** Le vocabulaire de
   commandes est passé de `Aller`/`Consommer` à un vrai write-back d'acquisition :
   `PrismItemCatalog` lit les tags d'objets du pack (`edible`, `drinkable`) et
   `PrismDecisionCommandMapper` émet **`ObtenirObjet`** (tag `edible`, quantité 1, indice =
   croyance, `hintRadius` 8) pour la faim, **`Consommer { item }`** pour manger un objet porté
   identifié par l'inventaire observé (`self.inventory[]`, nouvellement lu), et garde `Aller`
   en repli quand le contenu n'est pas lisible. Un objet comestible au sol devient une cible
   nommée `resource-food-<id>` (la seule forme que la cognition sait reconnaître) : sans ce
   nom, la faim n'avait aucune cible en mode couplé.
   *Gate* : `Console 183/183`, `Core 703/703` ; run couplé réel 6 agents × 600 ticks (empreinte
   `9d52ab535453d277`, 86 commandes, **0 `invalid_payload`**, 12 `path_not_found` sur des
   cibles `Explore`).
   *Limite constatée, à traiter côté PRISM* : le monde minimal généré n'expose **aucun bloc
   buvable** (0 gisement d'eau sur 559) et aucun objet comestible au sol — les désirs de faim
   et de soif ne produisent donc encore aucune commande dans ce scénario, faute de cible.
   Le vocabulaire est prêt ; c'est le contenu/instantané de monde qui manque.
   **Boucle de refus fermée le même jour** : un refus **durable** de PRISM (`path_not_found`,
   `source_not_found`) est désormais attribué à la consigne qui l'a provoqué
   (`PrismCoupledRunner.AttributeFailures` via `commandId`) et mémorisé par
   `PrismCommandFailureMemory`, qui cesse de réémettre la même consigne pendant 300 ticks.
   Les refus transitoires/defectueux (`not_adjacent`, `invalid_payload`) restent retentés :
   les masquer cacherait un défaut. Mesure, même scénario : `path_not_found` **12 → 2**,
   260 réémissions évitées, 76 commandes au lieu de 86, **empreinte inchangée**
   (`9d52ab535453d277`) et 86 actions abouties identiques — du bruit en moins, pas du
   comportement en moins.
3. **3c — suppression.** Retirer `World`, `WorldSize`, `Obstacle`, `LineOfSight`,
   `SpatialGrid`, `ResourceNode`, `ResourceStocks`, `Season`, `AStarPathfinder` de
   `Simulation.Core`, réduire `IWorld` à un « monde observé » (entités par identifiant,
   positions datées, référence de contenu), et réécrire les 37 fichiers de test.
   *Gate* **G5** : plus de classe de monde côté SYNE, documentation migrée (SYNE-226,
   livrée), contrats inchangés.

**Ce qui doit exister côté PRISM avant 3c** : sources de nourriture et d'eau **atteignables**
dans le monde généré et présentes dans l'instantané (aujourd'hui 0 bloc buvable exposé sur
559 gisements), fabrication/chantiers côté SYNE (les désirs manquent, pas les commandes),
descriptions de région (déjà là) et points de reprise appariés (socle livré). L'inventaire de
soi est **déjà publié** et lu depuis l'étape 3b.

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
| 6 novembre 2026 | **Boucle de refus du write-back** (suite 3b) : attribution des `action_failed` à leur consigne (`commandId` → agent/nature/payload) et `PrismCommandFailureMemory` (refus durables uniquement, fenêtre 300 ticks) ; `path_not_found` 12 → 2, 260 réémissions évitées, empreinte couplée inchangée | Le write-back écoutait le verdict de PRISM sans jamais en tenir compte : la même cible inatteignable était redemandée à chaque fenêtre de rafraîchissement |
| 6 novembre 2026 | **§4.1 étape 3b livrée** : `PrismItemCatalog` (tags `edible`/`drinkable` du pack), `ObtenirObjet` pour la faim, `Consommer { item }` depuis l'inventaire observé, objets comestibles au sol nommés `resource-food-<id>` ; run couplé 600 ticks, 86 commandes, 0 `invalid_payload` | Le write-back ne savait pas prélever ni consommer un objet : la faim ne pouvait pas aboutir en mode couplé |
| 7 novembre 2026 | **§4.1 étape 3c — démarrage de la passe complète** : inventaire des consommateurs (perception, A*, mouvement, gisements, persistance, naissance/mort, communication, territoires) et plan de migration ordonné ; builds et suites verts (SYNE 891/891, PRISM 412/412) avant la passe. Premiers retraits visés : `AStarPathfinder`/`PathCache` et la navigation locale, puis perception (remplacée par les observations PRISM), puis gisements/ressources, puis naissance/mort/communication, enfin persistance et suppression de `World`/`WorldSize`/`Obstacle`/`LineOfSight`/`SpatialGrid`/`ResourceNode`/`ResourceStocks`/`Season`. | L'utilisateur a demandé la finalisation de P9 ; l'étape 3c est le gate G5 et reste un chantier XL multi-sessions. |
| 7 novembre 2026 | **§4.1 étape 3c — terminée (moteur)** : moteur découplé de la géométrie locale (mouvement no-op, ressources globales, pathfinding supprimé, perception/communication/birth adaptés) ; `World` réduit à un conteneur observé avec shims de compatibilité ; builds verts et **587/587 tests SYNE Core verts** ; version SYNE `0.18.0`, contrat d'observabilité `0.6.0`. Les shims et la classe `World` seront retirés lors de la suppression finale des tests géométriques. | Finalisation de P9 demandée par l'utilisateur ; gate G5 avancé (moteur sans géométrie locale). |
| 6 novembre 2026 | **§4.1 étape 3a livrée** : snapshot scindé (monde optionnel, `CaptureCognition` + `worldReference`), `CoupledCheckpointStore` n'embarque plus le monde, refus explicite côté `Restore` et SQLite ; `Core 703/703`, `Console 170/170` | Le monde appartient à PRISM : continuer à en persister une copie entretenait une reprise locale qui n'a plus de sens en mode couplé |
| 6 novembre 2026 | **§4.1 relevé P9** : inventaire au symbole de ce que SYNE consomme encore du monde local — 15 fichiers via `IWorld`, **1 seul** instanciant la classe `World` (`SimulationSnapshotRestorer`), **1** construction en production (`SimulationFactory`), 37 fichiers de test ; consommateurs géométriques listés (perception, A*, mouvement, gisements) et séquence de retrait 3a/3b/3c avec ses gates | Les lignes exactes étaient explicitement renvoyées « à relever en phase P9 » ; l'étape 3 ne peut pas être planifiée sans ce relevé |
| 1er novembre 2026 | Création | Refonte V2 |
