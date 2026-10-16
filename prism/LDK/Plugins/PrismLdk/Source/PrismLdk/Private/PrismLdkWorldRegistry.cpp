// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Implémentation du registre de monde PRISM et de l'échelle de projection.

#include "PrismLdkWorldRegistry.h"

#include "PrismLdkSubsystem.h"

namespace
{
	/** Vrai si la valeur peut servir de taille de cellule ou de facteur d'échelle. */
	static bool IsPositiveFinite(double Value)
	{
		return FMath::IsFinite(Value) && Value > 0.0;
	}
}

void FPrismLdkWorldRegistry::RecomputeScale()
{
	Stats.bScaleValid = IsPositiveFinite(Stats.CellSize) && IsPositiveFinite(Stats.TileSizeUu);
	Stats.K = Stats.bScaleValid ? (Stats.TileSizeUu / Stats.CellSize) : 0.0;
}

bool FPrismLdkWorldRegistry::SetTileSizeUu(double InTileSizeUu)
{
	if (!IsPositiveFinite(InTileSizeUu))
	{
		UE_LOG(LogPrismLdk, Warning,
			TEXT("registre : taille de cellule refusée (%f) : la valeur doit être finie et strictement positive."),
			InTileSizeUu);
		return false;
	}

	Stats.TileSizeUu = InTileSizeUu;
	RecomputeScale();
	return true;
}

void FPrismLdkWorldRegistry::SetStatsFromDescription(const FPrismSyneWorldDescription& World)
{
	Stats.WorldVersion = World.Version;
	Stats.CellSize = World.CellSize;
	Stats.MetersPerUnit = World.MetersPerUnit;
	Stats.CellCountX = World.CellCountX;
	Stats.CellCountY = World.CellCountY;
	Stats.TicksPerSecond = World.TicksPerSecond;
	Stats.SimulatedSecondsPerTick = World.SimulatedSecondsPerTick;
	RecomputeScale();
}

bool FPrismLdkWorldRegistry::RegisterAgent(int64 Id, AActor* Actor)
{
	if (!IsValid(Actor))
	{
		UE_LOG(LogPrismLdk, Warning, TEXT("registre : agent %lld refusé : acteur invalide."), Id);
		return false;
	}

	// Remplacement silencieux : le registre ne possède pas les acteurs, il ne
	// détruit donc jamais l'ancien (c'est le rôle du director).
	AgentsById.Add(Id, Actor);
	return true;
}

bool FPrismLdkWorldRegistry::UnregisterAgent(int64 Id)
{
	return AgentsById.Remove(Id) > 0;
}

AActor* FPrismLdkWorldRegistry::GetAgentActor(int64 Id) const
{
	const TWeakObjectPtr<AActor>* Found = AgentsById.Find(Id);
	return Found ? Found->Get() : nullptr;
}

bool FPrismLdkWorldRegistry::HasAgent(int64 Id) const
{
	return GetAgentActor(Id) != nullptr;
}

int32 FPrismLdkWorldRegistry::GetAgentCount() const
{
	int32 Count = 0;
	for (const TPair<int64, TWeakObjectPtr<AActor>>& Entry : AgentsById)
	{
		if (Entry.Value.IsValid())
		{
			++Count;
		}
	}
	return Count;
}

TArray<int64> FPrismLdkWorldRegistry::GetAgentIds() const
{
	TArray<int64> Ids;
	Ids.Reserve(AgentsById.Num());
	for (const TPair<int64, TWeakObjectPtr<AActor>>& Entry : AgentsById)
	{
		if (Entry.Value.IsValid())
		{
			Ids.Add(Entry.Key);
		}
	}

	Ids.Sort();
	return Ids;
}

bool FPrismLdkWorldRegistry::RegisterObstacle(const FString& Id, AActor* Actor)
{
	if (Id.IsEmpty())
	{
		UE_LOG(LogPrismLdk, Warning, TEXT("registre : obstacle refusé : identifiant vide."));
		return false;
	}

	if (!IsValid(Actor))
	{
		UE_LOG(LogPrismLdk, Warning, TEXT("registre : obstacle '%s' refusé : acteur invalide."), *Id);
		return false;
	}

	ObstaclesById.Add(Id, Actor);
	return true;
}

bool FPrismLdkWorldRegistry::UnregisterObstacle(const FString& Id)
{
	return ObstaclesById.Remove(Id) > 0;
}

AActor* FPrismLdkWorldRegistry::GetObstacleActor(const FString& Id) const
{
	const TWeakObjectPtr<AActor>* Found = ObstaclesById.Find(Id);
	return Found ? Found->Get() : nullptr;
}

bool FPrismLdkWorldRegistry::HasObstacle(const FString& Id) const
{
	return GetObstacleActor(Id) != nullptr;
}

int32 FPrismLdkWorldRegistry::GetObstacleCount() const
{
	int32 Count = 0;
	for (const TPair<FString, TWeakObjectPtr<AActor>>& Entry : ObstaclesById)
	{
		if (Entry.Value.IsValid())
		{
			++Count;
		}
	}
	return Count;
}

TArray<FString> FPrismLdkWorldRegistry::GetObstacleIds() const
{
	TArray<FString> Ids;
	Ids.Reserve(ObstaclesById.Num());
	for (const TPair<FString, TWeakObjectPtr<AActor>>& Entry : ObstaclesById)
	{
		if (Entry.Value.IsValid())
		{
			Ids.Add(Entry.Key);
		}
	}

	Ids.Sort([](const FString& Left, const FString& Right) { return Left < Right; });
	return Ids;
}

bool FPrismLdkWorldRegistry::AddResourceMarker(AActor* Actor)
{
	if (!IsValid(Actor))
	{
		UE_LOG(LogPrismLdk, Warning, TEXT("registre : marqueur de ressource refusé : acteur invalide."));
		return false;
	}

	// Compacte les références mortes avant d'ajouter : le tableau ne grandit pas
	// indéfiniment au fil des runs.
	ResourceMarkers.RemoveAll([](const TWeakObjectPtr<AActor>& Entry) { return !Entry.IsValid(); });

	for (const TWeakObjectPtr<AActor>& Entry : ResourceMarkers)
	{
		if (Entry.Get() == Actor)
		{
			return false;
		}
	}

	ResourceMarkers.Add(Actor);
	return true;
}

bool FPrismLdkWorldRegistry::RemoveResourceMarker(AActor* Actor)
{
	if (!IsValid(Actor))
	{
		return false;
	}

	return ResourceMarkers.RemoveAll([Actor](const TWeakObjectPtr<AActor>& Entry) { return Entry.Get() == Actor; }) > 0;
}

TArray<AActor*> FPrismLdkWorldRegistry::GetResourceMarkers() const
{
	TArray<AActor*> Markers;
	Markers.Reserve(ResourceMarkers.Num());
	for (const TWeakObjectPtr<AActor>& Entry : ResourceMarkers)
	{
		if (AActor* Marker = Entry.Get())
		{
			Markers.Add(Marker);
		}
	}
	return Markers;
}

int32 FPrismLdkWorldRegistry::GetResourceMarkerCount() const
{
	int32 Count = 0;
	for (const TWeakObjectPtr<AActor>& Entry : ResourceMarkers)
	{
		if (Entry.IsValid())
		{
			++Count;
		}
	}
	return Count;
}

void FPrismLdkWorldRegistry::ClearAll()
{
	AgentsById.Empty();
	ObstaclesById.Empty();
	ResourceMarkers.Empty();

	const double ConfiguredTileSizeUu = Stats.TileSizeUu;
	Stats = FPrismLdkWorldStats();
	Stats.TileSizeUu = ConfiguredTileSizeUu;
}

void UPrismLdkWorldRegistryBase::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);

	// Trace volontaire : elle date l'instant où le registre devient disponible,
	// avant toute construction de monde. La classe est native et concrète, donc
	// instanciée avec le module : cette ligne doit apparaître à **chaque** run,
	// sans dépendre du chargement d'un asset (voir BLUEPRINT_ROADMAP §11.4 pour
	// l'ancien montage par Blueprint, qui la faisait disparaître).
	UE_LOG(LogPrismLdk, Display, TEXT("registre de monde instancié (%s)"), *GetClass()->GetName());
}

void UPrismLdkWorldRegistryBase::Deinitialize()
{
	Registry.ClearAll();
	UE_LOG(LogPrismLdk, Display, TEXT("registre de monde libéré (%s)"), *GetClass()->GetName());

	Super::Deinitialize();
}

bool UPrismLdkWorldRegistryBase::SetTileSizeUu(double TileSizeUu)
{
	return Registry.SetTileSizeUu(TileSizeUu);
}

void UPrismLdkWorldRegistryBase::SetStatsFromDescription(const FPrismSyneWorldDescription& World)
{
	Registry.SetStatsFromDescription(World);
}

bool UPrismLdkWorldRegistryBase::RegisterAgent(int64 Id, AActor* Actor)
{
	return Registry.RegisterAgent(Id, Actor);
}

bool UPrismLdkWorldRegistryBase::UnregisterAgent(int64 Id)
{
	return Registry.UnregisterAgent(Id);
}

AActor* UPrismLdkWorldRegistryBase::GetAgentActor(int64 Id) const
{
	return Registry.GetAgentActor(Id);
}

bool UPrismLdkWorldRegistryBase::HasAgent(int64 Id) const
{
	return Registry.HasAgent(Id);
}

int32 UPrismLdkWorldRegistryBase::GetAgentCount() const
{
	return Registry.GetAgentCount();
}

TArray<int64> UPrismLdkWorldRegistryBase::GetAgentIds() const
{
	return Registry.GetAgentIds();
}

bool UPrismLdkWorldRegistryBase::RegisterObstacle(const FString& Id, AActor* Actor)
{
	return Registry.RegisterObstacle(Id, Actor);
}

bool UPrismLdkWorldRegistryBase::UnregisterObstacle(const FString& Id)
{
	return Registry.UnregisterObstacle(Id);
}

AActor* UPrismLdkWorldRegistryBase::GetObstacleActor(const FString& Id) const
{
	return Registry.GetObstacleActor(Id);
}

bool UPrismLdkWorldRegistryBase::HasObstacle(const FString& Id) const
{
	return Registry.HasObstacle(Id);
}

int32 UPrismLdkWorldRegistryBase::GetObstacleCount() const
{
	return Registry.GetObstacleCount();
}

TArray<FString> UPrismLdkWorldRegistryBase::GetObstacleIds() const
{
	return Registry.GetObstacleIds();
}

bool UPrismLdkWorldRegistryBase::AddResourceMarker(AActor* Actor)
{
	return Registry.AddResourceMarker(Actor);
}

bool UPrismLdkWorldRegistryBase::RemoveResourceMarker(AActor* Actor)
{
	return Registry.RemoveResourceMarker(Actor);
}

TArray<AActor*> UPrismLdkWorldRegistryBase::GetResourceMarkers() const
{
	return Registry.GetResourceMarkers();
}

void UPrismLdkWorldRegistryBase::ClearAll()
{
	Registry.ClearAll();
}
