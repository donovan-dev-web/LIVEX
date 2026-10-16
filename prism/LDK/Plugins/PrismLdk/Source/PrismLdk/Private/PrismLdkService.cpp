// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Implémentation de la surface de service locale du composant PRISM.

#include "PrismLdkService.h"
#include "PrismLdkSubsystem.h"

#include "Containers/Ticker.h"
#include "Engine/Engine.h"
#include "Engine/GameInstance.h"
#include "Engine/World.h"
#include "GenericPlatform/GenericPlatformMisc.h"
#include "HAL/PlatformMisc.h"
#include "HttpPath.h"
#include "HttpRequestHandler.h"
#include "HttpResultCallback.h"
#include "HttpServerModule.h"
#include "HttpServerRequest.h"
#include "HttpServerResponse.h"
#include "IHttpRouter.h"
#include "Misc/DateTime.h"
#include "Misc/EngineVersion.h"
#include "Misc/FileHelper.h"
#include "Misc/OutputDeviceRedirector.h"
#include "Misc/ScopeLock.h"

namespace
{
	/**
	 * Chemins de la surface de service. Chaque chemin est lié aux verbes GET et
	 * POST pour qu'un verbe non supporté reçoive un 405 explicite au lieu du 404
	 * générique du module moteur.
	 */
	const TCHAR* GServicePaths[] =
	{
		TEXT("/health/live"),
		TEXT("/health/ready"),
		TEXT("/health/details"),
		TEXT("/info"),
		TEXT("/metrics"),
		TEXT("/control/shutdown"),
	};

	/** Échappe une chaîne pour un corps JSON. */
	FString EscapeJson(const FString& Value)
	{
		FString Out;
		Out.Reserve(Value.Len() + 8);
		for (const TCHAR Character : Value)
		{
			switch (Character)
			{
			case TEXT('"'): Out += TEXT("\\\""); break;
			case TEXT('\\'): Out += TEXT("\\\\"); break;
			case TEXT('\n'): Out += TEXT("\\n"); break;
			case TEXT('\r'): Out += TEXT("\\r"); break;
			case TEXT('\t'): Out += TEXT("\\t"); break;
			default: Out.AppendChar(Character); break;
			}
		}
		return Out;
	}

	/** Formate un réel avec un point décimal, quelle que soit la locale du processus. */
	FString FormatMetric(double Value, int32 Precision = 1)
	{
		const FString Formatted = FString::Printf(TEXT("%.*f"), Precision, Value);
		return Formatted.Replace(TEXT(","), TEXT("."));
	}

	/** Vrai si la chaîne ne contient que des chiffres décimaux. */
	bool IsDecimalDigits(const FString& Value)
	{
		if (Value.IsEmpty())
		{
			return false;
		}

		for (const TCHAR Character : Value)
		{
			if (!FChar::IsDigit(Character))
			{
				return false;
			}
		}

		return true;
	}

	/** Vrai si le jeton est un commutateur (et non une valeur). */
	bool LooksLikeSwitch(const FString& Token)
	{
		return Token.Len() > 1 && Token.StartsWith(TEXT("-"));
	}

	/** Extrait un en-tête HTTP sans dépendre de la casse. */
	FString ExtractHeader(const FHttpServerRequest& Request, const TCHAR* Name)
	{
		for (const TPair<FString, TArray<FString>>& Header : Request.Headers)
		{
			if (Header.Key.Equals(Name, ESearchCase::IgnoreCase) && Header.Value.Num() > 0)
			{
				return Header.Value[0];
			}
		}

		return FString();
	}
}

FString PrismLdkEscapeLabel(const FString& Value)
{
	FString Out;
	Out.Reserve(Value.Len() + 8);
	for (const TCHAR Character : Value)
	{
		switch (Character)
		{
		case TEXT('\\'): Out += TEXT("\\\\"); break;
		case TEXT('"'): Out += TEXT("\\\""); break;
		case TEXT('\n'): Out += TEXT("\\n"); break;
		default: Out.AppendChar(Character); break;
		}
	}
	return Out;
}

TArray<FString> FPrismLdkServiceOptions::Tokenize(const FString& CommandLine)
{
	TArray<FString> Tokens;
	FString Current;
	bool bInQuotes = false;

	for (int32 Index = 0; Index < CommandLine.Len(); ++Index)
	{
		const TCHAR Character = CommandLine[Index];
		if (Character == TEXT('"'))
		{
			bInQuotes = !bInQuotes;
			continue;
		}

		if (!bInQuotes && FChar::IsWhitespace(Character))
		{
			if (!Current.IsEmpty())
			{
				Tokens.Add(Current);
				Current.Reset();
			}
			continue;
		}

		Current.AppendChar(Character);
	}

	if (!Current.IsEmpty())
	{
		Tokens.Add(Current);
	}

	return Tokens;
}

FPrismLdkServiceOptions FPrismLdkServiceOptions::Parse(const FString& CommandLine)
{
	FPrismLdkServiceOptions Result;
	const TArray<FString> Tokens = Tokenize(CommandLine);

	auto RequireValue = [&Result](const FString& Key) -> FString
	{
		Result.Problems.Add(FString::Printf(TEXT("l'argument « %s » attend une valeur"), *Key));
		return FString();
	};

	for (int32 Index = 0; Index < Tokens.Num(); ++Index)
	{
		const FString& Token = Tokens[Index];
		if (!Token.StartsWith(TEXT("-")))
		{
			Result.UnclaimedArguments.Add(Token);
			continue;
		}

		FString Key = Token;
		int32 Dashes = 0;
		while (Dashes < Key.Len() && Key[Dashes] == TEXT('-'))
		{
			++Dashes;
		}
		Key = Key.RightChop(Dashes);

		FString Value;
		bool bHasValue = false;
		int32 EqualsIndex = INDEX_NONE;
		if (Key.FindChar(TEXT('='), EqualsIndex))
		{
			Value = Key.RightChop(EqualsIndex + 1);
			Key.LeftInline(EqualsIndex);
			bHasValue = true;
		}
		else if (Index + 1 < Tokens.Num() && !LooksLikeSwitch(Tokens[Index + 1]))
		{
			Value = Tokens[Index + 1];
			bHasValue = true;
			++Index;
		}

		if (Key.Equals(TEXT("control-port"), ESearchCase::IgnoreCase) || Key.Equals(TEXT("port"), ESearchCase::IgnoreCase))
		{
			if (!bHasValue)
			{
				RequireValue(TEXT("--control-port"));
				continue;
			}

			if (!IsDecimalDigits(Value))
			{
				Result.Problems.Add(FString::Printf(TEXT("port d'écoute invalide : « %s » n'est pas un entier"), *Value));
				continue;
			}

			const int32 Port = FCString::Atoi(*Value);
			if (Port < 1 || Port > 65535)
			{
				Result.Problems.Add(FString::Printf(TEXT("port d'écoute hors domaine : %d (attendu 1..65535)"), Port));
				continue;
			}

			Result.ControlPort = Port;
			Result.bServiceRequested = true;
		}
		else if (Key.Equals(TEXT("instance-id"), ESearchCase::IgnoreCase))
		{
			if (!bHasValue) { RequireValue(TEXT("--instance-id")); continue; }
			Result.InstanceId = Value;
		}
		else if (Key.Equals(TEXT("work-dir"), ESearchCase::IgnoreCase))
		{
			if (!bHasValue) { RequireValue(TEXT("--work-dir")); continue; }
			Result.WorkDir = Value;
		}
		else if (Key.Equals(TEXT("log-dir"), ESearchCase::IgnoreCase))
		{
			if (!bHasValue) { RequireValue(TEXT("--log-dir")); continue; }
			Result.LogDir = Value;
		}
		else if (Key.Equals(TEXT("correlation-id"), ESearchCase::IgnoreCase))
		{
			if (!bHasValue) { RequireValue(TEXT("--correlation-id")); continue; }
			Result.CorrelationId = Value;
		}
		else if (Key.Equals(TEXT("bind"), ESearchCase::IgnoreCase))
		{
			if (!bHasValue) { RequireValue(TEXT("--bind")); continue; }
			Result.BindAddress = Value;
		}
		else if (Key.Equals(TEXT("headless"), ESearchCase::IgnoreCase))
		{
			Result.bHeadless = true;
		}
		else if (Key.Equals(TEXT("version"), ESearchCase::IgnoreCase) || Key.Equals(TEXT("info"), ESearchCase::IgnoreCase))
		{
			Result.bVersionRequested = true;
		}
		else
		{
			// Commutateur du moteur Unreal ou argument d'un autre composant :
			// conservé pour le journal, jamais refusé (l'exécutable est un jeu
			// Unreal qui reçoit ses propres commutateurs).
			Result.UnclaimedArguments.Add(Token);
		}
	}

	// Le contrat v1 rend ces arguments obligatoires au lancement supervisé.
	if (Result.bServiceRequested)
	{
		if (Result.InstanceId.IsEmpty()) { Result.Problems.Add(TEXT("argument obligatoire manquant : --instance-id")); }
		if (Result.WorkDir.IsEmpty()) { Result.Problems.Add(TEXT("argument obligatoire manquant : --work-dir")); }
		if (Result.LogDir.IsEmpty()) { Result.Problems.Add(TEXT("argument obligatoire manquant : --log-dir")); }
		if (Result.CorrelationId.IsEmpty() && FPlatformMisc::GetEnvironmentVariable(TEXT("LIVEX_CORRELATION_ID")).IsEmpty())
		{
			Result.Problems.Add(TEXT("argument obligatoire manquant : --correlation-id (ou LIVEX_CORRELATION_ID)"));
		}
	}

	if (Result.CorrelationId.IsEmpty())
	{
		Result.CorrelationId = FPlatformMisc::GetEnvironmentVariable(TEXT("LIVEX_CORRELATION_ID"));
	}

	return Result;
}

FString FPrismLdkServiceOptions::DescribeProblems() const
{
	return Problems.IsEmpty() ? FString() : FString::Join(Problems, TEXT(" ; "));
}

bool PrismLdkTokensMatch(const FString& Supplied, const FString& Expected)
{
	// Un jeton attendu vide ne peut jamais être satisfait : aucune commande de
	// contrôle n'est acceptée sans LIVEX_SESSION_TOKEN.
	if (Expected.IsEmpty() || Supplied.Len() != Expected.Len())
	{
		return false;
	}

	uint32 Difference = 0;
	for (int32 Index = 0; Index < Supplied.Len(); ++Index)
	{
		Difference |= static_cast<uint32>(Supplied[Index] ^ Expected[Index]);
	}

	return Difference == 0;
}

FString PrismLdkInfoJson(const FPrismLdkServiceState& State)
{
	return FString::Printf(
		TEXT("{\"id\":\"prism\",\"version\":\"%s\",\"protocolVersion\":%d,\"instanceId\":\"%s\",\"correlationId\":\"%s\",\"engineVersion\":\"%s\",\"worldVersion\":\"%s\",\"runId\":\"%s\",\"listening\":%s,\"ready\":%s}"),
		PRISM_COMPONENT_VERSION,
		PRISM_PROTOCOL_VERSION,
		*EscapeJson(State.InstanceId),
		*EscapeJson(State.CorrelationId),
		*EscapeJson(FEngineVersion::Current().ToString()),
		*EscapeJson(State.WorldVersion),
		*EscapeJson(State.RunId),
		State.bListening ? TEXT("true") : TEXT("false"),
		State.IsReady() ? TEXT("true") : TEXT("false"));
}

FString PrismLdkHealthDetailsJson(const FPrismLdkServiceState& State)
{
	const TCHAR* ServiceStatus = State.bListening ? TEXT("Healthy") : TEXT("Unhealthy");
	const TCHAR* EngineStatus = State.bEngineRunning ? TEXT("Healthy") : TEXT("Degraded");
	const TCHAR* StreamStatus = State.bWsConnected ? TEXT("Healthy") : TEXT("Degraded");
	const TCHAR* WorldStatus = State.bHasWorld ? TEXT("Healthy") : TEXT("Degraded");
	const TCHAR* Status = State.IsReady() ? TEXT("Healthy") : TEXT("NotReady");

	return FString::Printf(
		TEXT("{\"status\":\"%s\",\"readiness\":%s,\"cause\":\"%s\",\"checks\":[")
		TEXT("{\"name\":\"service\",\"status\":\"%s\"},")
		TEXT("{\"name\":\"engine\",\"status\":\"%s\"},")
		TEXT("{\"name\":\"world_description\",\"status\":\"%s\"},")
		TEXT("{\"name\":\"snapshot_stream\",\"status\":\"%s\",\"detail\":\"%s\"}],")
		TEXT("\"instances\":{\"tick\":%lld,\"agents\":%d,\"ticksPerSecond\":%d,\"maxTicks\":%d},")
		TEXT("\"lastError\":\"%s\",\"requestsServed\":%llu}"),
		Status,
		State.IsReady() ? TEXT("true") : TEXT("false"),
		State.IsReady() ? TEXT("") : TEXT("le moteur ne tourne pas encore"),
		ServiceStatus,
		EngineStatus,
		WorldStatus,
		StreamStatus,
		*EscapeJson(State.bWsConnected ? State.WsUrl : FString(TEXT("déconnecté"))),
		State.Tick,
		State.Agents,
		State.TicksPerSecond,
		State.MaxTicks,
		*EscapeJson(State.LastError),
		State.RequestsServed);
}

FString PrismLdkMetricsText(const FPrismLdkServiceState& State)
{
	// Unités : fps = frames par seconde ; tick = tick SYNE ; agents = population
	// simulée (aliveCount) ; *_total = compteur monotone depuis le démarrage.
	return FString::Printf(
		TEXT("# TYPE livex_prism_ready gauge\n")
		TEXT("# unit: bool\n")
		TEXT("livex_prism_ready %d\n")
		TEXT("# TYPE livex_prism_ws_connected gauge\n")
		TEXT("# unit: bool\n")
		TEXT("livex_prism_ws_connected{url=\"%s\"} %d\n")
		TEXT("# TYPE livex_prism_fps gauge\n")
		TEXT("# unit: frames/s\n")
		TEXT("livex_prism_fps %s\n")
		TEXT("# TYPE livex_prism_tick gauge\n")
		TEXT("# unit: tick SYNE\n")
		TEXT("livex_prism_tick %lld\n")
		TEXT("# TYPE livex_prism_agents gauge\n")
		TEXT("# unit: agents\n")
		TEXT("livex_prism_agents %d\n")
		TEXT("# TYPE livex_prism_http_requests_total counter\n")
		TEXT("# unit: requêtes\n")
		TEXT("livex_prism_http_requests_total %llu\n"),
		State.IsReady() ? 1 : 0,
		*PrismLdkEscapeLabel(State.WsUrl),
		State.bWsConnected ? 1 : 0,
		*FormatMetric(State.Fps),
		State.Tick,
		State.Agents,
		State.RequestsServed);
}

FPrismLdkRouteResult PrismLdkRouteRequest(const FString& Method, const FString& Path, const FString& AuthorizationHeader, const FPrismLdkServiceState& State, const FString& SessionToken)
{
	FPrismLdkRouteResult Result;

	const bool bIsGet = Method.Equals(TEXT("GET"), ESearchCase::CaseSensitive);
	const bool bIsPost = Method.Equals(TEXT("POST"), ESearchCase::CaseSensitive);

	auto JsonError = [&Result](int32 Code, const TCHAR* Error, const FString& Detail)
	{
		Result.Code = Code;
		Result.ContentType = TEXT("application/json; charset=utf-8");
		Result.Body = FString::Printf(TEXT("{\"error\":\"%s\",\"detail\":\"%s\"}"), Error, *EscapeJson(Detail));
		return Result;
	};

	// Verbès supportés par chemin (source unique de vérité, testée).
	const bool bKnownPath =
		Path == TEXT("/health/live") || Path == TEXT("/health/ready") || Path == TEXT("/health/details") ||
		Path == TEXT("/info") || Path == TEXT("/metrics") || Path == TEXT("/control/shutdown");

	if (!bKnownPath)
	{
		return JsonError(404, TEXT("not_found"), FString::Printf(TEXT("endpoint inconnu : %s"), *Path));
	}

	const bool bMethodAllowed = (Path == TEXT("/control/shutdown")) ? bIsPost : bIsGet;
	if (!bMethodAllowed)
	{
		return JsonError(405, TEXT("method_not_allowed"), FString::Printf(TEXT("%s n'accepte pas %s"), *Path, *Method));
	}

	if (Path == TEXT("/health/live"))
	{
		// Vivacité : le service répond, donc il est vivant. Ne remplace pas la readiness.
		Result.Code = 200;
		Result.Body = TEXT("{\"status\":\"Healthy\"}");
		return Result;
	}

	if (Path == TEXT("/health/ready"))
	{
		if (!State.IsReady())
		{
			return JsonError(503, TEXT("not_ready"), State.bListening
				? TEXT("le moteur ne tourne pas encore")
				: TEXT("le service de contrôle n'écoute pas"));
		}

		Result.Code = 200;
		Result.Body = TEXT("{\"status\":\"Healthy\"}");
		return Result;
	}

	if (Path == TEXT("/health/details"))
	{
		// Diagnostic : toujours 200 quand il écoute ; `/health/ready` reste
		// l'autorité de readiness.
		Result.Code = 200;
		Result.Body = PrismLdkHealthDetailsJson(State);
		return Result;
	}

	if (Path == TEXT("/info"))
	{
		Result.Code = 200;
		Result.Body = PrismLdkInfoJson(State);
		return Result;
	}

	if (Path == TEXT("/metrics"))
	{
		Result.Code = 200;
		Result.ContentType = TEXT("text/plain; version=0.0.4; charset=utf-8");
		Result.Body = PrismLdkMetricsText(State);
		return Result;
	}

	// /control/shutdown : mutation, jeton obligatoire.
	FString Supplied;
	if (AuthorizationHeader.StartsWith(TEXT("Bearer "), ESearchCase::IgnoreCase))
	{
		Supplied = AuthorizationHeader.RightChop(7).TrimStartAndEnd();
	}

	if (!PrismLdkTokensMatch(Supplied, SessionToken))
	{
		Result.Code = 401;
		Result.ContentType = TEXT("application/json; charset=utf-8");
		Result.Body = FString::Printf(TEXT("{\"error\":\"unauthorized\",\"detail\":\"%s\"}"),
			SessionToken.IsEmpty()
				? TEXT("LIVEX_SESSION_TOKEN n'est pas défini : les commandes de contrôle sont refusées")
				: TEXT("un jeton de session valide est requis"));
		return Result;
	}

	Result.Code = 200;
	Result.Body = TEXT("{\"ok\":true,\"action\":\"shutdown\"}");
	Result.bShutdown = true;
	return Result;
}

void PrismLdkServicePrintLine(const FString& Line)
{
	UE_LOG(LogPrismLdk, Display, TEXT("%s"), *Line);
	// Sortie standard du processus : le Launcher capture stdout/stderr.
	FPlatformMisc::LocalPrint(*FString::Printf(TEXT("%s\n"), *Line));
}

FPrismLdkService& FPrismLdkService::Get()
{
	static FPrismLdkService Instance;
	return Instance;
}

FPrismLdkServiceState FPrismLdkService::SnapshotState() const
{
	FScopeLock Lock(&StateLock);
	return State;
}

FPrismLdkRouteResult FPrismLdkService::RouteRequest(const FString& Method, const FString& Path, const FString& Authorization)
{
	FPrismLdkServiceState Snapshot;
	FString Token;
	{
		FScopeLock Lock(&StateLock);
		++State.RequestsServed;
		Snapshot = State;
		Token = SessionToken;
	}

	return PrismLdkRouteRequest(Method, Path, Authorization, Snapshot, Token);
}

EPrismLdkServiceExitCode FPrismLdkService::Start(const FPrismLdkServiceOptions& InOptions)
{
	Options = InOptions;
	SessionToken = FPlatformMisc::GetEnvironmentVariable(TEXT("LIVEX_SESSION_TOKEN"));

	if (SessionToken.IsEmpty())
	{
		UE_LOG(LogPrismLdk, Warning, TEXT("LIVEX_SESSION_TOKEN absent : toute commande de contrôle sera refusée (401)."));
	}

	if (Options.ControlPort < 1 || Options.ControlPort > 65535)
	{
		StartupError = FString::Printf(TEXT("port de contrôle invalide : %d"), Options.ControlPort);
		return EPrismLdkServiceExitCode::ConfigurationError;
	}

	if (!Options.BindAddress.Equals(TEXT("127.0.0.1"), ESearchCase::IgnoreCase) &&
		!Options.BindAddress.Equals(TEXT("localhost"), ESearchCase::IgnoreCase))
	{
		// Le module moteur décide de l'écoute ; on signale la demande sans la
		// promettre, et le jeton reste la seule protection des mutations.
		UE_LOG(LogPrismLdk, Warning, TEXT("--bind %s demandé : l'écoute réelle est décidée par le module HTTPServer, le jeton protège les mutations."), *Options.BindAddress);
	}

	Router = FHttpServerModule::Get().GetHttpRouter(static_cast<uint32>(Options.ControlPort), true);
	if (!Router.IsValid())
	{
		StartupError = FString::Printf(TEXT("port %d indisponible (échec d'écoute)"), Options.ControlPort);
		return EPrismLdkServiceExitCode::PortUnavailable;
	}

	struct FVerbBinding
	{
		EHttpServerRequestVerbs Verb;
		const TCHAR* Method;
	};

	const FVerbBinding VerbBindings[] =
	{
		{ EHttpServerRequestVerbs::VERB_GET, TEXT("GET") },
		{ EHttpServerRequestVerbs::VERB_POST, TEXT("POST") },
	};

	for (const TCHAR* Path : GServicePaths)
	{
		for (const FVerbBinding& Binding : VerbBindings)
		{
			const FString Method(Binding.Method);
			const FString RoutePath(Path);
			FHttpRequestHandler Handler = FHttpRequestHandler::CreateLambda(
				[this, Method, RoutePath](const FHttpServerRequest& Request, const FHttpResultCallback& Complete) -> bool
				{
					const FPrismLdkRouteResult RouteResult = RouteRequest(Method, RoutePath, ExtractHeader(Request, TEXT("Authorization")));

					TUniquePtr<FHttpServerResponse> Response = FHttpServerResponse::Create(RouteResult.Body, RouteResult.ContentType);
					if (Response.IsValid())
					{
						Response->Code = static_cast<EHttpServerResponseCodes>(RouteResult.Code);
					}
					Complete(MoveTemp(Response));

					if (RouteResult.bShutdown)
					{
						ScheduleShutdown();
					}

					return true;
				});

			Handles.Add(Router->BindRoute(FHttpPath(Path), Binding.Verb, Handler));
		}
	}

	FHttpServerModule::Get().StartAllListeners();
	bStartedListeners = true;

	{
		FScopeLock Lock(&StateLock);
		State.bListening = true;
		State.InstanceId = Options.InstanceId;
		State.CorrelationId = Options.CorrelationId;
	}

	bRunning = true;
	return EPrismLdkServiceExitCode::Success;
}

void FPrismLdkService::Stop()
{
	if (!bRunning && !bStartedListeners && !Router.IsValid())
	{
		return;
	}

	if (Router.IsValid())
	{
		for (const FHttpRouteHandle& Handle : Handles)
		{
			Router->UnbindRoute(Handle);
		}
		Router.Reset();
	}
	Handles.Reset();

	if (bStartedListeners)
	{
		// StopAllListeners agit sur le module entier : appelé uniquement quand ce
		// service a démarré les listeners, et au moment de l'arrêt du processus.
		FHttpServerModule::Get().StopAllListeners();
		bStartedListeners = false;
	}

	{
		FScopeLock Lock(&StateLock);
		State.bListening = false;
	}

	bRunning = false;
}

void FPrismLdkService::ScheduleShutdown()
{
	if (bShutdownScheduled)
	{
		// Idempotence : une seconde demande d'arrêt ne relance rien.
		return;
	}

	bShutdownScheduled = true;
	UE_LOG(LogPrismLdk, Log, TEXT("Arrêt demandé par POST /control/shutdown."));
	FTSTicker::GetCoreTicker().AddTicker(FTickerDelegate::CreateLambda([this](float)
	{
		PerformShutdown();
		return false;
	}), PRISM_SHUTDOWN_DELAY_SECONDS);
}

void FPrismLdkService::PerformShutdown()
{
	// 1. Terminer l'état courant : fermer le flux SYNE côté jeu.
	if (GEngine)
	{
		for (const FWorldContext& Context : GEngine->GetWorldContexts())
		{
			UGameInstance* GameInstance = Context.OwningGameInstance;
			if (!IsValid(GameInstance))
			{
				continue;
			}

			if (UPrismLdkSubsystem* Subsystem = GameInstance->GetSubsystem<UPrismLdkSubsystem>())
			{
				Subsystem->Disconnect();
			}
		}
	}

	// 2. Marqueur d'arrêt du contrat, écrit uniquement dans l'espace de travail fourni.
	if (!Options.WorkDir.IsEmpty())
	{
		const FString MarkerPath = Options.WorkDir / TEXT("shutdown.ok");
		const FString Content = FString::Printf(
			TEXT("instanceId=%s\ncorrelationId=%s\nutc=%s\n"),
			*Options.InstanceId,
			*Options.CorrelationId,
			*FDateTime::UtcNow().ToIso8601());
		if (!FFileHelper::SaveStringToFile(Content, *MarkerPath))
		{
			UE_LOG(LogPrismLdk, Warning, TEXT("Le marqueur shutdown.ok n'a pas pu être écrit dans %s"), *Options.WorkDir);
		}
	}

	// 3. Fermer l'écoute puis vider les journaux avant de sortir.
	Stop();
	if (GLog)
	{
		GLog->Flush();
	}

	UE_LOG(LogPrismLdk, Log, TEXT("Arrêt propre du service PRISM (code 0)."));
	if (GLog)
	{
		GLog->Flush();
	}

	FGenericPlatformMisc::RequestExitWithStatus(false, static_cast<uint8>(EPrismLdkServiceExitCode::Success), TEXT("PrismLdk /control/shutdown"));
}

void FPrismLdkService::ReportConnectionOptions(const FString& WebSocketUrl)
{
	FScopeLock Lock(&StateLock);
	State.WsUrl = WebSocketUrl;
}

void FPrismLdkService::ReportWebSocket(bool bConnected)
{
	FScopeLock Lock(&StateLock);
	State.bWsConnected = bConnected;
	if (bConnected)
	{
		State.LastError.Reset();
	}
}

void FPrismLdkService::ReportWorld(const FString& Version, int32 InTicksPerSecond)
{
	FScopeLock Lock(&StateLock);
	State.bHasWorld = true;
	State.WorldVersion = Version;
	State.TicksPerSecond = InTicksPerSecond;
}

void FPrismLdkService::ReportRun(const FString& InRunId, int32 InMaxTicks, const FString& InWorldVersion, int64 InTick, int32 InAliveCount)
{
	FScopeLock Lock(&StateLock);
	State.RunId = InRunId;
	State.MaxTicks = InMaxTicks;
	State.Tick = InTick;
	State.Agents = InAliveCount;
	if (!InWorldVersion.IsEmpty())
	{
		State.WorldVersion = InWorldVersion;
	}
}

void FPrismLdkService::ReportSnapshot(int64 InTick, int32 InAliveCount, const FString& InRunId)
{
	FScopeLock Lock(&StateLock);
	State.bHasSnapshot = true;
	State.Tick = InTick;
	State.Agents = InAliveCount;
	if (!InRunId.IsEmpty())
	{
		State.RunId = InRunId;
	}
}

void FPrismLdkService::ReportFrame(double DeltaSeconds)
{
	// Moyenne glissante : une lecture instantanée oscille trop pour être utile.
	const double Instantaneous = DeltaSeconds > 0.0 ? 1.0 / DeltaSeconds : 0.0;

	FScopeLock Lock(&StateLock);
	State.bEngineRunning = true;
	State.Fps = State.Fps <= 0.0 ? Instantaneous : State.Fps * 0.9 + Instantaneous * 0.1;
}

void FPrismLdkService::ReportError(const FString& Message)
{
	FScopeLock Lock(&StateLock);
	State.LastError = Message;
}
