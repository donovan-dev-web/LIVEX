# PRISM - Roadmap C++ pour le mode Monde Virtuel (PrismLdk)

*Date : 22 octobre 2026*  
*Objectif : Ajouter le strict nécessaire côté C++ au plugin PrismLdk pour alimenter proprement un rendu stylisé hand-painted (terrain procédural + PCG) tout en préservant intégralement le contrat SYNE existant.*

> Règle : on ne touche **pas** au transport (PrismLdkSubsystem), aux types (PrismLdkTypes) ni au registre dans son fonctionnement existant. On ajoute uniquement ce qui est justifié pour le rendu procédural stylisé.

---

## 1) État actuel (à conserver)

- **Transport** : `UPrismLdkSubsystem` gère HTTP/WS, dispatchers (`OnWorldInitialized`, `OnSnapshot`, `OnWorldDelta`, `OnSyneEvent`, `OnControlResult`, `OnError`).
- **Projection** : `UPrismLdkProjection` + helpers (`SyneToUnreal`, `CellToTileCenter`, `SyneRadiusToUnreal`) — aucune formule dans BP.
- **Registre** : `UPrismLdkWorldRegistryBase` (concret, instancié par GameInstanceSubsystem) stocke Id→Actor, dérive `k` depuis `WorldDescription` + `TileSizeUu`, `ClearAll` préserve `TileSizeUu`.
- **Données** : `FPrismSyneWorldDescription` contient `Cells[]`, `Obstacles[]` (pos+rayon), `Resources[]` (Water/Food/Wood/Mineral), `Agents[]`, `Regions[]`. `FPrismSyneWorldSnapshot` contient l'état courant.

Le rendu logique actuel (`BP_PRISM_TileField` HISM + cylindres + marqueurs) fonctionne très bien. On l'encapsule dans un **Renderer** sans casser ce flux.

---

## 2) Objectifs C++

Fournir à Blueprints/PCG un accès structuré aux données nécessaires pour :
- Générer un **relief stylisé hand-painted** à partir des obstacles (`FPrismSyneWorldObstacle` pos+radius) et de la grille logique.
- Placer de l'**eau** sous forme de lacs/étangs uniquement là où il y a des ressources `Water`.
- Alimenter **PCG** avec des Points, Data ou un DataAsset prêt à consommer.
- Aider à la cohérence avec la grille logique (pour NavMesh / placement agents) sans dupliquer la logique métier.
- Rester compatible **build packagé Windows/Linux**.

**Principe directeur** : le C++ ne choisit **aucune** décision métier. Il applique des réglages exposés à Blueprint à des données SYNE. Le style « hand-painted » passe par les matériaux, pas par la géométrie.

---

## 3) Ce qui a été livré (état au 22/10/2026)

### 3.1 `UPrismLdkPCGData` — Priorité HAUTE — **FAIT**

`Public/PrismLdkPCGData.h` / `Private/PrismLdkPCGData.cpp`. Hérite de `UDataAsset`.

Contient :
- métadonnées (`CellCountX/Y`, `CellSize`, `TileSizeUu`, `Seed`, `RunId`, `WorldName`) ;
- données brutes (cellules, obstacles, ressources) ;
- tableaux **projetés** prêts à consommer : `ObstacleCentersUnreal`, `ObstacleRadiiUnreal`, `WaterResourceCells`, `WaterResourceCentersUnreal`.

Fonction `BuildFromDescription(const FPrismSyneWorldDescription& World, const FPrismLdkWorldStats& Stats)`.

### 3.2 Extension `UPrismLdkBlueprintLibrary` — Priorité HAUTE — **FAIT**

| Fonction | Rôle |
| :-- | :-- |
| `CreatePCGDataFromWorldDescription(WorldDescription, WorldContext)` | Construit le `PCGData` |
| `GetObstaclesUnreal(WorldDescription, WorldContext)` | Obstacles projetés (centres + rayons) |
| `GetWaterResourceCells(WorldDescription, WorldContext)` | Cellules de ressources `Water` positionnées |
| `CellToTileCenterUnreal(Cell, WorldContext)` | Centre de tuile via le registre |
| `GetPlayableBoundsUnreal(WorldContext)` | Bornes jouables (NavMesh, PCG Volume) |

### 3.3 `APrismLdkVirtualWorldActor` — **FAIT, à valider sous Unreal 5.8** (ajout du 22/10)

`Public/PrismLdkVirtualWorld.h` / `Private/PrismLdkVirtualWorld.cpp`.

**Pourquoi du C++** : un heightfield de 37 000 à 262 000 sommets, avec normales, tangentes, UV et collision, ne se fait pas en Blueprint (boucles lentes, pas de tangentes, pas de collision). La génération est **déterministe** et se code une fois.

**Contenu** :
- `FPrismLdkVirtualWorldSettings` (USTRUCT BlueprintType) : réglages de terrain, eau, végétation et navigation, éditables depuis Blueprint.
- Le contexte reset ses tableaux projetés et dérive le masque bloqué depuis `CellWalkable` validé (grille complète, coordonnées uniques et dans les limites) ou depuis les obstacles si la grille manque ou est malformée. `CellWalkable` absent signifie fallback possible, jamais « tout marchable ».
- Génération du maillage par `UProceduralMeshComponent`, découpé en composants/chunks, collision configurable.
- **Contrat de projection** : la grille logique SYNE occupe exactement le rectangle `[0, W] × [0, H]` et reste **entièrement terre ferme**. La falaise, la plage et la mer sont une décoration **hors grille**, dans une bordure `ShoreMargin` (12 % par défaut). Le maillage affiché = grille + 2 × bordure.
- Profil altimétrique : mer → plage → falaise → plateau, piloté par la distance signée (SDF) au rectangle de la grille ; largeurs normalisées par la demi-largeur de la grille.
- **Obstacles SYNE → relief** : le rayon SYNE projeté donne l'étendue d'influence, la hauteur en découle (gros obstacle = montagne, petit = amer).
- Bruit organique fBm déterministe, seedé par la graine SYNE.
- **Lacs** : une cuvette par ressource `Water` positionnée, **à sa cellule projetée** (jamais déplacée) ; le niveau d'eau est **relatif au relief local** (`terrain local − LakeLevel`), la cuvette descend de `LakeDepth` sous cette surface, et la nappe est rognée au bord de la grille.
- **Mer** : disque irrégulier débordant du maillage, en anneau hors grille.
- **Échelle** : toutes les hauteurs suivent `TileSizeUu` (`VerticalScale = étendue / 224 000`), les fréquences de bruit font l'inverse.
- API Blueprint synchrone : `BuildWorld`, `SampleHeightAt`, `SampleNormalAt`, `SampleWaterLevelAt`, `GetLakeCount`, `IsBuilt`. API progressive multi-frame : `StartBuildWorldAsync`, `CancelBuildWorldAsync`, `OnBuildProgress`, `OnBuildCompleted`; la fin inclut le contrôle NavMesh ciblé quand `bRebuildNavigation` est actif. Overrides de matériaux.

**Bruit** : `FMath::PerlinNoise2D` (Core) avec décalage de graine — aucune table de bruit externe, donc aucune dépendance à une version d'engine, et un résultat identique sur tout build.

### 3.4 `PrismLdk.Build.cs` — **FAIT**

Dépendances ajoutées pour les fonctionnalités effectivement référencées : `NavigationSystem`, `ProceduralMeshComponent`. Le module `PCG` n'est pas requis par cette implémentation (les instances de végétation utilisent HISM).

### 3.5 Registre — **INCHANGÉ**

Aucune modification nécessaire. `k` reste dérivé du registre, jamais d'une variable Blueprint.

---

## 4) Fichiers

| Fichier | Action | Détails |
| :-- | :-- | :-- |
| `Public/PrismLdkPCGData.h` | Créé | DataAsset PCG |
| `Private/PrismLdkPCGData.cpp` | Créé | Implémentation |
| `Public/PrismLdkVirtualWorld.h` | Créé | Settings + acteur du monde virtuel |
| `Private/PrismLdkVirtualWorld.cpp` | Créé | Heightfield, relief, eau |
| `Public/PrismLdkBlueprintLibrary.h` | Modifié | Nouvelles fonctions |
| `Private/PrismLdkBlueprintLibrary.cpp` | Modifié | Implémentation |
| `PrismLdk.Build.cs` | Modifié | `NavigationSystem`, `ProceduralMeshComponent` |
| `Private/Tests/PrismLdkPCGDataSpec.cpp` | Créé | Specs DataAsset |
| `Private/Tests/PrismLdkVirtualWorldSpec.cpp` | Créé | Specs terrain virtuel |

---

## 5) Tests

| Spec | Vérifie |
| :-- | :-- |
| `PrismLdk.PCG.Data.BuildFromDescription.Basic` | Le DataAsset se construit, compte les cellules, projette les obstacles et les ressources `Water` |
| `PrismLdk.PCG.Data.BuildFromDescription.CellMaskAndWorldPosition` | Masque valide, coordonnées `Water` monde→cellule et rejet d'une grille incomplète/malformée |
| `PrismLdk.VirtualWorld.BuildsIsland` | `BuildWorld` génère un terrain, **tous les centres de cellules sont sur la terre ferme**, la mer est hors grille, le centre est plus haut que le bord, le lac est creusé sous **sa** surface d'eau, et les obstacles restaurent les blockers en cas de grille malformée |
| `PrismLdk.VirtualWorld.ScalesWithTileSize` | Un monde 10× plus petit (même `CellSize`, `K` divisé par 10) donne des hauteurs 10× plus petites, mer et lac compris |
| `PrismLdk.VirtualWorld.LakeStaysOnGrid` | Une ressource `Water` sur la cellule extrême `(0, 0)` garde la position de sa cellule projetée, produit un lac immergé dont même le fond reste au-dessus de la mer |
| `PrismLdk.VirtualWorld.AsyncBuildLifecycle` | Progression multi-frame, terminaison complète et tick arrêté |
| `PrismLdk.VirtualWorld.AsyncBuildCancellation` | Annulation avec ID correct, ID étranger ignoré et nettoyage |
| `PrismLdk.VirtualWorld.RejectsInvalidData` | `BuildWorld` avec des données invalides renvoie `false` sans planter |

Lancement : **Tools → Session Frontend → Automation → Run Tests**. Vérifier également dans l'éditeur un build réel avec collision et `bRebuildNavigation=true`; les Automation Specs sans monde/nav configuré ne prouvent pas le fonctionnement NavMesh.

**Historique seulement — 23/10/2026** : une ancienne validation locale rapportait une compilation `LDKEditor Win64 Development` réussie et 81 specs passées à cette date. Elle ne valide pas les modifications présentes dans le checkout actuel; les specs asynchrones ci-dessus doivent être relancées dans l'éditeur Unreal après recompilation.

Commande de lancement en ligne de commande (PowerShell, attention au quoting) :

```powershell
$argLine = '"D:\LIVEX\prism\LDK\LDK.uproject" -ExecCmds="Automation RunTests PrismLdk;Automation Quit" -unattended -nop4 -nosplash -windowed -ResX=1280 -ResY=720 -log -NoSound'
Start-Process -FilePath "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe" -ArgumentList $argLine -WorkingDirectory "D:\LIVEX\prism\LDK"
```

> Ne pas utiliser `-TestExit="Automation Test Queue Empty"` : la file est vide au démarrage et l'éditeur quitte avant de lancer les tests. Utiliser `Automation Quit`, qui est mis en file après les tests.

---

## 6) État Blueprint et travaux de validation restants

Le guide **« PRISM - Guide pas a pas Monde Virtuel »** décrit le flux cible et les graphes à construire. Les assets Blueprint présents dans le dépôt ne prouvent pas que tous ces graphes sont câblés ou testés dans l'éditeur.

1. Vérifier le contrat `BPI_PRISM_Renderer` + `BP_PRISM_RendererBase` + `BP_PRISM_LogicalRenderer` + `BP_PRISM_VirtualRenderer` dans l'éditeur.
2. Vérifier que `BP_PRISM_WorldDirector` attend effectivement le callback terminal du renderer avant de demander `Ready`.
3. Implémenter/valider le flux persistant `LDK_MenuMap` + `WBP_PRISM_MainMenu` + `WBP_PRISM_Loading`, streaming des deux modes, retour menu et gestion d'annulation selon le guide V7.
4. Vérifier le NavMesh en sous-niveau streamé, la navigation des agents, les matériaux/meshes et le package Windows/Linux.
5. Recompiler et relancer toutes les Automation Specs après les corrections; une validation antérieure ne remplace pas cette passe.

---

## 7) Pièges techniques rencontrés (à connaître)

| Piège | Symptôme | Solution |
| :-- | :-- | :-- |
| `TUniquePtr<FFastNoiseLite>` avec type incomplet | `error C4150: suppression de pointeur vers type incomplet` | `FFastNoiseLite` n'existe plus en 5.8 → bruit maison sur `FMath::PerlinNoise2D` |
| `Math/Noise.h` | `fatal error C1083: fichier include introuvable` | Ce chemin n'existe pas en 5.8 |
| `TArray<FRotator>` pour les tangentes | erreur de signature | UE 5.8 attend `TArray<FProcMeshTangent>` (`TangentX` + `bFlipTangentY`) |
| `Tangents.Last()` sur un tableau vide | crash au premier sommet | toujours utiliser la valeur locale |
| `std::cos` / `std::sin` | include `<cmath>` manquant | utiliser `FMath::Cos` / `FMath::Sin` |
| `LogPrismLdk` non déclaré | erreur de compilation | `LogPrismLdk` est dans `PrismLdkSubsystem.h` |
| `Game Instance Class` non réglé dans Project Settings | `Accessed None` sur le GameInstance | le régler dans **Maps & Modes**, pas sur le GameMode |
| `Ready` envoyé deux fois | `409 world_not_ready` | un seul `RequestReady`, déclenché par le signal de fin de rendu |
| Île plus petite que la grille (blob `IslandRadius = 0.92`) | les ressources `Water` tombent en mer ou sur la plage, le bord de la grille est sous l'eau : agents dans l'eau = contrat de projection violé | la grille **est** l'île : `GridInlandDistance` (SDF du rectangle) + bordure `ShoreMargin` de décoration ; le trait de côte est à `BeachWidth + CliffWidth` **hors** du rectangle, mamelonné vers le large uniquement |
| Winding des disques d'eau | mer et lacs invisibles sauf avec un matériau Two Sided | face avant Unreal = ordre **horaire vu du dessus** : `Base, Base+2+i, Base+1+i` |
| Hauteurs absolues (9 000, 4 000, 1 400 uu) | relief écrasé quand `TileSizeUu` change | `VerticalScale = max(WorldSizeX, WorldSizeY) / 224 000` appliqué aux hauteurs, `1 / VerticalScale` aux `NoiseScale`/`CoastNoiseScale` |
| `LakeLevel` absolu (400 uu) | aucun lac visible : la cuvette vaut 400 uu sous un plateau à 9 000 uu, la berge est aplatie au niveau de l'eau (z-fighting), la surface est coplanaire | niveau **relatif par lac** : `terrain local − LakeLevel`, cuvette de `LakeDepth` sous cette surface, berge sur les derniers `LakeShoreFraction` du rayon |
| Rayon de lac = `0.38 × TileSizeUu` | lacs de 1 200 uu sur un monde de 224 000 (invisibles) | plancher à 3 % de l'étendue du monde ; disque d'eau à `0.68 ×` le rayon d'influence |
| Ressources `Water` du monde SYNE sans notion de côte | `Monde virtuel genere : … 0 lacs` alors que `resources=12` : l'ancien modèle (île plus petite que la grille) plaçait les 3 cellules water en mer ou sur la plage, et le filtre les jetait toutes | ne rien filtrer ni déplacer : toute cellule est sur la terre ferme. La position du lac est **exactement** `(cellule + 0.5) × K` ; une position hors grille ne peut venir que d'une projection incohérente |
| Nappe d'eau qui déborde de la grille | un lac sur une cellule du bord flotte au-dessus de la falaise | disque rogné par `GridExitDistance` par segment, cuvette estompée à `0.5 × rayon` hors grille |
| Lac sur une pente | anneau sec sous le niveau de l'eau, côté aval | remonter la berge à `surface + LakeLevel × 0.5` quand le terrain local est plus bas : `Lerp(max(Height, Bank), Bowl, Mask)` |
| `TileSizeUu` incohérent avec `CellSize × K` | les lacs/obstacles ne tombent pas sur le terrain | `K` est **dérivé** (`TileSizeUu / CellSize`) par le registre ; ne jamais le fixer à la main en parallèle |
| Tests lancés via `Start-Process` + `WaitForExit` | la commande PowerShell ne rend pas la main (code de sortie `0xC000013A`) | rediriger la sortie (`-RedirectStandardOutput`) ou lancer puis lire `Saved/Logs/LDK.log` |
