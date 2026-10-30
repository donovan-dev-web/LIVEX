// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Tests automatisés de la surface de service du composant PRISM.

#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "GenericPlatform/GenericPlatformMisc.h"
#include "PrismLdkService.h"

/**
 * Verrouille le contrat du service local attendu par le Launcher : arguments
 * supervisés, jeton de session, codes HTTP par route et métriques.
 *
 * Exécution : Session Frontend → Automation → filtre « PrismLdk.Service »,
 * ou ligne de commande `-ExecCmds="Automation RunTests PrismLdk.Service"`.
 */
BEGIN_DEFINE_SPEC(FPrismLdkServiceSpec, TEXT("PrismLdk.Service"),
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
END_DEFINE_SPEC(FPrismLdkServiceSpec)

namespace
{
	/** État de service prêt à servir, utilisé comme base par les tests de routes. */
	FPrismLdkServiceState MakeReadyState()
	{
		FPrismLdkServiceState State;
		State.bListening = true;
		State.bEngineRunning = true;
		State.bWsConnected = true;
		State.bHasWorld = true;
		State.bHasSnapshot = true;
		State.Fps = 60.0;
		State.Tick = 42;
		State.MaxTicks = 400;
		State.Agents = 100;
		State.TicksPerSecond = 6;
		State.RequestsServed = 7;
		State.WsUrl = TEXT("ws://127.0.0.1:5180");
		State.InstanceId = TEXT("instance-test");
		State.CorrelationId = TEXT("correlation-test");
		State.WorldVersion = TEXT("1.1");
		State.RunId = TEXT("run-test");
		return State;
	}

	const TCHAR* GTestToken = TEXT("jeton-de-test");
}

void FPrismLdkServiceSpec::Define()
{
	Describe(TEXT("FPrismLdkServiceOptions::Tokenize"), [this]()
	{
		It(TEXT("découpe en respectant les guillemets doubles"), [this]()
		{
			const TArray<FString> Tokens = FPrismLdkServiceOptions::Tokenize(
				TEXT("--work-dir \"C:/Program Files/LIVEX work\" -game"));

			TestEqual(TEXT("nombre de jetons"), Tokens.Num(), 3);
			TestEqual(TEXT("premier"), Tokens[0], FString(TEXT("--work-dir")));
			TestEqual(TEXT("chemin entre guillemets"), Tokens[1], FString(TEXT("C:/Program Files/LIVEX work")));
			TestEqual(TEXT("commutateur moteur"), Tokens[2], FString(TEXT("-game")));
		});

		It(TEXT("ignore les espaces multiples"), [this]()
		{
			const TArray<FString> Tokens = FPrismLdkServiceOptions::Tokenize(TEXT("  -a   -b  "));

			TestEqual(TEXT("nombre de jetons"), Tokens.Num(), 2);
		});
	});

	Describe(TEXT("FPrismLdkServiceOptions::Parse"), [this]()
	{
		It(TEXT("n'ouvre aucun service sans --control-port"), [this]()
		{
			const FPrismLdkServiceOptions Options = FPrismLdkServiceOptions::Parse(TEXT("-game -log=prism.log -windowed"));

			TestFalse(TEXT("service non demandé"), Options.bServiceRequested);
			TestTrue(TEXT("aucun rejet"), Options.IsValid());
			TestEqual(TEXT("commutateurs moteur conservés"), Options.UnclaimedArguments.Num(), 3);
		});

		It(TEXT("lit le jeu d'arguments supervisé complet"), [this]()
		{
			const FPrismLdkServiceOptions Options = FPrismLdkServiceOptions::Parse(
				TEXT("--instance-id inst-1 --control-port 5299 --work-dir C:/livex/work --log-dir C:/livex/logs --correlation-id corr-1"));

			TestTrue(TEXT("service demandé"), Options.bServiceRequested);
			TestTrue(TEXT("configuration valide"), Options.IsValid());
			TestEqual(TEXT("port"), Options.ControlPort, 5299);
			TestEqual(TEXT("instance"), Options.InstanceId, FString(TEXT("inst-1")));
			TestEqual(TEXT("work-dir"), Options.WorkDir, FString(TEXT("C:/livex/work")));
			TestEqual(TEXT("log-dir"), Options.LogDir, FString(TEXT("C:/livex/logs")));
			TestEqual(TEXT("corrélation"), Options.CorrelationId, FString(TEXT("corr-1")));
		});

		It(TEXT("accepte la forme --clé=valeur et la casse libre"), [this]()
		{
			const FPrismLdkServiceOptions Options = FPrismLdkServiceOptions::Parse(
				TEXT("--Control-Port=5299 --Instance-Id=i1 --Work-Dir=w --Log-Dir=l --Correlation-Id=c"));

			TestTrue(TEXT("configuration valide"), Options.IsValid());
			TestEqual(TEXT("port"), Options.ControlPort, 5299);
			TestEqual(TEXT("instance"), Options.InstanceId, FString(TEXT("i1")));
		});

		It(TEXT("refuse un port non numérique ou hors domaine"), [this]()
		{
			const FPrismLdkServiceOptions Text = FPrismLdkServiceOptions::Parse(TEXT("--control-port abc"));
			const FPrismLdkServiceOptions TooHigh = FPrismLdkServiceOptions::Parse(TEXT("--control-port 70000"));

			TestFalse(TEXT("port textuel refusé"), Text.IsValid());
			TestTrue(TEXT("cause nommée"), Text.DescribeProblems().Contains(TEXT("n'est pas un entier")));
			TestFalse(TEXT("port hors domaine refusé"), TooHigh.IsValid());
			TestTrue(TEXT("cause de domaine"), TooHigh.DescribeProblems().Contains(TEXT("hors domaine")));
		});

		It(TEXT("refuse un argument sans valeur"), [this]()
		{
			const FPrismLdkServiceOptions Options = FPrismLdkServiceOptions::Parse(TEXT("--control-port --instance-id i1"));

			TestFalse(TEXT("configuration refusée"), Options.IsValid());
			TestTrue(TEXT("cause nommée"), Options.DescribeProblems().Contains(TEXT("attend une valeur")));
		});

		It(TEXT("exige les arguments obligatoires du service"), [this]()
		{
			// La corrélation est fournie par l'environnement pour que le test ne
			// dépende pas de l'environnement de la machine.
			FPlatformMisc::SetEnvironmentVar(TEXT("LIVEX_CORRELATION_ID"), TEXT("corr-env"));

			const FPrismLdkServiceOptions Options = FPrismLdkServiceOptions::Parse(TEXT("--control-port 5299"));

			TestFalse(TEXT("configuration refusée"), Options.IsValid());
			TestEqual(TEXT("trois causes"), Options.Problems.Num(), 3);
			TestTrue(TEXT("instance manquante"), Options.DescribeProblems().Contains(TEXT("--instance-id")));
			TestTrue(TEXT("work-dir manquant"), Options.DescribeProblems().Contains(TEXT("--work-dir")));
			TestTrue(TEXT("log-dir manquant"), Options.DescribeProblems().Contains(TEXT("--log-dir")));
		});

		It(TEXT("lit la corrélation depuis LIVEX_CORRELATION_ID"), [this]()
		{
			FPlatformMisc::SetEnvironmentVar(TEXT("LIVEX_CORRELATION_ID"), TEXT("corr-env"));

			const FPrismLdkServiceOptions Options = FPrismLdkServiceOptions::Parse(
				TEXT("--control-port 5299 --instance-id i1 --work-dir w --log-dir l"));

			TestTrue(TEXT("configuration valide"), Options.IsValid());
			TestEqual(TEXT("corrélation d'environnement"), Options.CorrelationId, FString(TEXT("corr-env")));
		});

		It(TEXT("accepte --headless et --version"), [this]()
		{
			const FPrismLdkServiceOptions Options = FPrismLdkServiceOptions::Parse(TEXT("--version --headless"));

			TestTrue(TEXT("mode diagnostic"), Options.bVersionRequested);
			TestTrue(TEXT("headless"), Options.bHeadless);
			TestFalse(TEXT("service non demandé"), Options.bServiceRequested);
		});
	});

	Describe(TEXT("PrismLdkTokensMatch"), [this]()
	{
		It(TEXT("accepte le jeton attendu"), [this]()
		{
			TestTrue(TEXT("jeton identique"), PrismLdkTokensMatch(GTestToken, GTestToken));
		});

		It(TEXT("refuse un jeton différent ou de longueur différente"), [this]()
		{
			TestFalse(TEXT("jeton différent"), PrismLdkTokensMatch(TEXT("jeton-de-tesu"), GTestToken));
			TestFalse(TEXT("jeton plus court"), PrismLdkTokensMatch(TEXT("jeton"), GTestToken));
			TestFalse(TEXT("jeton vide"), PrismLdkTokensMatch(FString(), GTestToken));
		});

		It(TEXT("refuse tout jeton quand la session n'en fournit aucun"), [this]()
		{
			TestFalse(TEXT("jeton attendu vide"), PrismLdkTokensMatch(FString(), FString()));
			TestFalse(TEXT("jeton non vide face à un attendu vide"), PrismLdkTokensMatch(GTestToken, FString()));
		});
	});

	Describe(TEXT("PrismLdkRouteRequest"), [this]()
	{
		It(TEXT("répond 200 sur /health/live même sans readiness"), [this]()
		{
			FPrismLdkServiceState State = MakeReadyState();
			State.bEngineRunning = false;

			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("GET"), TEXT("/health/live"), FString(), State, GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 200);
			TestTrue(TEXT("corps sain"), Result.Body.Contains(TEXT("Healthy")));
		});

		It(TEXT("répond 200 sur /health/ready quand le moteur tourne"), [this]()
		{
			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("GET"), TEXT("/health/ready"), FString(), MakeReadyState(), GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 200);
		});

		It(TEXT("répond 503 sur /health/ready avant le démarrage du moteur"), [this]()
		{
			FPrismLdkServiceState State = MakeReadyState();
			State.bEngineRunning = false;

			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("GET"), TEXT("/health/ready"), FString(), State, GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 503);
			TestTrue(TEXT("cause de non-préparation"), Result.Body.Contains(TEXT("not_ready")));
		});

		It(TEXT("répond 200 sur /health/details avec le détail des checks"), [this]()
		{
			FPrismLdkServiceState State = MakeReadyState();
			State.bWsConnected = false;

			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("GET"), TEXT("/health/details"), FString(), State, GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 200);
			TestTrue(TEXT("check de service"), Result.Body.Contains(TEXT("\"name\":\"service\"")));
			TestTrue(TEXT("flux dégradé"), Result.Body.Contains(TEXT("\"name\":\"snapshot_stream\",\"status\":\"Degraded\"")));
		});

		It(TEXT("expose l'identité du composant sur /info"), [this]()
		{
			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("GET"), TEXT("/info"), FString(), MakeReadyState(), GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 200);
			TestTrue(TEXT("id"), Result.Body.Contains(TEXT("\"id\":\"prism\"")));
			TestTrue(TEXT("version"), Result.Body.Contains(FString::Printf(TEXT("\"version\":\"%s\""), PRISM_COMPONENT_VERSION)));
			TestTrue(TEXT("protocolVersion"), Result.Body.Contains(FString::Printf(TEXT("\"protocolVersion\":%d"), PRISM_PROTOCOL_VERSION)));
		});

		It(TEXT("expose les métriques en texte Prometheus"), [this]()
		{
			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("GET"), TEXT("/metrics"), FString(), MakeReadyState(), GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 200);
			TestTrue(TEXT("type de contenu"), Result.ContentType.StartsWith(TEXT("text/plain")));
			TestTrue(TEXT("readiness"), Result.Body.Contains(TEXT("livex_prism_ready 1")));
			TestTrue(TEXT("flux"), Result.Body.Contains(TEXT("livex_prism_ws_connected{url=\"ws://127.0.0.1:5180\"} 1")));
			TestTrue(TEXT("cadence"), Result.Body.Contains(TEXT("livex_prism_fps 60.0")));
			TestTrue(TEXT("tick"), Result.Body.Contains(TEXT("livex_prism_tick 42")));
			TestTrue(TEXT("population"), Result.Body.Contains(TEXT("livex_prism_agents 100")));
		});

		It(TEXT("échappe les valeurs de label des métriques"), [this]()
		{
			FPrismLdkServiceState State = MakeReadyState();
			State.WsUrl = TEXT("ws://host/\"flux\"\\x");

			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("GET"), TEXT("/metrics"), FString(), State, GTestToken);

			TestTrue(TEXT("guillemet échappé"), Result.Body.Contains(TEXT("url=\"ws://host/\\\"flux\\\"\\\\x\"")));
		});

		It(TEXT("refuse l'arrêt sans jeton"), [this]()
		{
			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("POST"), TEXT("/control/shutdown"), FString(), MakeReadyState(), GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 401);
			TestFalse(TEXT("aucun arrêt armé"), Result.bShutdown);
			TestTrue(TEXT("cause"), Result.Body.Contains(TEXT("unauthorized")));
		});

		It(TEXT("refuse l'arrêt avec un jeton erroné"), [this]()
		{
			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("POST"), TEXT("/control/shutdown"), TEXT("Bearer mauvais-jeton"), MakeReadyState(), GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 401);
			TestFalse(TEXT("aucun arrêt armé"), Result.bShutdown);
		});

		It(TEXT("refuse l'arrêt quand aucun jeton de session n'est défini"), [this]()
		{
			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("POST"), TEXT("/control/shutdown"), TEXT("Bearer "), MakeReadyState(), FString());

			TestEqual(TEXT("statut"), Result.Code, 401);
			TestFalse(TEXT("aucun arrêt armé"), Result.bShutdown);
			TestTrue(TEXT("cause explicite"), Result.Body.Contains(TEXT("LIVEX_SESSION_TOKEN")));
		});

		It(TEXT("accepte l'arrêt avec le jeton attendu"), [this]()
		{
			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("POST"), TEXT("/control/shutdown"), TEXT("Bearer jeton-de-test"), MakeReadyState(), GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 200);
			TestTrue(TEXT("arrêt armé"), Result.bShutdown);
			TestTrue(TEXT("corps de confirmation"), Result.Body.Contains(TEXT("\"action\":\"shutdown\"")));
		});

		It(TEXT("accepte le préfixe d'authentification en casse libre"), [this]()
		{
			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("POST"), TEXT("/control/shutdown"), TEXT("bearer jeton-de-test"), MakeReadyState(), GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 200);
			TestTrue(TEXT("arrêt armé"), Result.bShutdown);
		});

		It(TEXT("répond 405 sur un verbe non supporté d'un chemin connu"), [this]()
		{
			const FPrismLdkRouteResult ReadShutdown = PrismLdkRouteRequest(TEXT("GET"), TEXT("/control/shutdown"), TEXT("Bearer jeton-de-test"), MakeReadyState(), GTestToken);
			const FPrismLdkRouteResult WriteInfo = PrismLdkRouteRequest(TEXT("POST"), TEXT("/info"), FString(), MakeReadyState(), GTestToken);

			TestEqual(TEXT("shutdown en GET"), ReadShutdown.Code, 405);
			TestEqual(TEXT("info en POST"), WriteInfo.Code, 405);
		});

		It(TEXT("répond 404 sur un chemin inconnu"), [this]()
		{
			const FPrismLdkRouteResult Result = PrismLdkRouteRequest(TEXT("GET"), TEXT("/control/config"), FString(), MakeReadyState(), GTestToken);

			TestEqual(TEXT("statut"), Result.Code, 404);
			TestTrue(TEXT("cause"), Result.Body.Contains(TEXT("not_found")));
		});
	});
}

#endif // WITH_DEV_AUTOMATION_TESTS
