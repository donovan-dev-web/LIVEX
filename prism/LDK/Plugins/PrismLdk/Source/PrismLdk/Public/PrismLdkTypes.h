// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Types Blueprint du PrismLdk (états de connexion et de run SYNE).

#pragma once

#include "CoreMinimal.h"
#include "PrismLdkTypes.generated.h"

UENUM(BlueprintType)
enum class EPrismSyneConnectionState : uint8 { Disconnected, Connecting, Connected, Reconnecting, Error };

UENUM(BlueprintType)
enum class EPrismSyneRunState : uint8 { Idle, WorldPreparing, Ready, Running, Paused, Finished, Unknown };

UENUM(BlueprintType)
enum class EPrismSyneMessageType : uint8
{
	Unknown,
	WorldInitialized,
	WorldDelta,
	Snapshot,
	DecisionMade,
	ActionCompleted,
	TickSummary,
	MessageSent,
	MessageReceived,
	GroupDecision,
	WorldBookWritten
};

UENUM(BlueprintType)
enum class EPrismSyneTerrainType : uint8
{
	Unknown,
	Plains
};

UENUM(BlueprintType)
enum class EPrismSyneResourceType : uint8
{
	Unknown,
	Food,
	Water,
	Wood,
	Mineral
};

UENUM(BlueprintType)
enum class EPrismSyneSeason : uint8
{
	Unknown,
	Spring,
	Summer,
	Autumn,
	Winter
};

UENUM(BlueprintType)
enum class EPrismSyneAction : uint8
{
	Unknown,
	Idle,
	SeekFood,
	SeekWater,
	Rest,
	Flee,
	Socialize,
	Explore,
	Eat,
	Drink
};

UENUM(BlueprintType)
enum class EPrismSyneActionOutcome : uint8
{
	Unknown,
	Executed,
	Blocked
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneConnectionOptions
{
	GENERATED_BODY()
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="SYNE") FString HttpUrl = TEXT("http://127.0.0.1:5181");
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="SYNE") FString WebSocketUrl = TEXT("ws://127.0.0.1:5180");
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="SYNE") bool bAutoReconnect = true;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="SYNE", meta=(ClampMin="0.1")) float ReconnectDelaySeconds = 2.0f;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneVector2D
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) double X = 0.0;
	UPROPERTY(BlueprintReadOnly) double Y = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneBelief
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Subject;
	UPROPERTY(BlueprintReadOnly) FString Predicate;
	UPROPERTY(BlueprintReadOnly) FString Value;
	UPROPERTY(BlueprintReadOnly) double Confidence = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneGoal
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Kind;
	UPROPERTY(BlueprintReadOnly) int64 Age = 0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneTrust
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString PeerId;
	UPROPERTY(BlueprintReadOnly) double Trust = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneAgentSnapshot
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) int64 Id = 0;
	UPROPERTY(BlueprintReadOnly) FString Species;
	UPROPERTY(BlueprintReadOnly) EPrismSyneAction CurrentActionEnum = EPrismSyneAction::Unknown;
	UPROPERTY(BlueprintReadOnly) EPrismSyneAction CurrentIntentionEnum = EPrismSyneAction::Unknown;
	UPROPERTY(BlueprintReadOnly) FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly) double Energy = 0.0;
	UPROPERTY(BlueprintReadOnly) double Hunger = 0.0;
	UPROPERTY(BlueprintReadOnly) double Thirst = 0.0;
	UPROPERTY(BlueprintReadOnly) double Fatigue = 0.0;
	UPROPERTY(BlueprintReadOnly) FString CurrentIntention;
	UPROPERTY(BlueprintReadOnly) FString CurrentAction;
	UPROPERTY(BlueprintReadOnly) TMap<FString, double> Traits;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneBelief> Beliefs;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneGoal> Goals;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneTrust> Trust;
	UPROPERTY(BlueprintReadOnly) int32 MemoryCount = 0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneResource
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Id;
	UPROPERTY(BlueprintReadOnly) FString Type;
	UPROPERTY(BlueprintReadOnly) EPrismSyneResourceType TypeEnum = EPrismSyneResourceType::Unknown;
	UPROPERTY(BlueprintReadOnly) double Quantity = 0.0;
	UPROPERTY(BlueprintReadOnly) FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly) bool bHasPosition = false;
	UPROPERTY(BlueprintReadOnly) bool bPositionIsCellIndex = false;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneObstacle
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Id;
	UPROPERTY(BlueprintReadOnly) FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly) double Radius = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldCell
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) int32 X = 0;
	UPROPERTY(BlueprintReadOnly) int32 Y = 0;
	UPROPERTY(BlueprintReadOnly) FString TerrainType;
	UPROPERTY(BlueprintReadOnly) EPrismSyneTerrainType TerrainTypeEnum = EPrismSyneTerrainType::Unknown;
	UPROPERTY(BlueprintReadOnly) bool bWalkable = true;
	UPROPERTY(BlueprintReadOnly) double Height = 0.0;
	UPROPERTY(BlueprintReadOnly) double MovementCost = 1.0;
	UPROPERTY(BlueprintReadOnly) TArray<FString> ObstacleIds;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldRegion
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Id;
	UPROPERTY(BlueprintReadOnly) int32 X = 0;
	UPROPERTY(BlueprintReadOnly) int32 Y = 0;
	UPROPERTY(BlueprintReadOnly) int32 Width = 0;
	UPROPERTY(BlueprintReadOnly) int32 Height = 0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldAgent
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) int64 Id = 0;
	UPROPERTY(BlueprintReadOnly) FString Species;
	UPROPERTY(BlueprintReadOnly) FPrismSyneVector2D Position;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldObstacle
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Id;
	UPROPERTY(BlueprintReadOnly) FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly) double Radius = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldDescription
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Version;
	UPROPERTY(BlueprintReadOnly) int64 Seed = 0;
	UPROPERTY(BlueprintReadOnly) double Width = 0.0;
	UPROPERTY(BlueprintReadOnly) double Height = 0.0;
	UPROPERTY(BlueprintReadOnly) double CellSize = 0.0;
	UPROPERTY(BlueprintReadOnly) int32 TicksPerSecond = 10;
	// Échelle temporelle/ spatiale (description 1.1, ADR-017, additif). Read-only :
	// se lit dans `world_initialized`, jamais codé en dur côté PRISM (TRANSPORT_API §2).
	// `simulatedTimeSeconds = Tick × SimulatedSecondsPerTick`.
	UPROPERTY(BlueprintReadOnly) int32 SimulatedSecondsPerTick = 60;
	UPROPERTY(BlueprintReadOnly) double MetersPerUnit = 1.0;
	UPROPERTY(BlueprintReadOnly) int32 CellCountX = 0;
	UPROPERTY(BlueprintReadOnly) int32 CellCountY = 0;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneWorldAgent> Agents;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneWorldCell> Cells;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneWorldObstacle> Obstacles;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneResource> Resources;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneWorldRegion> Regions;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldDeltaChange
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Kind;
	UPROPERTY(BlueprintReadOnly) FString Id;
	UPROPERTY(BlueprintReadOnly) FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly) double Radius = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldDelta
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString RunId;
	UPROPERTY(BlueprintReadOnly) int64 Tick = 0;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneWorldDeltaChange> Changes;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneGroup
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) int64 GroupId = 0;
	UPROPERTY(BlueprintReadOnly) TArray<int64> Members;
	UPROPERTY(BlueprintReadOnly) int32 Size = 0;
	UPROPERTY(BlueprintReadOnly) int64 LeaderId = 0;
	UPROPERTY(BlueprintReadOnly) bool bHasLeader = false;
	UPROPERTY(BlueprintReadOnly) int64 BornTick = 0;
	UPROPERTY(BlueprintReadOnly) double Cohesion = 0.0;
	UPROPERTY(BlueprintReadOnly) double Consensus = 0.0;
	UPROPERTY(BlueprintReadOnly) FString Decision;
	// Le groupe est exposé pour le RENDU : identité, appartenance, teinte et
	// état lisible. Les mesures d'analyse du contrat 0.5.0
	// (`maxBondDistance` / `meanBondDistance`, instrumentation A0-3) ne sont
	// volontairement PAS exposées : PRISM représente le monde et les entités en
	// 3D, il ne consomme pas les données destinées à l'analyse.
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneTerritory
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Id;
	UPROPERTY(BlueprintReadOnly) FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly) double Radius = 0.0;
	UPROPERTY(BlueprintReadOnly) int32 MemberCount = 0;
	UPROPERTY(BlueprintReadOnly) TArray<int64> Members;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneBook
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Id;
	UPROPERTY(BlueprintReadOnly) int64 AuthorId = 0;
	UPROPERTY(BlueprintReadOnly) FString Title;
	UPROPERTY(BlueprintReadOnly) FString Content;
	UPROPERTY(BlueprintReadOnly) int64 WrittenTick = 0;
	UPROPERTY(BlueprintReadOnly) int32 ReadCount = 0;
	UPROPERTY(BlueprintReadOnly) TArray<int64> Readers;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldAction
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) int64 AgentId = 0;
	UPROPERTY(BlueprintReadOnly) FString Action;
	UPROPERTY(BlueprintReadOnly) EPrismSyneAction ActionEnum = EPrismSyneAction::Unknown;
	UPROPERTY(BlueprintReadOnly) FString Outcome;
	UPROPERTY(BlueprintReadOnly) EPrismSyneActionOutcome OutcomeEnum = EPrismSyneActionOutcome::Unknown;
	UPROPERTY(BlueprintReadOnly) FString Cause;
	UPROPERTY(BlueprintReadOnly) double EnergyDelta = 0.0;
	UPROPERTY(BlueprintReadOnly) double HungerDelta = 0.0;
	UPROPERTY(BlueprintReadOnly) double ThirstDelta = 0.0;
	UPROPERTY(BlueprintReadOnly) double FatigueDelta = 0.0;
	UPROPERTY(BlueprintReadOnly) FString Reserve;
	UPROPERTY(BlueprintReadOnly) double ReserveConsumed = 0.0;
	UPROPERTY(BlueprintReadOnly) bool bHasReserve = false;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldSnapshot
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Version;
	UPROPERTY(BlueprintReadOnly) FString EngineVersion;
	UPROPERTY(BlueprintReadOnly) FString RunId;
	// Contrat 0.2.1 (additif) : le seed effectif voyage dans le snapshot — il n'a
	// plus à être dérivé du runId (diagnostics PRISM).
	UPROPERTY(BlueprintReadOnly) int64 Seed = 0;
	UPROPERTY(BlueprintReadOnly) int64 Tick = 0;
	UPROPERTY(BlueprintReadOnly) double SimulatedTimeMinutes = 0.0;
	// Contrat 0.4.0 (additif, ADR-017) : secondes simulées exactes
	// (`tick × simulatedSecondsPerTick`). `SimulatedTimeMinutes` reste le
	// plancher entier historique. Absent d'un flux antérieur au 0.4.0 → le
	// plugin applique la formule du contrat avec le ratio de `world_initialized`.
	UPROPERTY(BlueprintReadOnly) int64 SimulatedTimeSeconds = 0;
	UPROPERTY(BlueprintReadOnly) int32 AliveCount = 0;
	UPROPERTY(BlueprintReadOnly) FString Season;
	UPROPERTY(BlueprintReadOnly) EPrismSyneSeason SeasonEnum = EPrismSyneSeason::Unknown;
	UPROPERTY(BlueprintReadOnly) int32 SeasonIndex = 0;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneAgentSnapshot> Agents;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneResource> Resources;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneObstacle> Obstacles;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneGroup> Groups;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneTerritory> Territories;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneBook> Books;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneWorldDeltaChange> WorldChanges;
	UPROPERTY(BlueprintReadOnly) TArray<FPrismSyneWorldAction> Actions;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneEvent
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) FString Type;
	UPROPERTY(BlueprintReadOnly) FString RunId;
	UPROPERTY(BlueprintReadOnly) int64 Tick = 0;
	UPROPERTY(BlueprintReadOnly) int64 AgentId = 0;
	UPROPERTY(BlueprintReadOnly) int64 TargetId = 0;
	UPROPERTY(BlueprintReadOnly) FString Action;
	UPROPERTY(BlueprintReadOnly) FString Cause;
	UPROPERTY(BlueprintReadOnly) FString ValueJson;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneStatus
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) EPrismSyneRunState State = EPrismSyneRunState::Idle;
	UPROPERTY(BlueprintReadOnly) FString RunId;
	UPROPERTY(BlueprintReadOnly) int64 Tick = 0;
	UPROPERTY(BlueprintReadOnly) int32 AliveCount = 0;
	// 0 = non renseigné (monde non préparé : `/api/control/status` publie `null`
	// pour `ticksPerSecond`). La valeur réelle se lit dans `world_initialized`.
	UPROPERTY(BlueprintReadOnly) int32 TicksPerSecond = 0;
	// Contrat 0.4.0 (additif) : `/api/control/status` publie aussi le pas
	// temporel du monde préparé. `null` avant toute préparation → le booléen
	// reste faux (absence ≠ valeur nulle).
	UPROPERTY(BlueprintReadOnly) int32 SimulatedSecondsPerTick = 0;
	UPROPERTY(BlueprintReadOnly) bool bHasSimulatedSecondsPerTick = false;
	UPROPERTY(BlueprintReadOnly) int64 Seed = 0;
	UPROPERTY(BlueprintReadOnly) int32 MaxTicks = 0;
	UPROPERTY(BlueprintReadOnly) bool bWorldPrepared = false;
	UPROPERTY(BlueprintReadOnly) FString WorldVersion;
	UPROPERTY(BlueprintReadOnly) bool bWorldReadyAcknowledged = false;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneControlResult
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) bool bOk = false;
	UPROPERTY(BlueprintReadOnly) FString Action;
	UPROPERTY(BlueprintReadOnly) FString Error;
	UPROPERTY(BlueprintReadOnly) FPrismSyneStatus Status;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneError
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly) int32 HttpCode = 0;
	UPROPERTY(BlueprintReadOnly) FString Code;
	UPROPERTY(BlueprintReadOnly) FString Message;
	UPROPERTY(BlueprintReadOnly) FString RawPayload;
};
