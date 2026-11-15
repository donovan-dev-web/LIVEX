using Prism.Core.World;

namespace Prism.Core.Navigation;

/// <summary>
/// Regions a path search read, with the revision each one carried when the search ran.
/// </summary>
/// <remarks>
/// <para>
/// A* does not depend only on the path it returns: two searches can end on different itineraries
/// of the same cost because of any cell they <em>evaluated</em>. Invalidating a cached result on
/// <see cref="WorldGrid.Revision"/> is therefore exact but far too broad: a mining anywhere kills
/// every agent's path, which is what made a tick recompute every search (PRISM-104). Invalidating
/// on the path's own cells is the opposite error — it is not exact, and it silently changes
/// trajectories.
/// </para>
/// <para>
/// This set is the exact middle ground: every cell the search touched belongs to a region, and
/// <see cref="WorldGrid.GetRegionRevision"/> moves exactly when a cell of that region is written.
/// If no region of the set moved, the graph is identical over the whole area the search explored,
/// so replaying the search would produce the same itinerary, node for node. The set is a
/// <b>superset</b> of what the search really needed (a whole region is invalidated for one cell),
/// so it can only keep less than it could — never more than it may.
/// </para>
/// </remarks>
public sealed class RegionReadSet
{
    private readonly int[] _regions;
    private readonly long[] _revisions;

    internal RegionReadSet(int[] regions, long[] revisions)
    {
        _regions = regions;
        _revisions = revisions;
    }

    /// <summary>Number of distinct regions the search read.</summary>
    public int Count => _regions.Length;

    /// <summary>Region identifiers, ascending (canonical, so a comparison never depends on visit order).</summary>
    public ReadOnlySpan<int> Regions => _regions;

    /// <summary>
    /// True when no region of the set has been written since the search: the cached result is then
    /// exactly what a fresh search would return.
    /// </summary>
    public bool IsStillValid(WorldGrid world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var regions = _regions;
        var revisions = _revisions;
        for (var index = 0; index < regions.Length; index++)
        {
            if (world.GetRegionRevision(regions[index]) != revisions[index])
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Collects the regions read by one search. Reused from search to search: marking a region costs
/// one comparison thanks to a generation stamp, so a search pays O(regions touched), not O(world).
/// </summary>
internal sealed class RegionReadSetBuilder
{
    private readonly WorldGrid _world;
    private int[] _mark;
    private int[] _regions;
    private long[] _revisions;
    private int _count;
    private int _generation;

    public RegionReadSetBuilder(WorldGrid world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        _mark = new int[world.RegionCount];
        _regions = new int[Math.Min(world.RegionCount, InitialCapacity)];
        _revisions = new long[_regions.Length];
    }

    private const int InitialCapacity = 64;

    private int RegionCapacity => _world.RegionCount;

    /// <summary>Starts a new search: no region is marked yet.</summary>
    public void Begin()
    {
        if (_mark.Length != RegionCapacity)
        {
            _mark = new int[RegionCapacity];
            _generation = 0;
        }

        if (++_generation == int.MaxValue)
        {
            Array.Clear(_mark);
            _generation = 1;
        }

        _count = 0;
    }

    /// <summary>Marks the region holding a cell the search read (the cell may be outside the world).</summary>
    public void MarkCell(int x, int y)
    {
        if ((uint)x >= (uint)_world.Width || (uint)y >= (uint)_world.Height)
        {
            return;
        }

        MarkRegion((x / _world.RegionSize) + (_world.RegionCountX * (y / _world.RegionSize)));
    }

    private void MarkRegion(int regionId)
    {
        if (_mark[regionId] == _generation)
        {
            return;
        }

        _mark[regionId] = _generation;
        if (_count == _regions.Length)
        {
            var capacity = Math.Min(RegionCapacity, _regions.Length * 2);
            Array.Resize(ref _regions, capacity);
            Array.Resize(ref _revisions, capacity);
        }

        _regions[_count] = regionId;
        _revisions[_count] = _world.GetRegionRevision(regionId);
        _count++;
    }

    /// <summary>Freezes the set, sorted by region id so the stored form is canonical.</summary>
    public RegionReadSet Build()
    {
        var regions = new int[_count];
        var revisions = new long[_count];
        Array.Copy(_regions, regions, _count);
        Array.Copy(_revisions, revisions, _count);
        Array.Sort(regions, revisions);
        return new RegionReadSet(regions, revisions);
    }
}
