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
                return JsonSerializer.Deserialize<SimulationOptions>(merged.RootElement.GetRawText(), JsonOptions)
                    ?? throw new InvalidDataException($"Configuration invalide (JSON vide) : {path}");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Configuration invalide (JSON mal formé) : {path} — {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Superpose <paramref name="overlay"/> sur <paramref name="baseOptions"/> :
    /// les propriétés présentes dans l'overlay écrasent la base, les autres sont conservées.
    /// </summary>
    public static SimulationOptions Merge(SimulationOptions baseOptions, SimulationOptions overlay)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);
        ArgumentNullException.ThrowIfNull(overlay);

        using var baseDoc = JsonDocument.Parse(JsonSerializer.Serialize(baseOptions, JsonOptions));
        using var overlayDoc = JsonDocument.Parse(JsonSerializer.Serialize(overlay, JsonOptions));
        using var merged = MergeObjects(baseDoc.RootElement, overlayDoc.RootElement);
        return JsonSerializer.Deserialize<SimulationOptions>(merged.RootElement.GetRawText(), JsonOptions)!;
    }

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