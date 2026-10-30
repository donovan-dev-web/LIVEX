// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Platform;

public interface IIntegration
{
    void CreateStartMenuShortcut(string name, string targetPath, string? iconPath);
    void CreateDesktopShortcut(string name, string targetPath, string? iconPath);
    void RemoveShortcuts(string name);
    void WriteUninstallEntry(string displayName, string version, string installDir, string uninstallCommand);
    void RemoveUninstallEntry();

    /// <summary>
    /// Associe l'extension de paquet de campagne (.livexp) au lanceur et à son
    /// icône. Écart assumé à la spec §8.3, décidé par l'architecte le 29/10/2026.
    /// </summary>
    void AssociatePackage(string launcherPath, string? iconPath);

    /// <summary>Retire l'association créée par l'installateur, et elle seule.</summary>
    void RemovePackageAssociation();
}
