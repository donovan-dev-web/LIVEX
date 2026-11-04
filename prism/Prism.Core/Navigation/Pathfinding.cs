using Prism.Core.World;

namespace Prism.Core.Navigation;

public enum PathfindingStatus : byte
{
    Found,
    NotFound,
    NodeBudgetExceeded,
}

public sealed record PathfindingResult(
    PathfindingStatus Status,
    IReadOnlyList<MovementNode> Path,
    int Cost,
    int ExpandedNodes)
{
    public bool Succeeded => Status == PathfindingStatus.Found;
}

public readonly record struct PathCacheStatistics(long Hits, long Misses);

public sealed class PathfindingCache
{
    private readonly int _capacity;
    private readonly Dictionary<PathCacheKey, CacheEntry> _entries = [];
    private long _sequence;
    private long _hits;
    private long _misses;

    public PathfindingCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public PathCacheStatistics Statistics => new(_hits, _misses);

    internal bool TryGet(
        MovementGraph graph,
        long worldRevision,
        MovementNode start,
        MovementNode goal,
        int maximumExpandedNodes,
        out PathfindingResult? result)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpandedNodes);
        if (_entries.TryGetValue(
                new PathCacheKey(graph, worldRevision, start, goal, maximumExpandedNodes),
                out var entry))
        {
            _entries[entry.Key] = entry with { LastUsed = _sequence++ };
            _hits++;
            result = entry.Result;
            return true;
        }

        _misses++;
        result = null;
        return false;
    }

    internal void Store(
        MovementGraph graph,
        long worldRevision,
        MovementNode start,
        MovementNode goal,
        int maximumExpandedNodes,
        PathfindingResult result)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpandedNodes);
        if (result.ExpandedNodes > maximumExpandedNodes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(result),
                "The cached search must fit within its expansion budget.");
        }

        if (result.Status == PathfindingStatus.NodeBudgetExceeded)
        {
            return;
        }

        var key = new PathCacheKey(graph, worldRevision, start, goal, maximumExpandedNodes);
        _entries[key] = new CacheEntry(key, result, _sequence++);
        while (_entries.Count > _capacity)
        {
            var oldest = _entries.Values.First();
            foreach (var candidate in _entries.Values.Skip(1))
            {
                if (candidate.LastUsed < oldest.LastUsed)
                {
                    oldest = candidate;
                }
            }

            _entries.Remove(oldest.Key);
        }
    }

    private readonly record struct PathCacheKey(
        MovementGraph Graph,
        long Revision,
        MovementNode Start,
        MovementNode Goal,
        int MaximumExpandedNodes);
    private sealed record CacheEntry(PathCacheKey Key, PathfindingResult Result, long LastUsed);
}

public static class AStarPathfinder
{
    public static PathfindingResult FindPath(
        MovementGraph graph,
        int startX,
        int startY,
        int goalX,
        int goalY,
        int maximumExpandedNodes,
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
            cache.TryGet(
                graph,
                worldRevision,
                startNode,
                goalNode,
                maximumExpandedNodes,
                out var cached))
        {
            return cached!;
        }

        var result = Search(graph, startNode, goalNode, maximumExpandedNodes);
        cache?.Store(
            graph,
            worldRevision,
            startNode,
            goalNode,
            maximumExpandedNodes,
            result);
        return result;
    }

    /// <summary>
    /// Searches inside a restricted set of nodes (used by the hierarchical corridor search).
    /// The caller keeps the fallback to the unrestricted search when the restriction fails.
    /// </summary>
    public static PathfindingResult FindPathRestricted(
        MovementGraph graph,
        MovementNode start,
        MovementNode goal,
        int maximumExpandedNodes,
        Func<MovementNode, bool> allowed)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(allowed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpandedNodes);
        if (!allowed(start) || !allowed(goal))
        {
            return new PathfindingResult(PathfindingStatus.NotFound, [], 0, 0);
        }

        return Search(graph, start, goal, maximumExpandedNodes, allowed);
    }

    private static PathfindingResult Search(
        MovementGraph graph,
        MovementNode start,
        MovementNode goal,
        int maximumExpandedNodes,
        Func<MovementNode, bool>? allowed = null)
    {
        var frontier = new PriorityQueue<MovementNode, NodePriority>();
        var costs = new Dictionary<MovementNode, int> { [start] = 0 };
        var previous = new Dictionary<MovementNode, MovementNode>();
        long sequence = 0;
        frontier.Enqueue(
            start,
            CreatePriority(start, goal, 0, sequence++, graph.Rules.PathSpeedupPermille));
        var expanded = 0;

        while (frontier.TryDequeue(out var current, out var priority))
        {
            if (priority.G != costs[current])
            {
                continue;
            }

            var currentCost = costs[current];
            if (current == goal)
            {
                return new PathfindingResult(
                    PathfindingStatus.Found,
                    ReconstructPath(previous, current),
                    currentCost,
                    expanded);
            }

            if (expanded >= maximumExpandedNodes)
            {
                return new PathfindingResult(
                    PathfindingStatus.NodeBudgetExceeded,
                    [],
                    0,
                    expanded);
            }

            expanded++;
            foreach (var edge in graph.GetNeighbors(current))
            {
                if (allowed is not null && !allowed(edge.Destination))
                {
                    continue;
                }

                var candidateCost = checked(currentCost + edge.Cost);
                if (costs.TryGetValue(edge.Destination, out var knownCost) &&
                    candidateCost >= knownCost)
                {
                    continue;
                }

                costs[edge.Destination] = candidateCost;
                previous[edge.Destination] = current;
                frontier.Enqueue(
                    edge.Destination,
                    CreatePriority(
                        edge.Destination,
                        goal,
                        candidateCost,
                        sequence++,
                        graph.Rules.PathSpeedupPermille));
            }
        }

        return new PathfindingResult(PathfindingStatus.NotFound, [], 0, expanded);
    }

    private static NodePriority CreatePriority(
        MovementNode node,
        MovementNode goal,
        int cost,
        long sequence,
        int pathSpeedupPermille)
    {
        var dx = Math.Abs(goal.X - node.X);
        var dy = Math.Abs(goal.Y - node.Y);
        var diagonal = Math.Min(dx, dy);
        var cardinal = Math.Max(dx, dy) - diagonal;
        var diagonalCost = Math.Max(1, (1414 * 1000) / pathSpeedupPermille);
        var cardinalCost = Math.Max(1, (1000 * 1000) / pathSpeedupPermille);
        var heuristic = checked(diagonal * diagonalCost + cardinal * cardinalCost);
        return new NodePriority(checked(cost + heuristic), heuristic, cost, node.Y, node.X, sequence);
    }

    private static IReadOnlyList<MovementNode> ReconstructPath(
        IReadOnlyDictionary<MovementNode, MovementNode> previous,
        MovementNode current)
    {
        var path = new List<MovementNode> { current };
        while (previous.TryGetValue(current, out var parent))
        {
            path.Add(parent);
            current = parent;
        }

        path.Reverse();
        return Array.AsReadOnly(path.ToArray());
    }

    private readonly record struct NodePriority(
        int F,
        int H,
        int G,
        int Y,
        int X,
        long Sequence) : IComparable<NodePriority>
    {
        public int CompareTo(NodePriority other)
        {
            var comparison = F.CompareTo(other.F);
            if (comparison != 0) return comparison;
            comparison = H.CompareTo(other.H);
            if (comparison != 0) return comparison;
            comparison = G.CompareTo(other.G);
            if (comparison != 0) return comparison;
            comparison = Y.CompareTo(other.Y);
            if (comparison != 0) return comparison;
            comparison = X.CompareTo(other.X);
            return comparison != 0 ? comparison : Sequence.CompareTo(other.Sequence);
        }
    }
}
