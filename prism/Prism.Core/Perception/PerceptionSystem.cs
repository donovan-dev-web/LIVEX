using System.Collections.Concurrent;
using Prism.Core.World;

namespace Prism.Core.Perception;

public sealed record PerceptionMaterialRule(
    bool Solid,
    bool Liquid,
    bool Perceivable,
    uint TagMask);

public sealed class PerceptionRules
{
    private readonly IReadOnlyDictionary<ushort, PerceptionMaterialRule> _materials;

    public PerceptionRules(
        IReadOnlyDictionary<ushort, PerceptionMaterialRule> materials,
        ushort airMaterialId)
    {
        ArgumentNullException.ThrowIfNull(materials);
        _materials = new Dictionary<ushort, PerceptionMaterialRule>(materials);
        if (!_materials.ContainsKey(airMaterialId))
        {
            throw new ArgumentException("Perception rules must include the air material.", nameof(materials));
        }

        AirMaterialId = airMaterialId;
    }

    public ushort AirMaterialId { get; }

    internal PerceptionMaterialRule GetMaterial(ushort materialId) =>
        _materials.TryGetValue(materialId, out var rule)
            ? rule
            : throw new InvalidOperationException($"Perception rules do not define material '{materialId}'.");
}

public sealed record PerceptionSettings(
    int NearRadius = 16,
    int MidRadiusRegions = 3,
    int MaximumObservedPerAgent = 256)
{
    public void Validate()
    {
        if (NearRadius is < 0 or >= 64)
        {
            throw new ArgumentOutOfRangeException(nameof(NearRadius));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(MidRadiusRegions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumObservedPerAgent);
    }
}

public enum PerceptionEntityKind : byte
{
    Block,
    GroundItem,
    Agent,
    RegionSummary,
}

public enum PerceptionChangeKind : byte
{
    Appeared,
    Changed,
    Disappeared,
}

public readonly record struct PerceptionEntry(
    PerceptionEntityKind Kind,
    int X,
    int Y,
    int Z,
    ulong Id,
    ushort MaterialId,
    byte State,
    ushort ItemTypeId,
    int Quantity,
    uint TagMask);

public readonly record struct PerceptionChange(
    PerceptionChangeKind Change,
    PerceptionEntry Entry);

public sealed record SelfObservation(
    AgentPosition Position,
    IReadOnlyList<AgentItemStack> Inventory);

public sealed record PerceptionFrame(
    IReadOnlyList<PerceptionChange> Changes,
    SelfObservation Self);

public sealed class PerceptionSystem
{
    private readonly ConcurrentDictionary<ulong, SortedDictionary<ObservationKey, PerceptionEntry>> _previous = [];

    public PerceptionFrame Observe(
        AgentBody observer,
        WorldGrid world,
        AgentRegistry agents,
        GroundItemStore groundItems,
        PerceptionRules rules,
        PerceptionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(groundItems);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        var candidates = new SortedDictionary<ObservationKey, PerceptionEntry>();
        AddNearbyBlocks(candidates, observer.Position, world, rules, settings.NearRadius);
        AddNearbyGroundItems(candidates, observer.Position, groundItems, settings.NearRadius);
        AddNearbyAgents(candidates, observer, agents, settings.NearRadius);
        AddRegionSummaries(candidates, observer.Position, world, rules, settings.MidRadiusRegions);

        var previous = _previous.TryGetValue(observer.AgentId, out var last)
            ? last
            : new SortedDictionary<ObservationKey, PerceptionEntry>();
        var pendingChanges = new List<(ObservationKey Key, PerceptionChange Change)>();
        foreach (var (key, entry) in candidates)
        {
            if (!previous.TryGetValue(key, out var oldEntry))
            {
                pendingChanges.Add((key, new PerceptionChange(PerceptionChangeKind.Appeared, entry)));
            }
            else if (oldEntry != entry)
            {
                pendingChanges.Add((key, new PerceptionChange(PerceptionChangeKind.Changed, entry)));
            }
        }

        foreach (var (key, entry) in previous)
        {
            if (!candidates.ContainsKey(key))
            {
                pendingChanges.Add((key, new PerceptionChange(PerceptionChangeKind.Disappeared, entry)));
            }
        }

        var selectedChanges = pendingChanges
            .OrderBy(static item => item.Change.Change == PerceptionChangeKind.Disappeared ? 0 : 1)
            .ThenBy(static item => GetPriority(item.Change.Entry.Kind))
            .ThenBy(static item => item.Key)
            .Take(settings.MaximumObservedPerAgent)
            .ToArray();
        var changes = selectedChanges.Select(static item => item.Change).ToArray();
        Array.Sort(changes, CompareChanges);

        var next = new SortedDictionary<ObservationKey, PerceptionEntry>();
        var sentKeys = selectedChanges.Select(static item => item.Key).ToHashSet();
        foreach (var (key, oldEntry) in previous)
        {
            if (candidates.TryGetValue(key, out var currentEntry))
            {
                next.Add(key, sentKeys.Contains(key) ? currentEntry : oldEntry);
            }
            else if (!sentKeys.Contains(key))
            {
                next.Add(key, oldEntry);
            }
        }

        foreach (var (key, entry) in selectedChanges)
        {
            if (entry.Change != PerceptionChangeKind.Disappeared && !next.ContainsKey(key))
            {
                next.Add(key, entry.Entry);
            }
        }

        _previous[observer.AgentId] = next;
        return new PerceptionFrame(
            Array.AsReadOnly(changes),
            new SelfObservation(observer.Position, Array.AsReadOnly(observer.Inventory.Snapshot().ToArray())));
    }

    public bool ForgetAgent(ulong agentId) => _previous.TryRemove(agentId, out _);

    private static int CompareChanges(PerceptionChange left, PerceptionChange right)
    {
        var comparison = GetPriority(left.Entry.Kind).CompareTo(GetPriority(right.Entry.Kind));
        if (comparison != 0) return comparison;
        comparison = left.Entry.Z.CompareTo(right.Entry.Z);
        if (comparison != 0) return comparison;
        comparison = left.Entry.Y.CompareTo(right.Entry.Y);
        if (comparison != 0) return comparison;
        comparison = left.Entry.X.CompareTo(right.Entry.X);
        if (comparison != 0) return comparison;
        comparison = left.Entry.Id.CompareTo(right.Entry.Id);
        return comparison != 0 ? comparison : left.Change.CompareTo(right.Change);
    }

    private static int GetPriority(PerceptionEntityKind kind) => kind switch
    {
        PerceptionEntityKind.Agent => 0,
        PerceptionEntityKind.GroundItem => 1,
        PerceptionEntityKind.Block => 2,
        PerceptionEntityKind.RegionSummary => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static void AddNearbyBlocks(
        SortedDictionary<ObservationKey, PerceptionEntry> candidates,
        AgentPosition position,
        WorldGrid world,
        PerceptionRules rules,
        int radius)
    {
        var centerX = position.CellX;
        var centerY = position.CellY;
        var radiusSquared = (long)radius * radius;
        var minX = (int)Math.Max(0L, (long)centerX - radius);
        var maxX = (int)Math.Min(world.Width - 1L, (long)centerX + radius);
        var minY = (int)Math.Max(0L, (long)centerY - radius);
        var maxY = (int)Math.Min(world.Height - 1L, (long)centerY + radius);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!WithinRadius(centerX, centerY, x, y, radiusSquared))
                {
                    continue;
                }

                var surface = world.GetSurfaceColumn(x, y);
                var coordinate = new BlockCoordinate(x, y, surface.SurfaceZ);
                var materialId = surface.MaterialId;
                var state = surface.State;
                if (world.TryGetBlockOverride(coordinate, out var surfaceOverride))
                {
                    if (surfaceOverride.IsAir)
                    {
                        continue;
                    }

                    materialId = surfaceOverride.MaterialId;
                    state = surfaceOverride.State;
                }

                AddBlockIfVisible(candidates, coordinate, materialId, state, world, rules);
            }
        }

        foreach (var (coordinate, block) in world.EnumerateOverrides())
        {
            if (block.IsAir || coordinate.Z < world.GetSurfaceColumn(coordinate.X, coordinate.Y).SurfaceZ ||
                !WithinRadius(centerX, centerY, coordinate.X, coordinate.Y, radiusSquared))
            {
                continue;
            }

            var surface = world.GetSurfaceColumn(coordinate.X, coordinate.Y);
            if (coordinate.Z == surface.SurfaceZ)
            {
                continue;
            }

            AddBlockIfVisible(candidates, coordinate, block.MaterialId, block.State, world, rules);
        }
    }

    private static void AddBlockIfVisible(
        SortedDictionary<ObservationKey, PerceptionEntry> candidates,
        BlockCoordinate coordinate,
        ushort materialId,
        byte state,
        WorldGrid world,
        PerceptionRules rules)
    {
        var material = rules.GetMaterial(materialId);
        if (!material.Perceivable || !IsExposed(coordinate, world, rules))
        {
            return;
        }

        var entry = new PerceptionEntry(
            PerceptionEntityKind.Block,
            coordinate.X,
            coordinate.Y,
            coordinate.Z,
            0,
            materialId,
            state,
            0,
            0,
            material.TagMask);
        candidates[new ObservationKey(entry.Kind, coordinate.X, coordinate.Y, coordinate.Z, 0)] = entry;
    }

    private static bool IsExposed(BlockCoordinate coordinate, WorldGrid world, PerceptionRules rules)
    {
        if (IsOpen(coordinate.X, coordinate.Y, coordinate.Z + 1, world, rules))
        {
            return true;
        }

        return IsOpen(coordinate.X - 1, coordinate.Y, coordinate.Z, world, rules) ||
               IsOpen(coordinate.X + 1, coordinate.Y, coordinate.Z, world, rules) ||
               IsOpen(coordinate.X, coordinate.Y - 1, coordinate.Z, world, rules) ||
               IsOpen(coordinate.X, coordinate.Y + 1, coordinate.Z, world, rules);
    }

    private static bool IsOpen(int x, int y, int z, WorldGrid world, PerceptionRules rules)
    {
        if ((uint)z >= (uint)world.VerticalSize || !world.TryGetSurfaceColumn(x, y, out var surface))
        {
            return false;
        }

        if (world.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var block))
        {
            return block.IsAir || IsTransparent(block.MaterialId, rules);
        }

        if (z > surface.SurfaceZ)
        {
            return true;
        }

        if (z == surface.SurfaceZ)
        {
            return IsTransparent(surface.MaterialId, rules);
        }

        return false;
    }

    private static bool IsTransparent(ushort materialId, PerceptionRules rules)
    {
        if (materialId == rules.AirMaterialId)
        {
            return true;
        }

        var material = rules.GetMaterial(materialId);
        return !material.Solid && !material.Liquid;
    }

    private static void AddNearbyGroundItems(
        SortedDictionary<ObservationKey, PerceptionEntry> candidates,
        AgentPosition position,
        GroundItemStore groundItems,
        int radius)
    {
        var radiusSquared = (long)radius * radius;
        foreach (var item in groundItems.Snapshot())
        {
            if (!WithinRadius(position.CellX, position.CellY, item.Position.X, item.Position.Y, radiusSquared))
            {
                continue;
            }

            var entry = new PerceptionEntry(
                PerceptionEntityKind.GroundItem,
                item.Position.X,
                item.Position.Y,
                item.Position.Z,
                item.Id,
                0,
                0,
                item.Stack.ItemTypeId,
                item.Stack.Quantity,
                0);
            candidates[new ObservationKey(entry.Kind, entry.X, entry.Y, entry.Z, entry.Id)] = entry;
        }
    }

    private static void AddNearbyAgents(
        SortedDictionary<ObservationKey, PerceptionEntry> candidates,
        AgentBody observer,
        AgentRegistry agents,
        int radius)
    {
        var radiusSquared = (long)radius * radius;
        foreach (var agent in agents.Agents)
        {
            if (agent.AgentId == observer.AgentId ||
                !WithinRadius(
                    observer.Position.CellX,
                    observer.Position.CellY,
                    agent.Position.CellX,
                    agent.Position.CellY,
                    radiusSquared))
            {
                continue;
            }

            var entry = new PerceptionEntry(
                PerceptionEntityKind.Agent,
                agent.Position.CellX,
                agent.Position.CellY,
                agent.Position.FeetZ,
                agent.AgentId,
                0,
                0,
                0,
                0,
                0);
            candidates[new ObservationKey(entry.Kind, entry.X, entry.Y, entry.Z, entry.Id)] = entry;
        }
    }

    private static void AddRegionSummaries(
        SortedDictionary<ObservationKey, PerceptionEntry> candidates,
        AgentPosition position,
        WorldGrid world,
        PerceptionRules rules,
        int radius)
    {
        if (!world.TryGetRegionId(position.CellX, position.CellY, out var centerRegion))
        {
            return;
        }

        var centerX = centerRegion % world.RegionCountX;
        var centerY = centerRegion / world.RegionCountX;
        for (var regionY = (int)Math.Max(0L, (long)centerY - radius);
             regionY <= (int)Math.Min(world.RegionCountY - 1L, (long)centerY + radius);
             regionY++)
        {
            for (var regionX = (int)Math.Max(0L, (long)centerX - radius);
                 regionX <= (int)Math.Min(world.RegionCountX - 1L, (long)centerX + radius);
                 regionX++)
            {
                var regionId = regionX + world.RegionCountX * regionY;
                var tagMask = GetExposedRegionTagMask(world, rules, regionX, regionY);
                if (tagMask == 0)
                {
                    continue;
                }

                var entry = new PerceptionEntry(
                    PerceptionEntityKind.RegionSummary,
                    regionX,
                    regionY,
                    0,
                    (ulong)regionId,
                    0,
                    0,
                    0,
                    0,
                    tagMask);
                candidates[new ObservationKey(entry.Kind, regionX, regionY, 0, (ulong)regionId)] = entry;
            }
        }
    }

    private static uint GetExposedRegionTagMask(
        WorldGrid world,
        PerceptionRules rules,
        int regionX,
        int regionY)
    {
        var mask = 0u;
        var minX = regionX * world.RegionSize;
        var minY = regionY * world.RegionSize;
        var maxX = Math.Min(world.Width, minX + world.RegionSize);
        var maxY = Math.Min(world.Height, minY + world.RegionSize);
        for (var y = minY; y < maxY; y++)
        {
            for (var x = minX; x < maxX; x++)
            {
                var surface = world.GetSurfaceColumn(x, y);
                var coordinate = new BlockCoordinate(x, y, surface.SurfaceZ);
                var materialId = surface.MaterialId;
                if (world.TryGetBlockOverride(coordinate, out var surfaceOverride))
                {
                    if (surfaceOverride.IsAir)
                    {
                        continue;
                    }

                    materialId = surfaceOverride.MaterialId;
                }

                if (IsExposed(coordinate, world, rules))
                {
                    mask |= rules.GetMaterial(materialId).TagMask;
                }
            }
        }

        foreach (var (coordinate, block) in world.EnumerateRegionOverrides(
                     regionX + world.RegionCountX * regionY))
        {
            if (block.IsAir || coordinate.Z < world.GetSurfaceColumn(coordinate.X, coordinate.Y).SurfaceZ)
            {
                continue;
            }

            if (coordinate.Z != world.GetSurfaceColumn(coordinate.X, coordinate.Y).SurfaceZ &&
                IsExposed(coordinate, world, rules))
            {
                mask |= rules.GetMaterial(block.MaterialId).TagMask;
            }
        }

        return mask;
    }

    private static bool WithinRadius(int centerX, int centerY, int x, int y, long radiusSquared)
    {
        var deltaX = (long)x - centerX;
        var deltaY = (long)y - centerY;
        return deltaX * deltaX + deltaY * deltaY <= radiusSquared;
    }

    private readonly record struct ObservationKey(
        PerceptionEntityKind Kind,
        int X,
        int Y,
        int Z,
        ulong Id) : IComparable<ObservationKey>
    {
        public int CompareTo(ObservationKey other)
        {
            var comparison = Kind.CompareTo(other.Kind);
            if (comparison != 0) return comparison;
            comparison = Z.CompareTo(other.Z);
            if (comparison != 0) return comparison;
            comparison = Y.CompareTo(other.Y);
            if (comparison != 0) return comparison;
            comparison = X.CompareTo(other.X);
            return comparison != 0 ? comparison : Id.CompareTo(other.Id);
        }
    }
}
