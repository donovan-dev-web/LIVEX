# Plugin Unreal SYNE — intégration minimale

## 1. Périmètre et principes

Le plugin doit être un **client de transport et d'observabilité**, pas une
réimplémentation du moteur SYNE. Le mock expose :

- WebSocket texte JSON UTF-8 sur `ws://127.0.0.1:5180/` ;
- contrôle HTTP JSON sur `http://127.0.0.1:5181` ;
- un monde 2D et des ticks déterministes, par défaut 50 agents et 400 ticks ;
- un contrat `0.2.0` et `engineVersion` `0.11.0`.

Toutes les adresses et versions doivent être configurables. Le plugin ne doit
pas supposer que le serveur est local en production.

## 2. Architecture recommandée

Séparer les responsabilités dans cinq classes :

| Classe | Responsabilité |
|---|---|
| `USyneSubsystem` (`UGameInstanceSubsystem`) | façade Blueprint, configuration et cycle de vie |
| `FSyneTransport` | WebSocket, HTTP, thread de réception, reconnexion |
| `FSyneJsonCodec` | parse/validation JSON et conversion vers types Unreal |
| `FSyneStateStore` | dernier snapshot, tick accepté, `runId`, files d'événements |
| `USyneWorldComponent` (optionnel) | projection visuelle dans le monde Unreal |

Le transport ne doit jamais toucher des `UObject` depuis son thread. Il pousse
des messages validés dans une file thread-safe ; `USyneSubsystem::Tick` les
consomme sur le game thread et déclenche les delegates Blueprint. Les données
brutes et les erreurs de décodage doivent rester observables pour le diagnostic.

## 3. Responsabilités C++ minimales

1. `Connect()` ouvre le WebSocket et l'abonnement aux trames texte uniquement
   lorsqu'il est appelé explicitement. Le subsystem ne se connecte pas au
   démarrage. La reconnexion automatique, si activée, ne s'applique qu'après
   une connexion demandée puis interrompue ; `Disconnect()` la désactive.
2. `Prepare(seed, ticksPerSecond)` appelle `POST /api/control/prepare` avec la
   seed et une fréquence positive en ticks/seconde, puis `Ready("1.0")`
   accuse réception avant `Start()`. Après cette préparation explicite,
   `Start` réutilise le monde et sa cadence ; son seed, s'il est envoyé, doit
   correspondre au seed préparé. Un `Start()` direct reste rétrocompatible
   (auto-prepare implicite).
3. `Tick(float)` dépile sans bloquer et applique `world_initialized`, les
   `world_delta`, puis les snapshots dans l'ordre.
4. Le codec exige `type` et `tick` pour les messages de flux ; un champ inconnu
   est ignoré pour permettre les ajouts additifs.
5. Le store rejette un snapshot d'un autre `runId`, sauf après `Reset`/nouveau
   `Start`, et signale un saut de tick sans fermer la connexion.
6. Les delegates sont déclenchés sur le game thread, après mise à jour du store.
7. `Disconnect()` annule reconnexions et requêtes en cours à la destruction.

Le plugin ne simule ni cognition, ni navigation, ni règles de mortalité. Il
convertit et expose les observations du serveur.

## 4. API native et Blueprint-facing

API native conseillée (noms indicatifs) :

```cpp
bool Connect(const FSyneConnectionOptions& Options);
TFuture<FSyneWorldDescription> Prepare(int64 Seed, int32 TicksPerSecond = 10);
TFuture<FSyneControlResult> Ready(FString WorldVersion);
void Disconnect();
TFuture<FSyneControlResult> Start(int64 Seed, int32 MaxTicks);
TFuture<FSyneControlResult> Pause();
TFuture<FSyneControlResult> Resume();
TFuture<FSyneControlResult> Stop();
TFuture<FSyneControlResult> Reset(int64 Seed, int32 MaxTicks);
bool GetLatestSnapshot(FSyneWorldSnapshot& Out) const;
ESyneConnectionState GetConnectionState() const;
```

Expose la même façade en Blueprint (`BlueprintCallable`) et les lectures en
`BlueprintPure` quand elles ne mutent pas l'état :

- `Connect`, `Disconnect`, `Start`, `Pause`, `Resume`, `Stop`, `Reset`,
  `RequestStatus` ;
- `GetLatestSnapshot`, `GetAgentById`, `GetConnectionState` ;
- événements `OnConnected`, `OnDisconnected`, `OnSnapshot`,
  `OnSyneEvent`, `OnControlResult`, `OnError`.

Les callbacks de commande doivent retourner `ok`, `action`, `runId`, `state`,
`tick`, `aliveCount`, `seed` et `maxTicks`; un code HTTP non-2xx devient une
erreur typée et non une exception Blueprint.

## 5. Structs, enums, delegates et événements

Types minimum à exposer : `FSyneVector2D`, `FSyneAgentSnapshot`,
`FSyneResource`, `FSyneObstacle`, `FSyneTerritory`, `FSyneGroup`,
`FSyneBook`, `FSyneWorldSnapshot`, `FSyneEvent`, `FSyneStatus`,
`FSyneControlResult`, `FSyneError`.

La `WorldDescription` inclut également `agents[]` avant le premier tick. Chaque
entrée fournit l'identifiant, l'espèce et la position 2D initiale, afin
qu'Unreal puisse créer les acteurs avant `Ready`/`Start`. Ces mêmes agents et
positions sont présents dans le premier snapshot.

Le snapshot global `0.2.0` contient l'état dynamique complet par tick :
`agents[]`, `resources[]`, `obstacles[]`, `groups[]`, `territories[]`,
`books[]`, ainsi que `worldChanges[]` et `actions[]` pour les mutations et
actions du tick. Les événements unitaires restent émis pour compatibilité,
mais ne doivent pas être réappliqués lorsqu'on utilise le snapshot comme source
de vérité. La topologie statique `cells[]` est envoyée une fois dans
`world_initialized`.

Enums recommandés :

- `ESyneMessageType`: `Snapshot`, `DecisionMade`, `ActionCompleted`,
  `TickSummary`, `MessageSent`, `MessageReceived`, `GroupDecision`,
  `WorldBookWritten`, plus `Unknown` ;
- `ESyneRunState`: `Idle`, `WorldPreparing`, `Ready`, `Running`, `Paused`,
  `Finished` ;
- `ESyneConnectionState`: `Disconnected`, `Connecting`, `Connected`,
  `Reconnecting`, `Error`.

Les events SYNE sont extensibles : conserver `Type` en `FName`/`FString`,
`AgentId`, `TargetId`, `Action`, `Cause`, `Tick` et `ValueJson` (objet JSON
conservé ou map typée). Ne pas créer une branche C++ obligatoire pour chaque
nouveau type d'événement.

## 6. Cycle de contrôle et reconnexion

Ordre recommandé : `Connect` → attendre `Connected` → `Prepare` → `Ready` → `Start` → consommer les
trames → `Pause/Resume` si nécessaire → `Stop` ou fin `Finished` → `Reset` pour
un nouveau run. `Start` pendant un run `running` ou `paused` répond HTTP 409
avec `error: "run_active"`. `Reset` arrête puis démarre immédiatement un run.

Une déconnexion WebSocket déclenche une reconnexion bornée (backoff, jitter
optionnel) sans relancer automatiquement `Start`. Après reconnexion, appeler
`status`, comparer `runId`/`tick`, puis demander `Reset` ou reprendre la
consommation selon la politique du jeu. Le mock ne rejoue pas les trames
perdues. `Pause` gèle les ticks ; `Resume` recrée l'intervalle ; `Stop` revient
à `idle` ; l'arrivée à `maxTicks` passe à `finished`. Les états incluent
`worldPreparing` et `ready`; `start` sans accusé après `prepare` renvoie
`409 world_not_ready`.

## 7. Sécurité et erreurs

Le mock écoute en loopback par défaut : cela ne fournit aucune authentification.
En environnement partagé, limiter l'interface réseau, ajouter authentification
et TLS au proxy, valider taille/temps des messages, et ne jamais afficher de
payload sensible dans les logs. Refuser les URLs non autorisées et les schémas
non `ws/wss` ou `http/https`.

Erreurs HTTP connues : `400 invalid_json`, `404 not_found`, `409 run_active`.
Prévoir aussi timeout, fermeture anormale, JSON invalide, type inconnu, tick
régressif et `runId` incohérent. Une erreur de donnée doit isoler la trame et
laisser la connexion utilisable quand c'est possible.

## 8. Limites explicites du mock

Le mock est déterministe et compatible avec le contrat, mais **pas bit-à-bit
identique au moteur C#**. Les algorithmes cognitifs, perception complète,
pathfinding A*, reproduction, mortalité, communication relayée et dynamique
complète des groupes sont simplifiés. Le déplacement suit la cible déterministe,
la vitesse de l'agent et les obstacles, puis utilise un détour local simplifié
plutôt que l'A* de SYNE. Les marqueurs initiaux de ressources ne sont pas des
stocks localisés ; les stocks du snapshot sont globaux.
Le replay JSONL transporte les messages ligne par ligne ; il ne restaure pas
l'état interne C#. Le plugin doit donc tester le contrat et non déduire une
équivalence scientifique.

## 9. Versionnage

Comparer `version` (contrat) et `engineVersion` séparément. Les champs ajoutés
dans un même contrat sont additifs ; ignorer les inconnus. Refuser ou dégrader
proprement une version majeure incompatible, journaliser une version mineure
inconnue, et inclure ces versions dans les rapports de compatibilité.
