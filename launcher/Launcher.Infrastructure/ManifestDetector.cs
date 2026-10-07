using System.Text.Json;
using System.Text.RegularExpressions;
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

                // Priorité des sources : la première détection d'un composant gagne — sauf si
                // elle est inutilisable. Une installation dont le manifeste est invalide ne doit
                // pas masquer une installation valide détectée plus loin : sinon un manifeste
                // corrompu dans `components/` rendrait un SYNE parfaitement installé introuvable.
                if (!found.TryGetValue(installation.ComponentId, out var previous))
                {
                    found[installation.ComponentId] = installation;
                }
                else if (!previous.ManifestValid && installation.ManifestValid)
                {
                    _journal.Warn("Detection",
                        $"{installation.ComponentId} : installation valide de {installation.Location} remplace {previous.Location} ({previous.DetectionCause ?? "inutilisable"})");
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
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidManifestException(manifestPath, "la racine doit être un objet JSON");
            }

            var schema = ReadSchema(document.RootElement);
            if (schema > SupportedManifestSchema)
            {
                throw new SchemaNotSupportedException("manifeste de composant", SupportedManifestSchema, schema);
            }

            manifest = JsonSerializer.Deserialize<ComponentManifest>(json, ContractJson.Options)
                ?? throw new InvalidManifestException(manifestPath, "contenu vide");
            Validate(manifest, manifestPath, document.RootElement);
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

    private static int ReadSchema(JsonElement root) =>
        root.TryGetProperty("schema", out var element)
        && element.ValueKind == JsonValueKind.Number
        && element.TryGetInt32(out var schema)
            ? schema
            : 0;

    private static void Validate(ComponentManifest manifest, string location, JsonElement root)
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
        else if (!IsValidId(manifest.Id))
        {
            problems.Add("champ « id » invalide (minuscules ASCII, chiffres et tirets uniquement)");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            problems.Add("champ « name » manquant");
        }

        if (string.IsNullOrWhiteSpace(manifest.Type))
        {
            problems.Add("champ « type » manquant");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            problems.Add("champ « version » manquant");
        }

        if (manifest.Capabilities is null
            || !root.TryGetProperty("capabilities", out var capabilitiesElement)
            || capabilitiesElement.ValueKind != JsonValueKind.Array)
        {
            problems.Add("champ « capabilities » absent ou invalide");
        }
        else if (manifest.Capabilities.Any(string.IsNullOrWhiteSpace)
            || manifest.Capabilities.Distinct(StringComparer.Ordinal).Count() != manifest.Capabilities.Count)
        {
            problems.Add("champ « capabilities » contient une capacité vide ou en double");
        }

        var executable = manifest.executable;
        if (executable is null
            || new[] { executable.Path, executable.Windows, executable.Linux }
                .All(string.IsNullOrWhiteSpace))
        {
            problems.Add("champ « executable » manquant");
        }
        else if (new[] { executable.Path, executable.Windows, executable.Linux }
                 .Where(path => !string.IsNullOrWhiteSpace(path))
                 .Any(path => !IsSafeRelativePath(path!)))
        {
            problems.Add("les chemins « executable » doivent rester relatifs à l'installation");
        }

        if (manifest.WorkingDirectory is { } workingDirectory && !IsSafeRelativePath(workingDirectory))
        {
            problems.Add("le chemin « workingDirectory » doit rester relatif à l'installation");
        }

        if (manifest.Runtime is not null && manifest.Runtime is not ("dotnet" or "native" or "script"))
        {
            problems.Add("champ « runtime » invalide");
        }

        if (manifest.Endpoints is null || !manifest.Endpoints.ContainsKey("control"))
        {
            problems.Add("champ « endpoints.control » manquant");
        }

        if (manifest.Endpoints is not null)
        {
            foreach (var (name, endpoint) in manifest.Endpoints)
            {
                if (string.IsNullOrWhiteSpace(name) || endpoint is null)
                {
                    problems.Add("champ « endpoints » contient un point d'accès invalide");
                    continue;
                }

                if (endpoint.Transport is not ("http" or "websocket"))
                {
                    problems.Add($"transport invalide pour le point d'accès « {name} »");
                }

                if (endpoint.Port is < 1 or > 65535)
                {
                    problems.Add($"port invalide pour le point d'accès « {name} »");
                }

                if (name == "control" && (endpoint.Transport != "http" || endpoint.Enabled == false))
                {
                    problems.Add("« endpoints.control » doit être un point HTTP activé");
                }

                if (endpoint.LaunchArgument is { } argument
                    && !Regex.IsMatch(argument, "^--?[A-Za-z0-9][A-Za-z0-9-]*$", RegexOptions.CultureInvariant))
                {
                    problems.Add($"argument invalide pour le point d'accès « {name} »");
                }
            }
        }

        if (manifest.Health is null
            || manifest.Health.Probe is not ("http" or "file")
            || string.IsNullOrWhiteSpace(manifest.Health.Path)
            || (manifest.Health.IntervalMs is { } interval && interval < 100))
        {
            problems.Add("champ « health » absent ou invalide");
        }

        if (manifest.Timeouts is null
            || manifest.Timeouts.StartupMs is not > 0
            || manifest.Timeouts.ShutdownMs is not > 0)
        {
            problems.Add("champ « timeouts » absent ou invalide");
        }

        if (manifest.ProtocolVersion is <= 0)
        {
            problems.Add("champ « protocolVersion » invalide");
        }

        if (problems.Count > 0)
        {
            throw new InvalidManifestException(location, string.Join(" ; ", problems));
        }
    }

    private static bool IsValidId(string id) =>
        id.Length > 0
        && id[0] is >= 'a' and <= 'z'
        && id.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.StartsWith('/') || path.StartsWith('\\')
            || (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':'))
        {
            return false;
        }

        return path.Split(['/', '\\'], StringSplitOptions.None)
            .All(segment => segment.Length > 0 && segment != "..");
    }

    private static string? ResolveExecutable(ComponentManifest? manifest)
    {
        if (manifest is null || manifest.executable is null)
        {
            return null;
        }

        var platformPath = OperatingSystem.IsWindows()
            ? manifest.executable.Windows
            : manifest.executable.Linux;
        return platformPath ?? manifest.executable.Path;
    }
}
