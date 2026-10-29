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
	/**
	 * Mode développement : si vrai, le jeton `SessionToken` ci-dessous est
	 * présenté à SYNE à la place de la variable d'environnement
	 * `LIVEX_SESSION_TOKEN`. Permet de tester en PIE contre un SYNE lancé par le
	 * Launcher (jeton aléatoire, non exposé à l'éditeur) sans reconditionner le
	 * projet. **À laisser faux pour le package** : le Launcher injecte alors le
	 * jeton dans l'environnement et le plugin le récupère tout seul.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="SYNE|Session") bool bUseManualSessionToken = false;
	/** Jeton présenté à SYNE quand `bUseManualSessionToken` est vrai. Ignoré sinon. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="SYNE|Session") FString SessionToken;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneVector2D
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double X = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Y = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneBelief
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Subject;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Predicate;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Value;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Confidence = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneGoal
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Kind;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Age = 0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneTrust
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString PeerId;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Trust = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneAgentSnapshot
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Id = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Species;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") EPrismSyneAction CurrentActionEnum = EPrismSyneAction::Unknown;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") EPrismSyneAction CurrentIntentionEnum = EPrismSyneAction::Unknown;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Energy = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Hunger = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Thirst = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Fatigue = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString CurrentIntention;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString CurrentAction;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TMap<FString, double> Traits;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneBelief> Beliefs;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneGoal> Goals;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneTrust> Trust;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 MemoryCount = 0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneResource
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Id;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Type;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") EPrismSyneResourceType TypeEnum = EPrismSyneResourceType::Unknown;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Quantity = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") bool bHasPosition = false;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") bool bPositionIsCellIndex = false;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneObstacle
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Id;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Radius = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldCell
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 X = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 Y = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString TerrainType;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") EPrismSyneTerrainType TerrainTypeEnum = EPrismSyneTerrainType::Unknown;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") bool bWalkable = true;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Height = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double MovementCost = 1.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FString> ObstacleIds;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldRegion
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Id;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 X = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 Y = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 Width = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 Height = 0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldAgent
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Id = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Species;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FPrismSyneVector2D Position;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldObstacle
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Id;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Radius = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldDescription
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Version;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Seed = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Width = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Height = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double CellSize = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 TicksPerSecond = 10;
	// Échelle temporelle/ spatiale (description 1.1, ADR-017, additif). Read-only :
	// se lit dans `world_initialized`, jamais codé en dur côté PRISM (TRANSPORT_API §2).
	// `simulatedTimeSeconds = Tick × SimulatedSecondsPerTick`.
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 SimulatedSecondsPerTick = 60;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double MetersPerUnit = 1.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 CellCountX = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 CellCountY = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneWorldAgent> Agents;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneWorldCell> Cells;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneWorldObstacle> Obstacles;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneResource> Resources;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneWorldRegion> Regions;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldDeltaChange
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Kind;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Id;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Radius = 0.0;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldDelta
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString RunId;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Tick = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneWorldDeltaChange> Changes;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneGroup
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 GroupId = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<int64> Members;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 Size = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 LeaderId = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") bool bHasLeader = false;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 BornTick = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Cohesion = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Consensus = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Decision;
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
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Id;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FPrismSyneVector2D Position;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double Radius = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 MemberCount = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<int64> Members;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneBook
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Id;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 AuthorId = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Title;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Content;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 WrittenTick = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 ReadCount = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<int64> Readers;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldAction
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 AgentId = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Action;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") EPrismSyneAction ActionEnum = EPrismSyneAction::Unknown;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Outcome;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") EPrismSyneActionOutcome OutcomeEnum = EPrismSyneActionOutcome::Unknown;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Cause;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double EnergyDelta = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double HungerDelta = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double ThirstDelta = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double FatigueDelta = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Reserve;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double ReserveConsumed = 0.0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") bool bHasReserve = false;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneWorldSnapshot
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Version;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString EngineVersion;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString RunId;
	// Contrat 0.2.1 (additif) : le seed effectif voyage dans le snapshot — il n'a
	// plus à être dérivé du runId (diagnostics PRISM).
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Seed = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Tick = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") double SimulatedTimeMinutes = 0.0;
	// Contrat 0.4.0 (additif, ADR-017) : secondes simulées exactes
	// (`tick × simulatedSecondsPerTick`). `SimulatedTimeMinutes` reste le
	// plancher entier historique. Absent d'un flux antérieur au 0.4.0 → le
	// plugin applique la formule du contrat avec le ratio de `world_initialized`.
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 SimulatedTimeSeconds = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 AliveCount = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Season;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") EPrismSyneSeason SeasonEnum = EPrismSyneSeason::Unknown;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 SeasonIndex = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneAgentSnapshot> Agents;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneResource> Resources;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneObstacle> Obstacles;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneGroup> Groups;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneTerritory> Territories;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneBook> Books;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneWorldDeltaChange> WorldChanges;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") TArray<FPrismSyneWorldAction> Actions;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneEvent
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Type;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString RunId;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Tick = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 AgentId = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 TargetId = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Action;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Cause;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString ValueJson;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneStatus
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") EPrismSyneRunState State = EPrismSyneRunState::Idle;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString RunId;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Tick = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 AliveCount = 0;
	// 0 = non renseigné (monde non préparé : `/api/control/status` publie `null`
	// pour `ticksPerSecond`). La valeur réelle se lit dans `world_initialized`.
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 TicksPerSecond = 0;
	// Contrat 0.4.0 (additif) : `/api/control/status` publie aussi le pas
	// temporel du monde préparé. `null` avant toute préparation → le booléen
	// reste faux (absence ≠ valeur nulle).
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 SimulatedSecondsPerTick = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") bool bHasSimulatedSecondsPerTick = false;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int64 Seed = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 MaxTicks = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") bool bWorldPrepared = false;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString WorldVersion;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") bool bWorldReadyAcknowledged = false;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneControlResult
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") bool bOk = false;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Action;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Error;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FPrismSyneStatus Status;
};

USTRUCT(BlueprintType)
struct PRISMLDK_API FPrismSyneError
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") int32 HttpCode = 0;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Code;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString Message;
	UPROPERTY(BlueprintReadOnly, Category="SYNE|Data") FString RawPayload;
};
