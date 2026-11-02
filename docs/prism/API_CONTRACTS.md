# API_CONTRACTS — Contrat d'interface SYNE↔PRISM

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — TRANSPORT DE RÉFÉRENCE DÉCIDÉ]
**Version de contrat** : `1.0.0` (cible) — remplace la consommation PRISM de `../syne/API_CONTRACTS.md` `0.5.0`
**Dernière mise à jour** : 2 novembre 2026
**Dépend de** : [`adr/ADR-008-contrat-syne-prism-macro-actions.md`](adr/ADR-008-contrat-syne-prism-macro-actions.md), [`SIMULATION_LOOP.md`](SIMULATION_LOOP.md), `../launcher/INTEGRATION_CONTRACT.md`
**Source Monographie** : §2.4

---

## 1. Principes

Le contrat est défini **indépendamment du transport** (ADR-008). Il repose sur **quatre familles de messages** et un **ordre de tick fixe**.

| Famille | Sens | Rôle |
| :-- | :-- | :-- |
| **Commandes** | SYNE → PRISM | Intentions des agents |
| **Observations** | PRISM → SYNE | Ce que chaque agent perçoit |
| **Événements** | PRISM → SYNE | Résultats, échecs, interruptions, effets |
| **Flux de lecture** | PRISM → ECHOS, visualiseur | Instantanés et deltas de chunks, métriques, état des chantiers |

Règles générales :
- JSON `camelCase`, champs `protocolVersion` et `tick` dans chaque message ; un composant **refuse** une version majeure inconnue.
- Identifiants (`agentId`, `regionId`, `blockTypeId`, `itemTypeId`, `recipeId`, `siteId`, `structureId`) **stables pendant un run**.
- Commandes **idempotentes** : chaque commande porte un `commandId` unique ; rejouer un `commandId` déjà exécuté renvoie le résultat stocké sans le dupliquer.
- Un **échec est toujours motivé** (code stable + détail).
- Les champs inconnus sont ignorés (compatibilité ascendante) ; l'absence d'un champ facultatif signifie « non mesuré », jamais `0`.

## 2. Cycle et ordre du tick

```text
SYNE tick t :  lire(observations[t-1], événements[t-1])
             → délibérer → émettre commandes[t]
PRISM tick t:  appliquer commandes[t] → simuler (SIMULATION_LOOP §2)
             → produire observations[t], événements[t]
```

Séquence de préparation (plan de contrôle HTTP + plan de données) :

```text
Prepare(config, seed, contentHash)
 → world_ready(worldVersion, worldChecksum, spawnSites[], calendar, params)
 → SYNE : Incarner(agent…) pour la population initiale
 → Start(maxTicks) → [ tick t ← TickRequest ; TickResult ]*
```

## 3. Commandes (SYNE → PRISM)

Toutes portent `{ commandId, agentId, tick }`.

| Commande | Paramètres | Durée | Échecs fréquents |
| :-- | :-- | :-- | :-- |
| `Aller` | Cible (position ou objet/structure perçue) | Selon la distance | `path_not_found`, `obstacle_appeared`, `target_out_of_world` |
| `Miner` | Bloc | Selon dureté, outil, maîtrise | `tool_tier_insufficient`, `block_not_mineable`, `block_already_mined`, `target_out_of_world`, `not_adjacent`, `not_perceived` |
| `Poser` | Bloc, objet consommé | Courte | `block_not_placeable`, `item_block_mismatch`, `cell_occupied`, `item_missing`, `agent_in_cell`, `target_out_of_world`, `not_adjacent`, `not_perceived` |
| `Fabriquer` | Recette, station (si requise) | Durée de la recette | `input_missing`, `tool_missing`, `station_absent`, `station_busy`, `mastery_insufficient`, `capacity_reached`, `not_adjacent`, `not_perceived`, `unknown_recipe` |
| `Labourer` | Sol | Courte | `soil_unsuitable`, `tool_missing`, `target_out_of_world`, `not_adjacent`, `not_perceived` |
| `Planter` / `Recolter` | Parcelle, graine / culture | Courte | `soil_unsuitable`, `cell_occupied`, `not_ripe`, `no_water`, `item_missing`, `seed_not_plantable`, `capacity_reached`, `crop_not_found`, `target_out_of_world`, `not_adjacent`, `not_perceived` |
| `Prendre` / `Deposer` | Conteneur, objet, quantité | Courte | `access_denied`, `capacity_reached`, `container_empty`, `item_missing`, `not_adjacent`, `not_perceived` |
| `Consommer` | Objet `edible` ou bloc `drinkable` perçu et adjacent | Courte | `item_missing`, `not_edible`, `not_drinkable`, `not_adjacent`, `not_perceived`, `target_out_of_world` |
| `DemarrerChantier` | Gabarit, position, rotation | Courte | `terrain_unsuitable`, `volume_not_clear`, `unknown_template` |
| `Contribuer` | Chantier, matériaux ou travail | Selon l'apport | `site_completed`, `site_destroyed`, `item_missing` |
| `Transferer` | Agent destinataire, objet, quantité | Courte | `refused`, `too_far`, `capacity_reached`, `item_missing`, `not_perceived` |
| `DefinirAcces` | Structure/conteneur, liste, droits | Courte | `not_owner`, `unknown_target` |
| `Incarner` / `Retirer` | Profil, position / raison | Immédiate | `invalid_position`, `agent_exists` |
| `Annuler` | — | Immédiate | `nothing_to_cancel` |

**Macro-actions** (même enveloppe) : `ObtenirObjet{ type|tag, qty, hint }`, `Fabriquer{ recipeId }`, `Cultiver{ parcel }`, `Construire{ templateId, position }`, `Contribuer{ siteId }`. PRISM les décompose en commandes élémentaires sur plusieurs ticks. `Demander`/`Offrir` sont des accords **SYNE-only** ; seul `Transferer` traverse le contrat.

**Transfert entre agents.** Il n'a lieu qu'après un accord conclu côté SYNE ; PRISM l'exécute de façon **atomique** (tout ou rien).

## 4. Événements (PRISM → SYNE)

Chaque événement porte `{ eventId, tick, kind, agentId?, payload }`.

| Événement | Contenu |
| :-- | :-- |
| `action_completed` | `commandId`, résultat (objets obtenus, bloc modifié) |
| `action_failed` | `commandId`, `reason` (code stable §3), `detail` |
| `action_interrupted` | `commandId`, cause (cible disparue, besoin critique signalé par SYNE, conflit perdu) |
| `item_consumed` | `source` (`item` ou `source`), objet ou matériau, **effets** (valeurs de besoins à appliquer par SYNE) |
| `block_changed_nearby` | Position, ancien/nouveau type (dans le champ proche de l'agent) |
| `site_progress` | `siteId`, avancement, matériaux manquants |
| `structure_completed` / `structure_damaged` | `structureId`, gabarit, intégrité |
| `crop_ripe` | Parcelle, position |
| `day_changed` / `season_changed` | Calendrier |
| `agent_status_changed` | `sheltered`, `onPath`, `night` |

**Conflits.** En cas de conflit (§ `SIMULATION_LOOP.md` §4), le perdant reçoit `action_failed` avec `blocked_by_other` ou `target_gone`. Un échec sans raison est interdit par le contrat (sinon le planificateur ne peut pas replanifier).

## 5. Observations (PRISM → SYNE)

### 5.1 Trois niveaux de perception

| Niveau | Contenu | Rayon (départ) |
| :-- | :-- | :-- |
| **Proche** | Objets d'intérêt (blocs `perceivable`, objets au sol, agents, structures, chantiers) avec détail | 16 blocs |
| **Moyen** | **Résumé par région** : masque de tags présents (blocs exposés seulement), présence de structures, sans détail | 3 régions |
| **Lointain** | **Rien** : l'agent s'appuie sur sa mémoire spatiale | — |

Les observations sont envoyées sous forme de **différences** par rapport au tick précédent (apparu / modifié / disparu), pas d'instantanés complets, avec un plafond `maxObservedPerAgent`. L'observation contient aussi l'**état de soi** : position, inventaire, outil en main, maîtrises, `status{ sheltered, onPath, night }`. Le noyau expose actuellement les blocs, objets au sol, agents et masques de tags régionaux ; les structures/chantiers, outil en main, maîtrises et statuts seront ajoutés avec leurs modèles respectifs.

### 5.2 Positions

À chaque tick, PRISM publie les **positions de tous les agents** (compactes) pour que SYNE calcule la communication (rayon 50). La visibilité *perceptive* d'un autre agent reste limitée au champ proche.

## 6. Flux de lecture (PRISM → ECHOS, visualiseur)

| Message | Contenu |
| :-- | :-- |
| `world_snapshot` | À la connexion : métadonnées, calendrier, régions complètes (compressées) |
| `region_delta` | Régions *dirty* du tick, ou liste de modifications ; empreinte de région |
| `entities` | Agents (position, `activeAction`), objets au sol, chantiers, structures |
| `metrics` | Stocks par type, production/consommation par `ruleId`, utilisation des stations et structures, distances transportées, durées de chantier, échecs par raison |
| `checksum` | Empreinte du monde tous les `checksumIntervalTicks` |

Le flux de lecture est **non intrusif** : ECHOS et le visualiseur ne modifient jamais le monde. Une interruption du visualiseur n'interrompt jamais le run.

## 7. Plan de contrôle (HTTP)

Conforme au manifeste Launcher v1 : `GET /health/ready`, `POST /control/prepare`, `/control/start`, `/control/pause`, `/control/resume`, `/control/stop`, `GET /control/status` ; transport HTTP JSON et authentification par jeton de session. La liaison de données utilise WebSocket JSON en lockstep ; le flux de lecture utilise WebSocket JSON. Les campagnes locales appellent directement `IWorldHost` avec les mêmes DTO. Détails : [`adr/ADR-012-transport.md`](adr/ADR-012-transport.md).

## 8. Versionnement et compatibilité

| Cas | Règle |
| :-- | :-- |
| Version majeure inconnue | Refus au `Prepare` avec `protocol_unsupported` |
| Ajout de champ facultatif / nouveau code d'échec | MINOR |
| Retrait ou changement de sémantique | MAJOR |
| Pack de contenu | Le `contentHash` est échangé et vérifié à `Prepare` |

Le contrat est distribué sous forme de **schémas JSON** dans [`schemas/`](schemas/) et de types `Prism.Contracts` ; les tests de contrat s'exécutent sur la liaison réseau et la liaison embarquée avec les mêmes jeux de cas. Les enveloppes lockstep sont versionnées ; les versions de même majeure sont acceptées, les majeures inconnues sont refusées avec `protocol_unsupported`.

## 9. Tailles de départ

| Quantité | Ordre de grandeur (100 agents / 1 000 agents) |
| :-- | :-- |
| Commandes par tick | ≈ 10 / ≈ 100 (les macro-actions réduisent le volume) |
| Positions d'agents par tick | 100 × ~16 o / 1 000 × ~16 o |
| Observations différentielles | À mesurer (B2) ; plafond `maxObservedPerAgent` |

Ces ordres de grandeur sont **à confirmer** par les benchmarks B2/B4.

## Points restés ouverts dans ce document

Q-02 (accès). Le transport de référence est tranché par ADR-012 ; les schémas JSON seront publiés dans `docs/prism/schemas/` en phase P7.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 2 novembre 2026 | Perception et flux de lecture adaptés aux régions 2.5D | ADR-011/012 |
| 2 novembre 2026 | Transport de référence documenté | ADR-012 |
| 1er novembre 2026 | Création | Refonte V2 |
