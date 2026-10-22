# PRISM — Guide pas à pas : Menu, Représentation Logique et Monde Virtuel

*Pour Donovan · 22 octobre 2026 · Complète « PRISM — Guide pas à pas de la partie Blueprint ».*
*Ce guide remplace le flux actuel : un `MenuLevel` en premier niveau, deux représentations distinctes, et un mode « Monde Virtuel » stylisé hand-painted.*

## 0. Comment utiliser ce guide

Ce guide dit **quoi faire à l'intérieur de chaque élément**, nœud par nœud, comme le guide logique. Pour chaque élément : les variables à ajouter, les graphes à câbler dans l'ordre, puis un **test de fin d'étape**.

Conventions : `Nom` = nœud, variable ou fonction tel qu'il apparaît dans l'éditeur ; `→` = « puis branche sur » ; **[À VÉRIFIER]** = je n'ai pas pu le confirmer, regarde dans l'éditeur ; **[Piège]** = erreur silencieuse classique.

### 0.1 L'idée générale

Rien de ce que fait SYNE ne change. Le plugin C++ `PrismLdk` reçoit les messages et les expose à Blueprint. Ce guide ajoute **un point d'entrée (le menu)** et **une couche de rendu (les Renderer)**.

```text
Launcher → démarre SYNE + PRISM
                    │
                    ▼
        MenuLevel  (WBP_PRISM_MainMenu)
          │  Connexion + configuration (URL, token, seed, ticks)
          │  Bouton 1 → Représentation Logique
          │  Bouton 2 → Monde Virtuel
          ▼
    BP_PRISM_GameInstance   ← seul à parler au plugin
      │  SelectedRenderMode = Logical | Virtual
      ▼
   ┌──────────────┴──────────────┐
   ▼                             ▼
LDK_LogicalMap              LDK_VirtualMap
   │                             │
   ▼                             ▼
BP_PRISM_WorldDirector  (identique dans les deux cas)
   │  lit GI.SelectedRenderMode
   │  spawn le renderer adapté
   ▼
 ┌──────────────────────┬───────────────────────────────┐
 ▼                      ▼                               ▼
BP_PRISM_LogicalRenderer   BP_PRISM_VirtualRenderer   (BPI_PRISM_Renderer)
 │  HISM actuel            │  APrismLdkVirtualWorldActor
 │  (inchangé)             │  + PCGData + végétation
 └──────────────────────┴───────────────────────────────┘
   │                             │
   └──────────► agents, registre, NavMesh ◄──────────┘
                  (identique dans les deux cas)
```

**Le cœur de la méthode** : le `WorldDirector` ne change pas de rôle. Il lit le mode dans le GameInstance et délègue la partie visuelle à un Renderer. La simulation, les agents, le registre, le NavMesh : **identiques** dans les deux modes.

### 0.2 Les 7 règles à ne jamais enfreindre

1. **Aucune formule de conversion dans un graphe.** Tu utilises les nœuds `SYNE|Projection` et les helpers `PRISM|PCG` du plugin.
2. **`k` n'est jamais une variable Blueprint.** Il se lit toujours via `Get Stats` du registre.
3. **Le snapshot est la vérité.** Les deltas et événements servent aux notifications.
4. **Seul le GameInstance** appelle `Configure` et `Connect`.
5. **Seul le WorldDirector** crée et détruit les acteurs du monde.
6. **Pas de `Get All Actors Of Class` par tick** : utilise le registre.
7. **Les agents s'identifient par leur `Id` SYNE** (Integer64).

> **Règle 8 (nouvelle)** : **le Renderer ne fabrique jamais d'état**. Il ne fait que *dessiner* ce que SYNE a décrit. S'il a besoin d'une hauteur de sol, il la demande au terrain — il ne l'invente pas.

### 0.3 Ce qui a déjà été fait côté C++ (22/10)

Le plugin `PrismLdk` a été étendu pour le mode virtuel. **Tu n'as rien à écrire en C++** pour cette partie.

| Élément | Fichier | Rôle |
| :-- | :-- | :-- |
| `UPrismLdkPCGData` | `Public/PrismLdkPCGData.h` | Données SYNE projetées, prêtes à consommer |
| `CreatePCGDataFromWorldDescription` | `PrismLdkBlueprintLibrary.h` | Construit le `PCGData` depuis la description |
| `GetObstaclesUnreal` | idem | Obstacles projetés (centres + rayons) |
| `GetWaterResourceCells` | idem | Cellules de ressources `Water` positionnées |
| `CellToTileCenterUnreal` | idem | Centre de tuile via le registre |
| `GetPlayableBoundsUnreal` | idem | Bornes jouables (NavMesh, PCG Volume) |
| `APrismLdkVirtualWorldActor` | `Public/PrismLdkVirtualWorld.h` | Génère l'îlle, les falaises, la mer et les lacs **au runtime** |
| `FPrismLdkVirtualWorldSettings` | idem | Tous les réglages de forme et de relief, réglables en Blueprint |

**Les specs** : `PrismLdk.PCG.Data.BuildFromDescription.Basic`, `PrismLdk.VirtualWorld.BuildsIsland`, `PrismLdk.VirtualWorld.RejectsInvalidData`. Elles tournent avec les autres (Tools → Session Frontend → Automation).

### 0.4 Ce qui change par rapport au guide logique

| Élément | Guide logique (B1→B6) | Ce guide (V0→V9) |
| :-- | :-- | :-- |
| Point d'entrée | `LDK_BaseMap` directement | `MenuLevel` → bouton → level choisi |
| Rendu | Codé dans le `WorldDirector` | Délégué à un `BPI_PRISM_Renderer` |
| Sol | `BP_PRISM_TileField` (HISM) | `BP_PRISM_TileField` (inchangé) **ou** terrain procédural |
| eau | aucune | Lacs/étangs sur ressources `Water` |
| Navigation | tuiles planes | tuiles planes **ou** relief irrégulier |

> **\[Piège\]** Le guide logique reste **valide**. Ne le modifie pas : il décrit le cœur du système. Ce guide construit *par-dessus*. Le jalon B1 (GameInstance, session) est déjà fait : ici on ne le refait pas, on l'étend.

### 0.5 Les jalons et leur test de fin

| Jalon | Ce que tu fais | Test de fin |
| :-- | :-- | :-- |
| V0 | Niveaux, GameModes, renommage | `MenuLevel` s'ouvre, `LDK_LogicalMap` existe |
| V1 | GameInstance : mode de rendu | `SelectedRenderMode` existe, compile |
| V2 | `BPI_PRISM_Renderer` | l'interface compile |
| V3 | `BP_PRISM_RendererBase` | compile |
| V4 | `BP_PRISM_LogicalRenderer` | le monde logique s'affiche **identique** |
| V5 | `BP_PRISM_VirtualRenderer` (socle) | l'îlle s'affiche |
| V6 | `WorldDirector` branché sur le Renderer | les deux modes fonctionnent |
| V7 | `WBP_PRISM_MainMenu` + `MenuLevel` | le menu connecte et lance |
| V8 | Végétation + style | l'île est « habitée » |
| V9 | Packaging | le build Win/Linux tourne |

> **Ordre impératif** : V4 avant V6. Si tu branches le Director sur un Renderer qui n'existe pas encore, tu ne peux plus valider le mode logique, et tu perds ton point de repère.

---

## 1. Jalon V0 — Niveaux, GameModes, renommage

### 1.1 Renommer la map logique

1. Content Browser → `Plugins/PrismLdk/Content/Level/`
2. Clic droit sur `LDK_BaseMap.umap` → **Rename** → `LDK_LogicalMap.umap`
3. Un clic droit dans le Content Browser → **Fix Up Redirectors** (si le Renamer ne les a pas faits)

> **\[Piège\]** Le Renamer de l'éditeur est la seule façon safe. Un renommage manuel du fichier sur le disque casse toutes les références serialisées.

### 1.2 Créer les deux nouveaux niveaux

**Content Browser → Plugins/PrismLdk/Content/Level/ → Add → Levels → Basic Empty Level**

- `MenuLevel.umap`
- `LDK_VirtualMap.umap`

Ouvre-les. Dans chacun :
- **Place Actors** → ajouter `Directional Light` et `Sky Light` (le ciel de base)
- **Source Control** → *Check Out* si le projet est sous Git

### 1.3 Créer les deux GameModes

**Content Browser → Plugins/PrismLdk/Content/Framework/ → Add → Blueprints → Blueprint Class → All Classes → GameModeBase**

- `BP_PRISM_GameMode_Menu` (parent : `GameModeBase`)
- `BP_PRISM_GameMode_InGame` (parent : `GameModeBase`)

**`BP_PRISM_GameMode_InGame` → Class Defaults** :

| Champ | Valeur |
| :-- | :-- |
| `Player Controller Class` | `BP_PRISM_PlayerController` |
| `Default Pawn Class` | `BP_PRISM_ObserverPawn` |
| `HUD Class` | `BP_PRISM_HUD` |

**`BP_PRISM_GameMode_Menu` → Class Defaults** :

| Champ | Valeur |
| :-- | :-- |
| `Player Controller Class` | `BP_PRISM_PlayerController` |
| `Default Pawn Class` | `None` |
| `HUD Class` | `None` |

> **\[Piège\]** Le `Default Pawn Class` du mode Menu doit être `None` : un `SpectatorPawn` dans le menu ferait tourner une caméra inutile et pourrait capter l'input avant l'UI.

### 1.4 Régler les niveaux (World Settings)

**`LDK_LogicalMap`** : World Settings (bouton en haut) → **GameMode Override** = `BP_PRISM_GameMode_InGame`.
**`LDK_VirtualMap`** : idem → `BP_PRISM_GameMode_InGame`.
**`MenuLevel`** : World Settings → **GameMode Override** = `BP_PRISM_GameMode_Menu`.

### 1.5 Project Settings → Maps & Modes

| Champ | Valeur |
| :-- | :-- |
| `Game Default Map` | `Plugins/PrismLdk/Content/Level/MenuLevel` |
| `Editor Startup Map` | `Plugins/PrismLdk/Content/Level/MenuLevel` |
| `Global Default GameMode` | `BP_PRISM_GameMode_Menu` |
| `Game Instance Class` | `BP_PRISM_GameInstance` |

> **\[Piège\]** `Game Instance Class` se règle **ici**, pas sur le GameMode. Sans ça, `BP_PRISM_GameInstance` n'est jamais instancié : aucun `Init`, aucune connexion, et aucune erreur visible. C'est le piège le plus coûteux du projet.

### 1.6 Test de fin de V0

1. Ouvre `MenuLevel` → il s'ouvre, une lumière et un ciel sont visibles.
2. Ouvre `LDK_LogicalMap` → le `BP_PRISM_WorldDirector` est là (tu l'ajoutes en V6 s'il manque).
3. Le Content Browser ne montre plus aucun `LDK_BaseMap`.

---

## 2. Jalon V1 — GameInstance : le mode de rendu

Ouvre `Plugins/PrismLdk/Content/Core/BP_PRISM_GameInstance`.

### 2.1 L'énumération du mode

**Content Browser → Add → Blueprints → Enumeration** → nom `EPrismRenderMode`. Ouvre-la et ajoute deux entrées, dans cet ordre :

1. `Logical`
2. `Virtual`

> **\[Piège\]** L'ordre des entrées compte : `Logical` doit être **la première** (valeur 0), sinon un acteur sauvegardé avec la valeur par défaut changerait de mode. Crée l'énum **avant** de l'utiliser dans le GameInstance et le widget.

### 2.2 Nouvelle variable

**My Blueprint → + → Variable** :

| Variable | Type | Default | Notes |
| :-- | :-- | :-- | :-- |
| `SelectedRenderMode` | `EPrismRenderMode` | `Logical` | **Instance Editable** : coche *Instance Editable* dans Details (tu la poseras dans le menu) |

> **\[Note\]** Ce n'est pas une variable *privée* : le menu doit la poser. Coche **Instance Editable** (pas *Edit Anywhere* — ça l'exposerait dans l'éditeur de niveau sans raison).

### 2.3 Nouvelle fonction de relais

**My Blueprint → + → Function** → nom `SetRenderMode`, entrée `Mode` (type `EPrismRenderMode`) :

```text
SetRenderMode (Mode)
  → Set SelectedRenderMode
```

Une fonction plutôt qu'un accès direct : elle centralise le point d'écriture, et tu pourras y ajouter un `Print String` de debug.

### 2.4 Test de fin de V1

1. Compile.
2. En PIE sur `MenuLevel`, un `Print String (SelectedRenderMode)` affiche `Logical`.
3. La fonction `SetRenderMode` existe et compile.

---

## 3. Jalon V2 — L'interface `BPI_PRISM_Renderer`

**Content Browser → Plugins/PrismLdk/Content/World/ → Add → Blueprints → Blueprint Interface** → nom `BPI_PRISM_Renderer`.

### 3.1 Les 5 fonctions

**My Blueprint → + → Function**, une par une. Pour chacune : **Details → Inputs → +** pour ajouter les entrées.

| Fonction | Entrée | Sortie | Type de sortie |
| :-- | :-- | :-- | :-- |
| `Initialize` | `WorldDescription` : `FPrismSyneWorldDescription` | — | — |
| | `Registry` : `Prism Ldk World Registry Base` | | |
| | `Config` : `DA_PRISM_SessionConfig` | | |
| `Build` | — | `bSuccess` | `Boolean` |
| `ReconcileFromSnapshot` | `Snapshot` : `FPrismSyneWorldSnapshot` | — | — |
| `Reset` | — | — | — |
| `GetRendererType` | — | `RenderMode` | `EPrismRenderMode` |

### 3.2 Test de fin de V2

Compile. L'interface est vide mais valide. C'est normal : **une interface Blueprint n'a aucun implémentation**, elle ne porte que des signatures.

---

## 4. Jalon V3 — `BP_PRISM_RendererBase`

**Content Browser → Plugins/PrismLdk/Content/World/ → Add → Blueprints → Blueprint Class → All Classes → Actor** → nom `BP_PRISM_RendererBase`.

### 4.1 Class Settings → Interfaces

**Class Settings → Interfaces → Add** → `BPI_PRISM_Renderer`.

Puis, dans **My Blueprint → Interfaces**, déplie `BPI_PRISM_Renderer` : chaque fonction apparaît. Clic droit sur chacune → **Implement**. (Sur un Actor, l'implémentation crée un *event* que tu peux appeler comme une fonction.)

> **\[Piège\]** Ne confonds pas **Add** (déclarer que la classe implémente l'interface) et **Implement** (créer le corps de la fonction). Si tu oublies **Implement**, le Director appellera l'interface et rien ne se passera — sans erreur.

### 4.2 Variables

| Variable | Type | Notes |
| :-- | :-- | :-- |
| `WorldDesc` | `FPrismSyneWorldDescription` | Copie de la description reçue |
| `Registry` | `Prism Ldk World Registry Base` | Pour `Get Stats` |
| `Config` | `DA_PRISM_SessionConfig` | Pour `TileSizeUu` |
| `GameInstance` | `BP_PRISM_GameInstance` | Pour `SelectedRenderMode` |

### 4.3 Implémentations par défaut

**`Initialize` (Event → Implement Initialize)** :

```text
Implement Initialize (WorldDescription, Registry, Config)
  → Set WorldDesc      ← WorldDescription
  → Set Registry       ← Registry
  → Set Config         ← Config
  → Get Game Instance → Cast To BP_PRISM_GameInstance → Set GameInstance
```

**`GetRendererType` (Implement)** :

```text
Implement GetRendererType
  → Branch (GameInstance est valide)
      vrai → Get GameInstance → Cast To BP_PRISM_GameInstance → Get SelectedRenderMode
```

**`Reset` (Implement)** : laisse vide (les enfants le surchargent).
**`Build` (Implement)** : renvoie `false` par défaut (les enfants le surchargent).
**`ReconcileFromSnapshot` (Implement)** : laisse vide.

### 4.4 Test de fin de V3

Compile. Ouvre l'onglet **Class Defaults** : la liste des fonctions doit montrer `Initialize`, `Build`, `Reconcile From Snapshot`, `Reset`, `Get Renderer Type`.

---

## 5. Jalon V4 — `BP_PRISM_LogicalRenderer` (extraire l'existant)

**But** : ne rien changer au rendu actuel, mais le mettre dans un Renderer pour que le Director ait une interface unique.

### 5.1 Créer le Blueprint

**Add → Blueprints → Blueprint Class → All Classes → `BP_PRISM_RendererBase`** → nom `BP_PRISM_LogicalRenderer`, dossier `World/`.

### 5.2 Variables

| Variable | Type | Notes |
| :-- | :-- | :-- |
| `TileField` | `BP_PRISM_TileField` | Le sol HISM |
| `bLogicalBuilt` | `Boolean` | Évite un double build |

### 5.3 `Build` — le cœur

Implémente `Build` (clic droit sur la fonction dans l'override → **Implement Build**).

```text
Implement Build
  → Branch (bLogicalBuilt) → vrai : Return (déjà construit)
  → Get Game Instance → Cast To BP_PRISM_GameInstance → Get Config
  → Spawn Actor from Class (BP_PRISM_TileField)
        Spawn Transform : Location (0,0,0), Rotation (0,0,0)
        Collision Handling Override : Always Spawn
  → Set TileField
  → TileField → Build (WorldDesc)
  → Set bLogicalBuilt = true
  → Call ED_PRISM_RenderReady      ← ajouté en V6 (§6.4)
  → Return true
```

> **\[Piège\]** `TileField.Build` lit `Get Stats` **du registre**. Le registre doit avoir été alimenté (`SetStatsFromDescription`) **avant** l'appel. L'ordre dans le Director (§6) est donc impératif, pas décoratif.

### 5.4 `Reset`

```text
Implement Reset
  → Branch (IsValid (TileField)) → vrai :
       TileField → Clear
       TileField → Destroy Actor
       Set TileField = None
  → Set bLogicalBuilt = false
```

### 5.5 `ReconcileFromSnapshot`

Laisse vide pour l'instant : le rendu logique est **statique** (les obstacles ne changent pas d'apparence entre deux ticks). Si tu veux que les obstacles *disparaissent* quand SYNE les supprime, tu le feras en V6 (le Director s'en charge déjà).

### 5.6 Test de fin de V4 — le plus important du jalon

Ce test ne doit **rien** changer au comportement actuel.

1. Dans `LDK_LogicalMap`, place un `BP_PRISM_LogicalRenderer` en plus du `BP_PRISM_WorldDirector` (tu le retireras en V6).
2. **Ne le branche pas encore.** Compile et lance.
3. Le monde doit s'afficher **exactement comme avant** (tuiles HISM, obstacles, agents).
4. Le Renderer ne doit **rien** faire (il n'est appelé par personne).

> Si le monde s'affiche différemment, c'est que tu as modifié autre chose. Reviens en arrière : V4 ne doit être **que** de l'extraction de code.

---

## 6. Jalon V6 (fait avant V5) — Brancher le `WorldDirector`

**But** : le Director choisit le Renderer selon le mode, et appelle `Initialize` puis `Build`.

> Je place ce jalon **avant** V5 à dessein : le mode logique doit rester vérifiable à chaque étape. V5 (Renderer virtuel) viendra branché sur une chaîne déjà testée.

### 6.1 Variables du Director

Ouvre `Plugins/PrismLdk/Content/World/BP_PRISM_WorldDirector`.

| Variable | Type | Notes |
| :-- | :-- | :-- |
| `ActiveRenderer` | `BP_PRISM_RendererBase` | Le renderer actif |
| `RenderMode` | `EPrismRenderMode` | Copie locale du mode choisi |

### 6.2 `Event BeginPlay` — lire le mode

Après l'existant :

```text
Event BeginPlay
  → ... (existant : Set Registry, Get Game Instance → Cast → Set GI)
  → Get Game Instance → Cast To BP_PRISM_GameInstance
      → Get SelectedRenderMode → Set RenderMode
```

> **\[Piège\]** Le `Open Level` **recrée** le `GameInstance` ? **Non** : le GameInstance survit au changement de level. `SelectedRenderMode` est donc toujours lisible dans le nouveau level. C'est ce qui rend le menu possible.

### 6.3 `BuildWorldFromDescription` — deleguer le rendu

Reprends le graphe existant (§4.5 du guide logique) et modifie-le :

```text
BuildWorldFromDescription (World)
  → Set bBuilding = true
  → ResetWorld
  → Registry.SetTileSizeUu (Config.TileSizeUu)
        → false → Print « TileSizeUu invalide » → Set bBuilding = false → Return
  → Registry.SetStatsFromDescription (World)
  → Registry.GetStats → Branch (bScaleValid)
        → false → Print « échelle invalide » → Set bBuilding = false → Return

  → *** NOUVEAU : choisir et construire le Renderer ***
  → Branch (IsValid (ActiveRenderer)) → vrai :
       ActiveRenderer → Reset
       ActiveRenderer → Destroy Actor
       Set ActiveRenderer = None
  → Switch on RenderMode
       Logical → Spawn Actor from Class (BP_PRISM_LogicalRenderer) → Set ActiveRenderer
       Virtual → ** laisse cette branche vide pour l'instant ** (V5 la remplit)
  → ActiveRenderer → Initialize (World, Registry, Config)
  → ActiveRenderer → Build → Branch (bSuccess)
       → false → Print « rendu impossible » → Set bBuilding = false → Return

  → SpawnObstacles (World.Obstacles)
  → SpawnResourceMarkers (World.Resources)
  → SpawnInitialAgents (World.Agents)
  → Set bBuilding = false
  → (le GI.RequestReady est appelé par l'appelant, après BuildWorldFromDescription)
```

### 6.4 Qui appelle `Ready` — le point le plus subtil

Dans le guide logique (§4.5 étape A), le `EVT_WorldInitialized` fait :

```text
EVT_WorldInitialized (World) → BuildWorldFromDescription (World) → GI.RequestReady
```

**Problème** : en mode virtuel, `Build` peut prendre plusieurs secondes (génération du maillage). Si `Ready` part **avant** la fin, SYNE démarre la simulation pendant que le terrain n'existe pas.

**Solution** : le Renderer signale la fin. Deux options :

**Option A (recommandée) — un dispatcher sur le Renderer** :

Dans `BP_PRISM_RendererBase`, crée un **Event Dispatcher** `ED_PRISM_RenderReady` (sans entrée). `Build` l'appelle quand le rendu est prêt.

```text
// Dans BP_PRISM_RendererBase
Event Dispatcher : ED_PRISM_RenderReady
```

Puis dans le Director :

```text
EVT_WorldInitialized (World)
  → Branch (RenderMode == Virtual) → vrai :
       WorldDirector → Assign ED_PRISM_RenderReady (du renderer) → Custom Event EVT_RenderReady
  → BuildWorldFromDescription (World)
```

```text
EVT_RenderReady
  → GI.RequestReady
```

**Option B — un flag + un tick** : `bWorldReadyToStart` mis à true par le Renderer, vérifié dans le `Tick` du Director. Plus simple mais moins propre (un tick qui attend).

> **\[Piège\]** `Assign` sur un dispatcher d'un **Actor** : l'Event Dispatcher d'un Actor s'assign depuis n'importe quel Blueprint (`Acteur → Assign ED_...`). Il ne faut **pas** le chercher dans le GameInstance. Si tu ne le trouves pas dans le menu, fais un clic droit sur la variable `ActiveRenderer` dans le graphe → le menu contextuel propose `Assign ED_PRISM_RenderReady`.

### 6.5 `ResetWorld` — détruire le Renderer

Ajoute à la fin de `ResetWorld` (le graphe existant du guide logique §4.5) :

```text
ResetWorld
  → ... (existant : destruction agents / obstacles / marqueurs, Registry.ClearAll)
  → ** RETIRE l'ancien TileField.Clear / Destroy du Director
       (le TileField appartient maintenant au Renderer logique) **
  → Branch (IsValid (ActiveRenderer)) → vrai :
       ActiveRenderer → Reset
       ActiveRenderer → Destroy Actor
       Set ActiveRenderer = None
  → Set bVirtualBuilt / bLogicalBuilt = false (sur le Renderer, via Reset)
  → Set LastRunId = ""
```

> **\[Piège\]** Si tu laisses l'ancien `TileField.Clear` dans le Director et que le Renderer possède désormais le `TileField`, tu vas nettoyer un acteur qui n'existe plus (ou détruire celui du Renderer avant qu'il ne serve). **Une seule autorité par acteur** : le Director possède agents, obstacles et marqueurs ; le Renderer possède le sol et le terrain.

### 6.6 `UpdateFromSnapshot` — passer au Renderer

Ajoute à la fin de `UpdateFromSnapshot` :

```text
  → Branch (IsValid (ActiveRenderer)) → vrai :
       ActiveRenderer → ReconcileFromSnapshot (Snapshot)
```

### 6.7 Test de fin de V6

1. `LDK_LogicalMap`, `RenderMode = Logical` (par défaut) → **le monde s'affiche exactement comme avant**. Aucun doublon d'obstacle, aucun doublon d'agent.
2. `Registry.GetAgentCount` = `Snapshot.AliveCount`.
3. `Registry.GetObstacleCount` = 5 (le profil prism en déclare 5).
4. Le `ActiveRenderer` n'est pas `None` (vérifie avec un `Print String` de son nom).

---

## 7. Jalon V5 — `BP_PRISM_VirtualRenderer` (socle)

**But** : générer l'îlle stylisée à partir des données SYNE.

### 7.1 Créer le Blueprint

**Add → Blueprints → Blueprint Class → All Classes → `BP_PRISM_RendererBase`** → nom `BP_PRISM_VirtualRenderer`, dossier `World/`.

### 7.2 Composants

**Aucun composant à ajouter.**

`APrismLdkVirtualWorldActor` (C++) porte **déjà** ses deux `ProceduralMeshComponent` (`TerrainMesh` et `WaterMesh`). Il est spawné par le Renderer ; le Renderer reste un acteur vide qui pilote.

> **\[Piège\]** Ne mets **pas** de `ProceduralMeshComponent` dans `BP_PRISM_VirtualRenderer` : tu aurais deux maillages, deux collisions, et le Renderer ne saurait plus lequel modifier.

### 7.3 Variables

| Variable | Type | Notes |
| :-- | :-- | :-- |
| `VirtualWorld` | `Prism Ldk Virtual World Actor` | L'acteur C++ |
| `PCGData` | `Prism Ldk PCG Data` | Les données projetées |
| `VWSettings` | `Prism Ldk Virtual World Settings` | Les réglages (struct) |
| `bVirtualBuilt` | `Boolean` | Évite un double build |
| `bRenderReady` | `Boolean` | Signal de fin (mode synchrone : `true` après `Build`) |

### 7.4 `Initialize`

```text
Implement Initialize (WorldDescription, Registry, Config)
  → (super : Set WorldDesc / Registry / Config / GameInstance)
  → Set VWSettings
        (utilise un Blueprint Struct de `FPrismLdkVirtualWorldSettings` comme
         valeur par défaut : Class Defaults → VWSettings →Structure →
         toutes les valeurs par défaut du C++)
```

> **\[Piège\]** `FPrismLdkVirtualWorldSettings` est une **USTRUCT** exposée. Le Renderer doit en avoir **une instance**, pas un pointeur. Si tu veux la régler depuis l'extérieur, rends la variable `VirtualWorld` accessible et modifie `VirtualWorld → (réglages)` — ou ajoute une fonction `ApplySettings (NewSettings)`.

### 7.5 `Build` — la génération

```text
Implement Build
  → Branch (bVirtualBuilt) → vrai : Return true
  → Branch (IsValid (Registry)) → faux : Return false
  → Registry → Get Stats → Branch (bScaleValid) → faux : Return false

  → *** Données projetées ***
  → Create PCG Data From World Description (WorldDesc) → Set PCGData
        → null → Print « PCGData nul » → Return false

  → *** Acteur du monde virtuel ***
  → Spawn Actor from Class (Prism Ldk Virtual World Actor)
        Spawn Transform : Location (0,0,0)
        Collision Handling Override : Always Spawn
  → Set VirtualWorld

  → *** Génération ***
  → VirtualWorld → Build World (PCGData, VWSettings)
        → false → Print « génération impossible » → Return false

  → Branch (IsValid (VirtualWorld → TerrainMaterialOverride)) → faux :
       (laisser le matériau du composant : le Blueprint du Renderer le réglera)
  → Set bVirtualBuilt = true
  → Set bRenderReady = true
  → Call ED_PRISM_RenderReady
  → Return true
```

### 7.6 `Reset`

```text
Implement Reset
  → Branch (IsValid (VirtualWorld)) → vrai :
       VirtualWorld → Destroy Actor
       Set VirtualWorld = None
  → Set PCGData = None
  → Set bVirtualBuilt = false
  → Set bRenderReady = false
```

### 7.7 `ReconcileFromSnapshot`

Laisse vide. Le terrain est **statique** : il est reconstruit si la description change (nouveau run), pas à chaque tick.

> **\[Piège\]** `ReconcileFromSnapshot` est appelé **à chaque tick** (6 par seconde). N'y mets **jamais** de reconstruction de maillage : le jeu planterait.

### 7.8 Le matériau du terrain

L'acteur C++ expose `TerrainMaterialOverride` et `WaterMaterialOverride` (des `UMaterialInterface`).

**Option propre** : crée un Blueprint du C++ : `BP_PRISM_VirtualWorld` (parent `APrismLdkVirtualWorldActor`), dans lequel tu assignes :
- `TerrainMaterialOverride` → ton matériau master stylisé
- `WaterMaterialOverride` → ton matériau d'eau

Puis, dans `BP_PRISM_VirtualRenderer`, spawn **ce Blueprint** :

```text
→ Spawn Actor from Class (BP_PRISM_VirtualWorld)
```

et non la classe C++ directe. Comme ça, l'artiste peut retoucher le matériau sans toucher au graphe.

### 7.9 Test de fin de V5

1. Place un `BP_PRISM_VirtualRenderer` dans `LDK_VirtualMap`.
2. Branche-le temporairement : `EVT_WorldInitialized → BuildWorldFromDescription`.
3. **Attendu** :
   - Une île apparaît, de la taille `224 000 × 224 000` uu.
   - **Trois montagnes** sur la côte sud (les trois disques `r = 150` du profil prism).
   - **Une crete** au centre (disque `r = 60`).
   - **Un petit rocher** au nord-ouest (`amer-1`, `r = 8`).
   - **Trois lacs** (les 3 ressources `water` du mock) — visibles uniquement si le matériau de l'eau est assigné.
   - Une **mer** autour de l'île.
4. Aucun crash, aucun agent (pas encore branché).

> **\[Piège\]** L'amer (`r = 8` SYNE → 800 uu) est **plus petit qu'une maille** si `GridResolution` est bas. Avec `GridResolution = 192` sur 224 000 uu, une maille fait 1167 uu : l'amer est sous la maille. **Passe `GridResolution` à 384 ou 512** si tu veux voir l'amer. C'est un réglage, pas un bug.

---

## 8. Jalon V7 — Le menu

### 8.1 `WBP_PRISM_MainMenu`

**Content Browser → Plugins/PrismLdk/Content/UI/ → Add → User Interface → Widget Blueprint** → nom `WBP_PRISM_MainMenu`.

### 8.2 Designer — les éléments

Pose ces widgets dans un `Canvas Panel` (ou un `Vertical Box` à gauche, un `Vertical Box` de boutons à droite) :

**Panneau « Connexion »** :

| Widget | Nom | Notes |
| :-- | :-- | :-- |
| `EditableTextBox` | `Txt_HttpUrl` | Default Text `http://127.0.0.1:5181` |
| `EditableTextBox` | `Txt_WsUrl` | Default Text `ws://127.0.0.1:5180` |
| `EditableTextBox` | `Txt_Token` | Le jeton de session (mode dev) |
| `EditableTextBox` | `Txt_Seed` | Default Text `42` |
| `EditableTextBox` | `Txt_Tps` | Default Text `6` |
| `EditableTextBox` | `Txt_MaxTicks` | Default Text `400` |

**Panneau « Simulation »** :

| Widget | Nom |
| :-- | :-- |
| `Button` | `Btn_Connect` |
| `Button` | `Btn_Prepare` |
| `TextBlock` | `Txt_State` |
| `TextBlock` | `Txt_Error` |

**Panneau « Représentation »** :

| Widget | Nom |
| :-- | :-- |
| `Button` | `Btn_Logical` |
| `Button` | `Btn_Virtual` |
| `Button` | `Btn_Quit` |

### 8.3 Graph — variables

| Variable | Type | Notes |
| :-- | :-- | :-- |
| `GI` | `BP_PRISM_GameInstance` | Le GameInstance |
| `Config` | `DA_PRISM_SessionConfig` | Pour les valeurs par défaut |

### 8.4 `Event Construct`

```text
Event Construct
  → Get Game Instance → Cast To BP_PRISM_GameInstance → Set GI
  → GI → Get Config → Set Config
  → Branch (IsValid (GI)) → faux : Return
  → Set Txt_HttpUrl  (Text) ← Config.HttpUrl
  → Set Txt_WsUrl    (Text) ← Config.WebSocketUrl
  → Set Txt_Seed     (Text) ← ToString (Config.DefaultSeed)
  → Set Txt_Tps      (Text) ← ToString (Config.DefaultTicksPerSecond)
  → Set Txt_MaxTicks (Text) ← ToString (Config.DefaultMaxTicks)
  → GI → Assign ED_PRISM_Connected    → Custom Event EVT_Connected
  → GI → Assign ED_PRISM_Disconnected → Custom Event EVT_Disconnected
  → GI → Assign ED_PRISM_ControlResult → Custom Event EVT_ControlResult
  → GI → Assign ED_PRISM_Error        → Custom Event EVT_Error
  → RefreshButtons
```

### 8.5 `RefreshButtons` — l'état réel des boutons

**My Blueprint → + → Function** → `RefreshButtons` :

```text
RefreshButtons
  → Branch (IsValid (GI)) → faux : Return
  → GI → Get Config → Set Config

  → LDK = GI → (variable privée LDK du GameInstance : inaccessible)
```

> **\[Piège\]** La variable `LDK` du GameInstance est **privée**. Pour lire l'état de connexion depuis le widget, il faut soit :
> **(a)** rendre `LDK` accessible (coche *Public* dans les détails de la variable du GameInstance), soit
> **(b)** ajouter une fonction `GetConnectionState () → EPrismSyneConnectionState` dans le GameInstance :
> ```text
> // BP_PRISM_GameInstance
> Function GetConnectionState
>   → LDK → Get Connection State
> ```
> **(b) est mieux** : le GameInstance reste le seul à toucher le plugin. Choisis (b).

Avec (b), la fonction du widget :

```text
RefreshButtons
  → Branch (IsValid (GI)) → faux : Return
  → GI → Get Connection State → Set State
  → GI → Get Status → Set Status

  → Set Btn_Connect  (Enabled) ← (State != Connected)
  → Set Btn_Prepare (Enabled) ← (State == Connected) AND (Status.State != Running) AND (Status.State != Paused)
  → Set Btn_Logical (Enabled) ← (Status.bWorldPrepared)
  → Set Btn_Virtual(Enabled) ← (Status.bWorldPrepared)
  → Set Btn_Quit    (Enabled) ← true

  → Set Txt_State (Text) ← ToString (Status.State)
```

> **\[À VÉRIFIER\]** Les valeurs exactes de `EPrismSyneRunState` : `Idle`, `WorldPreparing`, `Ready`, `Running`, `Paused`, `Finished`, `Unknown`. Ouvre l'énum dans l'éditeur pour confirmer.

### 8.6 Les 4 boutons

**`Btn_Connect → OnClicked`** :

```text
OnClicked
  → Branch (IsValid (GI)) → faux : Return
  → Config → Get HttpUrl  → GI → (appel de configuration)
```

> **\[Piège\]** `Configure` a été appelé avec les valeurs de `Config` dans `Event Init` du GameInstance. Si l'utilisateur change l'URL dans le menu, il faut **reconfigurer** puis reconnecter. Voici la procédure complète :

```text
OnClicked (Btn_Connect)
  → GI → Set HttpUrl From Menu (Txt_HttpUrl.Text, Txt_WsUrl.Text)
  → GI → RequestConnect
```

Et dans le GameInstance, ajoute :

```text
// BP_PRISM_GameInstance
Function SetHttpUrl From Menu (HttpUrl, WsUrl)
  → LDK → Configure (Options modifiées)
        Options.HttpUrl = HttpUrl ; Options.WebSocketUrl = WsUrl
```

**`Btn_Prepare → OnClicked`** :

```text
OnClicked
  → Branch (IsValid (GI)) → faux : Return
  → Seed     = (Txt_Seed.Text → To Integer (Integer64))
  → Tps      = (Txt_Tps.Text → To Integer)
  → MaxTicks = (Txt_MaxTicks.Text → To Integer)
  → GI → RequestStart (Seed, Tps, MaxTicks)
```

> **\[Piège\]** La conversion texte → entier échoue silencieusement (renvoie 0). `Seed = 0` est une seed **valide** ! C'est pour ça que le guide logique dit « si la conversion échoue, prends les valeurs de `Config` ». Implémente-le :
> ```text
> OnClicked
>   → Split text → IsNumeric ? → si vrai : Convert ; si faux : Config.DefaultSeed
> ```

**`Btn_Logical → OnClicked`** :

```text
OnClicked
  → GI → SetRenderMode (Logical)
  → Open Level (by name) : Level/LDK_LogicalMap
        (le nom court suffit : "LDK_LogicalMap")
  → (effets : Transition — optionnel)
```

**`Btn_Virtual → OnClicked`** : identique, avec `SetRenderMode (Virtual)` et `Open Level (LDK_VirtualMap)`.

**`Btn_Quit → OnClicked`** :

```text
OnClicked
  → Quit Game
```

### 8.7 Les gestionnaires d'événements

**`EVT_Connected`** : `→ Set Txt_Error (Text) ← ""` puis `→ RefreshButtons`.
**`EVT_Disconnected`** : `→ Set Txt_Error (Text) ← "Déconnecté"` puis `→ RefreshButtons`.
**`EVT_ControlResult (Result)`** :

```text
EVT_ControlResult (Result)
  → Branch (Result.bOk) → faux :
       Set Txt_Error (Text) ← Result.Error
  → RefreshButtons
```

**`EVT_Error (Error)`** :

```text
EVT_Error (Error)
  → Set Txt_Error (Text) ← Error.Code + " : " + Error.Message
  → RefreshButtons
```

### 8.8 `MenuLevel` — Level Blueprint

Ouvre `MenuLevel` → **Blueprints → Open Level Blueprint** :

```text
Event BeginPlay
  → Create Widget (WBP_PRISM_MainMenu)
        Owning Player : (leave None) ; Class : WBP_PRISM_MainMenu
  → Add to Viewport
  → Set Input Mode (Game And UI)
  → Set Show Mouse Cursor (true)
```

> **\[Piège\]** `Set Input Mode (UI Only)` empêche de cliquer dans le viewport mais aussi d'utiliser les raccourcis clavier. `Game And UI` est le bon compromis. N'oublie pas `Show Mouse Cursor = true`, sinon le joueur ne peut pas cliquer sur les boutons.

### 8.9 Test de fin de V7

1. Lance le jeu depuis l'éditeur avec `MenuLevel` en map de démarrage.
2. Le menu s'affiche, les champs sont pré-remplis depuis `Config`.
3. **Connecter** → le mock affiche `WEBSOCKET connected` ; `Txt_State` change.
4. **Préparer** → le mock affiche `PREPARE`, puis `READY`.
5. **Représentation Logique** → le niveau s'ouvre, le monde logique s'affiche, la simulation tourne.
6. Retour au menu (arrete et relance) → **Monde Virtuel** → l'îlle s'affiche.

---

## 9. Jalon V8 — Végétation et style

### 9.1 Pourquoi des HISM, pas PCG

Le PCG runtime a des pièges en build packagé (partitioning, `Generate at Runtime`, sources de génération). Les **HISM** (comme le mode logique) sont :
- déjà utilisés et validés dans ce projet ;
- déterministes ;
- sans configuration de packaging.

**Décision** : la végétation du mode virtuel est posée par des **HISM** dont les positions sont calculées par le C++ (déterministe) ou par le Blueprint. Le PCG reste une **option** (V8.4).

### 9.2 Répartir la végétation

Crée un `BP_PRISM_VegetationScatter` (Actor) dans `World/`.

**Variables** :

| Variable | Type | Notes |
| :-- | :-- | :-- |
| `HISM_Tree` | `HierarchicalInstancedStaticMeshComponent` | Feuillage |
| `HISM_Rock` | `HierarchicalInstancedStaticMeshComponent` | Rochers |
| `HISM_Grass` | `HierarchicalInstancedStaticMeshComponent` | Herbe / buisson |
| `ScatterSeed` | `Integer` | `42` par défaut |
| `TreesPerKilo` | `Float` | Densité (ex. `0.6`) |
| `RocksPerKilo` | `Float` | Densité (ex. `1.2`) |
| `MinSlopeDeg` | `Float` | `38` : au-dessus, pas d'arbre |
| `MaxAltitudeUu` | `Float` | `6500` : au-dessus, pas d'arbre |

### 9.3 Calculer les positions — dans le C++ (recommandé)

Plutôt qu'une boucle Blueprint sur 224 000 uu de terrain (lourd, et `Get Random Reachable Point` n'a pas le sens ici), ajoute au `APrismLdkVirtualWorldActor` une fonction de dispersion. **C'est du C++ justifié** : bruit de Poisson + échantillonnage de la heightfield, c'est imbriqué en Blueprint.

> **Alternative sans C++** : utilise un `PCG Component` avec un graphe « Surface Sampler » + « Static Mesh Spawner ». C'est faisable, mais c'est le chemin fragile en packaging. Fais-le **après** que le reste marche, en V8.4.

Tant que le C++ n'expose pas la dispersion, utilise cette approche **Blueprint simple et robuste** :

```text
ScatterVegetation
  → Get Playable Bounds Unreal → Bounds
  → ForEach (0..N)  (N = 600 iterations, hardcodé ou exposé)
     → Random (Stream : ScatterSeed) → Random Float in Range (Bounds.Min.X, Bounds.Max.X) → X
     → Random Float in Range (Bounds.Min.Y, Bounds.Max.Y) → Y
     → VirtualWorld → Sample Height At (X, Y) → Z
     → Branch (Z < MinAltitude) → faux : continue
     → VirtualWorld → Sample Normal At (X, Y) → Normal
     → Slope = acos (Normal.Z) → Degrés
     → Branch (Slope > MaxSlopeDeg) → vrai : continue
     → Branch (Z < SeaLevel) → vrai : continue     (pas d'arbre dans l'eau)
     → Random → variante (mesh + rotation + scale)
     → Add Instance (HISM_Tree, Transform)
```

> **\[Piège\]** 600 itérations dans un graphe Blueprint au `BeginPlay` = un pic CPU. **Décale-le** : fais-le après le `Build` du monde virtuel, dans un cadre séparé (le renderer virtuel appelle le scatter une fois le terrain prêt). Ou passe à 300 itérations pour commencer.

### 9.4 L'eau : matériau et style

**Matériau d'eau** (M_Prism_Water) :
- Blend Mode : `Translucent`
- Normal map animée (scrolling)
- Couleur : bleu-vert, saturation moyenne (hand-painted)
- Fresnel pour les reflets de bord

**Le rendu des lacs** : l'acteur C++ crée un disque par ressource `Water`, au niveau `LakeLevel` (400 uu), avec un rayon irrégulier (jitter par bruit). Le terrain creuse la cuvette en dessous : **l'eau visible est exactement la partie immergée**.

> **\[Piège\]** Le disque d'eau est plus grand que la cuvette utile. C'est **voulu** : le terrain monte au-dessus du niveau de l'eau autour, ce qui masque l'excédent. Si tu réduis `LakeRadiusFraction`, le lac rétrécit.

### 9.5 Le style « hand-painted » : les matériaux, pas la géométrie

Le style vient d'un **matériau master** sur le terrain, avec des transitions douces :

| Couche | Critère de mélange | Rôle |
| :-- | :-- | :-- |
| Sable | altitude proche du niveau de la mer | Plages |
| Herbe | altitude médiane, pente faible | Prairie |
| Roche | pente forte (normal Z < 0.6) | Falaises |
| Roche sombre | altitude élevée | Sommets |
| Terre | bruit (masque peint) | Transitions |

**Construction du matériau** (dans l'éditeur, une seule fois) :
1. `M_Prism_Terrain` : `Material Domain = Surface`, `Shading Model = Default Lit`
2. `BaseColor` : `LandscapeLayerBlend`-like fait main, ou un simple `Height` → `Gradient` puis `Noise` → `Blend`
3. `Normal` : `Normal from Height` sur le bruit (relief peint)
4. `Roughness` : 0.7 à 0.95, modulé par le bruit

> **\[Note\]** UE 5.8 a l'**Auto-UV** : un mesh sans UV correcte peut quand même recevoir un matériau texturé. Mais le terrain généré a des UV propres (0..1) — ne change rien.

### 9.6 V8.4 (option) — PCG pour la végétation

Si tu veux aller plus loin avec PCG :
1. **Plugins → PCG Framework** : actif par défaut. **PCG Primitives** (Experimental) : à activer si tu veux les nœuds de mesh.
2. Dans `LDK_VirtualMap`, place un `PCG Volume` couvrant l'île.
3. Crée un `PCG Graph` : `Surface Sampler` → `Static Mesh Spawner`.
4. **PCG World Actor** (World Settings) : active **Generate at Runtime**.
5. **PCG Component** sur l'acteur : `Generation Trigger = Generate at Runtime`.

> **\[Piège\]** En build packagé, un PCG Graph ne s'exécute que si le **PCG Component** est `Generate at Runtime` **et** si le PCG World Actor est marqué pour la génération runtime. En PIE, ça marche souvent ; en package, non. Teste **toujours** en package avant de considérer cette voie comme acquise.

---

## 10. Jalon V9 — Tests finaux et packaging

### 10.1 Checklist fonctionnelle

- [ ] Le jeu démarre sur `MenuLevel`
- [ ] `Connecter` fonctionne (mock ou SYNE)
- [ ] `Préparer` fonctionne, la graine et les ticks sont pris en compte
- [ ] `Représentation Logique` → monde identique à l'ancien flux
- [ ] `Monde Virtuel` → île, montagnes, falaises, mer, lacs
- [ ] Les 3 lacs correspondent aux 3 ressources `water` du mock
- [ ] Les agents se déplacent dans les **deux** modes
- [ ] Pas de doublon d'agent (`Registry.GetAgentCount` = `Snapshot.AliveCount`)
- [ ] Le NavMesh est valide dans le mode virtuel (l'agent marche sur le relief)

### 10.2 Le NavMesh en mode virtuel

C'est le point le plus délicat.

**Réglages** :
- **Project Settings → Navigation Mesh** : `Runtime Generation = Dynamic` (déjà le cas si ça marche en mode logique).
- Le `NavMeshBoundsVolume` de `LDK_VirtualMap` doit couvrir l'île **entière** : `224 000 × 224 000` uu. Positionne-le à `(112 000, 112 000, 0)` avec une taille de `230 000`.

**Séquence** :
1. Le terrain est généré (collision activée : `bCreateCollision = true`).
2. La collision est **cuite** : `bUseAsyncCooking = false` → la cuisson est bloquante, elle est déjà faite quand `Build` rend la main.
3. Le NavMesh se reconstruit automatiquement (dynamic).
4. **Le premier agent peut ne pas trouver de NavMesh** pendant la frame de reconstruction.

**Solution** : ne déclenche le `Start` de la simulation qu'**après** que le NavMesh est prêt. Dans `EVT_RenderReady` (V6.4), ajoute un délai :

```text
EVT_RenderReady
  → Delay (0.5)
  → GI.RequestReady
```

> **\[Piège\]** Le `Delay` est un pansement, pas une garantie. Le vrai test : après le build, **force** une reconstruction du NavMesh. `Rebuild Navigation` (console `RebuildNavMesh`) puis vérifie que `Get Random Reachable Point In Radius` réussit pour un agent.
>
> **Solution robuste** : dans `BP_PRISM_AgentController.MoveToCell`, si `Project Point To Navigation` échoue, **réessaie** au tick suivant au lieu d'abandonner. Le guide logique (§5.1) dit déjà « si échec aussi : Print et STOP » — en mode virtuel, change-le en « réessaie ».

### 10.3 Le flux `Ready` / `Start` en mode virtuel

```
MenuLevel : Connecter → Préparer
   ↓ (Prepare)
MenuLevel : WorldDescription reçue
   ↓
MenuLevel : Open Level (LDK_VirtualMap)
   ↓
LDK_VirtualMap : WorldDirector.EVT_WorldInitialized
   ↓
   BuildWorldFromDescription → Renderer Virtuel → Build World (maillage)
   ↓ (signalé)
   ED_PRISM_RenderReady → Delay → GI.RequestReady
   ↓ (Ready accepté)
   GI.EVT_OnControlResult(action == "ready") → GI.RequestStartRun()
   ↓
   Snapshots → agents en mouvement
```

> **\[Piège\]** `RequestReady` et `RequestStartRun` sont déclenchés par le **GameInstance** (§2.4 Étape D du guide logique : `EVT_OnControlResult` avec `Action == "ready"`). Si tu déplaces le `RequestReady` dans le Director, **pense à retirer l'ancien appel** dans le GameInstance, sinon tu envoies deux `Ready` (SYNE répond `409 world_not_ready` ou `world_already_ready`).

### 10.4 Packaging Windows / Linux

**Ce qui doit marcher en package** (et qui ne marche pas toujours en PIE) :

| Élément | Piège connu |
| :-- | :-- |
| Génération du terrain | Aucune : c'est du C++ pur, pas d'asset externe. **Ça marche.** |
| Matériaux du terrain | Doivent être des `Material Interface` ** cooked**. Un matériau en `Material Instance` d'un master non-« Usage » ne se cook pas. Utilise des `Material` simples. |
| `ProceduralMeshComponent` | Le composant est dans un plugin moteur : il est cooké avec le projet. **Ça marche.** |
| PCG (si utilisé) | `PCG World Actor` doit permettre la génération runtime. **Risqué.** |
| Navigation dynamique | `Project Settings → Navigation System → Runtime Generation = Dynamic` doit être cooké. Vérifie dans le build. |

**Commande de packaging** :

```powershell
& "C:\Program Files\Epic Games\UE_5.8\Engine\Build\BatchFiles\RunUAT.bat" `
  BuildCookRun `
  -project="D:\LIVEX\prism\LDK\LDK.uproject" `
  -noP4 -platform=Win64 -clientconfig=Development `
  -build -cook -stage -pak -prereqs -archive
```

> **\[Piège\]** `-pak` pack les assets. Sans `-cook`, les niveaux et matériaux ne sont pas dans le package : le jeu démarre sur une carte vide.
>
> **Linux** : ajoute `-platform=Linux`. Il faut un toolchain Linux installé (ou une image de build UE fournie par Epic).

### 10.5 Test de fin de V9

1. Package Windows en `Development`.
2. Lance l'exécutable **hors éditeur**.
3. Le menu s'affiche, connecte au mock, prépare, ouvre le monde virtuel, l'île s'affiche, les agents bougent.
4. Fais de même pour la représentation logique.

---

## 11. Dépannage : symptômes fréquents

| Symptôme | Cause probable | À vérifier |
| :-- | :-- | :-- |
| Le menu ne s'affiche pas au lancement | `Game Default Map` non réglé | Project Settings → Maps & Modes |
| `Accessed None` sur `GI` dans le widget | `Cast` non fait ou widget créé trop tôt | `Event Construct` du widget |
| Les boutons restent grisés | `RefreshButtons` jamais appelé | Appelle-le après chaque `ED_PRISM_*` |
| `SelectedRenderMode` toujours `Logical` | Le menu ne pose pas la variable, ou le `Open Level` avant le `Set` | `SetRenderMode` **avant** `Open Level` |
| L'îlle est plate | `GridResolution` trop bas, ou `ObstacleHeightFactor = 0` | Réglages du Renderer virtuel |
| Les montagnes ne sont pas au bon endroit | Le rayon d'influence n'utilise pas `k` | `ObstacleInfluences` calculé avec `TileSizeUu / CellSize` |
| Pas de lacs | Les ressources `Water` n'ont pas de position, ou `bPositionIsCellIndex` mal traité | `PCGData.WaterResourceCells` est vide ? |
| Les lacs flottent au-dessus du terrain | `LakeLevel` trop haut par rapport au terrain local | Baisse `LakeLevel` ou monte `IslandHeight` |
| Aucun agent ne bouge en mode virtuel | NavMesh pas reconstruit, ou pas encore prêt | §10.2 ; `Delay` avant `Ready` ; `Project Point To Navigation` qui échoue |
| `409 world_not_ready` | `Ready` envoyé deux fois, ou avant la construction | Un seul `RequestReady` (§10.3) |
| `401 unauthorized` | Jeton de session absent | §1.1 du guide logique |
| L'ile est invisible en package | Matériau non cooké, ou `ProceduralMeshComponent` absent | `-cook` dans le RunUAT ; matériau en `Material` simple |
| Le PCG ne génère rien en package | `Generate at Runtime` non activé sur le composant | §9.6 |

---

## 12. Ce qui a changé par rapport au guide logique — récapitulatif

| Élément | Avant | Après |
| :-- | :-- | :-- |
| Map de démarrage | `LDK_BaseMap` | `MenuLevel` |
| Map logique | `LDK_BaseMap` | `LDK_LogicalMap` |
| Map virtuelle | (n'existait pas) | `LDK_VirtualMap` |
| Rendu | dans le `WorldDirector` | dans un `BPI_PRISM_Renderer` |
| Mode choisi | — | `GI.SelectedRenderMode` |
| Terrain | `BP_PRISM_TileField` (HISM) | `BP_PRISM_TileField` **ou** `APrismLdkVirtualWorldActor` |
| Eau | aucune | mer + lacs (ressources `Water`) |
| C++ | transport + projection + registre | + `UPrismLdkPCGData`, `APrismLdkVirtualWorldActor`, helpers |

**Rien de ce qui est décrit dans « PRISM — Guide pas à pas de la partie Blueprint » n'est devenu faux.** Le guide logique reste la référence pour la session, le registre, les agents et l'interface. Ce guide ajoute le menu et le mode virtuel par-dessus.

---

## 13. Par où commencer concrètement

1. **V0** : renommer `LDK_BaseMap` → `LDK_LogicalMap`, créer `MenuLevel` et `LDK_VirtualMap`, les deux GameModes, et les réglages Project Settings. *(15 min)*
2. **V1** : l'énum `EPrismRenderMode` et `SelectedRenderMode` dans le GameInstance. *(5 min)*
3. **V2 → V3** : `BPI_PRISM_Renderer` puis `BP_PRISM_RendererBase`. *(20 min)*
4. **V4** : `BP_PRISM_LogicalRenderer` — extraction du TileField, **sans changement de comportement**. *(30 min)*
5. **V6** : brancher le Director (spawn du renderer, `Initialize`, `Build`, `Ready` conditionné). *(45 min)*
6. **V7** : `WBP_PRISM_MainMenu` + `MenuLevel`. *(1 h)*
7. **V5** : `BP_PRISM_VirtualRenderer` + `BP_PRISM_VirtualWorld` (matériaux). *(1 h)*
8. **V8** : végétation, style. *(1-2 h)*
9. **V9** : NavMesh, packaging, tests. *(1 h)*

Quand V0, V1 et V4 sont verts, le reste suit le même schéma : un élément à la fois, un test à la fin de chaque jalon. Si un graphe te bloque, dis-moi lequel (et ce que tu vois dans `LogPrismLdk`) et on le reprend nœud par nœud.
