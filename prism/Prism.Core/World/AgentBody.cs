namespace Prism.Core.World;

public readonly record struct AgentPosition(int X, int Y, int Z)
{
    public int CellX => FloorDiv(X, 1000);
    public int CellY => FloorDiv(Y, 1000);
    public int FeetZ => FloorDiv(Z, 1000);

    public static AgentPosition AtCell(int x, int y, int feetZ) =>
        new(checked(x * 1000 + 500), checked(y * 1000 + 500), checked(feetZ * 1000));

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = Math.DivRem(value, divisor, out var remainder);
        return remainder < 0 ? quotient - 1 : quotient;
    }
}

public sealed record AgentBodyProfile(
    int SpeedPermille = 1000,
    int CarrySlots = 12)
{
    public void Validate()
    {
        if (SpeedPermille is < 500 or > 1500)
        {
            throw new ArgumentOutOfRangeException(nameof(SpeedPermille));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(CarrySlots);
    }
}

public readonly record struct AgentItemStack(
    ushort ItemTypeId,
    int Quantity,
    int MaxStackSize,
    int? DurabilityRemaining = null);

public sealed class AgentInventory
{
    private readonly AgentItemStack?[] _slots;

    public AgentInventory(int slotCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slotCount);
        _slots = new AgentItemStack?[slotCount];
    }

    public int Capacity => _slots.Length;
    public int UsedSlots => _slots.Count(slot => slot.HasValue);

    public IReadOnlyList<AgentItemStack> Snapshot() =>
        _slots.Where(slot => slot.HasValue).Select(slot => slot!.Value).ToArray();

    public bool CanAdd(ushort itemTypeId, int quantity, int maxStackSize)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStackSize);
        var existingCount = Count(itemTypeId);
        if (quantity > int.MaxValue - existingCount ||
            _slots.Any(slot =>
                slot is { ItemTypeId: var existingId, MaxStackSize: var existingLimit } &&
                existingId == itemTypeId &&
                existingLimit != maxStackSize))
        {
            return false;
        }

        long available = 0;
        foreach (var slot in _slots)
        {
            if (slot is { ItemTypeId: var existingId, Quantity: var existingQuantity } &&
                existingId == itemTypeId)
            {
                available += maxStackSize - existingQuantity;
            }
            else if (!slot.HasValue)
            {
                available += maxStackSize;
            }
        }

        return quantity <= available;
    }

    public bool TryAdd(ushort itemTypeId, int quantity, int maxStackSize, out int remaining)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStackSize);
        if (quantity > int.MaxValue - Count(itemTypeId))
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "The item total exceeds the supported quantity.");
        }

        if (_slots.Any(slot =>
                slot is { ItemTypeId: var existingId, MaxStackSize: var existingLimit } &&
                existingId == itemTypeId &&
                existingLimit != maxStackSize))
        {
            throw new ArgumentException("An item type cannot have inconsistent stack limits.", nameof(maxStackSize));
        }

        remaining = quantity;
        for (var index = 0; index < _slots.Length && remaining > 0; index++)
        {
            if (_slots[index] is not { } stack || stack.ItemTypeId != itemTypeId)
            {
                continue;
            }

            var moved = Math.Min(remaining, maxStackSize - stack.Quantity);
            if (moved <= 0)
            {
                continue;
            }

            _slots[index] = stack with { Quantity = stack.Quantity + moved };
            remaining -= moved;
        }

        for (var index = 0; index < _slots.Length && remaining > 0; index++)
        {
            if (_slots[index].HasValue)
            {
                continue;
            }

            var moved = Math.Min(remaining, maxStackSize);
            _slots[index] = new AgentItemStack(itemTypeId, moved, maxStackSize);
            remaining -= moved;
        }

        return remaining == 0;
    }

    internal bool TryAddStacks(IReadOnlyList<AgentItemStack> additions)
    {
        ArgumentNullException.ThrowIfNull(additions);
        var staged = (AgentItemStack?[])_slots.Clone();
        foreach (var addition in additions)
        {
            if (addition.Quantity <= 0 ||
                addition.MaxStackSize <= 0 ||
                addition.DurabilityRemaining is < 0 ||
                addition.DurabilityRemaining is > 0 && addition.MaxStackSize != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(additions));
            }

            if (staged.Any(slot =>
                    slot is { ItemTypeId: var id, MaxStackSize: var maximum } &&
                    id == addition.ItemTypeId &&
                    maximum != addition.MaxStackSize))
            {
                return false;
            }

            var remaining = addition.Quantity;
            if (addition.DurabilityRemaining is null)
            {
                for (var index = 0; index < staged.Length && remaining > 0; index++)
                {
                    if (staged[index] is not { ItemTypeId: var id, Quantity: var quantity } stack ||
                        id != addition.ItemTypeId ||
                        stack.DurabilityRemaining is not null)
                    {
                        continue;
                    }

                    var moved = Math.Min(remaining, addition.MaxStackSize - quantity);
                    if (moved <= 0)
                    {
                        continue;
                    }

                    staged[index] = stack with { Quantity = quantity + moved };
                    remaining -= moved;
                }
            }

            for (var index = 0; index < staged.Length && remaining > 0; index++)
            {
                if (staged[index].HasValue)
                {
                    continue;
                }

                var moved = Math.Min(remaining, addition.MaxStackSize);
                staged[index] = addition with { Quantity = moved };
                remaining -= moved;
            }

            if (remaining > 0)
            {
                return false;
            }
        }

        Array.Copy(staged, _slots, _slots.Length);
        return true;
    }

    public bool TryTake(ushort itemTypeId, int quantity, out IReadOnlyList<AgentItemStack> removed)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (Count(itemTypeId) < quantity)
        {
            removed = [];
            return false;
        }

        var remaining = quantity;
        var taken = new List<AgentItemStack>();
        for (var index = 0; index < _slots.Length && remaining > 0; index++)
        {
            if (_slots[index] is not { } stack || stack.ItemTypeId != itemTypeId)
            {
                continue;
            }

            var amount = Math.Min(stack.Quantity, remaining);
            taken.Add(stack with { Quantity = amount });
            remaining -= amount;
            _slots[index] = amount == stack.Quantity
                ? null
                : stack with { Quantity = stack.Quantity - amount };
        }

        removed = taken;
        return true;
    }

    internal bool TryApplyWear(
        ushort itemTypeId,
        int maximumDurability,
        int wear,
        out bool broken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDurability);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(wear);
        broken = false;
        if (maximumDurability == 0)
        {
            return Count(itemTypeId) > 0;
        }

        for (var index = 0; index < _slots.Length; index++)
        {
            if (_slots[index] is not { ItemTypeId: var id } stack || id != itemTypeId)
            {
                continue;
            }

            var current = stack.DurabilityRemaining ?? maximumDurability;
            if (current > maximumDurability)
            {
                throw new InvalidOperationException(
                    $"Item {itemTypeId} has more durability than its content definition.");
            }

            if (stack.Quantity != 1)
            {
                throw new InvalidOperationException(
                    $"Durable item {itemTypeId} must occupy a single-item stack.");
            }

            var remaining = Math.Max(0, current - wear);
            if (remaining == 0)
            {
                _slots[index] = null;
                broken = true;
            }
            else
            {
                _slots[index] = stack with { DurabilityRemaining = remaining };
            }

            return true;
        }

        return false;
    }

    internal bool TryExchange(
        IReadOnlyDictionary<ushort, int> removals,
        IReadOnlyList<AgentItemStack> additions)
    {
        ArgumentNullException.ThrowIfNull(removals);
        ArgumentNullException.ThrowIfNull(additions);
        if (removals.Any(entry => entry.Value <= 0) ||
            additions.Any(stack => stack.Quantity <= 0 || stack.MaxStackSize <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(removals), "Inventory exchange quantities must be positive.");
        }

        var staged = (AgentItemStack?[])_slots.Clone();
        foreach (var (itemTypeId, quantity) in removals.OrderBy(entry => entry.Key))
        {
            if (staged.Where(slot => slot is { ItemTypeId: var id } && id == itemTypeId)
                    .Sum(slot => (long)slot!.Value.Quantity) < quantity)
            {
                return false;
            }

            var remaining = quantity;
            for (var index = 0; index < staged.Length && remaining > 0; index++)
            {
                if (staged[index] is not { ItemTypeId: var id } stack || id != itemTypeId)
                {
                    continue;
                }

                var removed = Math.Min(stack.Quantity, remaining);
                remaining -= removed;
                staged[index] = removed == stack.Quantity
                    ? null
                    : stack with { Quantity = stack.Quantity - removed };
            }
        }

        foreach (var addition in additions)
        {
            if (staged.Any(slot =>
                    slot is { ItemTypeId: var id, MaxStackSize: var maxStackSize } &&
                    id == addition.ItemTypeId &&
                    maxStackSize != addition.MaxStackSize))
            {
                return false;
            }

            var existingQuantity = staged
                .Where(slot => slot is { ItemTypeId: var id } && id == addition.ItemTypeId)
                .Sum(slot => (long)slot!.Value.Quantity);
            if (addition.Quantity > int.MaxValue - existingQuantity)
            {
                return false;
            }

            var remaining = addition.Quantity;
            for (var index = 0; index < staged.Length && remaining > 0; index++)
            {
                if (staged[index] is not { ItemTypeId: var id, Quantity: var current } stack ||
                    id != addition.ItemTypeId)
                {
                    continue;
                }

                var moved = Math.Min(remaining, addition.MaxStackSize - current);
                if (moved <= 0)
                {
                    continue;
                }

                staged[index] = stack with { Quantity = current + moved };
                remaining -= moved;
            }

            for (var index = 0; index < staged.Length && remaining > 0; index++)
            {
                if (staged[index].HasValue)
                {
                    continue;
                }

                var moved = Math.Min(remaining, addition.MaxStackSize);
                staged[index] = new AgentItemStack(addition.ItemTypeId, moved, addition.MaxStackSize);
                remaining -= moved;
            }

            if (remaining > 0)
            {
                return false;
            }
        }

        Array.Copy(staged, _slots, _slots.Length);
        return true;
    }

    public int Count(ushort itemTypeId) =>
        _slots.Where(slot => slot is { ItemTypeId: var id } && id == itemTypeId)
            .Sum(slot => slot!.Value.Quantity);

    internal void Clear() => Array.Clear(_slots);
}

public sealed class AgentBody
{
    internal AgentBody(ulong agentId, AgentPosition position, AgentBodyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        AgentId = agentId;
        Position = position;
        Profile = profile;
        Inventory = new AgentInventory(profile.CarrySlots);
    }

    public ulong AgentId { get; }
    public AgentPosition Position { get; internal set; }
    public AgentBodyProfile Profile { get; }
    public AgentInventory Inventory { get; }
}

public readonly record struct GroundItem(
    ulong Id,
    BlockCoordinate Position,
    AgentItemStack Stack);

public sealed class GroundItemStore
{
    private readonly SortedDictionary<ulong, GroundItem> _items = [];
    private ulong _nextId;

    public int Count => _items.Count;

    public IReadOnlyList<GroundItem> Snapshot() => _items.Values.ToArray();

    public bool CanAdd(long stackCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stackCount);
        return (ulong)stackCount <= ulong.MaxValue - _nextId;
    }

    public void AddStack(
        ushort itemTypeId,
        int quantity,
        int maxStackSize,
        BlockCoordinate position)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStackSize);
        var requiredIds = (ulong)((quantity - 1) / maxStackSize + 1);
        if (requiredIds > ulong.MaxValue - _nextId)
        {
            throw new InvalidOperationException("The ground-item identifier space is exhausted.");
        }

        var remaining = quantity;
        while (remaining > 0)
        {
            var stackQuantity = Math.Min(remaining, maxStackSize);
            var item = new GroundItem(
                _nextId++,
                position,
                new AgentItemStack(itemTypeId, stackQuantity, maxStackSize));
            _items.Add(item.Id, item);
            remaining -= stackQuantity;
        }
    }

    internal void AddInventory(AgentInventory inventory, BlockCoordinate position)
    {
        var stacks = inventory.Snapshot();
        if ((ulong)stacks.Count > ulong.MaxValue - _nextId)
        {
            throw new InvalidOperationException("The ground-item identifier space is exhausted.");
        }

        foreach (var stack in stacks)
        {
            var item = new GroundItem(_nextId++, position, stack);
            _items.Add(item.Id, item);
        }

        inventory.Clear();
    }
}

public sealed class AgentRegistry
{
    private readonly SortedDictionary<ulong, AgentBody> _agents = [];

    public int Count => _agents.Count;
    public IEnumerable<AgentBody> Agents => _agents.Values;

    public bool TryGet(ulong agentId, out AgentBody? agent) =>
        _agents.TryGetValue(agentId, out agent);

    public AgentBody Incarnate(
        ulong agentId,
        AgentPosition position,
        AgentBodyProfile profile,
        MovementGraph movement)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(movement);
        profile.Validate();
        if (_agents.ContainsKey(agentId))
        {
            throw new AgentLifecycleException("agent_exists");
        }

        if (!movement.CanStandAt(position.CellX, position.CellY, out var expectedFeetZ) ||
            position.Z != checked(expectedFeetZ * 1000))
        {
            throw new AgentLifecycleException("invalid_position");
        }

        var body = new AgentBody(agentId, position, profile);
        _agents.Add(agentId, body);
        return body;
    }

    public bool Retire(ulong agentId, GroundItemStore groundItems)
    {
        ArgumentNullException.ThrowIfNull(groundItems);
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return false;
        }

        groundItems.AddInventory(
            agent.Inventory,
            new BlockCoordinate(agent.Position.CellX, agent.Position.CellY, agent.Position.FeetZ));
        _agents.Remove(agentId);
        return true;
    }
}

public sealed class AgentLifecycleException(string code)
    : InvalidOperationException($"Agent lifecycle operation failed: {code}.")
{
    public string Code { get; } = code;
}
