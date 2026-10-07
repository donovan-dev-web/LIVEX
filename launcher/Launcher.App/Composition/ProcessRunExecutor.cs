using System.Diagnostics;
using Launcher.Application;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Protocol;
using Launcher.Protocol.Model;

namespace Launcher.App.Composition;

/// <summary>
/// Exécuteur de runs (EXPERIMENTS.md §6.4) : alloue les ports, prépare le dossier du run,
/// lance le moteur avec la graine et l'horizon, attend la fin, collecte les sorties.
/// Un run n'écrit que dans son dossier (DATA_FLOW.md §3).
/// </summary>
public sealed class ProcessRunExecutor : IRunExecutor
{
    /// <summary>
    /// Cadence d'interrogation de l'avancement. Assez serrée pour que la barre avance
    /// à l'œil (5 échantillons/s), assez lâche pour ne pas solliciter inutilement le
    /// moteur pendant un run. Un moteur qui ne publie aucun état reste sans avancement :
    /// la sonde est best-effort et ne fait jamais échouer l'exécution.
    /// </summary>
    private static readonly TimeSpan TickProgressInterval = TimeSpan.FromMilliseconds(200);

    private readonly IProcessManager _processes;
    private readonly OrchestrationService _orchestration;
    private readonly PortAllocator _ports;
    private readonly ISessionJournal _journal;
    private readonly IHealthProbe _probe;
    private readonly EchosAnalysisService? _echos;

    /// <inheritdoc />
    public event EventHandler<RunTickProgress>? TickProgress;

    /// <summary>Initialise l'exécuteur. <paramref name="echos"/> est facultatif :
    /// sans lui, aucun flux live n'est proposé (les campagnes sans API ECHOS).</summary>
    public ProcessRunExecutor(
        IProcessManager processes,
        OrchestrationService orchestration,
        PortAllocator ports,
        ISessionJournal journal,
        IHealthProbe probe,
        EchosAnalysisService? echos = null)
    {
        _processes = processes;
        _orchestration = orchestration;
        _ports = ports;
        _journal = journal;
        _probe = probe;
        _echos = echos;
    }

    /// <inheritdoc />
    public async Task<RunResult> ExecuteAsync(RunSpec spec, CancellationToken cancellationToken)
    {
        var installation = _orchestration.Registry.GetActiveInstallation("syne")
            ?? throw new InvalidOperationException("moteur « syne » non détecté : campagne non exécutable (porte P2)");
        var manifest = installation.Manifest!;
        var executable = OperatingSystem.IsWindows()
            ? manifest.executable.Windows ?? manifest.executable.Path
            : manifest.executable.Linux ?? manifest.executable.Path;
        var executablePath = Path.GetFullPath(Path.Combine(installation.Location, executable!));
        if (!File.Exists(executablePath))
        {
            throw new InvalidOperationException($"binaire du moteur absent : {executablePath}");
        }

        // Un run n'écrit que dans son dossier : data/, logs/.
        var runDirectory = spec.WorkDirectory;
        var dataDirectory = Path.Combine(runDirectory, "data");
        var logsDirectory = Path.Combine(runDirectory, "logs");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(logsDirectory);
        var configurationPath = Path.Combine(runDirectory, "launcher-config.json");
        await File.WriteAllTextAsync(configurationPath, System.Text.Json.JsonSerializer.Serialize(
            // Surcouche partielle : le moteur la fusionne sur ses défauts intégrés,
            // seul ticksPerSecond est remplacé. La même valeur est archivée dans
            // config.resolved.json — les deux viennent de RunEngineProfile, pour que
            // le paquet ne puisse pas attester une configuration différente de celle
            // qui a été appliquée (EXPERIMENTS.md §12).
            RunEngineProfile.ConfigOverlay(spec)), cancellationToken).ConfigureAwait(false);

        var declaredControlPort = manifest.Endpoints is { } endpoints && endpoints.TryGetValue("control", out var controlEndpoint)
            ? controlEndpoint.Port
            : null;
        var control = _ports.Resolve("syne", $"run-{spec.RunId}", "control", declaredControlPort);
        // Port d'observation (SYNE diffuse les snapshots en direct quand il est
        // fourni) : c'est la voie de l'analyse temps réel — sans lui, ECHOS ne
        // voit le run qu'à la fin de la campagne, via l'ingestion d'archive.
        var observe = _ports.Resolve("syne", $"run-{spec.RunId}", "observe", null);
        var correlationId = OrchestrationService.NewCorrelationId();

        var arguments = new List<string>
        {
            "--headless",
            "--instance-id", $"syne-{spec.RunId}",
            "--control-port", control.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--observe-port", observe.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--work-dir", runDirectory,
            "--log-dir", logsDirectory,
            "--simulation", spec.Simulation,
            "--seed", spec.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--max-ticks", spec.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--config", configurationPath,
            "--autostart",
            // Identité analytique du run : SYNE l'écrit dans le flux exporté,
            // ECHOS l'enregistre sous cette clé. Sans elle, deux campagnes
            // porteraient chacune un « RUN-0001 » dans la même base.
            "--run-id", RunIdentity.For(spec.ExperimentId, spec.RunId),
            // Artefact de rejouabilité : le flux archivé dans le paquet permet de
            // réanalyser le run après coup, sans SYNE ni capture temps réel.
            "--export-stream",
            // Corrélation propagée par les deux voies prévues (INTEGRATION_CONTRACT.md §3.1, §7.2).
            CorrelationHeaders.CorrelationIdArgument, correlationId,
        };

        var instanceId = $"syne-{spec.RunId}";
        var completion = new TaskCompletionSource<ProcessExitedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnExited(object? sender, ProcessExitedEventArgs args)
        {
            if (args.InstanceId == instanceId)
            {
                completion.TrySetResult(args);
            }
        }

        // Abonnement avant StartAsync : un run court peut se terminer avant le retour
        // de StartAsync, auquel cas un abonnement tardif perdrait l'unique notification.
        _processes.Exited += OnExited;
        var startedAt = DateTimeOffset.UtcNow;
        int? processId = null;
        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? progressTask = null;
        try
        {
            // Analyse temps réel : ECHOS est prévenu AVANT le lancement — son
            // consommateur se reconnecte en boucle et capte la description du
            // monde dès l'ouverture du port d'observation. Best effort : ni bloquant
            // ni fatal (le repli reste l'ingestion d'archive en fin de campagne).
            await RequestLiveIngestAsync(spec, observe.Port, cancellationToken).ConfigureAwait(false);

            processId = await _processes.StartAsync(new ProcessLaunchSpec
            {
                InstanceId = instanceId,
                ExecutablePath = executablePath,
                WorkingDirectory = runDirectory,
                Arguments = arguments,
                Environment = new Dictionary<string, string>
                {
                    [CorrelationHeaders.SessionTokenEnvVar] = spec.SessionToken,
                    [CorrelationHeaders.CorrelationIdEnvVar] = correlationId,
                    [CorrelationHeaders.InstallRootEnvVar] = installation.Location,
                },
                StdOutLogPath = Path.Combine(logsDirectory, "stdout.log"),
                StdErrLogPath = Path.Combine(logsDirectory, "stderr.log"),
            }, cancellationToken).ConfigureAwait(false);

            _orchestration.Registry.Add(new ComponentInstance
            {
                InstanceId = instanceId,
                ComponentId = "syne",
                Installation = installation,
                ProcessId = processId,
                StartedAt = startedAt,
            });

            // Sonde d'avancement : elle court en parallèle de l'attente de sortie, sans
            // jamais la retarder ni faire échouer le run.
            progressTask = PollTickProgressAsync(new Uri(control.Url), progressCts.Token);

            var exited = await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            var endedAt = DateTimeOffset.UtcNow;
            if (exited.ExitCode != ExitCodes.Normal)
            {
                throw new InvalidOperationException($"le moteur s'est terminé avec le code {exited.ExitCode} ({exited.Outcome})");
            }

            // Collecte : le Launcher reçoit, vérifie la présence, ne lit pas le contenu scientifique.
            var dataFiles = CollectFiles(dataDirectory, runDirectory);
            var logFiles = CollectLogFiles(logsDirectory);

            return new RunResult(
                spec.RunId,
                spec.Seed,
                RunStatuses.Termine,
                spec.Attempt,
                null,
                dataFiles,
                logFiles,
                spec.Ticks,
                endedAt - startedAt,
                exited.ExitCode,
                startedAt,
                endedAt,
                [new JsonComponentRef { Id = manifest.Id, Version = manifest.Version }]);
        }
        catch (OperationCanceledException)
        {
            if (processId is { } pid)
            {
                // Le délai déclaré au manifeste vaut aussi pour l'annulation : trop court, il
                // interrompt le moteur au milieu de l'écriture de ses exports.
                await _processes.StopAsync(instanceId, pid, new Uri(control.Url), spec.SessionToken,
                    installation.ShutdownGrace, CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            // Libération toujours : un run échoué ne doit jamais fuiter ni son port, ni son entrée de registre.
            progressCts.Cancel();
            if (progressTask is not null)
            {
                await progressTask.ConfigureAwait(false);
            }

            _processes.Exited -= OnExited;
            _orchestration.Registry.Remove(instanceId);
            _ports.Release($"run-{spec.RunId}");
        }
    }

    /// <summary>
    /// Cadence au-delà de laquelle aucun flux live n'est proposé : au rythme batch
    /// (1000 ticks/s) le consommateur livrerait un run partiel, là où l'ingestion
    /// d'archive de fin de campagne enregistre le flux complet. Sous cette borne,
    /// l'analyse temps réel prime.
    /// </summary>
    private const int LiveIngestMaxTicksPerSecond = 100;

    /// <summary>
    /// Publie l'adresse du flux live à ECHOS. Best effort : une API absente ne fait
    /// ni échouer ni retarder le run — l'ingestion d'archive de fin de campagne
    /// reste la voie de repli.
    /// </summary>
    private async Task RequestLiveIngestAsync(RunSpec spec, int observePort, CancellationToken cancellationToken)
    {
        if (_echos is null)
        {
            return;
        }

        var cadence = RunEngineProfile.TicksPerSecondFor(spec);
        if (cadence > LiveIngestMaxTicksPerSecond)
        {
            _journal.Info("LiveIngest",
                $"run à {cadence} ticks/s : flux live non demandé, ingestion d'archive en fin de campagne");
            return;
        }

        var started = await _echos.TryStartLiveIngestAsync(observePort, cancellationToken).ConfigureAwait(false);
        if (started)
        {
            _journal.Info("LiveIngest",
                $"flux live du run proposé à ECHOS (ws://127.0.0.1:{observePort}/) — analyse temps réel");
        }
        else
        {
            _journal.Warn("LiveIngest",
                "ECHOS n'a pas pris le flux live : analyse temps réel indisponible, ingestion en fin de campagne conservée");
        }
    }

    /// <summary>
    /// Interroge l'avancement du run tant qu'il tourne et le publie. Ne lève jamais :
    /// la sonde est un confort d'affichage, la sortie du processus reste l'unique fait
    /// qui décide du résultat (EXPERIMENTS.md §8).
    /// </summary>
    private async Task PollTickProgressAsync(Uri controlEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (await _probe.FetchRunProgressAsync(controlEndpoint, cancellationToken).ConfigureAwait(false)
                    is { } progress)
                {
                    TickProgress?.Invoke(this, progress);
                }

                await Task.Delay(TickProgressInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Arrêt attendu : fin du run, ou annulation.
        }
#pragma warning disable CA1031 // La sonde ne doit jamais faire échouer un run.
        catch (Exception)
        {
            // Un moteur qui ne publie aucun état laisse simplement la barre vide.
        }
#pragma warning restore CA1031
    }

    private static IReadOnlyDictionary<string, byte[]> CollectFiles(string dataDirectory, string runDirectory)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (!Directory.Exists(dataDirectory))
        {
            return files;
        }

        foreach (var file in Directory.EnumerateFiles(dataDirectory, "*", SearchOption.AllDirectories))
        {
            var entryName = Path.GetRelativePath(runDirectory, file).Replace('\\', '/');
            files[entryName] = File.ReadAllBytes(file);
        }

        return files;
    }

    private static IReadOnlyList<(string Name, byte[] Content)> CollectLogFiles(string logsDirectory)
    {
        var logs = new List<(string, byte[])>();
        if (!Directory.Exists(logsDirectory))
        {
            return logs;
        }

        foreach (var file in Directory.EnumerateFiles(logsDirectory))
        {
            logs.Add((Path.GetFileName(file), File.ReadAllBytes(file)));
        }

        return logs;
    }
}
