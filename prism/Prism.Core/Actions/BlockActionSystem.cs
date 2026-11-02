using System.Collections.Frozen;
using Prism.Core.Accounting;
using Prism.Core.Determinism;
using Prism.Core.World;

namespace Prism.Core.Actions;

public readonly record struct ActionItemRule(
    ushort ItemTypeId,
    int MaxStackSize,
    int ToolTier,
    ushort? PlaceMaterialId = null,
    int Durability = 0,
    int WearPerAction = 1);

public readonly record struct ActionDropRule(
    ushort ItemTypeId,
    int MinimumQuantity,
    int MaximumQuantity,
    int ChancePermille,
    int? RequiredState);

public sealed record MiningMaterialRule(
    ushort MaterialId,
    bool Mineable,
    int Hardness,
    int MinimumToolTier,
    IReadOnlyList<ActionDropRule> Drops,
    bool Placeable = false);

public sealed class BlockActionRules
{
    private readonly FrozenDictionary<ushort, MiningMaterialRule> _materials;
    private readonly FrozenDictionary<ushort, ActionItemRule> _items;

    public BlockActionRules(
        IEnumerable<MiningMaterialRule> materials,
        IEnumerable<ActionItemRule> items,
        int baseTicksPerHardness = 10,
        int maximumMasterySpeedupPermille = 500)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(items);
        if (baseTicksPerHardness <= 0 ||
            maximumMasterySpeedupPermille is < 0 or > 900)
        {
            throw new ArgumentOutOfRangeException(nameof(baseTicksPerHardness));
        }

        _materials = materials.ToFrozenDictionary(rule => rule.MaterialId);
        _items = items.ToFrozenDictionary(rule => rule.ItemTypeId);
        if (_items.Values.Any(rule =>
                rule.MaxStackSize <= 0 ||
                rule.ToolTier < 0 ||
                rule.Durability < 0 ||
                rule.Durability > 0 && rule.MaxStackSize != 1 ||
                rule.WearPerAction <= 0))
        {
            throw new ArgumentException("An action item rule is invalid.", nameof(items));
        }

        foreach (var rule in _materials.Values)
        {
            if (rule.Hardness < 0 || rule.MinimumToolTier < 0 ||
                rule.Drops.Any(drop =>
                    !_items.ContainsKey(drop.ItemTypeId) ||
                    drop.MinimumQuantity <= 0 ||
                    drop.MaximumQuantity < drop.MinimumQuantity ||
                    drop.ChancePermille is < 0 or > 1000 ||
                    drop.RequiredState is < 0 or > byte.MaxValue))
            {
                throw new ArgumentException($"Mining rule for material {rule.MaterialId} is invalid.", nameof(materials));
            }
        }

        if (_items.Values.Any(item =>
                item.PlaceMaterialId is { } materialId &&
                (!_materials.TryGetValue(materialId, out var material) || !material.Placeable)))
        {
            throw new ArgumentException("An item references a material that cannot be placed.", nameof(items));
        }

        BaseTicksPerHardness = baseTicksPerHardness;
        MaximumMasterySpeedupPermille = maximumMasterySpeedupPermille;
    }

    public int BaseTicksPerHardness { get; }
    public int MaximumMasterySpeedupPermille { get; }

    internal bool TryGetMaterial(ushort materialId, out MiningMaterialRule? rule) =>
        _materials.TryGetValue(materialId, out rule);

    internal ActionItemRule GetItem(ushort itemTypeId) =>
        _items.TryGetValue(itemTypeId, out var item)
            ? item
            : throw new InvalidOperationException($"Action rules do not define item '{itemTypeId}'.");

    internal bool TryGetItem(ushort itemTypeId, out ActionItemRule item) =>
        _items.TryGetValue(itemTypeId, out item);
}

public enum BlockActionStatus : byte
{
    Succeeded,
    Failed,
}

public sealed record BlockActionResult(
    BlockActionStatus Status,
    string? ErrorCode,
    int DurationTicks,
    IReadOnlyList<AgentItemStack> Produced,
    ushort? BrokenToolItemTypeId = null)
{
    public bool Succeeded => Status == BlockActionStatus.Succeeded;

    public static BlockActionResult Failure(string code) =>
        new(BlockActionStatus.Failed, code, 0, []);
}

public static class BlockActionSystem
{
    public static int GetMiningDuration(
        int hardness,
        int masteryPermille,
        int baseTicksPerHardness,
        int maximumMasterySpeedupPermille)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hardness);
        ArgumentOutOfRangeException.ThrowIfNegative(masteryPermille);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseTicksPerHardness);
        if (maximumMasterySpeedupPermille is < 0 or > 900)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMasterySpeedupPermille));
        }

        if (hardness == 0)
        {
            return 0;
        }

        var speedup = Math.Min(masteryPermille, maximumMasterySpeedupPermille);
        var duration = checked((long)hardness * baseTicksPerHardness * (1000 - speedup));
        return checked((int)Math.Max(1, (duration + 999) / 1000));
    }

    public static BlockActionResult Mine(
        WorldGrid world,
        AgentBody agent,
        GroundItemStore groundItems,
        BlockActionRules rules,
        MatterLedger ledger,
        ulong runSeed,
        long tick,
        BlockCoordinate target,
        bool targetPerceived,
        int masteryPermille = 0)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(groundItems);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        if (!targetPerceived)
        {
            return BlockActionResult.Failure("not_perceived");
        }

        if ((uint)target.Z >= (uint)world.VerticalSize ||
            !world.TryGetSurfaceColumn(target.X, target.Y, out _))
        {
            return BlockActionResult.Failure("target_out_of_world");
        }

        if (!IsAdjacent(agent.Position, target))
        {
            return BlockActionResult.Failure("not_adjacent");
        }

        if (!TryResolveTarget(world, target, out var materialId, out var state))
        {
            return BlockActionResult.Failure("block_already_mined");
        }

        if (!rules.TryGetMaterial(materialId, out var material) || material is null || !material.Mineable)
        {
            return BlockActionResult.Failure("block_not_mineable");
        }

        var tool = agent.Inventory.Snapshot()
            .Select(stack => rules.TryGetItem(stack.ItemTypeId, out var item) ? item : default)
            .Where(item => item.ToolTier > 0)
            .OrderByDescending(item => item.ToolTier)
            .ThenBy(item => item.ItemTypeId)
            .FirstOrDefault();
        var bestToolTier = tool.ToolTier;
        if (bestToolTier < material.MinimumToolTier)
        {
            return BlockActionResult.Failure("tool_tier_insufficient");
        }

        var drops = RollDrops(material, state, world, target, rules, runSeed, tick);
        var stacks = MergeDrops(drops);
        var inventorySnapshot = agent.Inventory.Snapshot();
        foreach (var stack in stacks)
        {
            if (inventorySnapshot.Any(existing =>
                    existing.ItemTypeId == stack.ItemTypeId &&
                    existing.MaxStackSize != stack.MaxStackSize))
            {
                return BlockActionResult.Failure("inventory_stack_limit_mismatch");
            }

            if (stack.Quantity > int.MaxValue - agent.Inventory.Count(stack.ItemTypeId))
            {
                return BlockActionResult.Failure("inventory_quantity_limit");
            }
        }

        var requiredGroundStacks = stacks.Sum(stack =>
            ((long)stack.Quantity + stack.MaxStackSize - 1) / stack.MaxStackSize);
        if (!groundItems.CanAdd(requiredGroundStacks))
        {
            throw new InvalidOperationException("The ground-item identifier space is exhausted.");
        }

        world.SetBlockOverride(target, new BlockOverride(0, 0, IsAir: true));
        foreach (var stack in stacks)
        {
            if (!agent.Inventory.TryAdd(stack.ItemTypeId, stack.Quantity, stack.MaxStackSize, out var remaining))
            {
                var placed = stack.Quantity - remaining;
                if (placed > 0)
                {
                    ledger.RecordCreated(stack.ItemTypeId, placed, $"mine:{materialId}");
                }

                groundItems.AddStack(
                    stack.ItemTypeId,
                    remaining,
                    stack.MaxStackSize,
                    target);
                ledger.RecordCreated(stack.ItemTypeId, remaining, $"mine:{materialId}");
            }
            else
            {
                ledger.RecordCreated(stack.ItemTypeId, stack.Quantity, $"mine:{materialId}");
            }
        }

        var duration = GetMiningDuration(
            material.Hardness,
            masteryPermille,
            rules.BaseTicksPerHardness,
            rules.MaximumMasterySpeedupPermille);
        ushort? brokenToolItemTypeId = null;
        if (tool.ToolTier > 0 &&
            ToolWearSystem.Apply(agent, tool, ledger))
        {
            brokenToolItemTypeId = tool.ItemTypeId;
        }

        return new BlockActionResult(
            BlockActionStatus.Succeeded,
            null,
            duration,
            Array.AsReadOnly(stacks.ToArray()),
            brokenToolItemTypeId);
    }

    public static BlockActionResult Place(
        WorldGrid world,
        AgentBody agent,
        BlockActionRules rules,
        MatterLedger ledger,
        BlockCoordinate target,
        ushort itemTypeId,
        ushort materialId,
        AgentRegistry agents,
        bool targetPerceived)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(agents);

        if (!targetPerceived)
        {
            return BlockActionResult.Failure("not_perceived");
        }

        if (!IsAdjacent(agent.Position, target))
        {
            return BlockActionResult.Failure("not_adjacent");
        }

        if (!world.TryGetSurfaceColumn(target.X, target.Y, out var surface) ||
            (uint)target.Z >= (uint)world.VerticalSize)
        {
            return BlockActionResult.Failure("target_out_of_world");
        }

        if (!rules.TryGetMaterial(materialId, out var material) ||
            material is null ||
            !material.Placeable)
        {
            return BlockActionResult.Failure("block_not_placeable");
        }

        if (!rules.TryGetItem(itemTypeId, out var item))
        {
            return BlockActionResult.Failure("unknown_item");
        }

        if (item.PlaceMaterialId != materialId)
        {
            return BlockActionResult.Failure("item_block_mismatch");
        }

        if (target.Z <= surface.SurfaceZ ||
            (world.TryGetBlockOverride(target, out var existing) && !existing.IsAir))
        {
            return BlockActionResult.Failure("cell_occupied");
        }

        if (agents.Agents.Any(existingAgent =>
                target.X == existingAgent.Position.CellX &&
                target.Y == existingAgent.Position.CellY &&
                target.Z >= existingAgent.Position.FeetZ &&
                target.Z < existingAgent.Position.FeetZ + 2))
        {
            return BlockActionResult.Failure("agent_in_cell");
        }

        if (!agent.Inventory.TryTake(itemTypeId, 1, out _))
        {
            return BlockActionResult.Failure("item_missing");
        }

        world.SetBlockOverride(target, new BlockOverride(materialId, 0, IsAir: false));
        ledger.RecordDestroyed(itemTypeId, 1, "place");
        return new BlockActionResult(BlockActionStatus.Succeeded, null, 1, []);
    }

    private static bool TryResolveTarget(
        WorldGrid world,
        BlockCoordinate target,
        out ushort materialId,
        out byte state)
    {
        materialId = 0;
        state = 0;
        if ((uint)target.Z >= (uint)world.VerticalSize ||
            !world.TryGetSurfaceColumn(target.X, target.Y, out var surface))
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
            state = block.State;
            return true;
        }

        if (target.Z != surface.SurfaceZ)
        {
            return false;
        }

        materialId = surface.MaterialId;
        state = surface.State;
        return true;
    }

    private static List<(ushort ItemTypeId, int Quantity, int MaxStackSize)> RollDrops(
        MiningMaterialRule material,
        byte state,
        WorldGrid world,
        BlockCoordinate target,
        BlockActionRules rules,
        ulong runSeed,
        long tick)
    {
        var drops = new List<(ushort ItemTypeId, int Quantity, int MaxStackSize)>();
        var blockIndex = checked(
            (ulong)(uint)target.X +
            (ulong)(uint)world.Width *
            ((ulong)(uint)target.Y + (ulong)(uint)world.Height * (ulong)(uint)target.Z));
        for (var index = 0; index < material.Drops.Count; index++)
        {
            var drop = material.Drops[index];
            if (drop.RequiredState is { } requiredState && requiredState != state)
            {
                continue;
            }

            var key = blockIndex ^ DeterministicRandom.Mix64((ulong)(uint)index);
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
            drops.Add((drop.ItemTypeId, quantity, item.MaxStackSize));
        }

        return drops;
    }

    private static List<AgentItemStack> MergeDrops(
        IReadOnlyList<(ushort ItemTypeId, int Quantity, int MaxStackSize)> drops)
    {
        var merged = new SortedDictionary<ushort, (int Quantity, int MaxStackSize)>();
        foreach (var drop in drops)
        {
            if (merged.TryGetValue(drop.ItemTypeId, out var existing))
            {
                merged[drop.ItemTypeId] = (checked(existing.Quantity + drop.Quantity), existing.MaxStackSize);
            }
            else
            {
                merged.Add(drop.ItemTypeId, (drop.Quantity, drop.MaxStackSize));
            }
        }

        return merged.SelectMany(pair =>
        {
            var stacks = new List<AgentItemStack>();
            var remaining = pair.Value.Quantity;
            while (remaining > 0)
            {
                var quantity = Math.Min(remaining, pair.Value.MaxStackSize);
                stacks.Add(new AgentItemStack(pair.Key, quantity, pair.Value.MaxStackSize));
                remaining -= quantity;
            }

            return stacks;
        }).ToList();
    }

    internal static bool IsAdjacent(AgentPosition position, BlockCoordinate target)
    {
        var distance = Math.Abs((long)position.CellX - target.X) +
                       Math.Abs((long)position.CellY - target.Y) +
                       Math.Abs((long)position.FeetZ - target.Z);
        return distance <= 1;
    }
}
