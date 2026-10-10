# API_CONTRACTS.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 24 septembre 2026
**Dépend de** : `../COMMUNICATION.md`, `DATA_MODEL.md`
**Source Monographie** : §2.4 (contrats de transport), §5.4 (PRISM), §3.24 (événements), ADR-003/ADR-004

---

## 1. Objectif

Définit les **contrats de données** exposés par le moteur .NET SYNE aux
consommateurs, dont ECHOS et PRISM (projet Unreal) via le plugin PRISM-LDK
(`PrismLdk`). Les
consommateurs affichent ou pilotent le moteur ; SYNE reste l'autorité pour la
simulation et les décisions. Toute évolution est gérée par `../../VERSIONING.md`.

Le service séparé `../../syne-mock/` simule une partie de ces contrats et flux
pour développer le plugin sans démarrer le moteur. Il facilite l'intégration,
mais n'est ni SYNE ni une référence d'équivalence algorithmique ; il ne définit
pas les résultats attendus de la simulation réelle.

## 2. Contrat temps réel — WebSocket (5180 par défaut)

Avant tout `snapshot`, SYNE émet `world_initialized` avec `version`, `seed` et
`world`. La description versionnée contient `width`, `height`, `cellSize`,
`ticksPerSecond`, `simulatedSecondsPerTick`, `metersPerUnit`, `cellCountX`,
`cellCountY`, `agents[]`, `cells[]`,
`obstacles[]`, `resources[]` et `regions[]`.
**Version de description 1.1 (ADR-017, contrat 0.4.0)** : `simulatedSecondsPerTick`
(entier, 60 par défaut — 1 tick = 1 minute simulée ; 5 pour le profil `prism`)
et `metersPerUnit` (double informatif, défaut 1,0) sont **additifs** ; les champs
antérieurs sont inchangés et une référence à `"1.0"` reste acceptée par les
consommateurs qui comparent les versions. Chaque obstacle initial contient
`id`, `x`, `y` et `radius`; `cells[].obstacles[]` référence aussi les ID qui
recouvrent la cellule.
Chaque agent initial expose `id`, `species` et `position{x,y}` ; ces agents et
positions sont ceux du monde préparé et sont réutilisés pour le premier tick.
`world.resources[]` décrit les emplacements initiaux à instancier : trois
emplacements déterministes par type (`food`, `water`, `wood`, `mineral`) quand
la grille a au moins trois cellules, avec `id`, `kind`, `x`, `y` (indices de
cellule) et `quantity`. Ces points de placement ne sont pas des stocks de nœuds
dynamiques : les ressources simulées restent les réserves globales par type
exposées dans les snapshots.
Chaque cellule expose `x`, `y`, `terrainType`, `walkable`, `height`, `movementCost` et
`obstacles[]`. Le layout est déterministe pour un seed. Les changements
d'obstacles/saisons/territoires existants restent les deltas applicables par
Unreal (`world.construction_placed` et `world.construction_removed` compris).
SYNE n'a actuellement pas de configuration de biomes ou types de terrain :
`terrainType` vaut `plains` pour toutes les cellules. L'obstacle initial est
configurable via `world.obstacles` et `world.obstacleLayout`.
`POST /api/control/prepare` accepte `seed` et le champ optionnel
`ticksPerSecond` (entier strictement positif). La seed et la cadence sont
conservées avec le monde préparé ; la cadence est renvoyée dans
`world.ticksPerSecond` et utilisée pour les ticks du run qui suit. Après un
prepare explicite, `start` réutilise ce monde : un seed différent ou une
nouvelle configuration doit être envoyé via un nouveau `prepare`, sinon la
requête est refusée (409).

Le moteur émet exactement un message global `snapshot` par tick. Ce snapshot
est l'état dynamique autoritaire complet du tick : tous les agents, les stocks,
les obstacles, les groupes, les territoires et les livres actifs, plus
`worldChanges[]` et `actions[]` pour les mutations et actions exécutées pendant
ce tick. La topologie statique (`cells[]`, terrain et dimensions) est envoyée
une fois dans `world_initialized` ; les cellules modifiées doivent être
reconstruites à partir de `worldChanges[]` et des obstacles complets du snapshot.
Les événements `decision_made`, `action_completed` et `world_delta` restent
émis séparément pour compatibilité/diagnostic, mais les consommateurs qui
mettent à jour le monde doivent traiter le snapshot comme source de vérité et
éviter d'appliquer deux fois ses mutations.

Transport : WebSocket local, **trames texte UTF-8 contenant du JSON** (`camelCase`).
SYNE envoie `WebSocketMessageType.Text`, jamais une trame binaire. Deux types de
messages (Monographie §5.4.1) :

`runId` dans les snapshots est l'identité de contenu consommée par ECHOS. Elle
est opaque et stable pendant le run. Depuis la **calibration D1 (contrat 0.2.1)**,
les runs pilotés utilisent le **format canonique `run-<seed>-<12hex>`** : le seed
reste lisible dans l'identifiant (repli de dérivation ECHOS sur les flux ≤ 0.2.0)
et le suffixe hexadécimal garantit l'unicité entre deux runs de même seed (le
format `run-<seed>` collisionnait les runs successifs côté stockage ECHOS).
La réponse HTTP à `start`/`status` expose ce même identifiant pour permettre au
client de corréler le pilotage et le flux.

> **Implémentation actuelle (SYNE-080, contrat 0.2.1)** : émetteur BCL (HttpListener + `AcceptWebSocketAsync`,
> zéro dépendance) dans `Simulation.Console`, activé par `--observe` (port `--observe-port`, défaut 5180,
> bind `127.0.0.1`). Chaque tick émet **1 snapshot global complet + 1 `tick_summary` + 1 `decision_made` + 1
> `action_completed` par entité, + événements de communication dès qu'un message circule**,
> diffusion à **tous** les consommateurs connectés. L'émission
> n'ajoute aucun tirage PRNG (déterminisme inchangé, DETERMINISM.md §3).

### 2.1 `snapshot` — WorldSnapshot

| Champ | Type | Description |
| :-- | :-- | :-- |
| `version` | string | Version du contrat (SemVer) |
| `engineVersion` | string | Version du moteur (DETERMINISM.md §3.6.2) — identifie les règles du run |
| `runId` | string | Identifiant du run (format canonique piloté : `run-<seed>-<12hex>`) |
| `seed` | uint | **0.2.1 (additif, rétro-compatible)** — seed effectif du run. ECHOS n'a plus à dériver le seed du `runId` (dérivation qui perdait le seed des runs pilotés et invalidait `same_seed` dans `/api/compare`) |
| `tick` | uint | Numéro de tick courant |
| `simulatedTimeMinutes` | uint | Temps simulé (minutes, **plancher entier** — historique ECHOS stocké en `INTEGER`, inchangé) |
| `simulatedTimeSeconds` | uint | **0.4.0 (additif, ADR-017)** — temps simulé en secondes : `tick × simulatedSecondsPerTick` (60 par défaut ⇒ `minutes × 60`) |
| `aliveCount` | uint | Entités vivantes |
| `agents[]` | array | État complet exposé de chaque entité (position, besoins, intention, action exécutée, traits, croyances, objectifs, confiance, mémoire) — **0.3.0 (additif, sous drapeaux)** : `agents[].inventory` (quantités par type de ressource, D8) et `agents[].commitments` (engagements actifs/résolus, D5) sont émis **seulement** quand `agents.actions.inventory.enabled` / `agents.actions.commitments.enabled` sont actifs ; sortie bit-à-bit identique sinon (rétro-compatible à la lecture) |
| `resources[]` | array | Stocks globaux `{type, quantity}` — 4 types depuis **SYNE ph7c** (food, water, wood, **mineral**) (DATA_MODEL §8.1), pas des stocks localisés par nœud |
| `obstacles[]` | array | Constructions/obstacles statiques `{id, x, y, radius}` — depuis **SYNE ph11d** (SYNE-071), ordre d'insertion (déterminisme) (DATA_MODEL §2) |
| `groups[]`, `territories[]`, `books[]` | arrays | État complet courant des systèmes activés |
| `worldChanges[]` | array | Mutations d'obstacle de ce tick `{kind, id, x, y, radius}` ; vide si aucune |
| `actions[]` | array | Résultat d'action du tick par agent : `{agentId, action, outcome, cause, energyDelta, hungerDelta, thirstDelta, fatigueDelta, reserve?, reserveConsumed?}` ; vide seulement si aucun agent |

Exemple (format condensé) :

```json
{ "type": "snapshot", "version": "0.2.0", "engineVersion": "0.11.0", "runId": "run-abc",
  "tick": 5010, "simulatedTimeMinutes": 5010, "aliveCount": 98, "season": "spring", "seasonIndex": 0,
  "agents": [ { "id": "1", "species": "Entité A", "position": {"x": 53.0, "y": 76.5},
                "energy": 60, "hunger": 30, "thirst": 40,
                "currentIntention": "Explore", "currentAction": "Explore" } ],
  "resources": [ { "type": "food", "quantity": 90 }, { "type": "water", "quantity": 912 },
                  { "type": "wood", "quantity": 50 }, { "type": "mineral", "quantity": 0 } ],
  "obstacles": [ { "id": "maison-1", "x": 100.0, "y": 100.0, "radius": 10.0 } ],
  "territories": [ { "id": "camp", "x": 250.0, "y": 250.0, "radius": 40.0, "memberCount": 12,
                     "members": [1, 2, 3, 4, 5] } ],
  "groups": [ { "groupId": 1, "members": [1, 2, 3], "size": 3,
                "leaderId": 1, "bornTick": 5000, "cohesion": 0.42,
                "decision": "SeekFood", "consensus": 0.80 } ],
  "worldChanges": [ { "kind": "added", "id": "maison-1", "x": 100, "y": 100, "radius": 10 } ],
  "actions": [ { "agentId": "1", "action": "Explore", "outcome": "executed",
                 "energyDelta": -0.5, "hungerDelta": 0, "thirstDelta": 0,
                 "fatigueDelta": 0 } ] }
```

> Le contrat `0.4.0` (ADR-017) ajoute `simulatedTimeSeconds` au snapshot (additif) :
> le temps simulé exact voyage à côté du plancher `simulatedTimeMinutes`, que les
> consommateurs historiques continuent de valider et stocker tel quel.
> Le contrat `0.2.0` ajoute les champs agrégés `worldChanges[]` et `actions[]` au snapshot (additif).
> `agents[].id` est sérialisé comme chaîne décimale par SYNE ; `currentIntention` est l'objectif
> courant et `currentAction` l'action atomique exécutée au tick. Les deux peuvent être `Idle`.
> `runId` = `run-<seed>` ;
> `engineVersion` = `0.11.0` (jalon U8 — livres, SYNE-121 : `world.book_written` / `world.book_read` +
> champ `books[]` du snapshot, **additifs** MINOR ; les ajouts précédents de saisons/territoires restent actifs). Les champs `season`/`seasonIndex`
> (SYNE-072) donnent la saison courante (nom camelCase + index 0..3, déterministe depuis le tick).
> Le champ `territories[]` (SYNE-073, présent seulement si `world.territories.enabled`) liste les
> zones « points de survie » suivies `{id, x, y, radius, memberCount, members[]}` : la **présence**
> des entités délimite le territoire effectif (décision n°21) — `members[]` par identifiant
> croissant, suivi déterministe 0 PRNG (DETERMINISM.md §3).
> Les champs `obstacles[]`
> (SYNE-071, ajout **additif**, MINOR) listent les constructions/obstacles du monde au tick :
> `{id, x, y, radius}`, ordre d'insertion stable. Le champ `groups[]`
> (syne-060/061, ajout **additif**, MINOR) liste les groupes actifs au tick : `groupId`,
> `members[]`, `size`, `leaderId`, `bornTick`, `cohesion` (cohésion moyenne au dernier LOD),
> `decision`/`consensus` (dernière décision collective, `SYSTEMS_SPEC` §5).

### 2.2 `event` — ExternalEvent

| Champ | Type | Description |
| :-- | :-- | :-- |
| `type` | string | Type d'événement (`decision_made`, `action_completed`, `tick_summary`, `agent_spawned`, `agent_died`, `message_sent`, `message_received`, `group_formed`, `group_dissolved`, `group_decision`, `world.construction_placed`, `world.construction_removed`, `world.season_changed`, `world.territory_membership_changed`, `world.book_written`, `world.book_read`, `conflict`...) |
| `tick` | uint | Tick |
| `agentId?` | string | Entité concernée |
| `targetId?` | string | Cible |
| `action?` | string | Action (le cas échéant) |
| `cause?` | string | Cause |
| `value?` | object | Valeur contextuelle (détails) |

Exemple :

```json
{ "type": "decision_made", "tick": 5010, "agentId": "a1",
  "action": "Eat", "cause": "hunger 75", "value": { "utility": 15.5 } }
```

> Événements typés du prototype : `tick_summary`, `agent_spawned`, `agent_died`, `decision_made` (ADR-004).
> **V0.1 émet** `tick_summary` (1/tick, `value.aliveCount`) et `decision_made` (1/entité/tick,
> `value = {intention, utility, deliberated, interrupted}`, `cause = "hunger=…,thirst=…,fatigue=…"`).
> `deliberated`/`interrupted` (bool, jalon SYNE ph3) indiquent si le tick a délibéré (fréquence
> configurable, décision n°14) ou interrompu l'action par besoin critique (décision n°15).
> **`action_completed` (jalon SYNE ph4)** : 1/entité/tick — suite de l'exécution atomique
> (SYNE-040) ; `action` = action exécutée (ex. `Eat`), `value = {outcome: executed|blocked,
> energyDelta, hungerDelta, thirstDelta, fatigueDelta, reserve?, reserveConsumed?}`,
> `cause` = raison du blocage éventuel (réserve vide).
> **`message_sent`/`message_received` (jalon SYNE ph5)** : diffusés par `ObservabilityTickEmitter`
> après les boucles entités dès qu'une pulsation circule. `message_sent` : `agentId` = émetteur
> (d'origine, préservé aux relais), `targetId?` = cible nominale, `action` = `MessageType`,
> `value = {messageId, hops, confidence, payloadLength}`. `message_received` : `agentId` = récepteur
> (interception incluse), `action` = `MessageType`, `value = {messageId, hops, confidence, understood}`
> (COMMUNICATION_PROTOCOL.md §8).
> **`group_formed`/`group_dissolved`/`group_decision` (jalon SYNE ph6)** : diffusés par
> `ObservabilityTickEmitter` à chaque révision LOD (défaut 10 ticks) par `Cognition.Groups`.
> `group_formed` : `agentId` = leader émergent, `value = {groupId, size, cohesion, members[]}`.
> `group_dissolved` : `agentId` = leader sortant, `value = {groupId, lifetime, success,
> membersOut, membersIn, members[]}` (bilan de vie + turnover brut — consommé par ECHOS
> `group_dynamics`). `group_decision` : `agentId` = leader, `action` = intention majoritaire,
> `value = {groupId, decision, consensus}` (votum pondéré par la confiance au leader).
> **`agent_spawned` (jalon SYNE ph6)** : émis par `BirthSystem` à la naissance — `agentId` = enfant
> (id nouvellement alloué, dernier du run), `cause = "birth"`, `value = {childId, motherId,
> fatherId, species, x, y}` ; traits/mémoire hérités consultables via la décision `Born` (SYNE-062/063).
> **`agent_died` (jalon SYNE ph7b, SYNE-074)** : émis par `DeathSystem` quand une entité atteint
> le seuil fatal (énergie nulle, épuisement) — `agentId` = défunt, `cause = "exhaustion"`,
> `value = {cause, species}` (Monographie §6.2.10). L'appelant purge ensuite l'esprit de la
> cognition et des groupes (les groupes vides sont dissous).
> **`world.construction_placed`/`world.construction_removed` (jalon SYNE ph11d, SYNE-071)** :
> modification d'environnement **sans agentId** (événement du monde) — une construction posée
> (`PlaceConstruction`) ou retirée (`RemoveConstruction`) d'un obstacle statique. `targetId` =
> id de l'obstacle, `value = {id, x, y, radius}` (positions/rayon arrondis à 4 décimales).
> Drainés par `ObservabilityTickEmitter` au tick suivant la modification, avant l'émission du
> snapshot correspondant (chaque modification est émise **exactement une fois**).
> **`world.season_changed` (jalon U8, SYNE-072)** : basculement du cycle de saisons (cycle actif
> `world.seasons.enabled`) — événement du monde **sans agentId**, émis **au tick exact** du
> basculement, uniquement quand la saison change. `targetId` = saison courante, `value =
> {previous, current}` (clés camelCase). Déterminisme total : la saison est une fonction pure
> du tick (0 tirage PRNG, DETERMINISM.md §3).
> **`world.territory_membership_changed` (jalon U8, SYNE-073)** : bascule d'appartenance d'une
> entité à une zone de territoire (`world.territories.zones[]`, disques « points de survie »,
> décision n°21) — suivi actif seulement (`world.territories.enabled`). `agentId` = entité qui
> franchit, `targetId` = id de la zone, `value = {kind: "entered" | "left"}`. Ordre déterministe :
> par zone (ordre de pose), identifiant croissant, **sorties avant entrées** ; chaque bascule
> émise **exactement une fois** (drainées par `ObservabilityTickEmitter` au tick de la
> modification). `Entered`/`Left` = changement de l'état de présence (function pure des positions,
> `distance ≤ radius`, 0 tirage PRNG — DETERMINISM.md §3) ; centré sur les répliques, chaque
> zone rejoue la même séquence.

> **`world.book_written` / `world.book_read` (SYNE-121)** : mutations émises après le snapshot correspondant. Écriture : `agentId` auteur, `targetId` livre, `value = {id, title, writtenTick, cost}`. Lecture : `agentId` lecteur, `targetId` livre, `value = {id, readBenefit}`. Le snapshot `books[]` (seulement si `world.books.enabled`) contient `{id, authorId, title, content, writtenTick, readCount, readers[]}`. Les lecteurs distincts conservent l’ordre de première lecture. Aucun effet cognitif n’est appliqué avant le futur moteur mémoire.

## 3. Contrat de contrôle — HTTP (5181 par défaut)

États : `idle`, `worldPreparing`, `ready`, `running`, `paused`, `finished`.
`POST /api/control/prepare` prépare le monde; `GET /api/world` le restitue.
`POST /api/control/ready` accepte `{ "worldVersion": "1.1" }` (optionnel, version
courante de la description — ADR-017) et
accuse réception de la préparation; le statut expose
`worldReadyAcknowledged`. Après un `prepare` explicite, `start` exige cet accusé
et répond `409 world_not_ready` sinon. Un `start` direct conserve
l'auto-préparation historique et est implicitement prêt.
Depuis la **calibration D1** : un `start` alors que l'état est `finished`
(run arrivé à son `maxTicks`) répond **`409 run_finished`** — « run terminé —
appelez /api/control/reset avant de redémarrer » — au lieu du `world_not_ready`
générique, qui orientait le client vers `prepare` au lieu de `reset`.
Les mutations d'obstacles sont regroupées dans `world_delta` :
`{type, runId, tick, changes:[{kind:"added"|"removed",id,x,y,radius}]}`.

API REST locale de contrôle consommée par les clients (dont le plugin PRISM
dans Unreal ; Monographie §5.4.2) :

| Méthode | Endpoint | Corps |
| :-- | :-- | :-- |
| POST | `/api/control/prepare` | `{ seed?, ticksPerSecond?, config? }` |
| GET | `/api/world` | — |
| POST | `/api/control/ready` | `{ worldVersion? }` |
| POST | `/api/control/start` | `{ seed, config }` (optionnel) |
| POST | `/api/control/pause` | — |
| POST | `/api/control/resume` | — |
| POST | `/api/control/reset` | `{ seed?, config?, maxTicks? }` |
| GET | `/api/control/status` | — |

- Binding local par défaut : `127.0.0.1:5181`. Le port HTTP se configure avec
  `--serve-port`; le port WebSocket avec `--observe-port` (défauts 5181 et
  5180). Vérifier la configuration effective plutôt que supposer des ports fixes.
- État interrogé par polling (~2 s, [HÉRITÉ]).
- **Implémentation V0.1 (SYNE-113, livré avec U8)** : serveur BCL (`HttpListener`,
  zéro dépendance, ADR-002/003) dans `Simulation.Console`, activé par `--serve`
  (`--serve-port`, défaut 5181). Machine à états : `idle → running ⇋ paused → finished`.
  `start { seed?, config? }` (JSON partiel fusionné sur les défauts, même règle que
  `--config`) construit le run et répond `{ ok, action, runId, state, tick, aliveCount, seed }` ;
  `pause`/`resume` gèlent/reprennent l'avancement des ticks ; `reset { seed?, config?, maxTicks? }`
  reconstruit un run sur le même modèle que `start` — la surcouche `config` (JSON partiel,
  même règle) construit le nouveau monde et son absence applique le profil de référence ;
  le `runId` du nouveau run est généré (il n'est pas utilisé pour restaurer — la reprise
  depuis le dernier `tick_states` reste le contrat de persistance §4 via
  `SqlitePersistenceStore`). `GET /api/control/status` expose
  `{ state, runId, tick, aliveCount, seed, maxTicks, worldPrepared, worldVersion,
  worldReadyAcknowledged, ticksPerSecond, simulatedSecondsPerTick }` (polling ECHOS) —
  `ticksPerSecond` et `simulatedSecondsPerTick` sont `null` tant qu'aucun monde
  n'est préparé puis valent la valeur du monde préparé (`simulatedSecondsPerTick`
  additif ADR-017, contrat 0.4.0).
  Ce contrat est **non intrusif** (SYNE-081, DETERMINISM.md §3) : aucune commande ne
  retire de tirage au PRNG ni ne change la trajectoire (vérifié par test — run piloté
  == run ininterrompu, état bit-à-bit). Consommé tel quel par le `ControlClient` ECHOS
  (`echos/echos/ingestion/control_client.py`) — testé en interop réel.

## 4. Contrat de persistance

Le schéma SQLite (Annexe G) sert de **contrat de persistance** — voir `PERSISTENCE.md`. Les tables `events_log` et `decision_traces` alimentent l'analyse ECHOS (voir `LOGGING_INSTRUMENTATION.md` ECHOS).

## 5. Compatibilité ascendante

- Ajout de champs = compatible (`MINOR`).
- Suppression/renommage/redéfinition de sens = **breaking** (`MAJOR`).
- Le champ `version` du snapshot permet aux consommateurs de détecter les changements.

---

## Points restés ouverts dans ce document
- Extension du `WorldSnapshot` : **clos de fait (constaté le 30/09/2026)** — le snapshot V0.1 expose agents, stocks, obstacles, groupes, territoires et livres actifs plus `worldChanges[]`/`actions[]` (cf. §2) ; les croyances/goals/relations ne sont **pas** exposées dans le snapshot (conformité à l'observabilité partielle §6.11) et transitent par les traces de décision et le contexte ECHOS. Toute extension future suit la règle MINOR/BREAKING ci-dessus.
- Nomenclature des types d'`ExternalEvent` : **clos de fait (constaté le 30/09/2026)** — la nomenclature est fixée par le code (`ExternalEvent.cs`) et verrouillée par `ObservabilitySensorTests` (capteurs `message_sent`/`received`, `agent_died`, `agent_spawned`, `action_completed`) ; les événements `world.season_changed`, `world.territory_membership_changed` et `world.construction_placed/removed` sont documentés en §2. Toute extension suit la règle MINOR/BREAKING ci-dessus.
- Multi-consommateur : **tranché en V0.1** — diffusion à tous les clients connectés (pas de règle single-consumer).