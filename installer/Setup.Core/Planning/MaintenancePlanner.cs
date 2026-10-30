// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using Setup.Core.Distribution;
using Setup.Core.Lifecycle;

namespace Setup.Core.Planning;

/// <summary>Action de maintenance disponible sur une installation existante.</summary>
public enum MaintenanceOperation
{
    Update,
    Repair,
    ManageComponents,
    ChangeWorkspace
}

/// <summary>
/// Paramètres d'une maintenance. <see cref="Record"/> décrit l'installation
/// existante ; <see cref="Manifest"/> est la distribution d'origine ou la
/// nouvelle distribution, selon l'opération.
/// </summary>
public sealed record MaintenanceRequest(
    InstallationRecord Record,
    MaintenanceOperation Operation,
    DistributionManifest? Manifest = null,
    IReadOnlyList<string>? AddComponents = null,
    IReadOnlyList<string>? RemoveComponents = null,
    string? NewWorkspacePath = null,
    IReadOnlyList<FileEntry>? Inventory = null,
    string? PayloadRoot = null,
    bool DryRun = true);

/// <summary>
/// Plans de maintenance. Aucun plan ne touche à un composant obligatoire, et
/// le changement d'espace de travail ne déplace ni ne supprime l'ancien.
/// </summary>
public static class MaintenancePlanner
{
    public static InstallPlan Plan(MaintenanceRequest request)
    {
        var actions = new List<PlanAction>();
        var warnings = new List<string>();
        var step = 0;
        long bytesToWrite = 0;
        long bytesToDownload = 0;

        switch (request.Operation)
        {
            case MaintenanceOperation.Update:
                PlanUpdate(request, actions, warnings, ref step, ref bytesToWrite);
                break;

            case MaintenanceOperation.Repair:
                PlanRepair(request, actions, warnings, ref step, ref bytesToWrite);
                break;

            case MaintenanceOperation.ManageComponents:
                foreach (var id in request.AddComponents ?? Array.Empty<string>())
                    PlanAdd(request, id, actions, warnings, ref step, ref bytesToWrite, ref bytesToDownload);

                foreach (var id in request.RemoveComponents ?? Array.Empty<string>())
                    PlanRemove(request, id, actions, warnings, ref step);
                break;

            case MaintenanceOperation.ChangeWorkspace:
                PlanWorkspace(request, actions, warnings, ref step);
                break;
        }

        return new InstallPlan(
            InstallOperation.Maintenance,
            actions,
            Array.Empty<string>(),
            bytesToWrite,
            bytesToDownload,
            warnings,
            RequiresConfirmation: false);
    }

    // ------------------------------------------------------------- mise à jour

    private static void PlanUpdate(
        MaintenanceRequest request,
        List<PlanAction> actions,
        List<string> warnings,
        ref int step,
        ref long bytesToWrite)
    {
        var manifest = request.Manifest;
        if (manifest is null)
        {
            warnings.Add("Aucune distribution fournie : la mise à jour est impossible.");
            return;
        }

        if (ProductVersion.IsDowngrade(request.Record.Version, manifest.Version))
        {
            warnings.Add($"Mise à jour refusée : la version {manifest.Version} est antérieure à {request.Record.Version}.");
            return;
        }

        var installDir = request.Record.InstallDir;
        var installed = new HashSet<string>(
            (request.Inventory ?? Array.Empty<FileEntry>()).Select(entry => entry.Path),
            StringComparer.OrdinalIgnoreCase);

        // Les composants obligatoires sont toujours mis à jour ; un composant
        // facultatif n'est mis à jour que s'il est déjà installé.
        var components = manifest.Components
            .Where(component => component.Mandatory
                                || component.Files.Any(file => installed.Contains(file.Path)))
            .ToList();

        var updated = new List<FileEntry>();
        foreach (var file in components.SelectMany(c => c.Files).DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
        {
            var target = Path.Combine(installDir, file.Path);
            var source = request.PayloadRoot is null ? null : Path.Combine(request.PayloadRoot, file.Path);
            actions.Add(new ReplaceFileAction(target, source, file.Sha256, file.Size, ++step));
            bytesToWrite += file.Size;
            updated.Add(file);
        }

        actions.Add(new ReplaceFileAction(
            Path.Combine(installDir, ".livex-install.json"), null, null, null, ++step,
            Content: Serialize(request.Record with { Version = manifest.Version })));

        actions.Add(new ReplaceFileAction(
            Path.Combine(installDir, ".livex-distribution.json"), null, null, null, ++step,
            Content: Serialize(manifest)));

        actions.Add(new UpdateInventoryAction(
            Path.Combine(installDir, ".livex-inventory.json"),
            request.Record.Product,
            manifest.Version,
            updated,
            Array.Empty<string>(),
            ++step));

        actions.Add(new WriteRegistrationAction("HKCU", "LIVEX", installDir, ++step));
    }

    // ------------------------------------------------------------- réparation

    private static void PlanRepair(
        MaintenanceRequest request,
        List<PlanAction> actions,
        List<string> warnings,
        ref int step,
        ref long bytesToWrite)
    {
        var manifest = request.Manifest;
        if (manifest is null)
        {
            warnings.Add("La distribution d'origine est introuvable : la réparation ne peut pas restaurer les fichiers manquants.");
            return;
        }

        var inventory = request.Inventory ?? Array.Empty<FileEntry>();
        if (inventory.Count == 0)
        {
            warnings.Add("Inventaire absent : la réparation ne peut pas déterminer les fichiers à restaurer.");
            return;
        }

        var installDir = request.Record.InstallDir;
        var missing = inventory
            .Where(entry => request.DryRun || !File.Exists(Path.Combine(installDir, entry.Path)))
            .Take(request.DryRun ? 3 : int.MaxValue)
            .ToList();

        if (missing.Count == 0)
        {
            warnings.Add("Aucune réparation nécessaire : tous les fichiers inventoriés sont présents.");
            return;
        }

        foreach (var entry in missing)
        {
            var target = Path.Combine(installDir, entry.Path);
            var source = request.PayloadRoot is null ? null : Path.Combine(request.PayloadRoot, entry.Path);
            actions.Add(new WriteFileAction(target, source, entry.Sha256, entry.Size, ++step));
            bytesToWrite += entry.Size;
        }

        warnings.Add($"{missing.Count} fichier(s) seront restaurés depuis la distribution d'origine.");
    }

    // ------------------------------------------------------------- composants

    private static void PlanAdd(
        MaintenanceRequest request,
        string? componentId,
        List<PlanAction> actions,
        List<string> warnings,
        ref int step,
        ref long bytesToWrite,
        ref long bytesToDownload)
    {
        var manifest = request.Manifest;
        if (manifest is null)
        {
            warnings.Add("Aucune distribution fournie : l'ajout de composant est impossible.");
            return;
        }

        var component = manifest.Components.FirstOrDefault(c => c.Id == componentId);
        if (component is null)
        {
            warnings.Add($"Composant inconnu : {componentId}.");
            return;
        }

        if (component.Acquisition.Kind == "unavailable")
        {
            warnings.Add($"Composant indisponible : {component.Id} ({component.Acquisition.Reason}).");
            return;
        }

        var installDir = request.Record.InstallDir;
        var added = new List<FileEntry>();

        foreach (var file in component.Files)
        {
            var target = Path.Combine(installDir, file.Path);
            var source = request.PayloadRoot is null ? null : Path.Combine(request.PayloadRoot, file.Path);
            actions.Add(new WriteFileAction(target, source, file.Sha256, file.Size, ++step));
            bytesToWrite += file.Size;
            added.Add(file);
        }

        if (component.Acquisition.Kind == "release-asset" && component.Acquisition.Url is not null)
        {
            var asset = component.Acquisition.Asset ?? component.Id + ".zip";
            var sha = component.Acquisition.Sha256 ?? string.Empty;
            var size = component.Acquisition.Size ?? 0;
            bytesToDownload += size;

            if (component.Id == "prism-ldk")
            {
                var destination = Path.Combine(installDir, "plugins", "prism-ldk", asset);
                actions.Add(new CreateDirectoryAction(Path.GetDirectoryName(destination)!, ++step));
                actions.Add(new DownloadAssetAction(destination, component.Acquisition.Url, sha, size, ++step));
            }
            else
            {
                var cache = Path.Combine(request.Record.DataRoot, "cache", "downloads", sha);
                var destination = Path.Combine(installDir, "components", component.Id);
                actions.Add(new DownloadAssetAction(cache, component.Acquisition.Url, sha, size, ++step));
                actions.Add(new CreateDirectoryAction(destination, ++step));
                actions.Add(new ExtractArchiveAction(cache, destination, ++step));
            }
        }

        actions.Add(new UpdateInventoryAction(
            Path.Combine(installDir, ".livex-inventory.json"),
            request.Record.Product,
            request.Record.Version,
            added,
            Array.Empty<string>(),
            ++step));
    }

    private static void PlanRemove(
        MaintenanceRequest request,
        string? componentId,
        List<PlanAction> actions,
        List<string> warnings,
        ref int step)
    {
        var installDir = request.Record.InstallDir;
        var manifest = request.Manifest;
        var component = manifest?.Components.FirstOrDefault(c => c.Id == componentId);

        if (component is { Mandatory: true })
        {
            warnings.Add($"Le composant obligatoire « {component.Id} » ne peut pas être retiré.");
            return;
        }

        var removed = new List<string>();

        if (component is not null && component.Files.Count > 0)
        {
            foreach (var file in component.Files)
            {
                actions.Add(new DeleteFileAction(Path.Combine(installDir, file.Path), ++step));
                removed.Add(file.Path);
            }
        }
        else
        {
            // Sans manifeste : on retire les fichiers d'inventaire du composant.
            var inventory = request.Inventory ?? Array.Empty<FileEntry>();
            var prefix = $"components/{componentId}/";
            foreach (var file in inventory.Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                actions.Add(new DeleteFileAction(Path.Combine(installDir, file.Path), ++step));
                removed.Add(file.Path);
            }

            actions.Add(new DeleteDirectoryAction(
                Path.Combine(installDir, "components", componentId ?? string.Empty), ++step));
        }

        actions.Add(new UpdateInventoryAction(
            Path.Combine(installDir, ".livex-inventory.json"),
            request.Record.Product,
            request.Record.Version,
            Array.Empty<FileEntry>(),
            removed,
            ++step));
    }

    // ------------------------------------------------------------- espace de travail

    private static void PlanWorkspace(
        MaintenanceRequest request,
        List<PlanAction> actions,
        List<string> warnings,
        ref int step)
    {
        var newWorkspace = request.NewWorkspacePath;
        if (string.IsNullOrWhiteSpace(newWorkspace))
        {
            warnings.Add("Aucun nouvel espace de travail fourni.");
            return;
        }

        var previous = request.Record.WorkspacePath;
        if (!string.IsNullOrWhiteSpace(previous) &&
            string.Equals(Path.GetFullPath(previous), Path.GetFullPath(newWorkspace), StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("L'espace de travail est déjà celui demandé.");
            return;
        }

        actions.Add(new CreateDirectoryAction(newWorkspace, ++step));
        actions.Add(new MergeSettingsAction(
            Path.Combine(request.Record.DataRoot, "settings.json"),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["workspace"] = newWorkspace },
            ++step));

        actions.Add(new ReplaceFileAction(
            Path.Combine(request.Record.InstallDir, ".livex-install.json"),
            null,
            null,
            null,
            ++step,
            Content: Serialize(request.Record with { WorkspacePath = newWorkspace })));

        if (!string.IsNullOrWhiteSpace(previous))
        {
            warnings.Add($"L'ancien espace de travail « {previous} » est conservé tel quel : aucun fichier n'est déplacé ni supprimé.");
        }
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
}
