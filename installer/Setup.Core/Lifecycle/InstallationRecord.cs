namespace Setup.Core.Lifecycle;

/// <summary>
/// Identité d'une installation (.livex-install.json).
/// </summary>
public sealed record InstallationRecord(
    string Product,
    string Version,
    string InstallDir,
    string DataRoot,
    string? WorkspacePath,
    DateTimeOffset InstalledAt);