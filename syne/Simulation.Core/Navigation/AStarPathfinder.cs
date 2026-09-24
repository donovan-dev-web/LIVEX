using Simulation.Core.Configuration;
using Simulation.Core.World;
using WorldType = Simulation.Core.World.World;

namespace Simulation.Core.Navigation;

/// <summary>
/// Cheminement déterministe (SYNE-077, Monographie §6.2.12) : A* sur une grille
/// rasterisée du monde — les disques obstacles bloquent les cellules dont le
/// centre tombe dans le disque (marge demi-cellule). Voisinage carré 8-connexe
/// parcouru dans un ordre fixe, fermeture par égalité à 1e-12, départage
/// déterministe par (f, puis g, puis (x, y)) — aucune consommation du PRNG.
///
/// Le repli est « sur place » : si aucun chemin n'existe (destination dans un
/// obstacle ou inaccessible), ou si le plafond d'expansion
/// <see cref="PathfindingSettings.MaxExpansionCells"/> est atteint, le chemin
/// renvoyé est vide (l'entité ne se déplace pas ce tick-ci).
/// </summary>
public sealed class AStarPathfinder
{
    private readonly WorldType _world;
    private readonly PathfindingSettings _settings;
    private readonly PathCache _cache;
    private readonly int _cellCountX;
    private readonly int _cellCountY;
    private bool[] _blocked;
    private ulong _rasterizedRevision;

    public AStarPathfinder(WorldType world, PathfindingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settings);
        _world = world;
        _settings = settings;
        _cache = new PathCache(settings.CacheCapacity);
        _cellCountX = Math.Max(1, (int)Math.Ceiling(world.Size.Width / settings.CellSize));
        _cellCountY = Math.Max(1, (int)Math.Ceiling(world.Size.Height / settings.CellSize));
        _blocked = RasterizeObstacles();
        _rasterizedRevision = world.ObstacleRevision;
    }

    public int CellCountX => _cellCountX;

    public int CellCountY => _cellCountY;

    public PathCache Cache => _cache;

    /// <summary>
    /// Re-rasterise la grille bloquée et purge le cache de chemins si la révision
    /// des obstacles du monde a changé depuis la dernière rasterisation (SYNE-071) :
    /// une construction posée/retirée en cours de run doit être contournée — aucun
    /// chemin mémorisé n'est plus valide. Sans changement (cas nominal), ne fait rien
    /// (coût nul, déterminisme préservé).
    /// </summary>
    public void Refresh()
    {
        if (_world.ObstacleRevision == _rasterizedRevision)
        {
            return;
        }

        _blocked = RasterizeObstacles();
        _cache.Clear();
        _rasterizedRevision = _world.ObstacleRevision;
    }

    /// <summary>
    /// Centre du chemin depuis <paramref name="from"/> vers <paramref name="to"/>
    /// (cellule source exclue, but inclus). Chemin vide si non trouvé (repli sur
    /// place) ou si les deux extrêmes partagent la même cellule. La mémoïsation
    /// du cache LRU est consultée en premier (déterminisme de l'ordre d'appel).
    /// </summary>
    public IReadOnlyList<Position> FindPath(Position from, Position to)
    {
        if (!_settings.Enabled)
        {
            return [];
        }

        (int sx, int sy) = CellOf(from);
        (int tx, int ty) = CellOf(to);
        if (sx == tx && sy == ty)
        {
            return [];
        }

        IReadOnlyList<Position>? cached = _cache.TryGet(sx, sy, tx, ty);
        if (cached is not null)
        {
            return cached;
        }

        IReadOnlyList<Position> path = Search(from, to, sx, sy, tx, ty, _blocked);
        _cache.Add(sx, sy, tx, ty, path);
        return path;
    }

    /// <summary>
    /// A* pondéré : f = g + h (h = octile), voisinage 8-connexe ordonné
    /// (cardinaux puis diagonales), graine = centre de cellule. L'expansion est
    /// plafonnée à <c>maxExpansionCells</c>.
    /// </summary>
    private IReadOnlyList<Position> Search(
        Position from,
        Position to,
        int sx,
        int sy,
        int tx,
        int ty,
        bool[] blocked)
    {
        if (Index(tx, ty) >= blocked.Length || blocked[Index(tx, ty)])
        {
            return [];
        }

        var open = new List<Node>(64) { new(sx, sy, F: Heuristic(sx, sy, tx, ty), G: 0.0) };
        var gScore = new Dictionary<(int X, int Y), double>
        {
            [(sx, sy)] = 0.0,
        };
        var cameFrom = new Dictionary<(int X, int Y), (int X, int Y)>();
        var closed = new HashSet<(int X, int Y)>();
        int expansions = 0;

        while (open.Count > 0)
        {
            Node current = open[0];
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
            {
                if (Compare(current, open[i]) > 0)
                {
                    current = open[i];
                    bestIndex = i;
                }
            }

            open[bestIndex] = open[^1];
            open.RemoveAt(open.Count - 1);

            if (!closed.Add((current.X, current.Y)))
            {
                continue;
            }

            if (current.X == tx && current.Y == ty)
            {
                return Reconstruct(cameFrom, sx, sy, tx, ty);
            }

            expansions++;
            if (expansions > _settings.MaxExpansionCells)
            {
                return [];
            }

            foreach ((int nx, int ny) in Neighbors(current.X, current.Y))
            {
                int index = Index(nx, ny);
                if (index < 0 || index >= blocked.Length || blocked[index])
                {
                    continue;
                }

                double step = (nx != current.X && ny != current.Y) ? DiagonalCost : CardCost;
                double tentative = gScore[(current.X, current.Y)] + step;
                var key = (nx, ny);
                if (!gScore.TryGetValue(key, out double known) || tentative + 1e-12 < known)
                {
                    gScore[key] = tentative;
                    cameFrom[key] = (current.X, current.Y);
                    open.Add(new Node(nx, ny, tentative + Heuristic(nx, ny, tx, ty), tentative));
                }
            }
        }

        return [];
    }

    private const double CardCost = 1.0;
    private static readonly double DiagonalCost = Math.Sqrt(2.0);

    /// <summary>Départage déterministe : f puis g puis (x, y).</summary>
    private static int Compare(Node a, Node b)
    {
        int byF = a.F.CompareTo(b.F);
        if (byF != 0)
        {
            return byF;
        }

        int byG = a.G.CompareTo(b.G);
        if (byG != 0)
        {
            return byG;
        }

        int byX = a.X.CompareTo(b.X);
        return byX != 0 ? byX : a.Y.CompareTo(b.Y);
    }

    private IReadOnlyList<Position> Reconstruct(
        IReadOnlyDictionary<(int X, int Y), (int X, int Y)> cameFrom,
        int sx,
        int sy,
        int tx,
        int ty)
    {
        var cells = new List<(int X, int Y)>();
        var current = (X: tx, Y: ty);
        while (current.X != sx || current.Y != sy)
        {
            cells.Add(current);
            if (!cameFrom.TryGetValue(current, out (int X, int Y) parent))
            {
                break;
            }

            current = parent;
        }

        cells.Reverse();

        var positions = new List<Position>(cells.Count);
        foreach ((int x, int y) in cells)
        {
            positions.Add(CellCenter(x, y));
        }

        return positions;
    }

    private Position CellCenter(int x, int y) => new(
        (x + 0.5) * _settings.CellSize,
        (y + 0.5) * _settings.CellSize);

    /// <summary>Heuristique octile (admissible, coûts cohérents).</summary>
    private static double Heuristic(int x, int y, int tx, int ty)
    {
        int dx = Math.Abs(x - tx);
        int dy = Math.Abs(y - ty);
        return CardCost * Math.Max(dx, dy) + (DiagonalCost - CardCost) * Math.Min(dx, dy);
    }

    /// <summary>
    /// Voisinage 8-connexe dans un ordre fixe (cardinaux puis diagonales) — deux
    /// diagonales orthogonales entre elles alternent pour un départage stable.
    /// </summary>
    private (int X, int Y)[] Neighbors(int x, int y)
    {
        var neighbors = new (int, int)[8]
        {
            (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1),
            (x + 1, y + 1), (x + 1, y - 1), (x - 1, y + 1), (x - 1, y - 1),
        };
        return neighbors;
    }

    /// <summary>Cellule bloquée si son centre tombe dans un disque obstacle.</summary>
    private bool[] RasterizeObstacles()
    {
        var blocked = new bool[_cellCountX * _cellCountY];
        foreach (Obstacle obstacle in _world.Obstacles)
        {
            double margin = _settings.CellSize * 0.5;
            for (int y = 0; y < _cellCountY; y++)
            {
                for (int x = 0; x < _cellCountX; x++)
                {
                    Position center = CellCenter(x, y);
                    if (obstacle.Position.DistanceTo(center) <= obstacle.Radius + margin)
                    {
                        blocked[(y * _cellCountX) + x] = true;
                    }
                }
            }
        }

        return blocked;
    }

    private (int X, int Y) CellOf(Position position)
    {
        int x = (int)Math.Floor(position.X / _settings.CellSize);
        int y = (int)Math.Floor(position.Y / _settings.CellSize);
        return (Math.Clamp(x, 0, _cellCountX - 1), Math.Clamp(y, 0, _cellCountY - 1));
    }

    private int Index(int x, int y) => (y * _cellCountX) + x;

    /// <summary>Nœud d'ouverture d'A* (f = g + h, ordre déterministe).</summary>
    private readonly record struct Node(int X, int Y, double F, double G);
}