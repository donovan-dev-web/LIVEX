using Launcher.Protocol;

namespace Launcher.Package;

/// <summary>
/// Règles de nom d'entrée du paquet <c>.livexp</c> (PACKAGE_FORMAT.md §8), appliquées à
/// l'écriture comme à la lecture : chemin relatif uniquement, aucun « .. », aucun lecteur,
/// aucune extension exécutable.
///
/// <para>Une seule définition pour les deux sens du flux : si l'écrivain acceptait ce que le
/// lecteur refuse, un paquet s'écrirait puis deviendrait illisible ; si le lecteur acceptait
/// ce que l'écrivain refuse, la contre-mesure §8 ne serait qu'une formalité à l'écriture.
/// Les noms produits par un composant externe (analyse ECHOS, données d'un run) passent par
/// la même règle : ils ne décident pas du chemin écrit dans le paquet.</para>
/// </summary>
public static class PackageEntryRules
{
    /// <summary>Extensions exécutable : aucune entrée portant l'une d'elles n'est admise (§8).</summary>
    private static readonly string[] ExecutableExtensions =
    {
        ".exe", ".dll", ".so", ".dylib", ".bat", ".cmd", ".sh", ".ps1", ".msi", ".com", ".scr", ".bin",
    };

    /// <summary>Vrai si le nom d'entrée respecte les règles du format.</summary>
    public static bool IsSafe(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName)
            || Path.IsPathRooted(entryName)
            || entryName.Contains("..", StringComparison.Ordinal)
            || entryName.Contains(':')
            || entryName.StartsWith('/')
            || entryName.StartsWith('\\'))
        {
            return false;
        }

        return !ExecutableExtensions.Contains(Path.GetExtension(entryName), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Refuse un nom d'entrée non conforme (PACKAGE_FORMAT.md §8).</summary>
    /// <exception cref="UnsafeEntryPathException">Le nom viole une règle du format.</exception>
    public static void EnsureSafe(string entryName)
    {
        if (!IsSafe(entryName))
        {
            throw new UnsafeEntryPathException(entryName);
        }
    }
}
