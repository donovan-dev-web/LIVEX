using Prism.Content;

namespace Prism.Bots;

/// <summary>
/// Key/id lookup a bot needs to speak the command vocabulary without hardcoding ordinal ids.
/// It is built once per run from the compiled pack, exactly like a client would build it from
/// the published content.
/// </summary>
public sealed class BotContentMap
{
    private readonly CompiledContentPack _pack;

    private BotContentMap(CompiledContentPack pack)
    {
        _pack = pack;
    }

    public CompiledContentPack Pack => _pack;

    public static BotContentMap FromContent(CompiledContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return new BotContentMap(pack);
    }

    public bool TryItemId(string key, out ushort itemTypeId) =>
        _pack.Registries.Items.TryGetId(key, out itemTypeId);

    public ushort ItemId(string key) =>
        _pack.Registries.Items.TryGetId(key, out var id)
            ? id
            : throw new KeyNotFoundException($"The content pack does not declare item '{key}'.");

    public bool TryMaterialId(string key, out ushort materialId) =>
        _pack.Registries.Materials.TryGetId(key, out materialId);

    public ushort MaterialId(string key) =>
        _pack.Registries.Materials.TryGetId(key, out var id)
            ? id
            : throw new KeyNotFoundException($"The content pack does not declare material '{key}'.");

    public ushort RecipeId(string key) =>
        _pack.Registries.Recipes.TryGetId(key, out var id)
            ? id
            : throw new KeyNotFoundException($"The content pack does not declare recipe '{key}'.");

    public string ItemKey(ushort itemTypeId) =>
        _pack.Registries.Items.TryGetKey(itemTypeId, out var key) && key is not null
            ? key
            : throw new KeyNotFoundException($"The content pack does not declare item id '{itemTypeId}'.");

    public string MaterialKey(ushort materialId) =>
        _pack.Registries.Materials.TryGetKey(materialId, out var key) && key is not null
            ? key
            : throw new KeyNotFoundException($"The content pack does not declare material id '{materialId}'.");

    public bool ItemHasTag(ushort itemTypeId, string tag)
    {
        foreach (var item in _pack.Items)
        {
            if (ItemId(item.Id) == itemTypeId)
            {
                return Array.Exists(item.Tags, candidate => StringComparer.Ordinal.Equals(candidate, tag));
            }
        }

        return false;
    }

    /// <summary>Material id an item places into the world, when the content declares one.</summary>
    public bool TryPlacedMaterialId(ushort itemTypeId, out ushort materialId)
    {
        materialId = 0;
        foreach (var item in _pack.Items)
        {
            if (ItemId(item.Id) == itemTypeId && item.Places is { Length: > 0 } places)
            {
                return TryMaterialId(places, out materialId);
            }
        }

        return false;
    }

    public bool IsSolidMaterial(ushort materialId) =>
        Material(materialId) is { Solid: true };

    public bool IsMineableMaterial(ushort materialId) =>
        Material(materialId) is { Solid: true, Hardness: > 0 } material && material.Drops.Length > 0;

    public bool IsTillableMaterial(ushort materialId) =>
        Material(materialId) is { Tillable: true };

    public bool MaterialHasTag(ushort materialId, string tag) =>
        Material(materialId) is { } material &&
        Array.Exists(material.Tags, candidate => StringComparer.Ordinal.Equals(candidate, tag));

    public bool IsEdibleItem(ushort itemTypeId) => ItemHasTag(itemTypeId, "edible");

    private MaterialDefinition? Material(ushort materialId)
    {
        foreach (var material in _pack.Materials)
        {
            if (MaterialId(material.Id) == materialId)
            {
                return material;
            }
        }

        return null;
    }
}
