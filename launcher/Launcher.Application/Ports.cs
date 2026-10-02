using Launcher.Domain.Model;
using Launcher.Protocol.Model;

namespace Launcher.Application;

/// <summary>Ports sortants de la couche application (ARCHITECTURE.md §1 : l'application dépend du domaine).</summary>
public interface IPackageService
{
    /// <summary>Crée le paquet d'une campagne et renvoie son chemin.</summary>
    string Create(ExperimentDefinition experiment);

    /// <summary>Clôture un run dans le paquet selon la séquence normative §5.2.</summary>
    void CompleteRun(string packagePath, RunCompletion completion);

    /// <summary>Scelle le paquet d'une campagne.</summary>
    string Seal(string packagePath);

    /// <summary>Marque le paquet comme récupérable après interruption.</summary>
    void MarkRecoverable(string packagePath);

    /// <summary>Écrit le rapport d'émergence et les fichiers d'analyse agrégée (canal 3 — restitution).</summary>
    void WriteAnalysisReport(string packagePath, string emergenceReport, IReadOnlyDictionary<string, byte[]> aggregateFiles);

    /// <summary>Enregistre les versions des composants engagés dans le manifeste (EXPERIMENTS.md §12).</summary>
    void RegisterComponents(string packagePath, IReadOnlyList<Protocol.Model.JsonComponentRef> components);

    /// <summary>Lit l'état du paquet et son index des runs.</summary>
    (string State, RunIndex Index) ReadState(string packagePath);
}

/// <summary>Résultat d'un run exécuté par le moteur.</summary>
public sealed record RunResult(
    string RunId,
    long Seed,
    string Status,
    int Attempt,
    string? Cause,
    IReadOnlyDictionary<string, byte[]> DataFiles,
    IReadOnlyList<(string Name, byte[] Content)> LogFiles,
    long TicksReached,
    TimeSpan Duration,
    int ExitCode = 0,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? EndedAt = null,
    IReadOnlyList<Protocol.Model.JsonComponentRef>? ComponentVersions = null);

/// <summary>Éléments écrits dans le paquet à la clôture d'un run.</summary>
public sealed class RunCompletion
{
    /// <summary>Identifiant du run (RUN-nnnn).</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Entrée d'index à enregistrer (source de reprise).</summary>
    public RunIndexEntry IndexEntry { get; init; } = new();

    /// <summary>Contenu de run.json.</summary>
    public string RunJson { get; init; } = string.Empty;

    /// <summary>Contenu de config.resolved.json.</summary>
    public string ConfigResolvedJson { get; init; } = string.Empty;

    /// <summary>Fichiers de données du run, collectés depuis les composants.</summary>
    public IReadOnlyDictionary<string, byte[]> DataFiles { get; init; } = new Dictionary<string, byte[]>();

    /// <summary>Journaux corrélés au run.</summary>
    public IReadOnlyList<(string Name, byte[] Content)> LogFiles { get; init; } = Array.Empty<(string, byte[])>();

    /// <summary>Événement de journal à ajouter au paquet.</summary>
    public RunJournalEvent? CompletionEvent { get; set; }

    /// <summary>Analyse individuelle d'ECHOS pour ce run, écrite sous analysis/individual/.</summary>
    public IReadOnlyDictionary<string, byte[]> AnalysisFiles { get; init; } = new Dictionary<string, byte[]>();

    /// <summary>Versions des composants engagés, lues de leurs manifestes (EXPERIMENTS.md §12).</summary>
    public IReadOnlyList<Protocol.Model.JsonComponentRef> ComponentVersions { get; init; } = Array.Empty<Protocol.Model.JsonComponentRef>();
}

/// <summary>Exécution d'un run par le moteur (pilotage SYNE, réel ou stub).</summary>
public interface IRunExecutor
{
    /// <summary>Exécute un run et renvoie son résultat normalisé.</summary>
    Task<RunResult> ExecuteAsync(RunSpec spec, CancellationToken cancellationToken);
}

/// <summary>Spécification d'exécution d'un run, résolue par l'application.</summary>
public sealed class RunSpec
{
    /// <summary>Identifiant de campagne.</summary>
    public string ExperimentId { get; init; } = string.Empty;

    /// <summary>Identifiant du run (RUN-nnnn).</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Graine dérivée du run.</summary>
    public long Seed { get; init; }

    /// <summary>Horizon de ticks.</summary>
    public long Ticks { get; init; }

    /// <summary>Nombre d'agents initiaux demandé au moteur.</summary>
    public int AgentCount { get; init; } = 50;

    /// <summary>Identifiant de la simulation à charger.</summary>
    public string Simulation { get; init; } = string.Empty;

    /// <summary>Numéro de tentative (politique retry).</summary>
    public int Attempt { get; init; } = 1;

    /// <summary>Répertoire de travail du run, où le composant écrit ses sorties.</summary>
    public string WorkDirectory { get; init; } = string.Empty;

    /// <summary>Jeton de session, transmis par variable d'environnement.</summary>
    public string SessionToken { get; init; } = string.Empty;
}

/// <summary>Demande d'analyse à ECHOS (ADR-003 : le Launcher demande, ECHOS produit).</summary>
public interface IAnalysisService
{
    /// <summary>Demande l'analyse d'un run. Renvoie les fichiers produits.</summary>
    Task<IReadOnlyDictionary<string, byte[]>> AnalyzeRunAsync(RunResult run, string experimentId, CancellationToken cancellationToken);

    /// <summary>Demande l'analyse agrégée et le rapport d'émergence en fin de campagne.</summary>
    Task<(IReadOnlyDictionary<string, byte[]> AggregateFiles, string? EmergenceReport)> AnalyzeExperimentAsync(string experimentId, CancellationToken cancellationToken);
}
