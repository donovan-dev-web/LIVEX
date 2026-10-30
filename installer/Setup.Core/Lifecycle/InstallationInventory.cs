// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Distribution;

namespace Setup.Core.Lifecycle;

/// <summary>Entrée d'inventaire : chemin relatif au répertoire logiciel.</summary>
public sealed record InventoryEntry(string Path, long Size, string Sha256);

/// <summary>
/// Inventaire des fichiers installés (.livex-inventory.json). Il permet la
/// réparation, la désinstallation ciblée et le signalement des résidus.
/// </summary>
public sealed record InstallationInventory(
    string Product,
    string Version,
    string InstallId,
    DateTimeOffset InstalledAt,
    IReadOnlyList<InventoryEntry> Files)
{
    public static InstallationInventory Create(
        string product,
        string version,
        string installId,
        IEnumerable<FileEntry> files) =>
        new(product, version, installId, DateTimeOffset.UtcNow,
            files.Select(f => new InventoryEntry(f.Path, f.Size, f.Sha256)).ToArray());
}
