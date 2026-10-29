namespace Setup.Core.Platform.Linux;

public sealed class LinuxProcessProbe : IProcessProbe
{
    public bool IsProcessRunningFrom(string directory) => GetProcessIdsFrom(directory).Count > 0;

    public IReadOnlyList<int> GetProcessIdsFrom(string directory)
    {
        var result = new List<int>();
        var full = Path.GetFullPath(directory).TrimEnd('/');
        try
        {
            foreach (var pidDir in Directory.GetDirectories("/proc"))
            {
                var name = Path.GetFileName(pidDir);
                if (!int.TryParse(name, out var pid)) continue;
                try
                {
                    var exe = Path.Combine(pidDir, "exe");
                    if (File.Exists(exe))
                    {
                        var target = new FileInfo(exe).ResolveLinkTarget(true)?.FullName;
                        if (!string.IsNullOrEmpty(target) && target.StartsWith(full, StringComparison.Ordinal))
                            result.Add(pid);
                    }
                }
                catch { }
            }
        }
        catch { }
        return result;
    }
}