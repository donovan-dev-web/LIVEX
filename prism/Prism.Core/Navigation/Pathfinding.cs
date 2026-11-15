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

    /// <summary>
    /// Regions the search read, with their revision at the time (PRISM-104). A cache reuses the
    /// result only while <see cref="RegionReadSet.IsStillValid"/> holds, which is exactly the
    /// condition under which a fresh search would return the same itinerary.
    /// </summary>
    public RegionReadSet? Reads { get; init; }
}

public readonly record struct PathCacheStatistics(long Hits, long Misses);

/// <summary>Issue of a cache lookup: a hit, an absence, or an entry proven stale by a region write.</summary>
public enum PathCacheLookup : byte
{
    Hit,
    Miss,
    Invalidated,
}

/// <summary>
/// Cache of path results, exact without depending on the global world revision.
/// </summary>
/// <remarks>
/// <para>
/// The key is the search itself (graph, start, goal, node budget); the validity is the set of
/// regions the search read (<see cref="RegionReadSet"/>). An entry survives any write that lands
/// outside those regions, which is what keeps agents walking in a world that is being mined
/// elsewhere — the case that made every tick recompute every search (PRISM-104).
/// </para>
/// <para>
/// Eviction is a real LRU (O(1)) rather than a scan: the lifecycle shares one cache across every
/// agent, so a scan per insert would itself become the cost.
/// </para>
/// </remarks>
public sealed class PathfindingCache
{
    private readonly int _capacity;
    private readonly Dictionary<PathCacheKey, LinkedListNode<CacheEntry>> _entries = [];
    private readonly LinkedList<CacheEntry> _lru = [];
    private long _hits;
    private long _misses;
    private long _invalidations;

    public PathfindingCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public PathCacheStatistics Statistics => new(_hits, _misses);

    /// <summary>Entrées trouvées puis rejetées parce qu'une région lue avait été écrite.</summary>
    public long Invalidations => _invalidations;

    internal PathCacheLookup TryGet(
        MovementGraph graph,
        MovementNode start,
        MovementNode goal,
        int maximumExpandedNodes,
        out PathfindingResult? result)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpandedNodes);
        if (!_entries.TryGetValue(
                new PathCacheKey(graph, start, goal, maximumExpandedNodes),
                out var node))
        {
            _misses++;
            result = null;
            return PathCacheLookup.Miss;
        }

        if (node.Value.Result.Reads is not { } reads || !reads.IsStillValid(graph.World))
        {
            _lru.Remove(node);
            _entries.Remove(node.Value.Key);
            _invalidations++;
            _misses++;
            result = null;
            return PathCacheLookup.Invalidated;
        }

        _lru.Remove(node);
        _lru.AddFirst(node);
        _hits++;
        result = node.Value.Result;
        return PathCacheLookup.Hit;
    }

    internal void Store(
        MovementGraph graph,
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

        // Sans ensemble de régions, l'entrée ne saurait pas quand elle périme : on ne la garde pas.
        if (result.Reads is null)
        {
            return;
        }

        var key = new PathCacheKey(graph, start, goal, maximumExpandedNodes);
        if (_entries.TryGetValue(key, out var existing))
        {
            _lru.Remove(existing);
            _entries.Remove(key);
        }

        var entry = new CacheEntry(key, result);
        _entries[key] = _lru.AddFirst(entry);
        while (_entries.Count > _capacity)
        {
            var oldest = _lru.Last!;
            _lru.RemoveLast();
            _entries.Remove(oldest.Value.Key);
        }
    }

    private readonly record struct PathCacheKey(
        MovementGraph Graph,
        MovementNode Start,
        MovementNode Goal,
        int MaximumExpandedNodes);

    private sealed record CacheEntry(PathCacheKey Key, PathfindingResult Result);
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
        PathfindingCache? cache = null) =>
        FindPath(graph, startX, startY, goalX, goalY, maximumExpandedNodes, cache, null);

    public static PathfindingResult FindPath(
        MovementGraph graph,
        int startX,
        int startY,
        int goalX,
        int goalY,
        int maximumExpandedNodes,
        PathfindingCache? cache,
        PathfindingMetrics? metrics)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpandedNodes);

        var start = graph.GetNode(startX, startY);
        var goal = graph.GetNode(goalX, goalY);
        if (start is null || goal is null)
        {
            // Aucune recherche n'est lancée : le développement est nul, mais la demande a bien
            // échoué et les compteurs doivent le dire.
            var absent = new PathfindingResult(PathfindingStatus.NotFound, [], 0, 0);
            metrics?.RecordSearch(absent.Status, 0);
            return absent;
        }

        var startNode = start.Value;
        var goalNode = goal.Value;
        if (cache is not null)
        {
            switch (cache.TryGet(graph, startNode, goalNode, maximumExpandedNodes, out var cached))
            {
                case PathCacheLookup.Hit:
                    metrics?.RecordCacheHit();
                    return cached!;
                case PathCacheLookup.Invalidated:
                    metrics?.RecordCacheInvalidation();
                    break;
            }
        }

        var result = Search(graph, startNode, goalNode, maximumExpandedNodes, true, null);
        metrics?.RecordSearch(result.Status, result.ExpandedNodes);
        cache?.Store(graph, startNode, goalNode, maximumExpandedNodes, result);
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
        Func<MovementNode, bool> allowed) =>
        FindPathRestricted(graph, start, goal, maximumExpandedNodes, allowed, null);

    public static PathfindingResult FindPathRestricted(
        MovementGraph graph,
        MovementNode start,
        MovementNode goal,
        int maximumExpandedNodes,
        Func<MovementNode, bool> allowed,
        PathfindingMetrics? metrics)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(allowed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumExpandedNodes);
        if (!allowed(start) || !allowed(goal))
        {
            return new PathfindingResult(PathfindingStatus.NotFound, [], 0, 0);
        }

        var result = Search(graph, start, goal, maximumExpandedNodes, true, allowed);
        metrics?.RecordSearch(result.Status, result.ExpandedNodes);
        return result;
    }

    /// <summary>
    /// Buffers de recherche réutilisés d'une recherche à l'autre.
    /// </summary>
    /// <remarks>
    /// Une recherche A* qui n'aboutit pas épuise le budget de nœuds, donc le monde
    /// entier : allouer des dictionnaires pour 65 536 nœuds à chaque appel coûtait
    /// l'essentiel du milliard d'allocations d'un tick. Les tableaux sont indexés
    /// par la case (y × largeur + x) ; un marqueur de génération évite de les
    /// effacer, donc une recherche coûte O(nœuds visités), pas O(monde).
    /// </remarks>
    private sealed class SearchScratch
    {
        public int[] Stamp = [];
        public int[] Cost = [];
        public int[] Previous = [];
        public int[] FeetZ = [];
        public RegionReadSetBuilder? Reads;
        public WorldGrid? ReadsWorld;
        public int Generation;
        public int Size;

        public void Begin(int size)
        {
            if (Size != size)
            {
                Stamp = new int[size];
                Cost = new int[size];
                Previous = new int[size];
                FeetZ = new int[size];
                Size = size;
                Generation = 0;
            }

            if (++Generation == int.MaxValue)
            {
                Array.Clear(Stamp);
                Generation = 1;
            }
        }

        /// <summary>
        /// Tampon de régions lues, lié au monde qu'il décrit : un tampon d'un autre
        /// monde lirait les révisions d'un monde qui n'est plus celui de la recherche.
        /// </summary>
        public RegionReadSetBuilder BeginReads(WorldGrid world, bool reset)
        {
            if (Reads is null || !ReferenceEquals(ReadsWorld, world))
            {
                Reads = new RegionReadSetBuilder(world);
                ReadsWorld = world;
                reset = true;
            }

            if (reset)
            {
                Reads.Begin();
            }

            return Reads;
        }
    }

    /// <summary>
    /// Tampons de recherche par thread : la boucle du host est mono-thread et un
    /// tampon partagé entre recherches concurrentes fausserait les résultats.
    /// </summary>
    [System.ThreadStatic]
    private static SearchScratch? _scratch;

    private static PathfindingResult Search(
        MovementGraph graph,
        MovementNode start,
        MovementNode goal,
        int maximumExpandedNodes,
        bool captureReads,
        Func<MovementNode, bool>? allowed)
    {
        graph.BeginStandCache();
        int width = graph.World.Width;
        int height = graph.World.Height;
        int size = checked(width * height);
        SearchScratch scratch = _scratch ??= new SearchScratch();
        scratch.Begin(size);
        int[] stamp = scratch.Stamp;
        int[] costs = scratch.Cost;
        int[] previous = scratch.Previous;
        int[] feetZ = scratch.FeetZ;
        int generation = scratch.Generation;
        RegionReadSetBuilder? reads = captureReads
            ? scratch.BeginReads(graph.World, reset: true)
            : null;
        RegionReadSet? readSet = null;

        int startIndex = (start.Y * width) + start.X;
        int goalIndex = (goal.Y * width) + goal.X;
        if (reads is not null)
        {
            // Les deux extrémités sont lues avant la boucle (`GetNode`) : une écriture sur la
            // cible pendant qu'elle est inaccessible doit périmer le résultat, sinon un échec
            // mémorisé survivrait à l'ouverture du chemin.
            reads.MarkCell(start.X, start.Y);
            reads.MarkCell(goal.X, goal.Y);
        }

        var frontier = new PriorityQueue<int, NodePriority>();
        long sequence = 0;
        frontier.Enqueue(
            startIndex,
            CreatePriority(start, goal, 0, sequence++, graph.Rules.PathSpeedupPermille));
        stamp[startIndex] = generation;
        costs[startIndex] = 0;
        previous[startIndex] = -1;
        feetZ[startIndex] = start.FeetZ;
        var expanded = 0;

        while (frontier.TryDequeue(out int currentIndex, out var priority))
        {
            if (stamp[currentIndex] != generation || priority.G != costs[currentIndex])
            {
                continue;
            }

            var current = new MovementNode(
                currentIndex % width,
                currentIndex / width,
                feetZ[currentIndex]);
            var currentCost = costs[currentIndex];
            if (currentIndex == goalIndex)
            {
                if (reads is not null)
                {
                    readSet = reads.Build();
                }

                return new PathfindingResult(
                    PathfindingStatus.Found,
                    ReconstructPath(previous, feetZ, currentIndex, width),
                    currentCost,
                    expanded)
                {
                    Reads = readSet,
                };
            }

            if (expanded >= maximumExpandedNodes)
            {
                // Le budget epuise a bien lu le monde : la recherche echoue, mais l'ensemble
                // des regions lues est exact et permet a l'appelant de memoriser cet echec
                // tant qu'aucune de ces regions n'est ecrite (PRISM-104).
                if (reads is not null)
                {
                    readSet = reads.Build();
                }

                return new PathfindingResult(
                    PathfindingStatus.NodeBudgetExceeded,
                    [],
                    0,
                    expanded)
                {
                    Reads = readSet,
                };
            }

            expanded++;
            if (reads is not null)
            {
                reads.MarkCell(current.X, current.Y);
            }

            foreach (var edge in graph.Neighbors(current))
            {
                if (reads is not null)
                {
                    // Le voisin a été lu par `CanStandAt`, qu'il soit retenu ou non : c'est
                    // cette lecture-là qui doit périmer le résultat si elle change.
                    reads.MarkCell(edge.Destination.X, edge.Destination.Y);
                }

                if (allowed is not null && !allowed(edge.Destination))
                {
                    continue;
                }

                int destinationIndex = (edge.Destination.Y * width) + edge.Destination.X;
                var candidateCost = checked(currentCost + edge.Cost);
                if (stamp[destinationIndex] == generation && candidateCost >= costs[destinationIndex])
                {
                    continue;
                }

                stamp[destinationIndex] = generation;
                costs[destinationIndex] = candidateCost;
                previous[destinationIndex] = currentIndex;
                feetZ[destinationIndex] = edge.Destination.FeetZ;
                frontier.Enqueue(
                    destinationIndex,
                    CreatePriority(
                        edge.Destination,
                        goal,
                        candidateCost,
                        sequence++,
                        graph.Rules.PathSpeedupPermille));
            }

            if (reads is not null)
            {
                // Les voisins refusés n'apparaissent pas dans l'énumération : ils ont pourtant
                // été lus, et une écriture qui les rend praticables change la réponse. Les huit
                // cases du voisinage couvrent aussi les deux cases cardinales relues par un
                // déplacement diagonal.
                reads.MarkCell(current.X, current.Y - 1);
                reads.MarkCell(current.X, current.Y + 1);
                reads.MarkCell(current.X - 1, current.Y);
                reads.MarkCell(current.X + 1, current.Y);
                reads.MarkCell(current.X - 1, current.Y - 1);
                reads.MarkCell(current.X + 1, current.Y - 1);
                reads.MarkCell(current.X - 1, current.Y + 1);
                reads.MarkCell(current.X + 1, current.Y + 1);
            }
        }

        if (reads is not null)
        {
            readSet = reads.Build();
        }

        return new PathfindingResult(PathfindingStatus.NotFound, [], 0, expanded)
        {
            Reads = readSet,
        };
    }

    private static IReadOnlyList<MovementNode> ReconstructPath(
        int[] previous,
        int[] feetZ,
        int goalIndex,
        int width)
    {
        var path = new List<MovementNode>();
        int current = goalIndex;
        while (current >= 0)
        {
            path.Add(new MovementNode(current % width, current / width, feetZ[current]));
            current = previous[current];
        }

        path.Reverse();
        return Array.AsReadOnly(path.ToArray());
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
