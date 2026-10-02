using Launcher.Domain.Model;
using Launcher.Infrastructure;
using Launcher.Package;
using Launcher.Protocol;
using Launcher.Protocol.Model;

namespace Launcher.App.Composition;

/// <summary>Résultat d'une vérification d'environnement (PACKAGING.md §6).</summary>
public sealed record EnvironmentCheck(string Name, bool Passed, string Detail);

/// <summary>
/// Commande --check : diagnostic sans interface, sortie textuelle et stable,
/// citable dans un rapport d'incident. N'écrit aucun journal de paquet.
/// Les huit vérifications de PACKAGING.md §6, dans l'ordre du tableau ;
/// le navigateur est une information non bloquant : il ne modifie jamais le code de sortie.
/// </summary>
public static class EnvironmentChecker
{
    /// <summary>Seuil d'espace disque libre par défaut, en octets (1 Gio).</summary>
    private const long MinFreeDiskBytes = 1L * 1024 * 1024 * 1024;

    /// <summary>Exécute toutes les vérifications sur les emplacements par défaut.</summary>
    public static IReadOnlyList<EnvironmentCheck> Run() => Run(null, null);

    /// <summary>Exécute toutes les vérifications avec des racines explicites (bancs de tests hermétiques).</summary>
    /// <param name="dataRoot">Racine des données utilisateur ; nulle pour LIVEX_DATA ou le profil utilisateur.</param>
    /// <param name="componentRoots">Racines de détection des composants ; nulles pour les sources standard.</param>
    public static IReadOnlyList<EnvironmentCheck> Run(string? dataRoot, IReadOnlyList<string>? componentRoots)
    {
        dataRoot ??= WorkspaceLayout.UserDataRoot();
        var installations = Detect(dataRoot, componentRoots);

        return
        [
            CheckExecution(),
            CheckDataDirectoryWritable(dataRoot),
            CheckFreeDiskSpace(dataRoot),
            CheckPackageFormat(dataRoot),
            CheckComponents(installations),
            CheckEngine(installations),
            CheckPorts(installations),
            CheckBrowser(),
        ];
    }

    /// <summary>Ligne de sortie stable : marqueur, nom, détail (PACKAGING.md §6).</summary>
    public static string Format(EnvironmentCheck check) =>
        $"[{(check.Passed ? "OK  " : "ÉCHEC")}] {check.Name} : {check.Detail}";

    private static IReadOnlyList<ComponentInstallation> Detect(string dataRoot, IReadOnlyList<string>? componentRoots)
    {
        using var journal = new SessionFileJournal(Path.Combine(dataRoot, "sessions"));
        var detector = new ManifestDetector(journal);
        return componentRoots is null ? detector.Detect() : detector.DetectFromRoots(componentRoots);
    }

    /// <summary>Exécution : si --check répond, le binaire se lance ; l'environnement d'exécution est affiché.</summary>
    private static EnvironmentCheck CheckExecution()
    {
        var version = typeof(EnvironmentChecker).Assembly.GetName().Version?.ToString(3) ?? "inconnue";
        return new EnvironmentCheck("exécution", true,
            $"livex-launcher {version} — .NET {Environment.Version}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
    }

    private static EnvironmentCheck CheckDataDirectoryWritable(string dataRoot)
    {
        try
        {
            Directory.CreateDirectory(dataRoot);
            var probe = Path.Combine(dataRoot, ".write-probe");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return new EnvironmentCheck("droits d'écriture", true, $"répertoire de données inscriptible : {dataRoot}");
        }
        catch (Exception exception)
        {
            return new EnvironmentCheck("droits d'écriture", false, $"répertoire de données non inscriptible : {exception.Message}");
        }
    }

    private static EnvironmentCheck CheckFreeDiskSpace(string dataRoot)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dataRoot))!);
            if (drive.AvailableFreeSpace < MinFreeDiskBytes)
            {
                return new EnvironmentCheck("espace disque", false, $"espace disque insuffisant : {drive.AvailableFreeSpace / (1024 * 1024)} Mio libres");
            }

            return new EnvironmentCheck("espace disque", true, $"{drive.AvailableFreeSpace / (1024 * 1024 * 1024)} Gio libres");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new EnvironmentCheck("espace disque", false, $"espace disque non mesurable : {exception.Message}");
        }
    }

    /// <summary>Version du format : schema connu pour tous les paquets présents (PACKAGING.md §9).</summary>
    private static EnvironmentCheck CheckPackageFormat(string dataRoot)
    {
        var packagesRoot = Path.Combine(dataRoot, "packages");
        try
        {
            if (!Directory.Exists(packagesRoot))
            {
                return new EnvironmentCheck("version du format", true, "aucun paquet présent");
            }

            var verified = 0;
            foreach (var packagePath in Directory.EnumerateFiles(packagesRoot, "*.livexp"))
            {
                try
                {
                    using var reader = new LivexPackageReader(packagePath);
                    _ = reader.Manifest.Schema;
                    verified++;
                }
                catch (SchemaNotSupportedException exception)
                {
                    return new EnvironmentCheck("version du format", false,
                        $"version de paquet non prise en charge : {Path.GetFileName(packagePath)} (requise ≤ {exception.SupportedSchema}, lue {exception.ReceivedSchema})");
                }
                catch (Exception exception) when (exception is CorruptedPackageException
                    or UnsafeEntryPathException
                    or InvalidDataException)
                {
                    // Un paquet corrompu n'est pas un problème de version : il est signalé à l'ouverture (G1).
                }
            }

            return new EnvironmentCheck("version du format", true, $"{verified} paquet(s) au schema connu");
        }
        catch (IOException exception)
        {
            return new EnvironmentCheck("version du format", false, $"paquets non lisibles : {exception.Message}");
        }
    }

    private static EnvironmentCheck CheckComponents(IReadOnlyList<ComponentInstallation> installations)
    {
        if (installations.Count == 0)
        {
            return new EnvironmentCheck("composants", false, "aucun composant détecté — emplacements attendus : components/, LIVEX_HOME");
        }

        var valid = installations.Count(i => i.ManifestValid);
        var detail = valid == 0
            ? $"{installations.Count} installation(s) détectée(s), aucun manifeste valide"
            : $"{installations.Count} composant(s) détecté(s), {valid} valide(s)";
        return new EnvironmentCheck("composants", valid > 0, detail);
    }

    /// <summary>Moteur : SYNE présent et exécutable, distinct de la détection générale.</summary>
    private static EnvironmentCheck CheckEngine(IReadOnlyList<ComponentInstallation> installations)
    {
        var engine = installations.FirstOrDefault(i => i.ComponentId == "syne");
        if (engine is null)
        {
            return new EnvironmentCheck("moteur", false, "moteur absent : SYNE non détecté");
        }

        if (!engine.ManifestValid)
        {
            return new EnvironmentCheck("moteur", false, $"moteur absent : SYNE inutilisable — {engine.DetectionCause ?? "manifeste invalide"}");
        }

        if (!engine.BinaryPresent)
        {
            return new EnvironmentCheck("moteur", false, $"moteur absent : binaire absent — {engine.Location}");
        }

        return new EnvironmentCheck("moteur", true, $"SYNE {engine.Manifest!.Version} présent et exécutable : {engine.Location}");
    }

    /// <summary>Ports : aucun conflit sur les ports déclarés par les manifestes (NETWORK.md §6.2).</summary>
    private static EnvironmentCheck CheckPorts(IReadOnlyList<ComponentInstallation> installations)
    {
        foreach (var installation in installations.Where(i => i.ManifestValid))
        {
            foreach (var (kind, endpoint) in installation.Manifest!.Endpoints ?? new Dictionary<string, JsonEndpoint>())
            {
                if (endpoint?.Port is not { } port)
                {
                    continue;
                }

                if (!TcpPortProbe.IsFree(port))
                {
                    return new EnvironmentCheck("ports", false,
                        $"conflit de port : {port} occupé ({installation.ComponentId}/{kind}, déclaré au manifeste)");
                }
            }
        }

        var declared = installations.Where(i => i.ManifestValid)
            .SelectMany(i => (i.Manifest!.Endpoints ?? new Dictionary<string, JsonEndpoint>()).Values)
            .Count(endpoint => endpoint?.Port is not null);
        return new EnvironmentCheck("ports", true, $"{declared} port(s) déclaré(s), aucun conflit");
    }

    /// <summary>Navigateur : information non bloquante, jamais une cause d'échec (PACKAGING.md §6).</summary>
    private static EnvironmentCheck CheckBrowser()
    {
        var declared = Environment.GetEnvironmentVariable("BROWSER");
        return string.IsNullOrWhiteSpace(declared)
            ? new EnvironmentCheck("navigateur", true, "aucun navigateur par défaut déclaré (information, non bloquant)")
            : new EnvironmentCheck("navigateur", true, $"navigateur par défaut déclaré : {declared} (information)");
    }
}
