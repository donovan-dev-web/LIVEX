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

    /// <summary>
    /// The item that places the material carrying <paramref name="stationTag"/> (the workbench
    /// item for tag <c>workbench</c>), i.e. what a bot has to craft and place to be able to use
    /// a station it has never seen. Recipes name a station by its material key ("workbench")
    /// while the material itself only carries the generic tag "station", exactly like
    /// <see cref="Prism.Bots.PlannerBot"/>'s station lookup: both spellings are accepted, else no
    /// plan would ever craft and place the station its recipes require.
    /// </summary>
    public bool TryStationItemId(string stationTag, out ushort itemTypeId)
    {
        itemTypeId = 0;
        foreach (var item in _pack.Items)
        {
            if (item.Places is not { Length: > 0 } places ||
                !TryMaterialId(places, out var materialId))
            {
                continue;
            }

            if (MaterialHasTag(materialId, stationTag) ||
                StringComparer.Ordinal.Equals(places, stationTag))
            {
                itemTypeId = ItemId(item.Id);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The seed that grows the crop producing <paramref name="itemTypeId"/>, when the item is a
    /// harvest: this is what turns "obtain three wheat" into "sow wheat and wait".
    /// </summary>
    public bool TryCropSeedFor(ushort itemTypeId, out ushort seedItemId)
    {
        seedItemId = 0;
        foreach (var material in _pack.Materials)
        {
            if (material.Growth is null || material.SeedItem is not { Length: > 0 } seedKey)
            {
                continue;
            }

            var harvestsItem = Array.Exists(
                material.Drops,
                drop => TryItemId(drop.Item, out var dropped) && dropped == itemTypeId);
            if (harvestsItem && TryItemId(seedKey, out seedItemId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when some block of the pack drops this item — i.e. the item exists in the world and
    /// can be obtained by mining or harvesting instead of being crafted.
    /// </summary>
    public bool IsDroppedByAnyBlock(ushort itemTypeId)
    {
        foreach (var material in _pack.Materials)
        {
            foreach (var drop in material.Drops)
            {
                if (TryItemId(drop.Item, out var dropped) && dropped == itemTypeId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Items carrying <paramref name="tag"/>, in registry order.</summary>
    public IEnumerable<ushort> ItemsWithTag(string tag)
    {
        foreach (var item in _pack.Items)
        {
            if (Array.Exists(item.Tags, candidate => StringComparer.Ordinal.Equals(candidate, tag)))
            {
                yield return ItemId(item.Id);
            }
        }
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

    /// <summary>
    /// True when the item carries a shelf life, i.e. it spoils if carried too long. A plan has to
    /// gather such an item last: the cooking chain harvested its wheat, then spent hundreds of
    /// ticks on stone, furnace and fuel, and the wheat expired before the bake (`input_missing`).
    /// </summary>
    public bool HasShelfLife(ushort itemTypeId)
    {
        foreach (var item in _pack.Items)
        {
            if (ItemId(item.Id) == itemTypeId)
            {
                return item.ShelfLifeTicks > 0;
            }
        }

        return false;
    }

    /// <summary>The hoe item a bot needs to till, if the pack defines one.</summary>
    public bool TryHoeItemId(out ushort itemTypeId)
    {
        itemTypeId = 0;
        foreach (var item in _pack.Items)
        {
            if (Array.Exists(item.Tags, candidate => StringComparer.Ordinal.Equals(candidate, "hoe")))
            {
                itemTypeId = ItemId(item.Id);
                return true;
            }
        }

        return false;
    }

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
