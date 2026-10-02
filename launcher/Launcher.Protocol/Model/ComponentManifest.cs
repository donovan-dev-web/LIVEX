using System.Text.Json.Serialization;

namespace Launcher.Protocol.Model;

/// <summary>
/// Manifeste de composant (COMPONENTS.md §3, INTEGRATION_CONTRACT.md §2).
/// Seule source de vérité de l'orchestration : aucune donnée d'intégration codée en dur.
/// </summary>
public sealed class ComponentManifest
{
    /// <summary>Version de la structure du manifeste. Obligatoire, refusée si supérieure à la version connue.</summary>
    [JsonPropertyName("schema")]
    public int Schema { get; set; }

    /// <summary>Identifiant stable du composant (ex. « syne »).</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Nom affichable (ex. « SYNE »).</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Type fonctionnel : engine, analysis, immersion, utility.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Version du composant, semver.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Exécutable, relatif à l'installation. Chaîne simple ou par plateforme (windows/linux).</summary>
    [JsonPropertyName("executable")]
    public JsonExecutable executable { get; set; } = new();

    /// <summary>Runtime requis : dotnet, natif, script.</summary>
    [JsonPropertyName("runtime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Runtime { get; set; }

    /// <summary>Répertoire de travail relatif à l'installation.</summary>
    [JsonPropertyName("workingDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkingDirectory { get; set; }

    /// <summary>Arguments de ligne de commande de base.</summary>
    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Arguments { get; set; }

    /// <summary>Points d'accès exposés, avec ports déclarés.</summary>
    [JsonPropertyName("endpoints")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonEndpoint>? Endpoints { get; set; }

    /// <summary>Capacités déclarées, utilisées pour valider un profil.</summary>
    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; set; } = new();

    /// <summary>Sonde de santé et fréquence.</summary>
    [JsonPropertyName("health")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonHealth? Health { get; set; }

    /// <summary>Délais de démarrage et d'arrêt, en millisecondes.</summary>
    [JsonPropertyName("timeouts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonTimeouts? Timeouts { get; set; }

    /// <summary>Modes d'utilisation servis.</summary>
    [JsonPropertyName("contributesTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ContributesTo { get; set; }

    /// <summary>Version du protocole de données (NETWORK.md §2.5). Publiée dans le registre.</summary>
    [JsonPropertyName("protocolVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ProtocolVersion { get; set; }

    /// <summary>Composants requis par celui-ci, pour la complétion de profil.</summary>
    [JsonPropertyName("requires")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Requires { get; set; }
}

/// <summary>Exécutable déclaré : chaîne simple, ou par plateforme.</summary>
public sealed class JsonExecutable
{
    [JsonPropertyName("windows")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Windows { get; set; }

    [JsonPropertyName("linux")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Linux { get; set; }

    /// <summary>Forme simple : un chemin unique, sans distinction de plateforme.</summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; set; }
}

/// <summary>Point d'accès déclaré dans un manifeste.</summary>
public sealed class JsonEndpoint
{
    /// <summary>Transport : http, websocket.</summary>
    [JsonPropertyName("transport")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Transport { get; set; }

    /// <summary>Port déclaré. Priorité au manifeste : honoré s'il est libre (TESTING.md §6).</summary>
    [JsonPropertyName("port")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Port { get; set; }

    /// <summary>Booléen de déclaration (forme INTEGRATION_CONTRACT.md §2 : « control »: true).</summary>
    [JsonPropertyName("enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; set; }

    /// <summary>Option CLI qui reçoit le port résolu par le Launcher, si nécessaire.</summary>
    [JsonPropertyName("launchArgument")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LaunchArgument { get; set; }
}

/// <summary>Sonde de santé déclarée.</summary>
public sealed class JsonHealth
{
    /// <summary>Type de sonde : http, fichier.</summary>
    [JsonPropertyName("probe")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Probe { get; set; }

    /// <summary>Chemin de la sonde (ex. /health/ready).</summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; set; }

    /// <summary>Intervalle de sondage en millisecondes.</summary>
    [JsonPropertyName("intervalMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IntervalMs { get; set; }
}

/// <summary>Délais déclarés.</summary>
public sealed class JsonTimeouts
{
    [JsonPropertyName("startupMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StartupMs { get; set; }

    [JsonPropertyName("shutdownMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ShutdownMs { get; set; }
}
