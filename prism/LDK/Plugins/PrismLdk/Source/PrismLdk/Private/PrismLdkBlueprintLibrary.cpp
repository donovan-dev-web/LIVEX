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
