using System.Collections.Frozen;
using Prism.Core.Accounting;
using Prism.Core.Determinism;
using Prism.Core.Time;
using Prism.Core.World;

namespace Prism.Core.Actions;

public sealed record CropActionRule(
    CropGrowthRule Growth,
    ushort? SeedItemTypeId,
    IReadOnlyList<ActionDropRule> HarvestDrops);

public sealed class CropActionRules
{
    private readonly FrozenDictionary<ushort, CropActionRule> _cropsByMaterial;
    private readonly FrozenDictionary<ushort, CropActionRule> _cropsBySeed;
    private readonly FrozenSet<ushort> _tillableMaterials;
    private readonly FrozenDictionary<ushort, ActionItemRule> _items;

    public CropActionRules(
        IEnumerable<CropActionRule> crops,
        IEnumerable<ushort> tillableMaterials,
        ushort farmlandMaterialId,
        ushort? hoeItemTypeId,
        IEnumerable<ActionItemRule> items)
    {
        ArgumentNullException.ThrowIfNull(crops);
        ArgumentNullException.ThrowIfNull(tillableMaterials);
        ArgumentNullException.ThrowIfNull(items);

        _items = items.ToFrozenDictionary(item => item.ItemTypeId);
        if (_items.Values.Any(item =>
                item.MaxStackSize <= 0 ||
                item.ToolTier < 0 ||
                item.Durability < 0 ||
                item.Durability > 0 && item.MaxStackSize != 1 ||
                item.WearPerAction <= 0))
        {
            throw new ArgumentException("A crop action item rule is invalid.", nameof(items));
        }

        var cropArray = crops.ToArray();
        foreach (var crop in cropArray)
        {
            ArgumentNullException.ThrowIfNull(crop);
            ArgumentNullException.ThrowIfNull(crop.Growth);
            ArgumentNullException.ThrowIfNull(crop.HarvestDrops);
            crop.Growth.Validate();
            if (crop.SeedItemTypeId is { } seedId && !_items.ContainsKey(seedId) ||
                crop.HarvestDrops.Any(drop =>
                    !_items.ContainsKey(drop.ItemTypeId) ||
                    drop.MinimumQuantity <= 0 ||
                    drop.MaximumQuantity < drop.MinimumQuantity ||
                    drop.ChancePermille is < 0 or > 1000 ||
                    drop.RequiredState is < 0 or > byte.MaxValue))
            {
                throw new ArgumentException("A crop action rule references invalid items or drops.", nameof(crops));
            }
        }

        _cropsByMaterial = cropArray.ToFrozenDictionary(crop => crop.Growth.CropMaterialId);
        _cropsBySeed = cropArray
            .Where(crop => crop.SeedItemTypeId.HasValue)
            .ToFrozenDictionary(crop => crop.SeedItemTypeId!.Value);
        _tillableMaterials = tillableMaterials.ToFrozenSet();
        FarmlandMaterialId = farmlandMaterialId;
        HoeItemTypeId = hoeItemTypeId;
        if (hoeItemTypeId is { } hoeId && !_items.ContainsKey(hoeId))
        {
            throw new ArgumentException("The hoe item is not registered in crop action rules.", nameof(hoeItemTypeId));
        }
    }

    public ushort FarmlandMaterialId { get; }
    public ushort? HoeItemTypeId { get; }
    public IReadOnlySet<ushort> TillableMaterialIds => _tillableMaterials;
    public IReadOnlyCollection<ushort> SeedItemTypeIds => _cropsBySeed.Keys;
    public IReadOnlyCollection<CropActionRule> CropRules => _cropsByMaterial.Values;

    internal bool TryGetCropByMaterial(ushort materialId, out CropActionRule? rule) =>
        _cropsByMaterial.TryGetValue(materialId, out rule);

    internal bool TryGetCropBySeed(ushort seedItemTypeId, out CropActionRule? rule) =>
        _cropsBySeed.TryGetValue(seedItemTypeId, out rule);

    internal bool IsTillable(ushort materialId) => _tillableMaterials.Contains(materialId);

    internal ActionItemRule GetItem(ushort itemTypeId) =>
        _items.TryGetValue(itemTypeId, out var item)
            ? item
            : throw new InvalidOperationException($"Crop action rules do not define item '{itemTypeId}'.");
}

public sealed record CropActionResult(
    bool Succeeded,
    string? ErrorCode,
    int DurationTicks,
    IReadOnlyList<AgentItemStack> Produced,
    ushort? BrokenToolItemTypeId = null)
{
    public static CropActionResult Failure(string errorCode) =>
        new(false, errorCode, 0, []);
}

public static class CropActionSystem
{
    public static CropActionResult Till(
        WorldGrid world,
        AgentBody agent,
        CropActionRules rules,
        MatterLedger ledger,
        BlockCoordinate target,
        bool targetPerceived = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);

        if (!targetPerceived)
        {
            return CropActionResult.Failure("not_perceived");
        }

        if (!IsWithinWorld(world, target, out var surface))
        {
            return CropActionResult.Failure("target_out_of_world");
        }

        if (!BlockActionSystem.IsAdjacent(agent.Position, target))
        {
            return CropActionResult.Failure("not_adjacent");
        }

        if (target.Z != surface.SurfaceZ ||
            !TryResolveMaterial(world, target, out var soilMaterial) ||
            !rules.IsTillable(soilMaterial))
        {
            return CropActionResult.Failure("soil_unsuitable");
        }

        ActionItemRule? hoeItem = null;
        if (rules.HoeItemTypeId is { } hoeItemTypeId)
        {
            if (agent.Inventory.Count(hoeItemTypeId) == 0)
            {
                return CropActionResult.Failure("tool_missing");
            }

            hoeItem = rules.GetItem(hoeItemTypeId);
        }

        world.SetBlockOverride(
            target,
            new BlockOverride(rules.FarmlandMaterialId, 0, IsAir: false));
        ushort? brokenToolItemTypeId = hoeItem is { } item && ToolWearSystem.Apply(agent, item, ledger)
            ? item.ItemTypeId
            : null;
        return new CropActionResult(true, null, 1, [], brokenToolItemTypeId);
    }

    public static CropActionResult Plant(
        WorldGrid world,
        AgentBody agent,
        EventWheel wheel,
        CropActionRules rules,
        MatterLedger ledger,
        ulong runSeed,
        long tick,
        BlockCoordinate target,
        ushort seedItemTypeId,
        WorldSeason season,
        bool targetPerceived = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(wheel);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        if (!targetPerceived)
        {
            return CropActionResult.Failure("not_perceived");
        }

        if (!IsWithinWorld(world, target, out _))
        {
            return CropActionResult.Failure("target_out_of_world");
        }

        if (!BlockActionSystem.IsAdjacent(agent.Position, target))
        {
            return CropActionResult.Failure("not_adjacent");
        }

        if (!rules.TryGetCropBySeed(seedItemTypeId, out var crop) || crop is null)
        {
            return CropActionResult.Failure("seed_not_plantable");
        }

        if (agent.Inventory.Count(seedItemTypeId) == 0)
        {
            return CropActionResult.Failure("item_missing");
        }

        var result = CropGrowthSystem.Plant(
            world,
            wheel,
            runSeed,
            tick,
            target,
            season,
            crop.Growth);
        if (!result.Succeeded)
        {
            return CropActionResult.Failure(MapPlantFailure(result.ErrorCode));
        }

        if (!agent.Inventory.TryTake(seedItemTypeId, 1, out _))
        {
            throw new InvalidOperationException("The seed disappeared after a successful planting preflight.");
        }

        ledger.RecordDestroyed(seedItemTypeId, 1, "plant");
        return new CropActionResult(true, null, 1, []);
    }

    public static CropActionResult Harvest(
        WorldGrid world,
        AgentBody agent,
        CropActionRules rules,
        MatterLedger ledger,
        ulong runSeed,
        long tick,
        BlockCoordinate target,
        bool targetPerceived = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        if (!targetPerceived)
        {
            return CropActionResult.Failure("not_perceived");
        }

        if (!IsWithinWorld(world, target, out _))
        {
            return CropActionResult.Failure("target_out_of_world");
        }

        if (!BlockActionSystem.IsAdjacent(agent.Position, target))
        {
            return CropActionResult.Failure("not_adjacent");
        }

        if (!world.TryGetBlockOverride(target, out var block) ||
            block.IsAir ||
            !rules.TryGetCropByMaterial(block.MaterialId, out var crop) ||
            crop is null)
        {
            return CropActionResult.Failure("crop_not_found");
        }

        if (block.State < crop.Growth.Stages - 1)
        {
            return CropActionResult.Failure("not_ripe");
        }

        var drops = RollHarvestDrops(crop, block.State, world, target, rules, runSeed, tick);
        if (drops.Count > 0 && !agent.Inventory.TryExchange(new Dictionary<ushort, int>(), drops))
        {
            return CropActionResult.Failure("capacity_reached");
        }

        world.SetBlockOverride(target, new BlockOverride(0, 0, IsAir: true));
        foreach (var stack in drops)
        {
            ledger.RecordCreated(stack.ItemTypeId, stack.Quantity, $"harvest:{block.MaterialId}");
        }

        return new CropActionResult(true, null, 1, Array.AsReadOnly(drops.ToArray()));
    }

    private static List<AgentItemStack> RollHarvestDrops(
        CropActionRule crop,
        byte state,
        WorldGrid world,
        BlockCoordinate target,
        CropActionRules rules,
        ulong runSeed,
        long tick)
    {
        var blockIndex = checked(
            (ulong)(uint)target.X +
            (ulong)(uint)world.Width *
            ((ulong)(uint)target.Y + (ulong)world.Height * (ulong)(uint)target.Z));
        var drops = new SortedDictionary<ushort, (long Quantity, int MaxStackSize)>();
        for (var index = 0; index < crop.HarvestDrops.Count; index++)
        {
            var drop = crop.HarvestDrops[index];
            if (drop.RequiredState is { } requiredState && requiredState != state)
            {
                continue;
            }

            var key = blockIndex ^
                      DeterministicRandom.Mix64((ulong)crop.Growth.CropMaterialId << 32 | (uint)index);
            if (DeterministicRandom.CounterValue(
                    runSeed,
                    RandomStreamNames.Drops,
                    (ulong)tick,
                    key) % 1000 >= (ulong)drop.ChancePermille)
            {
                continue;
            }

            var quantityRange = drop.MaximumQuantity - drop.MinimumQuantity + 1;
            var quantityRoll = DeterministicRandom.CounterValue(
                runSeed,
                RandomStreamNames.Drops,
                (ulong)tick,
                key ^ 0xD1B54A32D192ED03UL);
            var quantity = drop.MinimumQuantity + (int)(quantityRoll % (uint)quantityRange);
            var item = rules.GetItem(drop.ItemTypeId);
            var current = drops.GetValueOrDefault(drop.ItemTypeId);
            drops[drop.ItemTypeId] = (checked(current.Quantity + quantity), item.MaxStackSize);
        }

        var stacks = new List<AgentItemStack>();
        foreach (var (itemTypeId, value) in drops)
        {
            var remaining = value.Quantity;
            while (remaining > 0)
            {
                var quantity = (int)Math.Min(remaining, value.MaxStackSize);
                stacks.Add(new AgentItemStack(itemTypeId, quantity, value.MaxStackSize));
                remaining -= quantity;
            }
        }

        return stacks;
    }

    private static bool IsWithinWorld(WorldGrid world, BlockCoordinate target, out SurfaceColumn surface)
    {
        if ((uint)target.Z >= (uint)world.VerticalSize)
        {
            surface = default;
            return false;
        }

        return world.TryGetSurfaceColumn(target.X, target.Y, out surface);
    }

    private static bool TryResolveMaterial(
        WorldGrid world,
        BlockCoordinate target,
        out ushort materialId)
    {
        materialId = 0;
        if (!world.TryGetSurfaceColumn(target.X, target.Y, out var surface))
        {
            return false;
        }

        if (world.TryGetBlockOverride(target, out var block))
        {
            if (block.IsAir)
            {
                return false;
            }

            materialId = block.MaterialId;
            return true;
        }

        if (target.Z != surface.SurfaceZ)
        {
            return false;
        }

        materialId = surface.MaterialId;
        return true;
    }

    private static string MapPlantFailure(string? errorCode) => errorCode switch
    {
        "crop.invalid_soil" => "soil_unsuitable",
        "crop.water_required" => "no_water",
        "crop.block_occupied" => "cell_occupied",
        _ => errorCode ?? "plant_failed",
    };
}
