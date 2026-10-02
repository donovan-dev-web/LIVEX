using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcher.Protocol;

/// <summary>
/// Sérialisation JSON canonique de tous les contrats : camelCase, UTF-8, fins de ligne LF,
/// sans dépendance à la locale ni à la plateforme (PACKAGING.md §11).
/// </summary>
public static class ContractJson
{
    /// <summary>Options de sérialisation canoniques, partagées par écriture et lecture.</summary>
    public static JsonSerializerOptions Options { get; } = Create(indent: true);

    /// <summary>Options compactes, pour le journal NDJSON (une ligne par événement).</summary>
    public static JsonSerializerOptions Compact { get; } = Create(indent: false);

    private static JsonSerializerOptions Create(bool indent)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = indent,
            IndentCharacter = ' ',
            IndentSize = 2,
            NewLine = "\n",
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    /// <summary>Sérialise un objet en JSON canonique (indenté).</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Sérialise un objet en une ligne JSON compacte, pour NDJSON.</summary>
    public static string SerializeLine<T>(T value) => JsonSerializer.Serialize(value, Compact);

    /// <summary>Désérialise avec validation explicite du champ « schema », refusée si inconnue.</summary>
    public static T DeserializeWithSchema<T>(string json, int maxSupportedSchema, string kind)
        where T : ISchemaVersioned
    {
        var document = JsonDocument.Parse(json);
        int schema;
        try
        {
            if (!document.RootElement.TryGetProperty("schema", out var element) || element.ValueKind != JsonValueKind.Number)
            {
                throw new SchemaNotSupportedException(kind, maxSupportedSchema, 0);
            }

            schema = element.GetInt32();
        }
        finally
        {
            document.Dispose();
        }

        if (schema > maxSupportedSchema)
        {
            throw new SchemaNotSupportedException(kind, maxSupportedSchema, schema);
        }

        var value = JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new CorruptedPackageException(kind, "document JSON vide ou illisible");
        if (value.Schema > maxSupportedSchema)
        {
            throw new SchemaNotSupportedException(kind, maxSupportedSchema, value.Schema);
        }

        return value;
    }
}

/// <summary>Porté par tout document JSON versionné par un champ « schema ».</summary>
public interface ISchemaVersioned
{
    /// <summary>Version de schéma du document.</summary>
    int Schema { get; }
}
