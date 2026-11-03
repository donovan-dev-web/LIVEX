using System.Text.Json;
using System.Text.Json.Serialization;
using Prism.Core.Content;

namespace Prism.Content;

public sealed record ContentPackDefinition(
    PackManifest Manifest,
    IReadOnlyList<ContentTag> Tags,
    IReadOnlyList<MaterialDefinition> Materials,
    IReadOnlyList<ItemDefinition> Items,
    IReadOnlyList<RecipeDefinition> Recipes,
    IReadOnlyList<StructureTemplateDefinition>? Templates = null,
    IReadOnlyList<GrowthRuleDefinition>? GrowthRules = null,
    IReadOnlyList<EffectDefinition>? Effects = null,
    JsonElement? Parameters = null);

public sealed record PackManifest(string Id, string Version, string SchemaVersion);

public sealed record ContentTag(string Id, string? Description = null, string? Parent = null);

public sealed record MaterialDefinition(
    string Id,
    bool Solid,
    int Hardness,
    int MinToolTier,
    string[] Tags,
    ItemDrop[] Drops,
    bool Perceivable,
    string? Station = null,
    string? Growth = null,
    MaterialRequirements? Requires = null,
    string? SeedItem = null,
    bool Tillable = false,
    string? Effects = null);

public sealed record ItemDefinition(
    string Id,
    string[] Tags,
    int MaxStack,
    int ToolTier = 0,
    int Durability = 0,
    int Weight = 0,
    string? Effects = null,
    int ShelfLifeTicks = 0,
    int UsePerAction = 1,
    string? Places = null);

public sealed record ItemDrop(
    string Item,
    [property: JsonPropertyName("qty")] QuantityRange Quantity,
    int? WhenState = null,
    int ChancePermille = 1000);

[JsonConverter(typeof(QuantityRangeJsonConverter))]
public readonly record struct QuantityRange(int Min, int Max);

public sealed class QuantityRangeJsonConverter : JsonConverter<QuantityRange>
{
    public override QuantityRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var quantity))
        {
            return new QuantityRange(quantity, quantity);
        }

        if (reader.TokenType != JsonTokenType.StartArray ||
            !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var min) ||
            !reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var max) ||
            !reader.Read() || reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("Quantity must be an integer or a two-integer [min, max] range.");
        }

        return new QuantityRange(min, max);
    }

    public override void Write(Utf8JsonWriter writer, QuantityRange value, JsonSerializerOptions options)
    {
        if (value.Min == value.Max)
        {
            writer.WriteNumberValue(value.Min);
            return;
        }

        writer.WriteStartArray();
        writer.WriteNumberValue(value.Min);
        writer.WriteNumberValue(value.Max);
        writer.WriteEndArray();
    }
}

public sealed record MaterialRequirements(string? Below = null, int? WaterWithin = null);

public sealed record RecipeIngredient(
    string? Item,
    string? Tag,
    [property: JsonPropertyName("qty")] int Quantity);

public sealed record RecipeProduct(string Item, [property: JsonPropertyName("qty")] int Quantity);

public sealed record RecipeDefinition(
    string Id,
    RecipeIngredient[] Inputs,
    RecipeProduct[] Outputs,
    string? Station,
    string? Tool,
    int DurationTicks,
    int MinMastery,
    string? MasteryCategory,
    RecipeProduct[]? ByProducts = null,
    bool IsRegeneration = false);

public sealed record StructureTemplateDefinition(
    string Id,
    int[] Size,
    TemplateBlock[] Blocks,
    TemplateMaterial[] Materials,
    int WorkTicks,
    TemplatePlacement Placement,
    TemplateEffect? Effect,
    int IntegrityThresholdPermille,
    bool IsTree = false);

public sealed record TemplateBlock(int[] At, string Block);

public sealed record TemplateMaterial(string Item, [property: JsonPropertyName("qty")] int Quantity);

public sealed record TemplatePlacement(bool FlatGround, bool ClearVolume);

public sealed record TemplateEffect(string Type, int[][] Volume, int FatigueRecoveryBonusPermille);

public sealed record GrowthRuleDefinition(
    string Id,
    int Stages,
    int StageTicks,
    int JitterTicks,
    Dictionary<string, int> SeasonFactorsPermille,
    int WitherAfterTicks);

public sealed record EffectDefinition(string Id, Dictionary<string, int> Values);

public sealed record ContentValidationError(string Code, string Path, string Message);

public sealed record CompiledContentPack(
    PackManifest Manifest,
    ContentRegistries Registries,
    IReadOnlyList<ContentTag> Tags,
    IReadOnlyList<MaterialDefinition> Materials,
    IReadOnlyList<ItemDefinition> Items,
    IReadOnlyList<RecipeDefinition> Recipes,
    IReadOnlyList<StructureTemplateDefinition> Templates,
    IReadOnlyList<GrowthRuleDefinition> GrowthRules,
    IReadOnlyList<EffectDefinition> Effects,
    JsonElement? Parameters,
    string ContentHash);

public sealed record ContentCompilationResult(
    CompiledContentPack? Pack,
    IReadOnlyList<ContentValidationError> Errors)
{
    public bool Succeeded => Pack is not null && Errors.Count == 0;
}
