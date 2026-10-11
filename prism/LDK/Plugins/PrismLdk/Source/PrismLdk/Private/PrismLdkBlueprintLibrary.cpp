// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Fonctions Blueprint exposant le sous-système PrismLdk aux plans Unreal.

#include "PrismLdkBlueprintLibrary.h"
#include "PrismLdk.h"
#include "PrismLdkSubsystem.h"

#include "Engine/GameInstance.h"

UPrismLdkSubsystem* UPrismLdkBlueprintLibrary::GetPrismLdkSubsystem(const UObject* WorldContextObject)
{
	if (!IsValid(WorldContextObject))
	{
		return nullptr;
	}

	const UWorld* World = WorldContextObject->GetWorld();
	if (!IsValid(World))
	{
		return nullptr;
	}

	UGameInstance* GameInstance = World->GetGameInstance();
	return IsValid(GameInstance) ? GameInstance->GetSubsystem<UPrismLdkSubsystem>() : nullptr;
}
