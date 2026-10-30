using System.IO;

namespace Setup.Core.Platform.Windows;

public sealed class WindowsPathPolicy : IPathPolicy
{
    public bool IsWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".livex-write-test");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsForbiddenInstallDir(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) return true;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.Equals(full, home, StringComparison.OrdinalIgnoreCase)) return true;
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (full.StartsWith(sys, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public bool Overlaps(string a, string b)
    {
        var fa = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fb = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fa.Equals(fb, StringComparison.OrdinalIgnoreCase)
            || fa.StartsWith(fb + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fb.StartsWith(fa + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public string DefaultInstallDir()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LIVEX");

    public string DefaultDataRoot()
    {
        var env = Environment.GetEnvironmentVariable("LIVEX_DATA");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".livex-data");
    }

    public string StartMenuShortcutPath(string name)
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "LIVEX", name + ".lnk");

    public string DesktopShortcutPath(string name)
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name + ".lnk");
}