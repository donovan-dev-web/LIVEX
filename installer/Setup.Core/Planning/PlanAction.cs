// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Planning;

/// <summary>
/// Action atomique d'un plan. Le plan est calculé avant toute écriture :
/// une action ne découvre jamais ce qu'elle doit faire.
/// </summary>
public abstract record PlanAction(
    string TargetPath,
    int Step,
    bool IsDestructive);

public sealed record CreateDirectoryAction(string TargetPath, int Step, bool IsDestructive = false)
    : PlanAction(TargetPath, Step, IsDestructive);

public sealed record WriteFileAction(
    string TargetPath,
    string? SourcePath,
    string? ExpectedSha256,
    long? ExpectedSize,
    int Step,
    bool IsDestructive = false,
    string? Content = null)
    : PlanAction(TargetPath, Step, IsDestructive);

public sealed record ReplaceFileAction(
    string TargetPath,
    string? SourcePath,
    string? ExpectedSha256,
    long? ExpectedSize,
    int Step,
    bool IsDestructive = true,
    string? Content = null)
    : PlanAction(TargetPath, Step, IsDestructive);

public sealed record DeleteFileAction(string TargetPath, int Step, bool IsDestructive = true)
    : PlanAction(TargetPath, Step, IsDestructive);

public sealed record DeleteDirectoryAction(string TargetPath, int Step, bool Recursive = true, bool IsDestructive = true)
    : PlanAction(TargetPath, Step, IsDestructive);

public sealed record DownloadAssetAction(
    string TargetPath,
    string Url,
    string ExpectedSha256,
    long ExpectedSize,
    int Step,
    bool IsDestructive = false)
    : PlanAction(TargetPath, Step, IsDestructive);

public sealed record ExtractArchiveAction(
    string ArchivePath,
    string DestinationDirectory,
    int Step,
    bool IsDestructive = false)
    : PlanAction(DestinationDirectory, Step, IsDestructive);

public sealed record CreateShortcutAction(
    string ShortcutPath,
    string TargetExePath,
    bool IsDesktop,
    int Step,
    bool IsDestructive = false)
    : PlanAction(ShortcutPath, Step, IsDestructive);

public sealed record WriteRegistrationAction(
    string Location,
    string Key,
    string Value,
    int Step,
    bool IsDestructive = false)
    : PlanAction(Location, Step, IsDestructive);

public sealed record RemoveRegistrationAction(
    string Location,
    string Key,
    int Step,
    bool IsDestructive = true)
    : PlanAction(Location, Step, IsDestructive);

public sealed record RemoveShortcutsAction(
    string Name,
    int Step,
    bool IsDestructive = true)
    : PlanAction(Name, Step, IsDestructive);

public sealed record AssociatePackageAction(
    string LauncherPath,
    string? IconPath,
    int Step,
    bool IsDestructive = false)
    : PlanAction(".livexp", Step, IsDestructive);

public sealed record RemovePackageAssociationAction(int Step, bool IsDestructive = true)
    : PlanAction(".livexp", Step, IsDestructive);

public sealed record MergeSettingsAction(
    string SettingsPath,
    IReadOnlyDictionary<string, object?> Changes,
    int Step,
    bool IsDestructive = false)
    : PlanAction(SettingsPath, Step, IsDestructive);

public sealed record RemoveSettingsAction(string SettingsPath, int Step, bool IsDestructive = true)
    : PlanAction(SettingsPath, Step, IsDestructive);

/// <summary>
/// Fusionne l'inventaire installé : ajoute les fichiers d'un composant ou en
/// retire ceux d'un composant supprimé, sans perdre le reste.
/// </summary>
public sealed record UpdateInventoryAction(
    string InventoryPath,
    string Product,
    string Version,
    IReadOnlyList<Distribution.FileEntry> Added,
    IReadOnlyList<string> Removed,
    int Step,
    bool IsDestructive = false)
    : PlanAction(InventoryPath, Step, IsDestructive);

/// <summary>
/// Clés de progression stables (« write-file », « extract »…) traduites par
/// l'interface. Aucune chaîne d'interface dans le noyau.
/// </summary>
public static class PlanActionLabel
{
    public static string Of(PlanAction action) => action switch
    {
        CreateDirectoryAction => "create-directory",
        WriteFileAction => "write-file",
        ReplaceFileAction => "replace-file",
        DeleteFileAction => "delete-file",
        DeleteDirectoryAction => "delete-directory",
        DownloadAssetAction => "download",
        ExtractArchiveAction => "extract",
        CreateShortcutAction => "shortcut",
        WriteRegistrationAction => "register",
        RemoveRegistrationAction => "unregister",
        RemoveShortcutsAction => "remove-shortcuts",
        AssociatePackageAction => "associate-package",
        RemovePackageAssociationAction => "remove-association",
        MergeSettingsAction => "settings",
        RemoveSettingsAction => "remove-settings",
        UpdateInventoryAction => "inventory",
        _ => "unknown"
    };
}
