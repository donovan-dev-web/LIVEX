using System.Diagnostics;

namespace Setup.Core.Platform.Windows;

public sealed class WindowsProcessProbe : IProcessProbe
{
    public bool IsProcessRunningFrom(string directory)
        => GetProcessIdsFrom(directory).Count > 0;

    public IReadOnlyList<int> GetProcessIdsFrom(string directory)
    {
        var result = new List<int>();
        var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = p.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path) && path.StartsWith(full, StringComparison.OrdinalIgnoreCase))
                    result.Add(p.Id);
            }
            catch
            {
                // accès refusé : ignorer
            }
        }
        return result;
    }
}