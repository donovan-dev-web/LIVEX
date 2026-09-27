# Référence des contrats consommables

Source : `syne-mock/src/server.js`, `test/server.test.js` et
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
action/currentAction: Idle | Eat | Drink | Rest | Explore
event.type: chaîne libre, valeurs listées ci-dessus
```

Conserver la valeur texte originale pour les valeurs futures. Les types de
message SYNE (`Information`, `Request`, `Response`, `Announcement`, `Warning`,
`Trading`, `Acknowledgement`) sont définis par `COMMUNICATION_PROTOCOL.md`,
mais le mock émet actuellement `Information`.

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

SYNE n'a actuellement pas de configuration de type de terrain/biome : les
cellules utilisent `terrainType: "plains"`. Le layout initial configurable
porte sur les obstacles et les territoires, pas sur les biomes.

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
