using Launcher.Domain.Model;
using Launcher.Protocol.Model;

namespace Launcher.Domain;

/// <summary>Abstraction du lancement et de l'arrêt des processus de composants (ARCHITECTURE.md §1).</summary>
public interface IProcessManager
{
    /// <summary>Démarre un composant et renvoie le PID. Le confinement anti-orphelin est de la responsabilité de l'implémentation.</summary>
    Task<int> StartAsync(ProcessLaunchSpec spec, CancellationToken cancellationToken);

    /// <summary>Arrêt propre : commande HTTP avec jeton, délai de grâce, puis arrêt forcé de l'arbre.</summary>
    Task<ProcessExit> StopAsync(string instanceId, int processId, Uri controlEndpoint, string sessionToken, TimeSpan graceful, CancellationToken cancellationToken);

    /// <summary>Arrêt forcé immédiat de l'arbre de processus.</summary>
    Task KillAsync(string instanceId, int processId, CancellationToken cancellationToken);

    /// <summary>Le processus est-il encore vivant ?</summary>
    bool IsAlive(int processId);

    /// <summary>Événement levé quand un processus se termine de lui-même.</summary>
    event EventHandler<ProcessExitedEventArgs> Exited;
}

/// <summary>Spécification de lancement préparée par le domaine, exécutée par l'infrastructure.</summary>
public sealed class ProcessLaunchSpec
{
    /// <summary>Identifiant d'instance.</summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>Chemin absolu de l'exécutable.</summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>Répertoire de travail.</summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>Arguments complets de ligne de commande. Liste mutable : les bancs de tests y ajoutent leurs pannes injectables.</summary>
    public List<string> Arguments { get; init; } = new();

    /// <summary>Variables d'environnement additionnelles (jeton, corrélation, ports). Jamais le jeton en argument.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>Fichier de journal stdout de l'instance.</summary>
    public string StdOutLogPath { get; init; } = string.Empty;

    /// <summary>Fichier de journal stderr de l'instance.</summary>
    public string StdErrLogPath { get; init; } = string.Empty;
}

/// <summary>Résultat normalisé d'une fin de processus.</summary>
public sealed record ProcessExit(int ExitCode, string Outcome, bool Graceful);

/// <summary>Arguments de l'événement de fin de processus.</summary>
public sealed class ProcessExitedEventArgs : EventArgs
{
    /// <summary>Identifiant d'instance concerné.</summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>Code de sortie du processus.</summary>
    public int ExitCode { get; init; }

    /// <summary>Résultat normalisé (INTEGRATION_CONTRACT.md §4).</summary>
    public string Outcome { get; init; } = string.Empty;
}

/// <summary>Sonde de santé d'un composant (INTEGRATION_CONTRACT.md §6).</summary>
public interface IHealthProbe
{
    /// <summary>
    /// Interroge le point de santé « prêt » et renvoie vrai si prêt, avec la cause en cas d'échec.
    /// Le chemin est déclaré au manifeste (« health.path ») : le défaut /health/ready vaut pour
    /// tout composant conforme au §6 ; un composant réel peut déclarer le sien (ex. /health).
    /// </summary>
    Task<(bool Ready, string? Cause)> ProbeReadyAsync(Uri controlEndpoint, CancellationToken cancellationToken, string readyPath = "/health/ready");

    /// <summary>Interroge /info pour vérifier identité et version.</summary>
    Task<ComponentInfo?> FetchInfoAsync(Uri controlEndpoint, CancellationToken cancellationToken);
}

/// <summary>Contenu de /info d'un composant.</summary>
public sealed record ComponentInfo(string ComponentId, string Version, int? ProtocolVersion);

/// <summary>Horloge injectable, pour les tests.</summary>
public interface IClock
{
    /// <summary>Heure courante, UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>Horloge réelle.</summary>
public sealed class SystemClock : IClock
{
    /// <summary>Heure courante du système, UTC.</summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Journal de session du Launcher : événements datés, corrélés (ARCHITECTURE.md §6).</summary>
public interface ISessionJournal
{
    /// <summary>Écrit un événement de session.</summary>
    void Log(SessionEvent entry);

    /// <summary>Écrit un événement d'information.</summary>
    void Info(string operation, string message, string? correlationId = null, string? instanceId = null);

    /// <summary>Écrit un événement d'avertissement.</summary>
    void Warn(string operation, string message, string? correlationId = null, string? instanceId = null);

    /// <summary>Écrit un événement d'erreur.</summary>
    void Fail(string operation, string message, string? correlationId = null, string? instanceId = null);
}

/// <summary>Événement du journal de session, schéma JSON Lines (OBSERVABILITY.md §7.1).</summary>
public sealed class SessionEvent
{
    /// <summary>Horodatage UTC ISO 8601.</summary>
    public string Ts { get; init; } = string.Empty;

    /// <summary>Info, Warn, Error.</summary>
    public string Level { get; init; } = "Info";

    /// <summary>Composant émetteur : « launcher » ou l'identifiant du composant concerné.</summary>
    public string Component { get; init; } = "launcher";

    /// <summary>Identifiant d'instance si applicable.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("instanceId")]
    public string? InstanceId { get; init; }

    /// <summary>Opération en cours : Start, Stop, Run, Campaign…</summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>Identifiant de corrélation de l'opération.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Identifiant d'expérience si applicable.</summary>
    public string? ExperimentId { get; init; }

    /// <summary>Identifiant de run si applicable.</summary>
    public string? RunId { get; init; }

    /// <summary>Message lisible, en français.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>Stockage des paquets et de l'espace de travail (ARCHITECTURE.md §1, couche infrastructure).</summary>
public interface IPackageStore
{
    /// <summary>Racine des paquets vivants.</summary>
    string PackagesRoot { get; }

    /// <summary>Racine des archives scellées.</summary>
    string ArchivesRoot { get; }

    /// <summary>Crée un paquet pour une campagne et renvoie son chemin.</summary>
    Task<string> CreatePackageAsync(ExperimentDefinition experiment, CancellationToken cancellationToken);

    /// <summary>Ouvre un paquet existant en lecture.</summary>
    Task<PackageSummary> OpenAsync(string packagePath, CancellationToken cancellationToken);

    /// <summary>Scelle un paquet vivant.</summary>
    Task<string> SealAsync(string packagePath, CancellationToken cancellationToken);
}

/// <summary>Résumé de lecture d'un paquet.</summary>
public sealed class PackageSummary
{
    /// <summary>Chemin du paquet.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Manifeste lu.</summary>
    public PackageManifest Manifest { get; init; } = new();

    /// <summary>Index des runs.</summary>
    public RunIndex Runs { get; init; } = new();

    /// <summary>Vrai si le paquet est lisible et cohérent.</summary>
    public bool Valid { get; init; }
}
