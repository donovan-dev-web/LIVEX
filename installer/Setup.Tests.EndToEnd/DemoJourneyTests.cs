// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Distribution;
using Setup.Core.Execution;
using Setup.Core.Lifecycle;
using Setup.Core.Planning;
using Setup.Core.Simulation;
using Setup.Tests.Support;
using Xunit;

namespace Setup.Tests.EndToEnd;

/// <summary>
/// Parcours complets dans le monde fictif : détection, préflight, plan,
/// progression, résultat, maintenance et désinstallation. Aucune écriture
/// disque n'est tolérée : c'est le contrat du mode démonstration.
/// </summary>
public class DemoJourneyTests
{
    [Fact]
    public async Task Parcours_Installation_Complet_Sans_Aucune_Ecriture()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, delayMs: 0);

        // 1. Détection : machine vierge.
        var detection = await engine.DetectAsync((InstallationRecord?)null);
        Assert.False(detection.IsInstalled);

        // 2. Préflight.
        var preflight = await engine.PreflightAsync(request);
        Assert.True(preflight.Success, string.Join("; ", preflight.Errors));

        // 3. Plan et récapitulatif.
        var plan = await engine.PlanAsync(request);
        Assert.NotEmpty(plan.Actions);
        Assert.True(plan.BytesToWrite > 0);
        Assert.Empty(plan.Warnings);

        // 4. Progression.
        var progress = new CollectingProgress();
        var result = await engine.ExecuteAsync(plan, request, progress);

        // 5. Résultat.
        Assert.True(result.Success);
        Assert.True(result.Simulated);
        Assert.Equal(plan.Actions.Count, progress.Reports.Count);
        Assert.Equal(plan.Actions.Count, progress.Reports[^1].Current);

        // 6. Aucun effet de bord.
        Assert.False(Directory.Exists(policy.InstallDir));
        Assert.False(Directory.Exists(policy.DataDir));
        Assert.False(Directory.Exists(request.WorkspaceDir));
    }

    [Fact]
    public async Task Parcours_Prism_Desactive_Conserve_Le_Socle_Et_Avertit()
    {
        var (engine, policy) = TestEngine.Create();
        var selection = DemoWorld.DefaultSelection().Where(id => id != "prism").ToArray();
        var request = TestEngine.DemoRequest(policy, selection: selection);

        var preflight = await engine.PreflightAsync(request);
        Assert.True(preflight.Success);

        var plan = await engine.PlanAsync(request);
        Assert.DoesNotContain(plan.Actions, action =>
            action.TargetPath.Contains("prism", StringComparison.OrdinalIgnoreCase) &&
            action is WriteFileAction);

        var result = await engine.ExecuteAsync(plan, request);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Parcours_Maintenance_Sur_Une_Installation_Existante()
    {
        var (engine, policy) = TestEngine.Create();
        var record = DemoWorld.CreateExistingInstallation(
            policy.InstallDir, policy.DataDir, Path.Combine(policy.DataDir, "workspace"));

        var detection = await engine.DetectAsync(record);
        Assert.True(detection.IsInstalled);
        Assert.True(detection.IsValid);
        Assert.Equal(DemoWorld.InstalledVersion, detection.Record!.Version);

        var newWorkspace = Path.Combine(policy.DataDir, "workspace-2");
        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            record, MaintenanceOperation.ChangeWorkspace, NewWorkspacePath: newWorkspace));

        var request = TestEngine.DemoRequest(policy, workspaceDir: newWorkspace);
        var result = await engine.ExecuteAsync(plan, request);

        Assert.True(result.Success);
        Assert.True(result.Simulated);
        Assert.Contains(plan.Warnings, warning => warning.Contains("conservé", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Parcours_Desinstallation_Complete_Avec_Confirmation()
    {
        var (engine, policy) = TestEngine.Create();
        var record = DemoWorld.CreateExistingInstallation(
            policy.InstallDir, policy.DataDir, Path.Combine(policy.DataDir, "workspace"));

        var plan = UninstallPlanner.Plan(new UninstallRequest(
            record, UninstallScope.SoftwareConfigAndWorkspace, DemoWorld.CreateInventory(),
            WorkspaceDeletionConfirmed: true));

        Assert.True(plan.RequiresConfirmation);
        Assert.Contains(plan.Actions, action =>
            action is DeleteDirectoryAction && action.TargetPath == record.WorkspacePath);

        var result = await engine.ExecuteUninstallAsync(
            plan,
            new UninstallRequest(record, UninstallScope.SoftwareConfigAndWorkspace, DemoWorld.CreateInventory(),
                WorkspaceDeletionConfirmed: true, SimulationDelayMs: 0));

        Assert.True(result.Success);
        Assert.True(result.Simulated);
        Assert.False(Directory.Exists(policy.InstallDir));
        Assert.False(Directory.Exists(policy.DataDir));
    }

    [Fact]
    public async Task Parcours_Echec_Affiche_Des_Residus_Sans_Ecrire()
    {
        var (engine, policy) = TestEngine.Create();
        var request = TestEngine.DemoRequest(policy, scenario: DemoScenario.FailureDuringWrite);
        var plan = await engine.PlanAsync(request);

        var result = await engine.ExecuteAsync(plan, request);

        Assert.False(result.Success);
        Assert.True(result.Simulated);
        Assert.NotEmpty(result.Residuals);
        Assert.False(Directory.Exists(policy.InstallDir));
    }

    [Fact]
    public async Task Le_Plan_De_Demonstration_Couvre_Les_Huit_Etapes_Du_Parcours()
    {
        var (engine, policy) = TestEngine.Create();
        var plan = await engine.PlanAsync(TestEngine.DemoRequest(policy, desktopShortcut: true));

        Assert.Contains(plan.Actions, action => action is CreateDirectoryAction);
        Assert.Contains(plan.Actions, action => action is WriteFileAction);
        Assert.Contains(plan.Actions, action => action is MergeSettingsAction);
        Assert.Contains(plan.Actions, action => action is CreateShortcutAction);
        Assert.Contains(plan.Actions, action => action is AssociatePackageAction);
        Assert.Contains(plan.Actions, action => action is WriteRegistrationAction);
    }
}
