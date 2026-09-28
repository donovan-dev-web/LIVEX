# Référence des contrats consommables

Source : `syne-mock/src/server.js`, la suite `syne-mock/test/` et
`docs/docs-syne/API_CONTRACTS.md`. Les exemples ci-dessous décrivent le mock,
pas une promesse d'API supplémentaire.

## WebSocket et ordre des messages

Le serveur diffuse chaque message à tous les clients connectés sous forme de
trame texte JSON. Pour un tick normal, l'ordre causal est :

1. pour chaque agent : `decision_made`, puis `action_completed` ;
2. éventuellement `message_sent` puis `message_received` (tick multiple de 5,
   si les deux agents sont dans `transmissionRange`) ;
3. éventuellement `group_decision` (selon `groups.lodInterval`) ;
4. au tick 100, éventuellement `world.book_written` ;
5. `snapshot` ;
6. `tick_summary`.

Il y a un snapshot par tick, après les mutations. Il n'y a pas de snapshot
initial automatique à la connexion. Une fin à `maxTicks` arrive après les
messages du dernier tick. En mode replay, les objets JSON du fichier JSONL sont
diffusés tels quels : ne pas appliquer l'ordre ci-dessus.

## Snapshot `WorldSnapshot`

Champs présents dans le mock :

```text
type, version, engineVersion, runId, tick, simulatedTimeMinutes, aliveCount
season, seasonIndex, agents[], resources[], obstacles[], territories[], groups[], books[]
worldChanges[], actions[]
```

Chaque agent contient `id` (chaîne décimale dans le snapshot), `species`,
`position{x,y}`, `energy`, `hunger`, `thirst`, `fatigue`,
`currentIntention`, `currentAction`, `traits`, `beliefs`, `goals`, `trust`,
`memoryCount`. Les tableaux `traits`, `beliefs`, `goals`, `trust` sont
remplis comme suit : traits configurés, objectif courant s'il existe, et
croyances/relations de confiance vides dans cette version du mock. Les ressources sont `{type, quantity}` pour
`food`, `water`, `wood`, `mineral`. Les territoires contiennent `id`, `x`, `y`,
`radius`, `memberCount`, `members`; les groupes et livres suivent les formes
émises par le serveur.
`worldChanges[]` contient les mutations d'obstacle du tick (vide s'il n'y en a
pas) ; `actions[]` contient un résultat d'action par agent du tick, avec
`agentId`, `action`, `outcome`, les deltas d'état et la réserve consommée le
cas échéant. L'état des agents/réserves/obstacles/groupes/territoires/livres
est global et complet au moment du snapshot.

Mapping `USTRUCT` recommandé :

```cpp
USTRUCT(BlueprintType) struct FSyneWorldAgent { GENERATED_BODY()
  UPROPERTY(BlueprintReadOnly) int64 Id = 0;
  UPROPERTY(BlueprintReadOnly) FString Species;
  UPROPERTY(BlueprintReadOnly) FSyneVector2D Position;
};
USTRUCT(BlueprintType) struct FSyneVector2D { GENERATED_BODY()
  UPROPERTY(BlueprintReadOnly) double X = 0;
  UPROPERTY(BlueprintReadOnly) double Y = 0;
};
USTRUCT(BlueprintType) struct FSyneAgentSnapshot { GENERATED_BODY()
  UPROPERTY(BlueprintReadOnly) int32 Id = 0;
  UPROPERTY(BlueprintReadOnly) FString Species;
  UPROPERTY(BlueprintReadOnly) FSyneVector2D Position;
  UPROPERTY(BlueprintReadOnly) double Energy = 0, Hunger = 0, Thirst = 0, Fatigue = 0;
  UPROPERTY(BlueprintReadOnly) FString CurrentAction;
};
```

`FSyneWorldDescription.Agents` porte la liste initiale ; la parcourir dans
`OnWorldInitialized` pour créer les acteurs. Le premier `WorldSnapshot.Agents`
reprend les mêmes identifiants et positions, et doit donc mettre à jour les
acteurs déjà créés.

Dans le plugin actuel, `FPrismSyneResource` est réutilisé pour les réserves du
snapshot et les marqueurs de placement initiaux. `bHasPosition` indique une
position fournie ; `bPositionIsCellIndex` vaut vrai uniquement dans la
description préparée, car `x/y` sont alors des indices entiers de cellule. Les
réserves des snapshots sont globales et n'ont pas de position.

Les autres `USTRUCT` reprennent les clés JSON en camelCase converties en
propriétés Unreal. Utiliser `int64`/`FString` pour les identifiants si un futur
moteur cesse de garantir des IDs numériques.

## Events `ExternalEvent`

Événement commun : `type`, `tick`, et selon le cas `agentId`, `targetId`,
`action`, `cause`, `value`. Le mock produit :

- `decision_made` : `value={intention,utility,deliberated,interrupted}` ;
- `action_completed` : `value={outcome,energyDelta,hungerDelta,thirstDelta,
  fatigueDelta}` et éventuellement `reserve,reserveConsumed` ;
- `message_sent` / `message_received` : `value` avec `messageId`, `hops`,
  `confidence`, `payloadLength` ou `understood` ;
- `group_decision` : `value={groupId,decision,consensus}` ;
- `world.book_written` : `value` contient le livre ;
- `tick_summary` : `value={aliveCount}`.

Les contrats docs-syne listent également des événements futurs/optionnels
(`agent_spawned`, `agent_died`, `group_formed`, `world.season_changed`, etc.).
Le plugin les désérialise génériquement tant que le mock ne les émet pas.

## JSON vers `UENUM`

Ne mapper en enum que des chaînes connues et prévoir `Unknown` :

```text
season: spring | summer | autumn | winter
action/currentAction: Idle | Eat | Drink | Rest | Explore | SeekFood | SeekWater | Flee | Socialize
event.type: chaîne libre, valeurs listées ci-dessus
```

`currentAction` ne prend que certaines valeurs de cette liste : il vaut
`currentIntention`, ou `Idle` quand l'intention n'a pas pu être exécutée. Voir
« Actions produites par le mock » pour le détail.

Conserver la valeur texte originale pour les valeurs futures. Les types de
message SYNE (`Information`, `Request`, `Response`, `Announcement`, `Warning`,
`Trading`, `Acknowledgement`) sont définis par `COMMUNICATION_PROTOCOL.md`,
mais le mock émet actuellement `Information`.

## Actions produites par le mock

Neuf intentions sont connues (`ACTIONS` dans `agent-decision-service.js`), mais
les besoins qui les déclenchent ne sont pas tous atteignables. Avec les valeurs
par défaut, les agents partent de `hunger: 0`, `thirst: 0`, `fatigue: 0`,
`safety: 1`, `social: 0`, `curiosity: 0` :

La colonne indique le **premier tick où l'action est effectivement produite**,
c'est-à-dire où elle l'emporte sur les autres candidates. Mesuré sur la graine
42 avec 50 agents, `maxTicks: 1000` :

| Action | Condition de candidature | 1ᵉʳ tick produit |
|---|---|---|
| `Idle` | toujours candidate | 1 |
| `Drink` | `thirst >= 50` et réserve `water > 0` | 72 |
| `Eat` | `hunger >= 50` et réserve `food > 0` | 100 |
| `Explore` | `curiosity >= 0.3` | 150 |
| `Rest` | `fatigue >= 70` | 234 |
| `Socialize` | `social >= 0.7` | 943 — **hors `maxTicks: 400`** |
| `SeekFood` | `hunger >= 50` et réserve `food == 0` | jamais (réserves à 10 000) |
| `SeekWater` | `thirst >= 50` et réserve `water == 0` | jamais (réserves à 10 000) |
| `Flee` | `safety <= 0.5` | **structurellement inatteignable** |

Candidature et exécution ne coïncident pas : `Socialize` devient candidate au
tick 700, mais les autres besoins gardent une utilité supérieure jusqu'au tick
943. De même, agir recharge le besoin, qui recommence à croître et fait revenir
l'action périodiquement. Toutes ces valeurs découlent des besoins par défaut, qui
démarrent à `hunger: 0`, `thirst: 0`, `fatigue: 0`, `safety: 1`, `social: 0`,
`curiosity: 0` et croissent aux taux `0.5`, `0.7`, `0.3`, `+0.001`, `0.001`,
`0.002`.

Deux limites méritent d'être connues avant de consommer le flux :

- **`Flee` ne peut pas se produire.** `safetyDriftRate` vaut `+0.001` et
  `safety` démarre à `1`, la seule borne étant `[0, 1]` : la valeur ne peut que
  monter. Aucune configuration des besoins ne rend `Flee` atteignable, il faut un
  déclencheur qui baisse la sécurité.
- **`Socialize` sort du run par défaut.** Il n'est produit qu'au tick 943, alors
  que `maxTicks` vaut 400.

Cinq intentions sont des actions de déplacement (`MOVEMENT_ACTIONS` :
`SeekFood`, `SeekWater`, `Flee`, `Socialize`, `Explore`) ; les quatre autres
consomment une réserve, un cycle d'énergie ou de la fatigue, et s'exécutent sur
place. Un déplacement refusé coûte `outcome: "blocked"` et ne débite pas
d'énergie ; `currentAction` retombe alors sur `Idle` tandis que
`currentIntention` conserve l'intention.

## Périmètre réel et simplifications assumées

`syne-mock` simule un sous-ensemble de SYNE, suffisant pour brancher PRISM et
ECHOS sans réécrire le moteur. Ce qui suit est **délibérément** absent, et un
client ne doit pas en dépendre :

- **La mort n'est pas simulée.** `aliveCount` est constant, et une énergie à 0
  ne retire pas l'agent.
- **La perception n'est pas simulée.** `beliefs`, `trust` et `memoryCount`
  restent vides, et aucun agent ne perçoit ses voisins : les utilitaires sont
  calculés à partir des seuls besoins.
- **La communication est décorative.** `message_sent` et `message_received` sont
  émis entre les agents 0 et 1 dès qu'ils sont à portée de
  `transmissionRange`, avec `payloadLength: 0`. `maxSendsPerTick`,
  `maxReceivesPerTick` et `maxHops` ne sont pas appliqués.
- **Les groupes sont décoratifs.** `createInitialGroups` les forme par
  proximité, puis `group_decision` recopie l'intention de l'agent 0. Cohésion et
  consensus sont des constantes.
- **Livres et territoires sont sans effet.** `books.enabled` vaut `false` par
  défaut, et un livre écrit n'a aucun lecteur. `territories` est publié mais
  n'influence aucune décision.
- **Les ressources locales ne sont jamais ciblées.** `world.resources[]`
  décrit la carte ; les agents ne consomment que les réserves globales
  `resources`.
- **Le temps est simplifié.** `simulatedTimeMinutes` vaut `tick`, et
  `seasonIndex` vaut `floor(tick / 90) % 4` : une saison dure 90 ticks, quelle que
  soit la cadence. Les deux échelles ne sont pas reliées — à
  `ticksPerSecond: 10`, une saison réelle de 9 s est étiquetée 90 minutes.
- **Le déplacement n'est pas un pathfinding.** `stepToward` et un contournement
  perpendiculaire déterministe remplacent l'A\* de SYNE sur la grille
  rasteurisée, et `worldChanges[]` ne porte aucune mutation de terrain.
- **`simulation.events` est un tampon de diagnostic borné**
  (`diagnostics.maxEvents`, 2 000 par défaut), pas l'historique. Le flux
  diffusé reste la source faisant autorité.

Ces écarts sont destinés à être refermés par SYNE, pas par le mock. Ce que le
mock publie dans `WorldSnapshot` et `WorldDescription` est destiné à être
consommé tel quel ; les listes ci-dessus décrivent ce qu'un client ne doit pas
encore lire comme porteur de sens.

## HTTP de contrôle

| Méthode | Route | Corps | Réponse |
|---|---|---|---|
| GET | `/api/control/status` | — | status JSON |
| GET | `/api/world` | — | `WorldDescription` ou `409 world_not_prepared` |
| POST | `/api/control/prepare` | `{"seed":42,"ticksPerSecond":10}` | monde préparé |
| POST | `/api/control/ready` | `{"worldVersion":"1.0"}` | accusé de réception |
| POST | `/api/control/start` | `{"seed":42,"maxTicks":400}` | `{ok,action,...status}` |
| POST | `/api/control/pause` | `{}` | idem |
| POST | `/api/control/resume` | `{}` | idem |
| POST | `/api/control/stop` | `{}` | idem |
| POST | `/api/control/reset` | `{"seed":42,"maxTicks":400}` | idem |

Le status contient `state` (`idle`, `worldPreparing`, `ready`, `running`,
`paused`, `finished`), `runId` (ou `null`), `tick`, `aliveCount`, `seed`,
`maxTicks`, `ticksPerSecond`, `worldPrepared`, `worldVersion` et
`worldReadyAcknowledged`. Les
valeurs non fournies à `start/reset` viennent de la
configuration. Toujours lire le code HTTP avant de parser une réponse succès.
`ticksPerSecond` de `prepare` doit être un entier strictement positif ; il est
renvoyé avec le monde préparé et mémorisé pour le run qui suit.
Après un prepare explicite, démarrer avec le même seed réutilise ce monde et
sa cadence. Un seed différent est rejeté avec `409 prepared_seed_mismatch` ;
répéter `prepare`, reconstruire le monde Unreal puis appeler `ready`.

Un `start` refusé ne laisse **jamais** de run à l'état `running` sans timer. En
particulier, un `replay.file` introuvable ou malformé lève avant l'entrée en
`running` : le monde reste `ready`, acquitté et réutilisable, et un nouvel appel
à `start` se comporte comme le premier une fois la cause corrigée. Un client peut
donc réessayer sans appeler `stop` au préalable.

`world_initialized` précède le premier snapshot et contient `version: "1.0"`,
`seed` et `world`. Celui-ci contient `width`, `height`, `cellSize`,
`ticksPerSecond`, `cellCountX`, `cellCountY`, des agents initiaux (`id`, `species`,
`position{x,y}`), des `cells` (`terrainType`, `walkable`, `height`,
`movementCost`, `obstacles`), des obstacles initiaux (`id`, `x`, `y`, `radius`),
des ressources locales et des régions. La liste
d'agents et leurs positions sont réutilisées dans le premier snapshot. La
génération est déterministe par seed. Le mock ne crée pas d'obstacle au tick 1 :
les obstacles du snapshot correspondent aux obstacles préparés tant qu'aucune
mutation n'a été explicitement ajoutée au scénario. Les mutations d'obstacles sont des
`world_delta` avec `changes` (`kind: "added"|"removed"`, `id`, `x`, `y`,
`radius`).

Le mock publie bien un type de terrain par cellule, avec trois profils :

| `terrainType` | `movementCost` | `walkable` |
|---|---|---|
| `plains` | 1 | `true` |
| `river` | 4 | `true` |
| `sea` | `Infinity` | `false` |

La mer est une bande le long du bord désigné par `world.terrain.seaEdge`, de
profondeur `seaDepthRatio × cellCountY` ; les rivières sont creusées depuis un
bord par `carveRiver` et ne remplacent jamais une cellule de mer. Un agent ne
peut ni être placé ni se retrouver sur une cellule `walkable: false`, et son pas
est borné par le `movementCost` le plus élevé entre sa case de départ et sa case
d'arrivée : entrer dans une rivière ralentit, en sortir aussi.

Ce découpage appartient au mock. SYNE n'a pas de configuration de type de
terrain ou de biome, et le layout initial configurable porte sur les obstacles
et les territoires, pas sur les biomes. Brancher le vrai moteur demandera donc
un mapping explicite, pas l'envoi de ce champ tel quel.

Le contrat de snapshot est `0.2.0` : un message global unique par tick,
contenant l'état dynamique complet et les deux listes `worldChanges[]` et
`actions[]`. Les événements `decision_made`, `action_completed` et
`world_delta` sont également émis pour compatibilité/diagnostic ; le client
doit traiter le snapshot comme état faisant autorité et ne pas appliquer une
deuxième fois les mêmes mutations depuis ces événements.

La topologie `cells[]` est envoyée une fois par `world_initialized`. Le mock ne
simule pas de changements de terrain/altitude ; ses changements dynamiques
actuels portent sur les obstacles.

## Blueprint : exemples de graphes

**Initialisation et démarrage** : `Event BeginPlay` → `Get Syne Subsystem` →
`Connect` → `OnConnected` → `Prepare(42, 10)` → `OnWorldInitialized` →
générer les tuiles/World Partition → `Ready("1.0")` → `Start(42, 400)` →
`OnSnapshot` → stocker `LatestSnapshot`.

**Pause sécurisée** : `Input Escape` → `Get Connection State` (Connected) →
`Pause`; afficher `OnControlResult` ou `OnError`.

**Affichage** : `OnSnapshot(Snapshot)` → `ForEach Agent` → `Position` →
`SetActorLocation` (conversion d'unités explicite, car SYNE est un plan 2D).

**Observabilité** : `OnSyneEvent` → `Switch on Syne Event Type` ; traiter les
types connus et une branche `Unknown`, sans supposer un seul événement par tick.
