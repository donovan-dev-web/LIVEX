using Prism.Core.Accounting;
using Prism.Core.World;
using System.Text.Json;

namespace Prism.Core.Actions;

[Flags]
public enum ContainerAccessRights : byte
{
    None = 0,
    Use = 1,
    Take = 2,
    Put = 4,
    Build = 8,
}

public enum AccessPrincipalKind : byte
{
    Agent,
    Group,
}

public readonly record struct AccessPrincipal(AccessPrincipalKind Kind, ulong Id)
{
    public static AccessPrincipal ForAgent(ulong agentId) =>
        new(AccessPrincipalKind.Agent, agentId);

    public static AccessPrincipal ForGroup(ulong groupId) =>
        new(AccessPrincipalKind.Group, groupId);
}

public readonly record struct ContainerAccessRule(
    AccessPrincipal Principal,
    ContainerAccessRights Rights);

public static class ContainerAccessRuleParser
{
    public static bool TryParse(JsonElement element, out ContainerAccessRule rule)
    {
        rule = default;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("kind", out var kindElement) ||
            kindElement.ValueKind != JsonValueKind.String ||
            !element.TryGetProperty("id", out var idElement) ||
            idElement.ValueKind != JsonValueKind.Number ||
            !idElement.TryGetUInt64(out var principalId) ||
            !element.TryGetProperty("rights", out var rightsElement) ||
            rightsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var principalKind = kindElement.GetString() switch
        {
            "agent" => AccessPrincipalKind.Agent,
            "group" => AccessPrincipalKind.Group,
            _ => (AccessPrincipalKind?)null,
        };
        if (principalKind is null)
        {
            return false;
        }

        var rights = ContainerAccessRights.None;
        foreach (var rightElement in rightsElement.EnumerateArray())
        {
            if (rightElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var right = rightElement.GetString() switch
            {
                "use" => ContainerAccessRights.Use,
                "take" => ContainerAccessRights.Take,
                "put" => ContainerAccessRights.Put,
                "build" => ContainerAccessRights.Build,
                _ => (ContainerAccessRights?)null,
            };
            if (right is null)
            {
                return false;
            }

            rights |= right.Value;
        }

        if (rights == ContainerAccessRights.None)
        {
            return false;
        }

        rule = new ContainerAccessRule(
            new AccessPrincipal(principalKind.Value, principalId),
            rights);
        return true;
    }
}

public sealed class ContainerInventory
{
    private const int CheckpointVersion = 1;
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new(JsonSerializerDefaults.Web);
    private const ContainerAccessRights AllRights =
        ContainerAccessRights.Use |
        ContainerAccessRights.Take |
        ContainerAccessRights.Put |
        ContainerAccessRights.Build;

    private readonly List<AgentItemStack> _stacks = [];
    private readonly Dictionary<AccessPrincipal, ContainerAccessRights> _access = [];

    public ContainerInventory(
        int capacitySlots,
        int decayPermillePerTick,
        BlockCoordinate position,
        AccessPrincipal owner)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacitySlots);
        if (decayPermillePerTick is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(decayPermillePerTick));
        }

        if (owner.Kind is not (AccessPrincipalKind.Agent or AccessPrincipalKind.Group))
        {
            throw new ArgumentOutOfRangeException(nameof(owner));
        }

        CapacitySlots = capacitySlots;
        DecayPermillePerTick = decayPermillePerTick;
        Position = position;
        Owner = owner;
    }

    public int CapacitySlots { get; }
    public int UsedSlots => _stacks.Count;
    public int DecayPermillePerTick { get; }
    public BlockCoordinate Position { get; }
    public AccessPrincipal Owner { get; }

    public IReadOnlyList<AgentItemStack> Snapshot() => _stacks.ToArray();

    public byte[] CaptureState()
    {
        var checkpoint = new ContainerCheckpoint(
            CheckpointVersion,
            CapacitySlots,
            DecayPermillePerTick,
            Position,
            Owner,
            _stacks.ToArray(),
            _access.OrderBy(static entry => entry.Key.Kind)
                .ThenBy(static entry => entry.Key.Id)
                .Select(static entry => new ContainerAccessRule(entry.Key, entry.Value))
                .ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint, CheckpointJsonOptions);
    }

    public static ContainerInventory RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        var checkpoint = JsonSerializer.Deserialize<ContainerCheckpoint>(
                             checkpointBytes,
                             CheckpointJsonOptions)
                         ?? throw new InvalidDataException("Container checkpoint is empty.");
        if (checkpoint.Version != CheckpointVersion ||
            checkpoint.Stacks is null ||
            checkpoint.AccessRules is null)
        {
            throw new InvalidDataException("Container checkpoint has an unsupported or incomplete format.");
        }

        ContainerInventory restored;
        try
        {
            restored = new ContainerInventory(
                checkpoint.CapacitySlots,
                checkpoint.DecayPermillePerTick,
                checkpoint.Position,
                checkpoint.Owner);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Container checkpoint contains invalid metadata.", exception);
        }

        if (!restored.TryAddStacks(checkpoint.Stacks))
        {
            throw new InvalidDataException("Container checkpoint contents exceed its capacity.");
        }

        try
        {
            var result = restored.SetAccessList(checkpoint.Owner, checkpoint.AccessRules);
            if (!result.Succeeded)
            {
                throw new InvalidDataException("Container checkpoint access rules are invalid.");
            }
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Container checkpoint access rules are invalid.", exception);
        }

        return restored;
    }

    public IReadOnlyList<SpoilageEvent> ApplyDecay(long tick, MatterLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        var spoiled = new List<SpoilageEvent>();
        for (var index = 0; index < _stacks.Count;)
        {
            var stack = _stacks[index];
            var advance = stack.AdvanceFreshness(tick, DecayPermillePerTick);
            if (advance.Stack is { } updated)
            {
                _stacks[index] = updated;
                index++;
                continue;
            }

            spoiled.Add(new SpoilageEvent(tick, stack.ItemTypeId, advance.SpoiledQuantity));
            ledger.RecordDestroyed(stack.ItemTypeId, advance.SpoiledQuantity, "spoil");
            _stacks.RemoveAt(index);
        }

        return spoiled;
    }

    public ContainerActionResult SetAccessList(
        AccessPrincipal requester,
        IEnumerable<ContainerAccessRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (requester != Owner)
        {
            return ContainerActionResult.Failure("not_owner");
        }

        var newAccess = new Dictionary<AccessPrincipal, ContainerAccessRights>();
        foreach (var rule in rules)
        {
            if (rule.Principal.Kind is not (AccessPrincipalKind.Agent or AccessPrincipalKind.Group) ||
                rule.Rights == ContainerAccessRights.None ||
                (rule.Rights & ~AllRights) != 0 ||
                !newAccess.TryAdd(rule.Principal, rule.Rights))
            {
                throw new ArgumentException("The container access list contains an invalid or duplicate rule.", nameof(rules));
            }
        }

        _access.Clear();
        foreach (var (principal, rights) in newAccess)
        {
            _access.Add(principal, rights);
        }

        return ContainerActionResult.Success(0);
    }

    internal bool IsAuthorized(
        IEnumerable<AccessPrincipal> principals,
        ContainerAccessRights requiredRight)
    {
        if (_access.Count == 0)
        {
            return true;
        }

        foreach (var principal in principals)
        {
            if (principal == Owner ||
                (_access.TryGetValue(principal, out var rights) && (rights & requiredRight) == requiredRight))
            {
                return true;
            }
        }

        return false;
    }

    internal bool CanAdd(ushort itemTypeId, int quantity, int maxStackSize)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStackSize);
        if (_stacks.Any(stack =>
                stack.ItemTypeId == itemTypeId &&
                stack.MaxStackSize != maxStackSize))
        {
            return false;
        }

        long available = 0;
        foreach (var stack in _stacks)
        {
            if (stack.ItemTypeId == itemTypeId)
            {
                available += maxStackSize - stack.Quantity;
            }
        }

        available += (long)(CapacitySlots - _stacks.Count) * maxStackSize;
        return quantity <= available;
    }

    public bool TryAdd(AgentItemStack stack)
    {
        if (!stack.HasValidState)
        {
            throw new ArgumentOutOfRangeException(nameof(stack), "The container stack is invalid.");
        }

        return TryAddStacks([stack]);
    }

    internal bool TryAddStacks(IReadOnlyList<AgentItemStack> additions)
    {
        ArgumentNullException.ThrowIfNull(additions);
        var staged = _stacks.ToList();
        foreach (var addition in additions)
        {
            if (!addition.HasValidState)
            {
                throw new ArgumentOutOfRangeException(nameof(additions));
            }

            if (staged.Any(stack =>
                    stack.ItemTypeId == addition.ItemTypeId &&
                    stack.MaxStackSize != addition.MaxStackSize))
            {
                return false;
            }

            var remaining = addition.Quantity;
            if (addition.DurabilityRemaining is null)
            {
                for (var index = 0; index < staged.Count && remaining > 0; index++)
                {
                    var stack = staged[index];
                    if (!addition.IsMergeableWith(stack))
                    {
                        continue;
                    }

                    var moved = Math.Min(remaining, addition.MaxStackSize - stack.Quantity);
                    if (moved <= 0)
                    {
                        continue;
                    }

                    staged[index] = stack with { Quantity = stack.Quantity + moved };
                    remaining -= moved;
                }
            }

            while (remaining > 0)
            {
                if (staged.Count >= CapacitySlots)
                {
                    return false;
                }

                var moved = Math.Min(remaining, addition.MaxStackSize);
                staged.Add(addition with { Quantity = moved });
                remaining -= moved;
            }
        }

        _stacks.Clear();
        _stacks.AddRange(staged);
        return true;
    }

    internal bool CanTake(ushort itemTypeId, int quantity) =>
        quantity > 0 && _stacks
            .Where(stack => stack.ItemTypeId == itemTypeId)
            .Sum(stack => (long)stack.Quantity) >= quantity;

    internal bool TryTake(ushort itemTypeId, int quantity)
        => TryTake(itemTypeId, quantity, out _);

    internal bool TryTake(
        ushort itemTypeId,
        int quantity,
        out IReadOnlyList<AgentItemStack> removedStacks)
    {
        if (!CanTake(itemTypeId, quantity))
        {
            removedStacks = [];
            return false;
        }

        var remaining = quantity;
        var taken = new List<AgentItemStack>();
        for (var index = 0; index < _stacks.Count && remaining > 0;)
        {
            var stack = _stacks[index];
            if (stack.ItemTypeId != itemTypeId)
            {
                index++;
                continue;
            }

            var removedQuantity = Math.Min(stack.Quantity, remaining);
            taken.Add(stack with { Quantity = removedQuantity });
            remaining -= removedQuantity;
            if (removedQuantity == stack.Quantity)
            {
                _stacks.RemoveAt(index);
            }
            else
            {
                _stacks[index] = stack with { Quantity = stack.Quantity - removedQuantity };
                index++;
            }
        }

        removedStacks = taken;
        return true;
    }

    private sealed record ContainerCheckpoint(
        int Version,
        int CapacitySlots,
        int DecayPermillePerTick,
        BlockCoordinate Position,
        AccessPrincipal Owner,
        AgentItemStack[] Stacks,
        ContainerAccessRule[] AccessRules);
}

public sealed class WorldContainerStore
{
    private readonly SortedDictionary<BlockCoordinate, ContainerInventory> _containers = new(
        Comparer<BlockCoordinate>.Create(static (left, right) =>
        {
            var comparison = left.Z.CompareTo(right.Z);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Y.CompareTo(right.Y);
            return comparison != 0 ? comparison : left.X.CompareTo(right.X);
        }));

    public int Count => _containers.Count;
    public bool IsEmpty => _containers.Count == 0;
    public IReadOnlyCollection<ContainerInventory> Snapshot() => _containers.Values.ToArray();

    public bool TryGet(BlockCoordinate position, out ContainerInventory? container) =>
        _containers.TryGetValue(position, out container);

    public bool TryAdd(ContainerInventory container)
    {
        ArgumentNullException.ThrowIfNull(container);
        return _containers.TryAdd(container.Position, container);
    }

    public bool Remove(BlockCoordinate position) => _containers.Remove(position);

    public byte[] CaptureState() =>
        JsonSerializer.SerializeToUtf8Bytes(
            _containers.Values.Select(static container => container.CaptureState()).ToArray(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public void RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        if (_containers.Count != 0)
        {
            throw new InvalidOperationException("Container state can only be restored into a new store.");
        }

        var checkpoints = JsonSerializer.Deserialize<byte[][]>(
                             checkpointBytes,
                             new JsonSerializerOptions(JsonSerializerDefaults.Web))
                         ?? throw new InvalidDataException("Container store checkpoint is empty.");
        foreach (var bytes in checkpoints)
        {
            if (bytes is null)
            {
                throw new InvalidDataException("Container store checkpoint contains an empty entry.");
            }

            var container = ContainerInventory.RestoreState(bytes);
            if (!_containers.TryAdd(container.Position, container))
            {
                throw new InvalidDataException("Container store checkpoint contains duplicate positions.");
            }
        }
    }
}

public sealed record ContainerActionResult(bool Succeeded, string? ErrorCode, int Quantity)
{
    public static ContainerActionResult Success(int quantity) => new(true, null, quantity);

    public static ContainerActionResult Failure(string errorCode) => new(false, errorCode, 0);
}

public static class ContainerActionSystem
{
    public static ContainerActionResult Deposit(
        AgentBody agent,
        ContainerInventory container,
        BlockActionRules rules,
        ushort itemTypeId,
        int quantity,
        IEnumerable<AccessPrincipal>? groupPrincipals = null,
        bool targetPerceived = true)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (!targetPerceived)
        {
            return ContainerActionResult.Failure("not_perceived");
        }

        if (!BlockActionSystem.IsAdjacent(agent.Position, container.Position))
        {
            return ContainerActionResult.Failure("not_adjacent");
        }

        var principals = GetPrincipals(agent.AgentId, groupPrincipals);
        if (!container.IsAuthorized(principals, ContainerAccessRights.Put))
        {
            return ContainerActionResult.Failure("access_denied");
        }

        if (!rules.TryGetItem(itemTypeId, out var item))
        {
            return ContainerActionResult.Failure("unknown_item");
        }

        if (agent.Inventory.Count(itemTypeId) < quantity)
        {
            return ContainerActionResult.Failure("item_missing");
        }

        if (!container.CanAdd(itemTypeId, quantity, item.MaxStackSize))
        {
            return ContainerActionResult.Failure("capacity_reached");
        }

        if (!agent.Inventory.TryTake(itemTypeId, quantity, out var removedStacks))
        {
            return ContainerActionResult.Failure("item_missing");
        }

        if (!container.TryAddStacks(removedStacks))
        {
            throw new InvalidOperationException("Container capacity changed after a successful preflight.");
        }

        return ContainerActionResult.Success(quantity);
    }

    public static ContainerActionResult Take(
        AgentBody agent,
        ContainerInventory container,
        BlockActionRules rules,
        ushort itemTypeId,
        int quantity,
        IEnumerable<AccessPrincipal>? groupPrincipals = null,
        bool targetPerceived = true,
        long tick = -1,
        MatterLedger? ledger = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if ((ledger is null) != (tick < 0))
        {
            throw new ArgumentException("Aging a container requires both the current tick and a ledger.");
        }

        if (!targetPerceived)
        {
            return ContainerActionResult.Failure("not_perceived");
        }

        if (!BlockActionSystem.IsAdjacent(agent.Position, container.Position))
        {
            return ContainerActionResult.Failure("not_adjacent");
        }

        var principals = GetPrincipals(agent.AgentId, groupPrincipals);
        if (!container.IsAuthorized(principals, ContainerAccessRights.Take))
        {
            return ContainerActionResult.Failure("access_denied");
        }

        if (!rules.TryGetItem(itemTypeId, out var item))
        {
            return ContainerActionResult.Failure("unknown_item");
        }

        if (!container.CanTake(itemTypeId, quantity))
        {
            return ContainerActionResult.Failure("container_empty");
        }

        if (ledger is not null && container.ApplyDecay(tick, ledger).Count > 0 &&
            !container.CanTake(itemTypeId, quantity))
        {
            return ContainerActionResult.Failure("item_spoiled");
        }

        if (!agent.Inventory.CanAdd(itemTypeId, quantity, item.MaxStackSize))
        {
            return ContainerActionResult.Failure("capacity_reached");
        }

        if (!container.TryTake(itemTypeId, quantity, out var removedStacks))
        {
            throw new InvalidOperationException("Container contents changed after a successful preflight.");
        }

        if (!agent.Inventory.TryAddStacks(removedStacks))
        {
            throw new InvalidOperationException("Agent inventory capacity changed after a successful preflight.");
        }

        return ContainerActionResult.Success(quantity);
    }

    private static HashSet<AccessPrincipal> GetPrincipals(
        ulong agentId,
        IEnumerable<AccessPrincipal>? groupPrincipals)
    {
        var principals = new HashSet<AccessPrincipal> { AccessPrincipal.ForAgent(agentId) };
        if (groupPrincipals is not null)
        {
            foreach (var principal in groupPrincipals)
            {
                if (principal.Kind != AccessPrincipalKind.Group)
                {
                    throw new ArgumentException("Only opaque group principals may be supplied here.", nameof(groupPrincipals));
                }

                principals.Add(principal);
            }
        }

        return principals;
    }
}
