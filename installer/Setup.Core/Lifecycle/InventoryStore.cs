// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using System.Text.Json;
using Setup.Core.Distribution;

namespace Setup.Core.Lifecycle;

/// <summary>
/// Lecture et écriture de l'inventaire installé
/// (<c>.livex-inventory.json</c>). Le fichier est facultatif : son absence est
/// un état géré, jamais une erreur.
/// </summary>
public static class InventoryStore
{
    public const string FileName = ".livex-inventory.json";

    public static InstallationInventory? Read(string installDir)
    {
        var path = PathFor(installDir);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<InstallationInventory>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Entrées d'inventaire, ou liste vide si le fichier est absent ou illisible.</summary>
    public static IReadOnlyList<FileEntry> ReadEntries(string installDir) =>
        Read(installDir)?.Files
            .Select(entry => new FileEntry(entry.Path, entry.Size, entry.Sha256))
            .ToArray() ?? Array.Empty<FileEntry>();

    public static void Write(string installDir, InstallationInventory inventory)
    {
        var path = PathFor(installDir);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    public static string PathFor(string installDir) => Path.Combine(installDir, FileName);
}
