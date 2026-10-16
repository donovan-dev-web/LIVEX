// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Bibliothèque de fonctions Blueprint donnant accès au sous-système PrismLdk.

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "PrismLdkBlueprintLibrary.generated.h"

class UPrismLdkSubsystem;
class UPrismLdkWorldRegistryBase;

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
};
