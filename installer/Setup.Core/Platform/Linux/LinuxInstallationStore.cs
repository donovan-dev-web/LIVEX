using Setup.Core.Lifecycle;
using System.Text.Json;

namespace Setup.Core.Platform.Linux;

public sealed class LinuxInstallationStore : IInstallationStore
{
    private readonly string _path;

    public LinuxInstallationStore(string dataRoot)
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var baseDir = !string.IsNullOrWhiteSpace(xdg) ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        var dir = Path.Combine(baseDir, "livex", "installer");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "installation.json");
    }

    public string StorePath => _path;

    public InstallationRecord? Read()
    {
        if (!File.Exists(_path)) return null;
        try { return JsonSerializer.Deserialize<InstallationRecord>(File.ReadAllText(_path)); }
        catch { return null; }
    }

    public void Write(InstallationRecord record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(record));
    }

    public void Delete()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }
}