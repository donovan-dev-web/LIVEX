using Prism.Core.World;

namespace Prism.Core.Navigation;

/// <summary>
/// Cheapest passage between two adjacent regions: the minimum movement cost over every pair of
/// walkable cells the movement rules connect across the region border.
/// </summary>
public sealed record RegionPortal(int FromRegion, int ToRegion, int Cost);

/// <summary>
/// Region-level abstraction of the movement graph. Every portal is discovered by scanning the
/// cells in canonical order and keeping the cheapest crossing per region pair, so the graph is
/// deterministic for a given world and set of movement rules.
/// </summary>
public sealed class RegionGraph
{
    private readonly Dictionary<int, RegionPortal[]> _portals;

    private RegionGraph(int regionCountX, int regionCountY, int regionSize, Dictionary<int, RegionPortal[]> portals)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(regionCountX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(regionCountY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(regionSize);
        RegionCountX = regionCountX;
        RegionCountY = regionCountY;
        RegionSize = regionSize;
        _portals = portals;
    }

    public int RegionCountX { get; }
    public int RegionCountY { get; }
    public int RegionSize { get; }
    public int RegionCount => checked(RegionCountX * RegionCountY);

    public bool TryGetRegion(int x, int y, out int regionId)
    {
        if ((uint)x >= (uint)checked(RegionCountX * RegionSize) ||
            (uint)y >= (uint)checked(RegionCountY * RegionSize))
        {
            regionId = -1;
            return false;
        }

        regionId = (x / RegionSize) + RegionCountX * (y / RegionSize);
        return true;
    }

    public IReadOnlyList<RegionPortal> GetPortals(int regionId) =>
        _portals.TryGetValue(regionId, out var portals) ? portals : [];

    public static RegionGraph Build(MovementGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var world = graph.World;
        var best = new Dictionary<(int From, int To), int>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (graph.GetNode(x, y) is not { } node)
                {
                    continue;
                }

                var fromRegion = RegionOf(world, node.X, node.Y);
                foreach (var edge in graph.GetNeighbors(node))
                {
                    var toRegion = RegionOf(world, edge.Destination.X, edge.Destination.Y);
                    if (fromRegion == toRegion)
                    {
                        continue;
                    }

                    var key = fromRegion < toRegion ? (fromRegion, toRegion) : (toRegion, fromRegion);
                    if (!best.TryGetValue(key, out var known) || edge.Cost < known)
                    {
                        best[key] = edge.Cost;
                    }
                }
            }
        }

        var adjacency = new Dictionary<int, List<RegionPortal>>();
        foreach (var entry in best.OrderBy(static item => item.Key.From).ThenBy(static item => item.Key.To))
        {
            var (from, to) = entry.Key;
            AddPortal(adjacency, from, new RegionPortal(from, to, entry.Value));
            AddPortal(adjacency, to, new RegionPortal(to, from, entry.Value));
        }

        var portals = adjacency.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value
                .OrderBy(static portal => portal.ToRegion)
                .ToArray());
        return new RegionGraph(world.RegionCountX, world.RegionCountY, world.RegionSize, portals);
    }

    /// <summary>
    /// Cheapest region sequence between two regions (Dijkstra, tie-break on region id so the
    /// result never depends on dictionary ordering).
    /// </summary>
    public bool TryFindRegionPath(int startRegion, int goalRegion, out IReadOnlyList<int> regions)
    {
        regions = [];
        if ((uint)startRegion >= (uint)RegionCount || (uint)goalRegion >= (uint)RegionCount)
        {
            return false;
        }

        if (startRegion == goalRegion)
        {
            regions = [startRegion];
            return true;
        }

        var costs = new Dictionary<int, int> { [startRegion] = 0 };
        var previous = new Dictionary<int, int>();
        var frontier = new PriorityQueue<int, (int Cost, int Region)>();
        frontier.Enqueue(startRegion, (0, startRegion));
        while (frontier.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost != costs[current])
            {
                continue;
            }

            if (current == goalRegion)
            {
                var path = new List<int> { current };
                while (previous.TryGetValue(current, out var parent))
                {
                    path.Add(parent);
                    current = parent;
                }

                path.Reverse();
                regions = path;
                return true;
            }

            foreach (var portal in GetPortals(current))
            {
                var candidate = priority.Cost + portal.Cost;
                if (costs.TryGetValue(portal.ToRegion, out var known) && candidate >= known)
                {
                    continue;
                }

                costs[portal.ToRegion] = candidate;
                previous[portal.ToRegion] = current;
                frontier.Enqueue(portal.ToRegion, (candidate, portal.ToRegion));
            }
        }

        return false;
    }

    private static void AddPortal(Dictionary<int, List<RegionPortal>> adjacency, int region, RegionPortal portal)
    {
        if (!adjacency.TryGetValue(region, out var list))
        {
            list = [];
            adjacency[region] = list;
        }

        list.Add(portal);
    }

    private static int RegionOf(WorldGrid world, int x, int y) =>
        (x / world.RegionSize) + world.RegionCountX * (y / world.RegionSize);
}

/// <summary>
/// Hierarchical pathfinding: the region graph chooses a corridor of regions, then the cell-level
/// A* searches only that corridor. The result is the same optimal path whenever the corridor
/// contains it; when the corridor search fails (stale portals, a corridor that looked cheaper at
/// region level) the plain search is run, so the answer is never worse than before.
/// </summary>
public static class HierarchicalPathfinder
{
    public static PathfindingResult FindPath(
        MovementGraph graph,
        int startX,
        int startY,
        int goalX,
        int goalY,
        int maximumExpandedNodes,
        RegionGraph? regionGraph = null,
        PathfindingCache? cache = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpandedNodes);

        var start = graph.GetNode(startX, startY);
        var goal = graph.GetNode(goalX, goalY);
        if (start is null || goal is null)
        {
            return new PathfindingResult(PathfindingStatus.NotFound, [], 0, 0);
        }

        var startNode = start.Value;
        var goalNode = goal.Value;
        var worldRevision = graph.World.Revision;
        if (cache is not null &&
            cache.TryGet(graph, worldRevision, startNode, goalNode, maximumExpandedNodes, out var cached))
        {
            return cached!;
        }

        var result = SearchCorridor(
            graph,
            startNode,
            goalNode,
            maximumExpandedNodes,
            regionGraph ?? RegionGraph.Build(graph));
        cache?.Store(graph, worldRevision, startNode, goalNode, maximumExpandedNodes, result);
        return result;
    }

    private static PathfindingResult SearchCorridor(
        MovementGraph graph,
        MovementNode start,
        MovementNode goal,
        int maximumExpandedNodes,
        RegionGraph regions)
    {
        var world = graph.World;
        var startRegion = RegionOf(world, start);
        var goalRegion = RegionOf(world, goal);
        if (startRegion < 0 || goalRegion < 0)
        {
            return AStarPathfinder.FindPath(
                graph, start.X, start.Y, goal.X, goal.Y, maximumExpandedNodes);
        }

        if (startRegion == goalRegion ||
            !regions.TryFindRegionPath(startRegion, goalRegion, out var corridor))
        {
            return AStarPathfinder.FindPath(
                graph, start.X, start.Y, goal.X, goal.Y, maximumExpandedNodes);
        }

        var allowed = corridor.ToHashSet();
        var restricted = AStarPathfinder.FindPathRestricted(
            graph,
            start,
            goal,
            maximumExpandedNodes,
            node => allowed.Contains(RegionOf(world, node)));
        if (restricted.Succeeded)
        {
            return restricted;
        }

        return AStarPathfinder.FindPath(
            graph, start.X, start.Y, goal.X, goal.Y, maximumExpandedNodes);
    }

    private static int RegionOf(WorldGrid world, MovementNode node) =>
        (node.X / world.RegionSize) + world.RegionCountX * (node.Y / world.RegionSize);
}
