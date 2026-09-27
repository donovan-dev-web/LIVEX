#include "PrismLdk.h"
#include "PrismLdkSubsystem.h"

#include "HttpModule.h"
#include "Interfaces/IHttpRequest.h"
#include "Interfaces/IHttpResponse.h"
#include "Json.h"
#include "Logging/LogMacros.h"
#include "WebSocketsModule.h"

DEFINE_LOG_CATEGORY(LogPrismLdk);

namespace PrismJson
{
	static FString String(const TSharedPtr<FJsonObject>& O, const TCHAR* Key)
	{
		FString V; return O.IsValid() && O->TryGetStringField(Key, V) ? V : FString();
	}

	static double Number(const TSharedPtr<FJsonObject>& O, const TCHAR* Key)
	{
		double V = 0.0; return O.IsValid() && O->TryGetNumberField(Key, V) ? V : 0.0;
	}
	static int64 Integer(const TSharedPtr<FJsonObject>& O, const TCHAR* Key)
	{
		double NumericValue = 0.0;
		if (O.IsValid() && O->TryGetNumberField(Key, NumericValue))
		{
			return static_cast<int64>(NumericValue);
		}
		FString StringValue;
		int64 ParsedValue = 0;
		return O.IsValid() && O->TryGetStringField(Key, StringValue) &&
			LexTryParseString(ParsedValue, *StringValue) ? ParsedValue : 0;
	}
	static int64 Integer(const TSharedPtr<FJsonValue>& Value)
	{
		double NumericValue = 0.0;
		if (Value.IsValid() && Value->TryGetNumber(NumericValue))
		{
			return static_cast<int64>(NumericValue);
		}
		FString StringValue;
		int64 ParsedValue = 0;
		return Value.IsValid() && Value->TryGetString(StringValue) &&
			LexTryParseString(ParsedValue, *StringValue) ? ParsedValue : 0;
	}
	static bool Boolean(const TSharedPtr<FJsonObject>& O, const TCHAR* Key, bool Default = false)
	{
		bool V = Default; return O.IsValid() && O->TryGetBoolField(Key, V) ? V : Default;
	}
	static TSharedPtr<FJsonObject> Object(const TSharedPtr<FJsonObject>& O, const TCHAR* Key)
	{
		const TSharedPtr<FJsonObject>* V = nullptr;
		return O.IsValid() && O->TryGetObjectField(Key, V) ? *V : nullptr;
	}
	static FPrismSyneVector2D Vector(const TSharedPtr<FJsonObject>& O)
	{
		FPrismSyneVector2D V;
		if (O.IsValid()) { V.X = Number(O, TEXT("x")); V.Y = Number(O, TEXT("y")); }
		return V;
	}
	static const TArray<TSharedPtr<FJsonValue>>* Array(const TSharedPtr<FJsonObject>& O, const TCHAR* Key)
	{
		const TArray<TSharedPtr<FJsonValue>>* Values = nullptr;
		return O.IsValid() && O->TryGetArrayField(Key, Values) ? Values : nullptr;
	}
	static EPrismSyneTerrainType TerrainType(const FString& Name)
	{
		return Name == TEXT("plains") ? EPrismSyneTerrainType::Plains : EPrismSyneTerrainType::Unknown;
	}
	static EPrismSyneResourceType ResourceType(const FString& Name)
	{
		if (Name == TEXT("food")) return EPrismSyneResourceType::Food;
		if (Name == TEXT("water")) return EPrismSyneResourceType::Water;
		if (Name == TEXT("wood")) return EPrismSyneResourceType::Wood;
		if (Name == TEXT("mineral")) return EPrismSyneResourceType::Mineral;
		return EPrismSyneResourceType::Unknown;
	}
	static EPrismSyneSeason Season(const FString& Name)
	{
		if (Name == TEXT("spring")) return EPrismSyneSeason::Spring;
		if (Name == TEXT("summer")) return EPrismSyneSeason::Summer;
		if (Name == TEXT("autumn")) return EPrismSyneSeason::Autumn;
		if (Name == TEXT("winter")) return EPrismSyneSeason::Winter;
		return EPrismSyneSeason::Unknown;
	}
	static EPrismSyneAction Action(const FString& Name)
	{
		if (Name == TEXT("Idle")) return EPrismSyneAction::Idle;
		if (Name == TEXT("SeekFood")) return EPrismSyneAction::SeekFood;
		if (Name == TEXT("SeekWater")) return EPrismSyneAction::SeekWater;
		if (Name == TEXT("Rest")) return EPrismSyneAction::Rest;
		if (Name == TEXT("Flee")) return EPrismSyneAction::Flee;
		if (Name == TEXT("Socialize")) return EPrismSyneAction::Socialize;
		if (Name == TEXT("Explore")) return EPrismSyneAction::Explore;
		if (Name == TEXT("Eat")) return EPrismSyneAction::Eat;
		if (Name == TEXT("Drink")) return EPrismSyneAction::Drink;
		return EPrismSyneAction::Unknown;
	}
}

void FPrismLdkModule::StartupModule()
{
}

void FPrismLdkModule::ShutdownModule()
{
}

void UPrismLdkSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
}

void UPrismLdkSubsystem::Deinitialize()
{
	Disconnect();
	Super::Deinitialize();
}

void UPrismLdkSubsystem::Configure(const FPrismSyneConnectionOptions& InOptions)
{
	Options = InOptions;
}

bool UPrismLdkSubsystem::Connect()
{
	if (ConnectionState == EPrismSyneConnectionState::Connected ||
		ConnectionState == EPrismSyneConnectionState::Connecting ||
		Socket.IsValid()) return true;

	bConnectionRequested = true;
	bReconnectPending = false;
	ReconnectElapsed = 0.0f;
	UE_LOG(LogPrismLdk, Log, TEXT("Connecting WebSocket: %s"), *Options.WebSocketUrl);
	ConnectionState = EPrismSyneConnectionState::Connecting;
	FWebSocketsModule& Module = FModuleManager::LoadModuleChecked<FWebSocketsModule>(TEXT("WebSockets"));
	Socket = Module.CreateWebSocket(Options.WebSocketUrl);
	const uint64 ThisGeneration = ++SocketGeneration;
	Socket->OnConnected().AddLambda([this, ThisGeneration]() {
		if (ThisGeneration != SocketGeneration) return;
		UE_LOG(LogPrismLdk, Log, TEXT("WebSocket connected"));
		bReconnectPending = false;
		ReconnectElapsed = 0.0f;
		ConnectionState = EPrismSyneConnectionState::Connected;
		OnConnected.Broadcast();
	});
	Socket->OnConnectionError().AddLambda([this, ThisGeneration](const FString& Message) {
		if (ThisGeneration != SocketGeneration) return;
		UE_LOG(LogPrismLdk, Error, TEXT("WebSocket connection error: %s"), *Message);
		Socket.Reset();
		ConnectionState = EPrismSyneConnectionState::Disconnected;
		bReconnectPending = bConnectionRequested && Options.bAutoReconnect;
		ReconnectElapsed = 0.0f;
		Error(0, TEXT("websocket_connection"), Message);
	});
	Socket->OnClosed().AddLambda([this, ThisGeneration](int32 Code, const FString& Reason, bool bClean) {
		if (ThisGeneration != SocketGeneration) return;
		UE_LOG(LogPrismLdk, Log, TEXT("WebSocket disconnected (code=%d, clean=%s, reason=%s)"), Code, bClean ? TEXT("true") : TEXT("false"), *Reason);
		Socket.Reset();
		ConnectionState = EPrismSyneConnectionState::Disconnected;
		bReconnectPending = bConnectionRequested && Options.bAutoReconnect;
		ReconnectElapsed = 0.0f;
		OnDisconnected.Broadcast();
	});
	Socket->OnMessage().AddLambda([this, ThisGeneration](const FString& Message) {
		if (ThisGeneration != SocketGeneration) return;
		UE_LOG(LogPrismLdk, Verbose, TEXT("WebSocket message received (%d chars)"), Message.Len());
		IncomingMessages.Enqueue({ Message });
	});
	Socket->Connect();
	return true;
}

void UPrismLdkSubsystem::Disconnect()
{
	UE_LOG(LogPrismLdk, Log, TEXT("Disconnect requested"));
	bConnectionRequested = false;
	bReconnectPending = false;
	ReconnectElapsed = 0.0f;
	const bool bWasConnected = ConnectionState == EPrismSyneConnectionState::Connected;
	++SocketGeneration;
	if (Socket.IsValid()) { Socket->Close(); Socket.Reset(); }
	ConnectionState = EPrismSyneConnectionState::Disconnected;
	if (bWasConnected) OnDisconnected.Broadcast();
}

void UPrismLdkSubsystem::Tick(float DeltaTime)
{
	FQueuedMessage Queued;
	while (IncomingMessages.Dequeue(Queued)) ParseMessage(Queued.Payload);
	if (bReconnectPending && bConnectionRequested && Options.bAutoReconnect &&
		ConnectionState == EPrismSyneConnectionState::Disconnected && !Options.WebSocketUrl.IsEmpty())
	{
		ReconnectElapsed += DeltaTime;
		if (ReconnectElapsed >= FMath::Max(0.1f, Options.ReconnectDelaySeconds))
		{
			ReconnectElapsed = 0.0f;
			bReconnectPending = false;
			UE_LOG(LogPrismLdk, Warning, TEXT("Retrying WebSocket after an unexpected disconnect"));
			Connect();
		}
	}
}

void UPrismLdkSubsystem::SendControl(const FString& Action, const FString& Body)
{
	const FString Url = Options.HttpUrl / FString::Printf(TEXT("/api/control/%s"), *Action);
	UE_LOG(LogPrismLdk, Log, TEXT("HTTP request: POST %s body=%s"), *Url, *Body);
	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
	Request->SetURL(Url);
	Request->SetVerb(TEXT("POST"));
	Request->SetHeader(TEXT("Content-Type"), TEXT("application/json"));
	Request->SetContentAsString(Body);
	Request->OnProcessRequestComplete().BindLambda([this, Action](FHttpRequestPtr Req, FHttpResponsePtr Response, bool bSucceeded)
	{
		const int32 Code = Response.IsValid() ? Response->GetResponseCode() : 0;
		UE_LOG(LogPrismLdk, Log, TEXT("HTTP response: %s -> %d"), *Action, Code);
		if (!bSucceeded || !Response.IsValid()) { Error(Code, TEXT("http_request_failed"), Action); return; }
		TSharedPtr<FJsonObject> Object;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Response->GetContentAsString());
		if (!FJsonSerializer::Deserialize(Reader, Object) || !Object.IsValid()) { Error(Code, TEXT("invalid_json"), Action, Response->GetContentAsString()); return; }
		FPrismSyneControlResult Result;
		Result.bOk = Object->GetBoolField(TEXT("ok"));
		Result.Action = PrismJson::String(Object, TEXT("action"));
		Result.Error = PrismJson::String(Object, TEXT("error"));
		ParseStatus(Object, Result.Status);
		Status = Result.Status;
		if (!Result.bOk || Code >= 400) { if (Result.Error.IsEmpty()) Result.Error = TEXT("http_error"); Error(Code, Result.Error, Action, Response->GetContentAsString()); }
		OnControlResult.Broadcast(Result);
	});
	Request->ProcessRequest();
}

void UPrismLdkSubsystem::Prepare(int64 Seed, int32 TicksPerSecond)
{
	UE_LOG(LogPrismLdk, Log, TEXT("Phase PREPARE (seed=%lld, ticksPerSecond=%d)"), Seed, TicksPerSecond);
	SendControl(TEXT("prepare"), FString::Printf(TEXT("{\"seed\":%lld,\"ticksPerSecond\":%d}"), Seed, TicksPerSecond));
}

void UPrismLdkSubsystem::Ready(const FString& WorldVersion)
{
	UE_LOG(LogPrismLdk, Log, TEXT("Phase READY (worldVersion=%s)"), *WorldVersion);
	SendControl(TEXT("ready"), FString::Printf(TEXT("{\"worldVersion\":\"%s\"}"), *WorldVersion));
}

void UPrismLdkSubsystem::Start(int64 Seed, int32 MaxTicks)
{
	UE_LOG(LogPrismLdk, Log, TEXT("Phase START (seed=%lld, maxTicks=%d)"), Seed, MaxTicks);
	SendControl(TEXT("start"), FString::Printf(TEXT("{\"seed\":%lld,\"maxTicks\":%d}"), Seed, MaxTicks));
}

void UPrismLdkSubsystem::Pause() { UE_LOG(LogPrismLdk, Log, TEXT("Phase PAUSE")); SendControl(TEXT("pause"), TEXT("{}")); }
void UPrismLdkSubsystem::Resume() { UE_LOG(LogPrismLdk, Log, TEXT("Phase RESUME")); SendControl(TEXT("resume"), TEXT("{}")); }
void UPrismLdkSubsystem::Stop() { UE_LOG(LogPrismLdk, Log, TEXT("Phase STOP")); SendControl(TEXT("stop"), TEXT("{}")); }
void UPrismLdkSubsystem::Reset(int64 Seed, int32 MaxTicks)
{
	UE_LOG(LogPrismLdk, Log, TEXT("Phase RESET (seed=%lld, maxTicks=%d)"), Seed, MaxTicks);
	SendControl(TEXT("reset"), FString::Printf(TEXT("{\"seed\":%lld,\"maxTicks\":%d}"), Seed, MaxTicks));
}

void UPrismLdkSubsystem::RequestStatus()
{
	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
	Request->SetURL(Options.HttpUrl / TEXT("/api/control/status"));
	Request->SetVerb(TEXT("GET"));
	Request->OnProcessRequestComplete().BindLambda([this](FHttpRequestPtr Req, FHttpResponsePtr Response, bool bSucceeded)
	{
		if (!bSucceeded || !Response.IsValid()) { Error(0, TEXT("status_failed"), TEXT("Unable to request status")); return; }
		TSharedPtr<FJsonObject> Object; const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Response->GetContentAsString());
		if (!FJsonSerializer::Deserialize(Reader, Object) || !Object.IsValid()) { Error(Response->GetResponseCode(), TEXT("invalid_json"), TEXT("Invalid status response")); return; }
		ParseStatus(Object, Status);
	});
	Request->ProcessRequest();
}

void UPrismLdkSubsystem::ParseMessage(const FString& Payload)
{
	TSharedPtr<FJsonObject> Object; const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Payload);
	if (!FJsonSerializer::Deserialize(Reader, Object) || !Object.IsValid()) { Error(0, TEXT("invalid_json"), TEXT("Invalid WebSocket payload"), Payload); return; }
	const FString Type = PrismJson::String(Object, TEXT("type"));
	if (Type == TEXT("world_initialized"))
	{
		UE_LOG(LogPrismLdk, Log, TEXT("World initialized message received"));
		ParseWorld(PrismJson::Object(Object, TEXT("world")), World);
		World.Version = PrismJson::String(Object, TEXT("version")); World.Seed = PrismJson::Integer(Object, TEXT("seed"));
		bHasWorld = true; OnWorldInitialized.Broadcast(World); return;
	}
	if (Type == TEXT("world_delta"))
	{
		UE_LOG(LogPrismLdk, Verbose, TEXT("World delta received"));
		FPrismSyneWorldDelta Delta; Delta.RunId = PrismJson::String(Object, TEXT("runId")); Delta.Tick = PrismJson::Integer(Object, TEXT("tick"));
		if (const auto* Changes = PrismJson::Array(Object, TEXT("changes")))
		for (const TSharedPtr<FJsonValue>& V : *Changes) { const TSharedPtr<FJsonObject> C = V->AsObject(); FPrismSyneWorldDeltaChange Change; Change.Kind = PrismJson::String(C, TEXT("kind")); Change.Id = PrismJson::String(C, TEXT("id")); Change.Position.X = PrismJson::Number(C, TEXT("x")); Change.Position.Y = PrismJson::Number(C, TEXT("y")); Change.Radius = PrismJson::Number(C, TEXT("radius")); Delta.Changes.Add(Change); }
		OnWorldDelta.Broadcast(Delta); return;
	}
	if (Type == TEXT("snapshot"))
	{
		UE_LOG(LogPrismLdk, Verbose, TEXT("Snapshot received"));
		ParseSnapshot(Object, LatestSnapshot);
		UE_LOG(LogPrismLdk, Verbose, TEXT("Global snapshot tick=%d contains %d agents"), LatestSnapshot.Tick, LatestSnapshot.Agents.Num());
		bHasSnapshot = true; OnSnapshot.Broadcast(LatestSnapshot); return;
	}
	FPrismSyneEvent Event; Event.Type = Type; Event.RunId = PrismJson::String(Object, TEXT("runId")); Event.Tick = PrismJson::Integer(Object, TEXT("tick")); Event.AgentId = PrismJson::Integer(Object, TEXT("agentId")); Event.TargetId = PrismJson::Integer(Object, TEXT("targetId")); Event.Action = PrismJson::String(Object, TEXT("action")); Event.Cause = PrismJson::String(Object, TEXT("cause"));
	const TSharedPtr<FJsonValue>* Value = Object->Values.Find(TEXT("value"));
	if (Value && Value->IsValid())
	{
		TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Event.ValueJson);
		FJsonSerializer::Serialize(*Value, TEXT(""), Writer);
		Writer->Close();
	}
	OnSyneEvent.Broadcast(Event);
}

void UPrismLdkSubsystem::ParseStatus(const TSharedPtr<FJsonObject>& Object, FPrismSyneStatus& Out) const
{
	const FString State = PrismJson::String(Object, TEXT("state"));
	Out.State = State == TEXT("running") ? EPrismSyneRunState::Running : State == TEXT("paused") ? EPrismSyneRunState::Paused : State == TEXT("finished") ? EPrismSyneRunState::Finished : State == TEXT("ready") ? EPrismSyneRunState::Ready : State == TEXT("worldPreparing") ? EPrismSyneRunState::WorldPreparing : EPrismSyneRunState::Idle;
	Out.RunId = PrismJson::String(Object, TEXT("runId")); Out.Tick = PrismJson::Integer(Object, TEXT("tick")); Out.AliveCount = static_cast<int32>(PrismJson::Integer(Object, TEXT("aliveCount"))); Out.TicksPerSecond = static_cast<int32>(PrismJson::Integer(Object, TEXT("ticksPerSecond"))); Out.Seed = PrismJson::Integer(Object, TEXT("seed")); Out.MaxTicks = static_cast<int32>(PrismJson::Integer(Object, TEXT("maxTicks"))); Out.bWorldPrepared = PrismJson::Boolean(Object, TEXT("worldPrepared")); Out.WorldVersion = PrismJson::String(Object, TEXT("worldVersion")); Out.bWorldReadyAcknowledged = PrismJson::Boolean(Object, TEXT("worldReadyAcknowledged"));
}

void UPrismLdkSubsystem::ParseWorld(const TSharedPtr<FJsonObject>& Object, FPrismSyneWorldDescription& Out) const
{
	if (!Object.IsValid()) return;
	Out = FPrismSyneWorldDescription();
	Out.Width = PrismJson::Number(Object, TEXT("width")); Out.Height = PrismJson::Number(Object, TEXT("height")); Out.CellSize = PrismJson::Number(Object, TEXT("cellSize")); Out.TicksPerSecond = static_cast<int32>(PrismJson::Integer(Object, TEXT("ticksPerSecond"))); Out.CellCountX = static_cast<int32>(PrismJson::Integer(Object, TEXT("cellCountX"))); Out.CellCountY = static_cast<int32>(PrismJson::Integer(Object, TEXT("cellCountY")));
	const TArray<TSharedPtr<FJsonValue>>* AgentValues = nullptr;
	if (Object->TryGetArrayField(TEXT("agents"), AgentValues))
	{
		for (const TSharedPtr<FJsonValue>& V : *AgentValues)
		{
			const TSharedPtr<FJsonObject> A = V->AsObject();
			FPrismSyneWorldAgent Agent;
			Agent.Id = PrismJson::Integer(A, TEXT("id"));
			Agent.Species = PrismJson::String(A, TEXT("species"));
			Agent.Position = PrismJson::Vector(PrismJson::Object(A, TEXT("position")));
			Out.Agents.Add(Agent);
		}
	}
	for (const TSharedPtr<FJsonValue>& V : Object->GetArrayField(TEXT("cells"))) { const TSharedPtr<FJsonObject> C = V->AsObject(); FPrismSyneWorldCell Cell; Cell.X = static_cast<int32>(PrismJson::Integer(C, TEXT("x"))); Cell.Y = static_cast<int32>(PrismJson::Integer(C, TEXT("y"))); Cell.TerrainType = PrismJson::String(C, TEXT("terrainType")); Cell.TerrainTypeEnum = PrismJson::TerrainType(Cell.TerrainType); Cell.bWalkable = PrismJson::Boolean(C, TEXT("walkable"), true); Cell.Height = PrismJson::Number(C, TEXT("height")); Cell.MovementCost = PrismJson::Number(C, TEXT("movementCost")); const TArray<TSharedPtr<FJsonValue>>* ObstacleValues = nullptr; if (C->TryGetArrayField(TEXT("obstacles"), ObstacleValues)) for (const TSharedPtr<FJsonValue>& O : *ObstacleValues) Cell.ObstacleIds.Add(O->AsString()); Out.Cells.Add(Cell); }
	for (const TSharedPtr<FJsonValue>& V : Object->GetArrayField(TEXT("obstacles"))) { const TSharedPtr<FJsonObject> O = V->AsObject(); FPrismSyneWorldObstacle Obstacle; Obstacle.Id = PrismJson::String(O, TEXT("id")); Obstacle.Position.X = PrismJson::Number(O, TEXT("x")); Obstacle.Position.Y = PrismJson::Number(O, TEXT("y")); Obstacle.Radius = PrismJson::Number(O, TEXT("radius")); Out.Obstacles.Add(Obstacle); }
	for (const TSharedPtr<FJsonValue>& V : Object->GetArrayField(TEXT("resources"))) { const TSharedPtr<FJsonObject> R = V->AsObject(); FPrismSyneResource Resource; Resource.Id = PrismJson::String(R, TEXT("id")); Resource.Type = PrismJson::String(R, TEXT("kind")); if (Resource.Type.IsEmpty()) Resource.Type = PrismJson::String(R, TEXT("type")); Resource.TypeEnum = PrismJson::ResourceType(Resource.Type); Resource.Quantity = PrismJson::Number(R, TEXT("quantity")); const TSharedPtr<FJsonObject> Position = PrismJson::Object(R, TEXT("position")); if (Position.IsValid()) { Resource.Position = PrismJson::Vector(Position); Resource.bHasPosition = true; } else if (R->HasField(TEXT("x")) && R->HasField(TEXT("y"))) { Resource.Position.X = PrismJson::Number(R, TEXT("x")); Resource.Position.Y = PrismJson::Number(R, TEXT("y")); Resource.bHasPosition = true; Resource.bPositionIsCellIndex = true; } Out.Resources.Add(Resource); }
	for (const TSharedPtr<FJsonValue>& V : Object->GetArrayField(TEXT("regions"))) { const TSharedPtr<FJsonObject> R = V->AsObject(); FPrismSyneWorldRegion Region; Region.Id = PrismJson::String(R, TEXT("id")); Region.X = static_cast<int32>(PrismJson::Integer(R, TEXT("x"))); Region.Y = static_cast<int32>(PrismJson::Integer(R, TEXT("y"))); Region.Width = static_cast<int32>(PrismJson::Integer(R, TEXT("width"))); Region.Height = static_cast<int32>(PrismJson::Integer(R, TEXT("height"))); Out.Regions.Add(Region); }
	UE_LOG(LogPrismLdk, Log, TEXT("World description parsed: agents=%d, cells=%d, obstacles=%d, resources=%d, regions=%d"),
		Out.Agents.Num(), Out.Cells.Num(), Out.Obstacles.Num(), Out.Resources.Num(), Out.Regions.Num());
	if (Out.Obstacles.IsEmpty())
		UE_LOG(LogPrismLdk, Warning, TEXT("World description contains no initial obstacles; check the SYNE world.obstacles flag and world.obstacleLayout configuration."));
}

void UPrismLdkSubsystem::ParseSnapshot(const TSharedPtr<FJsonObject>& Object, FPrismSyneWorldSnapshot& Out) const
{
	Out = FPrismSyneWorldSnapshot();
	Out.Version = PrismJson::String(Object, TEXT("version")); Out.EngineVersion = PrismJson::String(Object, TEXT("engineVersion"));
	Out.RunId = PrismJson::String(Object, TEXT("runId")); Out.Tick = PrismJson::Integer(Object, TEXT("tick")); Out.SimulatedTimeMinutes = PrismJson::Number(Object, TEXT("simulatedTimeMinutes")); Out.AliveCount = static_cast<int32>(PrismJson::Integer(Object, TEXT("aliveCount"))); Out.Season = PrismJson::String(Object, TEXT("season")); Out.SeasonEnum = PrismJson::Season(Out.Season); Out.SeasonIndex = static_cast<int32>(PrismJson::Integer(Object, TEXT("seasonIndex")));
	if (const auto* Agents = PrismJson::Array(Object, TEXT("agents")))
	for (const TSharedPtr<FJsonValue>& V : *Agents)
	{
		const TSharedPtr<FJsonObject> A = V->AsObject();
		FPrismSyneAgentSnapshot Agent;
		Agent.Id = PrismJson::Integer(A, TEXT("id"));
		Agent.Species = PrismJson::String(A, TEXT("species"));
		Agent.Position = PrismJson::Vector(PrismJson::Object(A, TEXT("position")));
		Agent.Energy = PrismJson::Number(A, TEXT("energy")); Agent.Hunger = PrismJson::Number(A, TEXT("hunger"));
		Agent.Thirst = PrismJson::Number(A, TEXT("thirst")); Agent.Fatigue = PrismJson::Number(A, TEXT("fatigue"));
		Agent.CurrentIntention = PrismJson::String(A, TEXT("currentIntention"));
		Agent.CurrentAction = PrismJson::String(A, TEXT("currentAction"));
		if (Agent.CurrentIntention.IsEmpty()) Agent.CurrentIntention = Agent.CurrentAction;
		if (Agent.CurrentAction.IsEmpty()) Agent.CurrentAction = Agent.CurrentIntention;
		Agent.CurrentIntentionEnum = PrismJson::Action(Agent.CurrentIntention);
		Agent.CurrentActionEnum = PrismJson::Action(Agent.CurrentAction);
		Agent.MemoryCount = static_cast<int32>(PrismJson::Integer(A, TEXT("memoryCount")));
		if (const TSharedPtr<FJsonObject> Traits = PrismJson::Object(A, TEXT("traits")))
			for (const auto& Trait : Traits->Values) { double Value = 0.0; if (Trait.Value.IsValid() && Trait.Value->TryGetNumber(Value)) Agent.Traits.Add(FString(Trait.Key), Value); }
		if (const auto* Beliefs = PrismJson::Array(A, TEXT("beliefs")))
			for (const TSharedPtr<FJsonValue>& Item : *Beliefs) { const TSharedPtr<FJsonObject> B = Item->AsObject(); FPrismSyneBelief Belief; Belief.Subject = PrismJson::String(B, TEXT("subject")); Belief.Predicate = PrismJson::String(B, TEXT("predicate")); Belief.Value = PrismJson::String(B, TEXT("value")); Belief.Confidence = PrismJson::Number(B, TEXT("confidence")); Agent.Beliefs.Add(Belief); }
		if (const auto* Goals = PrismJson::Array(A, TEXT("goals")))
			for (const TSharedPtr<FJsonValue>& Item : *Goals) { const TSharedPtr<FJsonObject> G = Item->AsObject(); FPrismSyneGoal Goal; Goal.Kind = PrismJson::String(G, TEXT("kind")); Goal.Age = PrismJson::Integer(G, TEXT("age")); Agent.Goals.Add(Goal); }
		if (const auto* Trusts = PrismJson::Array(A, TEXT("trust")))
			for (const TSharedPtr<FJsonValue>& Item : *Trusts) { const TSharedPtr<FJsonObject> T = Item->AsObject(); FPrismSyneTrust Trust; Trust.PeerId = PrismJson::String(T, TEXT("peerId")); Trust.Trust = PrismJson::Number(T, TEXT("trust")); Agent.Trust.Add(Trust); }
		Out.Agents.Add(Agent);
	}
	if (const auto* Resources = PrismJson::Array(Object, TEXT("resources")))
	for (const TSharedPtr<FJsonValue>& V : *Resources) { const TSharedPtr<FJsonObject> R = V->AsObject(); FPrismSyneResource Resource; Resource.Id = PrismJson::String(R, TEXT("id")); Resource.Type = PrismJson::String(R, TEXT("type")); Resource.TypeEnum = PrismJson::ResourceType(Resource.Type); Resource.Quantity = PrismJson::Number(R, TEXT("quantity")); const TSharedPtr<FJsonObject> Position = PrismJson::Object(R, TEXT("position")); if (Position.IsValid()) { Resource.Position = PrismJson::Vector(Position); Resource.bHasPosition = true; } else if (R->HasField(TEXT("x")) && R->HasField(TEXT("y"))) { Resource.Position.X = PrismJson::Number(R, TEXT("x")); Resource.Position.Y = PrismJson::Number(R, TEXT("y")); Resource.bHasPosition = true; } Out.Resources.Add(Resource); }
	if (const auto* Obstacles = PrismJson::Array(Object, TEXT("obstacles")))
	for (const TSharedPtr<FJsonValue>& V : *Obstacles) { const TSharedPtr<FJsonObject> O = V->AsObject(); FPrismSyneObstacle Obstacle; Obstacle.Id = PrismJson::String(O, TEXT("id")); Obstacle.Position = PrismJson::Vector(PrismJson::Object(O, TEXT("position"))); if (O->HasField(TEXT("x"))) Obstacle.Position.X = PrismJson::Number(O, TEXT("x")); if (O->HasField(TEXT("y"))) Obstacle.Position.Y = PrismJson::Number(O, TEXT("y")); Obstacle.Radius = PrismJson::Number(O, TEXT("radius")); Out.Obstacles.Add(Obstacle); }
	if (const auto* Groups = PrismJson::Array(Object, TEXT("groups")))
	for (const TSharedPtr<FJsonValue>& V : *Groups) { const TSharedPtr<FJsonObject> G = V->AsObject(); FPrismSyneGroup Group; Group.GroupId = PrismJson::Integer(G, TEXT("groupId")); Group.Size = static_cast<int32>(PrismJson::Integer(G, TEXT("size"))); Group.LeaderId = PrismJson::Integer(G, TEXT("leaderId")); Group.bHasLeader = G->HasField(TEXT("leaderId")); Group.BornTick = PrismJson::Integer(G, TEXT("bornTick")); Group.Cohesion = PrismJson::Number(G, TEXT("cohesion")); Group.Consensus = PrismJson::Number(G, TEXT("consensus")); Group.Decision = PrismJson::String(G, TEXT("decision")); if (const auto* Members = PrismJson::Array(G, TEXT("members"))) for (const TSharedPtr<FJsonValue>& Member : *Members) Group.Members.Add(PrismJson::Integer(Member)); Out.Groups.Add(Group); }
	if (const auto* Territories = PrismJson::Array(Object, TEXT("territories")))
	for (const TSharedPtr<FJsonValue>& V : *Territories) { const TSharedPtr<FJsonObject> T = V->AsObject(); FPrismSyneTerritory Territory; Territory.Id = PrismJson::String(T, TEXT("id")); Territory.Position.X = PrismJson::Number(T, TEXT("x")); Territory.Position.Y = PrismJson::Number(T, TEXT("y")); Territory.Radius = PrismJson::Number(T, TEXT("radius")); Territory.MemberCount = static_cast<int32>(PrismJson::Integer(T, TEXT("memberCount"))); if (const auto* Members = PrismJson::Array(T, TEXT("members"))) for (const TSharedPtr<FJsonValue>& Member : *Members) Territory.Members.Add(PrismJson::Integer(Member)); Out.Territories.Add(Territory); }
	if (const auto* Books = PrismJson::Array(Object, TEXT("books")))
	for (const TSharedPtr<FJsonValue>& V : *Books) { const TSharedPtr<FJsonObject> B = V->AsObject(); FPrismSyneBook Book; Book.Id = PrismJson::String(B, TEXT("id")); Book.AuthorId = PrismJson::Integer(B, TEXT("authorId")); Book.Title = PrismJson::String(B, TEXT("title")); Book.Content = PrismJson::String(B, TEXT("content")); Book.WrittenTick = PrismJson::Integer(B, TEXT("writtenTick")); Book.ReadCount = static_cast<int32>(PrismJson::Integer(B, TEXT("readCount"))); if (const auto* Readers = PrismJson::Array(B, TEXT("readers"))) for (const TSharedPtr<FJsonValue>& Reader : *Readers) Book.Readers.Add(PrismJson::Integer(Reader)); Out.Books.Add(Book); }
	if (const auto* Changes = PrismJson::Array(Object, TEXT("worldChanges")))
	for (const TSharedPtr<FJsonValue>& V : *Changes) { const TSharedPtr<FJsonObject> C = V->AsObject(); FPrismSyneWorldDeltaChange Change; Change.Kind = PrismJson::String(C, TEXT("kind")); Change.Id = PrismJson::String(C, TEXT("id")); Change.Position.X = PrismJson::Number(C, TEXT("x")); Change.Position.Y = PrismJson::Number(C, TEXT("y")); Change.Radius = PrismJson::Number(C, TEXT("radius")); Out.WorldChanges.Add(Change); }
	if (const auto* Actions = PrismJson::Array(Object, TEXT("actions")))
	for (const TSharedPtr<FJsonValue>& V : *Actions) { const TSharedPtr<FJsonObject> A = V->AsObject(); FPrismSyneWorldAction Action; Action.AgentId = PrismJson::Integer(A, TEXT("agentId")); Action.Action = PrismJson::String(A, TEXT("action")); Action.ActionEnum = PrismJson::Action(Action.Action); Action.Outcome = PrismJson::String(A, TEXT("outcome")); Action.OutcomeEnum = Action.Outcome == TEXT("executed") ? EPrismSyneActionOutcome::Executed : Action.Outcome == TEXT("blocked") ? EPrismSyneActionOutcome::Blocked : EPrismSyneActionOutcome::Unknown; Action.Cause = PrismJson::String(A, TEXT("cause")); Action.EnergyDelta = PrismJson::Number(A, TEXT("energyDelta")); Action.HungerDelta = PrismJson::Number(A, TEXT("hungerDelta")); Action.ThirstDelta = PrismJson::Number(A, TEXT("thirstDelta")); Action.FatigueDelta = PrismJson::Number(A, TEXT("fatigueDelta")); Action.Reserve = PrismJson::String(A, TEXT("reserve")); Action.ReserveConsumed = PrismJson::Number(A, TEXT("reserveConsumed")); Action.bHasReserve = A->HasField(TEXT("reserve")); Out.Actions.Add(Action); }
}

bool UPrismLdkSubsystem::GetLatestSnapshot(FPrismSyneWorldSnapshot& OutSnapshot) const { if (!bHasSnapshot) return false; OutSnapshot = LatestSnapshot; return true; }
bool UPrismLdkSubsystem::GetWorldDescription(FPrismSyneWorldDescription& OutWorld) const { if (!bHasWorld) return false; OutWorld = World; return true; }

void UPrismLdkSubsystem::Error(int32 Code, const FString& Name, const FString& Message, const FString& Raw)
{
	UE_LOG(LogPrismLdk, Error, TEXT("SYNE error code=%d name=%s message=%s"), Code, *Name, *Message);
	FPrismSyneError E; E.HttpCode = Code; E.Code = Name; E.Message = Message; E.RawPayload = Raw; OnError.Broadcast(E);
}

IMPLEMENT_MODULE(FPrismLdkModule, PrismLdk)
