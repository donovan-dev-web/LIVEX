// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Lifecycle;
using Setup.Core.Planning;
using Setup.Core.Preflight;

namespace Setup.Core.Execution;

public interface IInstallerEngine
{
    Task<PreflightResult> PreflightAsync(InstallRequest request, CancellationToken ct = default);

    Task<InstallPlan> PlanAsync(InstallRequest request, CancellationToken ct = default);

    Task<ExecutionResult> ExecuteAsync(
        InstallPlan plan,
        InstallRequest request,
        IProgress<ProgressReport>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Planifie une maintenance (mise à jour, réparation, gestion des
    /// composants, changement d'espace de travail). Le plan est pur : rien
    /// n'est écrit avant <see cref="ExecuteAsync"/>.
    /// </summary>
    Task<InstallPlan> PlanMaintenanceAsync(MaintenanceRequest request, CancellationToken ct = default);

    /// <summary>Exécute un plan de désinstallation. <c>DryRun</c> ne supprime rien.</summary>
    Task<ExecutionResult> ExecuteUninstallAsync(
        UninstallPlan plan,
        UninstallRequest request,
        IProgress<ProgressReport>? progress = null,
        CancellationToken ct = default);

    /// <summary>Détection sur la machine réelle.</summary>
    Task<DetectionResult> DetectAsync(CancellationToken ct = default);

    /// <summary>
    /// Détection sur un enregistrement fourni (démonstration, tests) : aucun
    /// accès au registre ni au disque.
    /// </summary>
    Task<DetectionResult> DetectAsync(InstallationRecord? simulatedRecord, CancellationToken ct = default);
}
