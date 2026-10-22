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

### 3.3 `APrismLdkVirtualWorldActor` — Priorité HAUTE — **FAIT** (ajout du 22/10)

`Public/PrismLdkVirtualWorld.h` / `Private/PrismLdkVirtualWorld.cpp`.

**Pourquoi du C++** : un heightfield de 37 000 à 262 000 sommets, avec normales, tangentes, UV et collision, ne se fait pas en Blueprint (boucles lentes, pas de tangentes, pas de collision). La génération est **déterministe** et se code une fois.

**Contenu** :
- `FPrismLdkVirtualWorldSettings` (USTRUCT BlueprintType) : tous les réglages de forme et de relief, éditables depuis Blueprint.
- Génération du maillage via `UProceduralMeshComponent` (section unique, collision optionnelle).
- Profil altimétrique : mer → plage → falaise → plateau, largeurs normalisées.
- **Obstacles SYNE → relief** : le rayon SYNE projeté donne l'étendue d'influence, la hauteur en découle (gros obstacle = montagne, petit = amer).
- Bruit organique fBm déterministe, seedé par la graine SYNE.
- **Lacs** : une cuvette par ressource `Water` positionnée ; surface d'eau au niveau `LakeLevel`.
- **Mer** : disque irrégulier débordant du monde.
- API Blueprint : `BuildWorld`, `SampleHeightAt`, `SampleNormalAt`, `IsBuilt`, overrides de matériaux.

**Bruit** : `FMath::PerlinNoise2D` (Core) avec décalage de graine — aucune table de bruit externe, donc aucune dépendance à une version d'engine, et un résultat identique sur tout build.

### 3.4 `PrismLdk.Build.cs` — **FAIT**

Dépendances ajoutées : `PCG`, `ProceduralMeshComponent`.

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
| `PrismLdk.Build.cs` | Modifié | `PCG`, `ProceduralMeshComponent` |
| `Private/Tests/PrismLdkPCGDataSpec.cpp` | Créé | Specs DataAsset |
| `Private/Tests/PrismLdkVirtualWorldSpec.cpp` | Créé | Specs terrain virtuel |

---

## 5) Tests

| Spec | Vérifie |
| :-- | :-- |
| `PrismLdk.PCG.Data.BuildFromDescription.Basic` | Le DataAsset se construit, compte les cellules, projette les obstacles et les ressources `Water` |
| `PrismLdk.VirtualWorld.BuildsIsland` | `BuildWorld` génère un terrain, le centre est plus haut que le bord, le coin est sous la mer, le lac est creusé, la normale est unitaire |
| `PrismLdk.VirtualWorld.RejectsInvalidData` | `BuildWorld` avec des données invalides renvoie `false` sans planter |

Lancement : **Tools → Session Frontend → Automation → Run Tests**.

**État au 22/10/2026** : compilation `LDKEditor Win64 Development` réussie, **79 specs exécutées, 79 succès**, dont les 3 specs ci-dessus.

Commande de lancement en ligne de commande (PowerShell, attention au quoting) :

```powershell
$argLine = '"D:\LIVEX\prism\LDK\LDK.uproject" -ExecCmds="Automation RunTests PrismLdk;Automation Quit" -unattended -nop4 -nosplash -windowed -ResX=1280 -ResY=720 -log -NoSound'
Start-Process -FilePath "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe" -ArgumentList $argLine -WorkingDirectory "D:\LIVEX\prism\LDK"
```

> Ne pas utiliser `-TestExit="Automation Test Queue Empty"` : la file est vide au démarrage et l'éditeur quitte avant de lancer les tests. Utiliser `Automation Quit`, qui est mis en file après les tests.

---

## 6) Ce qui reste à faire côté Blueprint

Le C++ est prêt. La suite est du Blueprint, détaillée dans **« PRISM - Guide pas a pas Monde Virtuel »** :

1. `BPI_PRISM_Renderer` + `BP_PRISM_RendererBase` + `BP_PRISM_LogicalRenderer` + `BP_PRISM_VirtualRenderer`.
2. `BP_PRISM_WorldDirector` : choisir le renderer selon `GI.SelectedRenderMode`, `Initialize` + `Build`, ne signaler `Ready` qu'après la fin du rendu.
3. `MenuLevel` + `WBP_PRISM_MainMenu` (connexion, configuration, deux boutons).
4. Végétation, style, NavMesh, packaging.

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
| Facteur `0.7071` sur le blob | l'île couvre les bords, il n'y a presque pas de mer | norme de superellipse brute : 1 au milieu d'un bord, ~1.33 au coin ; `IslandRadius = 0.92` laisse alors 8 960 uu de mer sur le pourtour |
