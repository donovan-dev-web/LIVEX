# API_CONTRACTS.md

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 24 septembre 2026
**Dépend de** : `../COMMUNICATION.md`, `DATA_MODEL.md`
**Source Monographie** : §2.4 (contrats de transport), §5.4 (PRISM), §3.24 (événements), ADR-003/ADR-004

---

## 1. Objectif

Définit les **contrats de données** exposés par SYNE — ils sont la langue commune entre SYNE, ECHOS et PRISM. Toute évolution est gérée par `../../VERSIONING.md`.

## 2. Contrat temps réel — WebSocket 5180

Transport : WebSocket local, **trames texte UTF-8 contenant du JSON** (`camelCase`).
SYNE envoie `WebSocketMessageType.Text`, jamais une trame binaire. Deux types de
messages (Monographie §5.4.1) :

`runId` dans les snapshots est l'identité de contenu consommée par ECHOS. Elle
est opaque et stable pendant le run (le mode batch peut utiliser `run-<seed>`).
La réponse HTTP à `start`/`status` expose ce même identifiant pour permettre au
client de corréler le pilotage et le flux.

> **Implémentation V0.1 (SYNE-080, livré avec U1)** : émetteur BCL (HttpListener + `AcceptWebSocketAsync`,
> zéro dépendance) dans `Simulation.Console`, activé par `--observe` (port `--observe-port`, défaut 5180,
> bind `127.0.0.1`). Chaque tick émet **1 snapshot + 1 `tick_summary` + 1 `decision_made` + 1
> `action_completed` par entité, + événements de communication dès qu'un message circule**,
> diffusion à **tous** les consommateurs connectés. L'émission
> n'ajoute aucun tirage PRNG (déterminisme inchangé, DETERMINISM.md §3).

### 2.1 `snapshot` — WorldSnapshot

| Champ | Type | Description |
| :-- | :-- | :-- |
| `version` | string | Version du contrat (SemVer) |
| `engineVersion` | string | Version du moteur (DETERMINISM.md §3.6.2) — identifie les règles du run |
| `runId` | string | Identifiant du run |
| `tick` | uint | Numéro de tick courant |
| `simulatedTimeMinutes` | uint | Temps simulé (minutes) |
| `aliveCount` | uint | Entités vivantes |
| `agents[]` | array | État des entités (position, santé, énergie, faim, soif, action courante...) |
| `resources[]` | array | Réserves globales `{type, quantity}` — 4 types depuis **SYNE ph7c** (food, water, wood, **mineral**) (DATA_MODEL §8.1) |
| `obstacles[]` | array | Constructions/obstacles statiques `{id, x, y, radius}` — depuis **SYNE ph11d** (SYNE-071), ordre d'insertion (déterminisme) (DATA_MODEL §2) |

Exemple (format condensé) :

```json
{ "type": "snapshot", "version": "0.1.0", "engineVersion": "0.11.0", "runId": "run-abc",
  "tick": 5010, "simulatedTimeMinutes": 5010, "aliveCount": 98, "season": "spring", "seasonIndex": 0,
  "agents": [ { "id": "a1", "position": {"x": 53.0, "y": 76.5}, "health": 80,
                "energy": 60, "hunger": 30, "thirst": 40, "currentAction": "MoveTo" } ],
  "resources": [ { "type": "food", "quantity": 90 }, { "type": "water", "quantity": 912 },
                  { "type": "wood", "quantity": 50 }, { "type": "mineral", "quantity": 0 } ],
  "obstacles": [ { "id": "maison-1", "x": 100.0, "y": 100.0, "radius": 10.0 } ],
  "territories": [ { "id": "camp", "x": 250.0, "y": 250.0, "radius": 40.0, "memberCount": 12,
                     "members": [1, 2, 3, 4, 5] } ],
  "groups": [ { "groupId": 1, "members": ["a1", "a2", "a3"], "size": 3,
                "leaderId": "a1", "bornTick": 5000, "cohesion": 0.42,
                "decision": "SeekFood", "consensus": 0.80 } ] }
```

> V0.1 émet par entité : `id` (uint), `species`, `position{x,y}`, `energy`, `hunger`, `thirst`, `fatigue`,
> `currentAction` (intention `DesireKind`, ex. `Idle`, `SeekWater`) ; `runId` = `run-<seed>` ;
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

## 3. Contrat de contrôle — HTTP 5181

API REST locale de contrôle, relayée par PRISM (Monographie §5.4.2) :

| Méthode | Endpoint | Corps |
| :-- | :-- | :-- |
| POST | `/api/control/start` | `{ seed, config }` (optionnel) |
| POST | `/api/control/pause` | — |
| POST | `/api/control/resume` | — |
| POST | `/api/control/reset` | `{ seed, runId }` |
| GET | `/api/control/status` | — |

- Binding local : `127.0.0.1:5181`.
- État interrogé par polling (~2 s, [HÉRITÉ]).
- **Implémentation V0.1 (SYNE-113, livré avec U8)** : serveur BCL (`HttpListener`,
  zéro dépendance, ADR-002/003) dans `Simulation.Console`, activé par `--serve`
  (`--serve-port`, défaut 5181). Machine à états : `idle → running ⇋ paused → finished`.
  `start { seed?, config? }` (JSON partiel fusionné sur les défauts, même règle que
  `--config`) construit le run et répond `{ ok, action, runId, state, tick, aliveCount, seed }` ;
  `pause`/`resume` gèlent/reprennent l'avancement des ticks ; `reset { seed?, runId? }`
  reconstruit un run (le `runId` reçu identifie l'appelant, il n'est pas utilisé pour
  restaurer — la reprise depuis le dernier `tick_states` reste le contrat de
  persistance §4 via `SqlitePersistenceStore`). `GET /api/control/status` expose
  `{ state, runId, tick, aliveCount, seed, maxTicks }` (polling ECHOS).
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
- Extension exacte du `WorldSnapshot` V0.1 (beliefs/goals/relations/groups) à figer lors de l'implémentation.
- Nomenclature exhaustive des types d'`ExternalEvent` V0.1 (alignée sur les événements du système).
- Multi-consommateur : **tranché en V0.1** — diffusion à tous les clients connectés (pas de règle single-consumer).