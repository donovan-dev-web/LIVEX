# API_CONTRACTS — Contrat d'interface SYNE↔PRISM

**Composant** : PRISM
**Statut** : [SPÉCIFICATION V2 — NON IMPLÉMENTÉ]
**Version de contrat** : `1.0.0` (cible) — remplace la consommation PRISM de `../syne/API_CONTRACTS.md` `0.5.0`
**Dernière mise à jour** : 1er novembre 2026
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
- Identifiants (`agentId`, `chunkId`, `blockTypeId`, `itemTypeId`, `recipeId`, `siteId`, `structureId`) **stables pendant un run**.
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
| `Miner` | Bloc | Selon dureté, outil, maîtrise | `tool_tier_insufficient`, `block_already_mined`, `not_adjacent`, `not_perceived` |
| `Poser` | Bloc, objet consommé | Courte | `cell_occupied`, `item_missing`, `agent_in_cell`, `not_adjacent` |
| `Fabriquer` | Recette, station | Durée de la recette | `input_missing`, `station_absent`, `station_busy`, `mastery_insufficient`, `unknown_recipe` |
| `Planter` / `Recolter` | Parcelle, graine / culture | Courte | `soil_unsuitable`, `not_ripe`, `no_water` |
| `Prendre` / `Deposer` | Conteneur, objet | Courte | `access_denied`, `capacity_reached`, `container_empty` |
| `Consommer` | Objet ou bloc `drinkable` | Courte | `item_missing`, `not_edible` |
| `DemarrerChantier` | Gabarit, position, rotation | Courte | `terrain_unsuitable`, `volume_not_clear`, `unknown_template` |
| `Contribuer` | Chantier, matériaux ou travail | Selon l'apport | `site_completed`, `site_destroyed`, `item_missing` |
| `Transferer` | Agent destinataire, objet | Courte | `refused`, `too_far`, `capacity_reached` |
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
| `item_consumed` | Objet, **effets** (valeurs de besoins à appliquer par SYNE) |
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
| **Moyen** | **Résumé par chunk** : masque de tags présents (blocs exposés seulement), présence de structures, sans détail | 3 chunks |
| **Lointain** | **Rien** : l'agent s'appuie sur sa mémoire spatiale | — |

Les observations sont envoyées sous forme de **différences** par rapport au tick précédent (apparu / modifié / disparu), pas d'instantanés complets, avec un plafond `maxObservedPerAgent`. L'observation contient aussi l'**état de soi** : position, inventaire, outil en main, maîtrises, `status{ sheltered, onPath, night }`.

### 5.2 Positions

À chaque tick, PRISM publie les **positions de tous les agents** (compactes) pour que SYNE calcule la communication (rayon 50). La visibilité *perceptive* d'un autre agent reste limitée au champ proche.

## 6. Flux de lecture (PRISM → ECHOS, visualiseur)

| Message | Contenu |
| :-- | :-- |
| `world_snapshot` | À la connexion : métadonnées, calendrier, chunks complets (compressés) |
| `chunk_delta` | Chunks *dirty* du tick, ou liste de modifications ; empreinte de chunk |
| `entities` | Agents (position, `activeAction`), objets au sol, chantiers, structures |
| `metrics` | Stocks par type, production/consommation par `ruleId`, utilisation des stations et structures, distances transportées, durées de chantier, échecs par raison |
| `checksum` | Empreinte du monde tous les `checksumIntervalTicks` |

Le flux de lecture est **non intrusif** : ECHOS et le visualiseur ne modifient jamais le monde. Une interruption du visualiseur n'interrompt jamais le run.

## 7. Plan de contrôle (HTTP)

Conforme au manifeste Launcher v1 : `GET /health/ready`, `POST /control/prepare`, `/control/start`, `/control/pause`, `/control/resume`, `/control/stop`, `GET /control/status` ; authentification par jeton de session. Détails d'implémentation : ISSUES `PRISM-0xx` (phase P7).

## 8. Versionnement et compatibilité

| Cas | Règle |
| :-- | :-- |
| Version majeure inconnue | Refus au `Prepare` avec `protocol_unsupported` |
| Ajout de champ facultatif / nouveau code d'échec | MINOR |
| Retrait ou changement de sémantique | MAJOR |
| Pack de contenu | Le `contentHash` est échangé et vérifié à `Prepare` |

Le contrat est distribué sous forme de **schémas JSON** et de types `Prism.Contracts` ; les tests de contrat s'exécutent sur la liaison réseau et la liaison embarquée avec les mêmes jeux de cas.

## 9. Tailles de départ

| Quantité | Ordre de grandeur (100 agents / 1 000 agents) |
| :-- | :-- |
| Commandes par tick | ≈ 10 / ≈ 100 (les macro-actions réduisent le volume) |
| Positions d'agents par tick | 100 × ~16 o / 1 000 × ~16 o |
| Observations différentielles | À mesurer (B2) ; plafond `maxObservedPerAgent` |

Ces ordres de grandeur sont **à confirmer** par les benchmarks B2/B4.

## Points restés ouverts dans ce document

Q-01 (transport), Q-02 (accès). Les schémas JSON seront publiés dans `docs/prism/schemas/` en phase P7.

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 1er novembre 2026 | Création | Refonte V2 |
