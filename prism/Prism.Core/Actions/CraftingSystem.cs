using System.Collections.Frozen;
using System.Text.Json;
using Prism.Core.Accounting;
using Prism.Core.World;

namespace Prism.Core.Actions;

public sealed record CraftingIngredientRule(
    IReadOnlyList<ushort> AcceptedItemTypeIds,
    int Quantity);

public readonly record struct CraftingProductRule(ushort ItemTypeId, int Quantity);

public sealed record CraftingRecipeRule(
    ushort RecipeId,
    string RecipeKey,
    IReadOnlyList<CraftingIngredientRule> Inputs,
    IReadOnlyList<CraftingProductRule> Outputs,
    IReadOnlyList<CraftingProductRule> ByProducts,
    ushort? StationMaterialId,
    ushort? ToolItemTypeId,
    int DurationTicks,
    int MinimumMastery,
    string MasteryCategory);

public sealed class CraftingRules
{
    private readonly FrozenDictionary<ushort, CraftingRecipeRule> _recipes;
    private readonly FrozenDictionary<ushort, ActionItemRule> _items;

    public CraftingRules(
        IEnumerable<CraftingRecipeRule> recipes,
        IEnumerable<ActionItemRule> items,
        int maximumMasterySpeedupPermille = 500)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(items);
        if (maximumMasterySpeedupPermille is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMasterySpeedupPermille));
        }

        _items = items.ToFrozenDictionary(item => item.ItemTypeId);
        if (_items.Values.Any(item =>
                item.MaxStackSize <= 0 ||
                item.ToolTier < 0 ||
                item.Durability < 0 ||
                item.Durability > 0 && item.MaxStackSize != 1 ||
                item.WearPerAction <= 0))
        {
            throw new ArgumentException("A crafting item rule is invalid.", nameof(items));
        }

        var recipeArray = recipes.ToArray();
        foreach (var recipe in recipeArray)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(recipe.RecipeKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(recipe.MasteryCategory);
            if (recipe.Inputs is null || recipe.Outputs is null || recipe.ByProducts is null ||
                recipe.Inputs.Count == 0 || recipe.Outputs.Count == 0 ||
                recipe.DurationTicks < 0 || recipe.MinimumMastery < 0 ||
                recipe.Inputs.Any(input =>
                    input.Quantity <= 0 ||
                    input.AcceptedItemTypeIds.Count == 0 ||
                    input.AcceptedItemTypeIds.Any(id => !_items.ContainsKey(id))) ||
                recipe.Outputs.Concat(recipe.ByProducts).Any(product =>
                    product.Quantity <= 0 || !_items.ContainsKey(product.ItemTypeId)) ||
                recipe.ToolItemTypeId is { } toolId && !_items.ContainsKey(toolId))
            {
                throw new ArgumentException($"Crafting rule '{recipe.RecipeKey}' is invalid.", nameof(recipes));
            }
        }

        _recipes = recipeArray.ToFrozenDictionary(recipe => recipe.RecipeId);
        MaximumMasterySpeedupPermille = maximumMasterySpeedupPermille;
    }

    public int MaximumMasterySpeedupPermille { get; }

    internal bool TryGetRecipe(ushort recipeId, out CraftingRecipeRule? recipe) =>
        _recipes.TryGetValue(recipeId, out recipe);

    internal ActionItemRule GetItem(ushort itemTypeId) =>
        _items.TryGetValue(itemTypeId, out var item)
            ? item
            : throw new InvalidOperationException($"Crafting rules do not define item '{itemTypeId}'.");
}

public sealed class CraftingStationRegistry
{
    private readonly Dictionary<BlockCoordinate, StationReservation> _reservations = [];

    public bool IsEmpty => _reservations.Count == 0;

    public int ReservationCount => _reservations.Count;

    public bool IsBusy(BlockCoordinate position, long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        return _reservations.TryGetValue(position, out var reservation) &&
               reservation.AvailableAtTick > tick;
    }

    public byte[] CaptureState()
    {
        var reservations = _reservations
            .OrderBy(static entry => entry.Key.Z)
            .ThenBy(static entry => entry.Key.Y)
            .ThenBy(static entry => entry.Key.X)
            .Select(static entry => new StationReservationSnapshot(
                entry.Key,
                entry.Value.AgentId,
                entry.Value.AvailableAtTick))
            .ToArray();
        return JsonSerializer.SerializeToUtf8Bytes(new StationRegistryCheckpoint(1, reservations));
    }

    public void RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        if (_reservations.Count != 0)
        {
            throw new InvalidOperationException("Crafting station state can only be restored into a new registry.");
        }

        var checkpoint = JsonSerializer.Deserialize<StationRegistryCheckpoint>(checkpointBytes)
                         ?? throw new InvalidDataException("Crafting station checkpoint is empty.");
        if (checkpoint.Version != 1 ||
            checkpoint.Reservations is null ||
            checkpoint.Reservations.Any(static reservation =>
                reservation.AvailableAtTick < 0) ||
            checkpoint.Reservations.Select(static reservation => reservation.Position).Distinct().Count() !=
            checkpoint.Reservations.Length)
        {
            throw new InvalidDataException("Crafting station checkpoint metadata or reservations are invalid.");
        }

        foreach (var reservation in checkpoint.Reservations)
        {
            _reservations.Add(
                reservation.Position,
                new StationReservation(reservation.AgentId, reservation.AvailableAtTick));
        }
    }

    internal bool TryReserve(BlockCoordinate position, ulong agentId, long tick, int durationTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfNegative(durationTicks);
        if (IsBusy(position, tick))
        {
            return false;
        }

        if (durationTicks == 0)
        {
            _reservations.Remove(position);
            return true;
        }

        _reservations[position] = new StationReservation(agentId, checked(tick + durationTicks));
        return true;
    }

    internal void Release(BlockCoordinate position, ulong agentId)
    {
        if (_reservations.TryGetValue(position, out var reservation) &&
            reservation.AgentId == agentId)
        {
            _reservations.Remove(position);
        }
    }

    private readonly record struct StationReservation(ulong AgentId, long AvailableAtTick);

    private sealed record StationReservationSnapshot(
        BlockCoordinate Position,
        ulong AgentId,
        long AvailableAtTick);

    private sealed record StationRegistryCheckpoint(
        int Version,
        StationReservationSnapshot[] Reservations);
}

public sealed record CraftingActionResult(
    bool Succeeded,
    string? ErrorCode,
    int DurationTicks,
    IReadOnlyList<AgentItemStack> Produced,
    ushort? BrokenToolItemTypeId = null)
{
    public static CraftingActionResult Failure(string errorCode) =>
        new(false, errorCode, 0, []);
}

public static class CraftingSystem
{
    public static CraftingActionResult Craft(
        WorldGrid world,
        AgentBody agent,
        CraftingStationRegistry stations,
        CraftingRules rules,
        MatterLedger ledger,
        ushort recipeId,
        BlockCoordinate? stationPosition,
        long tick,
        int mastery,
        bool stationPerceived = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(stations);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfNegative(mastery);

        if (!rules.TryGetRecipe(recipeId, out var recipe) || recipe is null)
        {
            return CraftingActionResult.Failure("unknown_recipe");
        }

        if (mastery < recipe.MinimumMastery)
        {
            return CraftingActionResult.Failure("mastery_insufficient");
        }

        if (recipe.ToolItemTypeId is { } toolItemTypeId &&
            agent.Inventory.Count(toolItemTypeId) < 1)
        {
            return CraftingActionResult.Failure("tool_missing");
        }

        if (recipe.StationMaterialId is { } requiredStation)
        {
            if (stationPosition is not { } position || !stationPerceived)
            {
                return CraftingActionResult.Failure(stationPerceived ? "station_absent" : "not_perceived");
            }

            if (!BlockActionSystem.IsAdjacent(agent.Position, position))
            {
                return CraftingActionResult.Failure("not_adjacent");
            }

            if (!TryResolveMaterial(world, position, out var stationMaterial) ||
                stationMaterial != requiredStation)
            {
                return CraftingActionResult.Failure("station_absent");
            }

            if (stations.IsBusy(position, tick))
            {
                return CraftingActionResult.Failure("station_busy");
            }
        }
        else if (stationPosition is not null)
        {
            return CraftingActionResult.Failure("unexpected_station");
        }

        if (!TryPlanInputs(
                agent.Inventory.Snapshot(),
                recipe.Inputs,
                recipe.ToolItemTypeId,
                out var removals))
        {
            return CraftingActionResult.Failure("input_missing");
        }

        var outputs = recipe.Outputs.Concat(recipe.ByProducts)
            .SelectMany(product =>
            {
                var item = rules.GetItem(product.ItemTypeId);
                var freshness = item.ShelfLifeTicks > 0 ? item.ShelfLifeTicks * 1000 : (int?)null;
                var stacks = new List<AgentItemStack>();
                var remaining = product.Quantity;
                while (remaining > 0)
                {
                    var quantity = Math.Min(remaining, item.MaxStackSize);
                    stacks.Add(new AgentItemStack(
                        product.ItemTypeId,
                        quantity,
                        item.MaxStackSize,
                        FreshnessPermilleTicks: freshness,
                        FreshnessUpdatedTick: freshness is null ? 0 : tick));
                    remaining -= quantity;
                }

                return stacks;
            })
            .ToArray();
        var duration = GetCraftingDuration(
            recipe.DurationTicks,
            mastery,
            rules.MaximumMasterySpeedupPermille);

        var stationReserved = recipe.StationMaterialId is not null;
        if (stationReserved &&
            !stations.TryReserve(stationPosition!.Value, agent.AgentId, tick, duration))
        {
            return CraftingActionResult.Failure("station_busy");
        }

        if (!agent.Inventory.TryExchange(removals, outputs))
        {
            if (stationReserved)
            {
                stations.Release(stationPosition!.Value, agent.AgentId);
            }

            return CraftingActionResult.Failure("capacity_reached");
        }

        foreach (var (itemTypeId, quantity) in removals.OrderBy(entry => entry.Key))
        {
            ledger.RecordDestroyed(itemTypeId, quantity, $"recipe:{recipe.RecipeKey}");
        }

        foreach (var product in outputs)
        {
            ledger.RecordCreated(product.ItemTypeId, product.Quantity, $"recipe:{recipe.RecipeKey}");
        }

        ushort? brokenToolItemTypeId = null;
        if (recipe.ToolItemTypeId is { } usedToolId &&
            ToolWearSystem.Apply(agent, rules.GetItem(usedToolId), ledger))
        {
            brokenToolItemTypeId = usedToolId;
        }

        return new CraftingActionResult(
            true,
            null,
            duration,
            Array.AsReadOnly(outputs),
            brokenToolItemTypeId);
    }

    public static int GetCraftingDuration(
        int baseDurationTicks,
        int mastery,
        int maximumMasterySpeedupPermille)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseDurationTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(mastery);
        if (maximumMasterySpeedupPermille is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMasterySpeedupPermille));
        }

        if (baseDurationTicks == 0)
        {
            return 0;
        }

        var speedup = Math.Min(mastery, maximumMasterySpeedupPermille);
        var scaledDuration = checked((long)baseDurationTicks * (1000 - speedup));
        return checked((int)Math.Max(1, (scaledDuration + 999) / 1000));
    }

    private static bool TryPlanInputs(
        IReadOnlyList<AgentItemStack> inventory,
        IReadOnlyList<CraftingIngredientRule> ingredients,
        ushort? protectedToolItemTypeId,
        out Dictionary<ushort, int> removals)
    {
        var remainingByItem = inventory
            .GroupBy(stack => stack.ItemTypeId)
            .ToDictionary(group => group.Key, group => group.Sum(stack => (long)stack.Quantity));
        if (protectedToolItemTypeId is { } toolItemTypeId &&
            remainingByItem.TryGetValue(toolItemTypeId, out var toolQuantity))
        {
            remainingByItem[toolItemTypeId] = toolQuantity - 1;
        }

        removals = [];
        foreach (var ingredient in ingredients)
        {
            long remaining = ingredient.Quantity;
            foreach (var itemTypeId in ingredient.AcceptedItemTypeIds.Order())
            {
                if (!remainingByItem.TryGetValue(itemTypeId, out var available))
                {
                    continue;
                }

                var consumed = Math.Min(remaining, available);
                if (consumed == 0)
                {
                    continue;
                }

                remainingByItem[itemTypeId] = available - consumed;
                var totalRemoved = (long)removals.GetValueOrDefault(itemTypeId) + consumed;
                if (totalRemoved > int.MaxValue)
                {
                    removals.Clear();
                    return false;
                }

                removals[itemTypeId] = (int)totalRemoved;
                remaining -= consumed;
                if (remaining == 0)
                {
                    break;
                }
            }

            if (remaining > 0)
            {
                removals.Clear();
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveMaterial(
        WorldGrid world,
        BlockCoordinate position,
        out ushort materialId)
    {
        materialId = 0;
        if ((uint)position.Z >= (uint)world.VerticalSize ||
            !world.TryGetSurfaceColumn(position.X, position.Y, out var surface))
        {
            return false;
        }

        if (world.TryGetBlockOverride(position, out var block))
        {
            if (block.IsAir)
            {
                return false;
            }

            materialId = block.MaterialId;
            return true;
        }

        if (position.Z != surface.SurfaceZ)
        {
            return false;
        }

        materialId = surface.MaterialId;
        return true;
    }
}
