using System.Text.Json;
using Prism.Core.Accounting;
using Prism.Core.Actions;
using Prism.Core.Perception;
using Prism.Core.Navigation;

namespace Prism.Core.World;

public sealed record AgentEntitySnapshot(
    ulong AgentId,
    AgentPosition Position,
    AgentBodyProfile Profile,
    AgentItemStack[] Inventory);

public sealed record AgentPerceptionSnapshot(ulong AgentId, PerceptionFrame Frame);

public sealed record AgentSpoilage(ulong AgentId, Accounting.SpoilageEvent Spoilage);

public sealed record MoveOrderStatus(
    ulong AgentId,
    string CommandId,
    bool Completed,
    string? ErrorCode,
    int StepsRemaining,
    BlockCoordinate Target);

public sealed class AgentLifecycleSystem
{
    private const int CheckpointVersion = 2;
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WorldGrid _world;
    private MovementGraph _movement;
    private readonly PerceptionSystem _perception = new();
    private PerceptionRules? _perceptionRules;
    private readonly PerceptionSettings _perceptionSettings;
    private AgentRegistry _agents = new();
    private GroundItemStore _groundItems = new();
    private readonly SortedDictionary<ulong, MoveOrderState> _moveOrders = [];
    private long _revision;

    public AgentLifecycleSystem(
        WorldGrid world,
        MovementRules rules,
        PerceptionRules? perceptionRules = null,
        PerceptionSettings? perceptionSettings = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        _world = world;
        _movement = new MovementGraph(world, rules);
        _perceptionRules = perceptionRules;
        _perceptionSettings = perceptionSettings ?? new PerceptionSettings();
        _perceptionSettings.Validate();
    }

    public int AgentCount => _agents.Count;
    public int GroundItemCount => _groundItems.Count;
    public int MoveOrderCount => _moveOrders.Count;
    public long Revision => _revision;

    public AgentEntitySnapshot[] SnapshotAgents() =>
        _agents.Agents
            .Select(agent => new AgentEntitySnapshot(
                agent.AgentId,
                agent.Position,
                agent.Profile,
                agent.Inventory.Snapshot().ToArray()))
            .ToArray();

    public GroundItem[] SnapshotGroundItems() => _groundItems.Snapshot().ToArray();

    public bool TryGetAgent(ulong agentId, out AgentBody? agent) =>
        _agents.TryGet(agentId, out agent);

    public bool HasPerceivedBlock(ulong agentId, BlockCoordinate position) =>
        _perception.HasPerceivedBlock(agentId, position);

    public bool HasPerceivedAgent(ulong observerAgentId, ulong targetAgentId) =>
        _perception.HasPerceivedAgent(observerAgentId, targetAgentId);

    public IReadOnlyList<PerceptionEntry> GetPerceivedEntries(ulong agentId) =>
        _perception.GetPerceivedEntries(agentId);

    public bool TryMoveOneStep(ulong agentId, BlockCoordinate target, out string? errorCode)
    {
        errorCode = null;
        if (!_agents.TryGet(agentId, out var agent))
        {
            errorCode = "agent_not_found";
            return false;
        }

        var path = AStarPathfinder.FindPath(
            _movement,
            agent!.Position.CellX,
            agent.Position.CellY,
            target.X,
            target.Y,
            maximumExpandedNodes: Math.Max(1, checked(_world.Width * _world.Height)));
        if (path.Status != PathfindingStatus.Found)
        {
            errorCode = path.Status == PathfindingStatus.NodeBudgetExceeded
                ? "path_budget_exceeded"
                : "path_not_found";
            return false;
        }

        if (path.Path.Count <= 1)
        {
            return true;
        }

        EnsureRevisionAvailable();
        var next = path.Path[1];
        agent.Position = AgentPosition.AtCell(next.X, next.Y, next.FeetZ);
        _revision++;
        return true;
    }

    public bool StartMoveOrder(
        ulong agentId,
        string commandId,
        BlockCoordinate target,
        out string? errorCode,
        out int stepCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        errorCode = null;
        stepCount = 0;
        if (!_agents.TryGet(agentId, out var agent))
        {
            errorCode = "agent_not_found";
            return false;
        }

        if ((uint)target.X >= (uint)_world.Width || (uint)target.Y >= (uint)_world.Height)
        {
            errorCode = "target_out_of_bounds";
            return false;
        }

        var path = AStarPathfinder.FindPath(
            _movement,
            agent!.Position.CellX,
            agent.Position.CellY,
            target.X,
            target.Y,
            maximumExpandedNodes: Math.Max(1, checked(_world.Width * _world.Height)));
        if (path.Status != PathfindingStatus.Found)
        {
            errorCode = path.Status == PathfindingStatus.NodeBudgetExceeded
                ? "path_budget_exceeded"
                : "path_not_found";
            return false;
        }

        var start = new BlockCoordinate(agent.Position.CellX, agent.Position.CellY, agent.Position.FeetZ);
        var steps = path.Path
            .Skip(1)
            .Select(static node => new BlockCoordinate(node.X, node.Y, node.FeetZ))
            .ToArray();
        EnsureRevisionAvailable();
        _moveOrders[agentId] = new MoveOrderState(
            commandId,
            target,
            start,
            steps,
            NextStep: 0);
        stepCount = steps.Length;
        _revision++;
        return true;
    }

    public bool CancelMoveOrder(ulong agentId) => _moveOrders.Remove(agentId);

    public IReadOnlyList<MoveOrderStatus> AdvanceMoveOrders()
    {
        if (_moveOrders.Count == 0)
        {
            return [];
        }

        var statuses = new List<MoveOrderStatus>();
        foreach (var agentId in _moveOrders.Keys.ToArray())
        {
            var order = _moveOrders[agentId];
            if (!_agents.TryGet(agentId, out var agent))
            {
                _moveOrders.Remove(agentId);
                statuses.Add(new MoveOrderStatus(
                    agentId,
                    order.CommandId,
                    Completed: false,
                    "agent_not_found",
                    order.Steps.Length - order.NextStep,
                    order.Target));
                continue;
            }

            if (order.NextStep >= order.Steps.Length)
            {
                _moveOrders.Remove(agentId);
                statuses.Add(new MoveOrderStatus(
                    agentId,
                    order.CommandId,
                    Completed: true,
                    null,
                    0,
                    order.Target));
                continue;
            }

            var expected = order.NextStep == 0
                ? order.Start
                : order.Steps[order.NextStep - 1];
            if (agent!.Position.CellX != expected.X ||
                agent.Position.CellY != expected.Y ||
                agent.Position.FeetZ != expected.Z)
            {
                _moveOrders.Remove(agentId);
                statuses.Add(new MoveOrderStatus(
                    agentId,
                    order.CommandId,
                    Completed: false,
                    "path_interrupted",
                    order.Steps.Length - order.NextStep,
                    order.Target));
                continue;
            }

            var next = order.Steps[order.NextStep];
            EnsureRevisionAvailable();
            agent.Position = AgentPosition.AtCell(next.X, next.Y, next.Z);
            _revision++;
            var remaining = order.Steps.Length - (order.NextStep + 1);
            if (remaining == 0)
            {
                _moveOrders.Remove(agentId);
                statuses.Add(new MoveOrderStatus(
                    agentId,
                    order.CommandId,
                    Completed: true,
                    null,
                    0,
                    order.Target));
                continue;
            }

            _moveOrders[agentId] = order with { NextStep = order.NextStep + 1 };
        }

        return statuses;
    }

    public bool TakeGroundItem(
        ulong agentId,
        ulong groundItemId,
        int quantity,
        BlockActionRules rules,
        out string? errorCode)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        errorCode = null;
        if (!_agents.TryGet(agentId, out var agent))
        {
            errorCode = "agent_not_found";
            return false;
        }

        if (!_groundItems.TryGet(groundItemId, out var groundItem) ||
            !_perception.GetPerceivedEntries(agentId).Any(entry =>
                entry.Kind == PerceptionEntityKind.GroundItem &&
                entry.Id == groundItemId))
        {
            errorCode = "source_not_found";
            return false;
        }

        if (!rules.TryGetItem(groundItem.Stack.ItemTypeId, out var itemRule))
        {
            errorCode = "unknown_item";
            return false;
        }

        if (!BlockActionSystem.IsAdjacent(agent!.Position, groundItem.Position))
        {
            errorCode = "not_adjacent";
            return false;
        }

        if (quantity > groundItem.Stack.Quantity)
        {
            errorCode = "item_missing";
            return false;
        }

        if (groundItem.Stack.MaxStackSize != itemRule.MaxStackSize)
        {
            errorCode = "inventory_stack_limit_mismatch";
            return false;
        }

        var stack = groundItem.Stack with { Quantity = quantity };
        if (!agent.Inventory.CanAdd(stack.ItemTypeId, quantity, stack.MaxStackSize))
        {
            errorCode = "capacity_reached";
            return false;
        }

        EnsureRevisionAvailable();
        if (!_groundItems.TryTake(groundItemId, quantity, out var removed) ||
            !agent.Inventory.TryAddStacks([removed]))
        {
            throw new InvalidOperationException("Ground-item pickup changed after successful preflight.");
        }

        _revision++;
        return true;
    }

    public bool IsWithinPerceptionRadius(ulong agentId, BlockCoordinate position)
    {
        if (!_agents.TryGet(agentId, out var agent))
        {
            return false;
        }

        var dx = (long)agent!.Position.CellX - position.X;
        var dy = (long)agent.Position.CellY - position.Y;
        var radius = _perceptionSettings.NearRadius;
        return dx * dx + dy * dy <= (long)radius * radius;
    }

    public BlockActionResult MineBlock(
        ulong agentId,
        BlockActionRules rules,
        MatterLedger ledger,
        ulong runSeed,
        long tick,
        BlockCoordinate target,
        int masteryPermille = 0)
    {
        if (!_agents.TryGet(agentId, out var agent))
        {
            return BlockActionResult.Failure("agent_not_found");
        }

        var result = BlockActionSystem.Mine(
            _world,
            agent!,
            _groundItems,
            rules,
            ledger,
            runSeed,
            tick,
            target,
            _perception.HasPerceivedBlock(agentId, target),
            masteryPermille);
        if (result.Succeeded)
        {
            _revision = checked(_revision + 1);
        }

        return result;
    }

    public BlockActionResult PlaceBlock(
        ulong agentId,
        BlockActionRules rules,
        MatterLedger ledger,
        BlockCoordinate target,
        ushort itemTypeId,
        ushort materialId)
    {
        if (!_agents.TryGet(agentId, out var agent))
        {
            return BlockActionResult.Failure("agent_not_found");
        }

        var result = BlockActionSystem.Place(
            _world,
            agent!,
            rules,
            ledger,
            target,
            itemTypeId,
            materialId,
            _agents,
            IsWithinPerceptionRadius(agentId, target));
        if (result.Succeeded)
        {
            _revision = checked(_revision + 1);
        }

        return result;
    }

    public void NotifyAgentMutation() => _revision = checked(_revision + 1);

    public AgentPerceptionSnapshot[] ObserveAgents(
        IReadOnlyList<StructureConstructionSite>? constructionSites = null,
        StructureEffectConsumer? structureEffects = null,
        GroupMembership? groupMembership = null)
    {
        if (_perceptionRules is null)
        {
            return [];
        }

        var activeAgents = _agents.Agents.ToArray();
        var activeIds = activeAgents.Select(static agent => agent.AgentId).ToHashSet();
        foreach (var previousAgentId in _perception.KnownAgents.Where(id => !activeIds.Contains(id)))
        {
            _perception.ForgetAgent(previousAgentId);
        }

        return activeAgents
            .Select(agent => new AgentPerceptionSnapshot(
                agent.AgentId,
                _perception.Observe(
                    agent,
                    _world,
                    _agents,
                    _groundItems,
                    _perceptionRules,
                    _perceptionSettings,
                    constructionSites,
                    structureEffects is null
                        ? null
                        : groupMembership is null
                            ? structureEffects.QueryShelter(
                                agent.AgentId,
                                new BlockCoordinate(
                                    agent.Position.CellX,
                                    agent.Position.CellY,
                                    agent.Position.FeetZ))
                            : structureEffects.QueryShelterWithGroups(
                                agent.AgentId,
                                new BlockCoordinate(
                                    agent.Position.CellX,
                                    agent.Position.CellY,
                                    agent.Position.FeetZ),
                                groupMembership))))
            .ToArray();
    }

    public AgentBody Incarnate(ulong agentId, AgentPosition position, AgentBodyProfile profile)
    {
        EnsureRevisionAvailable();
        var agent = _agents.Incarnate(agentId, position, profile, _movement);
        _revision++;
        return agent;
    }

    public bool Retire(ulong agentId)
    {
        if (!_agents.TryGet(agentId, out _))
        {
            return false;
        }

        EnsureRevisionAvailable();
        if (!_agents.Retire(
                agentId,
                _groundItems))
        {
            return false;
        }

        _revision++;
        return true;
    }

    private void EnsureRevisionAvailable()
    {
        if (_revision == long.MaxValue)
        {
            throw new InvalidOperationException("The agent lifecycle revision space is exhausted.");
        }
    }

    public IReadOnlyList<AgentSpoilage> AdvanceFreshness(
        long tick,
        int decayPermillePerTick,
        Accounting.MatterLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        var spoiled = new List<AgentSpoilage>();
        foreach (var agent in _agents.Agents)
        {
            foreach (var spoilage in agent.Inventory.ApplyDecay(tick, decayPermillePerTick, ledger))
            {
                spoiled.Add(new AgentSpoilage(agent.AgentId, spoilage));
            }
        }

        return spoiled;
    }

    public byte[] CaptureState()
    {
        var rules = _movement.Rules;
        var checkpoint = new AgentLifecycleCheckpoint(
            CheckpointVersion,
            rules.SolidMaterials.Order().ToArray(),
            rules.LiquidMaterials.Order().ToArray(),
            rules.MaximumAscent,
            rules.MaximumDescent,
            rules.PathMaterials.Order().ToArray(),
            rules.PathSpeedupPermille,
            _revision,
            _agents.Agents
                .Select(agent => new AgentCheckpoint(
                    agent.AgentId,
                    agent.Position,
                    agent.Profile,
                    agent.Inventory.CaptureSlots()))
                .ToArray(),
            _groundItems.Snapshot().ToArray(),
            _groundItems.NextId,
            _perceptionRules?.AirMaterialId,
            _perceptionRules?.SnapshotMaterials()
                .Select(static entry => new PerceptionMaterialCheckpoint(entry.Key, entry.Value))
                .ToArray(),
            _perception.CaptureState(),
            _moveOrders
                .Select(static entry => new MoveOrderCheckpoint(
                    entry.Key,
                    entry.Value.CommandId,
                    entry.Value.Target,
                    entry.Value.Start,
                    entry.Value.Steps,
                    entry.Value.NextStep))
                .ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(checkpoint, CheckpointJsonOptions);
    }

    public void RestoreState(ReadOnlySpan<byte> checkpointBytes)
    {
        if (_agents.Count != 0 || _groundItems.Count != 0 || _moveOrders.Count != 0 || _revision != 0)
        {
            throw new InvalidOperationException("Agent lifecycle state can only be restored into a new system.");
        }

        var checkpoint = JsonSerializer.Deserialize<AgentLifecycleCheckpoint>(
                             checkpointBytes,
                             CheckpointJsonOptions)
                         ?? throw new InvalidDataException("Agent lifecycle checkpoint is empty.");
        if (checkpoint.Version is not (1 or CheckpointVersion) ||
            checkpoint.SolidMaterials is null ||
            checkpoint.LiquidMaterials is null ||
            checkpoint.PathMaterials is null ||
            checkpoint.Agents is null ||
            checkpoint.GroundItems is null ||
            checkpoint.Revision < 0)
        {
            throw new InvalidDataException("Agent lifecycle checkpoint version or contents are invalid.");
        }

        MovementRules rules;
        try
        {
            rules = new MovementRules(
                checkpoint.SolidMaterials,
                checkpoint.LiquidMaterials,
                checkpoint.MaximumAscent,
                checkpoint.MaximumDescent,
                checkpoint.PathMaterials,
                checkpoint.PathSpeedupPermille);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Agent lifecycle checkpoint movement rules are invalid.", exception);
        }

        var movement = new MovementGraph(_world, rules);
        var agents = new AgentRegistry();
        foreach (var entry in checkpoint.Agents)
        {
            if (entry is null || entry.Profile is null || entry.InventorySlots is null)
            {
                throw new InvalidDataException("Agent lifecycle checkpoint contains an invalid agent.");
            }

            AgentBody agent;
            try
            {
                entry.Profile.Validate();
                agent = agents.Incarnate(entry.AgentId, entry.Position, entry.Profile, movement);
            }
            catch (Exception exception) when (exception is AgentLifecycleException or ArgumentException)
            {
                throw new InvalidDataException(
                    $"Agent lifecycle checkpoint contains invalid agent '{entry.AgentId}'.",
                    exception);
            }

            agent.Inventory.RestoreSlots(entry.InventorySlots);
        }

        var groundItems = new GroundItemStore();
        groundItems.Restore(checkpoint.GroundItems, checkpoint.NextGroundItemId);
        if (checkpoint.GroundItems.Any(item =>
                (uint)item.Position.X >= (uint)_world.Width ||
                (uint)item.Position.Y >= (uint)_world.Height ||
                (uint)item.Position.Z >= (uint)_world.VerticalSize))
        {
            throw new InvalidDataException("Agent lifecycle checkpoint contains ground items outside the world.");
        }

        PerceptionRules? perceptionRules = null;
        if (checkpoint.PerceptionAirMaterialId is { } airMaterialId ||
            checkpoint.PerceptionMaterials is not null)
        {
            if (checkpoint.PerceptionAirMaterialId is not { } validAirMaterialId ||
                checkpoint.PerceptionMaterials is null)
            {
                throw new InvalidDataException("Agent lifecycle checkpoint perception rules are incomplete.");
            }

            var materials = new Dictionary<ushort, PerceptionMaterialRule>();
            foreach (var material in checkpoint.PerceptionMaterials)
            {
                if (material is null || !materials.TryAdd(material.MaterialId, material.Rule))
                {
                    throw new InvalidDataException("Agent lifecycle checkpoint perception rules are invalid.");
                }
            }

            try
            {
                perceptionRules = new PerceptionRules(materials, validAirMaterialId);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Agent lifecycle checkpoint perception rules are invalid.", exception);
            }
        }

        if (checkpoint.PerceptionState is not null)
        {
            _perception.RestoreState(checkpoint.PerceptionState);
        }

        if (checkpoint.MoveOrders is not null)
        {
            foreach (var order in checkpoint.MoveOrders)
            {
                if (order is null ||
                    string.IsNullOrWhiteSpace(order.CommandId) ||
                    order.Steps is null ||
                    order.NextStep < 0 ||
                    order.NextStep > order.Steps.Length ||
                    !agents.TryGet(order.AgentId, out _) ||
                    !_moveOrders.TryAdd(order.AgentId, new MoveOrderState(
                        order.CommandId,
                        order.Target,
                        order.Start,
                        order.Steps,
                        order.NextStep)))
                {
                    throw new InvalidDataException(
                        "Agent lifecycle checkpoint contains an invalid move order.");
                }
            }
        }

        _movement = movement;
        _agents = agents;
        _groundItems = groundItems;
        _perceptionRules = perceptionRules;
        _revision = checkpoint.Revision;
    }

    private sealed record AgentLifecycleCheckpoint(
        int Version,
        ushort[] SolidMaterials,
        ushort[] LiquidMaterials,
        int MaximumAscent,
        int MaximumDescent,
        ushort[] PathMaterials,
        int PathSpeedupPermille,
        long Revision,
        AgentCheckpoint[] Agents,
        GroundItem[] GroundItems,
        ulong NextGroundItemId,
        ushort? PerceptionAirMaterialId = null,
        PerceptionMaterialCheckpoint[]? PerceptionMaterials = null,
        byte[]? PerceptionState = null,
        MoveOrderCheckpoint[]? MoveOrders = null);

    private sealed record MoveOrderCheckpoint(
        ulong AgentId,
        string CommandId,
        BlockCoordinate Target,
        BlockCoordinate Start,
        BlockCoordinate[] Steps,
        int NextStep);

    private sealed record MoveOrderState(
        string CommandId,
        BlockCoordinate Target,
        BlockCoordinate Start,
        BlockCoordinate[] Steps,
        int NextStep);

    private sealed record AgentCheckpoint(
        ulong AgentId,
        AgentPosition Position,
        AgentBodyProfile Profile,
        AgentItemStack?[] InventorySlots);

    private sealed record PerceptionMaterialCheckpoint(
        ushort MaterialId,
        PerceptionMaterialRule Rule);
}
