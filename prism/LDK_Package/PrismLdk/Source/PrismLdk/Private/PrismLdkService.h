// Projet : LIVEX (PRISM)
// Auteur : CHARTRAIN Donovan
// Rôle : Surface de service locale du composant PRISM (santé, info, métriques, arrêt).

#pragma once

#include "CoreMinimal.h"
#include "HAL/CriticalSection.h"
#include "IHttpRouter.h"

/**
 * Version du composant PRISM publiée dans `/info`.
 * À garder identique à `component.json` (contrat d'intégration Launcher v1).
 */
#define PRISM_COMPONENT_VERSION TEXT("0.1.0")

/** Version du protocole d'intégration Launcher (contrat v1). */
#define PRISM_PROTOCOL_VERSION 1

/**
 * Délai entre l'envoi de la réponse à `POST /control/shutdown` et l'arrêt du
 * processus : l'API du serveur HTTP ne garantit pas que les octets ont quitté
 * la socket quand le handler rend la main.
 */
#define PRISM_SHUTDOWN_DELAY_SECONDS 0.25f

/** Codes de sortie du contrat d'intégration (docs/launcher/INTEGRATION_CONTRACT.md §4). */
enum class EPrismLdkServiceExitCode : uint8
{
	/** Fin normale, y compris un arrêt demandé proprement. */
	Success = 0,
	/** Erreur non classée. */
	UnclassifiedError = 1,
	/** Configuration invalide : argument manquant, malformé ou hors domaine. */
	ConfigurationError = 2,
	/** Port indisponible : échec d'écoute du serveur de contrôle. */
	PortUnavailable = 3,
};

/**
 * Options de service lues sur la ligne de commande.
 *
 * Le service ne démarre que si `--control-port` est présent : hors Launcher
 * (éditeur, PIE) PRISM n'ouvre aucun port de contrôle.
 */
struct FPrismLdkServiceOptions
{
	/** `--control-port` présent : lancement supervisé par le Launcher. */
	bool bServiceRequested = false;

	/** `--version` ou `--info` : mode diagnostic, imprime l'identité puis quitte. */
	bool bVersionRequested = false;

	/** `--headless` : demandé par le Launcher quand la capacité est déclarée. */
	bool bHeadless = false;

	/** Port d'écoute du contrôle HTTP (`--control-port`). */
	int32 ControlPort = 0;

	/** Adresse demandée par `--bind` (l'écoute est en réalité décidée par le module HTTPServer). */
	FString BindAddress = TEXT("127.0.0.1");

	/** Identifiant d'instance de cette exécution (`--instance-id`). */
	FString InstanceId;

	/** Espace de travail de l'instance (`--work-dir`) ; seul répertoire où PRISM écrit. */
	FString WorkDir;

	/** Répertoire des journaux (`--log-dir`). */
	FString LogDir;

	/** Identifiant de corrélation (`--correlation-id`, aussi `LIVEX_CORRELATION_ID`). */
	FString CorrelationId;

	/**
	 * Arguments non réclamés par le service. Un exécutable Unreal reçoit les
	 * commutateurs du moteur (`-game`, `-log`, …) : ils sont conservés pour le
	 * diagnostic mais ne sont jamais refusés.
	 */
	TArray<FString> UnclaimedArguments;

	/** Causes de rejet : non vide ⇒ sortie avec le code 2. */
	TArray<FString> Problems;

	/** Vrai si le service peut démarrer. */
	bool IsValid() const { return Problems.IsEmpty(); }

	/** Décrit les causes de rejet en une ligne lisible (journal + opérateur). */
	FString DescribeProblems() const;

	/**
	 * Analyse une ligne de commande. Accepte `-clé valeur`, `-clé=valeur`,
	 * `--clé` et `--clé=valeur`, sans distinction de casse sur la clé.
	 */
	static FPrismLdkServiceOptions Parse(const FString& CommandLine);

	/** Découpe une ligne de commande en respectant les guillemets doubles. */
	static TArray<FString> Tokenize(const FString& CommandLine);
};

/** Instantané de l'état exposé par le service (aucun verrou : copie cohérente). */
struct FPrismLdkServiceState
{
	/** Le service écoute. */
	bool bListening = false;

	/** Le moteur tourne (au moins une frame rendue) : condition de readiness avec l'écoute. */
	bool bEngineRunning = false;

	/** La WebSocket SYNE est connectée. */
	bool bWsConnected = false;

	/** Une description de monde a été reçue (`world_initialized`). */
	bool bHasWorld = false;

	/** Au moins un snapshot a été reçu. */
	bool bHasSnapshot = false;

	/** Images par seconde mesurées (unité : frames/s). */
	double Fps = 0.0;

	/** Dernier tick SYNE observé. */
	int64 Tick = 0;

	/** Horizon du run en cours (`maxTicks`), 0 si inconnu. */
	int32 MaxTicks = 0;

	/** Population du dernier snapshot (`aliveCount`). */
	int32 Agents = 0;

	/** Cadence du run (ticks/seconde), 0 si inconnue. */
	int32 TicksPerSecond = 0;

	/** Nombre de requêtes servies depuis le démarrage. */
	uint64 RequestsServed = 0;

	/** URL du flux WebSocket utilisée. */
	FString WsUrl;

	/** Identifiant d'instance reçu du Launcher. */
	FString InstanceId;

	/** Identifiant de corrélation reçu du Launcher. */
	FString CorrelationId;

	/** Version du monde annoncée par SYNE (`world_initialized.version`). */
	FString WorldVersion;

	/** Identifiant du run en cours. */
	FString RunId;

	/** Dernière erreur observée (réseau, contrat). */
	FString LastError;

	/** Vrai si le service peut se déclarer prêt. */
	bool IsReady() const { return bListening && bEngineRunning; }
};

/** Résultat pur d'une route : le transport HTTP ne fait que le publier. */
struct FPrismLdkRouteResult
{
	/** Statut HTTP. */
	int32 Code = 200;

	/** Type de contenu de la réponse. */
	FString ContentType = TEXT("application/json; charset=utf-8");

	/** Corps de la réponse, déjà sérialisé. */
	FString Body;

	/** Le handler doit demander l'arrêt du processus après avoir répondu. */
	bool bShutdown = false;
};

/**
 * Résout une requête de la surface de service (fonction pure, testée par
 * `PrismLdkServiceSpec`). `SessionToken` vide ⇒ les mutations sont refusées.
 */
FPrismLdkRouteResult PrismLdkRouteRequest(const FString& Method, const FString& Path, const FString& AuthorizationHeader, const FPrismLdkServiceState& State, const FString& SessionToken);

/** Sérialise la réponse de `/info`. */
FString PrismLdkInfoJson(const FPrismLdkServiceState& State);

/** Sérialise le détail de `/health/details`. */
FString PrismLdkHealthDetailsJson(const FPrismLdkServiceState& State);

/** Sérialise les métriques au format texte Prometheus (unités documentées en tête). */
FString PrismLdkMetricsText(const FPrismLdkServiceState& State);

/** Échappe une valeur de label Prometheus (`\`, `"`, saut de ligne). */
FString PrismLdkEscapeLabel(const FString& Value);

/**
 * Compare le jeton fourni au jeton de session sans arrêt anticipé.
 * Un jeton attendu vide ne peut jamais être satisfait : sans
 * `LIVEX_SESSION_TOKEN`, aucune mutation n'est acceptée.
 */
bool PrismLdkTokensMatch(const FString& Supplied, const FString& Expected);

/**
 * Hôte du service : écoute sur `127.0.0.1:<port>` via le module moteur
 * `HTTPServer`, publie l'état reçu du jeu et déclenche l'arrêt propre.
 *
 * Les handlers HTTP s'exécutent hors du thread de jeu : l'état partagé est
 * protégé par un verrou et l'arrêt est replanifié sur le thread de jeu.
 */
class FPrismLdkService
{
public:
	/** Instance unique du module. */
	static FPrismLdkService& Get();

	/** Démarre l'écoute. Renvoie le code de sortie à appliquer (0 = succès). */
	EPrismLdkServiceExitCode Start(const FPrismLdkServiceOptions& InOptions);

	/** Retire les routes et arrête les listeners du module. Idempotent. */
	void Stop();

	/** Vrai entre `Start` réussi et `Stop`. */
	bool IsRunning() const { return bRunning; }

	/** Cause du dernier échec de démarrage, vide si aucun. */
	const FString& GetStartupError() const { return StartupError; }

	/** Arguments de service reçus mais non réclamés (journal de démarrage). */
	const TArray<FString>& GetUnclaimedArguments() const { return Options.UnclaimedArguments; }

	/** Mémorise l'URL de flux configurée (appelé par `Configure`). */
	void ReportConnectionOptions(const FString& WebSocketUrl);

	/** Met à jour l'état du flux SYNE. */
	void ReportWebSocket(bool bConnected);

	/** Mémorise la description de monde reçue. */
	void ReportWorld(const FString& Version, int32 InTicksPerSecond);

	/** Mémorise le statut d'un run. */
	void ReportRun(const FString& InRunId, int32 InMaxTicks, const FString& InWorldVersion, int64 InTick, int32 InAliveCount);

	/** Mémorise le dernier snapshot reçu. */
	void ReportSnapshot(int64 InTick, int32 InAliveCount, const FString& InRunId);

	/** Mesure la cadence de rendu et marque le moteur comme démarré. */
	void ReportFrame(double DeltaSeconds);

	/** Mémorise la dernière erreur (vide pour l'effacer). */
	void ReportError(const FString& Message);

private:
	/** Copie cohérente de l'état exposé. */
	FPrismLdkServiceState SnapshotState() const;

	/** Comptabilise puis résout une requête (partagé par tous les handlers HTTP). */
	FPrismLdkRouteResult RouteRequest(const FString& Method, const FString& Path, const FString& Authorization);

	/** Arme l'arrêt après la réponse HTTP (idempotent). */
	void ScheduleShutdown();

	/** Écrit `shutdown.ok`, ferme les sockets, vide les journaux et sort. */
	void PerformShutdown();

	mutable FCriticalSection StateLock;
	TSharedPtr<IHttpRouter> Router;
	TArray<FHttpRouteHandle> Handles;
	FPrismLdkServiceState State;
	FPrismLdkServiceOptions Options;
	FString SessionToken;
	FString StartupError;
	bool bRunning = false;
	bool bStartedListeners = false;
	bool bShutdownScheduled = false;
};

/** Imprime une ligne d'identité/diagnostic sur le log et la sortie standard du processus. */
void PrismLdkServicePrintLine(const FString& Line);
