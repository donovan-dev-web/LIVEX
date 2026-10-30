// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Role : Generation runtime du "Monde Virtuel" (ile stylisee hand-painted).
//
// Pourquoi du C++ : un terrain de hauteur (heightfield) construit a partir des
// obstacles SYNE demande un maillage, des normales et des UV. Faire cela en
// Blueprint (boucles sur 200x200 sommets) est impraticable et lent. La
// generation deterministe, elle, se code une fois ici.
//
// Contrainte : aucune decision metier. Le plugin ne choisit ni la forme de
// l'ile ni le rendu : il applique des reglages exposes a Blueprint et les
// donnees SYNE (obstacles, ressources water/food/wood/mineral). Le style
// "hand-painted" passe par les materiaux, pas par la geometrie.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "PrismLdkTypes.h"
#include "PrismLdkVirtualWorld.generated.h"

class ANavigationData;
class UNavigationSystemV1;

class UPrismLdkPCGData;
class UProceduralMeshComponent;
class UMaterialInterface;
class UStaticMesh;
class UHierarchicalInstancedStaticMeshComponent;
class UNavModifierComponent;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_ThreeParams(FPrismLdkVirtualBuildProgress, int32, BuildId, FName, Phase, float, Progress);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_ThreeParams(FPrismLdkVirtualBuildCompleted, int32, BuildId, bool, bSuccess, const FString&, Error);

/**
 * Reglages de la vegetation et des props regeneres en C++.
 *
 * Les maillages sont volontairement optionnels : sans maillage, le monde reste
 * valide (terrain + eau + navigation). Les Blueprints peuvent fournir des
 * maillages stylises (arbres, rochers, cultures, roseaux) sans que le plugin
 * connaisse leur contenu.
 */
USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismLdkVegetationSettings
{
	GENERATED_BODY()

	/** Active/désactive tout le scatter (les composants restent vides). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation")
	bool bEnabled = true;

	/** Maillage des arbres (ressources Wood). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation")
	TObjectPtr<UStaticMesh> TreeMesh;

	/** Maillage des rochers / affleurements (ressources Mineral). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation")
	TObjectPtr<UStaticMesh> RockMesh;

	/** Maillage des cultures / buissons (ressources Food). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation")
	TObjectPtr<UStaticMesh> CropMesh;

	/** Maillage des roseaux (ressources Water, rives des lacs). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation")
	TObjectPtr<UStaticMesh> ReedMesh;

	/** Nombre d'arbres vises. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation", meta=(ClampMin="0", ClampMax="50000"))
	int32 TreeCount = 6000;

	/** Nombre de rochers vises. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation", meta=(ClampMin="0", ClampMax="20000"))
	int32 RockCount = 1200;

	/** Nombre de cultures vises. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation", meta=(ClampMin="0", ClampMax="30000"))
	int32 CropCount = 2500;

	/** Nombre de roseaux vises. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation", meta=(ClampMin="0", ClampMax="20000"))
	int32 ReedCount = 800;

	/** Echelle minimale d'une instance. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation", meta=(ClampMin="0.05", ClampMax="10.0"))
	float MinScale = 0.7f;

	/** Echelle maximale d'une instance. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation", meta=(ClampMin="0.05", ClampMax="10.0"))
	float MaxScale = 1.5f;

	/** Graine deterministe du scatter. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Vegetation")
	int32 Seed = 1337;
};

/**
 * Reglages de la generation du monde virtuel.
 *
 * **Contrat de projection** : la grille logique SYNE (`CellCountX x CellCountY`
 * cellules de `TileSizeUu`) occupe exactement le rectangle `[0, WorldSizeX] x
 * [0, WorldSizeY]`. Ce rectangle est la representation de la simulation : tout
 * y est terre ferme (au-dessus de la mer). La mer, la plage et la falaise sont
 * une **decoration hors grille**, dans la bordure `ShoreMargin` ajoutee autour
 * du rectangle.
 *
 * Les distances sont en **unites Unreal** (uu) sauf indication contraire.
 * Les largeurs de bande (Beach/Cliff/Sea) sont des fractions de la demi-largeur
 * de la grille : 0 = au trait de cote, 1 = au centre de la grille.
 */
USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismLdkVirtualWorldSettings
{
	GENERATED_BODY()

	// --- Maillage ---

	/** Subdivisions par cote du heightfield. 384 -> 148 225 sommets. Clamp 16..512. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Maillage", meta=(ClampMin="16", ClampMax="512"))
	int32 GridResolution = 384;

	/**
	 * Subdivisions par chunk (0 = un seul maillage). Le terrain est decoupe en
	 * plusieurs ProceduralMeshComponents pour reduire la taille des buffers et
	 * ameliorer le culling. Les coutures sont exactes : la grille de hauteurs et
	 * de normales est globale.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Maillage", meta=(ClampMin="0", ClampMax="512"))
	int32 TerrainChunkResolution = 64;

	/** Taille, en uu, d'une repetition de texture monde (UV0). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Maillage", meta=(ClampMin="100.0", ClampMax="50000.0"))
	float TerrainUvScale = 2000.0f;

	/** Genere la collision sur le terrain. Necessaire pour le NavMesh. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Maillage")
	bool bCreateCollision = true;

	// --- Forme de l'ile ---

	/**
	 * Bordure de terrain ajoutee autour de la grille logique, en fraction de son
	 * etendue (0.12 = 12 % de chaque cote). C'est la seule zone ou vivent la
	 * falaise, la plage et la mer : la grille elle-meme reste entierement terre.
	 * Doit rester superieur a `BeachWidth + CliffWidth` (sinon la plage mange le
	 * bord de la grille), et assez grande pour que la mer soit visible.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ile", meta=(ClampMin="0.04", ClampMax="1.0"))
	float ShoreMargin = 0.12f;

	/**
	 * Mamelonnement du trait de cote, en fraction de la bordure disponible
	 * (`ShoreMargin` moins les bandes de plage et de falaise). Le deploiement se
	 * fait **vers le large** uniquement : aucune cellule de la grille ne peut
	 * devenir plage ou mer.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ile", meta=(ClampMin="0.0", ClampMax="1.0"))
	float CoastNoiseAmplitude = 0.65f;

	/** Frequence du bruit de cote (1/uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ile", meta=(ClampMin="0.0"))
	float CoastNoiseScale = 0.0000125f;

	/** Poids d'une seconde octave de cote : baies et caps plus larges. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ile", meta=(ClampMin="0.0", ClampMax="1.0"))
	float CoastWarpWeight = 0.35f;

	/** Frequence de la seconde octave de cote (1/uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ile", meta=(ClampMin="0.0"))
	float CoastWarpScale = 0.000004f;

	// --- Profil altimetrique ---

	/** Hauteur du plateau central (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Relief")
	float IslandHeight = 9000.0f;

	/** Hauteur de la falaise qui borde l'ile (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Relief")
	float CliffHeight = 5200.0f;

	/** Hauteur de la plage au pied de la falaise (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Relief")
	float BeachHeight = 350.0f;

	/** Largeur de la bande de plage (fraction normalisee). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Relief", meta=(ClampMin="0.0", ClampMax="0.5"))
	float BeachWidth = 0.055f;

	/** Largeur de la bande de falaise (fraction normalisee). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Relief", meta=(ClampMin="0.001", ClampMax="0.5"))
	float CliffWidth = 0.030f;

	/** Profondeur de la mer autour de l'ile (uu, negative vers le bas). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Relief")
	float SeaDepth = 4000.0f;

	/** Largeur de la transition mer/fond (fraction normalisee). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Relief", meta=(ClampMin="0.001"))
	float SeaWidth = 0.12f;

	/** Amplitude du mamelonnement de la ligne de falaise (fraction de CliffWidth). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Relief", meta=(ClampMin="0.0", ClampMax="1.0"))
	float CliffNoiseAmplitude = 0.25f;

	// --- Obstacles SYNE (montagnes, cretes, amers) ---

	/**
	 * Hauteur du relief additif d'un obstacle, en multiple de son influence.
	 * L'ancien profil absolu (Peak * facteur) rendait les petits obstacles
	 * invisibles et les gros obstacles sous le plateau. Le profil est maintenant
	 * **additif** : base locale + plateau + falaise.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Obstacles", meta=(ClampMin="0.0", ClampMax="4.0"))
	float ObstacleHeightFactor = 0.9f;

	/** Etendue de l'influence d'un obstacle, en multiple de son rayon projete. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Obstacles", meta=(ClampMin="0.1", ClampMax="10.0"))
	float ObstacleInfluenceScale = 1.35f;

	/** Debut de la falaise dans l'influence normalisee (0 = centre, 1 = bord). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Obstacles", meta=(ClampMin="0.0", ClampMax="0.95"))
	float ObstacleCliffStart = 0.45f;

	/** Fin de la falaise dans l'influence normalisee (0 = centre, 1 = bord). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Obstacles", meta=(ClampMin="0.05", ClampMax="1.0"))
	float ObstacleCliffEnd = 0.80f;

	/** Irreguralite du contour d'un obstacle (fraction de l'influence). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Obstacles", meta=(ClampMin="0.0", ClampMax="0.5"))
	float ObstacleOutlineNoise = 0.08f;

	/** Les obstacles peuvent creer de la terre hors de l'ile (cap, ilot). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Obstacles")
	bool bObstaclesCreateLand = true;

	// --- Eau (lacs et etangs) ---

	/**
	 * Rayon d'influence d'un lac, en fraction de la taille de tuile.
	 * Un plancher de 3 % de l'etendue du monde s'applique pour que les lacs
	 * restent visibles quelle que soit l'echelle.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau", meta=(ClampMin="0.05", ClampMax="4.0"))
	float LakeRadiusFraction = 0.38f;

	/** Variation deterministe du rayon d'un lac (+/- fraction). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau", meta=(ClampMin="0.0", ClampMax="0.8"))
	float LakeRadiusJitter = 0.2f;

	/**
	 * Profondeur de la surface de l'eau sous le terrain local (uu).
	 * Le niveau est relatif au relief : un lac sur un plateau n'est pas un
	 * trou de 9 000 uu, c'est une cuvette de la taille du relief environnant.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau", meta=(ClampMin="0.0"))
	float LakeLevel = 150.0f;

	/** Profondeur de la cuvette sous la surface de l'eau (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau", meta=(ClampMin="1.0"))
	float LakeDepth = 700.0f;

	/** Largeur de la berge, en fraction du rayon d'influence (0.05 = falaise, 0.9 = rive douce). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau", meta=(ClampMin="0.05", ClampMax="0.9"))
	float LakeShoreFraction = 0.35f;

	/** Generer la surface de mer autour de l'ile. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau")
	bool bGenerateSea = true;

	// --- Bruit organique ---

	/** Amplitude du bruit de relief (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit")
	float NoiseAmplitude = 1600.0f;

	/** Frequence du bruit de relief (1/uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="0.0"))
	float NoiseScale = 0.0000225f;

	/** Nombre d'octaves du bruit de relief. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="1", ClampMax="8"))
	int32 NoiseOctaves = 5;

	/** Amplitude du domain-warp applique au bruit (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="0.0"))
	float NoiseWarpAmplitude = 2000.0f;

	/** Frequence du domain-warp (1/uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="0.0"))
	float NoiseWarpScale = 0.000012f;

	/** Active les cretes (ridged noise) sur les hauts plateaux et les montagnes. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit")
	bool bUseRidgedMountains = true;

	/** Amplitude des cretes (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="0.0"))
	float RidgeNoiseAmplitude = 600.0f;

	/** Frequence des cretes (1/uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="0.0"))
	float RidgeNoiseScale = 0.000035f;

	/** Amplitude du bruit de detail (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="0.0"))
	float TerrainDetailAmplitude = 250.0f;

	/** Frequence du bruit de detail (1/uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="0.0"))
	float TerrainDetailScale = 0.00009f;

	/** Graine du bruit. Utilise `Seed` du monde SYNE si 0. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit")
	int32 NoiseSeed = 0;

	// --- Ressources et navigation ---

	/** Rayon d'influence d'une ressource, en cellules (couleurs, relief, scatter). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ressources", meta=(ClampMin="0.5", ClampMax="16.0"))
	float ResourceInfluenceRadiusCells = 3.0f;

	/** Relief additif maximal cause par une ressource (uu, 0 = desactive). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ressources", meta=(ClampMin="0.0", ClampMax="1000.0"))
	float ResourceHeightInfluence = 150.0f;

	/** Genere la vegetation/props a partir des ressources (HISM non collisionnants). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ressources")
	bool bGenerateResourceScatter = true;

	/** Vegetation et props. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ressources")
	FPrismLdkVegetationSettings Vegetation;

	/** Genere les volumes NavArea_Null sur les cellules non marchables. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Navigation")
	bool bGenerateNavBlockers = true;

	/** Rebuild le NavMesh apres la generation (asynchrone si le projet le permet). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Navigation")
	bool bRebuildNavigation = true;
};

/** Point d'influence d'une ressource, en uu. */
USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismLdkResourcePoint
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	FVector2D Center = FVector2D::ZeroVector;

	UPROPERTY(BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	EPrismSyneResourceType Type = EPrismSyneResourceType::Unknown;

	UPROPERTY(BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	float Radius = 0.0f;

	UPROPERTY(BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	float Quantity = 0.0f;
};

/**
 * Acteur du "Monde Virtuel" : construit l'ile, les lacs, la mer, la vegetation
 * et les volumes de navigation a partir des donnees SYNE, au runtime, en mode
 * package.
 *
 * Usage attendu (cote Blueprint, dans `BP_PRISM_VirtualRenderer`) :
 *   1. Spawn Actor from Class (PrismLdkVirtualWorldActor)
 *   2. `Build World (PCGData, Settings)` -> terrain, eau, props et navigation
 *   3. `Get Ground Height At (X, Y)` -> pour poser agents, marqueurs et props
 */
UCLASS(BlueprintType, Blueprintable)
class PRISMLDK_API APrismLdkVirtualWorldActor : public AActor
{
	GENERATED_BODY()

public:
	APrismLdkVirtualWorldActor();

	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	virtual void Tick(float DeltaSeconds) override;

	/**
	 * Construit le terrain (ile), les surfaces d'eau (mer + lacs), la vegetation
	 * et les volumes de navigation a partir des donnees SYNE. Idempotent : un
	 * second appel reconstruit et remplace tout ce qui a ete genere.
	 *
	 * @param Data     donnees produites par `Create PCG Data From World Description`
	 * @param Settings reglages de forme, relief, eau, vegetation et navigation
	 * @return vrai si le terrain a ete genere
	 */
	UFUNCTION(BlueprintCallable, Category="PRISM|Monde Virtuel")
	bool BuildWorld(UPrismLdkPCGData* Data, const FPrismLdkVirtualWorldSettings& Settings);

	/** Lance une génération progressive sur plusieurs frames (Game Thread). */
	UFUNCTION(BlueprintCallable, Category="PRISM|Monde Virtuel")
	bool StartBuildWorldAsync(UPrismLdkPCGData* Data, const FPrismLdkVirtualWorldSettings& Settings, int32 BuildId);

	/** Annule la génération en cours si BuildId correspond. */
	UFUNCTION(BlueprintCallable, Category="PRISM|Monde Virtuel")
	void CancelBuildWorldAsync(int32 BuildId);

	UPROPERTY(BlueprintAssignable, Category="PRISM|Monde Virtuel")
	FPrismLdkVirtualBuildProgress OnBuildProgress;

	UPROPERTY(BlueprintAssignable, Category="PRISM|Monde Virtuel")
	FPrismLdkVirtualBuildCompleted OnBuildCompleted;

	/**
	 * Hauteur du terrain (uu) a une position XY donnee. Sert a poser les agents,
	 * la vegetation, les rochers et les marqueurs au ras du sol ou de l'eau.
	 */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	float SampleHeightAt(double X, double Y) const;

	/** Alias explicite de `Sample Height At` pour l'interface BPI_PRISM_Renderer. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel", meta=(DisplayName="Get Ground Height At"))
	float GetGroundHeightAt(double X, double Y) const;

	/** Normale du terrain a une position XY donnee. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	FVector SampleNormalAt(double X, double Y) const;

	/**
	 * Niveau de la surface de l'eau a une position XY donnee : niveau du lac le
	 * plus proche s'il y en a un, sinon 0 (niveau de la mer). Combine a
	 * `Get Ground Height At`, permet d'eviter de poser de la vegetation dans l'eau.
	 */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	float SampleWaterLevelAt(double X, double Y) const;

	/** Force la reconstruction du NavMesh (apres un changement de blockers). */
	UFUNCTION(BlueprintCallable, Category="PRISM|Monde Virtuel")
	void RebuildNavigation();

	/** Vrai une fois `Build World` termine. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	bool IsBuilt() const { return bBuilt; }

	/** Progression du build progressif, dans [0,1]. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	float GetBuildProgress() const { return AsyncBuildProgress; }

	/** Vrai tant qu'une génération progressive est en cours. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	bool IsBuildInProgress() const { return AsyncBuildStage != EAsyncBuildStage::None; }

	/** Phase publiée par le build progressif. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	FName GetBuildPhase() const { return AsyncBuildPhase; }

	/** Nombre de lacs generes (ressources Water positionnees hors mer). */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	int32 GetLakeCount() const { return Lakes.Num(); }

	/** Nombre de chunks de terrain (1 sans decoupage). */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	int32 GetTerrainChunkCount() const { return TerrainChunks.Num(); }

	/** Nombre de volumes de navigation generes. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	int32 GetNavBlockerCount() const { return NavBlockers.Num(); }

	/** Nombre de cellules marquees non marchables par SYNE. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	int32 GetBlockedCellCount() const;

	/** Nombre de points d'influence de ressources. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	int32 GetResourcePointCount() const { return ResourcePoints.Num(); }

	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	int32 GetTreeInstanceCount() const;

	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	int32 GetRockInstanceCount() const;

	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	int32 GetCropInstanceCount() const;

	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	int32 GetReedInstanceCount() const;

	/** Materiau du terrain (override Blueprint du materiau du composant). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="PRISM|Monde Virtuel")
	TObjectPtr<UMaterialInterface> TerrainMaterialOverride;

	/** Materiau de l'eau. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="PRISM|Monde Virtuel")
	TObjectPtr<UMaterialInterface> WaterMaterialOverride;

	/** Premier maillage du terrain (root du terrain et chunk 0). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	TObjectPtr<UProceduralMeshComponent> TerrainMesh;

	/** Maillage des surfaces d'eau (mer + lacs). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	TObjectPtr<UProceduralMeshComponent> WaterMesh;

	/** Tous les chunks de terrain (TerrainMesh inclus). */
	UPROPERTY(VisibleAnywhere, Category="PRISM|Monde Virtuel")
	TArray<TObjectPtr<UProceduralMeshComponent>> TerrainChunks;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	TObjectPtr<UHierarchicalInstancedStaticMeshComponent> TreeInstances;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	TObjectPtr<UHierarchicalInstancedStaticMeshComponent> RockInstances;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	TObjectPtr<UHierarchicalInstancedStaticMeshComponent> CropInstances;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	TObjectPtr<UHierarchicalInstancedStaticMeshComponent> ReedInstances;

	/** Points d'influence des ressources (tous types). */
	UPROPERTY(BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	TArray<FPrismLdkResourcePoint> ResourcePoints;

private:
	/** Hauteur du terrain hors lacs (ile, mer, obstacles, bruit, ressources). */
	double ComputeBaseHeight(double X, double Y) const;

	/**
	 * Distance normalisee au trait de cote : positive sur terre, negative en mer.
	 * 0 = trait de cote. Unite : demi-largeur de la grille (`HalfMinUu`).
	 */
	double CoastDistanceAt(double X, double Y) const;

	/**
	 * Distance au bord du rectangle de la grille, en uu. Positive **dans** la
	 * grille (la simulation), negative dehors (mer / plage / decoration).
	 */
	double GridInlandDistance(double X, double Y) const;

	/** Distance, en uu, du point (X,Y) au bord de la grille dans la direction (DirX,DirY). */
	double GridExitDistance(double X, double Y, double DirX, double DirY) const;

	/** Hauteur du terrain final (base + cuvettes des lacs). */
	double ComputeHeight(double X, double Y) const;

	/** Masque [0,1] d'influence d'un type de ressource. */
	double GetResourceMask(double X, double Y, EPrismSyneResourceType Type) const;

	/** Construit les chunks de terrain (heightfield, normales, couleurs, UV). */
	bool BuildTerrain(const FPrismLdkVirtualWorldSettings& InSettings);

	void TickAsyncBuild();
	bool BuildTerrainChunkFromGrid(int32 ChunkIndex);
	void FinishAsyncBuild(bool bSuccess, const FString& Error);

	/** Construit les surfaces d'eau (mer + lacs) sur le composant `WaterMesh`. */
	void BuildWaterMesh(const FPrismLdkVirtualWorldSettings& InSettings);

	/** Construit la vegetation/props a partir des ressources. */
	void BuildResourceScatter(const FPrismLdkVirtualWorldSettings& InSettings);

	/** Construit les volumes NavArea_Null sur les cellules non marchables; false si un volume échoue. */
	bool BuildNavBlockers(const FPrismLdkVirtualWorldSettings& InSettings);

	/** Capture le NavData attendu et abonne au delegate avant NavigationSystem::Build(). */
	bool BeginNavigationBuild();

	/** Handler du delegate global ; filtre le NavData capture pour le build actif. */
	UFUNCTION()
	void HandleNavigationGenerationFinished(ANavigationData* FinishedNavData);

	/** Desabonne le delegate de navigation, si le build est toujours en attente. */
	void UnbindNavigationBuild();

	/** Valide le NavData attendu sur un point jouable et un blocker, si disponibles. */
	bool ValidateNavigationBuild() const;

	/** Detruit les volumes de navigation generes. */
	void CleanupNavBlockers();

	/** Detruit les chunks autres que `TerrainMesh` et vide le maillage principal. */
	void ResetTerrainChunks();

	/** Cree un chunk enfant de `TerrainMesh`. */
	UProceduralMeshComponent* CreateTerrainChunk();

	/** Vrai si la cellule logique est bloquee. */
	bool IsCellBlocked(int32 X, int32 Y) const;

	/** Bruit fractal (fBm) 2D, dans [-1, 1]. */
	double SampleFbm(double X, double Y) const;

	/** Bruit ridged 2D, dans [0, 1]. */
	double SampleRidged(double X, double Y) const;

	/** Bruit 2D simple, dans [-1, 1]. Utilise pour le deplacement de la cote. */
	double SampleNoise(double X, double Y) const;

	/** Recharge le contexte de generation (donnees + reglages + bruit). */
	void LoadContext(const UPrismLdkPCGData* Data, const FPrismLdkVirtualWorldSettings& Settings);

	bool bBuilt = false;

	enum class EAsyncBuildStage : uint8 { None, Heights, Normals, TerrainChunks, Water, Scatter, NavBlockers, Navigation, Complete };
	EAsyncBuildStage AsyncBuildStage = EAsyncBuildStage::None;
	int32 AsyncBuildId = INDEX_NONE;
	int32 AsyncBuildResolution = 0;
	int32 AsyncBuildVertexCount = 0;
	int32 AsyncBuildRow = 0;
	int32 AsyncBuildChunkResolution = 0;
	int32 AsyncBuildChunksX = 0;
	int32 AsyncBuildChunksY = 0;
	int32 AsyncBuildChunk = 0;
	double AsyncNavigationWaitSeconds = 0.0;
	bool bNavigationGenerationFinished = false;
	bool bNavigationBuildRequested = false;
	UPROPERTY(Transient)
	TObjectPtr<ANavigationData> ExpectedNavData;
	TWeakObjectPtr<UNavigationSystemV1> PendingNavigationSystem;
	FVector NavigationProbePoint = FVector::ZeroVector;
	FVector NavigationProbeExtent = FVector(250.0, 250.0, 1000.0);
	bool bHasNavigationProbe = false;
	float AsyncBuildProgress = 0.0f;
	FName AsyncBuildPhase = NAME_None;
	TArray<double> AsyncHeightGrid;
	TArray<FVector> AsyncNormalGrid;

	// Contexte de generation (membre plutot que parametre : la fonction de
	// hauteur est appelee des milliers de fois, on evite de la reconstruire).

	/** Etendue de la **grille logique** (= CellCount x TileSizeUu), en uu. */
	double WorldSizeX = 1.0;
	double WorldSizeY = 1.0;

	/** Etendue du **terrain** : grille + bordure de mer de chaque cote, en uu. */
	double TerrainSizeX = 1.0;
	double TerrainSizeY = 1.0;
	double TerrainMinX = 0.0;
	double TerrainMinY = 0.0;

	/** Demi-largeur minimale de la grille (unite des bandes Beach/Cliff/Sea). */
	double HalfMinUu = 1.0;

	/** Largeur de la bordure de decoration autour de la grille, en uu. */
	double ShoreMarginUu = 0.0;

	/** Largeur de la bande plage + falaise, hors grille, en uu. */
	double LandBandUu = 0.0;

	/** Marge de mamelonnement disponible pour le trait de cote, en uu. */
	double WobbleUu = 0.0;

	FPrismLdkVirtualWorldSettings Settings;

	/**
	 * Facteur d'echelle verticale : 1 pour un monde de 224 000 uu (70 cellules
	 * de 3 200). Toutes les hauteurs absolues sont multipliees par ce facteur
	 * et les frequences de bruit divisees, pour que le relief garde ses
	 * proportions quand `TileSizeUu` change.
	 */
	double VerticalScale = 1.0;

	/** Obstacles projetes : centre X, centre Y, rayon d'influence (uu). */
	TArray<FVector3d> ObstacleInfluences;

	/** Lacs : centre X, centre Y, rayon d'influence (uu). */
	TArray<FVector3d> Lakes;

	/** Niveau de la surface de chaque lac, meme index que `Lakes` (uu). */
	TArray<double> LakeWaterLevels;

	/** Taille d'une cellule logique en uu. */
	double CellSizeUu = 1.0;

	/** Cellules logiques (1 = bloquee). Index = Y * CellCountX + X. */
	TArray<uint8> CellBlocked;
	int32 CellCountX = 0;
	int32 CellCountY = 0;

	/** Volumes de navigation generes (AActor + UNavModifierComponent). */
	UPROPERTY()
	TArray<TObjectPtr<AActor>> NavBlockers;

	/**
	 * Decalage de graine applique aux coordonnees avant echantillonnage.
	 * Deux graines distinctes donnent donc deux champs de bruit distincts,
	 * sans table de bruit externe.
	 */
	FVector2D NoiseOffset = FVector2D::ZeroVector;
	FVector2D CoastNoiseOffset = FVector2D::ZeroVector;
	FVector2D WarpOffset = FVector2D::ZeroVector;
	FVector2D RidgeOffset = FVector2D::ZeroVector;
	FVector2D DetailOffset = FVector2D::ZeroVector;
};
