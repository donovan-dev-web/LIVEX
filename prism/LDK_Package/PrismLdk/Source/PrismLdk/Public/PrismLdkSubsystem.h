// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Sous-système GameInstance PrismLdk : connexion WebSocket à SYNE et pilotage du run.

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Containers/Queue.h"
#include "Dom/JsonObject.h"
#include "Tickable.h"
#include "IWebSocket.h"
#include "PrismLdkTypes.h"
#include "PrismLdkSubsystem.generated.h"

DECLARE_LOG_CATEGORY_EXTERN(LogPrismLdk, Log, All);

/**
 * Construit la valeur du header `Authorization` présenté à SYNE pour une
 * commande de contrôle : `Bearer <jeton>` si un jeton est fourni, chaîne vide
 * sinon (aucun header posé). Fonction pure, verrouillée par `PrismLdk.Auth`.
 *
 * Le jeton provient de `LIVEX_SESSION_TOKEN` (environnement hérité du Launcher)
 * ou d'un appel explicite à `UPrismLdkSubsystem::SetSessionToken` ; il n'est
 * **jamais** lu sur la ligne de commande (contrat d'intégration Launcher v1).
 */
PRISMLDK_API FString PrismLdkAuthorizationHeader(const FString& SessionToken);

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
	// `ConfigJson` est une surcouche JSON **objet** (même contrat que `config` de
	// `POST /api/control/prepare`) : sans elle, SYNE prépare le profil de
	// référence et le mock son fichier de configuration — deux mondes différents.
	// Laisser vide pour conserver le comportement historique.
	UFUNCTION(BlueprintCallable, Category="SYNE|Control", meta=(AdvancedDisplay="TicksPerSecond,ConfigJson"))
	void Prepare(int64 Seed, int32 TicksPerSecond = 10, const FString& ConfigJson = TEXT(""));
	// Version de la description du monde reçue dans `world_initialized` (contrat
	// 1.1 depuis ADR-017 : simulatedSecondsPerTick / metersPerUnit additifs).
	// À reconstruire en lecture de `world.version` sans valeur en dur (spec PRISM §6).
	UFUNCTION(BlueprintCallable, Category="SYNE|Control") void Ready(const FString& WorldVersion = TEXT("1.1"));
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

	/**
	 * Fixe le jeton de session présenté à SYNE sur les commandes de contrôle
	 * (`POST /api/control/*`). Robuste à une intégration qui ne dépendrait pas
	 * de `LIVEX_SESSION_TOKEN` : appelée explicitement, elle prime sur la
	 * variable d'environnement. Un jeton vide ⇒ plus aucun header `Authorization`
	 * (les mutations SYNE sont alors refusées par `401`).
	 */
	UFUNCTION(BlueprintCallable, Category="SYNE|Connection") void SetSessionToken(const FString& InSessionToken);
	UFUNCTION(BlueprintPure, Category="SYNE|State") FString GetSessionToken() const { return SessionToken; }

private:
	struct FQueuedMessage { FString Payload; };
	void ParseMessage(const FString& Payload);
	void SendControl(const FString& Action, const FString& Body);
	/** Jeton de session présenté à SYNE (`Authorization: Bearer`) sur les mutations. */
	FString SessionToken;
#if WITH_DEV_AUTOMATION_TESTS
public:
	/**
	 * Points d'entrée de test : rejouent une trame WebSocket et un corps de
	 * réponse de contrôle par les **parseurs réels** (`ParseMessage`,
	 * `ParseStatus`), sans socket. C'est le chemin exact que suivent SYNE et
	 * syne-mock : les specs `PrismLdk.Contract` vérifient le contrat consommé,
	 * pas une copie de celui-ci.
	 */
	void DebugParseMessage(const FString& Payload) { ParseMessage(Payload); }
	void DebugParseStatusBody(const FString& Body);
	FPrismSyneStatus DebugGetStatus() const { return Status; }
private:
#endif
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
