// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Fonctions Blueprint exposant le sous-système PrismLdk aux plans Unreal.

#include "PrismLdkBlueprintLibrary.h"
#include "PrismLdk.h"
#include "PrismLdkSubsystem.h"
#include "PrismLdkWorldRegistry.h"

#include "Engine/GameInstance.h"

namespace
{
	/** GameInstance du contexte Blueprint, `nullptr` si le contexte n'est pas dans un monde joué. */
	static UGameInstance* GetContextGameInstance(const UObject* WorldContextObject)
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
		return IsValid(GameInstance) ? GameInstance : nullptr;
	}
}

UPrismLdkSubsystem* UPrismLdkBlueprintLibrary::GetPrismLdkSubsystem(const UObject* WorldContextObject)
{
	UGameInstance* GameInstance = GetContextGameInstance(WorldContextObject);
	return GameInstance ? GameInstance->GetSubsystem<UPrismLdkSubsystem>() : nullptr;
}

UPrismLdkWorldRegistryBase* UPrismLdkBlueprintLibrary::GetPrismLdkWorldRegistry(const UObject* WorldContextObject)
{
	UGameInstance* GameInstance = GetContextGameInstance(WorldContextObject);
	if (!GameInstance)
	{
		// Appel légitime hors monde joué (détail d'asset, aperçu éditeur) : le
		// diagnostic reste discret, mais il existe quand on le cherche.
		UE_LOG(LogPrismLdk, Verbose, TEXT("Get Prism Ldk World Registry : aucun monde joué dans le contexte fourni."));
		return nullptr;
	}

	// La classe est **concrète** : le moteur l'instancie avec son module, donc
	// l'instance ne dépend plus du chargement d'un Blueprint enfant. Moteur 5.8 :
	// `GetSubsystemInternal` résout la classe exacte dans `SubsystemMap` avant de
	// retomber sur un dérivé (`FindAndPopulateSubsystemArray`) — un éventuel
	// Blueprint enfant est donc masqué, jamais choisi à la place du C++.
	UPrismLdkWorldRegistryBase* Registry = GameInstance->GetSubsystem<UPrismLdkWorldRegistryBase>();
	if (!Registry)
	{
		// Ne devrait plus arriver : la seule cause était l'absence de classe
		// chargée, supprimée avec le Blueprint enfant. Si la ligne apparaît, le
		// module PrismLdk n'est pas chargé par cette GameInstance.
		UE_LOG(LogPrismLdk, Error,
			TEXT("Get Prism Ldk World Registry : sous-système de registre absent de la GameInstance (%s) — le module PrismLdk est-il chargé ?"),
			*GetNameSafe(GameInstance));
	}
	return Registry;
}
#include "PrismLdkPCGData.h"
#include "PrismLdkProjection.h"

UPrismLdkPCGData* UPrismLdkBlueprintLibrary::CreatePCGDataFromWorldDescription(const UObject* WorldContextObject, const FPrismSyneWorldDescription& WorldDescription)
{
	UPrismLdkWorldRegistryBase* Registry = GetPrismLdkWorldRegistry(WorldContextObject);
	if (!Registry) return nullptr;
	UPrismLdkPCGData* Data = NewObject<UPrismLdkPCGData>();
	if (!Data) return nullptr;
	Data->BuildFromDescription(WorldDescription, Registry->GetStats());
	return Data;
}

void UPrismLdkBlueprintLibrary::GetObstaclesUnreal(const UObject* WorldContextObject, const FPrismSyneWorldDescription& WorldDescription, TArray<FVector>& OutCenters, TArray<double>& OutRadii)
{
	OutCenters.Empty(); OutRadii.Empty();
	UPrismLdkWorldRegistryBase* Registry = GetPrismLdkWorldRegistry(WorldContextObject);
	if (!Registry) return;
	const FPrismLdkWorldStats& Stats = Registry->GetStats();
	if (!Stats.bScaleValid) return;

	OutCenters.Reserve(WorldDescription.Obstacles.Num());
	OutRadii.Reserve(WorldDescription.Obstacles.Num());
	for (const auto& Obs : WorldDescription.Obstacles)
	{
		OutCenters.Add(UPrismLdkProjection::SyneToUnreal(Obs.Position, Stats.K));
		OutRadii.Add(UPrismLdkProjection::SyneRadiusToUnreal(Obs.Radius, Stats.K));
	}
}

TArray<FIntPoint> UPrismLdkBlueprintLibrary::GetWaterResourceCells(const UObject* WorldContextObject, const FPrismSyneWorldDescription& WorldDescription)
{
	TArray<FIntPoint> Result;
	for (const auto& Res : WorldDescription.Resources)
	{
		if (Res.TypeEnum == EPrismSyneResourceType::Water && Res.bHasPosition)
			Result.Add(FIntPoint((int32)Res.Position.X, (int32)Res.Position.Y));
	}
	return Result;
}

FVector UPrismLdkBlueprintLibrary::CellToTileCenterUnreal(const UObject* WorldContextObject, FIntPoint Cell)
{
	UPrismLdkWorldRegistryBase* Registry = GetPrismLdkWorldRegistry(WorldContextObject);
	if (!Registry) return FVector::ZeroVector;
	const FPrismLdkWorldStats& Stats = Registry->GetStats();
	if (!Stats.bScaleValid) return FVector::ZeroVector;
	return UPrismLdkProjection::CellToTileCenter(Cell, Stats.CellSize, Stats.K);
}

FBox UPrismLdkBlueprintLibrary::GetPlayableBoundsUnreal(const UObject* WorldContextObject)
{
	UPrismLdkWorldRegistryBase* Registry = GetPrismLdkWorldRegistry(WorldContextObject);
	if (!Registry) return FBox(ForceInit);
	const FPrismLdkWorldStats& Stats = Registry->GetStats();
	if (!Stats.bScaleValid || Stats.CellCountX <= 0 || Stats.CellCountY <= 0) return FBox(ForceInit);
	const double SizeX = Stats.CellCountX * Stats.TileSizeUu;
	const double SizeY = Stats.CellCountY * Stats.TileSizeUu;
	return FBox(FVector(0,0,-100), FVector(SizeX,SizeY,5000));
}
