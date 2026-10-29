using System.IO;

namespace Setup.Core.Platform.Linux;

public sealed class LinuxPathPolicy : IPathPolicy
{
    public bool IsWritable(string path)
    {
        try { Directory.CreateDirectory(path); var p = Path.Combine(path, ".livex-write-test"); File.WriteAllText(p, "x"); File.Delete(p); return true; }
        catch { return false; }
    }

    public bool IsForbiddenInstallDir(string path)
    {
        var full = Path.GetFullPath(path);
        if (full == "/" || full == Path.GetPathRoot(full)) return true;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (full == home) return true;
        if (full.StartsWith("/usr", StringComparison.Ordinal) || full.StartsWith("/etc", StringComparison.Ordinal)) return true;
        return false;
    }

    public bool Overlaps(string a, string b)
    {
        var fa = Path.GetFullPath(a).TrimEnd('/');
        var fb = Path.GetFullPath(b).TrimEnd('/');
        return fa == fb || fa.StartsWith(fb + "/", StringComparison.Ordinal) || fb.StartsWith(fa + "/", StringComparison.Ordinal);
    }

    public string DefaultInstallDir() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "opt", "livex");

    public string DefaultDataRoot()
    {
        var env = Environment.GetEnvironmentVariable("LIVEX_DATA");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".livex-data");
    }

    public string StartMenuShortcutPath(string name)
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "applications", name.ToLowerInvariant() + ".desktop");

    public string DesktopShortcutPath(string name)
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop",
            name.ToLowerInvariant() + ".desktop");
}