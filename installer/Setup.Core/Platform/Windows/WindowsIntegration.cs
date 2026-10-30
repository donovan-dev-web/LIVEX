// SPDX-License-Identifier: GPL-3.0-or-later
using Microsoft.Win32;

namespace Setup.Core.Platform.Windows;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsIntegration : IIntegration
{
    private const string PackageProgId = "LIVEX.Package";
    private const string PackageExtension = ".livexp";

    public void CreateStartMenuShortcut(string name, string targetPath, string? iconPath)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "LIVEX");
        Directory.CreateDirectory(dir);
        var lnk = Path.Combine(dir, name + ".lnk");
        CreateLnk(lnk, targetPath, iconPath);
    }

    public void CreateDesktopShortcut(string name, string targetPath, string? iconPath)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var lnk = Path.Combine(desktop, name + ".lnk");
        CreateLnk(lnk, targetPath, iconPath);
    }

    public void RemoveShortcuts(string name)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "LIVEX");
        TryDelete(Path.Combine(dir, name + ".lnk"));
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name + ".lnk"));
    }

    public void WriteUninstallEntry(string displayName, string version, string installDir, string uninstallCommand)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LIVEX");
        key.SetValue("DisplayName", displayName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("InstallLocation", installDir);
        key.SetValue("UninstallString", uninstallCommand);
        key.SetValue("NoModify", 1);
        key.SetValue("NoRepair", 1);
    }

    public void RemoveUninstallEntry()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LIVEX"); }
        catch { /* déjà absent */ }
    }

    public void AssociatePackage(string launcherPath, string? iconPath)
    {
        // Extension -> ProgID, en HKCU uniquement (aucune élévation).
        using (var ext = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{PackageExtension}"))
        {
            ext.SetValue(null, PackageProgId);
            using var openWith = ext.CreateSubKey("OpenWithProgids");
            openWith.SetValue(PackageProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        using var progId = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{PackageProgId}");
        progId.SetValue(null, "Paquet de campagne LIVEX");
        using (var icon = progId.CreateSubKey("DefaultIcon"))
        {
            icon.SetValue(null, string.IsNullOrEmpty(iconPath) ? launcherPath : $"{iconPath},0");
        }
        using (var command = progId.CreateSubKey(@"shell\open\command"))
        {
            command.SetValue(null, $"\"{launcherPath}\" \"%1\"");
        }
    }

    public void RemovePackageAssociation()
    {
        // Ne supprime que ce que l'installateur a créé : l'extension n'est
        // retirée que si elle pointe encore vers notre ProgID.
        try
        {
            using var ext = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{PackageExtension}", writable: true);
            if (ext is not null && string.Equals(ext.GetValue(null) as string, PackageProgId, StringComparison.Ordinal))
                ext.DeleteValue(string.Empty, throwOnMissingValue: false);
        }
        catch { /* ignoré */ }

        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{PackageProgId}"); }
        catch { /* déjà absent */ }
    }

    private static void CreateLnk(string path, string target, string? icon)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type is null) return;
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic sc = shell.CreateShortcut(path);
        sc.TargetPath = target;
        if (!string.IsNullOrEmpty(icon)) sc.IconLocation = icon;
        sc.Save();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignoré */ }
    }
}
