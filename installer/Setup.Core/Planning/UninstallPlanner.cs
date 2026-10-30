// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Distribution;
using Setup.Core.Lifecycle;

namespace Setup.Core.Planning;

/// <summary>Portée de désinstallation (spec §10.3 / ARCHITECTURE §11).</summary>
public enum UninstallScope
{
    /// <summary>Logiciel et intégrations ; expériences et préférences conservées.</summary>
    Software,

    /// <summary>Logiciel + configuration et cache ; expériences conservées.</summary>
    SoftwareAndConfig,

    /// <summary>Logiciel + configuration + espace de travail confirmé.</summary>
    SoftwareConfigAndWorkspace
}

/// <summary>
/// Paramètres d'une désinstallation. <see cref="WorkspaceDeletionConfirmed"/>
/// doit être vrai pour que la portée C produise une action destructive sur
/// l'espace de travail.
/// </summary>
public sealed record UninstallRequest(
    InstallationRecord Record,
    UninstallScope Scope,
    IReadOnlyList<FileEntry> Inventory,
    bool WorkspaceDeletionConfirmed = false,
    bool PurgeResearch = false,
    bool DryRun = true,
    int SimulationDelayMs = 45);

/// <summary>Plan de désinstallation, calculé avant toute suppression.</summary>
public sealed record UninstallPlan(
    UninstallScope Scope,
    IReadOnlyList<PlanAction> Actions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Residuals,
    string? WorkspacePath,
    bool RequiresConfirmation,
    int FileCount);

/// <summary>
/// Construit le plan de désinstallation. Aucune suppression n'est effectuée
/// ici : le plan est présenté, confirmé, puis exécuté.
/// </summary>
public static class UninstallPlanner
{
    public static UninstallPlan Plan(UninstallRequest request)
    {
        var actions = new List<PlanAction>();
        var warnings = new List<string>();
        var residuals = new List<string>();
        var step = 0;
        var record = request.Record;
        var installDir = record.InstallDir;

        // 1. Intégrations : association, raccourcis, entrée système.
        actions.Add(new RemovePackageAssociationAction(++step));
        actions.Add(new RemoveShortcutsAction("LIVEX", ++step));
        actions.Add(new RemoveRegistrationAction("HKCU", "LIVEX", ++step));

        // 2. Fichiers du logiciel, depuis l'inventaire.
        var files = request.Inventory
            .Select(entry => entry.Path)
            .Where(path => !ManifestValidator.PathIssues(path).Any())
            .Select(path => Path.Combine(installDir, path))
            .ToList();

        if (files.Count == 0)
        {
            warnings.Add("Inventaire absent : seuls les répertoires connus de l'installateur seront traités ; des fichiers du Launcher peuvent subsister.");
        }

        foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            actions.Add(new DeleteFileAction(file, ++step));
        }

        // Fichiers d'identité et manifeste d'origine, toujours traités.
        foreach (var name in new[] { ".livex-install.json", ".livex-inventory.json", ".livex-distribution.json" })
        {
            var path = Path.Combine(installDir, name);
            if (!files.Contains(path, StringComparer.OrdinalIgnoreCase))
                actions.Add(new DeleteFileAction(path, ++step));
        }

        // 3. Répertoires connus, du plus profond au plus haut.
        var knownDirectories = new[]
        {
            Path.Combine(installDir, "plugins", "prism-ldk"),
            Path.Combine(installDir, "plugins"),
            Path.Combine(installDir, "components"),
            Path.Combine(installDir, "contracts"),
            Path.Combine(installDir, "docs"),
            Path.Combine(installDir, "assets"),
            installDir
        };

        foreach (var directory in knownDirectories)
        {
            actions.Add(new DeleteDirectoryAction(directory, ++step, Recursive: false, IsDestructive: false));
        }

        // 4. Portée B : configuration et cache.
        if (request.Scope is UninstallScope.SoftwareAndConfig or UninstallScope.SoftwareConfigAndWorkspace)
        {
            actions.Add(new RemoveSettingsAction(Path.Combine(record.DataRoot, "settings.json"), ++step));
            actions.Add(new DeleteDirectoryAction(Path.Combine(record.DataRoot, "cache"), ++step));
        }
        else
        {
            warnings.Add("Le cache de téléchargement est conservé (portée A) : une réinstallation ne retéléchargera pas les modules optionnels.");
        }

        // 5. Portée C : espace de travail, seulement après confirmation explicite.
        string? workspace = record.WorkspacePath;
        var workspaceDeletionAllowed = false;
        if (request.Scope == UninstallScope.SoftwareConfigAndWorkspace)
        {
            if (string.IsNullOrWhiteSpace(workspace))
            {
                warnings.Add("Aucun espace de travail enregistré : aucune donnée d'expérience n'a été supprimée.");
            }
            else if (!request.WorkspaceDeletionConfirmed)
            {
                warnings.Add($"Suppression de l'espace de travail « {workspace} » non confirmée : les expériences sont conservées.");
            }
            else if (IsUnsafeWorkspace(workspace, record))
            {
                residuals.Add($"Espace de travail refusé car trop proche d'un répertoire système : {workspace}");
            }
            else
            {
                workspaceDeletionAllowed = true;
                actions.Add(new DeleteDirectoryAction(workspace, ++step));
            }
        }

        // 6. Extension « résultats de recherche », après seconde confirmation.
        if (request.PurgeResearch)
        {
            foreach (var name in new[] { "packages", "archives", "sessions", "echos" })
            {
                actions.Add(new DeleteDirectoryAction(Path.Combine(record.DataRoot, name), ++step));
            }
        }

        // Les journaux ne sont jamais supprimés par une portée : ils servent au
        // diagnostic d'un échec, même après désinstallation (spec §10.5).
        var requiresConfirmation = (request.Scope == UninstallScope.SoftwareConfigAndWorkspace && workspaceDeletionAllowed)
                                   || request.PurgeResearch;

        return new UninstallPlan(
            request.Scope,
            actions,
            warnings,
            residuals,
            workspace,
            requiresConfirmation,
            files.Count);
    }

    /// <summary>
    /// Refuse un espace de travail qui est la racine d'un volume, le répertoire
    /// personnel ou la racine de données : une suppression récursive y serait
    /// destructrice au-delà du périmètre confirmé.
    /// </summary>
    private static bool IsUnsafeWorkspace(string workspace, InstallationRecord record)
    {
        if (string.IsNullOrWhiteSpace(workspace)) return true;
        var full = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.Length == 0) return true;
        if (string.Equals(full, Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(full, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(full, Path.GetFullPath(record.DataRoot).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
