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
// donnees SYNE (obstacles, ressources water). Le style "hand-painted" passe
// par les materiaux, pas par la geometrie.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "PrismLdkVirtualWorld.generated.h"

class UPrismLdkPCGData;
class UProceduralMeshComponent;
class UMaterialInterface;

/**
 * Reglages de la generation du monde virtuel.
 *
 * Les distances sont en **unites Unreal** (uu) sauf indication contraire.
 * Les largeurs de bande (Beach/Cliff/Sea) sont des fractions normalisees de
 * la distance a la cote : 0 = a la cote, 1 = au centre de l'ile.
 */
USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismLdkVirtualWorldSettings
{
	GENERATED_BODY()

	// --- Maillage ---

	/** Subdivisions par cote du heightfield. 192 -> 37 249 sommets. Clamp 16..512. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Maillage", meta=(ClampMin="16", ClampMax="512"))
	int32 GridResolution = 192;

	/** Genere la collision sur le terrain. Necessaire pour le NavMesh. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Maillage")
	bool bCreateCollision = true;

	// --- Forme de l'ile ---

	/** Puissance du "blob" : 2 = ellipse, 4 = carre arrondi, 1.5 = losange. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ile", meta=(ClampMin="1.0", ClampMax="8.0"))
	float BlobPower = 2.4f;

	/** Rayon de l'ile, en fraction de la demi-diagonale du monde (0 = nowhere, 1 = tout). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ile", meta=(ClampMin="0.05", ClampMax="1.5"))
	float IslandRadius = 0.92f;

	/** Amplitude du deplacement de la cote par bruit (en fraction normalisee). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ile", meta=(ClampMin="0.0", ClampMax="0.5"))
	float CoastNoiseAmplitude = 0.12f;

	/** Frequence du bruit de cote (1/uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Ile", meta=(ClampMin="0.0"))
	float CoastNoiseScale = 0.0000125f;

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

	// --- Obstacles SYNE (montagnes, cretes, amers) ---

	/** Facteur reliant le rayon SYNE projete a la hauteur du relief. 0 = aucun relief. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Obstacles")
	float ObstacleHeightFactor = 0.45f;

	/** Etendue de l'influence d'un obstacle, en multiple de son rayon projete. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Obstacles", meta=(ClampMin="0.1", ClampMax="10.0"))
	float ObstacleInfluenceScale = 1.35f;

	/** Les obstacles peuvent creer de la terre hors de l'ile (cap, ilot). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Obstacles")
	bool bObstaclesCreateLand = true;

	// --- Eau (lacs et etangs) ---

	/** Rayon d'un lac, en fraction de la taille de tuile. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau", meta=(ClampMin="0.05", ClampMax="1.0"))
	float LakeRadiusFraction = 0.38f;

	/** Niveau de la surface des lacs (uu). Au-dessus du niveau de la mer. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau")
	float LakeLevel = 400.0f;

	/** Profondeur des lacs, au point le plus bas (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau")
	float LakeDepth = 900.0f;

	/** Hauteur de la plage autour d'un lac (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau")
	float LakeShoreHeight = 700.0f;

	/** Generer la surface de mer autour de l'ile. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Eau")
	bool bGenerateSea = true;

	// --- Bruit organique ---

	/** Amplitude du bruit de relief (uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit")
	float NoiseAmplitude = 1400.0f;

	/** Frequence du bruit de relief (1/uu). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="0.0"))
	float NoiseScale = 0.0000225f;

	/** Nombre d'octaves du bruit de relief. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit", meta=(ClampMin="1", ClampMax="8"))
	int32 NoiseOctaves = 4;

	/** Graine du bruit. Utilise `Seed` du monde SYNE si 0. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Bruit")
	int32 NoiseSeed = 0;
};

/**
 * Acteur du "Monde Virtuel" : construit l'ile, les lacs et la mer a partir des
 * donnees SYNE, au runtime, en mode package.
 *
 * Usage attendu (cote Blueprint, dans `BP_PRISM_VirtualRenderer`) :
 *   1. Spawn Actor from Class (PrismLdkVirtualWorldActor)
 *   2. `Build World (PCGData, Settings)` -> le terrain et l'eau apparaissent
 *   3. `Sample Height At (X, Y)` -> pour poser la vegetation au bon niveau
 */
UCLASS(BlueprintType, Blueprintable)
class PRISMLDK_API APrismLdkVirtualWorldActor : public AActor
{
	GENERATED_BODY()

public:
	APrismLdkVirtualWorldActor();

	/**
	 * Construit le terrain (ile) et les surfaces d'eau (mer + lacs) a partir des
	 * donnees SYNE. Idempotent : un second appel reconstruit et remplace les
	 * maillages.
	 *
	 * @param Data    donnees produites par `Create PCG Data From World Description`
	 * @param Settings reglages de forme et de relief
	 * @return vrai si le terrain a ete genere
	 */
	UFUNCTION(BlueprintCallable, Category="PRISM|Monde Virtuel")
	bool BuildWorld(UPrismLdkPCGData* Data, const FPrismLdkVirtualWorldSettings& Settings);

	/**
	 * Hauteur du terrain (uu) a une position XY donnee. Sert a poser la
	 * vegetation, les rochers et les marqueurs au ras du sol.
	 */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	float SampleHeightAt(double X, double Y) const;

	/** Normale du terrain a une position XY donnee. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	FVector SampleNormalAt(double X, double Y) const;

	/** Vrai une fois `Build World` termine. */
	UFUNCTION(BlueprintPure, Category="PRISM|Monde Virtuel")
	bool IsBuilt() const { return bBuilt; }

	/** Materiau du terrain (override Blueprint du materiau du composant). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="PRISM|Monde Virtuel")
	TObjectPtr<UMaterialInterface> TerrainMaterialOverride;

	/** Materiau de l'eau. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="PRISM|Monde Virtuel")
	TObjectPtr<UMaterialInterface> WaterMaterialOverride;

	/** Maillage du terrain. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	TObjectPtr<UProceduralMeshComponent> TerrainMesh;

	/** Maillage des surfaces d'eau (mer + lacs). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="PRISM|Monde Virtuel")
	TObjectPtr<UProceduralMeshComponent> WaterMesh;

private:
	/** Hauteur du terrain, en fonction du contexte de generation courant. */
	double ComputeHeight(double X, double Y) const;

	/** Construit les surfaces d'eau (mer + lacs) sur le composant `WaterMesh`. */
	void BuildWaterMesh(const FPrismLdkVirtualWorldSettings& Settings);

	/** Bruit fractal (fBm) 2D, dans [-1, 1]. */
	double SampleFbm(double X, double Y) const;

	/** Bruit 2D simple, dans [-1, 1]. Utilise pour le deplacement de la cote. */
	double SampleNoise(double X, double Y) const;

	/** Recharge le contexte de generation (donnees + reglages + bruit). */
	void LoadContext(const UPrismLdkPCGData* Data, const FPrismLdkVirtualWorldSettings& Settings);

	bool bBuilt = false;

	// Contexte de generation (membre plutot que parametre : la fonction de
	// hauteur est appelee des milliers de fois, on evite de la reconstruire).
	double WorldSizeX = 1.0;
	double WorldSizeY = 1.0;
	FPrismLdkVirtualWorldSettings Settings;

	/** Obstacles projetes : centre X, centre Y, rayon d'influence (uu). */
	TArray<FVector3d> ObstacleInfluences;

	/** Lacs : centre X, centre Y, rayon (uu). */
	TArray<FVector3d> Lakes;

	/**
	 * Decalage de graine applique aux coordonnees avant echantillonnage.
	 * Deux graines distinctes donnent donc deux champs de bruit distincts,
	 * sans table de bruit externe.
	 */
	FVector2D NoiseOffset = FVector2D::ZeroVector;
	FVector2D CoastNoiseOffset = FVector2D::ZeroVector;
};
