using System.Net.Sockets;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Infrastructure;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Tests.Integration.Infrastructure;

/// <summary>
/// Banc d'intégration (TESTING.md §4) : le moteur simulé est le vrai binaire Stub.Syne,
/// démarré comme un processus externe. Vérifie démarrage, codes de sortie, arrêt forcé,
/// sonde HTTP et confinement — sans aucun composant réel SYNE/ECHOS.
/// </summary>
public sealed class ProcessManagerTests : IAsyncLifetime
{
    private readonly string _stubPath;
    private readonly string _workRoot;
    private readonly InMemoryJournalAdapter _journal = new();
    private readonly ProcessManager _manager;

    public ProcessManagerTests()
    {
        _workRoot = Path.Combine(Path.GetTempPath(), $"livexp-int-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workRoot);
        _stubPath = StubLocator.StubSynePath();
        _manager = new ProcessManager(new SystemClock(), _journal);
    }

    /// <summary>Le processus Stub.Syne atteint son horizon de ticks et sort avec le code 0 (mode batch).</summary>
    [Fact]
    public async Task Stub_Syne_termine_seul_avec_code_zero()
    {
        var workDirectory = Path.Combine(_workRoot, "run-normal");
        var completion = await StartAndWaitAsync(workDirectory, spec =>
        {
            spec.Arguments.AddRange(["--ticks", "3"]);
        });

        Assert.Equal(0, completion.ExitCode);
        Assert.True(File.Exists(Path.Combine(workDirectory, "data", "result.json")));
    }

    /// <summary>Un crash injecté produit le résultat ProcessError du contrat (§4).</summary>
    [Fact]
    public async Task Crash_injecte_produit_ProcessError()
    {
        var completion = await StartAndWaitAsync(Path.Combine(_workRoot, "run-crash"), spec =>
        {
            spec.Arguments.AddRange(["--ticks", "10", "--crash-at-tick", "2"]);
        });

        Assert.Equal(1, completion.ExitCode);
        Assert.Equal(ExitOutcomeNames.UnknownError, completion.Outcome);
    }

    /// <summary>Un exit-code arbitraire est transporté tel quel (pannes injectables §13).</summary>
    [Fact]
    public async Task Code_de_sortie_arbitraire_est_mappe()
    {
        var completion = await StartAndWaitAsync(Path.Combine(_workRoot, "run-code7"), spec =>
        {
            spec.Arguments.AddRange(["--ticks", "2", "--exit-code", "7"]);
        });

        Assert.Equal(7, completion.ExitCode);
        Assert.Equal(ExitOutcomeNames.FileSystemError, ProcessManager.MapOutcome(7));
    }

    /// <summary>La sonde /health/ready répond sur le port de contrôle alloué.</summary>
    [Fact]
    public async Task Sonde_health_ready_repond()
    {
        var workDirectory = Path.Combine(_workRoot, "run-probe");
        Directory.CreateDirectory(workDirectory);
        var prober = new HealthProber(TimeSpan.FromSeconds(2));
        var port = FreePort();

        var processId = await _manager.StartAsync(BuildSpec("probe-0001", workDirectory, port, extra => { }), CancellationToken.None);
        try
        {
            var control = new Uri($"http://127.0.0.1:{port}/");
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            (bool Ready, string? Cause) probe = default;
            while (DateTime.UtcNow < deadline)
            {
                probe = await prober.ProbeReadyAsync(control, CancellationToken.None);
                if (probe.Ready)
                {
                    break;
                }

                await Task.Delay(100);
            }

            Assert.True(probe.Ready, $"sonde jamais au vert : {probe.Cause}");
            var info = await prober.FetchInfoAsync(control, CancellationToken.None);
            Assert.NotNull(info);
            Assert.Equal("syne", info!.ComponentId);
        }
        finally
        {
            await _manager.KillAsync("probe-0001", processId, CancellationToken.None);
        }
    }

    /// <summary>Le stub écrit exclusivement dans le dossier fourni (exigence commune §8).</summary>
    [Fact]
    public async Task Stub_ecrit_dans_son_dossier_seulement()
    {
        var workDirectory = Path.Combine(_workRoot, "run-scope");
        var otherDirectory = Path.Combine(_workRoot, "hors-perimetre");
        Directory.CreateDirectory(otherDirectory);
        var markerBefore = Directory.GetFileSystemEntries(otherDirectory).Length;

        await StartAndWaitAsync(workDirectory, spec =>
        {
            spec.Arguments.AddRange(["--ticks", "2"]);
        });

        Assert.True(Directory.GetFiles(Path.Combine(workDirectory, "data")).Length > 0);
        Assert.Equal(markerBefore, Directory.GetFileSystemEntries(otherDirectory).Length);
    }

    private async Task<ProcessExitedEventArgs> StartAndWaitAsync(string workDirectory, Action<ProcessLaunchSpec> configure)
    {
        Directory.CreateDirectory(workDirectory);
        var completion = new TaskCompletionSource<ProcessExitedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnExited(object? sender, ProcessExitedEventArgs args) => completion.TrySetResult(args);
        _manager.Exited += OnExited;
        try
        {
            var spec = BuildSpec($"test-{Guid.NewGuid():N}".Replace("-", string.Empty)[..12], workDirectory, FreePort(), configure);
            var processId = await _manager.StartAsync(spec, CancellationToken.None);
            var exited = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
            _ = processId;
            return exited;
        }
        finally
        {
            _manager.Exited -= OnExited;
        }
    }

    private static ProcessLaunchSpec BuildSpec(string instanceId, string workDirectory, int port, Action<ProcessLaunchSpec> configure)
    {
        var spec = new ProcessLaunchSpec
        {
            InstanceId = instanceId,
            ExecutablePath = StubLocator.StubSynePath(),
            WorkingDirectory = workDirectory,
            Arguments =
            [
                "--instance-id", instanceId,
                "--control-port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--work-dir", workDirectory,
                "--seed", "42",
            ],
            Environment = new Dictionary<string, string>
            {
                ["LIVEX_SESSION_TOKEN"] = "test-token",
            },
            StdOutLogPath = Path.Combine(workDirectory, "stdout.log"),
            StdErrLogPath = Path.Combine(workDirectory, "stderr.log"),
        };
        configure(spec);
        return spec;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        try
        {
            Directory.Delete(_workRoot, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }

        return Task.CompletedTask;
    }
}

/// <summary>Adaptateur de journal pour les bancs d'intégration.</summary>
public sealed class InMemoryJournalAdapter : ISessionJournal
{
    /// <summary>Événements enregistrés.</summary>
    public List<SessionEvent> Entries { get; } = new();

    /// <inheritdoc />
    public void Log(SessionEvent entry) => Entries.Add(entry);

    /// <inheritdoc />
    public void Info(string operation, string message, string? correlationId = null, string? instanceId = null) =>
        Entries.Add(new SessionEvent { Operation = operation, Message = message });

    /// <inheritdoc />
    public void Warn(string operation, string message, string? correlationId = null, string? instanceId = null) =>
        Entries.Add(new SessionEvent { Operation = operation, Message = message, Level = "Warn" });

    /// <inheritdoc />
    public void Fail(string operation, string message, string? correlationId = null, string? instanceId = null) =>
        Entries.Add(new SessionEvent { Operation = operation, Message = message, Level = "Error" });
}

/// <summary>Localise les binaires de stubs compilés par la solution.</summary>
public static class StubLocator
{
    /// <summary>Chemin du binaire Stub.Syne pour la configuration courante.</summary>
    public static string StubSynePath() => StubPath("Stub.Syne");

    /// <summary>Chemin du binaire Stub.Echos pour la configuration courante.</summary>
    public static string StubEchosPath() => StubPath("Stub.Echos");

    /// <summary>Chemin du binaire Stub.Prism pour la configuration courante.</summary>
    public static string StubPrismPath() => StubPath("Stub.Prism");

    /// <summary>Chemin du binaire d'un stub donné, avec extension sur Windows (G6 : deux plateformes).</summary>
    public static string StubPath(string stubName)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var configuration = new DirectoryInfo(baseDirectory).Parent!.Name;
        var fileName = OperatingSystem.IsWindows() ? $"{stubName}.exe" : stubName;
        var candidate = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", stubName, "bin", configuration, "net10.0", fileName));
        Assert.True(File.Exists(candidate), $"binaire stub introuvable : {candidate}");
        return candidate;
    }
}
