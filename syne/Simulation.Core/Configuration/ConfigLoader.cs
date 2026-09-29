using System.Text.Json;

namespace Simulation.Core.Configuration;

/// <summary>
/// Charge la configuration selon la priorité Annexe H §5 :
/// 1. défauts intégrés ; 2. fichier <c>--config</c> (surcouche) ; 3. flags CLI (overrides).
/// Un fichier partiel ne surcharge que les sections/propriétés présentes.
/// </summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static SimulationOptions LoadDefaults() => new();

    /// <summary>
    /// Sérialise des options vers une <b>surcouche</b> JSON utilisable par
    /// <see cref="MergeJson(SimulationOptions, string)"/>. Partager exactement les
    /// mêmes <see cref="JsonSerializerOptions"/> que le chargeur garantit qu'un
    /// profil sérialisé puis re-fusionné redonne le profil à l'identique.
    /// </summary>
    public static string ToJson(SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return JsonSerializer.Serialize(options, JsonOptions);
    }

    public static SimulationOptions LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Le chemin du fichier de configuration est vide.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fichier de configuration introuvable : {path}", path);
        }

        try
        {
            using var overlay = JsonDocument.Parse(File.ReadAllText(path));
            using var defaults = JsonDocument.Parse(JsonSerializer.Serialize(LoadDefaults(), JsonOptions));
            JsonDocument merged = MergeObjects(defaults.RootElement, overlay.RootElement);
            using (merged)
            {
                return Deserialize(merged.RootElement);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Configuration invalide (JSON mal formé) : {path} — {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Superpose une surcouche <b>JSON partielle</b> sur <paramref name="baseOptions"/>.
    ///
    /// <para>
    /// C'est la seule fusion qui respecte la règle « un fichier partiel ne
    /// surcharge que les sections/propriétés présentes » (Annexe H §5). La fusion
    /// doit se faire sur le <b>JSON brut</b> : désérialiser d'abord la surcouche en
    /// <see cref="SimulationOptions"/> la rend complète — chaque clé absente prend
    /// silencieusement la valeur par défaut du type, qui écrase alors la base. Un
    /// appelant cherchant à surcharger <c>worldWidth</c> seule réinitialiserait
    /// ainsi <b>tout le reste</b> du profil.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidDataException">Surcouche JSON mal formée.</exception>
    public static SimulationOptions MergeJson(SimulationOptions baseOptions, string overlayJson)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(overlayJson);

        using var baseDoc = JsonDocument.Parse(JsonSerializer.Serialize(baseOptions, JsonOptions));
        using var overlay = ParseOverlay(overlayJson);
        using var merged = MergeObjects(baseDoc.RootElement, overlay.RootElement);
        return Deserialize(merged.RootElement);
    }

    /// <inheritdoc cref="MergeJson(SimulationOptions, string)"/>
    public static SimulationOptions MergeJson(SimulationOptions baseOptions, JsonElement overlay)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);

        using var baseDoc = JsonDocument.Parse(JsonSerializer.Serialize(baseOptions, JsonOptions));
        using var merged = MergeObjects(baseDoc.RootElement, overlay);
        return Deserialize(merged.RootElement);
    }

    private static JsonDocument ParseOverlay(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Surcouche de configuration invalide (JSON mal formé) — {ex.Message}", ex);
        }
    }

    private static SimulationOptions Deserialize(JsonElement element) =>
        JsonSerializer.Deserialize<SimulationOptions>(element.GetRawText(), JsonOptions)
        ?? throw new InvalidDataException("Configuration invalide (JSON vide).");

    private static JsonDocument MergeObjects(JsonElement baseElement, JsonElement overlay)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteMerged(writer, baseElement, overlay);
        }

        return JsonDocument.Parse(buffer.ToArray());
    }

    private static void WriteMerged(Utf8JsonWriter writer, JsonElement baseElement, JsonElement overlay)
    {
        if (baseElement.ValueKind == JsonValueKind.Object && overlay.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();

            foreach (var property in overlay.EnumerateObject())
            {
                if (baseElement.TryGetProperty(property.Name, out var baseProperty))
                {
                    if (baseProperty.ValueKind == JsonValueKind.Object && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        writer.WritePropertyName(property.Name);
                        WriteMerged(writer, baseProperty, property.Value);
                        continue;
                    }
                }

                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }

            foreach (var property in baseElement.EnumerateObject())
            {
                bool overridden = false;
                foreach (var overlayProperty in overlay.EnumerateObject())
                {
                    if (overlayProperty.Name == property.Name)
                    {
                        overridden = true;
                        break;
                    }
                }

                if (!overridden)
                {
                    writer.WritePropertyName(property.Name);
                    property.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            return;
        }

        overlay.WriteTo(writer);
    }
}