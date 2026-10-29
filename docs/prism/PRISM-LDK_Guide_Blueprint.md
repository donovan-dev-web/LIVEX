# PRISM-LDK — Guide Blueprint minimal, pas à pas (v2)

**UE 5.8 · 26 octobre 2026 · Remplace « Guide utilisateur Blueprint pas à pas » (version unifiée)**
**Périmètre :** les Blueprints, widgets et maps sont créés dans `/Plugins/PrismLdk/Content/`. Le C++ du plugin n'est pas modifié.

> **Ce que ce guide change par rapport à la version unifiée.** Même flux, même plugin, mais moins d'assets (plus d'interface Renderer, de GameInstance relais ni de AgentController), un seul `enum` de phase à la place d'une vingtaine de booléens, aucun relais d'événements, aucune interface Renderer, aucun watchdog, aucune génération multiple. Chaque graphe est donné sous forme de schéma de nodes (notation ci-dessous).
>
> **Vérification :** les noms de nodes, de pins, de structs et d'événements de ce guide ont été contrôlés contre les `.h` du plugin (`PrismLdkSubsystem`, `PrismLdkTypes`, `PrismLdkProjection`, `PrismLdkWorldRegistry`, `PrismLdkBlueprintLibrary`, `PrismLdkPCGData`, `PrismLdkVirtualWorld`). Je n'ai pas pu ouvrir l'éditeur : les quelques points qui ne se voient que dans l'éditeur sont marqués **[À VÉRIFIER]** et regroupés en §15.

---

## 0. Notation des schémas

Chaque graphe est écrit avec ces conventions :

| Symbole | Sens |
|---|---|
| `[1]`, `[2]`… | Node **d'exécution** (fil blanc), numéroté dans l'ordre d'exécution |
| `(a)`, `(b)`… | Node **pur** (sans fil blanc), calcule une donnée |
| `▶` | Fil d'**exécution** : « puis exécute » |
| `◀` | Fil de **données** : la valeur de ce pin vient de… |
| `Pin ◀ Get X` | Relier la sortie de la variable `X` (Get) à ce pin |
| `(fin)` | Le chemin d'exécution s'arrête ici |

Exemple :

```
[1] Custom Event  EVT_Exemple        Valeur : Integer
    ▶ [2]
[2] Branch
    Condition ◀ (a)
    True  ▶ [3]
    False ▶ (fin)
[3] Set Compteur
    Compteur ◀ Valeur (pin du node [1])
    ▶ (fin)
(a) > (Integer)    A ◀ Valeur    B ◀ 0
```

Lecture : le Custom Event donne l'exécution au Branch. Le Branch lit le résultat du node pur `(a)`. Sur `True`, on exécute `[3]` qui met la variable `Compteur` à la valeur reçue.

Les noms de nodes sont ceux qui apparaissent dans le menu clic droit (`Get Prism Ldk Subsystem`, `Branch`…).

**Affichage des pins :** Unreal retire le préfixe `b` des booléens et espace les noms. `bOk` s'affiche **Ok**, `bScaleValid` **Scale Valid**, `bAutoReconnect` **Auto Reconnect**, `TileSizeUu` **Tile Size Uu**. Dans ce guide, les noms sont écrits comme dans les `.h` ; cherchez-les sous leur forme affichée.

---

## 1. Le flux visé

```
DÉMARRAGE   LDK_MenuMap (persistante) : PlayerController crée les 3 widgets
            Phase = Disconnected

CONNEXION   Clic Connect → Director.Configure + Connect
            OnConnected reçu → Phase = Idle   (boutons Logique / Virtuel actifs)

CHARGEMENT  Clic Logique/Virtuel → réglages mémorisés
            Phase = Streaming   Load Stream Level (sous-niveau choisi) → Completed
            Phase = Preparing   Prepare(Seed, TPS, ConfigJson)
                                attendre OnWorldInitialized ET ControlResult(prepare ok)
            Phase = Building    Logique : TileField + obstacles (instantané)
                                Virtuel : StartBuildWorldAsync → OnBuildCompleted
                                puis agents initiaux
            Phase = Readying    Ready(World.Version)
            Phase = Ready       ControlResult(ready ok) → HUD affiché

SIMULATION  Clic Start → Running ⇄ Paused (Pause / Resume)
            Chaque OnSnapshot → agents créés / déplacés / supprimés

FIN         Clic Stop → Stop → ControlResult → nettoyage → Unload Stream Level → Idle
```

**Il n'y a que deux « chefs » :**

- Le **WorldDirector** (un seul acteur dans `LDK_MenuMap`) parle au plugin, possède la phase, le streaming, le build, les agents et le nettoyage.
- Le **PlayerController** crée les widgets. Les widgets lisent la phase du Director et appellent ses fonctions.

Le GameInstance ne fait que garder la Config et les réglages (Seed, ticks/s, MaxTicks).

### Règles à ne jamais enfreindre

1. **Aucune formule de conversion dans un graphe** : on utilise les nodes `SYNE|Projection` du plugin.
2. **`K` n'est jamais une variable Blueprint** : on le lit avec `Registry → Get Stats` (struct `FPrismLdkWorldStats`).
3. **Le snapshot est la vérité** : `OnSnapshot` pilote les agents. Rien d'autre ne décide de leur existence.
4. **Seul le Director appelle le plugin** (`Configure`, `Connect`, `Prepare`, `Ready`, `Start`, `Pause`, `Resume`, `Stop`).
5. **Seul le Director crée et détruit les acteurs du monde.**
6. **Pas de `Get All Actors Of Class` par tick.** On utilise le registre. `Get Actor Of Class` n'est appelé qu'une seule fois par chargement (§9.9).
7. **Les agents s'identifient par leur `Id` SYNE** (Integer64).

---

## 2. Assets à créer (dans cet ordre)

| # | Asset | Dossier | Type |
|---|---|---|---|
| 1 | `EPrismRenderMode`, `EPrismPhase` | `Core/` | Enumerations |
| 2 | `BP_PRISM_SessionConfig` + `DA_PRISM_SessionConfig` | `Core/` | PrimaryDataAsset + instance |
| 3 | `BP_PRISM_GameInstance` | `Core/` | GameInstance |
| 4 | `LDK_MenuMap`, `LDK_LogicalMap`, `LDK_VirtualMap` | `Level/` | Maps |
| 5 | `BP_PRISM_TileField` | `World/` | Actor |
| 6 | `BP_PRISM_Obstacle` | `World/` | Actor |
| 7 | `BP_PRISM_Agent` | `World/` | Character |
| 8 | `BP_PRISM_VirtualWorld` | `World/` | Enfant de `Prism Ldk Virtual World Actor` |
| 9 | `BP_PRISM_WorldDirector` | `World/` | Actor |
| 10 | `WBP_PRISM_MainMenu`, `WBP_PRISM_Loading`, `WBP_PRISM_HUD` | `UI/` | Widget Blueprints |
| 11 | `BP_PRISM_ObserverPawn`, `BP_PRISM_PlayerController`, `BP_PRISM_GameMode` | `Framework/` | SpectatorPawn, PlayerController, GameModeBase |

Pas de `BP_PRISM_AgentController` : le `AIController` natif suffit. Pas de `BPI_PRISM_Renderer`, de `RendererBase`, de `LogicalRenderer`, de `VirtualRenderer`.

---

## 3. Étape 0 — Préparer le projet

1. Ouvrez le projet (`LDK.uproject`) sous **UE 5.8**. Activez `PrismLdk` dans **Edit → Plugins** si besoin, puis compilez la cible Editor.
2. Dans le Content Browser : **Settings (View Options) → Show Plugin Content** et **Show Engine Content** (nécessaire pour les formes de base `/Engine/BasicShapes/`).
3. Dans `Plugins/PrismLdk/Content/`, créez les dossiers `Core`, `Framework`, `Level`, `UI`, `World`.
4. **Project Settings → Engine → Navigation Mesh → Runtime → Runtime Generation = Dynamic** (le sol est créé pendant le jeu).

**Test :** dans un Blueprint quelconque, ces nodes sont trouvables : `Get Prism Ldk Subsystem`, `Get Prism Ldk World Registry`, `Syne To Unreal`, `Create PCG Data From World Description`. Sinon, corrigez l'activation/compilation du plugin avant toute chose.

---

## 4. Étape 1 — Enums, configuration et GameInstance

### 4.1 Les deux enums

**Add → Blueprints → Enumeration**, dans `Core/` :

- `EPrismRenderMode` : `Logical`, `Virtual`.
- `EPrismPhase` : `Disconnected`, `Idle`, `Streaming`, `Preparing`, `Building`, `Readying`, `Ready`, `Running`, `Paused`, `Stopping`, `Unloading`.

L'ordre des valeurs doit être exactement celui-ci. **Phase** est la seule variable d'état du flux : tous les boutons et tous les écrans en dépendent.

### 4.2 `BP_PRISM_SessionConfig` et `DA_PRISM_SessionConfig`

1. **Add → Blueprint Class → All Classes → PrimaryDataAsset** → `BP_PRISM_SessionConfig`.
2. Ajoutez ces variables, compilez, sauvegardez :

| Variable | Type | Valeur indicative |
|---|---|---|
| `HttpUrl` | String | `http://127.0.0.1:5181` |
| `WebSocketUrl` | String | `ws://127.0.0.1:5180` |
| `bAutoReconnect` | Boolean | true |
| `ReconnectDelaySeconds` | Float | 2.0 |
| `DefaultSeed` | Integer64 | 42 |
| `DefaultTicksPerSecond` | Integer | 6 |
| `DefaultMaxTicks` | Integer | 400 |
| `WorldConfigJson` | String | contenu de `configs/simulation/prism.json` sur une ligne |
| `TileSizeUu` | Double | 3200 |

3. **Add → Miscellaneous → Data Asset** → choisissez `BP_PRISM_SessionConfig` → nommez l'instance `DA_PRISM_SessionConfig` et renseignez les valeurs.

`K` n'est pas dans cette liste : le registre le calcule (`K = TileSizeUu / CellSize`). Ne stockez jamais de vrai jeton dans cet asset.

### 4.3 `BP_PRISM_GameInstance`

1. **Add → Blueprint Class → All Classes → GameInstance** → `BP_PRISM_GameInstance`.
2. Variables :

| Variable | Type | Défaut |
|---|---|---|
| `Config` | `BP_PRISM_SessionConfig` (Object Reference) | **choisir `DA_PRISM_SessionConfig` dans Class Defaults** |
| `Seed` | Integer64 | 42 |
| `TicksPerSecond` | Integer | 6 |
| `MaxTicks` | Integer | 400 |

3. Graphe (Event Graph) :

```
[1] Event Init
    ▶ [2]
[2] Set Seed              Seed ◀ (a)
    ▶ [3]
[3] Set TicksPerSecond    TicksPerSecond ◀ (b)
    ▶ [4]
[4] Set MaxTicks          MaxTicks ◀ (c)
    ▶ (fin)
(a) Get Config → DefaultSeed
(b) Get Config → DefaultTicksPerSecond
(c) Get Config → DefaultMaxTicks
```

`Config` est une référence directe (pas de chargement asynchrone, pas de polling) : l'asset est chargé en même temps que le GameInstance.

4. **Project Settings → Maps & Modes → Game Instance Class = `BP_PRISM_GameInstance`.**

**Test :** compilez. Les valeurs par défaut de `Config` apparaissent dans les Class Defaults.

---

## 5. Étape 2 — Les trois maps

### 5.1 Création

Dans `Level/` : **Add → Level → Empty Level**, trois fois : `LDK_MenuMap`, `LDK_LogicalMap`, `LDK_VirtualMap`.

### 5.2 Contenu de chaque map

- **`LDK_MenuMap`** (persistante) : un `PlayerStart`. Rien d'autre pour l'instant (le Director y sera posé au §11.5).
- **`LDK_LogicalMap`** : un `Directional Light`, un `Sky Light`, un `Nav Mesh Bounds Volume`.
- **`LDK_VirtualMap`** : un `Directional Light`, un `Sky Light` (+ ciel/atmosphère si vous voulez), un `Nav Mesh Bounds Volume`. Le `BP_PRISM_VirtualWorld` sera posé à l'étape 6.

Chaque sous-niveau porte **son propre éclairage** : pas de carte d'éclairage séparée.

Pour les deux `Nav Mesh Bounds Volume` : position `(112000, 112000, 0)` et taille `230000` sur chaque axe (Details → Brush Settings). Ils couvrent les 224 000 × 224 000 uu du monde du profil `prism`.

### 5.3 Enregistrer les sous-niveaux dans la map persistante

1. Ouvrez `LDK_MenuMap`, puis **Window → Levels**.
2. **Levels → Add Existing…** : ajoutez `LDK_LogicalMap` puis `LDK_VirtualMap`.
3. Pour chacun : clic droit → **Change Streaming Method → Blueprint**. Dans ses Details, décochez **Initially Loaded** et **Initially Visible** **[À VÉRIFIER : libellés exacts]**.
4. Sauvegardez `LDK_MenuMap`.

### 5.4 Noms de package à utiliser dans les nodes

`Load Stream Level` traite un nom **sans `/`** comme un asset de `/Game/`. Les maps du plugin demandent donc le **nom de package complet** :

- `/PrismLdk/Level/LDK_LogicalMap`
- `/PrismLdk/Level/LDK_VirtualMap`

Ces deux valeurs seront les défauts des variables `LogicalPackage` et `VirtualPackage` du Director (§9.1).

### 5.5 Réglages de projet maintenant

**Project Settings → Maps & Modes :** `Editor Startup Map` et `Game Default Map` = `LDK_MenuMap`. Le GameMode sera réglé au §11.4.

### 5.6 Test rapide du streaming (graphe temporaire)

Dans le **Level Blueprint** de `LDK_MenuMap` :

```
[1] Keyboard 1  (Pressed)
    ▶ [2]
[2] Load Stream Level
    Level Name ◀ "/PrismLdk/Level/LDK_LogicalMap"
    Make Visible After Load ◀ true
    Should Block on Load ◀ false
    Completed ▶ [3]
[3] Print String   In String ◀ "Logique chargé"
```

Lancez PIE, appuyez sur `1` : le message s'affiche et la map Logique devient visible. Répétez avec `/PrismLdk/Level/LDK_VirtualMap`. **Supprimez ce graphe temporaire** ensuite.

**Test :** les deux sous-niveaux se chargent par leur nom de package. Sinon, le nom est faux : corrigez-le avant d'aller plus loin.

---

## 6. Étape 3 — Les acteurs de rendu logique

### 6.1 `BP_PRISM_TileField` (sol logique)

1. **Add → Blueprint Class → Actor** → `BP_PRISM_TileField`.
2. Composants : deux **`Hierarchical Instanced Static Mesh`** nommés `HISM_Plains` et `HISM_Unknown`.
3. Pour chacun, **Static Mesh = `/Engine/BasicShapes/Cube`** (cube de 100 uu, pivot au centre, avec collision : les agents marchent dessus). Collision Presets = `BlockAll`. Donnez un matériau différent à chacun si vous voulez les distinguer.

**Fonction `Build(World : FPrismSyneWorldDescription) → Boolean`** (onglet Functions, Pure décoché) :

```
[1] Function Entry  Build      World : FPrismSyneWorldDescription
    ▶ [2]
[2] Clear Instances   Target ◀ HISM_Plains
    ▶ [3]
[3] Clear Instances   Target ◀ HISM_Unknown
    ▶ [4]
[4] Branch            Condition ◀ (a)         (a = description invalide ?)
    True  ▶ [5]
    False ▶ [6]
[5] Return Node       ReturnValue ◀ false
[6] ForEachLoop       Array ◀ (b)             (b = Break World → Cells)
    Loop Body ▶ [7]
    Completed ▶ [11]
[7] Switch on Enum EPrismSyneTerrainType    Selection ◀ (c)   (c = ArrayElement → TerrainTypeEnum)
    Plains  ▶ [8]
    default ▶ [9]
[8] Add Instance      Target ◀ HISM_Plains   Instance Transform ◀ (g)   ▶ (retour boucle)
[9] Add Instance      Target ◀ HISM_Unknown  Instance Transform ◀ (g)   ▶ (retour boucle)
[11] Return Node      ReturnValue ◀ true

(d) Make Int Point                  X ◀ ArrayElement.X    Y ◀ ArrayElement.Y
(e) Get Prism Ldk World Registry → Get Stats → Break FPrismLdkWorldStats   (TileSizeUu)
(f) Cell To Tile Center Unreal      Cell ◀ (d)                (World Context = Self ; passe par le registre)
(h) Stats.TileSizeUu                                          → TileUu
(i) Make Vector                     X ◀ (f).X   Y ◀ (f).Y   Z ◀ -50      (la face haute du cube est à Z = 0)
(j) Make Vector (échelle)           X ◀ (h)/100   Y ◀ (h)/100   Z ◀ 1
(g) Make Transform                  Location ◀ (i)   Scale ◀ (j)
```

- Le `/100` n'est **pas** une formule de projection : c'est la taille du Cube de base. **[À VÉRIFIER]** les dimensions du Cube en l'ouvrant.
- Condition invalide `(a)` : `Break World` → `CellCountX * CellCountY` ≠ `Length(Cells)`, ou une dimension ≤ 0. Mettez-la en place avec un `OR` de deux tests (`!=` et `<= 0`).
- Cellule d'un terrain futur : le `default` du Switch envoie vers `HISM_Unknown`, rien n'est jamais ignoré silencieusement.

**Test :** compile. (Le rendu sera testé au §12.)

### 6.2 `BP_PRISM_Obstacle`

1. **Actor** → `BP_PRISM_Obstacle`. Composant `Static Mesh` racine = `/Engine/BasicShapes/Cylinder` (100 uu de diamètre et de haut, pivot au centre), collision `BlockAll`.
2. Variable `ObstacleId` (String).
3. Fonction `InitObstacle(Id : String, RadiusUu : Double)` :

```
[1] Function Entry  InitObstacle     Id : String   RadiusUu : Double
    ▶ [2]
[2] Set ObstacleId       ObstacleId ◀ Id
    ▶ [3]
[3] Set Actor Scale 3D   New Scale 3D ◀ (a)
    ▶ [4]
[4] Add Actor World Offset   Delta Location ◀ (b)
    ▶ (fin)

(a) Make Vector   X ◀ (RadiusUu * 2) / 100   Y ◀ (RadiusUu * 2) / 100   Z ◀ 20
(b) Make Vector   X ◀ 0   Y ◀ 0   Z ◀ 1000       (moitié de la hauteur 20 × 100 uu)
```

La hauteur 20 est purement décorative.

### 6.3 `BP_PRISM_ResourceMarker` (optionnel)

Voir l'annexe A. Le cœur du guide n'en dépend pas.

---

## 7. Étape 4 — L'agent

### 7.1 Création

**Add → Blueprint Class → Character** → `BP_PRISM_Agent`, dossier `World/`.

Class Defaults :

- **AI Controller Class = `AIController`** (natif).
- **Auto Possess AI = `Placed in World or Spawned`** (sans ce réglage, `AI MoveTo` ne fait rien et sans erreur).

Ajoutez un composant `Static Mesh` attaché à la capsule (`/Engine/BasicShapes/Sphere`, collision `NoCollision`). Agrandissez-le (échelle 4 à 6) : à 60 000 uu d'altitude, un agent de 100 uu est invisible. C'est du visuel provisoire.

### 7.2 Variables

| Variable | Type |
|---|---|
| `AgentId` | Integer64 |
| `Species` | String |
| `TargetCell` | Int Point |
| `bHasTargetCell` | Boolean |

### 7.3 `InitAgent(Id : Integer64, InSpecies : String)`

```
[1] Function Entry  InitAgent    Id : Integer64    InSpecies : String
    ▶ [2]
[2] Set AgentId    AgentId ◀ Id
    ▶ [3]
[3] Set Species    Species ◀ InSpecies
```

### 7.4 `ApplyView(View : FPrismSyneAgentSnapshot, Cell : Int Point, TileCentre : Vector)`

Le Director calcule la cellule et le centre de tuile (avec la hauteur du sol) : l'agent n'a besoin d'aucune référence au Director.

```
[1] Function Entry  ApplyView     View   Cell   TileCentre
    ▶ [2]
[2] Branch      Condition ◀ (a)       (a = bHasTargetCell AND Cell == TargetCell)
    True  ▶ (fin)                     (même tuile : on ne relance pas le déplacement)
    False ▶ [3]
[3] Set TargetCell      TargetCell ◀ Cell
    ▶ [4]
[4] Set bHasTargetCell  bHasTargetCell ◀ true
    ▶ [5]
[5] Set Max Walk Speed  Target ◀ (b) Character Movement    Max Walk Speed ◀ (e)
    ▶ [6]
[6] Get Random Reachable Point In Radius
    Origin ◀ TileCentre    Radius ◀ (g)     (Navigation System: Context = Self)
    ReturnValue (bool) ▶ [7]  → Branch
[7] Branch      Condition ◀ ReturnValue de [6]
    True  ▶ [9]
    False ▶ [8]
[8] Project Point To Navigation
    Point ◀ TileCentre      Query Extent ◀ (200, 200, 3000)
    ReturnValue (bool) ▶ [8b] Branch : True → [9]   False → Print String "Pas de NavMesh" (fin)
[9] AI MoveTo
    Pawn ◀ Self    Destination ◀ RandomLocation (de [6] ou [8])    Acceptance Radius ◀ 50

(b) Get Prism Ldk World Registry → Get Stats → Break FPrismLdkWorldStats
(c) Find (Map Traits de View)   Key ◀ "speed"     → Found, Value
(d) Select Float   A ◀ Value(c)   B ◀ 1.0   Pick A ◀ Found(c)
(e) Max Walk Speed For Speed   Speed ◀ (d)   K ◀ Stats.K   Ticks Per Second ◀ Stats.TicksPerSecond
(f) Stats.TileSizeUu
(g) (f) / 2          (le cercle tient dans la tuile ; un rayon plus grand pourrait viser la tuile voisine)
```

Règles : **jamais** de `Set Actor Location` dans cette fonction (l'agent marche jusqu'à la tuile ; sa position peut différer légèrement de la position SYNE exacte, qui reste la vérité). Les points de navigation sont tirés autour du centre de tuile à la hauteur du sol.

**Test :** compile. Le déplacement sera testé au §12.

---

## 8. Étape 5 — `BP_PRISM_VirtualWorld`

1. **Add → Blueprint Class → All Classes → `Prism Ldk Virtual World Actor`** → `BP_PRISM_VirtualWorld`, dossier `World/`. Si le parent n'apparaît pas, le plugin n'est pas à jour/compilé : revenez à l'étape 0.
2. **Aucun graphe, aucun composant ajouté.** Le C++ possède terrain, eau, végétation et blockers.
3. Ouvrez `LDK_VirtualMap` et placez **une seule instance** de `BP_PRISM_VirtualWorld` à `(0, 0, 0)` (la grille SYNE occupe le rectangle `[0, W] × [0, H]`). Sauvegardez.

Les réglages (`FPrismLdkVirtualWorldSettings`) se passent au build depuis le Director (§9.9). Gardez leurs valeurs par défaut au début. Les matériaux (`TerrainMaterialOverride`, `WaterMaterialOverride`) et les meshes de végétation sont optionnels. En package, utilisez des `Material` simples (pas de Material Instance d'un master non cooké).

En mode virtuel, **ne spawnez pas d'obstacles Blueprint** : les obstacles SYNE sont déjà représentés par le relief C++.

**Test :** le Blueprint compile. L'éditeur affiche les propriétés du parent.

---

## 9. Étape 6 — `BP_PRISM_WorldDirector` (le cœur)

**Add → Blueprint Class → Actor** → `BP_PRISM_WorldDirector`, dossier `World/`. Il sera posé **une seule fois** dans `LDK_MenuMap` (§11.5). Les latents (`Load Stream Level`, `Unload Stream Level`) ne vivent que dans l'**Event Graph** (Custom Events) ; les fonctions n'en contiennent aucun.

### 9.1 Variables

| Variable | Type | Défaut / rôle |
|---|---|---|
| `GI` | `BP_PRISM_GameInstance` Object Ref | posée au BeginPlay |
| `LDK` | `Prism Ldk Subsystem` Object Ref | posée au BeginPlay |
| `Registry` | `Prism Ldk World Registry Base` Object Ref | posée au BeginPlay |
| `Phase` | `EPrismPhase` | `Disconnected` |
| `CurrentMode` | `EPrismRenderMode` | `Logical` |
| `bCommandPending` | Boolean | false — une seule commande HTTP à la fois |
| `PendingAction` | Name | aucune |
| `bPrepareAcked` | Boolean | false — `ControlResult(prepare)` positif reçu |
| `bWorldReceived` | Boolean | false — `OnWorldInitialized` reçu |
| `WorldDesc` | `FPrismSyneWorldDescription` | description du run |
| `LogicalPackage` | Name | `/PrismLdk/Level/LDK_LogicalMap` |
| `VirtualPackage` | Name | `/PrismLdk/Level/LDK_VirtualMap` |
| `ActivePackage` | Name | package du sous-niveau chargé |
| `VirtualSettings` | `Prism Ldk Virtual World Settings` (struct) | valeurs par défaut du C++ |
| `PCGData` | `Prism Ldk PCG Data` Object Ref | gardée jusqu'à la fin du build |
| `VirtualWorld` | `BP_PRISM_VirtualWorld` Object Ref | trouvée après le streaming virtuel |
| `TileField` | `BP_PRISM_TileField` Object Ref | créée pour le mode logique |
| `BuildId` | Integer | 0 — filtre les callbacks du build virtuel |
| `CameraStartHeight` | Float | 60000 |

Dans `VirtualSettings` (Class Defaults), vérifiez : collision activée (`bCreateCollision`) et reconstruction de navigation activée (`bRebuildNavigation`).

### 9.2 Event Dispatchers

Dans **My Blueprint → Event Dispatchers** :

- `ED_PhaseChanged(Phase : EPrismPhase, Message : String)`
- `ED_Progress(Progress : Float, Text : String)` — `Progress < 0` signifie « indéterminé »
- `ED_Snapshot(Snapshot : FPrismSyneWorldSnapshot)`
- `ED_Error(Message : String)`

Ce sont les **seuls** points d'accès des widgets. Les événements du plugin ne sont jamais relayés tels quels.

### 9.3 `Event BeginPlay` — récupérer le plugin et s'abonner

```
[1] Event BeginPlay
    ▶ [2]
[2] Cast To BP_PRISM_GameInstance     Object ◀ (a)
    Cast Failed ▶ [2b] Print String "GameInstance incorrect (Project Settings)" (fin)
    ▶ [3]
[3] Set GI         GI ◀ As BP_PRISM_GameInstance
    ▶ [4]
[4] Set LDK        LDK ◀ (b)
    ▶ [5]
[5] Set Registry   Registry ◀ (c)
    ▶ [6]
[6] Assign OnConnected         Target ◀ Get LDK   → créer le Custom Event EVT_OnConnected
    ▶ [7]
[7] Assign OnDisconnected      Target ◀ Get LDK   → EVT_OnDisconnected
    ▶ [8]
[8] Assign OnError             Target ◀ Get LDK   → EVT_OnError        (Error : FPrismSyneError)
    ▶ [9]
[9] Assign OnControlResult     Target ◀ Get LDK   → EVT_OnControlResult (Result : FPrismSyneControlResult)
    ▶ [10]
[10] Assign OnWorldInitialized Target ◀ Get LDK   → EVT_OnWorldInitialized (World : FPrismSyneWorldDescription)
    ▶ [11]
[11] Assign OnSnapshot         Target ◀ Get LDK   → EVT_OnSnapshot     (Snapshot : FPrismSyneWorldSnapshot)

(a) Get Game Instance
(b) Get Prism Ldk Subsystem          (World Context = Self)
(c) Get Prism Ldk World Registry     (World Context = Self)
```

Chaque `Assign` crée à côté de lui un Custom Event avec la bonne signature (tirez depuis le pin rouge `Event` → **Add Custom Event** si Unreal ne le crée pas seul), puis renommez-le comme indiqué. Les six événements sont des **points d'entrée indépendants** : aucun fil blanc ne les relie à la chaîne [6]→[11]. Le BeginPlay ne lance **aucune** action réseau.

`OnWorldDelta` et `OnSyneEvent` ne sont pas utilisés dans ce guide (§14).

### 9.4 Fonction `SetPhase(NewPhase : EPrismPhase, Message : String)`

```
[1] Function Entry  SetPhase    NewPhase   Message
    ▶ [2]
[2] Set Phase                 Phase ◀ NewPhase
    ▶ [3]
[3] Call ED_PhaseChanged      Phase ◀ NewPhase    Message ◀ Message
```

Toute modification de `Phase` passe par cette fonction.

### 9.5 Fonction `SendCommand(Action : Name) → Sent : Boolean`

C'est l'unique endroit qui envoie une commande HTTP. Elle refuse s'il y en a déjà une en cours ou si la connexion n'est pas établie.

```
[1] Function Entry  SendCommand     Action : Name
    ▶ [2]
[2] Branch        Condition ◀ (a)
    True  ▶ [3]
    False ▶ [4]
[3] Return Node   Sent ◀ false
[4] Set bCommandPending   ◀ true
    ▶ [5]
[5] Set PendingAction     ◀ Action
    ▶ [6]
[6] Switch on Name        Selection ◀ Action      (pins : Prepare, Ready, Start, Pause, Resume, Stop)
    Prepare ▶ [7]  LDK.Prepare       Seed ◀ GI.Seed   Ticks Per Second ◀ GI.TicksPerSecond   Config Json ◀ GI.Config.WorldConfigJson   ▶ [13]
    Ready   ▶ [8]  LDK.Ready         World Version ◀ WorldDesc.Version                         ▶ [13]
    Start   ▶ [9]  LDK.Start         Seed ◀ GI.Seed   Max Ticks ◀ GI.MaxTicks                  ▶ [13]
    Pause   ▶ [10] LDK.Pause                                                                   ▶ [13]
    Resume  ▶ [11] LDK.Resume                                                                  ▶ [13]
    Stop    ▶ [12] SetPhase(Stopping, "Arrêt de la simulation") ▶ LDK.Stop                     ▶ [13]
[13] Return Node  Sent ◀ true

(a) OR   A ◀ Get bCommandPending   B ◀ NOT( (b) )
(b) Equal (EPrismSyneConnectionState)   A ◀ (LDK → Get Connection State)   B ◀ Connected
```

**Pins avancés :** `Prepare` déclare `TicksPerSecond` et `ConfigJson` en affichage avancé. Sur le node `Prepare`, cliquez sur la **flèche en bas du node** pour déplier ces deux pins, sinon `ConfigJson` reste vide et SYNE prépare un autre monde que celui attendu. `Ready` expose `World Version`, `Start` expose `Seed` et `Max Ticks`.

Règle : le Director n'envoie qu'**une** commande à la fois. Les boutons des widgets sont grisés selon la phase (§10), ce qui évite l'essentiel des doubles clics.

### 9.6 Connexion

**Event `EVT_ConnectToSyne(Options : FPrismSyneConnectionOptions)`** (Custom Event, appelé par le menu) :

```
[1] Custom Event  EVT_ConnectToSyne    Options
    ▶ [2]
[2] Branch        Condition ◀ (a)               (a = Phase == Disconnected)
    False ▶ (fin)
    True  ▶ [3]
[3] LDK.Configure   In Options ◀ Options
    ▶ [4]
[4] LDK.Connect     (le Boolean retourné n'est pas le résultat réseau : on l'ignore)
    ▶ [5]
[5] SetPhase(Disconnected, "Connexion en cours…")
```

`Configure` ne prouve rien : seul `OnConnected` fait avancer la phase.

**Event `EVT_OnConnected`** :

```
[1] EVT_OnConnected
    ▶ [2] Branch   Condition ◀ (Phase == Disconnected)
    True  ▶ [3] SetPhase(Idle, "Connecté à SYNE")
    False ▶ (fin)
```

**Event `EVT_OnDisconnected`** :

```
[1] EVT_OnDisconnected
    ▶ [2] Switch on Enum EPrismPhase    Selection ◀ Get Phase
    Disconnected, Streaming, Building, Unloading ▶ (fin)     (les étapes en cours vérifieront la connexion à leur fin)
    Idle                                         ▶ [3] SetPhase(Disconnected, "Connexion perdue")
    Preparing, Readying, Ready, Running, Paused, Stopping ▶ [4] EVT_Fail("Connexion perdue")
```

L'auto-reconnexion du plugin (`bAutoReconnect`) renverra ensuite `OnConnected` : on repasse alors de `Disconnected` à `Idle`.

**Event `EVT_OnError(Error : FPrismSyneError)`** :

```
[1] EVT_OnError      Error
    ▶ [2] Call ED_Error     Message ◀ (b)
    ▶ [3] Branch            Condition ◀ (c)
    True  ▶ [4] Set bCommandPending ◀ false   ▶ [5] EVT_Fail("Commande sans réponse")
    False ▶ (fin)

(a) Break FPrismSyneError     (HttpCode, Code, Message, RawPayload)
(b) Select String   A ◀ Message   B ◀ Code   Pick A ◀ NOT(Message == "")
(c) AND   A ◀ Get bCommandPending   B ◀ (HttpCode == 0)
```

Une erreur HTTP structurée (code > 0) est suivie d'un `OnControlResult` qui, lui, fait avancer la machine. Un échec réseau (code 0) n'est suivi de rien : c'est ce cas que `(c)` traite. **[À VÉRIFIER]** dans le log la valeur de `HttpCode` pour un échec réseau. N'affichez jamais `RawPayload` ni un jeton.

### 9.7 Charger un monde : `EVT_LoadWorld(NewMode : EPrismRenderMode)`

Appelé par les boutons Logique/Virtuel du menu, après qu'ils ont mémorisé les réglages dans le GameInstance.

```
[1] Custom Event  EVT_LoadWorld     NewMode : EPrismRenderMode
    ▶ [2]
[2] Branch        Condition ◀ (a)                (a = Phase == Idle)
    False ▶ (fin)
    True  ▶ [3]
[3] Set CurrentMode       ◀ NewMode
    ▶ [4]
[4] Set ActivePackage     ◀ (b)                  (b = Select Name : Logical → LogicalPackage, Virtual → VirtualPackage)
    ▶ [5]
[5] Set bPrepareAcked ◀ false   ▶ [6] Set bWorldReceived ◀ false
    ▶ [7]
[7] SetPhase(Streaming, "Chargement du niveau")
    ▶ [8]
[8] Call ED_Progress      Progress ◀ -1    Text ◀ "Chargement du niveau"
    ▶ [9]
[9] Load Stream Level     Level Name ◀ Get ActivePackage   Make Visible After Load ◀ true   Should Block on Load ◀ false
    Completed ▶ [10]
[10] Get Streaming Level  Package Name ◀ Get ActivePackage     → Branch
[11] Branch               Condition ◀ (c)
    False ▶ [12] EVT_Fail("Niveau non chargé ou non visible")
    True  ▶ [13]
[13] Branch               Condition ◀ (d)                (d = LDK.GetConnectionState == Connected)
    False ▶ [14] EVT_Fail("Connexion perdue")
    True  ▶ [15]
[15] SetPhase(Preparing, "Préparation du monde (SYNE)")
    ▶ [16]
[16] SendCommand("Prepare")      → Sent
    ▶ [17] Branch   Condition ◀ NOT(Sent)   True ▶ EVT_Fail("Prepare non envoyé")   False ▶ (fin)

(c) AND   A ◀ IsValid(StreamingLevel de [10])   B ◀ AND( Is Level Loaded(StreamingLevel), Is Level Visible(StreamingLevel) )
```

`Completed` signifie que **l'action de streaming** est terminée, pas que le build l'est. Aucun `Delay` : on attend toujours un vrai signal.

### 9.8 `OnWorldInitialized`, `ControlResult` et `TryBuild`

La description du monde (`OnWorldInitialized`, via WebSocket) et l'acquittement du Prepare (`OnControlResult`, via HTTP) peuvent arriver dans **n'importe quel ordre**. On lance le build quand les deux sont là.

**Event `EVT_OnWorldInitialized(World)`** :

```
[1] EVT_OnWorldInitialized    World : FPrismSyneWorldDescription
    ▶ [2] Branch   Condition ◀ AND( Phase == Preparing , NOT(bWorldReceived) )
    False ▶ (fin)                     (message hors Prepare ou en double)
    True  ▶ [3] Set WorldDesc ◀ World   ▶ [4] Set bWorldReceived ◀ true   ▶ [5] EVT_TryBuild
```

**Event `EVT_TryBuild`** :

```
[1] EVT_TryBuild
    ▶ [2] Branch   Condition ◀ AND( Phase == Preparing , AND( bPrepareAcked , bWorldReceived ) )
    False ▶ (fin)
    True  ▶ [3] SetPhase(Building, "Construction du monde")
    ▶ [4] Registry.SetTileSizeUu      Tile Size Uu ◀ GI.Config.TileSizeUu       → Boolean
    ▶ [5] Branch   Condition ◀ ReturnValue de [4]
          False ▶ EVT_Fail("TileSizeUu invalide")
          True  ▶ [6]
    ▶ [6] Registry.SetStatsFromDescription    World ◀ WorldDesc
    ▶ [7] Branch   Condition ◀ (a)               (a = Registry.GetStats → Break FPrismLdkWorldStats → bScaleValid)
          False ▶ EVT_Fail("Échelle invalide (TileSizeUu / CellSize)")
          True  ▶ [8]
    ▶ [8] Branch   Condition ◀ (CurrentMode == Logical)
          True  ▶ EVT_BuildLogical
          False ▶ EVT_BuildVirtual
```

Le `Branch` de [2] ferme la porte dès qu'on passe en `Building` : un deuxième appel ne relancera rien.

**Event `EVT_OnControlResult(Result)`** — l'unique handler des résultats de commande :

```
[1] EVT_OnControlResult   Result : FPrismSyneControlResult     (Break → bOk, Action, Error, Status)
    ▶ [2] Branch   Condition ◀ Get bCommandPending
          False ▶ (fin)                  (résultat non attendu : ignoré)
          True  ▶ [3]
    ▶ [3] Set bCommandPending ◀ false
    ▶ [4] Switch on Name   Selection ◀ Get PendingAction

    Prepare ▶ [5] Branch (Result.bOk)
                 True  ▶ [6] Set bPrepareAcked ◀ true   ▶ [7] EVT_TryBuild
                 False ▶ [8] EVT_Fail(Result.Error)
    Ready   ▶ [9] Branch (Result.bOk)
                 True  ▶ [10] PlaceCamera   ▶ [11] SetPhase(Ready, "Monde prêt — cliquez sur Start")
                 False ▶ [12] EVT_Fail(Result.Error)
    Start   ▶ [13] Branch (Result.bOk)
                 True  ▶ [14] SetPhase(Running, "Simulation en cours")
                 False ▶ [15] Call ED_Error(Result.Error)
    Pause   ▶ [16] Branch (Result.bOk)   True ▶ SetPhase(Paused, "En pause")        False ▶ ED_Error(Result.Error)
    Resume  ▶ [17] Branch (Result.bOk)   True ▶ SetPhase(Running, "Simulation en cours")   False ▶ ED_Error(Result.Error)
    Stop    ▶ [18] Branch (Result.bOk)   False ▶ ED_Error(Result.Error)      (les deux sorties continuent)
             ▶ [19] EVT_Cleanup
```

**Pourquoi ne pas vérifier `Result.Action`** : une seule commande est en vol à la fois, donc tout résultat reçu pendant `bCommandPending` appartient à cette commande. Cela supprime la table d'alias (`prepared`/`started`/`start`…) de la version unifiée. Pour `Stop`, on nettoie **dans tous les cas** : même si le serveur refuse l'arrêt, le monde local est rendu.

### 9.9 Construire le monde

**Event `EVT_BuildLogical`** :

```
[1] EVT_BuildLogical
    ▶ [2] Call ED_Progress   Progress ◀ -1   Text ◀ "Construction du terrain logique"
    ▶ [3] Spawn Actor from Class   Class ◀ BP_PRISM_TileField   Spawn Transform ◀ identité   Collision ◀ Always Spawn
    ▶ [4] Set TileField ◀ ReturnValue
    ▶ [5] TileField.Build    World ◀ WorldDesc
    ▶ [6] Branch   Condition ◀ ReturnValue de [5]
          False ▶ [7] EVT_BuildFinished(false, "TileField.Build a échoué")
          True  ▶ [8] SpawnObstacles
          ▶ [9] EVT_BuildFinished(true, "")
```

**Event `EVT_BuildVirtual`** :

```
[1] EVT_BuildVirtual
    ▶ [2] Call ED_Progress   Progress ◀ 0   Text ◀ "Génération du terrain"
    ▶ [3] Get Actor Of Class   Actor Class ◀ BP_PRISM_VirtualWorld        (une seule fois par chargement)
    ▶ [4] Set VirtualWorld ◀ ReturnValue
    ▶ [5] Branch   Condition ◀ Is Valid(VirtualWorld)
          False ▶ EVT_BuildFinished(false, "BP_PRISM_VirtualWorld absent de LDK_VirtualMap")
          True  ▶ [6]
    ▶ [6] Bind Event to OnBuildProgress    Target ◀ VirtualWorld   → Custom Event EVT_VirtualProgress  (BuildId, Phase : Name, Progress : Float)
    ▶ [7] Bind Event to OnBuildCompleted   Target ◀ VirtualWorld   → Custom Event EVT_VirtualCompleted (BuildId, bSuccess, Error)
    ▶ [8] Set BuildId ◀ BuildId + 1
    ▶ [9] Set PCGData ◀ (a)
    ▶ [10] Branch   Condition ◀ Is Valid(PCGData)
          False ▶ EVT_BuildFinished(false, "PCGData invalide")
          True  ▶ [11]
    ▶ [11] VirtualWorld.Start Build World Async    Data ◀ PCGData   Settings ◀ VirtualSettings   Build Id ◀ BuildId
    ▶ [12] Branch   Condition ◀ ReturnValue
          False ▶ EVT_BuildFinished(false, "Build asynchrone refusé")      (aucun callback n'est promis dans ce cas)
          True  ▶ (fin : la suite arrive par EVT_VirtualProgress / EVT_VirtualCompleted)

(a) Create PCG Data From World Description    World Description ◀ WorldDesc   (World Context = Self)
    Ce node lit l'échelle dans le registre : `SetTileSizeUu` et `SetStatsFromDescription` doivent donc être faits avant (c'est le cas dans `EVT_TryBuild`).
```

**Événements du build virtuel** (créés depuis les pins rouges des `Bind Event`) :

```
[1] EVT_VirtualProgress   BuildId   Phase : Name   Progress : Float
    ▶ [2] Branch   Condition ◀ (BuildId == Get BuildId)
          True  ▶ [3] Call ED_Progress   Progress ◀ Progress   Text ◀ (To String (Name) Phase)
          False ▶ (fin)

[1] EVT_VirtualCompleted  BuildId   bSuccess : Boolean   Error : String
    ▶ [2] Branch   Condition ◀ (BuildId == Get BuildId)
          True  ▶ [3] EVT_BuildFinished(bSuccess, Error)
          False ▶ (fin)
```

Le C++ valide aussi le NavMesh avant de renvoyer `bSuccess = true` (contrôle ciblé, pas une preuve que tous les agents trouveront un chemin).

**Event `EVT_BuildFinished(bSuccess : Boolean, Error : String)`** — point de convergence des deux modes :

```
[1] EVT_BuildFinished    bSuccess   Error
    ▶ [2] Branch   Condition ◀ (Phase == Building)
          False ▶ (fin)                  (callback périmé ou en double)
          True  ▶ [3]
    ▶ [3] Branch   Condition ◀ bSuccess
          False ▶ [4] EVT_Fail(Error)
          True  ▶ [5]
    ▶ [5] Branch   Condition ◀ (LDK.GetConnectionState == Connected)
          False ▶ [6] EVT_Fail("Connexion perdue")
          True  ▶ [7]
    ▶ [7] SpawnInitialAgents
    ▶ [8] SetPhase(Readying, "Confirmation auprès de SYNE")
    ▶ [9] SendCommand("Ready")      → Sent
    ▶ [10] Branch   Condition ◀ NOT(Sent)   True ▶ EVT_Fail("Ready non envoyé")   False ▶ (fin)
```

`Ready` n'est **jamais** envoyé avant un build réussi. On l'envoie une seule fois grâce au `Branch` de [2]. `Start` ne part qu'au clic sur Start, après le `ControlResult` positif de `Ready`. Ne conditionnez pas Start à `GetStatus().State == Ready` : le statut compact peut ne pas être à jour juste après l'accusé.

### 9.10 Obstacles et agents

**Fonction `SpawnObstacles()`** (mode logique uniquement) :

```
[1] Function Entry
    ▶ [2] ForEachLoop    Array ◀ (Break WorldDesc → Obstacles)
          Loop Body ▶ [3]
[3] Spawn Actor from Class    Class ◀ BP_PRISM_Obstacle    Spawn Transform ◀ (c)    Collision ◀ Always Spawn
    ▶ [4] InitObstacle   Target ◀ ReturnValue   Id ◀ ArrayElement.Id   Radius Uu ◀ (b)
    ▶ [5] Registry.RegisterObstacle   Id ◀ ArrayElement.Id   Actor ◀ ReturnValue de [3]
          ▶ (retour boucle)

(a) Registry.GetStats → Break → K
(b) Syne Radius To Unreal    Radius ◀ ArrayElement.Radius   K ◀ Stats.K
(c) Make Transform           Location ◀ Syne To Unreal(Position ◀ ArrayElement.Position, K ◀ Stats.K)
```

**Fonction `GetGroundZ(X : Double, Y : Double) → Double`** :

```
[1] Function Entry  GetGroundZ    X   Y
    ▶ [2] Branch   Condition ◀ AND( CurrentMode == Virtual , Is Valid(VirtualWorld) )
          True  ▶ [3] Return Node   ReturnValue ◀ (VirtualWorld.Get Ground Height At   X ◀ X   Y ◀ Y)
          False ▶ [4] Return Node   ReturnValue ◀ 0
```

Le node `Get Ground Height At` est l'alias (DisplayName) de `Sample Height At` du C++. X/Y sont en unités Unreal ; le retour est un Float.

**Fonction `SpawnAgent(Id : Integer64, InSpecies : String, Position : FPrismSyneVector2D) → Actor`** :

```
[1] Function Entry  SpawnAgent    Id   InSpecies   Position
    ▶ [2] Spawn Actor from Class    Class ◀ BP_PRISM_Agent    Spawn Transform ◀ (c)    Collision ◀ Try To Adjust Location But Always Spawn
    ▶ [3] InitAgent    Target ◀ ReturnValue   Id ◀ Id   In Species ◀ InSpecies
    ▶ [4] Registry.RegisterAgent    Id ◀ Id   Actor ◀ ReturnValue de [2]      → Boolean
    ▶ [5] Branch   Condition ◀ ReturnValue de [4]
          True  ▶ [6] Return Node   ◀ acteur
          False ▶ [7] Destroy Actor (acteur)  ▶ [8] Return Node   ◀ None

(a) Syne To Unreal    Position ◀ Position   K ◀ Stats.K
(b) GetGroundZ        X ◀ (a).X   Y ◀ (a).Y
(c) Make Transform    Location ◀ Make Vector( (a).X , (a).Y , (b) + 150 )
```

La hauteur +150 évite de naître dans le sol ; la gravité du Character termine le placement.

**Fonction `SpawnInitialAgents()`** : `ForEachLoop( WorldDesc.Agents )` → `SpawnAgent( Id, Species, Position )`. Si un `SpawnAgent` renvoie `None`, affichez un `Print String` ; le premier snapshot recréera l'agent manquant.

**Fonction `UpdateAgents(Snapshot : FPrismSyneWorldSnapshot)`** — variable locale `SeenIds : Array<Integer64>` :

```
[1] Function Entry  UpdateAgents    Snapshot
    ▶ [2] ForEachLoop    Array ◀ (Break Snapshot → Agents)
          Loop Body ▶ [3]
          Completed ▶ [12]
[3] Branch   Condition ◀ Registry.HasAgent(Id ◀ ArrayElement.Id)
    False ▶ [4] SpawnAgent( ArrayElement.Id , ArrayElement.Species , ArrayElement.Position )   ▶ [5]
    True  ▶ [5]
[5] Add (SeenIds)   Item ◀ ArrayElement.Id
    ▶ [6] Registry.GetAgentActor( ArrayElement.Id )  →  Cast To BP_PRISM_Agent
          Cast Failed ▶ (retour boucle)
          ▶ [7] ApplyView   Target ◀ As BP_PRISM_Agent   View ◀ ArrayElement   Cell ◀ (a)   Tile Centre ◀ (d)
              ▶ (retour boucle)

[12] ForEachLoop    Array ◀ Registry.GetAgentIds
     Loop Body ▶ [13] Branch   Condition ◀ Contains(SeenIds, ArrayElement)
          True  ▶ (retour boucle)
          False ▶ [14] Registry.GetAgentActor → Destroy Actor   ▶ [15] Registry.UnregisterAgent( ArrayElement )   ▶ (retour boucle)

(a) Syne To Cell        Position ◀ ArrayElement.Position   Cell Size ◀ Stats.CellSize   Cell Count X ◀ Stats.CellCountX   Cell Count Y ◀ Stats.CellCountY
(b) Cell To Tile Center Unreal   Cell ◀ (a)   (World Context = Self)
(c) GetGroundZ          X ◀ (b).X   Y ◀ (b).Y
(d) Make Vector         X ◀ (b).X   Y ◀ (b).Y   Z ◀ (c)
```

`Registry.GetAgentIds` renvoie une copie triée : supprimer pendant la boucle est sûr.

**Event `EVT_OnSnapshot(Snapshot)`** :

```
[1] EVT_OnSnapshot     Snapshot : FPrismSyneWorldSnapshot
    ▶ [2] Branch   Condition ◀ OR( Phase == Running , Phase == Paused )
          False ▶ (fin)                  (le premier snapshot peut arriver avant le résultat de Start : le suivant sera pris)
          True  ▶ [3] Call ED_Snapshot   Snapshot ◀ Snapshot
    ▶ [4] UpdateAgents   Snapshot ◀ Snapshot
```

Le terrain n'est **jamais** reconstruit sur un snapshot. Les `Resources` d'un snapshot sont des stocks globaux sans position : on ne crée aucun acteur depuis elles.

### 9.11 Échec, arrêt, annulation et nettoyage

**Event `EVT_Fail(Message : String)`** — point unique de sortie en erreur :

```
[1] EVT_Fail     Message
    ▶ [2] Call ED_Error   Message ◀ Message
    ▶ [3] Branch   Condition ◀ AND( bPrepareAcked , LDK.GetConnectionState == Connected )
          True  ▶ [4] Set bPrepareAcked ◀ false     ▶ [5] EVT_StopSession
          False ▶ [6] EVT_Cleanup
```

Si le monde avait été préparé côté SYNE, on envoie un `Stop` (le serveur repasse à `Idle`), puis le résultat de `Stop` déclenche le nettoyage (§9.8). Sinon, on nettoie directement. `bPrepareAcked ← false` évite toute boucle si le `Stop` échoue aussi.

**Event `EVT_StopSession`** (appelé par le bouton Stop du HUD et par `EVT_Fail`) :

```
[1] EVT_StopSession
    ▶ [2] Branch   Condition ◀ Get bCommandPending
          True  ▶ (fin)                  (une commande est en vol : le clic est ignoré)
          False ▶ [3] SendCommand("Stop")   → Sent
    ▶ [4] Branch   Condition ◀ NOT(Sent)   True ▶ [5] EVT_Cleanup   False ▶ (fin)
```

**Event `EVT_AbortLoad`** (bouton Annuler du chargement, actif seulement en `Preparing` et `Readying`) :

```
[1] EVT_AbortLoad
    ▶ [2] Branch   Condition ◀ OR( Phase == Preparing , Phase == Readying )
          False ▶ (fin)
          True  ▶ [3] Set bCommandPending ◀ false      (on abandonne la commande en vol ; son résultat tardif sera ignoré)
          ▶ [4] EVT_Fail("Chargement annulé")
```

On n'annule pas pendant `Streaming` (un `Load Stream Level` latent n'est pas interruptible) ni pendant `Building` (le build asynchrone se termine seul).

**Fonction `DestroyWorldActors()`** :

```
[1] Function Entry
    ▶ [2] ForEachLoop    Array ◀ Registry.GetAgentIds
          Body ▶ Registry.GetAgentActor( ArrayElement ) → Branch(Is Valid) True ▶ Destroy Actor
          Completed ▶ [3]
    ▶ [3] ForEachLoop    Array ◀ Registry.GetObstacleIds
          Body ▶ Registry.GetObstacleActor( ArrayElement ) → Branch(Is Valid) True ▶ Destroy Actor
          Completed ▶ [4]
    ▶ [4] ForEachLoop    Array ◀ Registry.GetResourceMarkers
          Body ▶ Branch(Is Valid(ArrayElement)) True ▶ Destroy Actor
          Completed ▶ [5]
    ▶ [5] Branch   Condition ◀ Is Valid(TileField)   True ▶ Destroy Actor(TileField)
```

Le registre connaît déjà tous les acteurs créés : pas besoin d'un tableau en plus.

**Event `EVT_Cleanup`** — détruit le monde, décharge le sous-niveau, revient au menu :

```
[1] EVT_Cleanup
    ▶ [2] Branch   Condition ◀ (Phase == Unloading)
          True  ▶ (fin)                  (déjà en cours)
          False ▶ [3]
    ▶ [3] SetPhase(Unloading, "Nettoyage")
    ▶ [4] DestroyWorldActors
    ▶ [5] Registry.ClearAll                        (conserve TileSizeUu)
    ▶ [6] Set bCommandPending ◀ false   ▶ [7] Set bPrepareAcked ◀ false   ▶ [8] Set bWorldReceived ◀ false
    ▶ [9] Branch   Condition ◀ (b)                (b = Get Streaming Level(ActivePackage) valide ET Is Level Loaded)
          False ▶ [12]
          True  ▶ [10]
    ▶ [10] Unload Stream Level    Level Name ◀ Get ActivePackage   Should Block on Unload ◀ false
           Completed ▶ [12]
    ▶ [12] Set VirtualWorld ◀ None   ▶ [13] Set TileField ◀ None   ▶ [14] Set PCGData ◀ None
    ▶ [15] Branch   Condition ◀ (LDK.GetConnectionState == Connected)
           True  ▶ SetPhase(Idle, "Prêt")
           False ▶ SetPhase(Disconnected, "Déconnecté")
```

Dans le cas où un `Stop` a échoué ou n'a pas reçu de réponse, le serveur peut rester préparé : la prochaine `Prepare` renverra alors une erreur lisible dans `ED_Error`. C'est une limite assumée (§14).

### 9.12 Placer la caméra au `Ready`

**Fonction `PlaceCamera()`** (appelée par `EVT_OnControlResult` quand `Ready` réussit) :

```
[1] Function Entry
    ▶ [2] Get Player Pawn (Index 0)   → Branch Is Valid   False ▶ (fin)
    ▶ [3] Set Actor Location    Target ◀ Pawn   New Location ◀ (c)
    ▶ [4] Get Player Controller (0) → Set Control Rotation    New Rotation ◀ Make Rotator( Roll 0 , Pitch -55 , Yaw 0 )

(a) Make Int Point     X ◀ Stats.CellCountX / 2     Y ◀ Stats.CellCountY / 2
(b) Cell To Tile Center Unreal   Cell ◀ (a)   (World Context = Self)
(c) Make Vector        X ◀ (b).X   Y ◀ (b).Y   Z ◀ Get CameraStartHeight
```

Le centre vient du node de projection du plugin, pas d'une multiplication faite à la main.

**Test de l'étape 6 :** compilez. Les graphes seront exercés au §12.

---

## 10. Étape 7 — Les trois widgets

Principe commun : chaque widget a une variable **`Director`** (type `BP_PRISM_WorldDirector`, cochez **Instance Editable** et **Expose on Spawn**, ce qui fait apparaître un pin sur `Create Widget`). Il s'abonne lui-même aux dispatchers du Director dans `Event Construct` et se montre ou se cache selon la phase. Aucun widget ne touche au plugin.

### 10.1 Qui est visible, qui est actif, selon la phase

| Phase | MainMenu | Loading | HUD |
|---|---|---|---|
| `Disconnected` | visible · **Connect actif** · Logique/Virtuel grisés | caché | caché |
| `Idle` | visible · Connect grisé · **Logique/Virtuel actifs** | caché | caché |
| `Streaming`, `Building`, `Stopping`, `Unloading` | caché | visible · Annuler grisé | caché |
| `Preparing`, `Readying` | caché | visible · **Annuler actif** | caché |
| `Ready` | caché | caché | visible · **Start** + Stop actifs |
| `Running` | caché | caché | visible · **Pause** + Stop actifs |
| `Paused` | caché | caché | visible · **Resume** + Stop actifs |

Chaque widget implémente cette table dans une fonction **`ApplyPhase(Phase)`** : un `Switch on Enum EPrismPhase` dont chaque sortie règle `Set Visibility` (`Visible` ou `Collapsed`) et `Set Is Enabled` sur ses boutons.

### 10.2 `WBP_PRISM_MainMenu`

**Palette :** un `Canvas Panel` plein écran, un `Vertical Box` avec :

- `Edit_HttpUrl`, `Edit_WsUrl` (Editable Text)
- `Check_ManualToken` (CheckBox) et `Edit_Token` (Editable Text, **Is Password** coché) — pour le développement uniquement
- `Edit_Seed`, `Edit_Ticks`, `Edit_MaxTicks` (Editable Text)
- `Btn_Connect`, `Btn_Logical`, `Btn_Virtual` (Buttons avec un `Text`)
- `Txt_Status`, `Txt_Error` (Text Blocks)

**Variables :** `Director` (Expose on Spawn), `GI` (`BP_PRISM_GameInstance`).

**`Event Construct`** :

```
[1] Event Construct
    ▶ [2] Cast To BP_PRISM_GameInstance   Object ◀ Get Game Instance      → Set GI
    ▶ [3] Set Text (Edit_HttpUrl)     ◀ GI.Config.HttpUrl
    ▶ [4] Set Text (Edit_WsUrl)       ◀ GI.Config.WebSocketUrl
    ▶ [5] Set Text (Edit_Seed)        ◀ ToText(GI.Seed)
    ▶ [6] Set Text (Edit_Ticks)       ◀ ToText(GI.TicksPerSecond)
    ▶ [7] Set Text (Edit_MaxTicks)    ◀ ToText(GI.MaxTicks)
    ▶ [8] Bind Event to ED_PhaseChanged   Target ◀ Director   → Custom Event EVT_PhaseChanged (Phase, Message)
    ▶ [9] Bind Event to ED_Error          Target ◀ Director   → Custom Event EVT_Error (Message)
    ▶ [10] ApplyPhase   Phase ◀ Director.Phase

[1] EVT_PhaseChanged    Phase   Message
    ▶ [2] Set Text (Txt_Status) ◀ Message     ▶ [3] ApplyPhase( Phase )

[1] EVT_Error           Message
    ▶ [2] Set Text (Txt_Error) ◀ Message
```

**`Btn_Connect` → On Clicked** :

```
[1] Btn_Connect  On Clicked
    ▶ [2] Branch   Condition ◀ AND( Starts With(Edit_HttpUrl.Text , "http") , Starts With(Edit_WsUrl.Text , "ws") )
          False ▶ [3] Set Text (Txt_Error) ◀ "URL invalide (http… / ws…)"   (fin)
          True  ▶ [4] Set Text (Txt_Error) ◀ ""
    ▶ [5] Director.EVT_ConnectToSyne    Options ◀ (a)

(a) Make FPrismSyneConnectionOptions
      HttpUrl ◀ Edit_HttpUrl → To String          WebSocketUrl ◀ Edit_WsUrl → To String
      bAutoReconnect ◀ GI.Config.bAutoReconnect   ReconnectDelaySeconds ◀ GI.Config.ReconnectDelaySeconds
      bUseManualSessionToken ◀ Check_ManualToken → Is Checked
      SessionToken ◀ Edit_Token → To String
```

Pour un lancement par le Launcher, le jeton vient de l'environnement (`LIVEX_SESSION_TOKEN`) : laissez la case décochée. Ne logguez jamais le jeton.

**Fonction `StartLoad(Mode : EPrismRenderMode)`** (appelée par les deux boutons de mode) :

```
[1] Function Entry  StartLoad   Mode
    ▶ [2] Branch   Condition ◀ (d)
          False ▶ [3] Set Text (Txt_Error) ◀ "Seed, ticks/s et max ticks doivent être > 0"   (fin)
          True  ▶ [4] Set Text (Txt_Error) ◀ ""
    ▶ [5] Set GI.Seed            ◀ (a)
    ▶ [6] Set GI.TicksPerSecond  ◀ (b)
    ▶ [7] Set GI.MaxTicks        ◀ (c)
    ▶ [8] Director.EVT_LoadWorld    New Mode ◀ Mode

(a) To Integer64 (String)   ◀ Edit_Seed.Text → To String        [À VÉRIFIER : nom exact du node]
(b) To Integer (String)     ◀ Edit_Ticks.Text → To String
(c) To Integer (String)     ◀ Edit_MaxTicks.Text → To String
(d) AND( (b) > 0 , AND( (c) > 0 , (a) > 0 ) )
```

`Btn_Logical → On Clicked` : `StartLoad(Logical)`. `Btn_Virtual → On Clicked` : `StartLoad(Virtual)`.

### 10.3 `WBP_PRISM_Loading`

**Palette :** `Overlay` plein écran avec un fond sombre, au centre : `Txt_Phase`, `PB_Loading` (Progress Bar), `Txt_Error`, `Btn_Cancel`.

```
[1] Event Construct
    ▶ [2] Bind Event to ED_PhaseChanged   Target ◀ Director   → EVT_PhaseChanged (Phase, Message)
    ▶ [3] Bind Event to ED_Progress       Target ◀ Director   → EVT_Progress (Progress, Text)
    ▶ [4] Bind Event to ED_Error          Target ◀ Director   → EVT_Error (Message)
    ▶ [5] ApplyPhase( Director.Phase )

[1] EVT_PhaseChanged   Phase   Message
    ▶ [2] Set Text (Txt_Phase) ◀ Message
    ▶ [3] Branch   Condition ◀ (Phase == Streaming)     True ▶ Set Text (Txt_Error) ◀ ""     (nouvelle tentative : on efface l'ancienne erreur)
    ▶ [4] ApplyPhase( Phase )

[1] EVT_Progress   Progress   Text
    ▶ [2] Branch   Condition ◀ (Progress < 0)
          True  ▶ [3] Set Is Marquee (PB_Loading) ◀ true
          False ▶ [4] Set Is Marquee ◀ false   ▶ [5] Set Percent (PB_Loading) ◀ Progress
    ▶ [6] Set Text (Txt_Phase) ◀ Text

[1] EVT_Error   Message     ▶ [2] Set Text (Txt_Error) ◀ Message

[1] Btn_Cancel  On Clicked  ▶ [2] Director.EVT_AbortLoad
```

`Progress` est une fraction entre 0 et 1 (le C++ la publie dans `[0,1]`) : `Set Percent` la prend telle quelle.

### 10.4 `WBP_PRISM_HUD`

**Palette :** `Txt_State`, `Txt_Tick`, `Txt_Alive`, `Txt_Season`, `Txt_Time`, et quatre boutons `Btn_Start`, `Btn_Pause`, `Btn_Resume`, `Btn_Stop`.

```
[1] Event Construct
    ▶ [2] Bind Event to ED_PhaseChanged   Target ◀ Director   → EVT_PhaseChanged (Phase, Message)
    ▶ [3] Bind Event to ED_Snapshot       Target ◀ Director   → EVT_Snapshot (Snapshot)
    ▶ [4] ApplyPhase( Director.Phase )

[1] EVT_PhaseChanged   Phase   Message
    ▶ [2] Set Text (Txt_State) ◀ Message     ▶ [3] ApplyPhase( Phase )

[1] EVT_Snapshot   Snapshot
    ▶ [2] Set Text (Txt_Tick)   ◀ Format Text "Tick {0} / {1}"   {0} ◀ Snapshot.Tick   {1} ◀ GI.MaxTicks
    ▶ [3] Set Text (Txt_Alive)  ◀ ToText(Snapshot.AliveCount)
    ▶ [4] Set Text (Txt_Season) ◀ To Text (String)( Snapshot.Season )
    ▶ [5] Set Text (Txt_Time)   ◀ ToText(Snapshot.SimulatedTimeSeconds)

[1] Btn_Start  On Clicked   ▶ Director.SendCommand( "Start" )
[1] Btn_Pause  On Clicked   ▶ Director.SendCommand( "Pause" )
[1] Btn_Resume On Clicked   ▶ Director.SendCommand( "Resume" )
[1] Btn_Stop   On Clicked   ▶ Director.EVT_StopSession
```

`GI` se récupère comme dans le menu (Cast sur `Get Game Instance`, une fois, dans `Construct`). Le bouton Stop arrête la simulation **et** ramène au menu : il n'y a pas de bouton « Retour menu » séparé. Les snapshots n'arrivent qu'en `Running` (§9.10) : les compteurs restent à leur dernière valeur en pause.

**Test de l'étape 7 :** les trois widgets compilent. `ApplyPhase` est identique en structure dans les trois widgets (seules les sorties du Switch changent, selon §10.1).

---

## 11. Étape 8 — Pawn, PlayerController, GameMode, mise en place

### 11.1 `BP_PRISM_ObserverPawn`

**Add → Blueprint Class → SpectatorPawn** → `BP_PRISM_ObserverPawn`. Le monde fait 224 000 uu de côté : sélectionnez le composant de mouvement et réglez une vitesse élevée (par exemple `Max Speed = 30000`, `Acceleration = 60000`, `Deceleration = 60000`). **[À VÉRIFIER]** le nom du composant et le ressenti caméra.

### 11.2 `BP_PRISM_PlayerController`

**Add → Blueprint Class → PlayerController** → `BP_PRISM_PlayerController`. Variable `Director` (`BP_PRISM_WorldDirector`).

```
[1] Event BeginPlay
    ▶ [2] Get Actor Of Class    Actor Class ◀ BP_PRISM_WorldDirector      → Set Director
    ▶ [3] Branch   Condition ◀ Is Valid(Director)
          False ▶ [4] Print String ◀ "Aucun BP_PRISM_WorldDirector dans LDK_MenuMap"   (fin)
          True  ▶ [5]
    ▶ [5] Create Widget    Class ◀ WBP_PRISM_MainMenu   Owning Player ◀ Self   Director ◀ Get Director
          ▶ [6] Add to Viewport   Z Order ◀ 0
    ▶ [7] Create Widget    Class ◀ WBP_PRISM_HUD        Owning Player ◀ Self   Director ◀ Get Director
          ▶ [8] Add to Viewport   Z Order ◀ 5
    ▶ [9] Create Widget    Class ◀ WBP_PRISM_Loading    Owning Player ◀ Self   Director ◀ Get Director
          ▶ [10] Add to Viewport  Z Order ◀ 10
    ▶ [11] Set Show Mouse Cursor   ◀ true
    ▶ [12] Set Input Mode Game And UI   In Widget to Focus ◀ (vide)   In Mouse Lock Mode ◀ Do Not Lock
```

Le PlayerController n'a pas d'autre logique : les widgets pilotent le Director directement.

### 11.3 `BP_PRISM_GameMode`

**Add → Blueprint Class → GameModeBase** → `BP_PRISM_GameMode`. Class Defaults : `Default Pawn Class = BP_PRISM_ObserverPawn`, `Player Controller Class = BP_PRISM_PlayerController`, `HUD Class = None`.

### 11.4 Réglage du GameMode

Ouvrez `LDK_MenuMap` → **World Settings → GameMode Override = `BP_PRISM_GameMode`**. Les sous-niveaux n'ont aucun GameMode.

### 11.5 Poser le Director

Dans `LDK_MenuMap`, glissez **un seul** `BP_PRISM_WorldDirector` dans le niveau (position quelconque, par exemple `(0, 0, 0)`). Vérifiez dans l'Outliner qu'il n'y en a qu'un. Sauvegardez la map.

---

## 12. Tests de bout en bout

Lancez SYNE (via le Launcher ou à la main), puis PIE depuis `LDK_MenuMap`. Surveillez l'**Output Log** pendant tous les tests.

| # | Test | Résultat attendu |
|---|---|---|
| T1 | PIE sans SYNE | Menu visible, **Connect** seul actif. Aucun `Accessed None` dans le log. |
| T2 | Clic **Connect** (SYNE lancé) | Statut « Connecté à SYNE », boutons Logique/Virtuel actifs, Connect grisé. |
| T3 | **Logique** | Phases : chargement → préparation → construction → confirmation → HUD « Monde prêt — cliquez sur Start ». Sol visible, obstacles, agents au sol. **Rien ne bouge** avant Start. |
| T4 | **Start** | « Simulation en cours », compteur de tick qui avance, agents qui marchent vers leur tuile. |
| T5 | **Pause** puis **Resume** | Compteurs figés puis repartis ; agents arrêtés puis repartis. |
| T6 | **Stop** | Retour menu, Logique/Virtuel actifs. Outliner de l'éditeur (World Outliner du PIE) : plus aucun agent, obstacle ni TileField. |
| T7 | Refaire T3 → T6 **deux fois** | Même résultat : aucun acteur en double, aucun `409`. |
| T8 | **Virtuel** (T3 → T6) | Barre de progression pendant la génération, île avec lacs, agents posés sur le terrain et qui marchent (NavMesh). |
| T9 | Annuler pendant « Préparation » | Retour menu, message « Chargement annulé ». |
| T10 | Couper SYNE pendant Running | Retour menu avec « Connexion perdue » ; après reconnexion, `Idle`. |
| T11 | URL fausse puis Connect | Message d'erreur lisible, menu reste utilisable. |

**Critère d'acceptation :** T3 à T8 passent trois fois de suite sans redémarrer l'éditeur.

---

## 13. Diagnostic rapide

| Symptôme | Cause probable | Correction |
|---|---|---|
| Le niveau ne se charge pas (`Load Stream Level` sans effet) | Nom sans `/` interprété comme `/Game/` | Utiliser le package complet `/PrismLdk/Level/...` (§5.4) ; le sous-niveau doit être dans le panneau **Levels** |
| Widgets absents, log « Aucun BP_PRISM_WorldDirector » | Director non posé dans `LDK_MenuMap` | §11.5 |
| `Accessed None` sur `GI` | Game Instance Class non réglée | Project Settings → Maps & Modes |
| Bloqué sur « Préparation » | `OnWorldInitialized` n'arrive pas (WebSocket, jeton) ou résultat de Prepare absent | Log du plugin ; vérifier URL WS et jeton ; bouton Annuler |
| `409 world_not_ready` | `Ready` envoyé avant la fin du build ou deux fois | Vérifier que le seul appel à `SendCommand("Ready")` est dans `EVT_BuildFinished` |
| Le sol logique est invisible | Matériau absent ou cube mal positionné | Cube à `Z = -50` avec échelle Z = 1 (§6.1) |
| Agents qui tombent (logique) | HISM sans collision | `BlockAll` sur les deux HISM |
| Agents immobiles | `Auto Possess AI` mal réglé, ou NavMesh absent | §7.1 ; dans le viewport, `P` (Show Navigation) pour voir le NavMesh ; volume bien posé |
| Agents sous le terrain (virtuel) | Collision du terrain désactivée ou `GetGroundZ` à 0 | `bCreateCollision` dans `VirtualSettings` ; node `Get Ground Height At` |
| Boutons grisés après une erreur | Phase restée en `Stopping`/`Unloading` | Vérifier que `Unload Stream Level` atteint `Completed` ; regarder le log |
| Clic Start sans effet | `bCommandPending` resté à true | Voir `EVT_OnError` (§9.6) : un échec réseau doit le remettre à false |
| Obstacles/agents minuscules | Échelle des meshes de base | Cube/Cylindre/Sphère de 100 uu : vérifier les échelles (§6, §7) |

---

## 14. Limites assumées

Ce guide reste minimal volontairement. Hors périmètre :

- **`OnWorldDelta`** : les obstacles ajoutés ou retirés en cours de run ne sont pas répercutés.
- **Journal d'événements SYNE (`OnSyneEvent`)** et inspecteur d'agent.
- **Fin de run au `MaxTicks`** : l'arrêt se fait au clic sur Stop.
- **Annulation pendant `Streaming` et `Building`** : impossible (non interruptibles).
- **`Stop` refusé ou sans réponse** : le monde local est quand même nettoyé ; le serveur peut rester préparé et refuser la `Prepare` suivante, avec un message lisible.
- **Pas de `Reset` et pas de relance du même run** : on repasse par le menu.
- **Package Windows/Linux** : à valider après T3 → T8 (matériaux simples, pas de Material Instance de master non cooké).
- **NavMesh d'un sous-niveau streamé en package** : à tester spécifiquement (§15).

Si un de ces points devient nécessaire, l'ajouter comme annexe sans toucher au flux principal.

---

## 15. Points à vérifier dans l'éditeur

Tout ce qui est écrit contre les `.h` est déjà confirmé. Il reste ce qu'on ne voit que dans l'éditeur :

1. Libellés exacts de **Change Streaming Method → Blueprint** et **Initially Loaded / Visible** (§5.3).
2. Dimensions réelles des formes `/Engine/BasicShapes/` **Cube**, **Cylinder**, **Sphere** : le guide suppose 100 uu (§6, §7).
3. Valeur de `HttpCode` dans `FPrismSyneError` pour un échec réseau : le guide suppose 0 (§9.6).
4. Nom exact du node **To Integer64 (String)** (§10.2).
5. Nom du composant de mouvement du SpectatorPawn et ressenti caméra (§11.1).
6. Clé `speed` dans `Traits` d'un agent : reprise des guides précédents ; si absente, l'agent garde la vitesse par défaut du Character (§7.4).
7. **NavMesh dynamique dans un sous-niveau streamé**, notamment en package : à valider en conditions réelles. Si le NavMesh ne se construit pas dans le sous-niveau, déplacez **uniquement** les `Nav Mesh Bounds Volume` dans `LDK_MenuMap`.

---

## Annexe A — Marqueurs de ressources (optionnel, mode logique)

À ajouter seulement quand le reste fonctionne.

1. **Actor** `BP_PRISM_ResourceMarker` : `Static Mesh` (`/Engine/BasicShapes/Sphere`, collision `NoCollision`, échelle 3) + `Text Render` (taille 400, au-dessus de la sphère).
2. Fonction `InitMarker(Type : EPrismSyneResourceType)` : `Switch on Enum` → `Set Text (Text Render)` ← `"Water"`, `"Food"`, `"Wood"` ou `"Mineral"`.
3. Dans le Director, fonction `SpawnResourceMarkers()`, appelée dans `EVT_BuildLogical` juste après `SpawnObstacles` :

```
[1] Function Entry
    ▶ [2] ForEachLoop    Array ◀ (Break WorldDesc → Resources)
          Loop Body ▶ [3]
[3] Branch   Condition ◀ ArrayElement.HasPosition          (une ressource sans position est ignorée)
    False ▶ (retour boucle)
    True  ▶ [4]
[4] Spawn Actor from Class    Class ◀ BP_PRISM_ResourceMarker    Spawn Transform ◀ (d)    Collision ◀ Always Spawn
    ▶ [5] InitMarker      Target ◀ ReturnValue   Type ◀ ArrayElement.TypeEnum
    ▶ [6] Registry.AddResourceMarker    Actor ◀ ReturnValue de [4]
          ▶ (retour boucle)

(a) Make Int Point                X ◀ (ArrayElement.Position.X → Truncate)   Y ◀ (ArrayElement.Position.Y → Truncate)
(b) Cell To Tile Center Unreal    Cell ◀ (a)          (si PositionIsCellIndex)
(c) Syne To Unreal                Position ◀ ArrayElement.Position   K ◀ Stats.K          (sinon)
(d) Make Transform                Location ◀ Select Vector( PositionIsCellIndex ? (b) : (c) )
```

`PositionIsCellIndex` indique si `Position` est un **indice de cellule** (cas des ressources initiales de `world_initialized`) ou une position continue. `Registry.AddResourceMarker` est déjà couvert par `DestroyWorldActors` via `GetResourceMarkers`.

En mode virtuel, les ressources `Water` sont déjà des lacs (C++) : n'ajoutez pas de marqueurs.
