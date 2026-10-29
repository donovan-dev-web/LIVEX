using Microsoft.Win32;
using Setup.Core.Lifecycle;
using System.Text.Json;

namespace Setup.Core.Platform.Windows;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsInstallationStore : IInstallationStore
{
    private const string KeyPath = @"Software\LIVEX\Installer";

    public string StorePath => @"HKCU\" + KeyPath;

    public InstallationRecord? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (key is null) return null;
        var json = key.GetValue("Installation") as string;
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<InstallationRecord>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Write(InstallationRecord record)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        var json = JsonSerializer.Serialize(record);
        key.SetValue("Installation", json);
    }

    public void Delete()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(KeyPath); }
        catch { /* ignore */ }
    }
}