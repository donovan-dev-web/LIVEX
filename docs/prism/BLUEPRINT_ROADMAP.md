# BLUEPRINT_ROADMAP.md — Plan de réalisation détaillé de la partie Blueprint de PRISM

**Composant** : PRISM
**Statut** : [DRAFT] — plan de travail temporaire (à replier dans
`PRISM_UNREAL_IMPLEMENTATION.md` une fois la partie Blueprint réalisée)
**Dernière mise à jour** : 14 octobre 2026
**Dépend de** : [`ARCHITECTURE.md`](ARCHITECTURE.md),
[`PRISM_UNREAL_IMPLEMENTATION.md`](PRISM_UNREAL_IMPLEMENTATION.md),
[`SCENE_SPEC.md`](SCENE_SPEC.md), [`RENDERING_SPEC.md`](RENDERING_SPEC.md),
[`VISUALIZATION_SPEC.md`](VISUALIZATION_SPEC.md),
[`UX_INTERACTION.md`](UX_INTERACTION.md),
[`SCALE_AND_CADENCE_SPEC.md`](SCALE_AND_CADENCE_SPEC.md),
[`ASSETS_CONVENTIONS.md`](ASSETS_CONVENTIONS.md),
[`TRANSPORT_API.md`](TRANSPORT_API.md), [`TESTING.md`](TESTING.md),
[`../syne/API_CONTRACTS.md`](../syne/API_CONTRACTS.md),
[`../../docs/NAMING_CONVENTIONS.md`](../NAMING_CONVENTIONS.md)

---

## 1. Objet et périmètre

Ce document est le **plan de réalisation détaillé de la partie Blueprint** du
projet Unreal PRISM. Il énumère **chaque élément à créer**, son **contenu**
(classe parente, variables, fonctions, événements, dispatchers, interfaces,
câblage), l'**architecture de niveaux**, le **pipeline de données**, la
**conversion d'espace** et la **feuille de route jalon par jalon**.

Le C++ de PRISM-LDK (`PrismLdk`) porte la **projection d'espace** (§5.4,
`UPrismLdkProjection`), le **registre de monde** (§5.5,
`UPrismLdkWorldRegistryBase`) et la **surface de service** exigée par le
Launcher (§11). Il **compile** sur le moteur du dépôt :
`Build.bat LDKEditor Win64 Development` sous UE 5.8.3 → `Result: Succeeded`,
0 avertissement, et les trois Automation Specs (`PrismLdk.Projection`,
`PrismLdk.WorldRegistry`, `PrismLdk.Service`) sont vertes (§10, B0). Tout ce qui
transforme les données SYNE en **représentation 3D et interaction** est à
construire côté Unreal : c'est l'objet de ce plan.

**Méthode de rédaction (règle du document).** Aucune capacité d'Unreal n'est
affirmée ici sans source : soit un fait **vérifié** (en-tête ou source du moteur
avec son chemin, source du plugin, fichier de contrat, test), soit une mention
**[À VÉRIFIER]** accompagnée du geste de vérification. Les affirmations moteur
sont réunies en §13. Une phrase sans source est un défaut à corriger, pas une
opinion.

Frontière non négociable : **le plugin est un adaptateur**. Il expose les types,
les fonctions et les événements Blueprint **et la projection mathématique
déterministe** (§5.4) ; il ne crée ni tuile, ni acteur, ni collision, ni widget,
ni décision de simulation. **SYNE (et le mock)
restent l'unique source de l'état simulé, y compris des obstacles** ; PRISM les
**affiche**, il ne les décide pas.

## 2. Flux de bout en bout (détaillé)

### 2.1 Séquence nominale

```text
[Launcher]  détecte SYNE + PRISM (manifestes component.json)
[Launcher]  démarre SYNE --simulation prism --control-port Pc --observe-port Po,
            sans --autostart (le moteur attend), avec jeton LIVEX_SESSION_TOKEN
[Launcher]  démarre PRISM (binaire Unreal empaqueté) avec --instance-id,
            --control-port, --work-dir, --log-dir, --correlation-id, --data-port
[PRISM]     lit ses arguments : sous Launcher il reçoit SA surface de service,
            pas les ports de SYNE (voir §2.2.2 et §11.1)
[PRISM/BP]  Configure(Options) → Connect()
[SYNE]      accepte la WebSocket
[PRISM/BP]  OnConnected → Prepare(Seed, TicksPerSecond)
[SYNE]      prépare le monde et émet world_initialized sur la WebSocket
[PRISM/BP]  OnWorldInitialized → BuildWorldFromDescription → Ready(World.Version)
[PRISM/BP]  OnControlResult(bOk=true) → Start(Seed, MaxTicks)
[SYNE]      émet un snapshot par tick (6/s en profil prism)
[PRISM/BP]  OnSnapshot → met à jour la scène 3D (agents, obstacles, stocks, …)
[PRISM/BP]  OnWorldDelta / OnSyneEvent → notifications et effets ponctuels
[SYNE]      à MaxTicks → état finished
[PRISM/BP]  Reset / Stop selon l'action utilisateur
```

### 2.2 Précisions structurantes

1. **Le Launcher démarre SYNE, pas le run.** En mode piloté, le moteur tourne
   **sans `--autostart`** avec `--simulation prism` ; c'est **PRISM** qui pilote
   `Prepare → Ready → Start`. Référence : `launcher/Launcher.Tests.EndToEnd/PrismIntegrationEndToEndTests.cs`.
2. **Ports : ce que PRISM reçoit réellement sous Launcher.** Hors Launcher, le
   plugin utilise ses défauts — HTTP `http://127.0.0.1:5181` et WebSocket
   `ws://127.0.0.1:5180` (`TRANSPORT_API.md` §1 ; côté SYNE `--serve-port` 5181
   et `--observe-port` 5180). **Sous Launcher, rien ne transmet les ports de
   SYNE à PRISM** : le test de bout en bout vérifie que PRISM reçoit exactement
   `--instance-id`, `--control-port`, `--data-port`, `--headless` et **aucun**
   argument de lot SYNE (assertions `Contains`/`DoesNotContain` de
   `PrismUnlockEndToEndTests`,
   `launcher/Launcher.Tests.EndToEnd/PrismIntegrationEndToEndTests.cs`).
   `--control-port` est le port de contrôle **de PRISM** et `--data-port` celui
   de l'endpoint `ws` que **PRISM** déclare dans son manifeste — pas ceux de
   SYNE.
   - **Conséquence** : en lancement supervisé, `Configure` ne peut pas dériver
     les URLs de SYNE des seuls arguments reçus. La posture par défaut reste
     donc le lancement **hors Launcher** (5180/5181) tant que le point n'est pas
     tranché ; c'est un **prérequis de B7** (§11.1).
   - **Options** (à choisir explicitement, jamais implicitement) : (a) étendre
     le contrat d'intégration avec un argument d'endpoint transmis au composant
     dépendant (`--ws-url`, comme le fait déjà `launcher/Stub.Prism/Program.cs`, ou
     `--control-url`) ; (b) une variable d'environnement
     `LIVEX_SYNE_OBSERVE_URL` / `LIVEX_SYNE_CONTROL_URL` ; (c) déclarer dans le
     manifeste PRISM des endpoints dont le `launchArgument` porte l'URL du
     moteur. Dans tous les cas : **aucune URL SYNE en dur dans le Blueprint**, et
     `Configure` reste appelé avant `Connect`.
3. **PRISM expose sa propre surface de service** (`/health/ready`, `/info`,
   `/metrics`, `/control/shutdown` avec jeton) — un build Unreal ne la fournit
   pas par défaut. C'est la responsabilité du **projet PRISM**, pas du plugin.
4. **Verrou Immersion (ADR-006).** Le mode reste verrouillé tant que le
   manifeste PRISM n'est pas conforme (`immersion` + `snapshotStream` +
   `renderCadence` + `control`) **et** qu'un build réel fonctionne.
5. **Le mock n'est pas une preuve scientifique.** Il valide transport et
   graphes ; il ne prouve ni décisions ni trajectoires (mort non simulée,
   perception absente, social décoratif, pas d'A*).

## 3. Pré-requis et conventions

- **Unreal Engine 5.8.3** — vérifié : `Engine/Build/Build.version` porte
  `MajorVersion: 5`, `MinorVersion: 8`, `PatchVersion: 3`, branche
  `++UE5+Release-5.8`.
- Plugin `PrismLdk` **activé** dans **Edit → Plugins**. Dépendances réelles du
  module (`Source/PrismLdk/PrismLdk.Build.cs`) : `Core`, `CoreUObject`,
  `Engine`, `HTTP`, `Json`, `JsonUtilities`, `WebSockets`, `HTTPServer`. La
  casse du module moteur est **`HTTPServer`** (répertoire
  `Engine/Source/Runtime/Online/HTTPServer`) : l'écrire `HttpServer` déclenche un
  avertissement UBT et échoue sur un système de fichiers sensible à la casse.
- **Enhanced Input** est disponible sans ajout au projet :
  `Engine/Plugins/EnhancedInput/EnhancedInput.uplugin` porte
  `"EnabledByDefault": true`.
- **Nommage** : identifiants anglais, commentaires français, indentation
  tabulation (style Epic). `../NAMING_CONVENTIONS.md` §8 couvre le **C++ du
  plugin** (fichier = nom de classe ; préfixes `U`, `A`, `F`, `E`, `I`, `T`,
  `b`) — il ne définit **aucun** préfixe d'asset Blueprint. Les préfixes
  employés par ce plan (`BP_`, `WBP_`, `DA_`, `BPI_`, `IA_`, `A_`/`AM_`, `L_`
  pour un niveau, `DL_` pour une Data Layer) sont donc une **convention PRISM
  décidée ici**, à reporter dans `NAMING_CONVENTIONS.md` si elle est validée. Le
  préfixe de sous-niveau `SL_` disparaît avec l'abandon des sous-niveaux
  classiques (§4).
- **Deux projets Unreal, deux rôles** (décision §9) :
  - **`prism/LDK/LDK.uproject`** — projet Unreal de **création et de
    configuration du plugin**. Il porte des assets **génériques** : aucun mesh,
    aucune texture ni animation importée. C'est le projet courant.
  - **Projet PRISM final** (à créer) — second projet Unreal qui **intègre le
    plugin `PrismLdk`** et importe dans `/Game` les meshes, textures,
    animations et matériaux définitifs.
- **Emplacement des assets** : la partie Blueprint (GameInstance, subsystem,
  director, entités, widgets, niveau) vit dans le **Content du plugin**
  (`Plugins/PrismLdk/Content/`), puisque c'est le travail du LDK. Le plugin
  déclare déjà `"CanContainContent": true` (`PrismLdk.uplugin`). Conséquence à
  retenir : un contenu de plugin est cuit **avec le projet qui l'active** — la
  présence des assets se vérifie donc sur un build **empaqueté**, pas seulement
  en PIE.
- **Règle de référence** : un asset du plugin ne référence **que** du contenu du
  plugin. Le projet PRISM ajoute sa couche visuelle par **dérivation**
  (Blueprint enfant, `DA_PRISM_*`, paramètre de matériau) et par ses propres
  niveaux, jamais en modifiant un asset livré par le plugin — sinon une mise à
  jour du plugin écrase ou fait diverger le produit.
- **État actuel du Content** (`Plugins/PrismLdk/Content/`, relevé du
  14 octobre 2026 — c'est le seul état vérifiable, un `.uasset` n'étant pas
  relisible en diff) :

  | Chemin | État |
  | :-- | :-- |
  | `Core/BP_PRISM_GameInstance.uasset` | créé (B1) |
  | `Core/DA_PRISM_SessionConfig.uasset` | créé (B1) ; champs à confronter à §6.1.3 |
  | `GameInstance_LDK.uasset`, `ldk_GameMode.uasset` | restes de l'amorçage — à supprimer quand `BP_PRISM_GameInstance` / `BP_PRISM_GameMode` les remplacent |
  | `LDK_BaseMap.umap` (racine) **et** `Level/LDK_BaseMap.umap` | **deux copies** : garder celle de `Level/`, supprimer celle de la racine en B3 (sinon la carte ouverte dépend du chemin utilisé) |

  Il n'existe **aucun** `Content/Math/` ni asset `BML_` : le point de contrôle de
  §6.1.4 est **déjà satisfait**.
- **Arborescence de contenu cible** (dans le plugin, à créer) :

```text
Plugins/PrismLdk/Content/
├── Core/          BP_PRISM_GameInstance, DA_PRISM_SessionConfig (le registre de monde est en C++)
├── Framework/     BP_PRISM_GameMode, BP_PRISM_PlayerController, BP_PRISM_ObserverPawn, BP_PRISM_CameraRig
├── World/         BP_PRISM_WorldDirector, BP_PRISM_TileField, BP_PRISM_Obstacle, BP_PRISM_ResourceMarker
├── Entities/      BP_PRISM_Agent, BP_PRISM_AgentController, BPI_PRISM_EntityView, A_/AM_ (anim)
├── UI/            WBP_PRISM_ConnectionPanel, WBP_PRISM_HUD, WBP_PRISM_AgentInspector,
│                  WBP_PRISM_EventLog, WBP_PRISM_Diagnostics
└── Level/         LDK_BaseMap (une seule copie)
```

> Les sous-niveaux `SL_PRISM_*` du plan précédent sont **abandonnés** : un monde
> World Partition s'organise par cellules streamées et **Data Layers**, pas par
> sous-niveaux classiques (§4).

> **Aucun dossier `Math/`.** La projection SYNE → Unreal est du **C++ du
> plugin** (§5.4) : ce n'est pas un asset.

### 3.1 Notation des types

Les fiches de ce document décrivent des éléments **Blueprint** : elles emploient
les libellés que l'éditeur affiche. Correspondance avec les types C++ du
plugin :

| Type C++ (plugin, contrats) | Libellé Blueprint à utiliser |
| :-- | :-- |
| `double` | **Float** — il n'existe **pas** de type Blueprint nommé « double » ; la double précision se porte par le pin (`float` en C++ = simple, `double` = double) |
| `float` | Float (simple précision) |
| `int64` | **Integer64** |
| `int32` | Integer |
| `bool` | Boolean |
| `FString` | String |
| `FName` | Name |
| `TArray<T>` | Array |
| `TMap<K, V>` | Map |
| `FVector` | Vector |
| `FIntPoint` | **Int Point** |
| `AActor` | Actor (Object Reference) |
| `UMaterialInterface` | Material Interface (Object Reference) |
| `FPrismSyne*` | struct Blueprint homonyme |

**Précision numérique** : un `double` de `UPROPERTY` C++ est exposé à Blueprint
en **double précision** (LWC). La précision est perdue dès qu'un pin est relié à
une fonction qui attend un `float` : garder la double précision sur tous les
calculs mettant en jeu `k`. **[À VÉRIFIER]** le libellé exact du sélecteur de
type de pin dans l'éditeur 5.8 (le repère sûr est le C++ : un paramètre `double`
reste en double précision) ; ne pas s'appuyer sur le nom affiché pour choisir un
type.

## 4. Architecture de niveaux (une carte, World Partition à décider par mesure)

**Décision** : **une seule carte persistante** pour tout le monde PRISM. Le
contenu ne se répartit **pas** en sous-niveaux classiques.

- **Pas de sous-niveaux.** Un monde World Partition s'organise par **cellules
  streamées** et **Data Layers** ; le panneau *Levels* et les sous-niveaux sont
  présentés par Epic comme le mode des projets hérités ou hors jeu, remplacés
  par WP. Les noms `SL_PRISM_*` du plan précédent sont donc **abandonnés** :
  l'organisation logique se fait par **Data Layers** (`DL_PRISM_*`) et, pour du
  contenu réutilisable, par **Level Instances**.
- **Niveau persistant** : la carte courante (`Level/LDK_BaseMap` ; carte propre
  au projet PRISM final) — `BP_PRISM_GameMode`, `BP_PRISM_WorldDirector`,
  caméra, lumières, ciel. Toujours chargée.
- **Contenu auteur vs contenu généré** :

  | Nature | Emplacement | Chargement |
  | :-- | :-- | :-- |
  | Carte, cadre de jeu, caméra, lumières | racine de la carte | toujours chargé |
  | Matériaux et meshes **génériques** du LDK | carte ou Level Instance, rangés par Data Layer `DL_PRISM_*` | selon la couche et la cellule WP |
  | Tuiles, obstacles, marqueurs, agents (**générés** depuis SYNE) | spawnés par `BP_PRISM_WorldDirector` dans la carte | voir ci-dessous |

- **Valeurs par défaut réelles de World Partition sur le moteur 5.8** (lues dans
  la source du moteur, pas dans une page de documentation) :
  `URuntimePartition::SetDefaultValues()` pose `LoadingRange = 25600`
  (`Engine/Source/Runtime/Engine/Private/WorldPartition/RuntimeHashSet/RuntimePartition.cpp:27`)
  et `URuntimePartitionLHGrid::SetDefaultValues()` pose
  `CellSize = LoadingRange / 2`
  (`…/RuntimePartitionLHGrid.cpp:178`) → **CellSize = 12 800 uu (128 m)**,
  **LoadingRange = 25 600 uu (256 m)**.
  - **La cellule WP par défaut n'est donc pas 3 200 uu** : l'affirmation « une
    case SYNE = une cellule WP » du plan précédent était **fausse** (elle
    supposait 3 200 uu par défaut).
  - Pour obtenir **1 case SYNE (32 m × `k` = 3 200 uu) = 1 cellule WP**, la
    grille de runtime doit être **posée explicitement** (World Settings → World
    Partition → Runtime Grid) : `CellSize = 3200` et un `LoadingRange` multiple
    de 3 200 (la valeur par défaut `25600` = 8 cellules de rayon). Le HLOD de
    grille double la cellule à chaque niveau (`CellSize = parent × 2`), soit
    6 400 uu au premier niveau HLOD.
- **Entités générées et streaming.** Un acteur créé au runtime (`SpawnActor`)
  vit dans le niveau persistant : il est **chargé en permanence**, sauf mécanisme
  WP explicite (Level Instance, Data Layer runtime). C'est acceptable ici parce
  que la population est bornée (profil B : monde de 2 240 m, 70 × 70 cases,
  quelques dizaines d'agents), mais c'est une **limite assumée**, pas une
  propriété de WP — à mesurer en B7 avant de la garder.
- **Alternative sans World Partition (à considérer sérieusement)** : carte sans
  WP, terrain en `HierarchicalInstancedStaticMesh` (une instance par case, avec
  culling spatial natif) et entités spawnées. Le contenu étant **procédural**,
  WP n'apporte rien si le budget mémoire tient sans lui ; il ajoute en revanche
  cellules, HLOD et contraintes de spawn à maîtriser.
  - **Décision en B3, sur mesure** : activer WP seulement si le relevé
    (mémoire résidente et temps de chargement de la carte complète) l'exige.
    Le relevé est écrit dans l'artefact de jalon, pas supposé.
- **Échelle et cadence** (ADR-003, `SCALE_AND_CADENCE_SPEC.md`) : `k = 100 uu`
  par unité SYNE (1 unité = 1 m), tuile `CellSize × k`. Profil retenu
  (`configs/simulation/prism.json`, vérifié) : monde `2240 × 2240`,
  `worldCellSize = 32`, `ticksPerSecond = 6`, `simulatedSecondsPerTick = 5`.
  Le monde fait donc 2240 m × 2240 m et **70 × 70 = 4 900 cases** de 32 m.

## 5. Surface du plugin PRISM-LDK (à consommer)

### 5.1 Fonctions (`UPrismLdkSubsystem`)

**Accès** : nœud de la bibliothèque du plugin affiché **`SYNE`** (titre compact)
et dont la fonction est `Get Prism Ldk Subsystem`
(`Source/PrismLdk/Public/PrismLdkBlueprintLibrary.h`), ou nœud **Get Game
Instance Subsystem** avec la classe `PrismLdkSubsystem`. Le titre affiché et le
nom de la fonction diffèrent : chercher `SYNE` dans le menu de contexte.

| Fonction | Signature | Nature | Effet |
| :-- | :-- | :-- | :-- |
| `Configure` | `(FPrismSyneConnectionOptions InOptions)` | Callable | Mémorise URL HTTP/WebSocket, reconnexion, délai |
| `Connect` | `() → bool` | Callable | Ouvre la WebSocket (ne se déclenche jamais seul) |
| `Disconnect` | `()` | Callable | Ferme et annule reconnexion/requêtes |
| `Prepare` | `(int64 Seed, int32 TicksPerSecond = 10)` | Callable | `POST /api/control/prepare` |
| `Ready` | `(FString WorldVersion = "1.1")` | Callable | `POST /api/control/ready` |
| `Start` | `(int64 Seed, int32 MaxTicks = 400)` | Callable | `POST /api/control/start` |
| `Pause` / `Resume` / `Stop` | `()` | Callable | `POST /api/control/{pause,resume,stop}` |
| `Reset` | `(int64 Seed, int32 MaxTicks = 400)` | Callable | `POST /api/control/reset` |
| `RequestStatus` | `()` | Callable | `GET /api/control/status` |
| `GetConnectionState` | `() → EPrismSyneConnectionState` | Pure | État de connexion |
| `GetStatus` | `() → FPrismSyneStatus` | Pure | Dernier statut connu |
| `GetLatestSnapshot` | `(FPrismSyneWorldSnapshot& Out) → bool` | Pure | Dernier snapshot complet |
| `GetWorldDescription` | `(FPrismSyneWorldDescription& Out) → bool` | Pure | Dernière description de monde |

### 5.2 Dispatchers (`BlueprintAssignable`)

| Dispatcher | Paramètre | Sens |
| :-- | :-- | :-- |
| `OnConnected` | — | WebSocket établie |
| `OnDisconnected` | — | WebSocket fermée (propre ou subie) |
| `OnWorldInitialized` | `FPrismSyneWorldDescription` | Monde préparé, avant tout snapshot |
| `OnSnapshot` | `FPrismSyneWorldSnapshot` | État dynamique complet d'un tick |
| `OnWorldDelta` | `FPrismSyneWorldDelta` | Mutations d'obstacles du tick |
| `OnSyneEvent` | `FPrismSyneEvent` | Événement typé (décision, action, comm., groupe, monde) |
| `OnControlResult` | `FPrismSyneControlResult` | Résultat asynchrone d'une commande HTTP |
| `OnError` | `FPrismSyneError` | Erreur réseau/API/JSON |

### 5.3 Types exposés (champs réellement disponibles)

- `FPrismSyneConnectionOptions` : `HttpUrl` (`http://127.0.0.1:5181`),
  `WebSocketUrl` (`ws://127.0.0.1:5180`), `bAutoReconnect` (`true`),
  `ReconnectDelaySeconds` (`2.0`).
- `FPrismSyneWorldDescription` : `Version`, `Seed`, `Width`, `Height`,
  `CellSize`, `TicksPerSecond`, `SimulatedSecondsPerTick`, `MetersPerUnit`,
  `CellCountX`, `CellCountY`, `Agents[]`, `Cells[]`, `Obstacles[]`,
  `Resources[]`, `Regions[]`.
- `FPrismSyneAgentSnapshot` : `Id`, `Species`, `CurrentActionEnum`,
  `CurrentIntentionEnum`, `Position`, `Energy`, `Hunger`, `Thirst`, `Fatigue`,
  `CurrentIntention`, `CurrentAction`, `Traits` (map string→double), `Beliefs[]`,
  `Goals[]`, `Trust[]`, `MemoryCount`.
- `FPrismSyneWorldSnapshot` : `Version`, `EngineVersion`, `RunId`, `Seed`,
  `Tick`, `SimulatedTimeMinutes`, `AliveCount`, `Season`, `SeasonEnum`,
  `SeasonIndex`, `Agents[]`, `Resources[]`, `Obstacles[]`, `Groups[]`,
  `Territories[]`, `Books[]`, `WorldChanges[]`, `Actions[]`.
- `FPrismSyneGroup` : `GroupId`, `Members[]`, `Size`, `LeaderId`, `bHasLeader`,
  `BornTick`, `Cohesion`, `Consensus`, `Decision`.
- `FPrismSyneTerritory` : `Id`, `Position`, `Radius`, `MemberCount`, `Members[]`.
- `FPrismSyneBook` : `Id`, `AuthorId`, `Title`, `Content`, `WrittenTick`,
  `ReadCount`, `Readers[]`.
- `FPrismSyneWorldAction` : `AgentId`, `Action`, `ActionEnum`, `Outcome`,
  `OutcomeEnum`, `Cause`, `EnergyDelta`, `HungerDelta`, `ThirstDelta`,
  `FatigueDelta`, `Reserve`, `ReserveConsumed`, `bHasReserve`.
- `FPrismSyneStatus` : `State`, `RunId`, `Tick`, `AliveCount`, `TicksPerSecond`,
  `Seed`, `MaxTicks`, `bWorldPrepared`, `WorldVersion`, `bWorldReadyAcknowledged`.
- `FPrismSyneControlResult` : `bOk`, `Action`, `Error`, `Status`.
- `FPrismSyneError` : `HttpCode`, `Code`, `Message`, `RawPayload`.
- `UENUM` : `EPrismSyneConnectionState`, `EPrismSyneRunState`,
  `EPrismSyneMessageType`, `EPrismSyneTerrainType`, `EPrismSyneResourceType`,
  `EPrismSyneSeason`, `EPrismSyneAction`, `EPrismSyneActionOutcome`.

> Les `UENUM` sont définis en C++ : ajouter une valeur officielle du contrat
> demande une recompilation du plugin. Toujours conserver la valeur texte
> d'origine et traiter `Unknown`.

### 5.4 Projection d'espace (`UPrismLdkProjection`, C++)

Décision §9 : la projection est du **code du plugin**, exposé en
`BlueprintPure`. Ce n'est **ni un asset, ni une bibliothèque de macros**. Aucune
constante de produit n'y figure : `K` et `CellSize` sont **toujours** des
paramètres fournis par l'appelant (`GetStats().K`, `GetStats().CellSize`). Source :
`Source/PrismLdk/Public/PrismLdkProjection.h`.

| Fonction Blueprint | Entrées | Sortie |
| :-- | :-- | :-- |
| `Syne To Unreal` | `Position` (struct `PrismSyneVector2D`), `K` (double) | Vector |
| `Unreal To Syne` | `Location` (Vector), `K` | struct `PrismSyneVector2D` |
| `Syne To Cell` | `Position`, `CellSize`, `CellCountX`, `CellCountY` | Int Point |
| `Cell To Tile Origin` | `Cell`, `CellSize`, `K` | Vector |
| `Cell To Tile Center` | `Cell`, `CellSize`, `K` | Vector |
| `Syne Radius To Unreal` | `Radius`, `K` | double précision |
| `Max Walk Speed For Speed` | `Speed`, `K`, `TicksPerSecond` | double précision |
| `Tile Size From Cell Size` | `CellSize`, `K` | double précision |

Comportements **verrouillés par test** (Automation Spec `PrismLdk.Projection`,
`Source/PrismLdk/Private/Tests/PrismLdkProjectionSpec.cpp`) :

- `K`, `CellSize` ou `TicksPerSecond` invalides (≤ 0) → sortie **nulle**, jamais
  d'infini ni de NaN ;
- position hors-bornes → **cellule de bord** ;
- `SyneToCell` tronque vers le bas (`32` → cellule `1`) ;
- inverse exact : `UnrealToSyne(SyneToUnreal(P, k), k) = P`.

### 5.5 Registre de monde (`UPrismLdkWorldRegistryBase`, C++)

Sous-système de durée de vie **GameInstance**, **concret** (`Blueprintable`
seulement) : le moteur l'instancie avec le module, donc l'instance ne dépend
d'aucun asset chargé. Aucun Blueprint enfant n'est attendu — le montage
`Abstract` + `BP_PRISM_WorldSubsystem` a été retiré, voir §11.4. Accès : nœud
**`PRISM Registry`** (`Get Prism Ldk World Registry`, catégorie `PRISM|Registry`)
ou **Get Game Instance Subsystem**. Source :
`Source/PrismLdk/Public/PrismLdkWorldRegistry.h`.

| Fonction | Signature | Nature | Effet |
| :-- | :-- | :-- | :-- |
| `SetTileSizeUu` | `(double TileSizeUu) → bool` | Callable | Mémorise la taille de cellule produit (`DA_PRISM_SessionConfig.TileSizeUu`) ; `false` si ≤ 0 ou non finie |
| `SetStatsFromDescription` | `(FPrismSyneWorldDescription World)` | Callable | **Unique** dérivation de `K = TileSizeUu / CellSize` |
| `GetStats` | `() → FPrismLdkWorldStats` | Pure | `WorldVersion`, `TileSizeUu`, `CellSize`, `K`, `MetersPerUnit`, `CellCountX`, `CellCountY`, `TicksPerSecond`, `SimulatedSecondsPerTick`, `bScaleValid` |
| `RegisterAgent` / `UnregisterAgent` | `(int64 Id, AActor* Actor) → bool` / `(int64 Id) → bool` | Callable | Registre des agents |
| `GetAgentActor` / `HasAgent` / `GetAgentCount` / `GetAgentIds` | `(int64 Id)` … | Pure | Lecture ; `GetAgentIds` trié croissant |
| `RegisterObstacle` / `UnregisterObstacle` | `(FString Id, AActor* Actor) → bool` / `(FString Id) → bool` | Callable | Registre des obstacles |
| `GetObstacleActor` / `HasObstacle` / `GetObstacleCount` / `GetObstacleIds` | `(FString Id)` … | Pure | Lecture ; `GetObstacleIds` trié |
| `AddResourceMarker` / `RemoveResourceMarker` / `GetResourceMarkers` | `(AActor* Actor)` … | Callable / Pure | Marqueurs de ressource |
| `ClearAll` | `()` | Callable | Nouveau run : vide les registres, **conserve** `TileSizeUu` |

Faits **verrouillés par test** (Automation Spec `PrismLdk.WorldRegistry`) :
remplacement d'agent sans doublon ; refus d'un acteur invalide, d'un identifiant
d'obstacle vide, d'un `TileSizeUu` ≤ 0 ou NaN ; `K` identique quel que soit
l'ordre de `SetTileSizeUu` / `SetStatsFromDescription` ; `K = 0` et
`bScaleValid = false` si `CellSize ≤ 0` ; acteur détruit → absent du registre
(référence faible, jamais de pointeur pendant) ; `ClearAll` conserve
`TileSizeUu` et remet l'échelle dérivée à zéro.

**Pourquoi une classe C++ intermédiaire** — fait moteur, pas préférence de
style : `UGameInstanceSubsystem`, `UWorldSubsystem` et `ULocalPlayerSubsystem`
sont déclarées `UCLASS(Abstract, …, MinimalAPI)` **sans `Blueprintable`**
(`Engine/Source/Runtime/Engine/Public/Subsystems/{GameInstanceSubsystem,WorldSubsystem,LocalPlayerSubsystem}.h`,
moteur 5.8). Le sélecteur de classe parente ne propose que les classes
`Blueprintable` : un Blueprint ne peut donc pas dériver directement d'une base de
sous-système.

## 6. Détail des éléments Blueprint à créer

Chaque fiche donne : **type/classe parente**, **variables**, **fonctions**,
**événements/dispatchers**, **interfaces**, **câblage**.

### 6.1 Fondations

#### 6.1.1 `BP_PRISM_GameInstance`
- **Type** : Blueprint GameInstance.
- **Variables** :
  - `LDK : UPrismLdkSubsystem` (object ref) — référence au subsystem ;
  - `Session : F_PrismSessionState` (struct : `Seed` **Integer64**,
    `TicksPerSecond` **Integer**, `MaxTicks` **Integer**, `RunId` **String**,
    `WorldVersion` **String**, `bWorldReady` **Boolean**) — struct Blueprint à
    créer dans `Core/` ; le préfixe d'asset des structs Blueprint n'est pas
    défini par `NAMING_CONVENTIONS.md`, `F_PrismSessionState` suit la convention
    PRISM locale (§3) ;
  - `Config : DA_PRISM_SessionConfig` (object ref) ;
  - `bAutoStart` (**Boolean**) — si PRISM déclenche directement `Prepare` après
    connexion.
- **Dispatchers (projet, re-diffusion)** : `ED_PRISM_Connected`,
  `ED_PRISM_Disconnected`, `ED_PRISM_WorldInitialized(FPrismSyneWorldDescription)`,
  `ED_PRISM_Snapshot(FPrismSyneWorldSnapshot)`, `ED_PRISM_Delta(FPrismSyneWorldDelta)`,
  `ED_PRISM_Event(FPrismSyneEvent)`, `ED_PRISM_ControlResult(FPrismSyneControlResult)`,
  `ED_PRISM_Error(FPrismSyneError)`.
- **Fonctions** :
  - `Init` : `Get Prism Ldk Subsystem` → `Set LDK` → `Configure(Config)` → enregistre
    chaque dispatcher du plugin sur un Custom Event (`EVT_OnConnected`, …) →
    chaque Custom Event re-diffuse le dispatcher projet correspondant.
  - `RequestStart(Seed, TicksPerSecond, MaxTicks)` : mémorise `Session` puis
    `Prepare`. Appelée par le widget de connexion ou par l'auto-start.
  - `RequestReady()` : `Ready(Session.WorldVersion)` (lit la version reçue).
  - `RequestStartRun()` : `Start(Session.Seed, Session.MaxTicks)`.
  - `RequestPause/Resume/Stop/Reset/Status` : relais directs.
- **Câblage** : seul point qui appelle `Connect`. Ne fait **aucun** calcul de
  rendu ; délègue au registre de monde (§6.1.2, classe C++ concrète).
- **Pièges** : ne jamais appeler `Configure`/`Connect` sur une variable `LDK`
  non initialisée (vaut `None`). `Assign OnConnected` n'appelle pas l'événement
  immédiatement.

#### 6.1.2 `UPrismLdkWorldRegistryBase` — **plus de Blueprint**
- **Type** : sous-système C++ **concret** de la classe du plugin
  (`Blueprintable`, non abstraite), qui dérive de `UGameInstanceSubsystem`.
- **Aucun Blueprint enfant à créer.** Le montage précédent
  (`Abstract, Blueprintable` + `BP_PRISM_WorldSubsystem`) est retiré : il ne
  fonctionnait que tant que l'asset était chargé, et le registre disparaissait
  ensuite (§11.4). Si un `BP_PRISM_WorldSubsystem` subsiste dans `Content/`, il
  doit être **supprimé** : il serait instancié à côté du vrai registre et jamais
  utilisé, parce que `Get Subsystem` renvoie d'abord la classe exacte.
  - **Pourquoi la classe du plugin reste `Blueprintable`** : le moteur déclare
    `UGameInstanceSubsystem` en `UCLASS(Abstract, MinimalAPI)` **sans**
    `Blueprintable` (moteur 5.8,
    `Engine/Source/Runtime/Engine/Public/Subsystems/GameInstanceSubsystem.h`) ;
    `UWorldSubsystem` et `ULocalPlayerSubsystem` non plus. Passer par une classe
    du plugin est donc le seul moyen d'exposer ces fonctions à un graphe — et
    c'est aussi ce qui rend le registre testable.
- **Variables** : **aucune obligatoire**. L'état vit en C++
  (`FPrismLdkWorldRegistry`) et s'écrit par les fonctions ci-dessous ; ajouter
  seulement ce qui est propre à PRISM (références de widgets, caches
  d'affichage). **Jamais** une seconde copie du registre dans un graphe : deux
  copies d'une même correspondance divergent toujours.
- **Fonctions héritées de `UPrismLdkWorldRegistryBase`** (catégorie
  `PRISM|Registry`) :
  - `SetTileSizeUu(TileSizeUu)` — **Float (double précision)**, à appeler avec
    `DA_PRISM_SessionConfig.TileSizeUu` ; renvoie **Boolean** `false` si la
    valeur est refusée (0, négative, non finie) ;
  - `SetStatsFromDescription(World : FPrismSyneWorldDescription)` — **unique**
    point de dérivation de l'échelle (`k = TileSizeUu / CellSize`), à appeler
    sur `OnWorldInitialized` ; l'ordre avec `SetTileSizeUu` est indifférent ;
  - `GetStats()` → `FPrismLdkWorldStats` (`WorldVersion`, `TileSizeUu`,
    `CellSize`, `K`, `MetersPerUnit`, `CellCountX`, `CellCountY`,
    `TicksPerSecond`, `SimulatedSecondsPerTick`, `bScaleValid`) ;
  - agents : `RegisterAgent(Id : **Integer64**, Actor)`, `UnregisterAgent(Id)`,
    `GetAgentActor(Id)`, `HasAgent(Id)`, `GetAgentCount()`, `GetAgentIds()`
    (croissants) ;
  - obstacles : `RegisterObstacle(Id : **String**, Actor)`,
    `UnregisterObstacle(Id)`, `GetObstacleActor(Id)`, `HasObstacle(Id)`,
    `GetObstacleCount()`, `GetObstacleIds()` (triés) ;
  - ressources : `AddResourceMarker(Actor)`, `RemoveResourceMarker(Actor)`,
    `GetResourceMarkers()` ;
  - `ClearAll()` — nouveau run : vide les registres et **conserve**
    `TileSizeUu` (paramètre produit, pas état de run).
- **Statistiques d'exécution** : `FPrismLdkWorldStats` ne contient **pas**
  `Season`, `RunId`, `Tick`, `AliveCount` — ils se lisent sur
  `UPrismLdkSubsystem` (`GetStatus()`, `GetLatestSnapshot()`). `World` et
  `Latest` non plus ne sont pas recopiés ici : le registre ne détient que les
  correspondances et l'échelle.
- **Ce subsystem ne contient aucune formule.** Les conversions viennent du
  plugin (§5.4) : appeler les nœuds `Syne To Unreal`, `Syne To Cell`,
  `Cell To Tile Center`, … en passant `GetStats().K` / `GetStats().CellSize`.
  **Ne pas** recréer une seconde implémentation ici : c'est le défaut de la
  version précédente de ce plan. Corollaire : `k` n'est jamais une variable de
  Blueprint ; `bScaleValid = false` veut dire « échelle inexploitable », pas
  « `k` vaut 0 ».
- **Accès depuis un autre graphe** : nœud **`PRISM Registry`**
  (`Get Prism Ldk World Registry`, catégorie `PRISM|Registry`) ou **Get Game
  Instance Subsystem** avec `Prism Ldk World Registry Base` (la classe C++).
  Le premier est préférable : il reste juste même si le nom de la classe change.
- **Câblage** : source unique du **registre** (les conversions viennent du
  plugin) ; évite `Get All Actors Of Class` par tick. Instanciation : la classe
  est native, donc chargée avec le module (§11.4).

#### 6.1.3 `DA_PRISM_SessionConfig`
- **Type** : PrimaryDataAsset.
- **Champs** : `HttpUrl` (**String**), `WebSocketUrl` (**String**),
  `bAutoReconnect` (**Boolean**), `ReconnectDelaySeconds` (Float **simple**
  précision, cohérent avec le C++), `DefaultSeed` (**Integer64**),
  `DefaultTicksPerSecond` (**Integer**, = 6), `DefaultMaxTicks` (**Integer**),
  `TileSizeUu` (**double précision**, = 3200), `bAutoStart` (**Boolean**),
  `DefaultWorldVersion` (**String**, repli `"1.1"`).
- **Pas de champ `ScaleK`** : `k = TileSizeUu / World.CellSize` est **dérivé**
  une seule fois dans `SetStatsFromDescription`. Stocker `k` à côté de
  `TileSizeUu` créerait deux sources de vérité qui peuvent diverger.
- **Vérification de l'asset** (geste, pas supposition) : ouvrir
  `Core/DA_PRISM_SessionConfig` et confronter champ par champ à la liste
  ci-dessus ; en particulier `TileSizeUu = 3200` — sinon `k ≠ 100` et la scène
  est à la mauvaise échelle **sans erreur visible**. Type attendu :
  `PrimaryDataAsset` (`Engine/Source/Runtime/Engine/Classes/Engine/DataAsset.h`).
- **Rappel de nom** : `DefaultTicksPerSecond = 6` et
  `simulatedSecondsPerTick = 5` sont les valeurs du profil `prism`
  (`configs/simulation/prism.json`) ; le défaut du contrat reste 60 pour
  `simulatedSecondsPerTick`. La valeur qui fait foi est celle reçue dans
  `world_initialized`.

#### 6.1.4 Projection d'espace — **aucun asset à créer**

- **Décision §9** : la projection est du **C++ du plugin** (§5.4,
  `UPrismLdkProjection`), pas une macro Blueprint. **État vérifié :** aucun
  `Content/Math/` ni asset `BML_PRISM_Projection` n'existe dans le Content du
  plugin — le point est satisfait. Si un tel asset réapparaît, **le supprimer**.
- **Pourquoi pas une macro** : le calcul est déterministe et doit être testé.
  Une macro est *développée dans les graphes appelants* — chaque modification
  oblige à recompiler les Blueprints utilisateurs ; un `.uasset` n'est ni
  testable unitairement, ni relisible en diff Git (l'UE Diff Tool est un outil
  éditeur, pas un diff texte) ; et une fonction pure Blueprint n'est pas mise en
  cache, donc réévaluée à chaque évaluation du graphe. Le C++ apporte le test
  automatisé, le diff lisible et une correction sans toucher aux graphes.
- **Appel en Blueprint** : nœuds de catégorie `SYNE|Projection` —
  `Syne To Unreal`, `Unreal To Syne`, `Syne To Cell`, `Cell To Tile Origin`,
  `Cell To Tile Center`, `Syne Radius To Unreal`, `Max Walk Speed For Speed`,
  `Tile Size From Cell Size`, en passant `GetStats().K` et
  `GetStats().CellSize` du registre de monde (§6.1.2).
- **Vérification** : Automation Spec `PrismLdk.Projection` (§5.4) — Session
  Frontend → Automation, ou
  `-ExecCmds="Automation RunTests PrismLdk.Projection"`.

### 6.2 Cadre de jeu et caméra

#### 6.2.1 `BP_PRISM_GameMode`
- **Type** : GameModeBase. **Champs** : `GameInstanceClass = BP_PRISM_GameInstance`,
  `PlayerControllerClass = BP_PRISM_PlayerController`,
  `DefaultPawnClass = BP_PRISM_ObserverPawn`.
- **Fonctions** : `BeginPlay` (crée le `WorldDirector` si absent), `OnWorldInitialized`
  branché (ou délégué au director).

#### 6.2.2 `BP_PRISM_PlayerController`
- **Type** : PlayerController. **Variables** : `ConnectionPanel : WBP_PRISM_ConnectionPanel`,
  `HUD : WBP_PRISM_HUD`, `Inspector : WBP_PRISM_AgentInspector`,
  `EventLog : WBP_PRISM_EventLog`, `Diagnostics : WBP_PRISM_Diagnostics`,
  `bUIHidden : bool`.
- **Fonctions** : `CreateWidgets()` (BeginPlay), `ToggleUI()`,
  `SelectAgent(Id : Integer64)` (→ met à jour `Inspector`), `BindInput()` (input
  Enhanced Input).
- **Entrées** : `IA_ToggleUI`, `IA_ToggleDiagnostics`, `IA_FocusOnAgent`
  (sélection par clic/trace), `IA_QuitObservations`.

#### 6.2.3 `BP_PRISM_ObserverPawn`
- **Type** : Spectator Pawn (ou Pawn + `UFloatingPawnMovement`).
- **But** : caméra d'observation libre. **Note** : le joueur n'existe pas dans
  SYNE, il n'a ni trajectoire, ni besoins, ni perception.

#### 6.2.4 `BP_PRISM_CameraRig`
- **Type** : Actor. **Variables** : `Focus` (**Actor**), `Mode` (enum dédié :
  `Free`, `TopDown`, `Orbit`, `FollowAgent`), `FollowAgentId` (**Integer64**).
- **Fonctions** : `SetMode`, `FocusOnAgent(Id : Integer64)`, `ResetView`.
- **Contraintes** : ne modifie jamais la position simulée ; le suivi est visuel.

### 6.3 Construction du monde

#### 6.3.1 `BP_PRISM_WorldDirector`
- **Type** : Actor (persistant, un seul par niveau).
- **Variables** : `Registry : UPrismLdkWorldRegistryBase` (nœud
  `PRISM Registry` ; le type de base accepte le Blueprint concret),
  `Config : DA_PRISM_SessionConfig`, `TileField : BP_PRISM_TileField`,
  `bBuilding` (**Boolean**), `BuildPhase` (**Integer**), `LastRunId` (**String**).
- **Convention** : dans cette fiche, `Stats` désigne `Registry.GetStats()` ;
  les champs s'écrivent exactement `K`, `CellSize`, `CellCountX`, `CellCountY`,
  `TicksPerSecond`, `SimulatedSecondsPerTick`, `MetersPerUnit`, `TileSizeUu`,
  `WorldVersion`, `bScaleValid`.
- **Fonctions** :
  - `BuildWorldFromDescription(FPrismSyneWorldDescription World)` :
    1. `Registry.SetTileSizeUu(Config.TileSizeUu)` puis `Registry.SetStatsFromDescription(World)` ;
    2. `TileField.Build(World)` (carte unique : **aucun sous-niveau**, §4) ;
    3. `SpawnObstacles(World.Obstacles)` ;
    4. `SpawnResourceMarkers(World.Resources)` ;
    5. `SpawnInitialAgents(World.Agents)` ;
    6. `PrepareRegions(World.Regions)` (World Partition clusters) ;
    7. `Session.bWorldReady = true`.
  - `SpawnObstacles(Obstacles)` : pour chaque `{Id, Position, Radius}` →
    `SpawnActor(BP_PRISM_Obstacle)` à `Syne To Unreal(Position, Stats.K)` puis
    `InitFromObstacle` avec `RadiusUu = Syne Radius To Unreal(Radius, Stats.K)` ;
    `Registry.RegisterObstacle`.
  - `SpawnResourceMarkers(Resources)` : pour chaque ressource initiale
    (`Position` = **indice de cellule**) → `SpawnActor(BP_PRISM_ResourceMarker)`
    à `Cell To Tile Center(Cell, Stats.CellSize, Stats.K)` (jamais
    `Syne To Unreal` : ce sont des indices, pas des coordonnées continues).
  - `SpawnInitialAgents(Agents)` : pour chaque `{Id, Species, Position}` →
    `SpawnActor(BP_PRISM_Agent)` à `Syne To Unreal(Position, Stats.K)`,
    `Agent.InitFromId(Id)` + `Registry.RegisterAgent`.
  - `UpdateFromSnapshot(FPrismSyneWorldSnapshot Snapshot)` :
    1. si `Snapshot.RunId != LastRunId` → `ResetWorld()` puis rebuild logique
       (garde anti-mélange de runs ; `RunId` est mémorisé par le director, il
       n'est pas recopié dans le registre) ;
    2. pour chaque agent : retrouver/créer, appeler `ApplyAgentView` — celui-ci
       **sort immédiatement si rien n'a changé** pour cet agent (détection de
       changement, §7.2) ;
    3. réconcilier `Obstacles[]` (état complet) ;
    4. mettre à jour les compteurs d'affichage (tick, population, saison)
       depuis le snapshot — le registre ne duplique pas ces champs ;
    5. détruire les acteurs absents si la population a baissé.
  - `ApplyDelta(FPrismSyneWorldDelta Delta)` : `added` → instancier l'obstacle ;
    `removed` → détruire ; ignorer si `RunId`/`Tick` d'un ancien run.
  - `ResetWorld()` : `Registry.ClearAll()`, détruire les acteurs créés, vider les
    sous-niveaux runtime.
- **Câblage** : abonné à `ED_PRISM_WorldInitialized`, `ED_PRISM_Snapshot`,
  `ED_PRISM_Delta` du GameInstance. **Seul** à instancier des entités.

#### 6.3.2 `BP_PRISM_TileField`
- **Type** : Actor. **Variables** : `TileInstances`
  (`HierarchicalInstancedStaticMeshComponent`, ou une instance statique par
  case — voir décision §9), `MaterialByTerrain` (**Map**, clé **Name** →
  **Material Interface**), `CellCountX` (**Integer**) — index de case linéaire
  `Index = CellY × CellCountX + CellX`.
- **Fonctions** : `Build(World)` (parcourt `Cells[]`, place chaque instance à
  `Cell To Tile Origin(Cell, GetStats().CellSize, GetStats().K)`, matériau selon
  `TerrainType`), `Clear()`.
- **Pourquoi pas une Map `Int Point → Actor`** : le compilateur Kismet refuse une
  clé non hachable (`Editor/KismetCompiler/Private/KismetCompilerMisc.cpp`,
  messages `MapKeyTypeUnhashable_*`) et `FIntPoint` fournit bien `GetTypeHash`
  (`Runtime/Core/Public/Math/IntPoint.h`) ; en revanche le **support de l'UI
  Blueprint pour une clé de type struct n'a pas été vérifié sur 5.8**. Le plan
  n'en dépend donc pas : un index calculé et des instances portent la même
  information, avec un coût mémoire et de hachage nul.
- **Note** : SYNE n'expose aujourd'hui que `plains` (`TerrainType` en dur) ;
  prévoir le `Switch on EPrismSyneTerrainType` avec branche `Unknown`.

#### 6.3.3 `BP_PRISM_Obstacle`
- **Type** : Actor (StaticMesh + collision). **Variables** : `ObstacleId`
  (**String**), `RadiusUu` (**double précision**, rayon **déjà projeté** par le
  director via `Syne Radius To Unreal`).
- **Fonctions** : `InitFromObstacle(FPrismSyneObstacle)` (positionnement par le
  director + sphère/cylindre de collision de rayon `RadiusUu`),
  `ApplyChange(FPrismSyneWorldDeltaChange)`.
- **Interface** : implémente `BPI_PRISM_EntityView` (`ApplyDelta`, `Reset`).
- **Note** : **les obstacles viennent de SYNE/mock** ; PRISM ne les décide pas.

#### 6.3.4 `BP_PRISM_ResourceMarker`
- **Type** : Actor. **Variables** : `ResourceId` (**String**), `Type`
  (`EPrismSyneResourceType`), `Cell` (**Int Point**).
- **Fonctions** : `InitFromResource(FPrismSyneResource)` (position fournie par
  le director = `Cell To Tile Center` de `Cell`).
- **Note** : purement décoratif ; les **stocks** des snapshots sont **globaux**
  par type, pas attachés à ces acteurs.

### 6.4 Entités et navigation

#### 6.4.1 `BP_PRISM_Agent`
- **Type** : **Character** (décision §9) — `UCharacterMovementComponent` +
  `AIControllerClass = BP_PRISM_AgentController`.
- **Variables** :
  - `AgentId` (**Integer64**) ;
  - `Species` (**String**) ;
  - `CurrentAction` / `CurrentIntention` (`EPrismSyneAction`) ;
  - `Energy` / `Hunger` / `Thirst` / `Fatigue` (**double précision**) ;
  - `TargetCell` (**Int Point**) ; `bHasTargetCell` (**Boolean**) ;
  - `LastSynePosition` (struct `PrismSyneVector2D`) ;
  - `SpeedTrait` (**double précision**, depuis `Traits["speed"]`).
- **Fonctions** :
  - `InitFromWorld(FPrismSyneWorldAgent)` : `AgentId`, `Species`, position initiale ;
  - `ApplyAgentView(FPrismSyneAgentSnapshot)` : **sortie immédiate** si le tick
    et l'état de l'agent sont identiques au dernier appliqué ; sinon mémorise
    besoins/intention/action, calcule la cellule cible
    (`Syne To Cell(Position, Stats.CellSize, Stats.CellCountX, Stats.CellCountY)`)
    et met à jour `MaxWalkSpeed`
    (`Max Walk Speed For Speed(SpeedTrait, Stats.K, Stats.TicksPerSecond)`) ;
  - `ApplyAction(FPrismSyneWorldAction)` : résultat d'action du tick (journal/anim),
    **sans** en déduire l'état (source de vérité = snapshot) ;
  - `Reset()` (nouveau run).
- **Câblage** : appelé par le `WorldDirector` ; délègue le déplacement au
  `AIController`. **Ne jamais téléporter** : `AI Move To` vers un point NavMesh
  **dans la tuile cible**.

#### 6.4.2 `BP_PRISM_AgentController`
- **Type** : AIController. **Variables** : `TargetPoint` (**Vector**),
  `bMoving` (**Boolean**).
- **Fonctions** : `MoveToCell(Cell : Int Point, SynePosition : Vector)` :
  1. projeter un point NavMesh **borné à la tuile** (éviter un point voisin) ;
  2. `MoveToLocation` / `AI Move To` ;
  3. ne relancer un déplacement **que si la cellule a changé**.
- **Contraintes** : l'acteur peut rester décalé du point continu exact SYNE
  (relief/NavMesh) ; les coordonnées logiques restent l'état autoritaire.

#### 6.4.3 `BPI_PRISM_EntityView` (Blueprint Interface)
- **Fonctions** : `ApplyAgentView(FPrismSyneAgentSnapshot)`,
  `ApplyDelta(FPrismSyneWorldDeltaChange)`, `Reset()`.
- **But** : contrat d'actualisation hétérogène (agents, obstacles, marqueurs)
  sans dépendre d'une classe concrète.

#### 6.4.4 `BP_PRISM_AgentAnim` (optionnel)
- **Type** : Anim Blueprint. **But** : présenter l'action courante
  (marche/repos) ; aucune déduction d'état.

### 6.5 Interface utilisateur

#### 6.5.1 `WBP_PRISM_ConnectionPanel`
- **Contenu** : champs `Seed`, `TicksPerSecond`, `MaxTicks` ; boutons
  **Connecter / Préparer / Démarrer / Pause / Reprendre / Arrêter /
  Réinitialiser / Statut** ; zone d'erreur.
- **Fonctions** : chaque clic appelle la fonction correspondante du GameInstance.
  Désactiver les boutons selon `GetConnectionState` et `GetStatus.State`
  (`EPrismSyneRunState`). Afficher `OnError.Code`
  (`world_not_ready`, `run_active`, `run_finished`, `prepared_seed_mismatch`, …).

#### 6.5.2 `WBP_PRISM_HUD`
- **Contenu** : connexion, état SYNE, `RunId`, `Tick` / `MaxTicks`, population
  (`AliveCount`), saison (`SeasonEnum`), horloge.
- **Horloge** : afficher `simulatedTimeSeconds` **et** le ratio
  `R = TicksPerSecond × SimulatedSecondsPerTick` (profil PRISM : `6 × 5 = 30` ;
  journée simulée ≈ 48 min).

#### 6.5.3 `WBP_PRISM_AgentInspector`
- **Contenu** : pour l'agent sélectionné — identifiant, espèce, besoins,
  intention/action, `Traits`, `Beliefs`, `Goals`, `Trust`, `MemoryCount`.
- **Source** : `GetLatestSnapshot` (état courant), jamais un état mis en cache
  indépendant. Les champs vides (mock : `beliefs`/`trust`) ne sont pas complétés
  par des suppositions.

#### 6.5.4 `WBP_PRISM_EventLog`
- **Contenu** : liste défilante depuis `ED_PRISM_Event` (type, tick, agent,
  valeur) ; branche `Unknown` obligatoire ; `ValueJson` brut conservé.

#### 6.5.5 `WBP_PRISM_Diagnostics`
- **Contenu** : version du monde (`World.Version`), `engineVersion` du snapshot,
  `TicksPerSecond`, `RunId`, tick, `MaxTicks`, population, dernière erreur
  (`OnError`), état de reconnexion.

### 6.6 Visualisation (optionnel, après le socle)

Issu de `VISUALIZATION_SPEC.md`, **seulement** sur des champs réellement
exposés : relations de confiance (`Trust`), groupes (`Groups`), territoires
(`Territories`), heatmap de communication (événements `message_*`), chronologie.
Toute agrégation est une convention de présentation, déterministe, et ne
complète pas les champs absents.

## 7. Pipeline de données (détail des réactions)

### 7.1 `OnWorldInitialized` — construction (avant `Ready`)

1. `Registry.SetTileSizeUu(Config.TileSizeUu)` (une fois, idempotent) puis
   `Registry.SetStatsFromDescription(World)` : **calcul unique** de
   `k = TileSizeUu / CellSize`, plus `CellSize`, `CellCountX/Y`,
   `TicksPerSecond`, `SimulatedSecondsPerTick`, `MetersPerUnit`,
   `WorldVersion`.
2. Terrain : `TileField.Build` sur `Cells[]`.
3. Obstacles : instancier `Obstacles[]` (positions continues + rayon).
4. Ressources : instancier `Resources[]` (indices de cellule).
5. Agents : instancier `Agents[]` (positions continues), registre par `Id`.
6. Régions : préparer les clusters WP (`Regions[]`).
7. `Ready(World.Version)`.

**Invariant** : `world_initialized` précède toujours le premier snapshot ;
ne pas appeler `Start` avant la fin de la génération.

### 7.2 `OnSnapshot` — état autoritaire

1. Si `Snapshot.RunId` diffère du `LastRunId` mémorisé par le director →
   `ResetWorld` puis rebuild logique. (`RunId` n'est **pas** dans
   `FPrismLdkWorldStats` : le registre ne duplique pas les champs d'exécution.)
2. Agents : retrouver/créer par `Id`, `ApplyAgentView` (**sortie immédiate si
   rien n'a changé pour cet agent**), lancer le déplacement seulement si la
   cellule a changé.
3. Obstacles : réconcilier avec `Obstacles[]` (liste complète).
4. Stocks : `Resources[]` sont **globaux par type** (`food`, `water`, `wood`,
   `mineral`).
5. Groupes/Territoires/Livres : mettre à jour les vues actives.
6. Saison : `Season`/`SeasonIndex`.
7. `Actions[]` : résultats/deltas du tick (journal, feedback visuel).
8. `WorldChanges[]` : mutations de ce tick (voir garde anti-double emploi §7.4).
9. Compteurs d'affichage : `Snapshot.Tick`, `Snapshot.AliveCount`,
   `Snapshot.SeasonEnum`. Le registre ne stocke pas ces champs ; pour un accès
   direct hors snapshot, `UPrismLdkSubsystem.GetStatus()` /
   `GetLatestSnapshot()` (§5.1).

### 7.3 `OnSyneEvent` — notifications

- Brancher sur un dispatcher projet ; journal/résumé ; branche `Unknown`.
- Types connus : `tick_summary`, `decision_made`, `action_completed`,
  `message_sent`, `message_received`, `group_decision`,
  `world.book_written`, `world.season_changed`, etc.
- **Ne pas** réappliquer une mutation déjà présente dans le snapshot.

### 7.4 Garde anti-double mutation

`OnSnapshot` **et** `OnWorldDelta`/`OnSyneEvent` peuvent décrire la même
mutation. Règle : le **snapshot est la source de vérité** de l'état ; les
deltas/événements servent aux notifications/effets. Ne pas appliquer deux fois
la même mutation d'obstacle.

### 7.5 `OnDisconnected` — reconnexion (décision §9)

Politique retenue : **reprise automatique de la consommation** sans relancer le
run. Séquence :

1. Geler l'affichage (ne pas figer la caméra, marquer l'agent en « dernière
   position connue »).
2. `bAutoReconnect = true` : le plugin retente la WebSocket après le délai.
3. Sur `OnConnected` : `RequestStatus()` ; comparer `RunId`/`Tick` au dernier
   connu.
   - même `RunId` → reprendre la consommation (le mock/SYNE **ne rejoue pas** les
     trames manquées ; accepter un saut de tick).
   - `RunId` différent ou run `finished` → proposer `Reset`.
4. `OnError` affiche la cause ; aucune relance automatique de `Start`.

## 8. Conversion d'espace (formules et exemples)

```text
k                 = TileSizeUu / CellSize        (profil PRISM : 3200 / 32 = 100)
UnrealX           = SyneX * k                     UnrealY = SyneY * k
CellX             = floor(SyneX / CellSize)       CellY = floor(SyneY / CellSize)
TileOrigin        = (CellX * CellSize * k, CellY * CellSize * k)
TileCenter        = TileOrigin + (CellSize * k / 2)
MaxWalkSpeed      = traits.speed * k * TicksPerSecond   (0,5..1,5 → 300..900 uu/s)
```

Exemples (profil B, `CellSize = 32`, `k = 100`) :

| Donnée | Valeur SYNE | Repère | Unreal |
| :-- | :-- | :-- | :-- |
| Agent | `(12.5, 23.0)` | continu | `(1250, 2300)` uu ; cellule `(0, 0)` |
| Obstacle | `(150, 120)`, `radius 8` | continu + rayon | centre `(15000, 12000)` uu, rayon `800` uu |
| Cellule | `(3, 4)` | indice | origine `(9600, 12800)` uu ; centre `(11200, 14400)` uu |
| Marqueur ressource | cellule `(5, 7)` | indice | centre de tuile `(17600, 24000)` uu |
| Vitesse `speed = 0,5` | trait, par tick | vitesse | `300` uu/s (`× TicksPerSecond` = 6) |
| Vitesse `speed = 1,5` | trait, par tick | vitesse | `900` uu/s |

Points de vigilance :

- `Cells[].X/Y` et ressources initiales = **indices** ; agents/obstacles =
  **coordonnées continues**. Utiliser `Cell To Tile Center` pour les premiers,
  `Syne To Unreal` pour les seconds — c'est l'erreur de projection la plus
  probable.
- `Tick` est un entier **64 bits** ; `double` et `int64` sont des **types C++** :
  en Blueprint, lire **Float (double précision)** et **Integer64** (§3.1).
- `k` n'est **pas** une constante universelle : `k = TileSizeUu / CellSize`
  n'est égal à `MetersPerUnit × 100` que tant que 1 unité SYNE vaut 1 m et que
  1 unité Unreal vaut 1 cm. Un profil qui change `metersPerUnit` **doit**
  recalculer `k`, sinon l'échelle du monde devient fausse silencieusement.
- Les stocks de snapshot sont **globaux** par type.
- Toutes les valeurs ci-dessus sont **verrouillées par test** (Automation Spec
  `PrismLdk.Projection`, §5.4) : le tableau et le code ne peuvent pas diverger.

## 9. Décisions tranchées et points ouverts

| Sujet | Décision | Conséquence |
| :-- | :-- | :-- |
| Character vs Pawn | **Character** | `BP_PRISM_Agent` hérite de `ACharacter` : `CharacterMovement` + NavMesh + `AI Move To` directs ; `MaxWalkSpeed = speed*k*TPS`. |
| Génération de scène | **Une carte persistante, pas de sous-niveaux** | Organisation par **Data Layers** (`DL_PRISM_*`) et Level Instances ; tuiles et entités **générées** spawnées au runtime. World Partition **optionnel**, décidé par mesure en B3, et sa cellule posée explicitement s'il est activé. §4. |
| Version du monde | **Lire `World.Version`** | `Ready(World.Version)` ; le défaut `"1.1"` n'est qu'un repli. `Session.WorldVersion` est mémorisé. |
| Reconnexion | **Reprise de consommation** | `OnConnected` → `RequestStatus` → comparer `RunId`/`Tick` ; `Reset` si run différent/fini. §7.5. |
| Obstacles | **Fournis par SYNE/mock** | PRISM **affiche** `Obstacles[]`/`WorldChanges[]` ; aucun layout décidé côté PRISM. `Prepare` du plugin n'envoie pas `config.world`. |
| Calcul de projection | **C++ du plugin** | `UPrismLdkProjection` en `BlueprintPure` + Automation Spec ; aucun asset `BML_` ; `K`/`CellSize` **toujours** en paramètres, aucune constante produit dans le plugin. §5.4. |
| Emplacement des assets | **Content du plugin** | Deux projets Unreal : **LDK** (création/configuration du plugin, assets génériques, pas d'art importé) et **PRISM final** (intègre le plugin, importe meshes/textures/animations dans `/Game`). §3. |
| Registre de monde | **Sous-système C++ concret** | `UPrismLdkWorldRegistryBase` (`Blueprintable`, non abstraite, instanciée avec le module) porte les correspondances `Id → acteur` et l'échelle dérivée ; testable (`PrismLdk.WorldRegistry`), évite `Get All Actors Of Class` et ne dépend d'aucun asset chargé. §5.5, §6.1.2, §11.4. |
| World Partition | **[À DÉCIDER en B3, sur relevé]** | Défauts moteur : `CellSize = 12 800 uu`, `LoadingRange = 25 600 uu` ; WP n'est activé que si la mémoire/le chargement l'exigent, sinon carte simple + HISM. §4. |
| Ports SYNE sous Launcher | **[À TRANCHER — prérequis B7]** | Le Launcher ne transmet à PRISM que ses propres arguments (`--instance-id`, `--control-port`, `--data-port`, `--headless`). La transmission des URLs SYNE doit être choisie et écrite dans le contrat. §2.2, §11.1. |

## 10. Feuille de route détaillée (jalons B0 → B7)

### B0 — Compiler le plugin
- **État : fait (14 octobre 2026).** `prism/LDK/LDK.uproject` sous UE 5.8.3,
  plugin `PrismLdk` activé : `Build.bat LDKEditor Win64 Development` →
  **`Result: Succeeded`**, 0 avertissement ; UHT a traité les types ajoutés
  (`FPrismSyneWorldDescription.SimulatedSecondsPerTick`, `.MetersPerUnit`,
  `FPrismSyneWorldSnapshot.Seed`, `UPrismLdkWorldRegistryBase`).
- **Automation Specs exécutées** en éditeur headless (`UnrealEditor-Cmd`
  `-nullrhi`, `-ExecCmds="Automation RunTests
  PrismLdk.Projection+PrismLdk.WorldRegistry"`) : **34 tests, 0 échec**.
- **Artefacts** : plugin compilé ; journal de build ; rapport de test.
- **Sortie** : projet + plugin compilent, types Blueprint visibles, specs
  **vertes** (projection et registre).

### B1 — Fondations session/connexion
- **Tâches** : créer `DA_PRISM_SessionConfig`, puis `BP_PRISM_GameInstance`
  (le registre de monde n'a **plus** de Blueprint : §6.1.2, §11.4)
  (Init, Configure, Connect, re-diffusion des dispatchers) ; **confronter
  `DA_PRISM_SessionConfig` à §6.1.3 champ par champ** (`TileSizeUu = 3200`) ;
  **aucun asset de projection** (le calcul est en C++, §6.1.4 — déjà satisfait,
  aucun `Content/Math/` n'existe) ; **exécuter l'Automation Spec
  `PrismLdk.WorldRegistry`** (Session Frontend → Automation).
- **Artefacts** : GameInstance câblé ; registre instancié (`LogPrismLdk`) ;
  connexion manuelle fonctionnelle.
- **Sortie** : `OnConnected` reçu ; `GetConnectionState` = `Connected` ;
  `PrismLdk.WorldRegistry` vert.

### B2 — Cycle de préparation (sans rendu)
- **Tâches** : `RequestStart` → `Prepare` ; sur `OnWorldInitialized` mémoriser la
  description et la version ; `RequestReady` ; sur `OnControlResult.bOk` →
  `RequestStartRun` ; afficher `GetStatus` dans le HUD de diagnostic.
- **Artefacts** : cycle complet logué.
- **Sortie** : premiers `OnSnapshot` reçus à la cadence préparée.

### B3 — Construction du monde
- **Tâches** : `BP_PRISM_WorldDirector`, `BP_PRISM_TileField`,
  `BP_PRISM_Obstacle`, `BP_PRISM_ResourceMarker` ; **une seule carte**
  (`Level/LDK_BaseMap`) et **aucun sous-niveau** ; **décision World Partition
  mesurée** (§4) — si WP est activé, poser la grille de runtime explicitement
  (`CellSize = 3200`, `LoadingRange` multiple de 3 200) ; positionnement
  **exclusivement** par les nœuds du plugin (`Syne To Unreal`,
  `Cell To Tile Origin/Center`, `Syne Radius To Unreal`) avec
  `GetStats().K` / `GetStats().CellSize` — aucune formule dans les graphes.
- **Artefacts** : scène générée depuis `WorldDescription` ; **relevé
  mémoire/chargement** justifiant WP ou son absence ; doublon
  `Content/LDK_BaseMap.umap` supprimé.
- **Sortie** : tuiles, obstacles et marqueurs positionnés à l'échelle `k`, carte
  unique.

### B4 — Entités et navigation
- **Tâches** : `BP_PRISM_Agent` (Character), `BP_PRISM_AgentController`,
  `BPI_PRISM_EntityView` ; `UpdateFromSnapshot` par `Id` ; `MaxWalkSpeed` par
  agent ; déplacement par `MoveToCell` borné à la tuile.
- **Artefacts** : agents synchronisés.
- **Sortie** : poursuite sans dérive ni doublon ; `Reset` vide le registre.

### B5 — Interface
- **Tâches** : `WBP_PRISM_ConnectionPanel`, `WBP_PRISM_HUD`,
  `WBP_PRISM_AgentInspector`, `WBP_PRISM_EventLog`, `WBP_PRISM_Diagnostics` ;
  `BP_PRISM_PlayerController` (widgets, saisie, sélection).
- **Artefacts** : parcours utilisateur complet.
- **Sortie** : Pause/Reprendre/Arrêter/Reset reflètent l'état réel ; sélection
  d'agent fonctionnelle.

### B6 — Événements, mutations, viz optionnelles
- **Tâches** : `ApplyDelta` (obstacles), journal `OnSyneEvent`, effets
  ponctuels ; vues optionnelles (`VISUALIZATION_SPEC`).
- **Artefacts** : journal + mutations.
- **Sortie** : mutations d'obstacle appliquées **une seule fois**.

### B7 — Empaquetage et Launcher
- **Tâches** :
  - **fait** : service C++ du composant (`PrismLdkService` : `/health/live`,
    `/health/ready`, `/health/details`, `/info`, `/metrics`,
    `POST /control/shutdown` avec `Authorization: Bearer`) ; analyse des
    arguments supervisés et codes de sortie 2/3 ; Automation Spec
    `PrismLdk.Service`. Contrat détaillé : §10 de
    [`PRISM_UNREAL_IMPLEMENTATION.md`](PRISM_UNREAL_IMPLEMENTATION.md).
  - package Unreal (Shipping) ; lecture des arguments de service ; rédiger
    `component.json` PRISM ; installation de test ; vérifier le déverrouillage
    Immersion ; confirmer le module **`HTTPServer`** dans le build Shipping ;
    vérifier l'instanciation du registre dans un build **empaqueté** (§11.4).
  - **prérequis à trancher avant ce jalon** : transmission des URLs SYNE au
    composant PRISM (§2.2.2, §11.1) — sans elle, un PRISM supervisé ne sait pas
    à qui se connecter.
- **Artefacts** : build PRISM + manifeste.
- **Sortie** : le Launcher démarre/arrête PRISM ; l'échec de PRISM n'interrompt
  pas SYNE ; mode Immersion déverrouillé par manifeste conforme.

## 11. Empaquetage et intégration au Launcher (détail)

> **État au 14 octobre 2026** : la surface de service (santé, info, métriques,
> arrêt authentifié, codes de sortie) est **implémentée** dans le C++ du plugin
> — voir §10 de [`PRISM_UNREAL_IMPLEMENTATION.md`](PRISM_UNREAL_IMPLEMENTATION.md),
> qui fait foi pour les codes HTTP, les corps de réponse, les unités des
> métriques et le branchement au build. Ce qui suit reste le cadre du jalon.

### 11.1 Arguments à accepter, et URLs de SYNE
`launcher/contracts/INTEGRATION-CONTRACT-v1.md` §3 :

```text
--instance-id <id>   --control-port <port>   --work-dir <path>
--log-dir <path>     --correlation-id <id>   --headless (si capacité déclarée)
--data-port <port>   (port de l'endpoint ws que PRISM déclare, via launchArgument)
LIVEX_SESSION_TOKEN  (env, jamais ligne de commande)
LIVEX_CORRELATION_ID (env, doublon de --correlation-id)
```

PRISM doit lire ces arguments : `--control-port` sert à **exposer** sa propre
surface de service (`PrismLdkService`) et `--data-port` est le port de l'endpoint
`ws` qu'il **déclare** dans son manifeste. **Aucun de ces arguments ne décrit
SYNE.**

**Point ouvert — prérequis de B7** : comment PRISM apprend-il les URLs du moteur
lancé par le Launcher ? Rien ne les lui transmet aujourd'hui (§2.2.2). Trois
voies sont ouvertes : un argument d'endpoint (`--ws-url`, comme le fait déjà
`launcher/Stub.Prism/Program.cs`, ou `--control-url`) ; des variables
d'environnement `LIVEX_SYNE_OBSERVE_URL` / `LIVEX_SYNE_CONTROL_URL` ; ou un
`launchArgument` du manifeste PRISM porteur de l'URL. La voie retenue doit être
**écrite dans le contrat** puis couverte par un test de bout en bout — pas
seulement appelée depuis un graphe Blueprint.

### 11.2 Endpoints à exposer

| Endpoint | Méthode | Rôle |
| :-- | :-- | :-- |
| `/health/ready` | GET | Readiness (2xx = prêt) |
| `/health/live` | GET | (option) vivacité |
| `/health/details` | GET | (option) détail des checks |
| `/info` | GET | `{id:"prism", version, protocolVersion}` |
| `/metrics` | GET | (option) `livex_prism_fps`, `livex_prism_ws_connected` |
| `/control/shutdown` | POST | Arrêt propre, jeton requis |
| `/control/config` | GET / PUT | Optionnel en v1 (`docs/launcher/INTEGRATION_CONTRACT.md` §6) — **non implémenté** dans `PrismLdkService` à cette date ; s'il est ajouté, il exige le jeton |

Pas de JSON de readiness obligatoire : **le statut HTTP fait autorité**. La
définition de « prêt » côté PRISM reste à confirmer dans le contrat
(`docs/launcher/INTEGRATION_CONTRACT.md` §7 : « processus vivant et, si son
moteur l'expose, scène chargée **[À CONFIRMER]** ») ; l'implémentation actuelle
répond `503` avec la cause tant que le moteur et la description de monde ne sont
pas disponibles (§10.3 du guide d'intégration).

Contrat exact des réponses — `200`, `401` (jeton absent ou invalide), `404`
(chemin inconnu), `405` (verbe non supporté), `503` (pas prêt) — avec les corps
JSON, les unités des métriques et les codes de sortie 0/2/3 : §10.3 et §10.4 du
guide d'intégration. L'arrêt exige `Authorization: Bearer <LIVEX_SESSION_TOKEN>`,
jamais le jeton en ligne de commande.

### 11.3 Manifeste `component.json` PRISM (à livrer en B7)

```json
{
  "schema": 1,
  "id": "prism",
  "name": "PRISM",
  "type": "immersion",
  "version": "0.1.0",
  "executable": { "windows": "PRISM/Binaries/Win64/Prism.exe",
                  "linux": "PRISM/Binaries/Linux/Prism" },
  "capabilities": ["headless", "snapshotStream", "renderCadence"],
  "endpoints": {
    "control": { "transport": "http" },
    "ws": { "transport": "websocket", "launchArgument": "--data-port" }
  },
  "health": { "probe": "http", "path": "/health/ready", "intervalMs": 1000 },
  "timeouts": { "startupMs": 60000, "shutdownMs": 15000 },
  "contributesTo": ["immersion"]
}
```

> Exigences de `PrismRequirements.cs` : `type: immersion` (ou `contributesTo`),
> capacités `snapshotStream` **et** `renderCadence`, endpoint `control` actif.
> Vérifié par `launcher/Launcher.Tests.EndToEnd/PrismIntegrationEndToEndTests.cs`.

**Conformité au schéma** (`launcher/contracts/component-manifest-v1.schema.json`,
vérifiée champ par champ) : les dix propriétés obligatoires sont présentes
(`schema`, `id`, `name`, `type`, `version`, `executable`, `endpoints`,
`capabilities`, `health`, `timeouts`) ; `endpoints` exige `control` sur
`transport: http` (présent) ; l'`id` doit respecter `^[a-z][a-z0-9-]*$`
(`prism` ✅) ; `launchArgument` doit respecter `^--?[A-Za-z0-9][A-Za-z0-9-]*$`
(`--data-port` ✅) ; `executable` n'exige **pas** `path` (les clés par plateforme
suffisent) et refuse les clés inconnues ; `capabilities` et `contributesTo`
exigent des éléments uniques ; `health.intervalMs` ≥ 100 ; les chemins sont
relatifs à l'installation, sans racine, sans lettre de lecteur et sans `..`.

Points à traiter dans le manifeste réel :

- **`version` doit rester égale à `PRISM_COMPONENT_VERSION`** (`PrismLdkService.h`,`0.1.0`), puisque `/info` publie la même valeur.
- **`executable`** : viser le **nom réel** produit par le package Unreal
  (`PRISM-Win64-Shipping.exe`, ou un renommage explicite) — le Launcher refuse
  une installation dont l'exécutable déclaré n'existe pas. Ajouter
  `workingDirectory` (même dossier que l'exécutable) est plus robuste.
- **Champs optionnels du schéma** : `protocolVersion` (entier ≥ 1 ; `/info`
  répond `protocolVersion: 1`) et `requires` (liste d'`id`), dont la sémantique
  d'ordonnancement doit être vérifiée dans le Launcher avant de s'en servir.
- **Vérification** : ajouter le `component.json` PRISM comme fixture dans
  `launcher/contracts/examples/` afin qu'il passe par
  `launcher/Launcher.Tests.EndToEnd/ManifestContractTests.cs`, puis exécuter la
  suite Launcher — la validation par relecture ne suffit pas.

### 11.4 Sous-système de registre et classes chargées

**Résolu — 15 octobre 2026 : option 3 appliquée.** `UPrismLdkWorldRegistryBase`
est devenue **concrète** ; `PrismLdkWorldRegistry.h` et le guide §2.3 ne
demandent plus aucun Blueprint enfant.

Ce qui a été observé : le piège décrit ci-dessous ne se déclenche pas seulement
en build empaqueté, il s'est déclenché **en éditeur**. La ligne
`registre de monde instancié (BP_PRISM_WorldSubsystem_C)` figure dans les
sessions du matin et **disparaît** de la session de 11 h 58, sans qu'aucune
ligne de code ait changé : seul l'asset avait été refermé. `Get Prism Ldk World
Registry` renvoyait alors `nullptr`, la variable `Registry` du directeur de
monde restait nulle, `SetTileSizeUu` / `ClearAll` s'exécutaient sur `None`
(`Accessed None … property Registry`) et le monde n'était plus construit —
plus de `Ready`, plus de `Start`.

Décision : le registre est un **sous-système C++ concret**, instancié par le
moteur avec son module. Le mécanisme d'instanciation, relu dans le moteur 5.8,
le garantit :

- `FSubsystemCollectionBase::Initialize` collecte
  `GetDerivedClasses(UGameInstanceSubsystem, …)` puis `AddAndInitializeSubsystems`
  instancie chaque classe **non abstraite** et la range dans `SubsystemMap` sous
  sa classe **exacte** ;
- `GetSubsystemInternal` regarde `SubsystemMap.FindRef(SubsystemClass)` **avant**
  de retomber sur un dérivé (`FindAndPopulateSubsystemArray`).

Conséquence : `GetSubsystem<UPrismLdkWorldRegistryBase>()` renvoie toujours
l'instance C++, et un Blueprint dérivé — s'il en subsiste un dans le contenu —
passerait **inaperçu** : il faudrait le supprimer, pas s'en servir.

Historique conservé (motif du montage retiré) : le moteur instancie les
sous-systèmes à partir des **classes chargées**, et un Blueprint que rien ne
référence peut être absent. Les trois voies envisagées alors étaient :

1. référencer le Blueprint par une **référence de classe dure** depuis
   `BP_PRISM_GameInstance` ;
2. déclarer le Blueprint comme **primary asset** dans les règles de l'Asset
   Manager ;
3. rendre `UPrismLdkWorldRegistryBase` **concrète** et supprimer le Blueprint
   enfant. ← retenu

Repère de diagnostic : la ligne `registre de monde instancié (…)` de
`LogPrismLdk` doit apparaître **avant** la construction du monde, à chaque run
et sans dépendre d'un asset. Son absence signifie « registre non instancié »,
jamais « registre vide » — si elle manque alors que le module est chargé,
c'est un défaut du plugin, pas du contenu.

## 12. Checklist d'acceptation Blueprint

- [ ] Le projet PRISM compile avec `PrismLdk` activé (UHT OK sur les champs
      ajoutés et sur `UPrismLdkProjection`).
- [ ] Automation Spec `PrismLdk.Projection` **vert** (valeurs de contrôle du §8
      et cas invalides).
- [ ] Aucune formule de projection dans un graphe Blueprint : uniquement les
      nœuds du plugin, `GetStats().K` / `GetStats().CellSize` en entrée.
- [ ] Automation Spec `PrismLdk.WorldRegistry` **vert** (échelle dérivée,
      remplacement d'agent, refus des entrées invalides, acteurs détruits,
      `ClearAll`).
- [ ] Aucun Blueprint enfant de `UPrismLdkWorldRegistryBase` dans le contenu ;
      la ligne `registre de monde instancié (UPrismLdkWorldRegistryBase)`
      apparaît au démarrage **et** dans un build empaqueté (§11.4).
- [ ] Aucun asset `Math/` ni `BML_` dans le Content du plugin.
- [ ] `world_initialized` précède le premier snapshot ; scène générée avant
      `Ready` ; `Ready(World.Version)`.
- [ ] `OnSnapshot` actualise l'état ; événements/deltas ne doublent pas les
      mutations.
- [ ] Agents associés par `Id` ; poursuite sans dérive (`MaxWalkSpeed` correct).
- [ ] Obstacles affichés depuis `Obstacles[]`/`WorldChanges[]`, décidés par SYNE.
- [ ] Pause/Reprendre/Arrêter/Reset reflètent l'état réel.
- [ ] Reconnexion : reprise sans relance ; `Reset` si run différent/fini.
- [ ] Fermeture moteur → `OnDisconnected`/`OnError`, pas de crash.
- [ ] Enums inconnus restent diagnostiquables (`Unknown` + texte).
- [ ] (B7) Launcher démarre/arrête PRISM ; panne PRISM n'arrête pas SYNE ;
      Immersion déverrouillée par manifeste conforme.
- [ ] (B7) La voie de transmission des **URLs SYNE** vers PRISM est écrite dans
      le contrat, implémentée et couverte par un test (§2.2, §11.1).
- [ ] Aucun sous-niveau `SL_PRISM_*` ; une seule copie de `LDK_BaseMap` ; les
      restes d'amorçage (`GameInstance_LDK`, `ldk_GameMode`) supprimés.
- [ ] La décision World Partition est **justifiée par un relevé** (mémoire,
      chargement) et, si WP est actif, la grille pose `CellSize = 3200`.
- [ ] Chaque affirmation moteur de ce plan est sourcée (§13) ou marquée
      **[À VÉRIFIER]** avec son geste de vérification.
- [ ] Aucune Map Blueprint dont la clé est un struct, sans vérification de
      l'éditeur (§6.3.2).

## 13. Sources de vérité et vérifications

Une affirmation technique se **vérifie** ou se **marque**. Table des sources, à
rejouer avant de modifier une section de ce document : elle évite de réintroduire
une capacité moteur supposée (c'est l'erreur qui a fait écrire « Blueprint
GameInstanceSubsystem » en §6.1.2).

| Famille d'affirmation | Source de vérité | Comment vérifier |
| :-- | :-- | :-- |
| Version du moteur | `Engine/Build/Build.version` (5.8.3, branche `++UE5+Release-5.8`) | lire le fichier |
| Dépendances du module | `Source/PrismLdk/PrismLdk.Build.cs` ; casse exacte du module moteur (`Runtime/Online/HTTPServer`) | lire le fichier ; compiler et exiger 0 avertissement |
| Bases de sous-système non dérivables en Blueprint | `Engine/Source/Runtime/Engine/Public/Subsystems/*.h` (`UCLASS(Abstract, …, MinimalAPI)`, pas de `Blueprintable`) | lire les en-têtes ; tenter la création du Blueprint |
| Instanciation d'un sous-système | `Engine/Source/Runtime/Engine/Private/Subsystems/SubsystemCollection.cpp` (`GetDerivedClasses`, classes `Abstract` écartées ; `GetSubsystemInternal` : classe **exacte** avant tout dérivé) | lire la source ; la ligne `registre de monde instancié` doit apparaître à **chaque** run, sans dépendre d'un asset |
| Valeurs par défaut de World Partition | `RuntimePartition.cpp:27` (`LoadingRange = 25600`), `RuntimePartitionLHGrid.cpp:178` (`CellSize = LoadingRange / 2`) | lire la source ; ouvrir World Settings → World Partition → Runtime Grid |
| Sous-niveaux dans un monde WP | documentation Epic World Partition / Levels (sous-niveaux = mode hérité ; WP remplace le découpage manuel) | documentation Epic ; en cas de doute, rester sur cellules + Data Layers |
| Clés de Map en Blueprint | `Editor/KismetCompiler/Private/KismetCompilerMisc.cpp` (`MapKeyTypeUnhashable_*`) ; `Math/IntPoint.h` (`GetTypeHash`) | créer une variable Map en éditeur : la liste des clés proposées fait foi |
| Surface Blueprint du plugin | `Source/PrismLdk/Public/{PrismLdkSubsystem,PrismLdkTypes,PrismLdkProjection,PrismLdkWorldRegistry,PrismLdkBlueprintLibrary}.h` | lire les en-têtes ; les noms de nœuds viennent des `UFUNCTION` |
| Contrat de service du Launcher | `launcher/contracts/INTEGRATION-CONTRACT-v1.md` ; `docs/launcher/INTEGRATION_CONTRACT.md` §3/§4/§6/§7 | lire les contrats ; l'implémentation est `PrismLdkService` |
| Arguments réellement reçus par PRISM | `launcher/Launcher.Tests.EndToEnd/PrismIntegrationEndToEndTests.cs` (`PrismUnlockEndToEndTests`, assertions `Contains`/`DoesNotContain`) | exécuter la suite de tests Launcher |
| Contrat SYNE (routes, messages, champs) | `docs/prism/TRANSPORT_API.md` et `syne/API_CONTRACTS.md` (source de vérité) | lire les contrats ; ne jamais généraliser depuis un seul mock |
| Égalité `syne-mock` ↔ moteur pour la description du monde | `scripts/parity_mock_syne_world.js` — lance SYNE réel (profil `prism` + layout d'obstacles) et le mock sur la même géométrie, puis compare clés, en-tête, `cells[]` champ par champ et `obstacles[]` | **exécuter le script** (code 0 attendu, refus de conclure si aucune case n'est bloquée) ; ne jamais affirmer l'alignement du mock sans l'avoir lancé |
| Profil de simulation | `configs/simulation/prism.json` (monde 2240, cellule 32, 6 ticks/s, 5 s simulées par tick) | lire le fichier |
| Projection et échelle | `Source/PrismLdk/Private/PrismLdkProjection.cpp` + Spec `PrismLdk.Projection` | exécuter la spec ; le tableau de §8 en découle |
| Registre et échelle dérivée | `Source/PrismLdk/Public/PrismLdkWorldRegistry.h` + Spec `PrismLdk.WorldRegistry` | exécuter la spec |
| État du Content | arborescence `Plugins/PrismLdk/Content/` (un `.uasset` n'est pas relisible en diff) | `find` sur le dossier ; joindre le relevé à l'artefact de jalon |

Commandes de rejeu :

```bash
# Compilation de l'hôte LDK (0 avertissement attendu)
"$UE/Engine/Build/BatchFiles/Build.bat" LDKEditor Win64 Development \
  -Project="prism/LDK/LDK.uproject"

# Specs PRISM (30 tests unitaires, 0 échec attendu)
"$UE/Engine/Binaries/Win64/UnrealEditor-Cmd.exe" "prism/LDK/LDK.uproject" \
  -ExecCmds="Automation RunTests PrismLdk.Projection+PrismLdk.WorldRegistry" \
  -TestExit="Automation Test Queue Empty" -unattended -nopause -nosplash -nullrhi
```

## Mises à jour

| Date | Changement | Motif |
| 15 octobre 2026 | **Registre de monde en C++ pur** | Le montage `Abstract` + `BP_PRISM_WorldSubsystem` ne tenait que tant que l'asset était chargé : la ligne `registre de monde instancié` disparaît de la session de 11 h 58, `Get Prism Ldk World Registry` renvoie `nullptr`, le directeur de monde s'exécute sur `None` et le monde n'est plus construit. `UPrismLdkWorldRegistryBase` devient **concrète** (instanciée avec le module, résolution par classe exacte) ; le Blueprint enfant est supprimé du contenu, et §2.3 du guide, §5.5, §6.1.2, §11.4, la checklist §12 et §13 sont réalignés. |
| 15 octobre 2026 | **Parité de la description du monde avec le moteur** | `scripts/parity_mock_syne_world.js` ; §13 gagne la ligne « Égalité `syne-mock` ↔ moteur ». Le mock publiait des cellules que SYNE ne sait pas produire (mer/rivières `terrainType: sea|river`, `movementCost: null` issu d'un `Infinity`, obstacles procéduraux et à formes) : PRISM affichait un monde impossible et le plugin signalait l'écart. Mesure : 4 900 cases identiques champ par champ, 11 cases bloquées de part et d'autre. |
| 14 octobre 2026 | **Vérification intégrale et corrections** | Relecture de toutes les affirmations moteur sur la source du moteur 5.8 : sous-niveaux abandonnés dans un monde WP, défauts WP corrigés (`CellSize = 12 800 uu`, pas 3 200), casse `HTTPServer`, clé de Map `Int Point` non vérifiée remplacée par un index, `RunId`/`Tick`/`AliveCount` retirés des stats du registre, point ouvert des URLs SYNE sous Launcher, ajout de §5.5 et de §13 (sources de vérité). |
| :-- | :-- | :-- |
| 14 octobre 2026 | Création | Plan de route Blueprint PRISM avant implémentation Unreal. |
| 14 octobre 2026 | Détail exhaustif | Éléments (variables/fonctions/événements), sub-levels + World Partition, pipeline de données, jalons B0–B7 ; décisions tranchées (Character, sub-levels, version lue, reconnexion, obstacles SYNE). |
| 14 octobre 2026 | Registre de monde en C++ | `UGameInstanceSubsystem` n'est pas `Blueprintable` (moteur 5.8), donc `BP_PRISM_WorldSubsystem` dérive de `UPrismLdkWorldRegistryBase` : registre, échelle dérivée et tests en C++ (§6.1.2, §11.4). |
| 14 octobre 2026 | Correction de cohérence | Notation des types Blueprint (§3.1) au lieu des types C++ ; projection déplacée en C++ dans le plugin (`UPrismLdkProjection` + Automation Spec) au lieu de `BML_PRISM_Projection` ; assets dans le Content du plugin et clarification des deux projets Unreal (LDK / PRISM) ; conversion de rayon ; garde `metersPerUnit`/`k` ; détection de changement dans la boucle par tick. |
