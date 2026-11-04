using System.Collections.Frozen;
using Prism.Core.Accounting;
using Prism.Core.World;

namespace Prism.Core.Actions;

public sealed record ConsumableItemRule(
    ushort ItemTypeId,
    IReadOnlyDictionary<string, int> Effects);

public sealed record DrinkSourceRule(
    ushort MaterialId,
    IReadOnlyDictionary<string, int> Effects);

public sealed class ConsumptionRules
{
    private readonly FrozenDictionary<ushort, ConsumableItemRule> _items;
    private readonly FrozenDictionary<ushort, DrinkSourceRule> _sources;

    public ConsumptionRules(
        IEnumerable<ConsumableItemRule> items,
        IEnumerable<DrinkSourceRule> sources)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(sources);

        _items = items.ToFrozenDictionary(
            rule => rule.ItemTypeId,
            rule => rule with { Effects = FreezeEffects(rule.Effects) });
        _sources = sources.ToFrozenDictionary(
            rule => rule.MaterialId,
            rule => rule with { Effects = FreezeEffects(rule.Effects) });
    }

    public IReadOnlyCollection<ConsumableItemRule> Items => _items.Values;
    public IReadOnlyCollection<DrinkSourceRule> Sources => _sources.Values;

    internal bool TryGetItem(ushort itemTypeId, out ConsumableItemRule? rule) =>
        _items.TryGetValue(itemTypeId, out rule);

    internal bool TryGetSource(ushort materialId, out DrinkSourceRule? rule) =>
        _sources.TryGetValue(materialId, out rule);

    private static IReadOnlyDictionary<string, int> FreezeEffects(
        IReadOnlyDictionary<string, int> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (effects.Count == 0 || effects.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Consumption rules must define at least one valid effect value.", nameof(effects));
        }

        return effects.ToFrozenDictionary(StringComparer.Ordinal);
    }
}

public sealed record ItemConsumedEvent(
    ulong AgentId,
    string Source,
    ushort? ItemTypeId,
    ushort? MaterialId,
    IReadOnlyDictionary<string, int> Effects);

public sealed record ConsumptionActionResult(
    bool Succeeded,
    string? ErrorCode,
    int DurationTicks,
    ItemConsumedEvent? Event)
{
    public static ConsumptionActionResult Failure(string errorCode) =>
        new(false, errorCode, 0, null);
}

public static class ConsumptionSystem
{
    public static ConsumptionActionResult ConsumeItem(
        AgentBody agent,
        ConsumptionRules rules,
        MatterLedger ledger,
        ushort itemTypeId)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);

        if (!rules.TryGetItem(itemTypeId, out var itemRule) || itemRule is null)
        {
            return ConsumptionActionResult.Failure("not_edible");
        }

        if (agent.Inventory.Count(itemTypeId) == 0)
        {
            return ConsumptionActionResult.Failure("item_missing");
        }

        if (!agent.Inventory.TryTake(itemTypeId, 1, out _))
        {
            throw new InvalidOperationException("The consumable disappeared after its inventory preflight.");
        }

        ledger.RecordDestroyed(itemTypeId, 1, "consume");
        return new ConsumptionActionResult(
            true,
            null,
            1,
            new ItemConsumedEvent(
                agent.AgentId,
                "item",
                itemTypeId,
                null,
                itemRule.Effects));
    }

    /// <summary>
    /// Drinks from a structure effect volume (for example a well) that grants
    /// <c>water_access</c>. The event reuses the <c>source</c> channel so existing SYNE consumers
    /// keep working: it carries the pack's water material and its effects, with no new payload field.
    /// </summary>
    public static ConsumptionActionResult DrinkFromStructure(
        AgentBody agent,
        ConsumptionRules rules,
        ushort waterMaterialId,
        string structureId)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(structureId);
        if (!rules.TryGetSource(waterMaterialId, out var sourceRule) || sourceRule is null)
        {
            return ConsumptionActionResult.Failure("not_drinkable");
        }

        return new ConsumptionActionResult(
            true,
            null,
            1,
            new ItemConsumedEvent(
                agent.AgentId,
                "source",
                null,
                waterMaterialId,
                sourceRule.Effects));
    }

    public static ConsumptionActionResult DrinkFromSource(
        WorldGrid world,
        AgentBody agent,
        ConsumptionRules rules,
        BlockCoordinate target,
        bool targetPerceived = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(rules);

        if (!targetPerceived)
        {
            return ConsumptionActionResult.Failure("not_perceived");
        }

        if (!BlockActionSystem.IsAdjacent(agent.Position, target))
        {
            return ConsumptionActionResult.Failure("not_adjacent");
        }

        if (!world.TryGetSurfaceColumn(target.X, target.Y, out var surface) ||
            target.Z < 0 ||
            target.Z >= world.VerticalSize)
        {
            return ConsumptionActionResult.Failure("target_out_of_world");
        }

        ushort materialId;
        if (world.TryGetBlockOverride(target, out var block))
        {
            if (block.IsAir)
            {
                return ConsumptionActionResult.Failure("not_drinkable");
            }

            materialId = block.MaterialId;
        }
        else if (target.Z == surface.SurfaceZ)
        {
            materialId = surface.MaterialId;
        }
        else
        {
            return ConsumptionActionResult.Failure("not_drinkable");
        }

        if (!rules.TryGetSource(materialId, out var sourceRule) || sourceRule is null)
        {
            return ConsumptionActionResult.Failure("not_drinkable");
        }

        return new ConsumptionActionResult(
            true,
            null,
            1,
            new ItemConsumedEvent(
                agent.AgentId,
                "source",
                null,
                materialId,
                sourceRule.Effects));
    }
}
