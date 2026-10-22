// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Bibliothèque de fonctions Blueprint donnant accès au sous-système PrismLdk.

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "PrismLdkTypes.h"
#include "PrismLdkWorldRegistry.h"
#include "PrismLdkBlueprintLibrary.generated.h"

class UPrismLdkSubsystem;
class UPrismLdkWorldRegistryBase;
class UPrismLdkPCGData;

UCLASS()
class PRISMLDK_API UPrismLdkBlueprintLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	/**
	 * Returns the PrismLdk subsystem owned by the current game instance.
	 * The returned object is valid after the game instance has been created.
	 */
	UFUNCTION(BlueprintPure, Category="SYNE|Connection", meta=(WorldContext="WorldContextObject", CompactNodeTitle="SYNE"))
	static UPrismLdkSubsystem* GetPrismLdkSubsystem(const UObject* WorldContextObject);

	/**
	 * Renvoie le registre de monde PRISM de la GameInstance courante, ou
	 * `nullptr` si le contexte n'est pas dans un monde joué.
	 *
	 * Le registre est un sous-système C++ **concret** du plugin : sa classe est
	 * chargée avec le module, donc l'instance existe dès qu'une GameInstance
	 * existe, sans dépendre du chargement d'un asset.
	 */
	UFUNCTION(BlueprintPure, Category="PRISM|Registry", meta=(WorldContext="WorldContextObject", CompactNodeTitle="PRISM Registry"))
	static UPrismLdkWorldRegistryBase* GetPrismLdkWorldRegistry(const UObject* WorldContextObject);

	// === Monde Virtuel / PCG ===

	/** Crée et remplit un UPrismLdkPCGData à partir de la description SYNE et du registre courant. */
	UFUNCTION(BlueprintCallable, Category="PRISM|PCG", meta=(WorldContext="WorldContextObject"))
	static UPrismLdkPCGData* CreatePCGDataFromWorldDescription(const UObject* WorldContextObject, const FPrismSyneWorldDescription& WorldDescription);

	/** Retourne les obstacles projetés en Unreal (centres + rayons). */
	UFUNCTION(BlueprintPure, Category="PRISM|PCG", meta=(WorldContext="WorldContextObject"))
	static void GetObstaclesUnreal(const UObject* WorldContextObject, const FPrismSyneWorldDescription& WorldDescription, TArray<FVector>& OutCenters, TArray<double>& OutRadii);

	/** Retourne les cellules de ressources Water ayant une position. */
	UFUNCTION(BlueprintPure, Category="PRISM|PCG", meta=(WorldContext="WorldContextObject"))
	static TArray<FIntPoint> GetWaterResourceCells(const UObject* WorldContextObject, const FPrismSyneWorldDescription& WorldDescription);

	/** Centre de tuile en unités Unreal depuis une cellule (via registre). */
	UFUNCTION(BlueprintPure, Category="PRISM|PCG", meta=(WorldContext="WorldContextObject"))
	static FVector CellToTileCenterUnreal(const UObject* WorldContextObject, FIntPoint Cell);

	/** Bornes jouables en unités Unreal (CellCountX*TileSizeUu, CellCountY*TileSizeUu). */
	UFUNCTION(BlueprintPure, Category="PRISM|PCG", meta=(WorldContext="WorldContextObject"))
	static FBox GetPlayableBoundsUnreal(const UObject* WorldContextObject);
};
