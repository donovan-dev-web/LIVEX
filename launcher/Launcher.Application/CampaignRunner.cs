using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Protocol;
using Launcher.Protocol.Model;

namespace Launcher.Application;

/// <summary>Événement de journal d'exécution, ajouté au paquet par l'infrastructure.</summary>
public sealed record RunJournalEvent(DateTimeOffset At, string Event, string Message, string? RunId);

/// <summary>
/// Cas d'usage « campagne » (EXPERIMENTS.md). Une seule boucle d'orchestration, ordonnée,
/// avec annulation coopérative. Le Launcher planifie, attend, collecte et archive ;
/// il ne calcule rien (ADR-003).
/// </summary>
public sealed class CampaignRunner
{
    private readonly IPackageService _packages;
    private readonly IRunExecutor _executor;
    private readonly IAnalysisService? _analysis;
    private readonly IClock _clock;
    private readonly ISessionJournal _journal;

    /// <summary>
    /// Contexte de campagne courant, mémorisé pour republier l'avancement du run avec
    /// la même position dans la campagne (§8). Les runs étant séquentiels (§4), un seul
    /// run est en cours à la fois.
    /// </summary>
    private (string ExperimentId, string RunId, int Done, int Failed, int Total, long Seed) _current =
        (string.Empty, string.Empty, 0, 0, 0, 0L);

    /// <summary>Initialise le cas d'usage campagne.</summary>
    public CampaignRunner(IPackageService packages, IRunExecutor executor, IAnalysisService? analysis, IClock clock, ISessionJournal journal)
    {
        _packages = packages;
        _executor = executor;
        _analysis = analysis;
        _clock = clock;
        _journal = journal;
    }

    /// <summary>Événement de progression de campagne (vue de progression, EXPERIMENTS.md §8).</summary>
    public event EventHandler<CampaignProgress>? Progress;

    /// <summary>Crée une campagne : définition validée, paquet créé avec son squelette.</summary>
    public string CreateCampaign(ExperimentDefinition definition)
    {
        var problems = definition.Validate();
        if (problems.Count > 0)
        {
            throw new ArgumentException($"définition de campagne invalide : {string.Join(" ; ", problems)}");
        }

        var packagePath = _packages.Create(definition);
        _journal.Info("CampaignCreate", $"campagne {definition.Id} créée, paquet {Path.GetFileName(packagePath)}");
        return packagePath;
    }

    /// <summary>
    /// Exécute la campagne : runs séquentiels, politique d'échec, écriture normative du paquet.
    /// Renvoie le chemin du paquet scellé, ou lève OperationCanceledException si annulée.
    /// </summary>
    public async Task<string> ExecuteAsync(string packagePath, ExperimentDefinition definition, CancellationToken cancellationToken)
    {
        var (state, index) = _packages.ReadState(packagePath);
        if (state == PackageStates.Sealed)
        {
            throw new InvalidOperationException("campagne déjà scellée : aucune réexécution (USER_INTERFACE.md §5.2)");
        }

        // Reprise d'après runs/index.json, jamais sur l'examen des répertoires (EXPERIMENTS.md §9).
        var doneRunIds = index.Runs
            .Where(r => r.Status == RunStatuses.Termine)
            .Select(r => r.RunId)
            .ToHashSet(StringComparer.Ordinal);
        var failedByRunId = index.Runs
            .Where(r => r.Status == RunStatuses.Echoue)
            .ToDictionary(r => r.RunId, r => r, StringComparer.Ordinal);

        if (doneRunIds.Count > 0)
        {
            _journal.Info("CampaignResume", $"reprise : {doneRunIds.Count} run(s) déjà terminé(s), jamais rejoué(s)");
        }

        var durations = new List<TimeSpan>();

        try
        {
            // L'avancement du run est relayé tant que la boucle tourne (EXPERIMENTS.md §8) :
            // l'événement du moteur ne porte que l'état du run, la position dans la
            // campagne est celle déjà publiée.
            void OnTickProgress(object? sender, RunTickProgress progress) => PublishTickProgress(progress);
            _executor.TickProgress += OnTickProgress;
            try
            {
                await RunLoopAsync(packagePath, definition, cancellationToken, doneRunIds, failedByRunId, durations).ConfigureAwait(false);
            }
            finally
            {
                _executor.TickProgress -= OnTickProgress;
            }
        }
        catch (OperationCanceledException)
        {
            // Annulation entre deux runs : aucun run en cours à écrire, mais le paquet
            // est interrompu et doit être marqué récupérable (EXPERIMENTS.md §10).
            _packages.MarkRecoverable(packagePath);
            throw;
        }

        // Restitution : demande d'analyse agrégée et du rapport d'émergence à ECHOS (EXPERIMENTS.md §11).
        if (_analysis is not null)
        {
            var completedRunIds = _packages.ReadState(packagePath).Index.Runs
                .Where(run => run.Status == RunStatuses.Termine)
                .Select(run => run.RunId)
                .OrderBy(runId => runId, StringComparer.Ordinal)
                .ToArray();
            if (completedRunIds.Length == 0)
            {
                // Demander un rapport sans aucun run terminé est un 422 garanti côté ECHOS :
                // l'absence est consignée avec sa cause exacte plutôt qu'un rejet opaque.
                _journal.Warn("AnalysisUnavailable",
                    "fin de campagne : rapport d'émergence non demandé — aucun run terminé " +
                    "(l'analyse expérimentale exige au moins un run réussi)");
            }
            else
            {
                try
                {
                    var (aggregateFiles, report) = await _analysis.AnalyzeExperimentAsync(
                        definition.Id,
                        completedRunIds,
                        cancellationToken).ConfigureAwait(false);
                    if (report is not null)
                    {
                        _packages.WriteAnalysisReport(packagePath, report, aggregateFiles);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Isolation (INTEGRATION_CONTRACT.md §10.1) : une analyse expérimentale qui échoue
                    // ne fait ni échouer ni rouvrir la campagne. L'absence est consignée, jamais approximée.
                    _journal.Warn("AnalysisUnavailable",
                        $"fin de campagne : rapport d'émergence indisponible — {exception.Message}");
                }
            }
        }

        var sealedPath = _packages.Seal(packagePath);
        _journal.Info("CampaignSealed", $"campagne {definition.Id} scellée : {Path.GetFileName(sealedPath)}");
        return sealedPath;
    }

    private async Task RunLoopAsync(
        string packagePath,
        ExperimentDefinition definition,
        CancellationToken cancellationToken,
        HashSet<string> doneRunIds,
        Dictionary<string, RunIndexEntry> failedByRunId,
        List<TimeSpan> durations)
    {
        // Reprise : les runs déjà terminés comptent immédiatement comme terminés. Sans cela,
        // une campagne reprise à 4/5 afficherait 0/5 puis 1/5 — une progression fausse est
        // pire qu'absente, l'opérateur croirait un run rejoué alors qu'il est sauté.
        var runsDone = doneRunIds.Count;
        var runsFailed = 0;

        for (var runNumber = 0; runNumber < definition.RunCount; runNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runId = $"RUN-{runNumber + 1:D4}";
            if (doneRunIds.Contains(runId))
            {
                continue; // Aucune reprise d'un run réussi : propriété « Reprise exacte ».
            }

            var seed = definition.SeedFor(runNumber);
            var maxAttempts = definition.FailurePolicy == FailurePolicy.Retry ? Math.Max(1, definition.MaxRetries) : 1;
            RunResult? result = null;
            string? cause = null;
            IReadOnlyList<(string Name, byte[] Content)> failedLogs = Array.Empty<(string, byte[])>();
            RunSpec spec = null!;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var startedAt = _clock.UtcNow;
                Publish(definition.Id, runId, runsDone, runsFailed, definition.RunCount, seed);
                _journal.Info("RunStart", $"run {runId} démarré (graine {seed}, tentative {attempt})", null, runId);

                spec = new RunSpec
                {
                    ExperimentId = definition.Id,
                    RunId = runId,
                    Seed = seed,
                    Ticks = definition.Ticks,
                    TicksPerSecond = definition.TicksPerSecond,
                    AgentCount = definition.AgentCount,
                    Simulation = definition.Simulation,
                    Attempt = attempt,
                    WorkDirectory = Path.Combine(Path.GetDirectoryName(packagePath)!, "work", definition.Id, runId),
                    SessionToken = Guid.NewGuid().ToString("N"),
                };
                try
                {
                    result = await _executor.ExecuteAsync(spec, cancellationToken).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Annulation : le run passe à Annulé, rien n'est supprimé (EXPERIMENTS.md §10).
                    var cancelledEntry = new RunIndexEntry
                    {
                        RunId = runId,
                        Status = RunStatuses.Annule,
                        Seed = seed,
                        Attempt = attempt,
                        Cause = "arrêt demandé par l'utilisateur",
                        StartedAt = FormatUtc(startedAt),
                        EndedAt = FormatUtc(_clock.UtcNow),
                    };
                    _packages.CompleteRun(packagePath, new RunCompletion
                    {
                        RunId = runId,
                        IndexEntry = cancelledEntry,
                        RunJson = BuildRunJson(definition, runId, seed, attempt, RunStatuses.Annule, cancelledEntry.Cause),
                        ConfigResolvedJson = RunEngineProfile.SerializeResolved(spec, definition),
                        CompletionEvent = new RunJournalEvent(_clock.UtcNow, "run_cancelled", $"run {runId} annulé — campagne récupérable", runId),
                    });
                    _packages.MarkRecoverable(packagePath);
                    _journal.Warn("RunCancel", $"run {runId} annulé — campagne récupérable", null, runId);
                    throw;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    cause = exception.Message;
                    _journal.Fail("RunFailure", $"run {runId} en échec (tentative {attempt}) — {cause}", null, runId);
                    if (attempt >= maxAttempts)
                    {
                        // Un run Échoué conserve l'ensemble de ses journaux : le diagnostic
                        // d'un échec fait partie du paquet (EXPERIMENTS.md §6).
                        failedLogs = await CollectFailureLogsAsync(spec, exception, cancellationToken).ConfigureAwait(false);
                        result = null;
                    }
                }
            }

            var endedAt = _clock.UtcNow;
            if (result is not null)
            {
                runsDone++;
                var entry = new RunIndexEntry
                {
                    RunId = runId,
                    Status = RunStatuses.Termine,
                    Seed = seed,
                    Attempt = result.Attempt,
                    ExitCode = result.ExitCode,
                    StartedAt = FormatUtc(result.StartedAt ?? endedAt - result.Duration),
                    EndedAt = FormatUtc(result.EndedAt ?? endedAt),
                };
                durations.Add(result.Duration);

                // Versions des composants engagés, lues de leurs manifestes (EXPERIMENTS.md §12).
                if (result.ComponentVersions is { Count: > 0 } versions)
                {
                    _packages.RegisterComponents(packagePath, versions);
                }

                // Demande d'analyse du run à ECHOS : le Launcher demande, attend, collecte,
                // sans rien calculer (EXPERIMENTS.md §11, ADR-003). Son indisponibilité ne
                // fait pas échouer le run : l'absence est consignée dans le journal du paquet.
                IReadOnlyDictionary<string, byte[]> analysisFiles = new Dictionary<string, byte[]>();
                string analysisNote = "analyse individuelle non demandée";
                if (_analysis is not null)
                {
                    try
                    {
                        analysisFiles = await _analysis.AnalyzeRunAsync(result, definition.Id, cancellationToken).ConfigureAwait(false);
                        analysisNote = $"analyse individuelle collectée ({analysisFiles.Count} fichier(s))";
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        analysisNote = $"analyse individuelle indisponible — {exception.Message}";
                        _journal.Warn("AnalysisUnavailable", $"run {runId} : {analysisNote}", null, runId);
                    }
                }

                _packages.CompleteRun(packagePath, new RunCompletion
                {
                    RunId = runId,
                    IndexEntry = entry,
                    RunJson = BuildRunJson(definition, runId, seed, result.Attempt, RunStatuses.Termine, null, result),
                    ConfigResolvedJson = RunEngineProfile.SerializeResolved(spec, definition),
                    DataFiles = result.DataFiles,
                    LogFiles = result.LogFiles,
                    AnalysisFiles = analysisFiles,
                    CompletionEvent = new RunJournalEvent(endedAt, "run_completed", $"run {runId} terminé (graine {seed}) — {analysisNote}", runId),
                });
                Publish(definition.Id, runId, runsDone, runsFailed, definition.RunCount, seed);
            }
            else
            {
                runsFailed++;
                var entry = failedByRunId.TryGetValue(runId, out var previous)
                    ? new RunIndexEntry { RunId = previous.RunId, Status = RunStatuses.Echoue, Seed = previous.Seed, Attempt = maxAttempts, Cause = cause, StartedAt = previous.StartedAt, EndedAt = FormatUtc(endedAt) }
                    : new RunIndexEntry { RunId = runId, Status = RunStatuses.Echoue, Seed = seed, Attempt = maxAttempts, Cause = cause, EndedAt = FormatUtc(endedAt) };
                _packages.CompleteRun(packagePath, new RunCompletion
                {
                    RunId = runId,
                    IndexEntry = entry,
                    RunJson = BuildRunJson(definition, runId, seed, maxAttempts, RunStatuses.Echoue, cause),
                    ConfigResolvedJson = RunEngineProfile.SerializeResolved(spec, definition),
                    LogFiles = failedLogs,
                    CompletionEvent = new RunJournalEvent(endedAt, "run_failed", $"run {runId} en échec — {cause}", runId),
                });
                Publish(definition.Id, runId, runsDone, runsFailed, definition.RunCount, seed);

                if (definition.FailurePolicy == FailurePolicy.Stop)
                {
                    _packages.MarkRecoverable(packagePath);
                    _journal.Warn("CampaignStop", $"politique « stop » : campagne interrompue au run {runId}", null, runId);
                    throw new CampaignStoppedException(packagePath, runId, cause);
                }
            }
        }
    }

    /// <summary>Annule proprement : le run en cours est annulé, le paquet reste valide et scellable.</summary>
    public void Cancel(CancellationTokenSource source)
    {
        _journal.Warn("CampaignCancel", "annulation demandée par l'utilisateur");
        source.Cancel();
    }

    /// <summary>Estimation de fin : moyenne des runs terminés, jamais une extrapolation scientifique.</summary>
    public TimeSpan? EstimateRemaining(IReadOnlyList<TimeSpan> completedRunDurations, int runsRemaining)
    {
        if (completedRunDurations.Count == 0 || runsRemaining <= 0)
        {
            return null;
        }

        var average = TimeSpan.FromTicks(completedRunDurations.Sum(d => d.Ticks) / completedRunDurations.Count);
        return TimeSpan.FromTicks(average.Ticks * runsRemaining);
    }

    /// <summary>
    /// Collecte les journaux d'un run échoué depuis son dossier de travail : le diagnostic
    /// d'un échec fait partie du paquet (EXPERIMENTS.md §6). L'infrastructure expose cette
    /// collecte par le port de l'exécuteur ; le cas d'usage, lui, ne lit aucun contenu
    /// scientifique — il archive des journaux techniques.
    /// </summary>
    private async Task<IReadOnlyList<(string Name, byte[] Content)>> CollectFailureLogsAsync(RunSpec spec, Exception failure, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var logsDirectory = Path.Combine(spec.WorkDirectory, "logs");
        var logs = new List<(string, byte[])>();
        if (Directory.Exists(logsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(logsDirectory))
            {
                logs.Add((Path.GetFileName(file), File.ReadAllBytes(file)));
            }
        }

        // Le message d'incident lui-même est un fait d'exécution, pas une interprétation.
        logs.Add(("failure.txt", System.Text.Encoding.UTF8.GetBytes(
            $"run {spec.RunId} (tentative {spec.Attempt})\ncause : {failure.Message}\n")));
        return logs;
    }

    private void Publish(string experimentId, string runId, int done, int failed, int total, long seed)
    {
        // Mémorisé pour que l'avancement rapporté par le moteur puisse être republié
        // avec le même contexte de campagne (§8).
        _current = (experimentId, runId, done, failed, total, seed);
        Progress?.Invoke(this, new CampaignProgress
        {
            ExperimentId = experimentId,
            CurrentRunId = runId,
            CurrentSeed = seed,
            RunsDone = done,
            RunsFailed = failed,
            RunsTotal = total,
        });
    }

    /// <summary>Republie l'avancement du run avec le contexte de campagne courant.</summary>
    private void PublishTickProgress(RunTickProgress progress)
    {
        var (experimentId, runId, done, failed, total, seed) = _current;
        if (runId.Length == 0)
        {
            return; // Aucun run en cours : rien à progresser.
        }

        Progress?.Invoke(this, new CampaignProgress
        {
            ExperimentId = experimentId,
            CurrentRunId = runId,
            CurrentSeed = seed,
            RunsDone = done,
            RunsFailed = failed,
            RunsTotal = total,
            CurrentRunProgress = progress,
        });
    }

    private static string FormatUtc(DateTimeOffset moment) => moment.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static string BuildRunJson(ExperimentDefinition definition, string runId, long seed, int attempt, string status, string? cause, RunResult? result = null) =>
        ContractJson.Serialize(new Dictionary<string, object?>
        {
            ["schema"] = PackageConstants.SchemaVersion,
            ["runId"] = runId,
            ["experimentId"] = definition.Id,
            ["status"] = status,
            ["attempt"] = attempt,
            ["seed"] = seed,
            ["ticksRequested"] = definition.Ticks,
            ["simulation"] = definition.Simulation,
            ["cause"] = cause,
            ["exitCode"] = result?.ExitCode,
            ["startedAt"] = result?.StartedAt is { } started ? FormatUtc(started) : null,
            ["endedAt"] = result?.EndedAt is { } ended ? FormatUtc(ended) : null,
            ["platform"] = new Dictionary<string, object?>
            {
                ["os"] = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux",
                ["arch"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
                ["dotnet"] = System.Environment.Version.ToString(),
            },
        });
}

/// <summary>Progression d'une campagne, exposée à l'interface (EXPERIMENTS.md §8).</summary>
public sealed class CampaignProgress : EventArgs
{
    /// <summary>Identifiant de campagne.</summary>
    public string ExperimentId { get; init; } = string.Empty;

    /// <summary>Run en cours.</summary>
    public string CurrentRunId { get; init; } = string.Empty;

    /// <summary>Graine du run en cours.</summary>
    public long CurrentSeed { get; init; }

    /// <summary>Runs terminés.</summary>
    public int RunsDone { get; init; }

    /// <summary>Runs échoués.</summary>
    public int RunsFailed { get; init; }

    /// <summary>Runs total.</summary>
    public int RunsTotal { get; init; }

    /// <summary>
    /// Avancement du run en cours, rapporté par le moteur (§8). Nul quand le moteur
    /// ne publie pas d'état : l'interface montre alors la progression de campagne
    /// seule, plutôt qu'un pourcentage inventé.
    /// </summary>
    public RunTickProgress? CurrentRunProgress { get; init; }
}

/// <summary>Politique « stop » : le premier échec interrompt la campagne, paquet récupérable.</summary>
public sealed class CampaignStoppedException : Exception
{
    public CampaignStoppedException(string packagePath, string runId, string? cause)
        : base($"campagne interrompue au run {runId} (politique « stop ») — {cause ?? "cause inconnue"}")
    {
        PackagePath = packagePath;
        RunId = runId;
    }

    /// <summary>Chemin du paquet récupérable.</summary>
    public string PackagePath { get; }

    /// <summary>Run fautif.</summary>
    public string RunId { get; }
}
