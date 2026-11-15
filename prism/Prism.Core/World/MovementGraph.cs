using System.Collections.Frozen;
using Prism.Core.Diagnostics;

namespace Prism.Core.World;

public sealed class MovementRules
{
    public MovementRules(
        IEnumerable<ushort> solidMaterials,
        IEnumerable<ushort> liquidMaterials,
        int maximumAscent = 1,
        int maximumDescent = 3,
        IEnumerable<ushort>? pathMaterials = null,
        int pathSpeedupPermille = 2000)
    {
        ArgumentNullException.ThrowIfNull(solidMaterials);
        ArgumentNullException.ThrowIfNull(liquidMaterials);
        if (maximumAscent < 0 || maximumDescent < 0 ||
            pathSpeedupPermille is < 1000 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(MovementRules));
        }

        SolidMaterials = solidMaterials.ToFrozenSet();
        LiquidMaterials = liquidMaterials.ToFrozenSet();
        PathMaterials = (pathMaterials ?? []).ToFrozenSet();
        if (SolidMaterials.Overlaps(LiquidMaterials))
        {
            throw new ArgumentException("A material cannot be both solid and liquid.");
        }

        if (!PathMaterials.IsSubsetOf(SolidMaterials))
        {
            throw new ArgumentException("Walkable path materials must be solid.");
        }

        PathSpeedupPermille = pathSpeedupPermille;
        MaximumAscent = maximumAscent;
        MaximumDescent = maximumDescent;
    }

    public FrozenSet<ushort> SolidMaterials { get; }
    public FrozenSet<ushort> LiquidMaterials { get; }
    public FrozenSet<ushort> PathMaterials { get; }
    public int PathSpeedupPermille { get; }
    public int MaximumAscent { get; }
    public int MaximumDescent { get; }
}

public readonly record struct MovementNode(int X, int Y, int FeetZ);

public readonly record struct MovementEdge(MovementNode Destination, int Cost);

public sealed class MovementGraph
{
    private static readonly (int X, int Y, int Cost)[] NeighborOffsets =
    [
        (0, -1, 1000),
        (-1, 0, 1000),
        (1, 0, 1000),
        (0, 1, 1000),
        (-1, -1, 1414),
        (1, -1, 1414),
        (-1, 1, 1414),
        (1, 1, 1414),
    ];

    public MovementGraph(
        WorldGrid world,
        MovementRules rules,
        Func<IReadOnlyList<ActiveStructureEffect>>? activeEffectsProvider = null,
        bool verifyCaches = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        World = world;
        Rules = rules;
        ActiveEffectsProvider = activeEffectsProvider;
        VerifyCaches = verifyCaches;
    }

    public WorldGrid World { get; }
    public MovementRules Rules { get; }
    public Func<IReadOnlyList<ActiveStructureEffect>>? ActiveEffectsProvider { get; }

    /// <summary>
    /// <c>PRISM_VERIFY_CACHES=1</c> (PRISM-108): every memo hit of <see cref="CanStandAt"/> is
    /// recomputed without the memo and compared. The memo is only safe because every world write
    /// bumps the revision of the region the memoised cell belongs to; this mode turns that
    /// assumption into a loud failure.
    /// </summary>
    public bool VerifyCaches { get; }

    /// <summary>
    /// Tick of the running world, for cache-verification diagnostics only; -1 when the caller has
    /// no tick. Never read by the simulation, so it cannot affect a trajectory.
    /// </summary>
    public long Tick { get; set; } = -1;

    /// <summary>
    /// Number of <see cref="CanStandAt"/> answers served from the memo since the graph was
    /// created. Diagnostics only: incremented on a memo hit and never read by the simulation, so
    /// it cannot move a trajectory. It is what makes "a write in another region leaves this cell's
    /// memo alive" observable (PRISM-109).
    /// </summary>
    public long StandMemoHits { get; private set; }

    // Mémo de CanStandAt : une recherche A* interroge chaque case jusqu'à huit fois
    // (une par voisin), et chaque interrogation relisait la colonne, les overrides et
    // les jeux de matériaux. Le mémo est valable tant qu'aucune écriture n'a touché la
    // région de la case (PRISM-109) : la réponse ne dépend que de la colonne (x, y),
    // donc de sa seule région — la preuve est dans CanStandAtCore, qui ne lit aucune
    // case voisine. Une génération sépare les recherches (une recherche n'écrit pas le
    // monde), et la révision par case protège les appels hors recherche, qui, eux,
    // traversent les ticks.
    //
    // Les effets de structure (ponts, escaliers) sont appliqués avec des écritures de
    // grille, donc couverts par la même révision ; si un effet venait à changer sans
    // toucher la grille, ce mémo devrait être étendu à leur compteur. C'est l'hypothèse
    // que PRISM_VERIFY_CACHES=1 transforme en échec bruyant.
    private int[] _standStamp = [];
    private int[] _standFeet = [];
    private long[] _standRegionRevision = [];
    private int _standGeneration;

    /// <summary>Ouvre une nouvelle époque de mémo (une recherche de chemin).</summary>
    public void BeginStandCache()
    {
        if (++_standGeneration == int.MaxValue)
        {
            Array.Clear(_standStamp);
            _standGeneration = 1;
        }
    }

    public bool CanStandAt(int x, int y, out int feetZ)
    {
        int size = checked(World.Width * World.Height);
        if (_standStamp.Length != size)
        {
            _standStamp = new int[size];
            _standFeet = new int[size];
            _standRegionRevision = new long[size];
            _standGeneration = 1;
        }

        if ((uint)x >= (uint)World.Width || (uint)y >= (uint)World.Height)
        {
            feetZ = 0;
            return false;
        }

        int index = (y * World.Width) + x;
        long regionRevision = World.GetRegionRevisionAt(x, y);
        if (_standStamp[index] == _standGeneration &&
            _standRegionRevision[index] == regionRevision)
        {
            StandMemoHits++;
            if (VerifyCaches)
            {
                var freshStandable = CanStandAtCore(x, y, out var freshFeet);
                // Two "not standable" answers agree: only a disagreement on the verdict, or on
                // the feet of a standable cell, is a divergence.
                var memoStandable = _standFeet[index] >= 0;
                if (freshStandable != memoStandable ||
                    (freshStandable && freshFeet != _standFeet[index]))
                {
                    throw new CacheVerificationException(
                        "movement.standMemo",
                        Tick,
                        null,
                        $"({x},{y})",
                        "the memo says " +
                        (_standFeet[index] >= 0 ? $"standable with feet at {_standFeet[index]}" : "not standable") +
                        ", a fresh evaluation says " +
                        (freshStandable ? $"standable with feet at {freshFeet}" : "not standable"));
                }
            }

            feetZ = _standFeet[index];
            return feetZ >= 0;
        }

        bool standable = CanStandAtCore(x, y, out feetZ);
        _standStamp[index] = _standGeneration;
        _standRegionRevision[index] = regionRevision;
        _standFeet[index] = standable ? feetZ : -1;
        return standable;
    }

    private bool CanStandAtCore(int x, int y, out int feetZ)
    {
        feetZ = 0;
        if (!World.TryGetSurfaceColumn(x, y, out var surface))
        {
            return false;
        }

        var floorZ = surface.SurfaceZ;
        var solidFloor = false;
        var hasSurfaceOverride =
            World.TryGetBlockOverride(new BlockCoordinate(x, y, floorZ), out var floorOverride);
        if (hasSurfaceOverride && floorOverride.IsAir)
        {
            // The snapshot surface block was mined: the hole is the standing cell now, one level
            // below the neighbour's ground. Treating the column as unstandable for ever was a real
            // dead end: a ladder digging the stone of its furnace around itself left the agent on a
            // 1x1 pillar, every cardinal neighbour answered "cannot stand", and every walk died on
            // `path_not_found` (R1 tools, T+1170, 342 ore faces in sight and untouched). Terrain
            // below the snapshot surface is not stored as an override, so one level down is the
            // floor; a block that was removed again keeps descending only through known air.
            while (floorZ > 0 &&
                   World.TryGetBlockOverride(new BlockCoordinate(x, y, floorZ), out var below) &&
                   below.IsAir)
            {
                floorZ--;
            }

            solidFloor = floorZ > 0 &&
                         (!World.TryGetBlockOverride(new BlockCoordinate(x, y, floorZ), out var ground) ||
                          !ground.IsAir && Rules.SolidMaterials.Contains(ground.MaterialId));
        }
        else if (hasSurfaceOverride)
        {
            solidFloor = Rules.SolidMaterials.Contains(floorOverride.MaterialId);
        }
        else
        {
            solidFloor = Rules.SolidMaterials.Contains(surface.MaterialId);
        }

        // A stair effect lets an agent stand two levels above the surface even without a solid floor.
        if (!solidFloor && HasEffect(StructureEffectConsumer.StairEffectType, x, y, surface.SurfaceZ + 2))
        {
            feetZ = surface.SurfaceZ + 2;
            if (feetZ + 1 >= World.VerticalSize)
            {
                return false;
            }

            for (var z = feetZ; z < feetZ + 2; z++)
            {
                if (World.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var block) &&
                    !block.IsAir &&
                    (Rules.LiquidMaterials.Contains(block.MaterialId) ||
                     Rules.SolidMaterials.Contains(block.MaterialId)))
                {
                    return false;
                }
            }

            return true;
        }

        // A bridge effect lets an agent stand one level above the surface without a solid floor.
        if (!solidFloor && HasEffect(StructureEffectConsumer.BridgeEffectType, x, y, surface.SurfaceZ + 1))
        {
            solidFloor = true;
        }

        if (!solidFloor)
        {
            return false;
        }

        feetZ = floorZ + 1;
        for (var z = feetZ; z < feetZ + 2; z++)
        {
            if ((uint)z >= (uint)World.VerticalSize)
            {
                return false;
            }

            if (World.TryGetBlockOverride(new BlockCoordinate(x, y, z), out var block))
            {
                if (block.IsAir)
                {
                    continue;
                }

                if (Rules.LiquidMaterials.Contains(block.MaterialId) ||
                    Rules.SolidMaterials.Contains(block.MaterialId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public MovementNode? GetNode(int x, int y)
    {
        return CanStandAt(x, y, out var feetZ)
            ? new MovementNode(x, y, feetZ)
            : null;
    }

    /// <summary>
    /// Voisins d'un nœud, énumérés sans allocation : le cheminement A* visite des
    /// dizaines de milliers de nœuds par recherche et une énumération allouée par
    /// nœud coûtait à elle seule plus que le calcul du chemin.
    /// </summary>
    public NeighborEnumerator Neighbors(MovementNode from) => new(this, from);

    public IEnumerable<MovementEdge> GetNeighbors(MovementNode from)
    {
        foreach (var edge in Neighbors(from))
        {
            yield return edge;
        }
    }

    /// <summary>
    /// Énumère les cases où l'agent peut se tenir, sans <c>yield</c>.
    /// </summary>
    /// <param name="graph">Le graphe de mouvement interrogé.</param>
    /// <param name="from">Le nœud dont on cherche les voisins.</param>
    public struct NeighborEnumerator(MovementGraph graph, MovementNode from)
    {
        private readonly MovementGraph _graph = graph;
        private readonly MovementNode _from = from;
        private int _index = -1;
        private MovementEdge _current;

        public MovementEdge Current => _current;

        public NeighborEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            while (++_index < NeighborOffsets.Length)
            {
                var (offsetX, offsetY, cost) = NeighborOffsets[_index];
                if (_graph.TryGetNeighbor(_from, offsetX, offsetY, cost, out _current))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Un voisin précis, s'il est atteignable depuis <paramref name="from"/>.
    /// </summary>
    internal bool TryGetNeighbor(
        MovementNode from,
        int offsetX,
        int offsetY,
        int cost,
        out MovementEdge edge)
    {
        edge = default;
        var x = from.X + offsetX;
        var y = from.Y + offsetY;
        if (!CanStandAt(x, y, out var feetZ))
        {
            return false;
        }

        var deltaZ = feetZ - from.FeetZ;
        if (deltaZ > Rules.MaximumAscent || deltaZ < -Rules.MaximumDescent)
        {
            return false;
        }

        if (offsetX != 0 && offsetY != 0 &&
            (!CanTraverseCardinal(from, from.X + offsetX, from.Y) ||
             !CanTraverseCardinal(from, from.X, from.Y + offsetY)))
        {
            return false;
        }

        var destination = new MovementNode(x, y, feetZ);
        var edgeCost = cost;
        if (IsPath(from) && IsPath(destination))
        {
            edgeCost = Math.Max(1, (cost * 1000) / Rules.PathSpeedupPermille);
        }

        edge = new MovementEdge(destination, edgeCost);
        return true;
    }

    private bool IsPath(MovementNode node)
    {
        var surface = World.GetSurfaceColumn(node.X, node.Y);
        var coordinate = new BlockCoordinate(node.X, node.Y, surface.SurfaceZ);
        var materialId = World.TryGetBlockOverride(coordinate, out var block) && !block.IsAir
            ? block.MaterialId
            : surface.MaterialId;
        return Rules.PathMaterials.Contains(materialId) ||
               HasEffect(StructureEffectConsumer.PathSpeedEffectType, node.X, node.Y, node.FeetZ - 1);
    }

    private IReadOnlyList<ActiveStructureEffect> ActiveEffects =>
        ActiveEffectsProvider?.Invoke() ?? [];

    private bool HasEffect(string type, int x, int y, int z) =>
        ActiveEffects.Any(effect =>
            effect.Type == type &&
            x >= effect.Minimum.X && x <= effect.Maximum.X &&
            y >= effect.Minimum.Y && y <= effect.Maximum.Y &&
            z >= effect.Minimum.Z && z <= effect.Maximum.Z);

    private bool CanTraverseCardinal(MovementNode from, int x, int y)
    {
        return CanStandAt(x, y, out var feetZ) &&
               feetZ - from.FeetZ <= Rules.MaximumAscent &&
               feetZ - from.FeetZ >= -Rules.MaximumDescent;
    }
}
