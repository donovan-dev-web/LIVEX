// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.CommandLine;
using Setup.Core.Distribution;
using Setup.Core.Execution;
using Setup.Core.Lifecycle;
using Setup.Core.Planning;
using Setup.Core.Preflight;
using Setup.Core.Simulation;
using Setup.Tests.Support;
using Xunit;

namespace Setup.Tests.Unit;

/// <summary>
/// Vérifie la table de codes de sortie des modes non interactifs (INS-F-95) et
/// l'absence de toute attente de saisie (INS-F-96).
/// </summary>
public class HeadlessRunnerTests
{
    [Fact]
    public async Task Installation_Reussie_Retourne_0()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, delayMs: 0);
        var output = new StringWriter();

        var outcome = await HeadlessRunner.InstallAsync(engine, request, output);

        Assert.Equal(ExitCodes.Success, outcome.ExitCode);
        Assert.True(outcome.Result?.Simulated);
        Assert.Contains("Installation terminée.", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("[Pass] license", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preflight_En_Echec_Retourne_2_Sans_Executer()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, acceptLicense: false);
        var output = new StringWriter();

        var outcome = await HeadlessRunner.InstallAsync(engine, request, output);

        Assert.Equal(ExitCodes.PreflightFailed, outcome.ExitCode);
        Assert.Null(outcome.Result);
        Assert.Contains("[Fail] license", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Installation_Deja_Presente_Retourne_5()
    {
        var (_, policy) = TestEngine.Create();
        var manifest = DemoWorld.CreateManifest(DemoScenario.ExistingInstall);
        var record = DemoWorld.CreateExistingInstallation(
            policy.InstallDir, policy.DataDir, Path.Combine(policy.DataDir, "workspace")) with
        {
            Version = manifest.Version,
        };
        var engine = new StubEngine { Detection = new DetectionResult(true, record.InstallDir, record, true, Array.Empty<string>()) };
        var request = TestEngine.DemoRequest(policy, manifest: manifest, dryRun: false);

        var outcome = await HeadlessRunner.InstallAsync(engine, request, new StringWriter());

        Assert.Equal(ExitCodes.AlreadyInstalled, outcome.ExitCode);
        Assert.Equal(0, engine.Executions);
    }

    [Fact]
    public async Task Actif_Facultatif_Indisponible_Retourne_6()
    {
        var (_, policy) = TestEngine.Create();
        var engine = new StubEngine
        {
            Execution = new ExecutionResult(false, Array.Empty<string>(),
                new[] { "Actif facultatif indisponible : 404" }, "(simulation)", ExitCodeHint: ExitCodes.OptionalAssetUnavailable),
        };

        var outcome = await HeadlessRunner.InstallAsync(
            engine, TestEngine.DemoRequest(policy, dryRun: false), new StringWriter());

        Assert.Equal(ExitCodes.OptionalAssetUnavailable, outcome.ExitCode);
    }

    [Fact]
    public async Task Echec_D_Installation_Retourne_3()
    {
        var (_, policy) = TestEngine.Create();
        var engine = new StubEngine
        {
            Execution = new ExecutionResult(false, Array.Empty<string>(), new[] { "accès refusé" }, "journal.log"),
        };

        var outcome = await HeadlessRunner.InstallAsync(
            engine, TestEngine.DemoRequest(policy, dryRun: false), new StringWriter());

        Assert.Equal(ExitCodes.InstallFailed, outcome.ExitCode);
    }

    [Fact]
    public async Task Operation_Annulee_Retourne_2()
    {
        var (_, policy) = TestEngine.Create();
        var engine = new StubEngine
        {
            Execution = new ExecutionResult(false,
                new[] { "Opération annulée." }, Array.Empty<string>(), "journal.log",
                ExitCodeHint: ExitCodes.PreflightFailed),
        };

        var outcome = await HeadlessRunner.InstallAsync(
            engine, TestEngine.DemoRequest(policy, dryRun: false), new StringWriter());

        Assert.Equal(ExitCodes.PreflightFailed, outcome.ExitCode);
    }

    [Fact]
    public async Task Desinstallation_Avec_Residus_Retourne_4()
    {
        var engine = new StubEngine
        {
            Execution = new ExecutionResult(true, Array.Empty<string>(), new[] { "livex.lnk verrouillé" }, "journal.log"),
        };

        var outcome = await HeadlessRunner.UninstallAsync(engine, UninstallOf(UninstallScope.Software), new StringWriter());

        Assert.Equal(ExitCodes.UninstallPartial, outcome.ExitCode);
        Assert.Equal(1, engine.Executions);
    }

    [Fact]
    public async Task Desinstallation_Sans_Residu_Retourne_0()
    {
        var engine = new StubEngine();
        var outcome = await HeadlessRunner.UninstallAsync(engine, UninstallOf(UninstallScope.Software), new StringWriter());

        Assert.Equal(ExitCodes.Success, outcome.ExitCode);
    }

    [Fact]
    public async Task Desinstallation_Portee_C_Sans_Confirmation_Est_Annulee_Sans_Rien_Supprimer()
    {
        var engine = new StubEngine();
        var request = UninstallOf(UninstallScope.SoftwareConfigAndWorkspace);

        var outcome = await HeadlessRunner.UninstallAsync(engine, request, new StringWriter());

        Assert.Equal(ExitCodes.PreflightFailed, outcome.ExitCode);
        Assert.Equal(0, engine.Executions);
        Assert.Contains("confirmation", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Desinstallation_Portee_C_Confirmee_S_Execute()
    {
        var engine = new StubEngine();
        var request = UninstallOf(UninstallScope.SoftwareConfigAndWorkspace) with
        {
            WorkspaceDeletionConfirmed = true,
            PurgeResearch = true,
        };

        var outcome = await HeadlessRunner.UninstallAsync(engine, request, new StringWriter());

        Assert.Equal(ExitCodes.Success, outcome.ExitCode);
        Assert.Equal(1, engine.Executions);
    }

    [Fact]
    public async Task Reparation_Reussie_Retourne_0()
    {
        var (_, policy) = TestEngine.Create();
        var record = DemoWorld.CreateExistingInstallation(
            policy.InstallDir, policy.DataDir, Path.Combine(policy.DataDir, "workspace"));
        var engine = new StubEngine();
        var request = new MaintenanceRequest(
            record, MaintenanceOperation.Repair, DemoWorld.CreateManifest(DemoScenario.ExistingInstall));

        var outcome = await HeadlessRunner.RepairAsync(engine, request, new StringWriter());

        Assert.Equal(ExitCodes.Success, outcome.ExitCode);
        Assert.Equal(1, engine.Executions);
    }

    [Fact]
    public async Task Reparation_Sans_Distribution_Echoue_Explicitement()
    {
        var (_, policy) = TestEngine.Create();
        var record = DemoWorld.CreateExistingInstallation(
            policy.InstallDir, policy.DataDir, Path.Combine(policy.DataDir, "workspace"));
        var request = new MaintenanceRequest(record, MaintenanceOperation.Repair);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            HeadlessRunner.RepairAsync(new StubEngine(), request, new StringWriter()));
    }

    private static UninstallRequest UninstallOf(UninstallScope scope)
    {
        var record = new InstallationRecord(
            "LIVEX", "0.2.0", @"C:\Users\demo\AppData\Local\Programs\LIVEX",
            @"C:\Users\demo\AppData\Roaming\LIVEX", null, DateTimeOffset.UtcNow);

        return new UninstallRequest(record, scope, Array.Empty<FileEntry>(), DryRun: true);
    }

    /// <summary>Moteur piloté : le test décide du préflight, de la détection et du résultat.</summary>
    private sealed class StubEngine : IInstallerEngine
    {
        public PreflightResult Preflight { get; init; } = new(
            true,
            new[] { new PreflightCheck("license", PreflightStatus.Pass, null) },
            Array.Empty<string>());

        public DetectionResult Detection { get; init; } = new(false, null, null, false, Array.Empty<string>());

        public ExecutionResult Execution { get; init; } = new(true, Array.Empty<string>(), Array.Empty<string>(), "(simulation)");

        public int Executions { get; private set; }

        public Task<PreflightResult> PreflightAsync(InstallRequest request, CancellationToken ct = default) =>
            Task.FromResult(Preflight);

        public Task<InstallPlan> PlanAsync(InstallRequest request, CancellationToken ct = default) =>
            Task.FromResult(EmptyPlan());

        public Task<ExecutionResult> ExecuteAsync(InstallPlan plan, InstallRequest request,
            IProgress<ProgressReport>? progress = null, CancellationToken ct = default)
        {
            Executions++;
            return Task.FromResult(Execution);
        }

        public Task<InstallPlan> PlanMaintenanceAsync(MaintenanceRequest request, CancellationToken ct = default) =>
            Task.FromResult(EmptyPlan());

        private static InstallPlan EmptyPlan() => new(
            InstallOperation.Install,
            Array.Empty<PlanAction>(),
            Array.Empty<string>(),
            0,
            0,
            Array.Empty<string>(),
            RequiresConfirmation: false);

        public Task<ExecutionResult> ExecuteUninstallAsync(UninstallPlan plan, UninstallRequest request,
            IProgress<ProgressReport>? progress = null, CancellationToken ct = default)
        {
            Executions++;
            return Task.FromResult(Execution);
        }

        public Task<DetectionResult> DetectAsync(CancellationToken ct = default) => Task.FromResult(Detection);

        public Task<DetectionResult> DetectAsync(InstallationRecord? simulatedRecord, CancellationToken ct = default) =>
            Task.FromResult(Detection);
    }
}
