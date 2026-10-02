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
    private readonly IProcessManager _processes;
    private readonly OrchestrationService _orchestration;
    private readonly PortAllocator _ports;
    private readonly ISessionJournal _journal;

    /// <summary>Initialise l'exécuteur.</summary>
    public ProcessRunExecutor(IProcessManager processes, OrchestrationService orchestration, PortAllocator ports, ISessionJournal journal)
    {
        _processes = processes;
        _orchestration = orchestration;
        _ports = ports;
        _journal = journal;
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
        await File.WriteAllTextAsync(configurationPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            agents = new { initialCount = spec.AgentCount },
        }), cancellationToken).ConfigureAwait(false);

        var declaredControlPort = manifest.Endpoints is { } endpoints && endpoints.TryGetValue("control", out var controlEndpoint)
            ? controlEndpoint.Port
            : null;
        var control = _ports.Resolve("syne", $"run-{spec.RunId}", "control", declaredControlPort);
        var correlationId = OrchestrationService.NewCorrelationId();

        var arguments = new List<string>
        {
            "--headless",
            "--instance-id", $"syne-{spec.RunId}",
            "--control-port", control.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--work-dir", runDirectory,
            "--log-dir", logsDirectory,
            "--simulation", spec.Simulation,
            "--seed", spec.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--max-ticks", spec.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--config", configurationPath,
            "--autostart",
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
        try
        {
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
                await _processes.StopAsync(instanceId, pid, new Uri(control.Url), spec.SessionToken,
                    TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            // Libération toujours : un run échoué ne doit jamais fuiter ni son port, ni son entrée de registre.
            _processes.Exited -= OnExited;
            _orchestration.Registry.Remove(instanceId);
            _ports.Release($"run-{spec.RunId}");
        }
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
