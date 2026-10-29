// SPDX-License-Identifier: GPL-3.0-or-later
namespace Setup.Core.Platform;

public interface IPathPolicy
{
    bool IsWritable(string path);
    bool IsForbiddenInstallDir(string path);
    bool Overlaps(string a, string b);
    string DefaultInstallDir();
    string DefaultDataRoot();

    /// <summary>Chemin du raccourci du menu des applications.</summary>
    string StartMenuShortcutPath(string name);

    /// <summary>Chemin du raccourci du bureau.</summary>
    string DesktopShortcutPath(string name);
}
