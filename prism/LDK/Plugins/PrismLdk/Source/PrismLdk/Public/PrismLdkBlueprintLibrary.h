#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "PrismLdkBlueprintLibrary.generated.h"

class UPrismLdkSubsystem;

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
};
