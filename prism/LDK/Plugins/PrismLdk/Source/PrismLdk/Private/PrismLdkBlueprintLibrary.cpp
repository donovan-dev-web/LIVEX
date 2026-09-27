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
