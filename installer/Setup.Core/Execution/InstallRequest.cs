// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Distribution;
using Setup.Core.Simulation;

namespace Setup.Core.Execution;

/// <summary>
/// Paramètres d'une installation.
/// <para>
/// <see cref="DryRun"/> est un contrat fort : aucune écriture, aucun accès
/// réseau, aucun processus lancé. Le mode démonstration l'active toujours.
/// </para>
/// <para>
/// <see cref="Simulated"/> dit que le <em>monde</em> est fictif (manifeste de
/// démonstration, aucune charge utile ne peut exister). Il est distinct de
/// <see cref="DryRun"/>, qui ne fait que suspendre les écritures : un
/// <c>--check</c> sur une distribution réelle est un dry-run non simulé, et
/// doit donc signaler une charge utile manquante.
/// </para>
/// </summary>
public sealed record InstallRequest(
    DistributionManifest Manifest,
    string InstallDir,
    string DataRoot,
    string WorkspaceDir,
    IReadOnlyList<string> SelectedComponents,
    bool AcceptLicense,
    bool CreateDesktopShortcut,
    bool LaunchAfterInstall,
    bool DryRun,
    string? OfflineAssetsDir,
    bool NoNetwork,
    string? PayloadRoot = null,
    DemoScenario Scenario = DemoScenario.FreshInstall,
    int SimulationDelayMs = 45,
    bool Simulated = false);
