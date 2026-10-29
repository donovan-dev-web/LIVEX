// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Données structurées pour PCG (mode Monde Virtuel)

#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "PrismLdkTypes.h"
#include "PrismLdkWorldRegistry.h"
#include "PrismLdkPCGData.generated.h"

UCLASS(BlueprintType)
class PRISMLDK_API UPrismLdkPCGData : public UDataAsset
{
	GENERATED_BODY()

public:
	// Métadonnées
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") FString RunId;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") FString WorldVersion;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") int64 Seed = 0;

	// Échelle / grille
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") int32 CellCountX = 0;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") int32 CellCountY = 0;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") double CellSize = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") double TileSizeUu = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") double K = 0.0;

	// Données brutes utiles au PCG
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") TArray<FPrismSyneWorldObstacle> Obstacles;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") TArray<FPrismSyneWorldCell> Cells;
	/** Masque aplati, index = Y * CellCountX + X ; 1 = marchable, 0 = bloquee. */
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") TArray<uint8> CellWalkable;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") TArray<FPrismSyneResource> Resources;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") TArray<FPrismSyneWorldRegion> Regions;

	// Points prêts pour PCG
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") TArray<FVector> ObstacleCentersUnreal;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") TArray<double> ObstacleRadiiUnreal;

	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") TArray<FIntPoint> WaterResourceCells;
	UPROPERTY(BlueprintReadOnly, Category="Prism|PCG") TArray<FVector> WaterResourceCentersUnreal;

	// Construction
	UFUNCTION(BlueprintCallable, Category="Prism|PCG")
	void BuildFromDescription(const FPrismSyneWorldDescription& World, const FPrismLdkWorldStats& Stats);
};
