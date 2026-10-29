// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Platform;

/// <summary>
/// Intégration système simulée : enregistre les appels sans rien écrire.
/// Le mode démonstration l'utilise pour rester strictement sans effet de bord.
/// </summary>
public sealed class SimulatedIntegration : IIntegration
{
    private readonly List<string> _calls = new();

    /// <summary>Trace lisible des intégrations demandées, pour l'écran de résultat.</summary>
    public IReadOnlyList<string> Calls
    {
        get { lock (_calls) { return _calls.ToArray(); } }
    }

    private void Record(string call)
    {
        lock (_calls) { _calls.Add(call); }
    }

    public void CreateStartMenuShortcut(string name, string targetPath, string? iconPath) =>
        Record($"menu:{name}");

    public void CreateDesktopShortcut(string name, string targetPath, string? iconPath) =>
        Record($"bureau:{name}");

    public void RemoveShortcuts(string name) => Record($"raccourcis-supprimes:{name}");

    public void WriteUninstallEntry(string displayName, string version, string installDir, string uninstallCommand) =>
        Record($"desinstallation:{displayName}");

    public void RemoveUninstallEntry() => Record("desinstallation-supprimee");

    public void AssociatePackage(string launcherPath, string? iconPath) => Record("association:.livexp");

    public void RemovePackageAssociation() => Record("association-supprimee:.livexp");
}
