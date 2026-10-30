// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Execution;
using Setup.Core.Journal;
using Setup.Core.Planning;

namespace Setup.Core.Simulation;

/// <summary>
/// Rejoue un plan sans toucher au disque : aucune création de répertoire,
/// aucune écriture, aucun téléchargement, aucun raccourci, aucun registre.
/// Le parcours, la progression et les messages d'erreur sont ceux du réel.
/// </summary>
public static class SimulationExecutor
{
    public static Task<ExecutionResult> ExecuteAsync(
        InstallPlan plan,
        InstallRequest request,
        IProgress<ProgressReport>? progress = null,
        CancellationToken ct = default) =>
        Run(plan.Actions, plan.Warnings, plan.BytesToWrite, request.Scenario, request.SimulationDelayMs, progress, ct);

    public static Task<ExecutionResult> ExecuteAsync(
        UninstallPlan plan,
        UninstallRequest request,
        IProgress<ProgressReport>? progress = null,
        CancellationToken ct = default) =>
        Run(plan.Actions, plan.Warnings, 0, DemoScenario.FreshInstall, request.SimulationDelayMs, progress, ct);

    private static async Task<ExecutionResult> Run(
        IReadOnlyList<PlanAction> actions,
        IReadOnlyList<string> warnings,
        long bytesTotal,
        DemoScenario scenario,
        int delayMs,
        IProgress<ProgressReport>? progress,
        CancellationToken ct)
    {
        var journal = new InMemoryJournal();
        var affected = new List<string>();
        var total = actions.Count;
        var current = 0;

        try
        {
            foreach (var action in actions)
            {
                ct.ThrowIfCancellationRequested();
                current++;

                if (scenario == DemoScenario.FailureDuringWrite && ShouldInjectFailure(action, current, total))
                {
                    journal.Write(JournalLevel.Error, "install", PlanActionLabel.Of(action), "failed",
                        code: "SIMULATED_IO", path: action.TargetPath);
                    return new ExecutionResult(
                        false,
                        warnings,
                        new[] { "Échec simulé : écriture impossible dans le répertoire cible (scénario de démonstration)." },
                        journal.LogPath,
                        Simulated: true,
                        affected);
                }

                var bytes = total == 0 ? 0 : bytesTotal * current / total;
                progress?.Report(new ProgressReport(
                    action.GetType().Name, null, current, total, bytes, bytesTotal,
                    action.TargetPath, PlanActionLabel.Of(action)));

                affected.Add(action.TargetPath);
                journal.Write(JournalLevel.Info, "install", PlanActionLabel.Of(action), "ok", path: action.TargetPath);

                if (delayMs > 0)
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }

            return new ExecutionResult(true, warnings, Array.Empty<string>(), journal.LogPath, Simulated: true, affected);
        }
        catch (OperationCanceledException)
        {
            journal.Write(JournalLevel.Warn, "install", "execute", "cancelled");
            return new ExecutionResult(false, warnings, Array.Empty<string>(), journal.LogPath, Simulated: true, affected);
        }
    }

    /// <summary>
    /// L'échec est injecté sur une écriture de fichier, aux deux tiers du plan :
    /// assez tard pour que la progression soit visible, assez tôt pour que la
    /// compensation ait un sens.
    /// </summary>
    private static bool ShouldInjectFailure(PlanAction action, int current, int total) =>
        action is WriteFileAction && current >= Math.Max(1, total * 2 / 3);
}
