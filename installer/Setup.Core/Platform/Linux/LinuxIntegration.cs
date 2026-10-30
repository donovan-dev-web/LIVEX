// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

namespace Setup.Core.Platform.Linux;

public sealed class LinuxIntegration : IIntegration
{
    private const string MimeType = "application/x-livex-package";
    private const string AssociationDesktopFile = "livex-livexp.desktop";

    public void CreateStartMenuShortcut(string name, string targetPath, string? iconPath)
    {
        var apps = ApplicationsDirectory();
        Directory.CreateDirectory(apps);
        File.WriteAllText(Path.Combine(apps, "livex.desktop"), DesktopEntry(name, targetPath, iconPath, null));
    }

    public void CreateDesktopShortcut(string name, string targetPath, string? iconPath)
    {
        var desktop = DesktopDirectory();
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(desktop, "livex.desktop"), DesktopEntry(name, targetPath, iconPath, null));
    }

    public void RemoveShortcuts(string name)
    {
        TryDelete(Path.Combine(ApplicationsDirectory(), "livex.desktop"));
        TryDelete(Path.Combine(DesktopDirectory(), "livex.desktop"));
    }

    public void WriteUninstallEntry(string displayName, string version, string installDir, string uninstallCommand)
    {
        // Linux : l'entrée de désinstallation est portée par le .desktop et
        // l'enregistrement XDG, pas par une base système.
        var apps = ApplicationsDirectory();
        Directory.CreateDirectory(apps);
        var content = "[Desktop Entry]\n" +
                      "Type=Application\n" +
                      $"Name={displayName}\n" +
                      $"Exec={uninstallCommand}\n" +
                      "Terminal=false\n" +
                      "Categories=Utility;\n";
        File.WriteAllText(Path.Combine(apps, "livex-uninstall.desktop"), content);
    }

    public void RemoveUninstallEntry() => TryDelete(Path.Combine(ApplicationsDirectory(), "livex-uninstall.desktop"));

    public void AssociatePackage(string launcherPath, string? iconPath)
    {
        // Déclaration MIME utilisateur (aucune écriture système, pas de sudo).
        var mimePackages = Path.Combine(ShareDirectory(), "mime", "packages");
        Directory.CreateDirectory(mimePackages);
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                  "<mime-info xmlns=\"http://www.freedesktop.org/standards/shared-mime-info\">\n" +
                  $"  <mime-type type=\"{MimeType}\">\n" +
                  "    <comment>LIVEX campaign package</comment>\n" +
                  "    <comment xml:lang=\"fr\">Paquet de campagne LIVEX</comment>\n" +
                  "    <glob pattern=\"*.livexp\"/>\n" +
                  "  </mime-type>\n" +
                  "</mime-info>\n";
        File.WriteAllText(Path.Combine(mimePackages, "livex-livexp.xml"), xml);

        var apps = ApplicationsDirectory();
        Directory.CreateDirectory(apps);
        File.WriteAllText(Path.Combine(apps, AssociationDesktopFile),
            DesktopEntry("Ouvrir un paquet LIVEX", launcherPath, iconPath, MimeType));

        // Rafraîchit la base MIME utilisateur si l'outil est présent (best effort).
        TryRun("update-mime-database", Path.Combine(ShareDirectory(), "mime"));
        TryRun("update-desktop-database", apps);
    }

    public void RemovePackageAssociation()
    {
        TryDelete(Path.Combine(ShareDirectory(), "mime", "packages", "livex-livexp.xml"));
        TryDelete(Path.Combine(ApplicationsDirectory(), AssociationDesktopFile));
        TryRun("update-mime-database", Path.Combine(ShareDirectory(), "mime"));
    }

    private static string DesktopEntry(string name, string targetPath, string? iconPath, string? mimeType)
    {
        var icon = string.IsNullOrWhiteSpace(iconPath) ? "livex" : iconPath;
        var mime = string.IsNullOrWhiteSpace(mimeType) ? string.Empty : $"MimeType={mimeType};\n";
        return "[Desktop Entry]\n" +
               "Type=Application\n" +
               $"Name={name}\n" +
               $"Exec=\"{targetPath}\" %f\n" +
               $"Icon={icon}\n" +
               "Terminal=false\n" +
               "Categories=Utility;\n" +
               mime;
    }

    private static string HomeDirectory() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string ShareDirectory()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return !string.IsNullOrWhiteSpace(xdg) ? xdg : Path.Combine(HomeDirectory(), ".local", "share");
    }

    private static string ApplicationsDirectory() => Path.Combine(ShareDirectory(), "applications");

    private static string DesktopDirectory() => Path.Combine(HomeDirectory(), "Desktop");

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignoré */ }
    }

    private static void TryRun(string executable, string argument)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable, argument)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            process?.WaitForExit(5_000);
        }
        catch
        {
            // Outil absent : l'association reste déclarée, le bureau la
            // prendra en compte au prochain rafraîchissement.
        }
    }
}
