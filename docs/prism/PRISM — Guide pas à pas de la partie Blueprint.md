# PRISM — Guide pas à pas de la partie Blueprint

*Pour Donovan · 15 octobre 2026 · Complète `BLUEPRINT_ROADMAP.md` et `PRISM_UNREAL_IMPLEMENTATION.md`*

## 0. Comment utiliser ce guide

Tes deux documents disent **quoi créer et où**. Celui-ci dit **quoi faire à l'intérieur de chaque élément**, nœud par nœud. Pour chaque élément tu trouveras : les variables à ajouter, les graphes à câbler dans l'ordre, puis un **test de fin d'étape** pour savoir si c'est bon avant de continuer.

Conventions : `Nom` = nœud, variable ou fonction tel qu'il apparaît dans l'éditeur ; `→` = « puis branche sur » ; **\[À VÉRIFIER\]** = je n'ai pas pu le confirmer dans tes documents, regarde dans l'éditeur ; **\[Piège\]** = erreur silencieuse classique. Les noms d'éléments (`BP_PRISM_…`) sont ceux de ta roadmap.

### 0.1 L'idée générale

SYNE (ou le mock) simule le monde. Le plugin C++ `PrismLdk` reçoit ses messages et les expose à Blueprint. Ton travail Blueprint tient en 4 verbes : **piloter** la session (connecter, préparer, démarrer), **construire** la scène 3D depuis la description du monde, **déplacer** les acteurs à chaque snapshot, **afficher** des informations. Tu n'inventes jamais l'état du monde : tu l'affiches.

```text
SYNE / mock ──WebSocket + HTTP──► UPrismLdkSubsystem (plugin C++)
                                      │ dispatchers : OnConnected, OnSnapshot…
                                      ▼
                          BP_PRISM_GameInstance   ← seul à parler au plugin
                                      │ re-diffuse les ED_PRISM_*
                 ┌────────────────────┼─────────────────────┐
                 ▼                    ▼                     ▼
      BP_PRISM_WorldDirector     Widgets WBP_*      BP_PRISM_PlayerController
      (seul à créer des acteurs)
                 │ enregistre / retrouve par Id
                 ▼
      UPrismLdkWorldRegistryBase ← registre Id → acteur + échelle k (C++, aucun Blueprint)
```

### 0.2 Les 7 règles à ne jamais enfreindre

1. **Aucune formule de conversion dans un graphe.** Tu utilises les nœuds de catégorie `SYNE|Projection` (`Syne To Unreal`, `Syne To Cell`, `Cell To Tile Center`…) en leur passant `GetStats().K` et `GetStats().CellSize`.
2. **`k` n'est jamais une variable Blueprint.** Il se lit toujours via `Get Stats` du registre.
3. **Le snapshot est la vérité.** Les deltas et événements servent aux notifications ; ils ne rejouent pas une mutation déjà présente dans le snapshot.
4. **Seul le GameInstance** appelle `Configure` et `Connect`.
5. **Seul le WorldDirector** crée et détruit les acteurs du monde.
6. **Pas de `Get All Actors Of Class` par tick** : utilise le registre.
7. **Les agents s'identifient par leur `Id` SYNE** (Integer64), jamais par un nom ou un index de tableau.

### 0.3 Où tu en es

- **Fait côté C++** : le plugin compile, les 3 Automation Specs sont vertes (`PrismLdk.Projection`, `PrismLdk.WorldRegistry`, `PrismLdk.Service`), le service Launcher existe. Tu n'as plus rien à écrire en C++ pour cette partie.
- **Déjà créé côté Content** : `Core/BP_PRISM_GameInstance` et `Core/DA_PRISM_SessionConfig` (probablement vides ou incomplets).
- **À nettoyer** : `GameInstance_LDK`, `ldk_GameMode` et le doublon `LDK_BaseMap.umap` à la racine du Content.

| Jalon | Ce que tu fais | Test de fin |
| :-- | :-- | :-- |
| B1 | Session, registre, GameInstance | `OnConnected` reçu, spec registre verte |
| B2 | Cycle préparer → prêt → démarrer, sans rendu | snapshots reçus dans le log |
| B3 | Carte, tuiles, obstacles, marqueurs | le monde apparaît à la bonne échelle |
| B4 | Agents qui se déplacent | pas de doublon, pas de dérive |
| B5 | Interface et caméra | boutons et inspecteur fonctionnels |
| B6 | Événements, mutations d'obstacles | mutation appliquée une seule fois |
| B7 | Empaquetage Launcher | **à ne pas faire maintenant** |

## 1. Étape zéro : préparer l'environnement (15 minutes)

1. **Lancer le mock** : terminal dans le dossier `syne-mock` → `npm start -- config_prism.json`. Tu dois voir `SERVER listening` avec les ports 5181 (HTTP) et 5180 (WebSocket) et, au premier `Prepare`, `world=2240x2240 ... cells=4900`.
   - Utilise **`config_prism.json`**, pas les défauts du mock : c'est le miroir du profil `prism` du moteur (2 240 × 2 240, cellule 32, 6 TPS, 5 s/tick, aucun obstacle). Sans lui, le mock annonce un monde de 500 × 500 (cellule 10 → `k = 320`) que SYNE ne produira jamais, et tout ton rendu sera calibré sur une échelle fausse.
2. **Ouvrir** `prism/LDK/LDK.uproject` (UE 5.8.3). Vérifier dans **Edit → Plugins** que `PrismLdk` est activé.
3. **Window → Output Log**, filtre `LogPrismLdk`. Pour voir aussi les snapshots et deltas (niveau Verbose), tape dans la console de l'éditeur : `Log LogPrismLdk Verbose`.
4. **Project Settings → Maps & Modes** :
   - **Game Instance Class** = `BP_PRISM_GameInstance` ;
   - **Default GameMode** = `BP_PRISM_GameMode` (quand il existera, en B5) ;
   - **Editor Startup Map** et **Game Default Map** = `Level/LDK_BaseMap`.

> **\[Piège\]** Cette étape 4 n'est dite clairement ni dans la roadmap ni dans le doc d'implémentation. Sans elle, ton `BP_PRISM_GameInstance` n'est **jamais utilisé** : aucun `Init`, aucune connexion, et aucune erreur. La roadmap (§6.2.1) place un champ `GameInstanceClass` sur le GameMode ; dans Unreal la classe de GameInstance se règle normalement dans Project Settings. **\[À VÉRIFIER\]** dans ton éditeur.

5. **Habitude de travail** : après chaque graphe → **Compile**, **Save**, lancer **PIE**, lire `LogPrismLdk`. Utilise `Print String` pour tracer ce que tu ne comprends pas.

## 2. Jalon B1 — Fondations session et connexion

### 2.1 `DA_PRISM_SessionConfig` : remplir les valeurs

Ouvre l'asset (`Core/DA_PRISM_SessionConfig`) et compare champ par champ. Si un champ manque, ajoute-le avec le type indiqué (si l'asset est un Blueprint de classe PrimaryDataAsset, les valeurs se règlent dans **Class Defaults** ; si c'est une instance de data asset, dans le panneau **Details**).

| Champ | Type Blueprint | Valeur |
| :-- | :-- | :-- |
| `HttpUrl` | String | `http://127.0.0.1:5181` |
| `WebSocketUrl` | String | `ws://127.0.0.1:5180` |
| `bAutoReconnect` | Boolean | `true` |
| `ReconnectDelaySeconds` | Float (simple précision) | `2.0` |
| `DefaultSeed` | Integer64 | `42` |
| `DefaultTicksPerSecond` | Integer | `6` |
| `DefaultMaxTicks` | Integer | `400` (valeur par défaut de `Start` dans le plugin ; à ton choix) |
| `TileSizeUu` | Float **double précision** | **`3200`** |
| `bAutoStart` | Boolean | `false` pour l'instant |
| `DefaultWorldVersion` | String | `1.1` (simple valeur de repli) |

> **\[Piège\]** Si `TileSizeUu` n'est pas à 3200, `k` sera faux et la scène sera à la mauvaise échelle **sans aucune erreur visible**. Pas de champ `ScaleK` : `k` est calculé une seule fois par le registre.

### 2.2 `F_PrismSessionState` : la structure de session

**Content Browser → Add → Blueprints → Structure**, nom `F_PrismSessionState`, dossier `Core/`. Ajoute les 6 champs :

| Champ | Type |
| :-- | :-- |
| `Seed` | Integer64 |
| `TicksPerSecond` | Integer |
| `MaxTicks` | Integer |
| `RunId` | String |
| `WorldVersion` | String |
| `bWorldReady` | Boolean |

### 2.3 Le registre de monde : **rien à créer**

1. **Aucun Blueprint.** Le registre est le sous-système C++ `UPrismLdkWorldRegistryBase`, instancié par le moteur avec le plugin. Il n'y a **pas** de `BP_PRISM_WorldSubsystem` à créer, et s'il en existe un dans `Content/Core/`, **supprime-le** : il vivrait à côté du vrai registre et ne serait jamais celui que le nœud renvoie.
2. **Vérifier** : lance PIE et regarde `LogPrismLdk` — la ligne `registre de monde instancié (UPrismLdkWorldRegistryBase)` doit apparaître à chaque run, avant la construction du monde.
3. **Comment l'utiliser ailleurs** : le nœud **`PRISM Registry`** (`Get Prism Ldk World Registry`) renvoie le registre. Tu ne castes pas, tu ne crées pas de variable de copie.

Si la ligne n'apparaît pas alors que le plugin est chargé, c'est un défaut du plugin : signale-le plutôt que de chercher un asset.

### 2.4 `BP_PRISM_GameInstance` : le cœur du jalon

Ce Blueprint a un seul rôle : **seul interlocuteur du plugin**. Il écoute le plugin, retient l'état de session, et **re-diffuse** tout aux autres Blueprints via ses propres dispatchers.

#### Étape A — Variables

| Variable | Type | Remarque |
| :-- | :-- | :-- |
| `LDK` | Prism Ldk Subsystem (Object Reference) | vaut `None` tant qu'on ne l'affecte pas |
| `Session` | `F_PrismSessionState` | état de la session en cours |
| `Config` | `DA_PRISM_SessionConfig` (Object Reference) | **Class Defaults** : sélectionne ton asset comme valeur par défaut |

La roadmap cite aussi `bAutoStart` ici ; ne crée pas cette variable, lis directement `Config.bAutoStart` (une seule source).

#### Étape B — Les 8 Event Dispatchers

Dans **My Blueprint → + Event Dispatcher**, crée chacun avec ses entrées (sélectionne le dispatcher → **Details → Inputs → +**) :

| Dispatcher | Entrée |
| :-- | :-- |
| `ED_PRISM_Connected` | — |
| `ED_PRISM_Disconnected` | — |
| `ED_PRISM_WorldInitialized` | `World` : `FPrismSyneWorldDescription` |
| `ED_PRISM_Snapshot` | `Snapshot` : `FPrismSyneWorldSnapshot` |
| `ED_PRISM_Delta` | `Delta` : `FPrismSyneWorldDelta` |
| `ED_PRISM_Event` | `Event` : `FPrismSyneEvent` |
| `ED_PRISM_ControlResult` | `Result` : `FPrismSyneControlResult` |
| `ED_PRISM_Error` | `Error` : `FPrismSyneError` |

#### Étape C — Graphe d'initialisation (`Event Init`)

Dans l'Event Graph, clic droit → **Event Init** (c'est l'événement de démarrage du GameInstance). Câble :

```text
Event Init
 → Get Prism Ldk Subsystem          (nœud « SYNE » ; ou Get Game Instance Subsystem, classe PrismLdkSubsystem)
 → Set LDK
 → Is Valid (LDK) ─ non → Print String "LDK introuvable" (fin)
 → Make FPrismSyneConnectionOptions
     HttpUrl               ← Config.HttpUrl
     WebSocketUrl          ← Config.WebSocketUrl
     bAutoReconnect        ← Config.bAutoReconnect
     ReconnectDelaySeconds ← Config.ReconnectDelaySeconds
 → LDK.Configure (Options)
 → Assign OnConnected        → Custom Event EVT_OnConnected
 → Assign OnDisconnected     → Custom Event EVT_OnDisconnected
 → Assign OnWorldInitialized → Custom Event EVT_OnWorldInitialized
 → Assign OnSnapshot         → Custom Event EVT_OnSnapshot
 → Assign OnWorldDelta       → Custom Event EVT_OnWorldDelta
 → Assign OnSyneEvent        → Custom Event EVT_OnSyneEvent
 → Assign OnControlResult    → Custom Event EVT_OnControlResult
 → Assign OnError            → Custom Event EVT_OnError
```

Pour chaque `Assign`, tire le fil depuis la variable `LDK` (clic droit dans le graphe → cherche `Assign OnConnected`). Unreal crée automatiquement le Custom Event avec les bonnes entrées ; renomme-le comme ci-dessus.

> **\[Piège\]** `Assign` ne déclenche **pas** l'événement tout de suite : il l'enregistre seulement. Et n'appelle jamais `Configure` ni `Connect` avant que `Set LDK` ait été exécuté (sinon `None`).

#### Étape D — Les 8 Custom Events

Chaque événement fait son petit travail, puis **appelle le dispatcher projet correspondant** (nœud `Call ED_PRISM_…`).

| Custom Event | Avant de re-diffuser | Puis |
| :-- | :-- | :-- |
| `EVT_OnConnected` | `Branch (Config.bAutoStart)` → si vrai : `RequestStart(Config.DefaultSeed, Config.DefaultTicksPerSecond, Config.DefaultMaxTicks)` | `Call ED_PRISM_Connected` |
| `EVT_OnDisconnected` | rien | `Call ED_PRISM_Disconnected` |
| `EVT_OnWorldInitialized (World)` | `Set Session.WorldVersion` ← `World.Version` ; `Set Session.bWorldReady` ← `false` | `Call ED_PRISM_WorldInitialized(World)` |
| `EVT_OnSnapshot (Snapshot)` | `Set Session.RunId` ← `Snapshot.RunId` | `Call ED_PRISM_Snapshot(Snapshot)` |
| `EVT_OnWorldDelta (Delta)` | rien | `Call ED_PRISM_Delta(Delta)` |
| `EVT_OnSyneEvent (Event)` | rien | `Call ED_PRISM_Event(Event)` |
| `EVT_OnControlResult (Result)` | voir ci-dessous | `Call ED_PRISM_ControlResult(Result)` |
| `EVT_OnError (Error)` | `Print String` (code + message) pour le debug | `Call ED_PRISM_Error(Error)` |

**Cas `EVT_OnControlResult`** : ce dispatcher se déclenche après **chaque** commande HTTP (prepare, ready, start, pause…). Il faut donc filtrer :

```text
EVT_OnControlResult (Result)
 → Branch (Result.bOk)
    └ vrai → Branch (Result.Action == "ready")          [À VÉRIFIER : la valeur exacte du texte Action — affiche-la d'abord avec Print String]
              └ vrai → Set Session.bWorldReady = true → RequestStartRun()
 → Call ED_PRISM_ControlResult(Result)
```

Sans ce filtre, tu relancerais `Start` après chaque commande réussie.

#### Étape E — Les fonctions de relais

Crée ces fonctions dans le GameInstance. Elles sont appelées par l'interface (B5) et par les graphes ci-dessus :

| Fonction | Contenu |
| :-- | :-- |
| `RequestConnect()` | `LDK.Connect` ; si le retour est `false` → `Print String` |
| `RequestStart(Seed, TicksPerSecond, MaxTicks)` | `Set Session` (les 3 champs) → `LDK.Prepare(Seed, TicksPerSecond)` |
| `RequestReady()` | `LDK.Ready(Session.WorldVersion)` |
| `RequestStartRun()` | `LDK.Start(Session.Seed, Session.MaxTicks)` |
| `RequestPause()` / `RequestResume()` / `RequestStop()` | `LDK.Pause` / `LDK.Resume` / `LDK.Stop` |
| `RequestReset()` | `LDK.Reset(Session.Seed, Session.MaxTicks)` |
| `RequestStatus()` | `LDK.RequestStatus` |

La graine (`Seed`) reste dans `Session` pour pouvoir rejouer le même run avec `Reset`.

### 2.5 Test de fin de B1

Il n'y a pas encore d'interface : on fait un **test temporaire** dans le **Level Blueprint** de `LDK_BaseMap` (menu **Blueprints → Open Level Blueprint**) :

```text
Event BeginPlay → Delay (1.0)
 → Get Game Instance → Cast To BP_PRISM_GameInstance
 → RequestConnect
```

Puis dans `EVT_OnConnected`, ajoute un `Print String "Connecté"` temporaire.

1. Le mock tourne. Lance PIE.
2. **Attendu** : le terminal du mock affiche `WEBSOCKET connected` ; l'écran affiche « Connecté » ; `GetConnectionState` renvoie `Connected`.
3. Lance l'Automation Spec : **Tools → Session Frontend → Automation**, coche `PrismLdk.WorldRegistry`, **Start Tests** → vert.

B1 est terminé quand tout cela est vrai.

## 3. Jalon B2 — Cycle préparer → prêt → démarrer (sans rendu)

**But** : vérifier tout le dialogue avec SYNE avant de construire la moindre scène 3D. Tu n'as **rien de nouveau à créer** : tu complètes le test temporaire.

### 3.1 Ce que le cycle doit faire

```text
RequestStart(42, 6, 400)   → Prepare
OnWorldInitialized         → on lit la description, on règle le registre, on répond Ready
OnControlResult(ready, ok) → Start (déjà câblé en 2.4 Étape D)
OnSnapshot                 → un par tick, 6 par seconde
```

### 3.2 Compléter le Level Blueprint de test

Après `RequestConnect`, ajoute (toujours dans le Level Blueprint) :

```text
(après RequestConnect)
 → Assign ED_PRISM_Connected → Custom Event TEST_Connected
     TEST_Connected → RequestStart (42, 6, 400)

 → Assign ED_PRISM_WorldInitialized → Custom Event TEST_World (World)
     TEST_World
      → PRISM Registry → Set Tile Size Uu (Config.TileSizeUu)
      → PRISM Registry → Set Stats From Description (World)
      → PRISM Registry → Get Stats → Print String (K, CellSize, bScaleValid)
      → Get Game Instance → Cast → RequestReady

 → Assign ED_PRISM_Snapshot → Custom Event TEST_Snap (Snapshot)
     TEST_Snap → Print String (Snapshot.Tick, Snapshot.AliveCount)
```

`Config` se lit depuis le GameInstance casté (`Get Config`).

### 3.3 Test de fin de B2

- Le mock affiche dans l'ordre : `PREPARE`, `READY`, `START`.
- Tu vois les `Print String` de `TEST_Snap` défiler à environ 6 par seconde.
- `Get Stats` donne `bScaleValid = true`.
- **Regarde la valeur de `K`** : `k = TileSizeUu / CellSize`. Avec le profil `prism` de SYNE (`CellSize = 32`) tu obtiens **100**. Ton mock peut envoyer un autre `CellSize` (le doc d'implémentation cite 10 en exemple, ce qui donnerait 320) : ce n'est pas une erreur, c'est ce que le monde annonce. **\[À VÉRIFIER\]** la valeur envoyée par ton mock.
- Pas d'erreur `world_not_ready` ni `run_active` dans le log.

Tu supprimeras ce test temporaire quand le WorldDirector (B3) prendra le relais : c'est lui qui s'abonnera à `ED_PRISM_WorldInitialized`.

## 4. Jalon B3 — Construction du monde

**But** : quand `world_initialized` arrive, générer la scène (tuiles, obstacles, marqueurs) à la bonne échelle. Ordre de travail : les trois petits acteurs d'abord (4.2 à 4.4), puis le Director qui les assemble (4.5), puis la carte (4.6).

### 4.1 Rappel : deux sortes de coordonnées

| Donnée | Nature | Nœud à utiliser |
| :-- | :-- | :-- |
| `Cells[]` (X, Y) | indices de cellule | `Cell To Tile Center` (ou `Cell To Tile Origin`) |
| `Resources[]` initiales | **indices** de cellule | `Cell To Tile Center` |
| `Obstacles[]` | position continue + rayon | `Syne To Unreal` + `Syne Radius To Unreal` |
| `Agents[]` | position continue | `Syne To Unreal` |
| `Regions[]` | indices (origine de région) | pas un acteur : on n'en fait rien pour l'instant |

> **\[Piège\]** L'erreur de projection la plus probable : utiliser `Syne To Unreal` sur une ressource. C'est un indice de cellule, pas une position continue.

### 4.2 `BP_PRISM_TileField` (Actor) : le sol

**Composants** : un `HierarchicalInstancedStaticMeshComponent` nommé `HISM_Plains` (un seul composant pour 4 900 tuiles), plus un `HISM_Unknown` pour les terrains inconnus. Pour le `Static Mesh`, prends le **Plane** de base du moteur (`/Engine/BasicShapes/Plane`) et un matériau générique. Laisse la collision activée (les agents doivent marcher dessus).

**Variable** : `CellCountX` (Integer).

SYNE n'envoie aujourd'hui que le terrain `plains` ; la roadmap demande quand même un `Switch` avec une branche `Unknown`.

**Fonction `Clear()`** : `Clear Instances` sur chaque HISM.

**Fonction `Build(World : FPrismSyneWorldDescription)`** :

```text
Build (World)
 → Clear
 → Set CellCountX ← World.CellCountX
 → PRISM Registry → Get Stats → lire CellSize et K
 → Tile Size From Cell Size (CellSize, K) → TileSizeUu   (variable locale)
 → ForEach (World.Cells) :
      Switch on EPrismSyneTerrainType (terrain de la cellule)
         Plains  → cible = HISM_Plains
         défaut  → cible = HISM_Unknown
      Make Int Point (Cell.X, Cell.Y) → Cell
      Cell To Tile Center (Cell, CellSize, K) → Centre
      Make Transform : Location = Centre ; Scale = (TileSizeUu / 100, TileSizeUu / 100, 1)
      Add Instance (cible, Transform)
```

Pourquoi le centre et pas l'origine : le Plane de base a son pivot au centre. Si tu utilises un mesh dont le pivot est dans un coin, prends `Cell To Tile Origin`. Le `/ 100` n'est pas une formule de projection : c'est la taille du mesh de base. **\[À VÉRIFIER\]** la taille du Plane en l'ouvrant dans l'éditeur, et le nom exact du champ « terrain » dans la structure de cellule.

### 4.3 `BPI_PRISM_EntityView` et `BP_PRISM_Obstacle`

**D'abord l'interface** (utile dès maintenant, indispensable en B4). **Add → Blueprints → Blueprint Interface**, dossier `Entities/`, avec 3 fonctions :

| Fonction | Entrée |
| :-- | :-- |
| `ApplyAgentView` | `View` : `FPrismSyneAgentSnapshot` |
| `ApplyDelta` | `Change` : `FPrismSyneWorldDeltaChange` |
| `Reset` | — |

Une interface sert à dire « mets-toi à jour » à n'importe quel acteur sans connaître sa classe.

**Puis `BP_PRISM_Obstacle`** (Actor) :

- **Composant** : un `Static Mesh` (cylindre de base `/Engine/BasicShapes/Cylinder`) avec collision.
- **Variables** : `ObstacleId` (String), `RadiusUu` (Float double précision).
- **Class Settings → Interfaces → Add** : `BPI_PRISM_EntityView`.
- **Fonction `InitFromObstacle(Obstacle, RadiusUu)`** : `Set ObstacleId` ← `Obstacle.Id` ; `Set RadiusUu` ; `Set Actor Scale 3D` = (`RadiusUu × 2 / 100`, `RadiusUu × 2 / 100`, hauteur visuelle au choix, par exemple 3).
- **Événement d'interface `Reset`** : `Destroy Actor`.
- **Événement d'interface `ApplyDelta`** : vide pour l'instant (B6).

Le rayon arrive **déjà converti** en unités Unreal par le Director : l'obstacle ne refait aucun calcul d'échelle. Les obstacles viennent de SYNE : PRISM ne décide jamais d'un layout. **\[À VÉRIFIER\]** les dimensions du cylindre de base pour régler l'échelle.

### 4.4 `BP_PRISM_ResourceMarker` (Actor)

- **Composant** : un `Static Mesh` (sphère de base).
- **Variables** : `ResourceId` (String), `Type` (`EPrismSyneResourceType`), `Cell` (Int Point).
- **Fonction `InitFromResource(Resource)`** : `Set ResourceId`, `Set Type`, `Set Cell` ← (X, Y) de la ressource ; `Switch on EPrismSyneResourceType` : `Food`, `Water`, `Wood`, `Mineral` → un matériau de couleur différente chacun, `Unknown` → gris.

Ce marqueur est **purement décoratif** : les stocks des snapshots sont globaux par type, ils ne sont pas attachés à ces acteurs.

### 4.5 `BP_PRISM_WorldDirector` : le chef d'orchestre

C'est l'acteur le plus important. Il est **seul** à créer et détruire des acteurs du monde.

**Variables** : `Registry` (Prism Ldk World Registry Base), `Config` (`DA_PRISM_SessionConfig`, valeur par défaut = ton asset), `GI` (`BP_PRISM_GameInstance`), `TileField` (`BP_PRISM_TileField`), `bBuilding` (Boolean), `LastRunId` (String).

#### Étape A — `Event BeginPlay`

```text
Event BeginPlay
 → PRISM Registry → Set Registry
 → Get Game Instance → Cast To BP_PRISM_GameInstance → Set GI
 → Spawn Actor from Class (BP_PRISM_TileField) → Set TileField
 → GI : Assign ED_PRISM_WorldInitialized → Custom Event EVT_WorldInitialized (World)
 → GI : Assign ED_PRISM_Snapshot         → Custom Event EVT_Snapshot (Snapshot)
 → GI : Assign ED_PRISM_Delta            → Custom Event EVT_Delta (Delta)
 → GI : Assign ED_PRISM_ControlResult    → Custom Event EVT_ControlResult (Result)
```

`EVT_WorldInitialized (World)` → `BuildWorldFromDescription (World)` → `GI.RequestReady`. **Le `Ready` ne part qu'après la fin de la construction.** Et `Start` n'est appelé qu'après le `Ready` accepté (c'est déjà câblé dans le GameInstance, étape 2.4 D).

#### Étape B — `BuildWorldFromDescription (World)`

```text
BuildWorldFromDescription (World)
 → Set bBuilding = true
 → ResetWorld                                      (nettoie un éventuel ancien run)
 → Registry.SetTileSizeUu (Config.TileSizeUu)       → si le retour est false : Print « TileSizeUu invalide » et STOP
 → Registry.SetStatsFromDescription (World)
 → Registry.GetStats → Branch (bScaleValid)         → faux : Print « échelle invalide » et STOP
 → TileField.Build (World)
 → SpawnObstacles (World.Obstacles)
 → SpawnResourceMarkers (World.Resources)
 → SpawnInitialAgents (World.Agents)               (à compléter en B4)
 → (Regions : rien pour l'instant ; pas de sous-niveaux)
 → Set bBuilding = false
```

#### Étape C — Les fonctions de création

**`SpawnObstacles (Obstacles)`** :

```text
ForEach (Obstacles) :
   Syne To Unreal (Obstacle.Position, Stats.K)           → Location
   Spawn Actor from Class (BP_PRISM_Obstacle, Location)   (Collision Handling : Always Spawn, Ignore Collisions)
   InitFromObstacle (Obstacle, Syne Radius To Unreal (Obstacle.Radius, Stats.K))
   Registry.RegisterObstacle (Obstacle.Id, acteur)
```

**`SpawnResourceMarkers (Resources)`** :

```text
ForEach (Resources) :
   Branch (Resource.bHasPosition)     // la même structure sert aux stocks des snapshots, qui n'ont pas de position
     vrai → Make Int Point (Position.X, Position.Y) → Cell
            Cell To Tile Center (Cell, Stats.CellSize, Stats.K) → Location
            Spawn Actor from Class (BP_PRISM_ResourceMarker, Location)
            InitFromResource (Resource)
            Registry.AddResourceMarker (acteur)
```

**`ResetWorld ()`** : le registre connaît déjà tous les acteurs, pas besoin de tableaux en plus.

```text
ResetWorld
 → Registry.GetAgentIds     → ForEach → GetAgentActor → Is Valid → Destroy Actor
 → Registry.GetObstacleIds  → ForEach → GetObstacleActor → Is Valid → Destroy Actor
 → Registry.GetResourceMarkers → ForEach → Destroy Actor
 → TileField.Clear
 → Registry.ClearAll        (garde TileSizeUu)
 → Set LastRunId = ""
```

(La roadmap mentionne « vider les sous-niveaux runtime » : ignore, il n'y a plus de sous-niveaux.)

### 4.6 La carte `Level/LDK_BaseMap`

1. Ouvre la copie dans `Level/`. **Supprime le doublon** `LDK_BaseMap.umap` à la racine du Content (vérifie les références avant).
2. Place dans la carte : **un seul** `BP_PRISM_WorldDirector`, une `Directional Light`, un ciel/`Sky Light` génériques.
3. **NavMesh** (nécessaire en B4) : ajoute un `Nav Mesh Bounds Volume` couvrant la zone, et dans **Project Settings → Navigation Mesh** vérifie que la génération au runtime est dynamique, puisque le sol est créé pendant le jeu. **\[À VÉRIFIER\]** le nom exact de l'option. Le monde du profil `prism` fait 224 000 × 224 000 uu : commence par un volume plus petit pour tester.
4. **World Partition** : ne l'active pas maintenant. La décision se prend à la fin de B3 **sur mesure** : note le temps de chargement et la mémoire de la carte complète, et garde ces chiffres dans ton artefact de jalon.
5. Supprime le test temporaire de B2 dans le Level Blueprint, mais **garde** juste `RequestConnect` puis `RequestStart(42, 6, 400)` (en attendant l'interface).

### 4.7 Test de fin de B3

- Les tuiles apparaissent ; le monde fait la taille attendue (`CellCountX × TileSizeUu`).
- Obstacle de contrôle (si `k = 100`) : SYNE `(150, 120)`, rayon 8 → centre `(15000, 12000)` uu, rayon `800` uu.
- Marqueur de ressource en cellule `(5, 7)` → centre de tuile `(17600, 24000)` uu.
- `Registry.GetObstacleCount` = nombre d'obstacles de `World.Obstacles`.
- **Si aucun obstacle n'apparaît, c'est normal** tant que le serveur n'a pas de layout : SYNE a `world.obstacles = false` par défaut, et le nœud `Prepare` du plugin n'envoie pas de configuration de monde. Mets le layout dans le fichier de démarrage du mock (`world.obstacles: true` et `obstacleLayout: [ … ]`).
- Aucune formule d'échelle dans tes graphes : uniquement les nœuds du plugin.

## 5. Jalon B4 — Agents et navigation

**But** : un acteur par agent SYNE, retrouvé par son `Id`, qui marche vers la tuile annoncée par chaque snapshot.

### 5.1 `BP_PRISM_AgentController` (AIController)

**Variables** : `TargetPoint` (Vector), `bMoving` (Boolean).

**Événement `On Move Completed`** (hérité) : `Set bMoving = false`.

**Fonction `MoveToCell (Cell : Int Point)`** :

```text
MoveToCell (Cell)
 → PRISM Registry → Get Stats → CellSize, K
 → Tile Size From Cell Size (CellSize, K) → TileSizeUu
 → Cell To Tile Center (Cell, CellSize, K) → Centre
 → Get Random Reachable Point In Radius (Origin = Centre, Radius = TileSizeUu / 2)
      échec → Project Point To Navigation (Centre) ; si échec aussi : Print « pas de NavMesh » et STOP
 → Set TargetPoint
 → Set bMoving = true
 → Move To Location (TargetPoint, Acceptance Radius ≈ 50)
```

Pourquoi un rayon de `TileSizeUu / 2` : le cercle ainsi tracé tient **entièrement dans la tuile** ; un rayon plus grand pourrait choisir un point dans la tuile voisine (c'est le piège signalé par ton doc).

### 5.2 `BP_PRISM_Agent` (Character)

**Class Defaults** : `AI Controller Class` = `BP_PRISM_AgentController` ; `Auto Possess AI` = `Placed in World or Spawned`. **\[Piège\]** sans ce réglage, l'agent n'a pas de contrôleur et `Move To` ne fait rien, sans erreur.

**Visuel provisoire** : le LDK n'a pas d'art importé. Ajoute un `Static Mesh` simple (cône ou sphère de base) attaché à la capsule.

**Interfaces** : ajoute `BPI_PRISM_EntityView`.

**Variables** : `AgentId` (Integer64), `Species` (String), `CurrentAction` et `CurrentIntention` (`EPrismSyneAction`), `Energy`, `Hunger`, `Thirst`, `Fatigue` (Float double précision), `TargetCell` (Int Point), `bHasTargetCell` (Boolean), `LastSynePosition` (struct `PrismSyneVector2D`), `SpeedTrait` (Float double précision).

**Fonction `InitFromWorld (Agent : FPrismSyneWorldAgent)`** : `Set AgentId` ← `Agent.Id`, `Set Species`, `Set LastSynePosition` ← `Agent.Position`.

**Événement d'interface `ApplyAgentView (View)`** — le cœur de l'agent :

```text
ApplyAgentView (View)
 → Détection de changement : Branch (Position.X et Position.Y identiques à LastSynePosition
                                     ET Energy identique ET CurrentActionEnum identique)
      vrai → Return      (rien n'a changé : on sort tout de suite)
 → Set LastSynePosition ← View.Position
 → Set Energy / Hunger / Thirst / Fatigue ← View.…
 → Set CurrentAction ← View.CurrentActionEnum ; Set CurrentIntention ← View.CurrentIntentionEnum
 → Find (View.Traits, clé « speed ») : si trouvé → Set SpeedTrait
 → PRISM Registry → Get Stats
 → Max Walk Speed For Speed (SpeedTrait, Stats.K, Stats.TicksPerSecond) → Character Movement : Set Max Walk Speed
 → Syne To Cell (View.Position, Stats.CellSize, Stats.CellCountX, Stats.CellCountY) → NewCell
 → Branch (bHasTargetCell ET NewCell == TargetCell) → vrai : fin   (même cellule : on ne relance pas le déplacement)
 → Set TargetCell ← NewCell ; Set bHasTargetCell ← true
 → Get AI Controller → Cast To BP_PRISM_AgentController → MoveToCell (NewCell)
```

Les structures SYNE se comparent champ par champ (`Break` puis comparer X et Y). Les besoins non comparés ne sont pas perdus : l'inspecteur (B5) les lit dans le dernier snapshot.

**Règles** : ne **jamais** téléporter (`Set Actor Location` interdit dans cette fonction) ; l'agent peut rester un peu décalé du point SYNE exact, car la position logique SYNE reste l'état de référence.

**Fonction `ApplyAction (Action : FPrismSyneWorldAction)`** : sert au journal ou à une animation (par exemple mémoriser `OutcomeEnum`). Elle ne déduit **aucun** état : la vérité est dans le snapshot.

**Événement d'interface `Reset`** : `Destroy Actor`.

### 5.3 Compléter le `WorldDirector`

**`SpawnInitialAgents (Agents)`** :

```text
ForEach (Agents) :
   Syne To Unreal (Agent.Position, Stats.K) → Location   (ajoute Z + 200 pour poser l'agent au-dessus du sol)
   Spawn Actor from Class (BP_PRISM_Agent, Location)     (Collision : Try To Adjust Location But Always Spawn)
   InitFromWorld (Agent)
   Registry.RegisterAgent (Agent.Id, acteur) → si false : Destroy Actor + Print
```

**`UpdateFromSnapshot (Snapshot)`**, appelée par `EVT_Snapshot` :

```text
UpdateFromSnapshot (Snapshot)
 → Branch (bBuilding) → vrai : Return
 → Branch (LastRunId == "")      → vrai : Set LastRunId ← Snapshot.RunId
   sinon Branch (Snapshot.RunId != LastRunId)
        vrai → ResetWorld → GI.LDK.GetWorldDescription → BuildWorldFromDescription → Set LastRunId ← Snapshot.RunId
 → ForEach (Snapshot.Agents) :
      Registry.HasAgent (Id) ? non → spawn + RegisterAgent (comme SpawnInitialAgents)
      Registry.GetAgentActor (Id) → [Interface Message] ApplyAgentView (Agent)
      Add Id à SeenIds (tableau local)
 → Registry.GetAgentIds → ForEach : SeenIds contient Id ? non → GetAgentActor → Destroy Actor → UnregisterAgent
 → (B6) ReconcileObstacles (Snapshot.Obstacles)
```

Pour envoyer le message d'interface sans cast, utilise le nœud `ApplyAgentView (Message)` avec l'acteur du registre en cible.

**Dans `EVT_ControlResult (Result)`** : si `Result.bOk` et `Result.Action == "reset"` → `ResetWorld`. **\[À VÉRIFIER\]** la valeur exacte du texte `Action` (affiche-la d'abord). Après un `Reset`, il faut refaire `Prepare` pour obtenir un nouveau monde.

### 5.4 Test de fin de B4

- Les agents apparaissent et passent d'une tuile à l'autre.
- **Pas de doublon** : `Registry.GetAgentCount` = `Snapshot.AliveCount` (nombre d'agents du snapshot).
- **Vitesse** (avec `k = 100`, 6 ticks/s) : trait `speed = 0,5` → `MaxWalkSpeed = 300` ; `speed = 1,5` → `900`. Fais un `Print String` de `Max Walk Speed` pour un agent.
- `Reset` puis `Prepare` : le registre se vide puis se repeuple ; aucun snapshot de l'ancien run n'est appliqué.
- Si les agents ne bougent pas : voir la section « Dépannage ».

## 6. Jalon B5 — Interface, cadre de jeu et caméra

**But** : un parcours utilisateur complet (connecter → préparer → démarrer → observer → pause → reset) sans passer par le Level Blueprint de test.

### 6.1 `BP_PRISM_GameMode`, `BP_PRISM_ObserverPawn`, `BP_PRISM_PlayerController`

- **`BP_PRISM_GameMode`** (GameModeBase) : **Class Defaults** → `Player Controller Class` = `BP_PRISM_PlayerController`, `Default Pawn Class` = `BP_PRISM_ObserverPawn`. Rien à câbler de plus. Puis **Project Settings → Maps & Modes → Default GameMode** = `BP_PRISM_GameMode`. (Rappel : la classe de GameInstance se règle au même endroit, voir §1.)
- **`BP_PRISM_ObserverPawn`** : Blueprint dont la classe parente est **SpectatorPawn**. Rien à ajouter au début : il offre une caméra libre. Le joueur n'existe pas dans SYNE : ce pawn n'a ni besoins ni trajectoire simulée.
- **`BP_PRISM_CameraRig`** (optionnel, à faire en dernier) : Actor avec `Focus` (Actor), `Mode` (un enum à toi : `Free`, `TopDown`, `Orbit`, `FollowAgent`), `FollowAgentId` (Integer64) ; fonctions `SetMode`, `FocusOnAgent(Id)` (→ `Registry.GetAgentActor`), `ResetView`. La caméra suit visuellement ; elle ne modifie **jamais** la position simulée.

### 6.2 Recette commune à tous les widgets

1. **Add → User Interface → Widget Blueprint** (classe `User Widget`), dossier `UI/`.
2. Onglet **Designer** : pose les éléments (Text Block, Button, Editable Text, Progress Bar, Scroll Box).
3. Onglet **Graph** → `Event Construct` : `Get Game Instance` → `Cast To BP_PRISM_GameInstance` → `Set GI` → `Assign ED_PRISM_…` pour les dispatchers dont ce widget a besoin.
4. **Mets à jour dans les gestionnaires d'événements, pas dans `Event Tick`.** Un widget qui interroge l'état à chaque frame est un widget qui gaspille.

### 6.3 `WBP_PRISM_ConnectionPanel`

**Designer** : trois champs de saisie (`Seed`, `TicksPerSecond`, `MaxTicks`, pré-remplis depuis `Config`), huit boutons, un texte d'erreur.

| Bouton | Au clic (`OnClicked`) |
| :-- | :-- |
| Connecter | `GI.RequestConnect` |
| Préparer | convertis les champs texte en nombres (si la conversion échoue, prends les valeurs de `Config`) → `GI.RequestStart(Seed, TicksPerSecond, MaxTicks)` |
| Démarrer | `GI.RequestStartRun` |
| Pause / Reprendre / Arrêter | `GI.RequestPause` / `RequestResume` / `RequestStop` |
| Réinitialiser | `GI.RequestReset` |
| Statut | `GI.RequestStatus` |

**\[À VÉRIFIER\]** le nœud qui convertit un texte en Integer64 (cherche « To Integer64 »).

> **Décision à prendre ici.** En B2 tu as câblé un `Start` **automatique** dès que `Ready` est accepté (roadmap §2.1). Avec un bouton **Démarrer**, tu veux plutôt l'inverse : observer le monde construit **avant** de lancer. Retire alors l'appel `RequestStartRun` du `EVT_OnControlResult` du GameInstance et laisse le bouton le faire. Sinon `Start` partirait deux fois et SYNE répondrait `run_active`.

**Fonction `RefreshButtons`** : lis `Get Connection State` et `Get Status` → `State` (`EPrismSyneRunState`). Active chaque bouton selon ce tableau. **\[À VÉRIFIER\]** les valeurs exactes de `EPrismSyneRunState` en ouvrant l'enum dans l'éditeur, puis fais correspondre :

| Bouton | Actif quand |
| :-- | :-- |
| Connecter | la connexion n'est pas `Connected` |
| Préparer | connecté et aucun run actif |
| Démarrer | `Session.bWorldReady` vrai et run non démarré |
| Pause | run en cours |
| Reprendre | run en pause |
| Arrêter | run en cours ou en pause |
| Réinitialiser | run arrêté ou terminé |
| Statut | connecté |

Appelle `RefreshButtons` depuis les gestionnaires de `ED_PRISM_Connected`, `ED_PRISM_Disconnected` et `ED_PRISM_ControlResult` (le résultat porte un `Status`).

**Erreurs** : dans le gestionnaire de `ED_PRISM_Error`, affiche `Error.Code` et `Error.Message` dans la zone d'erreur. Codes à connaître : `world_not_ready`, `run_active`, `run_finished`, `prepared_seed_mismatch`. Ne suppose jamais qu'une commande a réussi.

### 6.4 `WBP_PRISM_HUD`

Etiquettes : connexion, état SYNE, `RunId`, `Tick / MaxTicks`, population (`AliveCount`), saison (`SeasonEnum`), horloge. Tout se met à jour dans le gestionnaire de `ED_PRISM_Snapshot` (`Snapshot.Tick`, `Snapshot.AliveCount`, `Snapshot.SeasonEnum`), plus `Get Status` pour `MaxTicks`.

**Horloge** : affiche le temps simulé du snapshot **et** le ratio `R = TicksPerSecond × SimulatedSecondsPerTick` lu dans `Get Stats` du registre. Au profil `prism`, `R = 6 × 5 = 30` : une seconde réelle = 30 secondes simulées, soit une journée simulée en 48 minutes.

### 6.5 `WBP_PRISM_AgentInspector`

- **Variable** : `SelectedId` (Integer64). **Fonction `ShowAgent(Id)`** : mémorise l'Id et rafraîchit.
- **Mise à jour** : dans le gestionnaire de `ED_PRISM_Snapshot`, cherche l'agent d'Id `SelectedId` dans `Snapshot.Agents` (boucle) et remplis : identifiant, espèce, besoins (`Progress Bar`), intention, action, `Traits` (ForEach sur la Map), `Beliefs`, `Goals`, `Trust`, `MemoryCount`.
- **Règle** : la source est le dernier snapshot, jamais un cache indépendant. Un champ vide (par exemple `Beliefs` ou `Trust` avec le mock) reste vide (« — »). On ne complète pas par supposition.

### 6.6 `WBP_PRISM_EventLog` et `WBP_PRISM_Diagnostics`

- **EventLog** : une `Scroll Box` ; à chaque `ED_PRISM_Event`, ajoute une ligne (type, tick, agent, `ValueJson` brut). Garde une branche `Unknown`. **Limite** la liste (par exemple 200 lignes, supprime la plus ancienne) : SYNE peut émettre jusqu'à N × T événements par seconde.
- **Diagnostics** : version du monde (`World.Version`, via `LDK.GetWorldDescription`), `EngineVersion` du snapshot, `TicksPerSecond`, `RunId`, tick, `MaxTicks`, population, dernière erreur, état de connexion.

### 6.7 `BP_PRISM_PlayerController`

**Variables** : une référence par widget (`ConnectionPanel`, `HUD`, `Inspector`, `EventLog`, `Diagnostics`) et `bUIHidden` (Boolean).

**`Event BeginPlay`** : `CreateWidgets` → pour chacun : `Create Widget` → `Add to Viewport` → `Set` de la référence. Puis `Set Show Mouse Cursor = true` et `Set Input Mode Game And UI`.

**Entrées (Enhanced Input)** :

1. Crée les **Input Actions** `IA_ToggleUI`, `IA_ToggleDiagnostics`, `IA_FocusOnAgent` (clic gauche), `IA_QuitObservations`, et un **Input Mapping Context** (par exemple `IMC_PRISM`) qui associe une touche à chacune.
2. Au `BeginPlay` : `Get Enhanced Input Local Player Subsystem` → `Add Mapping Context (IMC_PRISM)`.
3. Événements : `IA_ToggleUI` → `ToggleUI` (inverse `bUIHidden`, puis `Set Visibility` sur les widgets) ; `IA_ToggleDiagnostics` → idem pour `Diagnostics` ; `IA_QuitObservations` → `Quit Game`.

**Sélection d'un agent** (`IA_FocusOnAgent`) :

```text
Get Hit Result Under Cursor by Channel (Visibility)
 → Break Hit Result → Hit Actor
 → Cast To BP_PRISM_Agent → échec : fin
 → SelectAgent (AgentId)
 → Inspector.ShowAgent (AgentId)
```

> **\[Piège\]** Si le clic ne touche jamais l'agent, la capsule ou le mesh de l'agent ne bloque pas le canal `Visibility` (règle les **Collision Responses**).

### 6.8 Test de fin de B5

Sans plus rien toucher au Level Blueprint (retire le test) : **Connecter → Préparer → Démarrer → Pause → Reprendre → Arrêter → Réinitialiser**. Les boutons s'activent et se désactivent selon l'état réel, l'inspecteur affiche l'agent cliqué, les erreurs s'affichent avec leur code.

## 7. Jalon B6 — Événements, mutations d'obstacles, reconnexion

### 7.1 Appliquer les mutations d'obstacles sans les doubler

Le snapshot décrit déjà l'état complet des obstacles ; les deltas décrivent les mêmes changements. **Règle** : le snapshot fait foi, et chaque application doit être **idempotente** (rejouée deux fois, elle ne change rien).

**`ReconcileObstacles (Obstacles)`** (appelée depuis `UpdateFromSnapshot`) :

```text
ForEach (Obstacles du snapshot) :  Registry.HasObstacle (Id) ? non → spawn (comme SpawnObstacles)
Registry.GetObstacleIds → ForEach : absent du snapshot ? → GetObstacleActor → [Interface] Reset → UnregisterObstacle
```

**`EVT_Delta (Delta)`** (Director) :

```text
Branch (Delta.RunId != LastRunId) → vrai : Return        (delta d'un ancien run)
ForEach (changements du delta) :
   Kind == added   → Registry.HasObstacle (Id) ? oui : ignorer / non : spawn
   Kind == removed → Registry.HasObstacle (Id) ? non : ignorer / oui : [Interface] Reset, puis UnregisterObstacle
```

Si tu veux un **effet visuel** (particules, son) sur une mutation, joue-le depuis le delta, mais ne recrée jamais l'acteur s'il existe déjà. **\[À VÉRIFIER\]** les noms des champs de `FPrismSyneWorldDelta` et de ses changements (`Kind`, `RunId`, `Tick`).

### 7.2 Journal des événements

Dans `EVT_Event` du Director (ou directement dans `WBP_PRISM_EventLog`), fais un `Switch` sur le type d'événement (`tick_summary`, `decision_made`, `action_completed`, `message_sent`, `message_received`, `group_decision`, `world.book_written`, `world.season_changed`…) avec une branche `Unknown` qui affiche le texte brut. Tu peux filtrer `tick_summary` et `decision_made`, très fréquents. **Ne réapplique jamais** une mutation d'état depuis un événement.

### 7.3 Reconnexion (politique retenue : reprendre sans relancer)

1. `EVT_OnDisconnected` : le HUD affiche « Déconnecté » ; ne détruis rien ; les agents gardent leur dernière position connue. Avec `bAutoReconnect = true`, le plugin retente tout seul après le délai.
2. `EVT_OnConnected` (GameInstance) : `Branch (Session.RunId != "")` → vrai : `RequestStatus`.
3. Au résultat du statut : même `RunId` → reprends la consommation (SYNE ne rejoue pas les trames manquées : un saut de tick est normal) ; `RunId` différent ou run terminé → propose `Reset` dans l'interface.
4. Ne relance **jamais** `Start` automatiquement après une erreur.

**Test** : coupe le mock en pleine simulation → `OnDisconnected` et `OnError`, pas de crash ; relance-le → l'interface propose `Reset` si le run est différent.

### 7.4 Test de fin de B6

Une mutation d'obstacle ne produit **qu'un seul** acteur (ou une seule suppression) même si snapshot, delta et événement la rapportent tous les trois.

## 8. Dépannage : symptômes fréquents

| Symptôme | Cause probable | À vérifier |
| :-- | :-- | :-- |
| Rien ne se passe, `Init` jamais appelé | Game Instance Class non réglée | Project Settings → Maps & Modes |
| `Accessed None` sur `LDK` | `Set LDK` pas exécuté avant l'appel | `Is Valid` juste après `Set LDK` |
| Nœud introuvable dans le menu | titre compact du plugin | cherche `SYNE`, puis `Get Prism Ldk Subsystem` |
| Pas de ligne `registre de monde instancié` | plugin non chargé (ou module pas recompilé après un `git pull`) | recompiler, vérifier que `PrismLdk` est activé dans `.uproject` |
| `OnConnected` jamais reçu | mock non lancé, mauvais ports, `Connect` pas appelé | log du mock, `Config.WebSocketUrl` |
| `409 world_not_ready` | `Start` envoyé avant l'accord de `Ready` | ordre : Prepare → world\_initialized → Ready → ok → Start |
| `run_active` / `run_finished` | commande dans le mauvais état | `Reset` d'abord |
| `prepared_seed_mismatch` | `Start` avec une autre seed que `Prepare` | utilise `Session.Seed` |
| Monde à la mauvaise échelle | `TileSizeUu` différent de 3200, ou `CellSize` inattendu | `Print` de `Get Stats` (`K`, `CellSize`) |
| Ressources mal placées | `Syne To Unreal` utilisé au lieu de `Cell To Tile Center` | §4.1 |
| Aucun obstacle | pas de layout côté serveur | fichier de démarrage du mock |
| Les agents ne bougent pas | pas d'AI Controller, `Auto Possess AI` mal réglé, NavMesh absent ou pas généré | §4.6 et §5.2 ; en PIE, la touche `P` affiche le NavMesh |
| Agents qui dérivent | `MaxWalkSpeed` faux, ou téléportation | `Max Walk Speed For Speed` avec `K` et `TicksPerSecond` |
| Agents en double | spawn sans `HasAgent`, deux Directors dans la carte, `Assign` répété | un seul Director ; `Assign` une seule fois |
| Mutations d'obstacle doublées | application non idempotente | §7.1 |
| Chute de FPS | `Get All Actors Of Class` ou widget qui interroge à chaque `Tick` | utilise le registre et les dispatchers |

## 9. Incohérences repérées dans tes deux documents

Elles expliquent probablement une partie de la confusion. À corriger dans la doc :

1. **Version du monde** : le doc d'implémentation (§4) cite `"1.0"`, la roadmap `"1.1"` (en repli). La règle retenue est de lire `World.Version`.
2. **Cadence** : le doc d'implémentation dit qu'un tick = une minute simulée (6 ticks/s = 360×), la roadmap dit `simulatedSecondsPerTick = 5` au profil `prism` (R = 30). La valeur qui fait foi est celle reçue dans `world_initialized` ; mets à jour la table de cadence du doc d'implémentation.
3. **Exemples d'échelle** : le doc d'implémentation garde en exemple `CellSize = 10`, tuile 100 uu, `k = 10`, alors que le profil retenu est `32` / `3200` / `100`.
4. **GameInstance** : la roadmap (§6.2.1) place `GameInstanceClass` sur le GameMode ; ça se règle dans Project Settings (à vérifier).
5. **`Session.bWorldReady`** : écrit par le Director (§6.3.1, étape 7) alors que `Session` vit dans le GameInstance. Ce guide le fait dans le GameInstance.
6. **Sous-niveaux** : `ResetWorld` mentionne encore « vider les sous-niveaux runtime » alors qu'ils sont abandonnés.
7. **Horloge** : le HUD parle de `simulatedTimeSeconds`, la structure du snapshot (§5.3) expose `SimulatedTimeMinutes`.
8. **Nom de fonction** : `InitFromId(Id)` (§6.3.1) et `InitFromWorld(FPrismSyneWorldAgent)` (§6.4.1) désignent la même fonction.
9. **`bAutoStart`** : présent à la fois dans le GameInstance et dans `DA_PRISM_SessionConfig` ; garde une seule source (la config).
10. **Nombre de tests** : la roadmap annonce 34 tests en B0 et 30 tests dans la commande de rejeu (§13).

## 10. Jalon B7 — à ne pas commencer maintenant

B7 (empaquetage Shipping, `component.json`, intégration Launcher) vient après que B1 à B6 fonctionnent avec le mock. Un seul point à retenir pour plus tard : **sous Launcher, PRISM ne reçoit pas les URLs de SYNE**. Il faudra choisir une voie (un argument `--ws-url`, des variables d'environnement, ou un `launchArgument` du manifeste), l'écrire dans le contrat et la tester, avant ce jalon. Détail : roadmap §2.2, §10 (B7) et §11.

## 11. Par où commencer concrètement

1. Project Settings : Game Instance Class, Game Default Map (§1).
2. Remplir `DA_PRISM_SessionConfig` (§2.1) et créer `F_PrismSessionState` (§2.2) — le registre (§2.3) n'a rien à créer.
3. Câbler `BP_PRISM_GameInstance` dans l'ordre A, B, C, D, E (§2.4).
4. Test du Level Blueprint (§2.5), puis lancer la spec `PrismLdk.WorldRegistry`.

Quand ces quatre points sont verts, B1 est fini et le reste suit le même schéma : un élément à la fois, un test à la fin de chaque jalon. Si un graphe te bloque, dis-moi lequel (et ce que tu vois dans `LogPrismLdk`) et on le reprend nœud par nœud.
