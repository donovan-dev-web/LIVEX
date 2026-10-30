// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.CommandLine;
using Setup.Core.Distribution;
using Setup.Core.Planning;
using Setup.Core.Preflight;

namespace Setup.Core.Execution;

/// <summary>Résultat d'un mode non interactif : code de sortie + message lisible.</summary>
public sealed record HeadlessOutcome(
    int ExitCode,
    string Message,
    ExecutionResult? Result = null,
    PreflightResult? Preflight = null)
{
    public bool Success => ExitCode == ExitCodes.Success;
}

/// <summary>
/// Exécute une installation, une désinstallation ou une réparation sans
/// interface et applique la table de codes de sortie de la spécification
/// (INS-F-95, INS-F-96) :
/// <c>0</c> succès, <c>1</c> erreur d'usage, <c>2</c> préflight en échec ou
/// opération annulée, <c>3</c> échec d'installation, <c>4</c> désinstallation
/// partielle, <c>5</c> installation déjà présente, <c>6</c> actif facultatif
/// indisponible.
/// <para>
/// Ce lanceur n'attend aucune saisie : toute décision impossible se traduit par
/// un message explicite et un code, jamais par une attente.
/// </para>
/// </summary>
public static class HeadlessRunner
{
    /// <summary>Installe sans assistance et renvoie le code de sortie attendu.</summary>
    public static async Task<HeadlessOutcome> InstallAsync(
        IInstallerEngine engine,
        InstallRequest request,
        TextWriter output,
        CancellationToken ct = default)
    {
        var preflight = await engine.PreflightAsync(request, ct).ConfigureAwait(false);
        WriteChecks(preflight, output);

        if (!preflight.Success)
        {
            return new HeadlessOutcome(ExitCodes.PreflightFailed, "Préflight en échec.", Preflight: preflight);
        }

        if (!request.DryRun)
        {
            var detection = await engine.DetectAsync(ct).ConfigureAwait(false);
            if (detection is { IsInstalled: true, Record: { } record } &&
                string.Equals(record.Version, request.Manifest.Version, StringComparison.OrdinalIgnoreCase))
            {
                return new HeadlessOutcome(
                    ExitCodes.AlreadyInstalled,
                    $"LIVEX {record.Version} est déjà installé dans {record.InstallDir} ; utilisez --repair ou désinstallez d'abord.",
                    Preflight: preflight);
            }
        }

        var plan = await engine.PlanAsync(request, ct).ConfigureAwait(false);
        output.WriteLine($"Plan : {plan.Actions.Count} action(s), {plan.BytesToWrite} octet(s) à écrire.");

        var result = await engine.ExecuteAsync(plan, request, ProgressTo(output), ct).ConfigureAwait(false);
        WriteResult(result, output);

        var exitCode = result.Success
            ? ExitCodes.Success
            : result.ExitCodeHint ?? ExitCodes.InstallFailed;

        return new HeadlessOutcome(exitCode, result.Success ? "Installation terminée." : "Installation en échec.", result, preflight);
    }

    /// <summary>
    /// Installe depuis un fichier de réponses (<c>--unattended --config</c>).
    /// Un fichier absent, illisible ou incomplet échoue avec le code <c>2</c> :
    /// aucun chemin n'est deviné à la place de l'opérateur (ARCHITECTURE §18).
    /// </summary>
    public static async Task<HeadlessOutcome> InstallFromResponsesAsync(
        IInstallerEngine engine,
        DistributionManifest manifest,
        string configPath,
        string defaultDataRoot,
        bool noNetwork,
        TextWriter output,
        string? payloadRoot = null,
        CancellationToken ct = default)
    {
        if (!UnattendedResponsesFile.TryLoad(configPath, manifest, defaultDataRoot, noNetwork,
                out var responses, out var errors))
        {
            foreach (var error in errors)
            {
                output.WriteLine($"[Error] {error}");
            }

            return new HeadlessOutcome(ExitCodes.PreflightFailed, "Fichier de réponses invalide.");
        }

        return await InstallAsync(engine, responses!.ToRequest(manifest, payloadRoot), output, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Désinstalle selon la portée demandée. La portée C exige une confirmation
    /// explicite de la suppression de l'espace de travail ; sans elle,
    /// l'opération est annulée (code <c>2</c>) plutôt que devinée.
    /// </summary>
    public static async Task<HeadlessOutcome> UninstallAsync(
        IInstallerEngine engine,
        UninstallRequest request,
        TextWriter output,
        CancellationToken ct = default)
    {
        if (request.Scope == UninstallScope.SoftwareConfigAndWorkspace && !request.WorkspaceDeletionConfirmed)
        {
            return new HeadlessOutcome(
                ExitCodes.PreflightFailed,
                "La portée C supprime l'espace de travail : elle exige une confirmation explicite (--purge-research ou --confirm-workspace).");
        }

        var plan = UninstallPlanner.Plan(request);
        output.WriteLine($"Plan : {plan.FileCount} fichier(s) supprimé(s) au maximum.");

        foreach (var warning in plan.Warnings)
        {
            output.WriteLine($"[Warn] {warning}");
        }

        var result = await engine.ExecuteUninstallAsync(plan, request, ProgressTo(output), ct).ConfigureAwait(false);
        WriteResult(result, output);

        if (!result.Success)
        {
            return new HeadlessOutcome(result.ExitCodeHint ?? ExitCodes.InstallFailed, "Désinstallation interrompue.", result);
        }

        if (result.Residuals.Count > 0)
        {
            return new HeadlessOutcome(
                ExitCodes.UninstallPartial,
                $"Désinstallation partielle : {result.Residuals.Count} élément(s) résiduel(s).",
                result);
        }

        return new HeadlessOutcome(ExitCodes.Success, "Désinstallation terminée.", result);
    }

    /// <summary>Répare une installation existante depuis sa distribution d'origine.</summary>
    public static async Task<HeadlessOutcome> RepairAsync(
        IInstallerEngine engine,
        MaintenanceRequest request,
        TextWriter output,
        bool simulated = false,
        CancellationToken ct = default)
    {
        var plan = await engine.PlanMaintenanceAsync(request, ct).ConfigureAwait(false);
        output.WriteLine($"Plan : {plan.Actions.Count} action(s) de réparation.");

        var install = new InstallRequest(
            request.Manifest ?? throw new ArgumentException("Une réparation exige la distribution d'origine.", nameof(request)),
            request.Record.InstallDir,
            request.Record.DataRoot,
            request.Record.WorkspacePath ?? Path.Combine(request.Record.DataRoot, "workspace"),
            Array.Empty<string>(),
            AcceptLicense: true,
            CreateDesktopShortcut: false,
            LaunchAfterInstall: false,
            DryRun: request.DryRun,
            OfflineAssetsDir: null,
            NoNetwork: false,
            PayloadRoot: request.PayloadRoot,
            Simulated: simulated);

        var result = await engine.ExecuteAsync(plan, install, ProgressTo(output), ct).ConfigureAwait(false);
        WriteResult(result, output);

        var exitCode = result.Success
            ? ExitCodes.Success
            : result.ExitCodeHint ?? ExitCodes.InstallFailed;

        return new HeadlessOutcome(exitCode, result.Success ? "Réparation terminée." : "Réparation en échec.", result);
    }

    /// <summary>Rend les contrôles préalables lisibles dans un terminal.</summary>
    public static void WriteChecks(PreflightResult preflight, TextWriter output)
    {
        foreach (var check in preflight.Checks)
        {
            var message = string.IsNullOrEmpty(check.Message) ? string.Empty : ": " + check.Message;
            output.WriteLine($"[{check.Status}] {check.Name}{message}");
        }

        foreach (var error in preflight.Errors)
        {
            output.WriteLine($"[Error] {error}");
        }
    }

    private static void WriteResult(ExecutionResult result, TextWriter output)
    {
        if (result.Simulated)
        {
            output.WriteLine("(monde fictif : aucune écriture réelle)");
        }

        foreach (var warning in result.Warnings)
        {
            output.WriteLine($"[Warn] {warning}");
        }

        foreach (var residual in result.Residuals)
        {
            output.WriteLine($"[Error] {residual}");
        }

        output.WriteLine($"Journal : {result.LogPath}");
    }

    private static IProgress<ProgressReport> ProgressTo(TextWriter output) =>
        new InlineProgress(output);

    private sealed class InlineProgress(TextWriter output) : IProgress<ProgressReport>
    {
        public void Report(ProgressReport value) =>
            output.WriteLine($"[{value.Current}/{value.Total}] {value.Kind ?? value.Step} {value.TargetPath}".TrimEnd());
    }
}
