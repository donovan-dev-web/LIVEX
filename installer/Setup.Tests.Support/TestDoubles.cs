// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Execution;
using Setup.Core.Platform;

namespace Setup.Tests.Support;

/// <summary>
/// Progression synchrone : contrairement à <see cref="Progress{T}"/>, elle
/// n'utilise pas de contexte de synchronisation et ne perd donc aucun rapport
/// en fin d'exécution.
/// </summary>
public sealed class CollectingProgress : IProgress<ProgressReport>
{
    public List<ProgressReport> Reports { get; } = new();

    public void Report(ProgressReport value) => Reports.Add(value);
}

/// <summary>Politique de chemins déterministe, sans accès disque.</summary>
public sealed class FakePathPolicy : IPathPolicy
{
    private readonly List<string> _forbidden = new();
    private readonly List<string> _writable = new();

    public string InstallDir { get; set; } = Path.Combine(Path.GetTempPath(), "livex-tests", "app");
    public string DataDir { get; set; } = Path.Combine(Path.GetTempPath(), "livex-tests", "data");
    public string ShortcutDir { get; set; } = Path.Combine(Path.GetTempPath(), "livex-tests", "startmenu");
    public string DesktopDir { get; set; } = Path.Combine(Path.GetTempPath(), "livex-tests", "desktop");

    public bool WritableByDefault { get; set; } = true;

    public FakePathPolicy Forbid(string path)
    {
        _forbidden.Add(path);
        return this;
    }

    public FakePathPolicy NotWritable(string path)
    {
        _writable.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        return this;
    }

    public bool IsWritable(string path) => WritableByDefault;

    public bool IsForbiddenInstallDir(string path) =>
        _forbidden.Any(f => string.Equals(Path.GetFullPath(f), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));

    public bool Overlaps(string a, string b)
    {
        var fa = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fb = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fa.Equals(fb, StringComparison.OrdinalIgnoreCase)
               || fa.StartsWith(fb + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || fb.StartsWith(fa + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public string DefaultInstallDir() => InstallDir;

    public string DefaultDataRoot() => DataDir;

    public string StartMenuShortcutPath(string name) => Path.Combine(ShortcutDir, name + ".lnk");

    public string DesktopShortcutPath(string name) => Path.Combine(DesktopDir, name + ".lnk");
}

public sealed class FakeProcessProbe : IProcessProbe
{
    public bool Running { get; set; }

    public bool IsProcessRunningFrom(string directory) => Running;

    public IReadOnlyList<int> GetProcessIdsFrom(string directory) => Running ? new[] { 4242 } : Array.Empty<int>();
}

public sealed class FakePrerequisiteProbe : IPrerequisiteProbe
{
    public IReadOnlyList<string> Issues { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Check() => Issues;
}

public sealed class NoopFileSync : IFileSync
{
    public void Flush(string path)
    {
        // rien à faire
    }
}
