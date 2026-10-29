namespace Setup.Core.Distribution;

/// <summary>
/// Fichier déclaré par le manifeste de distribution.
/// </summary>
public sealed record FileEntry(
    string Path,
    long Size,
    string Sha256);