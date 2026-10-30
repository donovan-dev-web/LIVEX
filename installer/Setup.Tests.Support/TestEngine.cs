// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Distribution;
using Setup.Core.Execution;
using Setup.Core.Simulation;

namespace Setup.Tests.Support;

/// <summary>Fabriques communes aux trois projets de tests.</summary>
public static class TestEngine
{
    public static (InstallerEngine Engine, FakePathPolicy Policy) Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "livex-unit-" + Guid.NewGuid().ToString("N"));
        var policy = new FakePathPolicy
        {
            InstallDir = Path.Combine(root, "app"),
            DataDir = Path.Combine(root, "data"),
            ShortcutDir = Path.Combine(root, "startmenu"),
            DesktopDir = Path.Combine(root, "desktop")
        };

        var engine = new InstallerEngine(policy, new FakeProcessProbe(), new FakePrerequisiteProbe(), new NoopFileSync());
        return (engine, policy);
    }

    public static InstallerEngine WithRunningProcess(FakePathPolicy policy, bool running) =>
        new(policy, new FakeProcessProbe { Running = running }, new FakePrerequisiteProbe(), new NoopFileSync());

    public static InstallRequest DemoRequest(
        FakePathPolicy policy,
        string? workspaceDir = null,
        bool acceptLicense = true,
        bool dryRun = true,
        bool desktopShortcut = false,
        IReadOnlyList<string>? selection = null,
        DistributionManifest? manifest = null,
        DemoScenario scenario = DemoScenario.FreshInstall,
        int delayMs = 0,
        bool noNetwork = true,
        bool? simulated = null)
    {
        manifest ??= DemoWorld.CreateManifest(scenario);
        return new InstallRequest(
            manifest,
            policy.InstallDir,
            policy.DataDir,
            workspaceDir ?? Path.Combine(policy.DataDir, "workspace"),
            selection ?? DemoWorld.DefaultSelection(scenario),
            acceptLicense,
            desktopShortcut,
            LaunchAfterInstall: true,
            DryRun: dryRun,
            OfflineAssetsDir: null,
            NoNetwork: noNetwork,
            PayloadRoot: null,
            Scenario: scenario,
            SimulationDelayMs: delayMs,
            Simulated: simulated ?? dryRun);
    }
}
