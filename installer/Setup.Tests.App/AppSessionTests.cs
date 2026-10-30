// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Resources;
using Setup.App.CommandLine;
using Setup.App.Localization;
using Setup.App.Services;
using Setup.Core.Execution;
using Setup.Core.Planning;
using Setup.Core.Simulation;
using Xunit;

namespace Setup.Tests.App;

/// <summary>
/// Tests de la façade de l'assistant : monde fictif, préflight, plan,
/// simulation d'écriture et désinstallation. Aucun test n'écrit sur le disque.
/// </summary>
public class AppSessionTests
{
    [Fact]
    public void DemoSession_UsesFictionalWorldAndWritesNothing()
    {
        var session = CreateSession(DemoScenario.FreshInstall);

        Assert.True(session.IsDemo);
        Assert.Equal(DemoWorld.Version, session.Manifest.Version);
        Assert.Contains(session.Manifest.Components, c => c.Id == "launcher");
    }

    [Fact]
    public async Task DemoSession_DetectsExistingInstallation()
    {
        var session = CreateSession(DemoScenario.ExistingInstall);

        var detection = await session.DetectAsync();

        Assert.True(detection.IsInstalled);
        Assert.True(detection.IsValid);
        Assert.Equal(DemoWorld.InstalledVersion, detection.Record?.Version);
    }

    [Fact]
    public async Task DemoSession_Install_RunsToCompletion_WithoutWriting()
    {
        var session = CreateSession(DemoScenario.FreshInstall);
        var root = Path.Combine(Path.GetTempPath(), "livex-demo-" + Guid.NewGuid().ToString("N"));
        var request = BuildRequest(session, license: true,
            installDir: Path.Combine(root, "app"),
            dataRoot: Path.Combine(root, "data"),
            workspaceDir: Path.Combine(root, "workspace"));

        var preflight = await session.PreflightAsync(request);
        Assert.True(preflight.Success);

        var plan = await session.PlanAsync(request);
        Assert.NotEmpty(plan.Actions);
        Assert.True(plan.BytesToWrite > 0);

        var result = await session.ExecuteAsync(plan, request);

        Assert.True(result.Success);
        Assert.True(result.Simulated);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task DemoSession_FailureScenario_ReportsFailure()
    {
        var session = CreateSession(DemoScenario.FailureDuringWrite);
        var request = BuildRequest(session, license: true);
        var plan = await session.PlanAsync(request);

        var result = await session.ExecuteAsync(plan, request);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Residuals);
        Assert.True(result.Simulated);
    }

    [Fact]
    public void DemoSession_ExistingInstall_ReportsInstalledComponents()
    {
        var session = CreateSession(DemoScenario.ExistingInstall);

        var installed = session.InstalledComponentIds;

        Assert.Contains("launcher", installed);
        Assert.Contains("syne", installed);
        Assert.DoesNotContain("prism-ldk", installed);
    }

    [Fact]
    public async Task DemoSession_MaintenanceUpdate_IsPlannedAndSimulated()
    {
        var session = CreateSession(DemoScenario.ExistingInstall);
        await session.DetectAsync();
        var request = BuildRequest(session, license: true);

        var result = await session.ExecuteMaintenanceAsync(MaintenanceActionKind.Update, request);

        Assert.True(result.Success);
        Assert.True(result.Simulated);
        Assert.NotEmpty(result.AffectedPaths ?? Array.Empty<string>());
    }

    [Fact]
    public async Task DemoSession_MaintenanceRefusesToRemoveMandatoryComponent()
    {
        var session = CreateSession(DemoScenario.ExistingInstall);
        await session.DetectAsync();
        var request = BuildRequest(session, license: true);

        var result = await session.ExecuteMaintenanceAsync(
            MaintenanceActionKind.ManageComponents, request,
            addComponents: null, removeComponents: new[] { "launcher" });

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("obligatoire"));
    }

    [Fact]
    public async Task DemoSession_ChangeWorkspace_KeepsThePreviousFolder()
    {
        var session = CreateSession(DemoScenario.ExistingInstall);
        await session.DetectAsync();
        var request = BuildRequest(session, license: true,
            workspaceDir: Path.Combine(Path.GetTempPath(), "livex-new-workspace"));

        var result = await session.ExecuteMaintenanceAsync(
            MaintenanceActionKind.ChangeWorkspace, request);

        Assert.True(result.Success);
        Assert.True(result.Simulated);
        Assert.Contains(result.Warnings, warning => warning.Contains("conservé"));
    }

    [Fact]
    public async Task DemoSession_Uninstall_IsSimulated()
    {
        var session = CreateSession(DemoScenario.ExistingInstall);
        await session.DetectAsync();

        var result = await session.ExecuteUninstallAsync(UninstallScope.SoftwareConfigAndWorkspace, purgeResearch: true);

        Assert.True(result.Success);
        Assert.True(result.Simulated);
    }

    [Fact]
    public void Localization_ContainsTheSameKeysInBothLanguages()
    {
        var manager = new ResourceManager("Setup.App.Resources.Strings", typeof(Strings).Assembly);
        var french = manager.GetResourceSet(CultureInfo.GetCultureInfo("fr"), true, true)!;
        var english = manager.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true)!;

        var frenchKeys = french.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).ToHashSet();
        var englishKeys = english.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).ToHashSet();

        Assert.Empty(frenchKeys.Except(englishKeys));
        Assert.Empty(englishKeys.Except(frenchKeys));
        Assert.Contains("welcomeTitle", frenchKeys);
    }

    [Fact]
    public void SizeFormatter_UsesTheLanguageUnits()
    {
        Strings.Instance.SetLanguage("fr");
        Assert.EndsWith(" Mo", SizeFormatter.Format(5 * 1024 * 1024));

        Strings.Instance.SetLanguage("en");
        Assert.EndsWith(" MB", SizeFormatter.Format(5 * 1024 * 1024));

        Strings.Instance.SetLanguage("fr");
    }

    private static InstallerSession CreateSession(DemoScenario scenario)
    {
        var options = AppOptions.Parse(["--demo", "--demo-scenario", scenario switch
        {
            DemoScenario.ExistingInstall => "existing",
            DemoScenario.FailureDuringWrite => "failure",
            DemoScenario.PrismAbsent => "prism",
            _ => "fresh",
        }]);

        return InstallerSession.Create(options);
    }

    private static InstallRequest BuildRequest(
        InstallerSession session,
        bool license,
        string? installDir = null,
        string? dataRoot = null,
        string? workspaceDir = null) => new(
        session.Manifest,
        installDir ?? session.DefaultInstallDir,
        dataRoot ?? session.DefaultDataRoot,
        workspaceDir ?? session.DefaultWorkspaceDir,
        session.Manifest.Components.Where(c => c.Mandatory || c.Id == "prism").Select(c => c.Id).ToList(),
        AcceptLicense: license,
        CreateDesktopShortcut: false,
        LaunchAfterInstall: false,
        DryRun: session.IsDemo,
        OfflineAssetsDir: null,
        NoNetwork: true,
        Scenario: session.Scenario,
        SimulationDelayMs: 0,
        Simulated: session.IsDemo);
}
