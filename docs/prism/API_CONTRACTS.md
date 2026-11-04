# API_CONTRACTS — Contrat d'interface SYNE↔PRISM

**Composant** : PRISM
**Statut** : [CONTRAT GELÉ EN 1.0.0 (4 NOVEMBRE 2026) — DTO ET TRANSPORT HOST VALIDÉS]
**Version de contrat** : `1.0.0` (gelée le 4 novembre 2026) — remplace la consommation PRISM de `../syne/API_CONTRACTS.md` `0.5.0`
**Dernière mise à jour** : 4 novembre 2026
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

Toutes portent `{ commandId, agentId, tick }`. Chaque handler valide la forme de son payload avant exécution (`invalid_payload`) : `CommandPayloadValidator` couvre les champs obligatoires documentés ici (coordonnées, identifiants, quantités, listes de droits) et les handlers conservent leurs contrôles métier.

Un agent qui exécute une action à durée (`Miner`, `Fabriquer`, `Labourer`, `Planter`, `Recolter`) reste occupé jusqu'à `tick + durée` : toute action à durée reçue avant l'échéance échoue avec `agent_busy`, après validation des préconditions propres à l'action (un bloc non perçu ou un conteneur non vide reste signalé par son propre code). L'occupation est portée par `ActionDurationTracker`, exposée par `snapshot` et checkpointée avec la fenêtre d'idempotence du dispatch.

Une action à effet différé (`Miner`, `Fabriquer`, `Labourer`, `Planter`, `Recolter`) est enregistrée par le dispatch et résolue au tick `busyUntilTick` : l'événement `action_started` acquitte le début, `action_completed` ou `action_failed` arrive à l'échéance avec le même `commandId`. `Annuler` interrompt une action en attente sans appliquer d'effet et libère l'agent ; l'état en attente est checkpointé avec la fenêtre d'idempotence. Quand l'action vient d'une macro-étape, le dispatch renvoie son résultat à l'étape en attente (canal `DeferredActionResolutions`) : la macro publie `macro_step_deferred` au départ, n'avance pas tant que l'effet n'est pas appliqué, puis valide l'étape avec les quantités réellement produites. Une annulation annule aussi l'étape (`action_failed`, raison `cancelled`).

Chaque tick de cycle de vie applique la décroissance de fraîcheur des inventaires d'agents (1 000 ‰/tick) : une pile dont la fraîcheur atteint zéro est détruite, comptée `spoil` dans le ledger et publiée par un événement `item_spoiled` (`{itemTypeId, quantity}`). Les conteneurs posés suivent la même règle à chaque tick (`container.decayPermillePerTick`, défaut 1 000), avec un taux réduit dans le volume d'une structure `storage` achevée (`granary.decaySlowdownPermille`, 500 ‰ = taux divisé par deux) ; leur péremption publie le même événement `item_spoiled`.

| Commande | Paramètres | Durée | Échecs fréquents |
| :-- | :-- | :-- | :-- |
| `Aller` | `{target:{x,y,z}}` (ou `position`) ; chemin A* calculé à la réception | Un pas par tick | `path_not_found`, `path_budget_exceeded`, `path_interrupted`, `target_out_of_bounds`, `agent_not_found` |
| `Miner` | Bloc | Selon dureté, outil, maîtrise : `action_started` au tick de la commande, effet et `action_completed` à l'échéance | `agent_busy`, `tool_tier_insufficient`, `block_not_mineable`, `block_already_mined`, `target_out_of_world`, `not_adjacent`, `not_perceived` |
| `Poser` | Bloc, objet consommé | Courte | `block_not_placeable`, `item_block_mismatch`, `cell_occupied`, `item_missing`, `agent_in_cell`, `target_out_of_world`, `not_adjacent`, `not_perceived` |
| `Fabriquer` | Recette, station (si requise) | Durée de la recette : `action_started` au tick de la commande, effet et `action_completed` à l'échéance | `agent_busy`, `input_missing`, `tool_missing`, `station_absent`, `station_busy`, `mastery_insufficient`, `capacity_reached`, `not_adjacent`, `not_perceived`, `unknown_recipe` |
| `Labourer` | Sol | 1 tick : `action_started` au tick de la commande, effet et `action_completed` à l'échéance | `agent_busy`, `soil_unsuitable`, `tool_missing`, `target_out_of_world`, `not_adjacent`, `not_perceived` |
| `Planter` / `Recolter` | Parcelle, graine / culture | 1 tick : `action_started` au tick de la commande, effet et `action_completed` à l'échéance | `agent_busy`, `soil_unsuitable`, `cell_occupied`, `not_ripe`, `no_water`, `item_missing`, `seed_not_plantable`, `capacity_reached`, `crop_not_found`, `target_out_of_world`, `not_adjacent`, `not_perceived` |
| `Prendre` / `Deposer` | `{container:{x,y,z}, item, quantity}` | Courte ; `Prendre` applique le taux de décroissance du conteneur avant le retrait | `access_denied`, `capacity_reached`, `container_empty`, `container_not_found`, `container_not_empty`, `item_missing`, `item_spoiled`, `not_adjacent`, `not_perceived` |
| `Consommer` | Objet `edible`, bloc `drinkable` perçu et adjacent, ou volume `water_access` d'une structure achevée où se tient l'agent (le puits rend son volume buvable) | Courte | `item_missing`, `not_edible`, `not_drinkable`, `not_adjacent`, `not_perceived`, `target_out_of_world` |
| `DemarrerChantier` | `{siteId, templateId, origin:{x,y,z}}` (ou `position`) | Courte | `target_out_of_world`, `terrain_unsuitable`, `volume_not_clear`, `unknown_template`, `site_overlaps` |
| `Contribuer` | `{siteId}` ; une unité de travail et les matériaux dus par agent et par tick | Un tick par contribution | `site_not_found`, `site_not_active`, `duplicate_contribution`, `construction_block_obstructed`, `materials_missing`, `access_denied` |
| `Transferer` | Agent destinataire, objet, quantité | Courte | `refused`, `too_far`, `capacity_reached`, `item_missing`, `not_perceived` |
| `DefinirAcces` | `{siteId, rules:[{kind:"agent"\|"group", id, rights:["build", ...]}]}` | Courte | `not_owner`, `unknown_target`, `invalid_payload` |
| `DefinirAccesConteneur` | `{container:{x,y,z}, rules:[{kind:"agent"\|"group", id, rights:["take"\|"put", ...]}]}` | Courte | `not_owner`, `container_not_found`, `not_adjacent`, `not_perceived`, `invalid_payload` |
| `Incarner` / `Retirer` | `Incarner`: `{position:{x,y,z}, profile?}` ; `Retirer`: raison facultative | Immédiate | `invalid_position`, `agent_exists`, `agent_not_found` |
| `Annuler` | — | Immédiate | `nothing_to_cancel` |

**Macro-actions** (même enveloppe) : `ObtenirObjet{ type|tag, qty, hint }`, `Fabriquer{ recipeId }`, `FabriquerMacro{ recipeId }`, `Cultiver{ parcel }`, `Contribuer{ siteId }`. `ObtenirObjet`, `FabriquerMacro`, `Cultiver` et `Annuler` sont dispatchées par le host comme séquences multi-ticks pilotées par la phase `AdvanceMacroActions` ; `Fabriquer` reste l'action à effet différé décrite ci-dessus. La construction passe par `DemarrerChantier{ templateId, position }` puis `Contribuer{ siteId }` : il n'existe pas de kind `Construire` dispatché. `Demander`/`Offrir` sont des accords **SYNE-only** ; seul `Transferer` traverse le contrat.

**Transfert entre agents.** Il n'a lieu qu'après un accord conclu côté SYNE ; PRISM l'exécute de façon **atomique** (tout ou rien).

**Chantiers (socle host).** Avec un pack compilé, `DemarrerChantier`, `Contribuer` et `DefinirAcces` sont dispatchées de façon idempotente dans le tick. Chaque contribution apporte une unité de travail et débite atomiquement de l'inventaire les matériaux dus au prorata du travail ; une contribution refusée ne consomme rien. Le propriétaire et les droits `build` sont contrôlés, et matériaux consommés/manquants, progression, gabarits compilés et blocs posés survivent au checkpoint. La perception des chantiers est raccordée au flux d'observations et un consommateur applique les effets d'abri sous ACL `use` (agents et groupes) ; les autres types d'effets restent à raccorder.

**Actions atomiques (socle host).** Avec un pack compilé, `Miner`, `Poser`, `Fabriquer`, `Labourer`, `Planter`, `Recolter`, `Consommer`, `Transferer`, `Prendre` et `Deposer` sont dispatchées dans le tick. `Miner`, `Fabriquer`, `Labourer`, `Planter` et `Recolter` appliquent leur effet à l'échéance, après validation de leurs préconditions à la résolution ; les autres primitives s'exécutent au tick de la commande. `Poser` crée un inventaire de conteneur pour un objet portant le tag `container` ; la capacité host par défaut est de 24 emplacements. `Prendre`/`Deposer` prennent `{container:{x,y,z}, item, quantity}` et exigent un conteneur perçu et adjacent. Les conteneurs non vides empêchent le minage de leur bloc, et contenus, propriétaire et ACL du noyau survivent au checkpoint ; la configuration des ACL par commande et la péremption des piles sont raccordées, et les macro-actions attendent la résolution de l'action demandée avant d'avancer.

**Cycle de vie (socle host).** `Incarner` prend `agentId` dans l'enveloppe et une position `x/y/z` en cellules de grille (z = niveau des pieds) ; `profile` accepte `speedPermille` et `carrySlots`, avec valeurs par défaut du noyau. `Retirer` dépose l'inventaire au sol et les piles identiques de même taille se regroupent. `Aller` prend `{target:{x,y,z}}`, calcule un chemin A* à la réception puis avance d'une case par tick ; l'arrivée émet `action_completed` avec `result{completed:true}` et une interruption émet `action_failed` avec `path_interrupted`. Ordres de déplacement et position sont checkpointés. Ces commandes sont idempotentes, leurs entités et objets au sol apparaissent dans le flux de lecture et leur état survit aux checkpoints ; cela ne constitue pas encore une observation filtrée par agent.

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

Les observations sont envoyées sous forme de **différences** par rapport au tick précédent (apparu / modifié / disparu), pas d'instantanés complets, avec un plafond `maxObservedPerAgent`. L'observation contient aussi l'**état de soi** : position, inventaire, outil en main, maîtrises, `status{ sheltered, onPath, night }`. Le noyau expose les blocs, objets au sol, agents, chantiers et masques de tags régionaux ; une entrée `ConstructionSite` porte `siteId`, état (`planned`/`in_progress`/`completed`), travail requis et achevé, et suit le même diff apparu/modifié/disparu. Le statut `sheltered` et le bonus de récupération sont calculés à partir des effets actifs, sous réserve du droit `use` de la structure ; l'outil en main, les maîtrises, `onPath` et `night` restent à raccorder.

### 5.2 Positions

À chaque tick, PRISM publie les **positions de tous les agents** (compactes) pour que SYNE calcule la communication (rayon 50). La visibilité *perceptive* d'un autre agent reste limitée au champ proche.

## 6. Flux de lecture (PRISM → ECHOS, visualiseur)

| Message | Contenu |
| :-- | :-- |
| `world_snapshot` | À la connexion : métadonnées, régions complètes, empreinte et, si disponibles, agents/inventaires, objets au sol, chantiers, intégrité et effets actifs |
| `region_delta` | État complet de chaque région modifiée (colonnes et surcharges, permettant aussi les suppressions), tick et empreintes de région/monde |
| `entity_snapshot` | Après `Incarner`/`Retirer` : agents (position, profil, inventaire) et objets au sol |
| `tick_result` | Résultat du tick : observations par agent, événements, positions et objets au sol ; consommé par `PrismReadStreamClient` et `ViewerLiveSession` |
| `entities` | Cible future, non émis : agents (`activeAction`), chantiers et structures ; agents et objets au sol sont couverts par `entity_snapshot` et `tick_result` |
| `metrics` | `{tick, stocks[], matter[], structures[], reservedStations, activeStructureEffects, failures[]}` : stocks par type (agents, conteneurs, sol), production/consommation par `ruleId`, avancement/intégrité/effets par chantier, réservations de stations et échecs de commande par raison. Publié à la même cadence que `checksum` (100 ticks par défaut). |
| `checksum` | Empreinte du monde tous les `checksumIntervalTicks` |

Le flux de lecture est **non intrusif** : ECHOS et le visualiseur ne modifient jamais le monde. Une interruption du visualiseur n'interrompt jamais le run.

Le host émet `checksum` et `metrics` tous les 100 ticks par défaut (intervalle configurable dans `PrismHostRuntime`) ; l'empreinte porte sur l'état courant de la grille et ses métadonnées de génération. La route HTTP `/metrics` expose les mêmes agrégats au format Prometheus (stocks, matière créée/détruite, échecs par raison, réservations et effets actifs).
Chaque enveloppe porte `protocolVersion`, `sequence`, `tick`, `kind` et `payload`. La séquence est strictement croissante pour le host, mais un lecteur peut observer un saut lorsque le host remplace une file saturée par un nouveau `world_snapshot` complet. ECHOS accepte un démarrage sur un tel snapshot et un saut uniquement lorsqu'il est suivi d'un snapshot complet ; les messages différentiels après ce point doivent reprendre sans autre saut. ECHOS v8 conserve les enveloppes reçues et leur `contentHash` sans les projeter en métriques SYNE non présentes dans le contrat.
L'adaptateur SYNE consomme également ces enveloppes via `PrismReadStreamClient`; les enveloppes `tick_result` exposent les observations agent/tick sans les traduire encore en croyances. Le lecteur accepte les resynchronisations par snapshot et refuse une version majeure ou un message incohérent. Le visualiseur dispose du même socle (`ViewerReadStreamClient`/`ViewerLiveSession`, resynchronisation comprise) ; l'option `--connect` n'est pas encore exposée par son CLI.

## 7. Plan de contrôle (HTTP)

Le host expose la readiness et le cycle de contrôle HTTP authentifié ; le lockstep et la lecture utilisent WebSocket JSON. Il lie actuellement le serveur à `127.0.0.1`. Avec `contentDirectory`, `Prepare` compile le pack et génère le monde ; `Incarner`/`Retirer` mettent à jour les agents/objets au sol, les observations différentielles par agent sont émises à la phase perception du tick, et les snapshots d'entités sont diffusés aux lecteurs. Les snapshots incluent aussi chantiers, intégrité et effets actifs ; `Contribuer` débite atomiquement les matériaux requis. Après une mutation de grille, `region_delta` publie l'état complet des régions concernées et leurs empreintes ; les checksums périodiques sont émis. Les métriques sont publiées dans le flux et exposées par `/metrics` ; la perception des chantiers et le consommateur d'effets d'abri sont raccordés, la compression du flux reste à implémenter. Détails : [`adr/ADR-012-transport.md`](adr/ADR-012-transport.md).

## 8. Versionnement et compatibilité

> **Contrat gelé en `1.0.0` le 4 novembre 2026** (PRISM-081) : `ProtocolVersion.Current` vaut `1.0.0` ; la matrice contractuelle `CommandMatrixContractTests` couvre les 21 kinds de commande sur les trois liaisons (23/23 verts) et la campagne dorée réduite de `Prism.Bots.Tests` a produit des empreintes identiques avant et après l'optimisation du tick. Tout changement ultérieur du vocabulaire, des payloads ou de la sémantique est une **version MAJOR en pré-release** ; un ajout de champ facultatif reste MINOR.

| Cas | Règle |
| :-- | :-- |
| Version majeure inconnue | Refus du message avec `protocol_unsupported` avant traitement ; HTTP `400` et erreur WebSocket |
| Ajout de champ facultatif / nouveau code d'échec | MINOR |
| Retrait ou changement de sémantique | MAJOR |
| Pack de contenu | Le `contentHash` est échangé et vérifié à `Prepare` |

Le contrat est distribué sous forme de **schémas JSON** dans [`schemas/`](schemas/) et de types `Prism.Contracts` ; les tests de contrat s'exécutent sur la liaison réseau et la liaison embarquée avec les mêmes jeux de cas. Les enveloppes lockstep sont versionnées ; les versions de même majeure sont acceptées, les majeures inconnues sont refusées avec `protocol_unsupported`. Le host applique également cette règle aux routes HTTP de données et au lockstep WebSocket ; les champs inconnus restent ignorés.

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
| 4 novembre 2026 | Péremption des conteneurs à chaque tick, ralentie dans le volume d'une structure `storage` ; `item_spoiled` est aussi publié pour les conteneurs (même payload) | PRISM-098 |
| 4 novembre 2026 | `Consommer` accepte en plus le volume `water_access` d'une structure achevée : même payload, même événement `item_consumed` (canal `source`, matériau d'eau du pack) — ajout compatible MINOR, aucun champ nouveau | PRISM-096 |
| 4 novembre 2026 | Contrat gelé en `1.0.0` : `ProtocolVersion.Current = 1.0.0`, matrice 23/23 sur les trois liaisons, empreintes de run identiques avant/après l'optimisation du tick | PRISM-081 |
| 3 novembre 2026 | Dispatch des macro-actions (`ObtenirObjet`, `FabriquerMacro`, `Cultiver`, `Annuler`), `DefinirAccesConteneur`, `tick_result` et client de lecture du viewer documentés ; `Construire` retiré du vocabulaire dispatché | Audit de cohérence avec le code |
| 2 novembre 2026 | Erreurs de version majeure cohérentes entre HTTP et WebSocket documentées | PRISM-078 |
| 2 novembre 2026 | Implémentation partielle du host HTTP/WebSocket documentée | P7 |
| 2 novembre 2026 | Perception et flux de lecture adaptés aux régions 2.5D | ADR-011/012 |
| 2 novembre 2026 | Transport de référence documenté | ADR-012 |
| 1er novembre 2026 | Création | Refonte V2 |
