// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Execution;
using Setup.Core.Planning;
using Setup.Core.Preflight;
using Setup.Core.Simulation;
using Setup.Tests.Support;
using Xunit;

namespace Setup.Tests.Unit;

public class InstallerEngineTests
{
    [Fact]
    public async Task Preflight_Echoue_Si_La_Licence_N_est_Pas_Acceptee()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, acceptLicense: false);

        var result = await engine.PreflightAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Checks, check => check.Name == "license" && check.Failed);
    }

    [Fact]
    public async Task Preflight_Reussit_Sur_Le_Monde_Fictif()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy);

        var result = await engine.PreflightAsync(request);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Contains(result.Checks, check => check.Name == "writable" && check.IsSimulated);
    }

    [Fact]
    public async Task Preflight_Reel_Echoue_Sans_Charge_Utile()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, dryRun: false);

        var result = await engine.PreflightAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Checks, check => check.Name == "payload" && check.Failed);
    }

    [Fact]
    public async Task Preflight_Simule_Accepte_L_Absence_De_Charge_Utile()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, dryRun: true, simulated: true);

        var result = await engine.PreflightAsync(request);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Contains(result.Checks, check => check.Name == "payload" && check.Status == PreflightStatus.Info);
    }

    [Fact]
    public async Task Check_Sur_Une_Distribution_Reelle_Echoue_Si_La_Charge_Utile_Manque()
    {
        // --check : dry-run (aucune écriture) sur une distribution réelle.
        // Le monde n'est pas fictif, donc l'absence de payload/ est un défaut.
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, dryRun: true, simulated: false);

        var result = await engine.PreflightAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Checks, check => check.Name == "payload" && check.Failed);
    }

    [Fact]
    public async Task Preflight_Refuse_Un_Espace_De_Travail_Imbrique_Dans_Le_Logiciel()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, workspaceDir: Path.Combine(policy.InstallDir, "workspace"));

        var result = await engine.PreflightAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Checks, check => check.Name == "workspace" && check.Failed);
    }

    [Fact]
    public async Task Preflight_Refuse_Un_Processus_En_Cours_En_Mode_Reel()
    {
        var (engine, policy) = TestEngine.Create();
        var processProbe = new FakeProcessProbe { Running = true };
        var realEngine = new InstallerEngine(policy, processProbe, new FakePrerequisiteProbe(), new NoopFileSync());
        var request = TestEngine.DemoRequest(policy, dryRun: false);

        var result = await realEngine.PreflightAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Checks, check => check.Name == "process" && check.Failed);
        Assert.NotNull(engine);
    }

    [Fact]
    public async Task Plan_Ne_Cree_Rien_Sur_Le_Disque()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy);

        var plan = await engine.PlanAsync(request);

        Assert.NotEmpty(plan.Actions);
        Assert.False(Directory.Exists(policy.InstallDir));
        Assert.False(Directory.Exists(policy.DataDir));
        Assert.False(Directory.Exists(request.WorkspaceDir));
    }

    [Fact]
    public async Task Plan_Declare_L_Association_Livexp_Et_Le_Raccourci_Menu()
    {
        var (engine, policy) = TestEngine.Create();
        var plan = await engine.PlanAsync(TestEngine.DemoRequest(policy));

        Assert.Contains(plan.Actions, action => action is AssociatePackageAction);
        Assert.Contains(plan.Actions, action => action is CreateShortcutAction { IsDesktop: false });
        Assert.DoesNotContain(plan.Actions, action => action is CreateShortcutAction { IsDesktop: true });
    }

    [Fact]
    public async Task Plan_Ajoute_Le_Raccourci_Bureau_Seulement_Si_Demande()
    {
        var (engine, policy) = TestEngine.Create();
        var plan = await engine.PlanAsync(TestEngine.DemoRequest(policy, desktopShortcut: true));

        Assert.Contains(plan.Actions, action => action is CreateShortcutAction { IsDesktop: true });
    }

    [Fact]
    public async Task Plan_Avertit_Sur_Un_Composant_Indisponible_Selectionne()
    {
        var (engine, policy) = TestEngine.Create();
        var manifest = DemoWorld.CreateManifest(DemoScenario.PrismAbsent);
        var selection = DemoWorld.DefaultSelection().Append("prism").ToArray();
        var request = TestEngine.DemoRequest(policy, manifest: manifest, selection: selection);

        var plan = await engine.PlanAsync(request);

        Assert.Contains(plan.Warnings, warning => warning.Contains("indisponible", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Plan_N_Extrait_Jamais_L_Archive_Du_LDK()
    {
        var (engine, policy) = TestEngine.Create();
        var selection = DemoWorld.DefaultSelection().Append("prism-ldk").ToArray();
        var plan = await engine.PlanAsync(TestEngine.DemoRequest(policy, selection: selection));

        var download = Assert.Single(plan.Actions.OfType<DownloadAssetAction>());
        Assert.Contains(Path.Combine("plugins", "prism-ldk"), download.TargetPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(plan.Actions.OfType<ExtractArchiveAction>(), action => action.ArchivePath == download.TargetPath);
    }

    [Fact]
    public async Task Execution_Simulee_N_ecrit_Rien_Et_Progresse_Jusqu_Au_Bout()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, delayMs: 0);
        var plan = await engine.PlanAsync(request);

        var progress = new CollectingProgress();
        var result = await engine.ExecuteAsync(plan, request, progress);

        Assert.True(result.Success, string.Join("; ", result.Residuals));
        Assert.True(result.Simulated);
        Assert.Equal("(simulation)", result.LogPath);
        Assert.Equal(plan.Actions.Count, progress.Reports.Count);
        Assert.Equal(plan.Actions.Count, progress.Reports[^1].Current);
        Assert.False(Directory.Exists(policy.InstallDir));
        Assert.False(Directory.Exists(policy.DataDir));
        Assert.False(Directory.Exists(request.WorkspaceDir));
    }

    [Fact]
    public async Task Scenario_D_echec_Rend_L_Execution_En_Echec_Sans_Ecrire()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, scenario: DemoScenario.FailureDuringWrite, delayMs: 0);
        var plan = await engine.PlanAsync(request);

        var result = await engine.ExecuteAsync(plan, request);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Residuals);
        Assert.False(Directory.Exists(policy.InstallDir));
    }

    [Fact]
    public async Task Detection_Simulee_Retrouve_Une_Installation_Existante()
    {
        var (engine, policy) = TestEngine.Create();
        var record = DemoWorld.CreateExistingInstallation(policy.InstallDir, policy.DataDir, Path.Combine(policy.DataDir, "workspace"));

        var result = await engine.DetectAsync(record);

        Assert.True(result.IsInstalled);
        Assert.True(result.IsValid);
        Assert.Equal(policy.InstallDir, result.InstallDir);
    }

    [Fact]
    public async Task Detection_Simulee_Sans_Enregistrement_Ne_Conclut_Pas_A_Une_Installation()
    {
        var (engine, _) = TestEngine.Create();

        var result = await engine.DetectAsync((Setup.Core.Lifecycle.InstallationRecord?)null);

        Assert.False(result.IsInstalled);
    }
}
