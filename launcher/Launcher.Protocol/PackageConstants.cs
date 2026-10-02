namespace Launcher.Protocol;

/// <summary>
/// Constantes de contrat partagées. Versionnées avec soin : tout changement de sens
/// impose un incrément de version (PACKAGE_FORMAT.md §7).
/// </summary>
public static class PackageConstants
{
    /// <summary>Version de schéma du format de paquet et des contrats JSON du Launcher.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Nom du manifeste du paquet.</summary>
    public const string ManifestEntry = "manifest.json";

    /// <summary>Nom de la définition de campagne dans le paquet.</summary>
    public const string ExperimentEntry = "experiment.json";

    /// <summary>Nom du journal d'exécution, ajout seul.</summary>
    public const string JournalEntry = "journal.ndjson";

    /// <summary>Nom de la provenance (qui, quand, quelles versions, quelle plateforme).</summary>
    public const string ProvenanceEntry = "provenance.json";

    /// <summary>Nom de l'index des runs, source de reprise.</summary>
    public const string RunsIndexEntry = "runs/index.json";

    /// <summary>Nom du fichier d'intégrité d'un run.</summary>
    public const string RunIntegrityEntry = "integrity.json";

    /// <summary>Préfixe des répertoires de run, numérotés à partir de 1 sur quatre chiffres.</summary>
    public const string RunDirectoryPrefix = "RUN-";

    /// <summary>Nom du rapport d'émergence, fichier de référence (DATA_FLOW.md §3.1).</summary>
    public const string EmergenceReportEntry = "analysis/emergence_report.md";

    /// <summary>Horodatage d'entrée ZIP fixé pour le déterminisme (PACKAGE_FORMAT.md §6) : 1980-01-01 00:00:00 UTC, borne du format DOS.</summary>
    public const long ZipEpochTimestamp = 315532800L;
}

/// <summary>Statuts d'un run (EXPERIMENTS.md §6). Chaînes stables, portées par runs/index.json.</summary>
public static class RunStatuses
{
    public const string Planifie = "Planifie";
    public const string EnCours = "EnCours";
    public const string Termine = "Termine";
    public const string Echoue = "Echoue";
    public const string Annule = "Annule";
}

/// <summary>Codes de sortie normatifs des composants (INTEGRATION_CONTRACT.md §4).</summary>
public static class ExitCodes
{
    public const int Normal = 0;
    public const int UnspecifiedError = 1;
    public const int ConfigurationError = 2;
    public const int PortUnavailable = 3;
    public const int MissingResource = 4;
    public const int VersionMismatch = 5;
    public const int SimulationError = 6;
    public const int FileSystemError = 7;
    public const int InterruptedBySignal = 130;
    public const int TerminatedBySignal = 143;
}

/// <summary>Résultat normalisé d'une fin de processus (INTEGRATION_CONTRACT.md §4).</summary>
public static class ExitOutcomeNames
{
    public const string Completed = "Completed";
    public const string UnknownError = "UnknownError";
    public const string Cancelled = "Cancelled";
    public const string ConfigurationError = "ConfigurationError";
    public const string InstallationError = "InstallationError";
    public const string CommunicationError = "CommunicationError";
    public const string SimulationError = "SimulationError";
    public const string AnalysisError = "AnalysisError";
    public const string FileSystemError = "FileSystemError";
    public const string ProcessError = "ProcessError";
}

/// <summary>Corrélation propagée dans les opérations (OBSERVABILITY.md §7.2).</summary>
public static class CorrelationHeaders
{
    /// <summary>En-tête HTTP de corrélation, entre le Launcher et les composants.</summary>
    public const string HeaderCorrelationId = "X-Livex-Correlation-Id";

    /// <summary>Variable d'environnement de corrélation, transmise aux processus lancés.</summary>
    public const string CorrelationIdEnvVar = "LIVEX_CORRELATION_ID";

    /// <summary>Argument de ligne de commande de corrélation (INTEGRATION_CONTRACT.md §3.1).</summary>
    public const string CorrelationIdArgument = "--correlation-id";

    public const string SessionTokenEnvVar = "LIVEX_SESSION_TOKEN";
    public const string InstallRootEnvVar = "LIVEX_INSTALL_ROOT";
}
