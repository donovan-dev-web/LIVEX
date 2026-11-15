using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prism.Contracts;

/// <summary>
/// Écrit et lit <see cref="ObservationEnvelope"/> sans document JSON intermédiaire.
/// </summary>
/// <remarks>
/// <para>
/// L'hôte produit chaque observation à partir des objets typés du tick. Par réflexion,
/// <c>JsonSerializer</c> matérialise d'abord un <see cref="JsonElement"/> par charge utile avant de
/// l'écrire : à 1 000 agents cela valait 164 Mo de documents alloués par tick pour 41 Mo réellement
/// émis (PRISM-141). Ce convertisseur écrit la charge utile typée directement dans le flux.
/// </para>
/// <para>
/// La sortie est identique à celle de la sérialisation par réflexion : mêmes noms camelCase, même
/// ordre de propriétés (<c>protocolVersion</c>, <c>tick</c>, <c>agentId</c>, <c>changes</c>,
/// <c>self</c>), mêmes options pour les objets imbriqués. Une empreinte d'octets ne bouge donc
/// pas ; seule l'allocation disparaît. Un lecteur du protocole retrouve exactement le même JSON.
/// </para>
/// </remarks>
public sealed class ObservationEnvelopeConverter : JsonConverter<ObservationEnvelope>
{
    public override ObservationEnvelope Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("An observation envelope must be a JSON object.");
        }

        string? protocolVersion = null;
        long tick = 0;
        ulong agentId = 0;
        JsonElement changes = default;
        JsonElement self = default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (protocolVersion is null)
                {
                    throw new JsonException("Observation envelope protocolVersion is required.");
                }

                return new ObservationEnvelope(protocolVersion, tick, agentId, changes, self);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Observation envelope properties are malformed.");
            }

            var name = reader.GetString() ?? string.Empty;
            reader.Read();
            if (Matches(name, "protocolVersion", options))
            {
                protocolVersion = reader.GetString();
            }
            else if (Matches(name, "tick", options))
            {
                tick = reader.GetInt64();
            }
            else if (Matches(name, "agentId", options))
            {
                agentId = reader.GetUInt64();
            }
            else if (Matches(name, "changes", options))
            {
                changes = ParseElement(ref reader);
            }
            else if (Matches(name, "self", options))
            {
                self = ParseElement(ref reader);
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Observation envelope is incomplete.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        ObservationEnvelope value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("protocolVersion", value.ProtocolVersion);
        writer.WriteNumber("tick", value.Tick);
        writer.WriteNumber("agentId", value.AgentId);
        writer.WritePropertyName("changes");
        WritePayload(writer, value.ChangesPayload, value.ChangesElement, options);
        writer.WritePropertyName("self");
        WritePayload(writer, value.SelfPayload, value.SelfElement, options);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Écrit une charge utile : le JSON déjà présent tel quel, ou l'objet typé sérialisé
    /// directement dans le flux — jamais les deux, et jamais de DOM pour le chemin typé.
    /// </summary>
    private static void WritePayload(
        Utf8JsonWriter writer,
        object? payload,
        JsonElement element,
        JsonSerializerOptions options)
    {
        switch (payload)
        {
            case null:
                element.WriteTo(writer);
                return;
            case JsonElement typedElement:
                typedElement.WriteTo(writer);
                return;
            default:
                JsonSerializer.Serialize(writer, payload, payload.GetType(), options);
                return;
        }
    }

    /// <summary>
    /// Lit une charge utile en un élément autonome, en une seule copie.
    /// </summary>
    /// <remarks>
    /// <c>JsonDocument.ParseValue</c> suivi de <c>RootElement.Clone()</c> construisait
    /// un document jetable puis le recopiait intégralement : deux copies par payload,
    /// donc par observation et par tick. À 1 000 agents cela représentait l'essentiel
    /// du temps de lecture des pages de 8 Mo côté client. <c>JsonElement.ParseValue</c>
    /// rend directement l'élément, sans document intermédiaire à abandonner.
    /// </remarks>
    private static JsonElement ParseElement(ref Utf8JsonReader reader) =>
        JsonElement.ParseValue(ref reader);

    private static bool Matches(string name, string expected, JsonSerializerOptions options) =>
        string.Equals(
            name,
            expected,
            options.PropertyNameCaseInsensitive
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}
