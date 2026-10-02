using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Launcher.Domain;
using Launcher.Protocol;

namespace Launcher.Infrastructure;

/// <summary>
/// Gestionnaire de processus (INTEGRATION_CONTRACT.md §5) :
/// - démarrage explicite, arguments validés, jeton par variable d'environnement seulement ;
/// - confinement anti-orphelin (Job Object Windows, groupe de processus Linux) ;
/// - arrêt propre : POST /control/shutdown avec jeton, délai de grâce, arrêt forcé de l'arbre.
/// </summary>
public sealed class ProcessManager : IProcessManager
{
    private readonly IClock _clock;
    private readonly ISessionJournal _journal;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, Process> _processes = new(StringComparer.Ordinal);

    /// <summary>Initialise le gestionnaire avec l'horloge et le journal du domaine.</summary>
    public ProcessManager(IClock clock, ISessionJournal journal)
    {
        _clock = clock;
        _journal = journal;
        _httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    /// <inheritdoc />
    public event EventHandler<ProcessExitedEventArgs>? Exited;

    /// <inheritdoc />
    public async Task<int> StartAsync(ProcessLaunchSpec spec, CancellationToken cancellationToken)
    {
        if (spec.Arguments.Any(a => a.Contains(SessionTokenEnvVarName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("le jeton de session ne doit jamais figurer dans les arguments de ligne de commande");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = spec.ExecutablePath,
            WorkingDirectory = spec.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in spec.Environment)
        {
            startInfo.Environment[key] = value;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(spec.StdOutLogPath)!);
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var instanceId = spec.InstanceId;

        if (!process.Start())
        {
            throw new InvalidOperationException($"démarrage impossible : {spec.ExecutablePath}");
        }

        // Confinement Unix : le fils reçoit son propre groupe de processus, afin que l'arrêt
        // forcé vise ce groupe sans jamais toucher le groupe du Launcher (INTEGRATION_CONTRACT.md §5.2).
        ProcessTreeKiller.MakeOwnProcessGroup(process.Id);

        _ = PumpAsync(process.StandardOutput, spec.StdOutLogPath, instanceId, "stdout", cancellationToken);
        _ = PumpAsync(process.StandardError, spec.StdErrLogPath, instanceId, "stderr", cancellationToken);

        lock (_processes)
        {
            _processes[instanceId] = process;
        }

        // Surveillance de sortie sans course : WaitForExitAsync détecte aussi une fin
        // survenue avant l'abonnement d'un observateur, ce que l'événement Exited seul ne garantit pas.
        _ = Task.Run(async () =>
        {
            try
            {
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }

            lock (_processes)
            {
                _processes.Remove(instanceId);
            }

            int exitCode;
            try
            {
                exitCode = process.ExitCode;
            }
            catch (InvalidOperationException)
            {
                exitCode = -1;
            }

            _journal.Info("ProcessExit", $"{instanceId} terminé (code {exitCode})", null, instanceId);
            Exited?.Invoke(this, new ProcessExitedEventArgs { InstanceId = instanceId, ExitCode = exitCode, Outcome = MapOutcome(exitCode) });
        });

        _journal.Info("ProcessStart", $"{instanceId} démarré (PID {process.Id})", null, instanceId);
        return process.Id;
    }

    private static async Task PumpAsync(StreamReader reader, string logPath, string instanceId, string stream, CancellationToken cancellationToken)
    {
        try
        {
            await using var writer = new StreamWriter(logPath, append: false, Encoding.UTF8);
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            // Une perte de flux ne doit jamais arrêter la supervision.
            Console.Error.WriteLine($"[{instanceId}] {stream} : {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ProcessExit> StopAsync(string instanceId, int processId, Uri controlEndpoint, string sessionToken, TimeSpan graceful, CancellationToken cancellationToken)
    {
        if (!IsAlive(processId))
        {
            return new ProcessExit(0, ExitOutcomeNames.Completed, true);
        }

        // 1. Demande d'arrêt : POST /control/shutdown avec jeton (INTEGRATION_CONTRACT.md §5.1).
        var stopped = await TryRequestShutdownAsync(controlEndpoint, sessionToken, cancellationToken).ConfigureAwait(false);
        var deadline = _clock.UtcNow + graceful;
        while (_clock.UtcNow < deadline && IsAlive(processId))
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        if (!IsAlive(processId))
        {
            return new ProcessExit(0, ExitOutcomeNames.Completed, true);
        }

        // 4. Au-delà du délai de grâce : arrêt forcé de tout l'arbre.
        _journal.Warn("ProcessStop", $"{instanceId} : délai de grâce dépassé, arrêt forcé de l'arbre", null, instanceId);
        await KillAsync(instanceId, processId, cancellationToken).ConfigureAwait(false);
        return new ProcessExit(ExitCodes.UnspecifiedError, stopped ? ExitOutcomeNames.Cancelled : ExitOutcomeNames.ProcessError, false);
    }

    private async Task<bool> TryRequestShutdownAsync(Uri controlEndpoint, string sessionToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(controlEndpoint, "/control/shutdown"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task KillAsync(string instanceId, int processId, CancellationToken cancellationToken)
    {
        try
        {
            lock (_processes)
            {
                if (_processes.TryGetValue(instanceId, out var process))
                {
                    ProcessTreeKiller.KillTree(process);
                    _processes.Remove(instanceId);
                    return Task.CompletedTask;
                }
            }

            ProcessTreeKiller.KillByPid(processId);
        }
        catch (Exception exception)
        {
            _journal.Fail("ProcessKill", $"{instanceId} : arrêt forcé en échec — {exception.Message}", null, instanceId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool IsAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Mappe un code de sortie vers le résultat normalisé du contrat (INTEGRATION_CONTRACT.md §4).</summary>
    public static string MapOutcome(int exitCode) => exitCode switch
    {
        ExitCodes.Normal => ExitOutcomeNames.Completed,
        ExitCodes.UnspecifiedError => ExitOutcomeNames.UnknownError,
        ExitCodes.ConfigurationError or ExitCodes.PortUnavailable => ExitOutcomeNames.ConfigurationError,
        ExitCodes.MissingResource => ExitOutcomeNames.InstallationError,
        ExitCodes.VersionMismatch => ExitOutcomeNames.CommunicationError,
        ExitCodes.SimulationError => ExitOutcomeNames.SimulationError,
        ExitCodes.FileSystemError => ExitOutcomeNames.FileSystemError,
        ExitCodes.InterruptedBySignal or ExitCodes.TerminatedBySignal => ExitOutcomeNames.Cancelled,
        _ => ExitOutcomeNames.ProcessError,
    };

    private const string SessionTokenEnvVarName = "LIVEX_SESSION_TOKEN";
}
