using System.Text.Json;
using Prism.Contracts;
using Prism.Core.World;

namespace Prism.Viewer;

public enum ViewerLayer
{
    Zones,
    Resources,
    Freshness,
    Paths,
    Structures,
    Ownership,
    Construction,
}

public sealed record ViewerAgentSample(
    ulong AgentId,
    float X,
    float Y,
    float Z,
    AgentItemStack[] Inventory,
    string? ActiveAction);

public sealed record ViewerGroundItemSample(
    ulong Id,
    int X,
    int Y,
    int Z,
    ushort ItemTypeId,
    int Quantity);

public sealed class ViewerEntityStore
{
    private const float MilliCellsPerCell = 1000f;
    private readonly Dictionary<ulong, AgentEntry> _agents = [];
    private readonly List<ViewerGroundItemSample> _groundItems = [];
    private readonly HashSet<ViewerLayer> _layers = [];
    private long _revision;

    public long Revision => _revision;

    public int AgentCount => _agents.Count;

    public IReadOnlyList<ViewerGroundItemSample> GroundItems => _groundItems;

    public IReadOnlyCollection<ViewerLayer> EnabledLayers => _layers;

    public void ReplaceAgents(IEnumerable<AgentEntitySnapshot> agents, long tick = 0)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        _agents.Clear();
        foreach (var agent in agents)
        {
            ArgumentNullException.ThrowIfNull(agent);
            _agents.Add(agent.AgentId, new AgentEntry(agent, agent, tick, tick));
        }

        _revision++;
    }

    public void ApplySnapshot(
        IReadOnlyList<AgentEntitySnapshot> agents,
        IReadOnlyList<GroundItem> groundItems,
        long tick)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(groundItems);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        var seen = new HashSet<ulong>();
        foreach (var agent in agents)
        {
            ArgumentNullException.ThrowIfNull(agent);
            if (!seen.Add(agent.AgentId))
            {
                continue;
            }

            if (_agents.TryGetValue(agent.AgentId, out var entry))
            {
                entry.Previous = entry.Current;
                entry.PreviousTick = entry.CurrentTick;
                entry.Current = agent;
                entry.CurrentTick = tick;
            }
            else
            {
                _agents.Add(agent.AgentId, new AgentEntry(agent, agent, tick, tick));
            }
        }

        foreach (var removed in _agents.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            _agents.Remove(removed);
        }

        _groundItems.Clear();
        foreach (var item in groundItems)
        {
            _groundItems.Add(new ViewerGroundItemSample(
                item.Id,
                item.Position.X,
                item.Position.Y,
                item.Position.Z,
                item.Stack.ItemTypeId,
                item.Stack.Quantity));
        }

        _revision++;
    }

    public void ApplyTickResult(TickResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        foreach (var eventEnvelope in result.Events)
        {
            if (eventEnvelope.AgentId is not { } agentId ||
                !_agents.TryGetValue(agentId, out var entry))
            {
                continue;
            }

            switch (eventEnvelope.Kind)
            {
                case "action_completed":
                case "action_failed":
                    entry.ActiveAction = null;
                    break;
                case "macro_step_completed":
                    entry.ActiveAction = ReadStep(eventEnvelope.Payload) ?? entry.ActiveAction;
                    break;
            }
        }

        _revision++;
    }

    public bool ApplyObservation(ObservationEnvelope observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Self.ValueKind != JsonValueKind.Object ||
            !observation.Self.TryGetProperty("position", out var position) ||
            !TryReadPosition(position, out var parsedPosition))
        {
            return false;
        }

        var inventory = ReadInventory(observation.Self);
        if (_agents.TryGetValue(observation.AgentId, out var entry))
        {
            if (observation.Tick < entry.CurrentTick)
            {
                return false;
            }

            entry.Previous = entry.Current;
            entry.PreviousTick = entry.CurrentTick;
            entry.Current = new AgentEntitySnapshot(
                observation.AgentId,
                parsedPosition,
                entry.Current.Profile,
                inventory);
            entry.CurrentTick = observation.Tick;
        }
        else
        {
            var snapshot = new AgentEntitySnapshot(
                observation.AgentId,
                parsedPosition,
                new AgentBodyProfile(),
                inventory);
            _agents.Add(
                observation.AgentId,
                new AgentEntry(snapshot, snapshot, observation.Tick, observation.Tick));
        }

        _revision++;
        return true;
    }

    public IReadOnlyList<ViewerAgentSample> SampleAgents(float interpolation)
    {
        var amount = Math.Clamp(interpolation, 0f, 1f);
        var samples = new List<ViewerAgentSample>(_agents.Count);
        foreach (var entry in _agents.Values.OrderBy(static entry => entry.Current.AgentId))
        {
            var from = entry.Previous.Position;
            var to = entry.Current.Position;
            samples.Add(new ViewerAgentSample(
                entry.Current.AgentId,
                (from.X + (to.X - from.X) * amount) / MilliCellsPerCell,
                (from.Y + (to.Y - from.Y) * amount) / MilliCellsPerCell,
                (from.Z + (to.Z - from.Z) * amount) / MilliCellsPerCell,
                entry.Current.Inventory,
                entry.ActiveAction));
        }

        return samples;
    }

    public bool TryGetAgent(ulong agentId, out ViewerAgentSample? sample, float interpolation = 1f)
    {
        if (!_agents.TryGetValue(agentId, out var entry))
        {
            sample = null;
            return false;
        }

        var amount = Math.Clamp(interpolation, 0f, 1f);
        var from = entry.Previous.Position;
        var to = entry.Current.Position;
        sample = new ViewerAgentSample(
            agentId,
            (from.X + (to.X - from.X) * amount) / MilliCellsPerCell,
            (from.Y + (to.Y - from.Y) * amount) / MilliCellsPerCell,
            (from.Z + (to.Z - from.Z) * amount) / MilliCellsPerCell,
            entry.Current.Inventory,
            entry.ActiveAction);
        return true;
    }

    public bool IsLayerEnabled(ViewerLayer layer) => _layers.Contains(layer);

    public bool ToggleLayer(ViewerLayer layer)
    {
        if (_layers.Remove(layer))
        {
            return false;
        }

        _layers.Add(layer);
        return true;
    }

    public void SetLayer(ViewerLayer layer, bool enabled)
    {
        if (enabled)
        {
            _layers.Add(layer);
        }
        else
        {
            _layers.Remove(layer);
        }
    }

    private static string? ReadStep(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("step", out var step) ||
            step.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return step.GetString();
    }

    private static AgentItemStack[] ReadInventory(JsonElement self)
    {
        if (!self.TryGetProperty("inventory", out var inventory) ||
            inventory.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var stacks = new List<AgentItemStack>();
        foreach (var stack in inventory.EnumerateArray())
        {
            if (stack.ValueKind != JsonValueKind.Object ||
                !stack.TryGetProperty("itemTypeId", out var itemTypeId) ||
                !itemTypeId.TryGetUInt16(out var parsedItemTypeId) ||
                !stack.TryGetProperty("quantity", out var quantity) ||
                !quantity.TryGetInt32(out var parsedQuantity) ||
                !stack.TryGetProperty("maxStackSize", out var maxStackSize) ||
                !maxStackSize.TryGetInt32(out var parsedMaxStackSize))
            {
                continue;
            }

            int? durability = null;
            if (stack.TryGetProperty("durabilityRemaining", out var durabilityValue) &&
                durabilityValue.TryGetInt32(out var parsedDurability))
            {
                durability = parsedDurability;
            }

            stacks.Add(new AgentItemStack(
                parsedItemTypeId,
                parsedQuantity,
                parsedMaxStackSize,
                durability));
        }

        return stacks.ToArray();
    }

    private static bool TryReadPosition(JsonElement value, out AgentPosition position)
    {
        position = default;
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("x", out var x) ||
            !value.TryGetProperty("y", out var y) ||
            !value.TryGetProperty("z", out var z) ||
            !x.TryGetInt32(out var parsedX) ||
            !y.TryGetInt32(out var parsedY) ||
            !z.TryGetInt32(out var parsedZ))
        {
            return false;
        }

        position = new AgentPosition(parsedX, parsedY, parsedZ);
        return true;
    }

    private sealed class AgentEntry(
        AgentEntitySnapshot previous,
        AgentEntitySnapshot current,
        long previousTick,
        long currentTick)
    {
        public AgentEntitySnapshot Previous { get; set; } = previous;
        public AgentEntitySnapshot Current { get; set; } = current;
        public long PreviousTick { get; set; } = previousTick;
        public long CurrentTick { get; set; } = currentTick;
        public string? ActiveAction { get; set; }
    }
}
