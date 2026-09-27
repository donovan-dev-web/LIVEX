#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Tickable.h"
#include "IWebSocket.h"
#include "PrismLdkTypes.h"
#include "PrismLdkSubsystem.generated.h"

DECLARE_LOG_CATEGORY_EXTERN(LogPrismLdk, Log, All);

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FPrismSyneConnected);
DECLARE_DYNAMIC_MULTICAST_DELEGATE(FPrismSyneDisconnected);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FPrismSyneWorldInitialized, const FPrismSyneWorldDescription&, World);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FPrismSyneWorldDeltaEvent, const FPrismSyneWorldDelta&, Delta);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FPrismSyneSnapshot, const FPrismSyneWorldSnapshot&, Snapshot);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FPrismSyneEventReceived, const FPrismSyneEvent&, Event);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FPrismSyneControlResultReceived, const FPrismSyneControlResult&, Result);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FPrismSyneErrorReceived, const FPrismSyneError&, Error);

UCLASS(BlueprintType)
class PRISMLDK_API UPrismLdkSubsystem : public UGameInstanceSubsystem, public FTickableGameObject
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;
	virtual void Tick(float DeltaTime) override;
	virtual TStatId GetStatId() const override { RETURN_QUICK_DECLARE_CYCLE_STAT(UPrismLdkSubsystem, STATGROUP_Tickables); }
	virtual bool IsTickable() const override { return true; }
	virtual bool IsTickableWhenPaused() const override { return true; }

	UPROPERTY(BlueprintAssignable, Category="SYNE|Events") FPrismSyneConnected OnConnected;
	UPROPERTY(BlueprintAssignable, Category="SYNE|Events") FPrismSyneDisconnected OnDisconnected;
	UPROPERTY(BlueprintAssignable, Category="SYNE|Events") FPrismSyneWorldInitialized OnWorldInitialized;
	UPROPERTY(BlueprintAssignable, Category="SYNE|Events") FPrismSyneWorldDeltaEvent OnWorldDelta;
	UPROPERTY(BlueprintAssignable, Category="SYNE|Events") FPrismSyneSnapshot OnSnapshot;
	UPROPERTY(BlueprintAssignable, Category="SYNE|Events") FPrismSyneEventReceived OnSyneEvent;
	UPROPERTY(BlueprintAssignable, Category="SYNE|Events") FPrismSyneControlResultReceived OnControlResult;
	UPROPERTY(BlueprintAssignable, Category="SYNE|Events") FPrismSyneErrorReceived OnError;

	UFUNCTION(BlueprintCallable, Category="SYNE|Connection") void Configure(const FPrismSyneConnectionOptions& InOptions);
	UFUNCTION(BlueprintCallable, Category="SYNE|Connection") bool Connect();
	UFUNCTION(BlueprintCallable, Category="SYNE|Connection") void Disconnect();
	UFUNCTION(BlueprintCallable, Category="SYNE|Control", meta=(AdvancedDisplay="TicksPerSecond"))
	void Prepare(int64 Seed, int32 TicksPerSecond = 10);
	UFUNCTION(BlueprintCallable, Category="SYNE|Control") void Ready(const FString& WorldVersion = TEXT("1.0"));
	UFUNCTION(BlueprintCallable, Category="SYNE|Control") void Start(int64 Seed, int32 MaxTicks = 400);
	UFUNCTION(BlueprintCallable, Category="SYNE|Control") void Pause();
	UFUNCTION(BlueprintCallable, Category="SYNE|Control") void Resume();
	UFUNCTION(BlueprintCallable, Category="SYNE|Control") void Stop();
	UFUNCTION(BlueprintCallable, Category="SYNE|Control") void Reset(int64 Seed, int32 MaxTicks = 400);
	UFUNCTION(BlueprintCallable, Category="SYNE|Control") void RequestStatus();
	UFUNCTION(BlueprintPure, Category="SYNE|State") EPrismSyneConnectionState GetConnectionState() const { return ConnectionState; }
	UFUNCTION(BlueprintPure, Category="SYNE|State") bool GetLatestSnapshot(FPrismSyneWorldSnapshot& OutSnapshot) const;
	UFUNCTION(BlueprintPure, Category="SYNE|State") bool GetWorldDescription(FPrismSyneWorldDescription& OutWorld) const;
	UFUNCTION(BlueprintPure, Category="SYNE|State") FPrismSyneStatus GetStatus() const { return Status; }

private:
	struct FQueuedMessage { FString Payload; };
	void ParseMessage(const FString& Payload);
	void SendControl(const FString& Action, const FString& Body);
	void ParseStatus(const TSharedPtr<FJsonObject>& Object, FPrismSyneStatus& Out) const;
	void ParseWorld(const TSharedPtr<FJsonObject>& Object, FPrismSyneWorldDescription& Out) const;
	void ParseSnapshot(const TSharedPtr<FJsonObject>& Object, FPrismSyneWorldSnapshot& Out) const;
	void Error(int32 Code, const FString& Name, const FString& Message, const FString& Raw = FString());

	FPrismSyneConnectionOptions Options;
	EPrismSyneConnectionState ConnectionState = EPrismSyneConnectionState::Disconnected;
	FPrismSyneStatus Status;
	FPrismSyneWorldDescription World;
	FPrismSyneWorldSnapshot LatestSnapshot;
	bool bHasWorld = false;
	bool bHasSnapshot = false;
	bool bConnectionRequested = false;
	bool bReconnectPending = false;
	float ReconnectElapsed = 0.0f;
	uint64 SocketGeneration = 0;
	TQueue<FQueuedMessage, EQueueMode::Mpsc> IncomingMessages;
	TSharedPtr<IWebSocket> Socket;
};
