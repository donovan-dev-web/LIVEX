// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Lifecycle;
using Setup.Core.Planning;
using Setup.Core.Simulation;
using Xunit;

namespace Setup.Tests.Unit;

public class UninstallPlannerTests
{
    private static InstallationRecord Record(string? workspace = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "livex-uninstall-" + Guid.NewGuid().ToString("N"));
        return new InstallationRecord(
            DemoWorld.Product,
            DemoWorld.InstalledVersion,
            Path.Combine(root, "app"),
            Path.Combine(root, "data"),
            workspace ?? Path.Combine(root, "data", "workspace"),
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Portee_Logiciel_Conserve_Configuration_Et_Espace_De_Travail()
    {
        var record = Record();
        var plan = UninstallPlanner.Plan(new UninstallRequest(
            record, UninstallScope.Software, DemoWorld.CreateInventory()));

        Assert.DoesNotContain(plan.Actions, action => action is RemoveSettingsAction);
        Assert.DoesNotContain(plan.Actions, action =>
            action is DeleteDirectoryAction && action.TargetPath == record.WorkspacePath);
        Assert.Contains(plan.Actions, action => action is RemovePackageAssociationAction);
        Assert.Contains(plan.Actions, action => action is RemoveShortcutsAction);
        Assert.Contains(plan.Actions, action => action is RemoveRegistrationAction);
        Assert.Contains(plan.Actions, action =>
            action is DeleteFileAction && action.TargetPath.EndsWith(".livex-install.json", StringComparison.Ordinal));
        Assert.Contains(plan.Warnings, warning => warning.Contains("cache", StringComparison.Ordinal));
    }

    [Fact]
    public void Portee_Logiciel_Config_Supprime_Configuration_Et_Cache()
    {
        var plan = UninstallPlanner.Plan(new UninstallRequest(
            Record(), UninstallScope.SoftwareAndConfig, DemoWorld.CreateInventory()));

        Assert.Contains(plan.Actions, action => action is RemoveSettingsAction);
        Assert.Contains(plan.Actions, action =>
            action is DeleteDirectoryAction { Recursive: true } && action.TargetPath.EndsWith("cache", StringComparison.Ordinal));
    }

    [Fact]
    public void Portee_Complete_Sans_Confirmation_Ne_Touche_Pas_A_L_Espace_De_Travail()
    {
        var record = Record();
        var plan = UninstallPlanner.Plan(new UninstallRequest(
            record, UninstallScope.SoftwareConfigAndWorkspace, DemoWorld.CreateInventory(), WorkspaceDeletionConfirmed: false));

        Assert.DoesNotContain(plan.Actions, action =>
            action is DeleteDirectoryAction && action.TargetPath == record.WorkspacePath);
        Assert.Contains(plan.Warnings, warning => warning.Contains("non confirmée", StringComparison.Ordinal));
        Assert.False(plan.RequiresConfirmation);
    }

    [Fact]
    public void Portee_Complete_Confirmee_Supprime_L_Espace_De_Travail_Et_Exige_Une_Confirmation()
    {
        var record = Record();
        var plan = UninstallPlanner.Plan(new UninstallRequest(
            record, UninstallScope.SoftwareConfigAndWorkspace, DemoWorld.CreateInventory(), WorkspaceDeletionConfirmed: true));

        Assert.Contains(plan.Actions, action =>
            action is DeleteDirectoryAction && action.TargetPath == record.WorkspacePath);
        Assert.True(plan.RequiresConfirmation);
    }

    [Fact]
    public void Un_Espace_De_Travail_Dangereux_Est_Refuse()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var record = Record(home);

        var plan = UninstallPlanner.Plan(new UninstallRequest(
            record, UninstallScope.SoftwareConfigAndWorkspace, DemoWorld.CreateInventory(), WorkspaceDeletionConfirmed: true));

        Assert.DoesNotContain(plan.Actions, action =>
            action is DeleteDirectoryAction && action.TargetPath == home);
        Assert.NotEmpty(plan.Residuals);
    }

    [Fact]
    public void La_Purge_Des_Resultats_De_Recherche_Exige_Une_Seconde_Confirmation()
    {
        var plan = UninstallPlanner.Plan(new UninstallRequest(
            Record(), UninstallScope.SoftwareAndConfig, DemoWorld.CreateInventory(), PurgeResearch: true));

        Assert.True(plan.RequiresConfirmation);
        Assert.Contains(plan.Actions, action =>
            action is DeleteDirectoryAction && action.TargetPath.EndsWith("sessions", StringComparison.Ordinal));
    }

    [Fact]
    public void Les_Journaux_Ne_Sont_Jamais_Supprimes()
    {
        var plan = UninstallPlanner.Plan(new UninstallRequest(
            Record(), UninstallScope.SoftwareConfigAndWorkspace, DemoWorld.CreateInventory(), PurgeResearch: true));

        Assert.DoesNotContain(plan.Actions, action =>
            action.TargetPath.Contains("logs", StringComparison.OrdinalIgnoreCase));
    }
}

public class MaintenancePlannerTests
{
    private static InstallationRecord Record() =>
        new(DemoWorld.Product, DemoWorld.InstalledVersion,
            Path.Combine(Path.GetTempPath(), "livex-maintenance", "app"),
            Path.Combine(Path.GetTempPath(), "livex-maintenance", "data"),
            Path.Combine(Path.GetTempPath(), "livex-maintenance", "data", "workspace"),
            DateTimeOffset.UtcNow);

    [Fact]
    public void Ajouter_Le_LDK_TELECHARGE_L_Archive_Sans_L_Extraire()
    {
        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            Record(), MaintenanceOperation.ManageComponents,
            Manifest: DemoWorld.CreateManifest(), AddComponents: new[] { "prism-ldk" }));

        var download = Assert.Single(plan.Actions.OfType<DownloadAssetAction>());
        Assert.Contains("prism-ldk", download.TargetPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(plan.Actions.OfType<ExtractArchiveAction>(), action => action.ArchivePath == download.TargetPath);
        Assert.Contains(plan.Actions, action => action is UpdateInventoryAction);
    }

    [Fact]
    public void Ajouter_La_Documentation_L_Extrait()
    {
        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            Record(), MaintenanceOperation.ManageComponents,
            Manifest: DemoWorld.CreateManifest(), AddComponents: new[] { "docs" }));

        Assert.Contains(plan.Actions, action => action is ExtractArchiveAction);
    }

    [Fact]
    public void Retirer_Un_Composant_Obligatoire_Est_Refuse()
    {
        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            Record(), MaintenanceOperation.ManageComponents,
            Manifest: DemoWorld.CreateManifest(), RemoveComponents: new[] { "syne" }));

        Assert.Empty(plan.Actions);
        Assert.Contains(plan.Warnings, warning => warning.Contains("obligatoire", StringComparison.Ordinal));
    }

    [Fact]
    public void Retirer_Prism_Supprime_Ses_Fichiers_Et_Met_A_Jour_L_Inventaire()
    {
        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            Record(), MaintenanceOperation.ManageComponents,
            Manifest: DemoWorld.CreateManifest(), RemoveComponents: new[] { "prism" }));

        Assert.Equal(2, plan.Actions.OfType<DeleteFileAction>().Count());
        Assert.Contains(plan.Actions, action => action is UpdateInventoryAction);
    }

    [Fact]
    public void Une_Mise_A_Jour_Refuse_Un_Downgrade()
    {
        var record = Record() with { Version = "0.9.0" };
        var manifest = DemoWorld.CreateManifest(); // 0.2.0-demo

        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            record, MaintenanceOperation.Update, Manifest: manifest));

        Assert.Empty(plan.Actions);
        Assert.Contains(plan.Warnings, warning => warning.Contains("antérieure", StringComparison.Ordinal));
    }

    [Fact]
    public void Une_Mise_A_Jour_Remplace_Les_Fichiers_Du_Socle()
    {
        var record = Record();
        var inventory = DemoWorld.CreateInventory();

        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            record, MaintenanceOperation.Update,
            Manifest: DemoWorld.CreateManifest(), Inventory: inventory));

        Assert.NotEmpty(plan.Actions.OfType<ReplaceFileAction>());
        Assert.Contains(plan.Actions, action => action is WriteRegistrationAction);
        Assert.Contains(plan.Actions, action => action is UpdateInventoryAction);
    }

    [Fact]
    public void Une_Reparation_Restaure_Les_Fichiers_Manquants()
    {
        var record = Record();
        var inventory = DemoWorld.CreateInventory();

        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            record, MaintenanceOperation.Repair,
            Manifest: DemoWorld.CreateManifest(), Inventory: inventory, DryRun: true));

        Assert.NotEmpty(plan.Actions.OfType<WriteFileAction>());
        Assert.Contains(plan.Warnings, warning => warning.Contains("restaurés", StringComparison.Ordinal));
    }

    [Fact]
    public void Une_Reparation_Sans_Inventaire_Ne_Devine_Rien()
    {
        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            Record(), MaintenanceOperation.Repair, Manifest: DemoWorld.CreateManifest()));

        Assert.Empty(plan.Actions);
        Assert.Contains(plan.Warnings, warning => warning.Contains("Inventaire absent", StringComparison.Ordinal));
    }

    [Fact]
    public void Changer_D_Espace_De_Travail_Ne_Deplace_Rien()
    {
        var record = Record();
        var plan = MaintenancePlanner.Plan(new MaintenanceRequest(
            record, MaintenanceOperation.ChangeWorkspace,
            NewWorkspacePath: Path.Combine(Path.GetTempPath(), "livex-maintenance", "nouveau")));

        Assert.Contains(plan.Actions, action => action is MergeSettingsAction);
        Assert.Contains(plan.Actions, action => action is ReplaceFileAction);
        Assert.DoesNotContain(plan.Actions, action => action is DeleteDirectoryAction);
        Assert.Contains(plan.Warnings, warning => warning.Contains("conservé", StringComparison.Ordinal));
    }
}
