using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Package;
using Launcher.Protocol;
using Launcher.Protocol.Model;
using Launcher.Tests.Unit.Domain;
using Xunit;

namespace Launcher.Tests.Unit.Campaign;

/// <summary>Service de paquets de test : réalise IPackageService par Launcher.Package, sans processus.</summary>
public sealed class TestPackageService : IPackageService
{
    /// <summary>Racine des paquets de test.</summary>
    public string Root { get; }

    /// <summary>Initialise le service sur un répertoire de test.</summary>
    public TestPackageService(string root)
    {
        Root = root;
        Directory.CreateDirectory(root);
    }

    /// <inheritdoc />
    public string Create(ExperimentDefinition experiment)
    {
        var path = Path.Combine(Root, $"{experiment.Id}.livexp");
        using var writer = LivexPackageWriter.CreateNew(path, experiment, "0.1.0-test", DateTimeOffset.UnixEpoch);
        return path;
    }

    /// <inheritdoc />
    public void CompleteRun(string packagePath, RunCompletion completion)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        writer.CompleteRun(
            completion.RunId,
            completion.IndexEntry,
            completion.RunJson,
            completion.ConfigResolvedJson,
            completion.DataFiles,
            completion.LogFiles,
            completion.CompletionEvent is { } @event
                ? JournalLine.Pack(@event.At, @event.Event, @event.Message, @event.RunId)
                : JournalLine.Pack(DateTimeOffset.UnixEpoch, "run_completed", $"run {completion.RunId}", completion.RunId),
            completion.AnalysisFiles);
    }

    /// <inheritdoc />
    public string Seal(string packagePath)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        return writer.Seal(DateTimeOffset.UnixEpoch);
    }

    /// <inheritdoc />
    public void MarkRecoverable(string packagePath)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        writer.MarkRecoverable();
    }

    /// <inheritdoc />
    public void WriteAnalysisReport(string packagePath, string emergenceReport, IReadOnlyDictionary<string, byte[]> aggregateFiles)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        writer.WriteEntry(PackageConstants.EmergenceReportEntry, emergenceReport);
        foreach (var (entryName, content) in aggregateFiles)
        {
            writer.WriteEntry($"analysis/aggregate/{entryName}", content);
        }
    }

    /// <inheritdoc />
    public void RegisterComponents(string packagePath, IReadOnlyList<JsonComponentRef> components)
    {
        using var writer = LivexPackageWriter.OpenForAppend(packagePath);
        writer.RegisterComponents(components);
    }

    /// <inheritdoc />
    public (string State, RunIndex Index) ReadState(string packagePath)
    {
        using var reader = new LivexPackageReader(packagePath);
        return (reader.Manifest.State, reader.RunIndex);
    }
}

/// <summary>Exécuteur scripté : réussit, échoue ou annule selon la configuration du test.</summary>
public sealed class ScriptedRunExecutor : IRunExecutor
{
    private readonly Func<RunSpec, RunResult> _script;

    /// <summary>Runs effectivement exécutés, dans l'ordre.</summary>
    public List<string> Executed { get; } = new();

    /// <summary>Initialise l'exécuteur avec son script.</summary>
    public ScriptedRunExecutor(Func<RunSpec, RunResult> script)
    {
        _script = script;
    }

    /// <inheritdoc />
    public Task<RunResult> ExecuteAsync(RunSpec spec, CancellationToken cancellationToken)
    {
        Executed.Add(spec.RunId);
        return Task.FromResult(_script(spec));
    }
}

/// <summary>
/// Scénarios de campagne (EXPERIMENTS.md, TESTING.md §8) : campagne nominale, politique d'échec,
/// reprise exacte, annulation. Tout est vérifié sans démarrer un seul composant.
/// </summary>
public sealed class CampaignRunnerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"livexp-campaign-{Guid.NewGuid():N}");
    private readonly InMemoryJournal _journal = new();

    public CampaignRunnerTests()
    {
        Directory.CreateDirectory(_directory);
    }

    private ExperimentDefinition Definition(int runs, FailurePolicy policy = FailurePolicy.Continue) => new()
    {
        Id = "EXP-CAMP-001",
        Title = "Campagne de test",
        Profile = WellKnownProfiles.Analyse,
        Simulation = "ecosystem_01",
        RunCount = runs,
        Ticks = 100,
        SeedStrategy = SeedStrategy.Derived,
        BaseSeed = 500,
        FailurePolicy = policy,
        MaxRetries = 1,
    };

    private RunResult Success(RunSpec spec) => new(
        spec.RunId, spec.Seed, RunStatuses.Termine, spec.Attempt, null,
        new Dictionary<string, byte[]> { ["data/result.json"] = System.Text.Encoding.UTF8.GetBytes($"{{\"seed\":{spec.Seed}}}") },
        Array.Empty<(string, byte[])>(), spec.Ticks, TimeSpan.FromMilliseconds(10));

    // ------------------------------------------------------------------
    // Scénario « campagne nominale » : création, exécution de tous les runs,
    // scellement, relecture (TESTING.md §8).
    // ------------------------------------------------------------------
    [Fact]
    public async Task Campagne_nominale_produit_paquet_scelle_relisible()
    {
        var packages = new TestPackageService(_directory);
        var executor = new ScriptedRunExecutor(Success);
        var runner = new CampaignRunner(packages, executor, null, new SystemClock(), _journal);
        var definition = Definition(3);

        var packagePath = runner.CreateCampaign(definition);
        var sealedPath = await runner.ExecuteAsync(packagePath, definition, CancellationToken.None);

        Assert.Equal(packagePath, sealedPath);
        using var reader = new LivexPackageReader(sealedPath);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        Assert.Equal(3, reader.Manifest.Counts.RunsDone);
        Assert.Equal(0, reader.Manifest.Counts.RunsFailed);
        Assert.Equal(3, reader.RunIndex.Runs.Count(r => r.Status == RunStatuses.Termine));
        // Les graines sont dérivées et écrites : baseSeed + n.
        Assert.Equal(500, reader.RunIndex.Runs[0].Seed);
        Assert.Equal(502, reader.RunIndex.Runs[2].Seed);
        // La configuration résolue est écrite pour chaque run.
        Assert.NotNull(reader.ReadEntry("runs/RUN-0001/config.resolved.json"));
        // Un paquet scellé refuse toute réexécution.
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ExecuteAsync(sealedPath, definition, CancellationToken.None));
    }

    // ------------------------------------------------------------------
    // Politique « stop » : le premier échec interrompt la campagne, paquet récupérable.
    // ------------------------------------------------------------------
    [Fact]
    public async Task Politique_stop_interrompt_et_marque_recoverable()
    {
        var packages = new TestPackageService(_directory);
        var executor = new ScriptedRunExecutor(spec =>
            spec.RunId == "RUN-0002"
                ? throw new InvalidOperationException("moteur en échec injecté")
                : Success(spec));
        var runner = new CampaignRunner(packages, executor, null, new SystemClock(), _journal);
        var definition = Definition(4, FailurePolicy.Stop);

        var packagePath = runner.CreateCampaign(definition);
        var exception = await Assert.ThrowsAsync<CampaignStoppedException>(
            () => runner.ExecuteAsync(packagePath, definition, CancellationToken.None));

        Assert.Equal("RUN-0002", exception.RunId);
        var (state, index) = packages.ReadState(packagePath);
        Assert.Equal(PackageStates.Recoverable, state);
        Assert.Single(index.Runs, r => r.Status == RunStatuses.Termine);
        Assert.Single(index.Runs, r => r.Status == RunStatuses.Echoue);
    }

    // ------------------------------------------------------------------
    // Politique « continue » : la campagne se termine malgré les échecs, runsFailed non nul.
    // ------------------------------------------------------------------
    [Fact]
    public async Task Politique_continue_consigne_les_echecs_et_scelle()
    {
        var packages = new TestPackageService(_directory);
        var executor = new ScriptedRunExecutor(spec =>
            spec.RunId == "RUN-0002"
                ? throw new InvalidOperationException("échec exploratoire")
                : Success(spec));
        var runner = new CampaignRunner(packages, executor, null, new SystemClock(), _journal);
        var definition = Definition(3, FailurePolicy.Continue);

        var packagePath = runner.CreateCampaign(definition);
        var sealedPath = await runner.ExecuteAsync(packagePath, definition, CancellationToken.None);

        using var reader = new LivexPackageReader(sealedPath);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        Assert.Equal(2, reader.Manifest.Counts.RunsDone);
        Assert.Equal(1, reader.Manifest.Counts.RunsFailed);
        // Un paquet scellé peut contenir des runs échoués, à condition que le manifeste le dise (EXPERIMENTS.md §7).
        Assert.Single(reader.RunIndex.Runs, r => r.Status == RunStatuses.Echoue && r.Cause!.Contains("exploratoire"));
    }

    // ------------------------------------------------------------------
    // Scénario « pause et reprise » : reprise sans rejouer un run réussi (TESTING.md §8).
    // ------------------------------------------------------------------
    [Fact]
    public async Task Reprise_ne_rejoue_aucun_run_termine()
    {
        var packages = new TestPackageService(_directory);
        var executor = new ScriptedRunExecutor(Success);
        var runner = new CampaignRunner(packages, executor, null, new SystemClock(), _journal);
        var definition = Definition(3);

        var packagePath = runner.CreateCampaign(definition);

        // Première exécution interrompue pendant le deuxième run (annulation coopérative).
        using (var source = new CancellationTokenSource())
        {
            var executorScript = new ScriptedRunExecutor(spec =>
            {
                if (spec.RunId == "RUN-0002")
                {
                    source.Cancel();
                    throw new OperationCanceledException(source.Token);
                }

                return Success(spec);
            });
            var interruptible = new CampaignRunner(packages, executorScript, null, new SystemClock(), _journal);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => interruptible.ExecuteAsync(packagePath, definition, source.Token));
        }

        var (stateAfterCancel, indexAfterCancel) = packages.ReadState(packagePath);
        Assert.Equal(PackageStates.Recoverable, stateAfterCancel);
        Assert.Single(indexAfterCancel.Runs, r => r.Status == RunStatuses.Termine && r.RunId == "RUN-0001");
        Assert.Single(indexAfterCancel.Runs, r => r.Status == RunStatuses.Annule && r.RunId == "RUN-0002");

        // Reprise : RUN-0001 n'est jamais rejoué, RUN-0002 et RUN-0003 s'exécutent.
        var executed = new List<string>();
        var resumingExecutor = new ScriptedRunExecutor(spec =>
        {
            executed.Add(spec.RunId);
            return Success(spec);
        });
        var resuming = new CampaignRunner(packages, resumingExecutor, null, new SystemClock(), _journal);
        var sealedPath = await resuming.ExecuteAsync(packagePath, definition, CancellationToken.None);

        Assert.Equal(["RUN-0002", "RUN-0003"], executed);
        using var reader = new LivexPackageReader(sealedPath);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        Assert.Equal(3, reader.Manifest.Counts.RunsDone);
        // La graine du run rejoué est exactement la même (EXPERIMENTS.md §9).
        Assert.Equal(501, reader.RunIndex.Runs.First(r => r.RunId == "RUN-0002").Seed);
    }

    // ------------------------------------------------------------------
    // Restitution : le rapport d'émergence d'ECHOS est archivé au scellement (canal 3).
    // ------------------------------------------------------------------
    [Fact]
    public async Task Rapport_d_emergence_archive_avant_scellement()
    {
        var packages = new TestPackageService(_directory);
        var executor = new ScriptedRunExecutor(Success);
        var analysisCalls = 0;
        var analysis = new RecordingAnalysisService(() => analysisCalls++, "# Rapport d'émergence\n\nProduit par ECHOS.");
        var runner = new CampaignRunner(packages, executor, analysis, new SystemClock(), _journal);
        var definition = Definition(1);

        var packagePath = runner.CreateCampaign(definition);
        var sealedPath = await runner.ExecuteAsync(packagePath, definition, CancellationToken.None);

        Assert.Equal(1, analysisCalls);
        using var reader = new LivexPackageReader(sealedPath);
        Assert.Contains("Rapport d'émergence", reader.ReadEmergenceReport());
    }

    // ------------------------------------------------------------------
    // ADR-003 / EXPERIMENTS.md §11 : AnalyzeRun est demandé pour chaque run terminé,
    // et son résultat est archivé sous analysis/individual/.
    // ------------------------------------------------------------------
    [Fact]
    public async Task AnalyzeRun_demande_par_run_et_archive_en_individual()
    {
        var packages = new TestPackageService(_directory);
        var executor = new ScriptedRunExecutor(Success);
        var analyzeRunCalls = 0;
        var analysis = new RecordingAnalysisService(
            onAnalyzeExperiment: () => 0,
            report: "# Rapport",
            onAnalyzeRun: () => analyzeRunCalls++,
            runFiles: new Dictionary<string, byte[]> { ["RUN-0001.json"] = System.Text.Encoding.UTF8.GetBytes("{}") });
        var runner = new CampaignRunner(packages, executor, analysis, new SystemClock(), _journal);
        var definition = Definition(1);

        var packagePath = runner.CreateCampaign(definition);
        var sealedPath = await runner.ExecuteAsync(packagePath, definition, CancellationToken.None);

        Assert.Equal(1, analyzeRunCalls);
        using var reader = new LivexPackageReader(sealedPath);
        var individual = reader.ReadEntry("analysis/individual/RUN-0001.json");
        Assert.NotNull(individual);
        Assert.Equal(1, reader.Manifest.Counts.RunsDone);
    }

    // ------------------------------------------------------------------
    // ADR-003 : l'indisponibilité d'ECHOS ne fait pas échouer le run ; l'absence est consignée.
    // ------------------------------------------------------------------
    [Fact]
    public async Task Echos_indisponible_ne_fait_pas_echouer_le_run()
    {
        var packages = new TestPackageService(_directory);
        var executor = new ScriptedRunExecutor(Success);
        var analysis = new RecordingAnalysisService(() => 0, null, onAnalyzeRun: () => throw new InvalidOperationException("analyste fautif"));
        var runner = new CampaignRunner(packages, executor, analysis, new SystemClock(), _journal);
        var definition = Definition(1);

        var packagePath = runner.CreateCampaign(definition);
        var sealedPath = await runner.ExecuteAsync(packagePath, definition, CancellationToken.None);

        using var reader = new LivexPackageReader(sealedPath);
        Assert.Equal(1, reader.Manifest.Counts.RunsDone);
        Assert.Null(reader.ReadEntry("analysis/individual/RUN-0001.json"));
        Assert.Contains(reader.Journal(), line => line.Message.Contains("analyse individuelle indisponible"));
    }

    // ------------------------------------------------------------------
    // INTEGRATION_CONTRACT.md §10.1 (isolation) : une analyse expérimentale qui échoue
    // ne fait ni échouer ni rouvrir la campagne — l'absence est consignée, le paquet se scelle.
    // ------------------------------------------------------------------
    [Fact]
    public async Task Rapport_indisponible_ne_empeche_pas_le_scellement()
    {
        var packages = new TestPackageService(_directory);
        var executor = new ScriptedRunExecutor(Success);
        var analysis = new RecordingAnalysisService(
            () => throw new InvalidOperationException("analyste fautif"),
            null);
        var runner = new CampaignRunner(packages, executor, analysis, new SystemClock(), _journal);
        var definition = Definition(1);

        var packagePath = runner.CreateCampaign(definition);
        var sealedPath = await runner.ExecuteAsync(packagePath, definition, CancellationToken.None);

        using var reader = new LivexPackageReader(sealedPath);
        Assert.Equal(PackageStates.Sealed, reader.Manifest.State);
        Assert.Equal(1, reader.Manifest.Counts.RunsDone);
        Assert.Null(reader.ReadEmergenceReport());
        Assert.Contains(_journal.Entries, entry => entry.Level == "Warn"
            && entry.Message.Contains("rapport d'émergence indisponible"));
    }

    // ------------------------------------------------------------------
    // EXPERIMENTS.md §6 : un run Échoué conserve l'ensemble de ses journaux.
    // ------------------------------------------------------------------
    [Fact]
    public async Task Run_echoue_conserve_ses_journaux()
    {
        var packages = new TestPackageService(_directory);
        var executor = new ScriptedRunExecutor(spec => throw new InvalidOperationException("moteur en panne"));
        var runner = new CampaignRunner(packages, executor, null, new SystemClock(), _journal);
        var definition = Definition(1, FailurePolicy.Continue);

        var packagePath = runner.CreateCampaign(definition);
        var sealedPath = await runner.ExecuteAsync(packagePath, definition, CancellationToken.None);

        using var reader = new LivexPackageReader(sealedPath);
        var failureNote = reader.ReadEntry("runs/RUN-0001/logs/failure.txt");
        Assert.NotNull(failureNote);
        Assert.Contains("moteur en panne", System.Text.Encoding.UTF8.GetString(failureNote));
        Assert.Single(reader.RunIndex.Runs, r => r.Status == RunStatuses.Echoue);
    }

    // ------------------------------------------------------------------
    // Estimation : moyenne des runs terminés, jamais une extrapolation scientifique.
    // ------------------------------------------------------------------
    [Fact]
    public void Estimation_moyenne_des_runs_termines()
    {
        var packages = new TestPackageService(_directory);
        var runner = new CampaignRunner(packages, new ScriptedRunExecutor(Success), null, new SystemClock(), _journal);
        var estimate = runner.EstimateRemaining([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)], 3);
        Assert.Equal(TimeSpan.FromSeconds(45), estimate);
        Assert.Null(runner.EstimateRemaining([], 3));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}

/// <summary>Analyste enregistré : compte les demandes et produit un rapport fixe (ADR-003).</summary>
public sealed class RecordingAnalysisService : IAnalysisService
{
    private readonly Func<int> _onAnalyzeExperiment;
    private readonly string? _report;
    private readonly Func<int>? _onAnalyzeRun;
    private readonly IReadOnlyDictionary<string, byte[]>? _runFiles;

    /// <summary>Initialise l'analyste de test.</summary>
    public RecordingAnalysisService(Func<int> onAnalyzeExperiment, string? report, Func<int>? onAnalyzeRun = null, IReadOnlyDictionary<string, byte[]>? runFiles = null)
    {
        _onAnalyzeExperiment = onAnalyzeExperiment;
        _report = report;
        _onAnalyzeRun = onAnalyzeRun;
        _runFiles = runFiles;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, byte[]>> AnalyzeRunAsync(RunResult run, string experimentId, CancellationToken cancellationToken)
    {
        _onAnalyzeRun?.Invoke();
        return Task.FromResult(_runFiles ?? (IReadOnlyDictionary<string, byte[]>)new Dictionary<string, byte[]>());
    }

    /// <inheritdoc />
    public Task<(IReadOnlyDictionary<string, byte[]> AggregateFiles, string? EmergenceReport)> AnalyzeExperimentAsync(string experimentId, CancellationToken cancellationToken)
    {
        _onAnalyzeExperiment();
        return Task.FromResult<(IReadOnlyDictionary<string, byte[]>, string?)>(
            (new Dictionary<string, byte[]> { ["aggregate.json"] = System.Text.Encoding.UTF8.GetBytes("{}") }, _report));
    }
}
