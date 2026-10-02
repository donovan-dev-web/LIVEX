using System.Text.Json;
using Launcher.Domain;
using Launcher.Domain.Model;
using Launcher.Protocol;
using Launcher.Protocol.Model;

namespace Launcher.Infrastructure;

/// <summary>
/// Détection des installations (COMPONENTS.md §12, PACKAGING.md §4). Purement déclarative :
/// lecture des manifestes et vérification des binaires, sans jamais démarrer un composant.
/// Sources, par priorité : arborescence de l'application, LIVEX_HOME, répertoire utilisateur,
/// registre de composants.
/// </summary>
public sealed class ManifestDetector
{
    private readonly ISessionJournal _journal;
    private static readonly int SupportedManifestSchema = 1;

    /// <summary>Initialise le détecteur.</summary>
    public ManifestDetector(ISessionJournal journal)
    {
        _journal = journal;
    }

    /// <summary>Détecte les composants sur toutes les sources standard (PACKAGING.md §4).</summary>
    public IReadOnlyList<ComponentInstallation> Detect() => DetectFromRoots(SourceRoots());

    /// <summary>Détecte les composants sur des racines explicites (banc de tests, installation portable).</summary>
    public IReadOnlyList<ComponentInstallation> DetectFromRoots(IEnumerable<string> roots)
    {
        var found = new Dictionary<string, ComponentInstallation>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                var installation = Inspect(directory);
                if (installation is null)
                {
                    continue;
                }

                // Priorité des sources : la première détection d'un composant gagne.
                if (!found.ContainsKey(installation.ComponentId))
                {
                    found[installation.ComponentId] = installation;
                }
            }
        }

        _journal.Info("Detection", $"{found.Count} composant(s) détecté(s)");
        return found.Values.ToList();
    }

    /// <summary>Examine un répertoire et renvoie l'installation qu'il porte, s'il en porte une.</summary>
    public ComponentInstallation? Inspect(string directory)
    {
        var manifestPath = Path.Combine(directory, "component.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        ComponentManifest? manifest = null;
        string? cause = null;
        try
        {
            var json = File.ReadAllText(manifestPath);
            var schema = ReadSchema(json);
            if (schema > SupportedManifestSchema)
            {
                throw new SchemaNotSupportedException("manifeste de composant", SupportedManifestSchema, schema);
            }

            manifest = JsonSerializer.Deserialize<ComponentManifest>(json, ContractJson.Options)
                ?? throw new InvalidManifestException(manifestPath, "contenu vide");
            Validate(manifest, manifestPath);
        }
        catch (Exception exception) when (exception is JsonException or InvalidManifestException or SchemaNotSupportedException)
        {
            cause = exception switch
            {
                SchemaNotSupportedException schemaException => $"version de manifeste non prise en charge (requise ≤ {schemaException.SupportedSchema}, lue {schemaException.ReceivedSchema})",
                InvalidManifestException invalid => invalid.Message,
                _ => "manifeste invalide",
            };
            _journal.Warn("Detection", cause, null);
        }

        var id = manifest?.Id ?? Path.GetFileName(directory).ToLowerInvariant();
        var executable = ResolveExecutable(manifest);
        var binaryPath = executable is null ? null : Path.Combine(directory, executable);
        var binaryPresent = binaryPath is not null && File.Exists(binaryPath);

        if (manifest is not null && cause is null && !binaryPresent)
        {
            cause = "binaire absent";
        }

        return new ComponentInstallation
        {
            ComponentId = id,
            Location = directory,
            Manifest = cause is null ? manifest : null,
            DetectionCause = cause,
            BinaryPresent = binaryPresent,
        };
    }

    private static IEnumerable<string> SourceRoots()
    {
        var roots = new List<string>
        {
            // 1. Arborescence de l'application : components/ à côté de l'exécutable.
            Path.Combine(AppContext.BaseDirectory, "components"),
        };

        // 2. LIVEX_HOME (PACKAGING.md §7).
        var livexHome = System.Environment.GetEnvironmentVariable("LIVEX_HOME");
        if (!string.IsNullOrWhiteSpace(livexHome))
        {
            roots.Add(livexHome);
        }

        // 3. Répertoire de l'utilisateur : développements locaux de composants.
        var userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            roots.Add(Path.Combine(userProfile, ".livex", "components"));
        }

        // 4. Registre de composants : fichier déclaré par l'utilisateur.
        var registryFile = Path.Combine(userProfile, ".livex", "components.registry");
        if (File.Exists(registryFile))
        {
            foreach (var line in File.ReadAllLines(registryFile))
            {
                if (!string.IsNullOrWhiteSpace(line) && Directory.Exists(line.Trim()))
                {
                    roots.Add(line.Trim());
                }
            }
        }

        return roots;
    }

    private static int ReadSchema(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("schema", out var element) && element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : 0;
    }

    private static void Validate(ComponentManifest manifest, string location)
    {
        var problems = new List<string>();
        if (manifest.Schema <= 0)
        {
            problems.Add("champ « schema » absent ou invalide");
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            problems.Add("champ « id » manquant");
        }

        if (string.IsNullOrWhiteSpace(manifest.Type))
        {
            problems.Add("champ « type » manquant");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            problems.Add("champ « version » manquant");
        }

        if (string.IsNullOrWhiteSpace(manifest.executable.Path)
            && string.IsNullOrWhiteSpace(manifest.executable.Windows)
            && string.IsNullOrWhiteSpace(manifest.executable.Linux))
        {
            problems.Add("champ « executable » manquant");
        }

        if (problems.Count > 0)
        {
            throw new InvalidManifestException(location, string.Join(" ; ", problems));
        }
    }

    private static string? ResolveExecutable(ComponentManifest? manifest)
    {
        if (manifest is null)
        {
            return null;
        }

        var platformPath = OperatingSystem.IsWindows()
            ? manifest.executable.Windows
            : manifest.executable.Linux;
        return platformPath ?? manifest.executable.Path;
    }
}
