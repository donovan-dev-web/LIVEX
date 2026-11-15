using System.Collections;
using Prism.Core.World;

namespace Prism.Core.Navigation;

/// <summary>
/// Champ de distance entier, multi-source, vers la cellule de surface la plus proche
/// satisfaisant un predicat (classe de cible). Un pas = une case adjacente praticable.
/// Valeur -1 = injoignable. 0 = source. (PRISM-143)
/// </summary>
/// <remarks>
/// <para>
/// Deterniniste par construction : les sources sont enfilees dans l'ordre canonique
/// (Z, puis Y, puis X) et le BFS explore les voisins dans l'ordre cardinal fixe
/// (est, nord, ouest, sud). Aucun atome, aucun tirage : l'ordre est independant du
/// timing et du nombre de workers.
/// </para>
/// <para>
/// Le cout est independant du nombre d'agents : une passe par classe et par
/// revision du monde, au lieu d'une recherche A* par agent et par replanification.
/// </para>
/// </remarks>
public sealed class DistanceField
{
    public const int Unreachable = -1;

    public int Width { get; }
    public int Height { get; }
    public int[] Distances { get; }

    public DistanceField(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
        Distances = new int[checked(width * height)];
        Array.Fill(Distances, Unreachable);
    }

    public int IndexOf(int x, int y) => y * Width + x;

    public bool InBounds(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height;

    public int Get(int x, int y) =>
        InBounds(x, y) ? Distances[IndexOf(x, y)] : Unreachable;

    public void Set(int x, int y, int value)
    {
        if (InBounds(x, y))
        {
            Distances[IndexOf(x, y)] = value;
        }
    }

    /// <summary>
    /// Calcule le champ par BFS multi-source. <paramref name="isSource"/> indique si la
    /// cellule de surface (x, y) est une source de la classe. <paramref name="isWalkable"/>
    /// indique si une cellule est praticable (pour la propagation). <paramref name="maxDistance"/>
    /// borne la propagation (0 = pas de borne).
    /// </summary>
    public static DistanceField Compute(
        int width,
        int height,
        Func<int, int, bool> isSource,
        Func<int, int, bool> isWalkable,
        int maxDistance = 0)
    {
        ArgumentNullException.ThrowIfNull(isSource);
        ArgumentNullException.ThrowIfNull(isWalkable);

        var field = new DistanceField(width, height);
        var queue = new Queue<int>(width * height / 4 + 16);

        // Enfilement des sources dans l'ordre canonique Z/Y/X -> ici Y puis X (surface).
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (isSource(x, y) && isWalkable(x, y))
                {
                    var idx = field.IndexOf(x, y);
                    field.Distances[idx] = 0;
                    queue.Enqueue(idx);
                }
            }
        }

        // Voisins cardinaux dans l'ordre fixe : est, nord, ouest, sud.
        ReadOnlySpan<(int dx, int dy)> neighbors = [(1, 0), (0, -1), (-1, 0), (0, 1)];

        while (queue.Count > 0)
        {
            var idx = queue.Dequeue();
            var x = idx % width;
            var y = idx / width;
            var dist = field.Distances[idx];
            if (maxDistance > 0 && dist >= maxDistance)
            {
                continue;
            }

            foreach (var (dx, dy) in neighbors)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (!field.InBounds(nx, ny))
                {
                    continue;
                }

                var nidx = field.IndexOf(nx, ny);
                if (field.Distances[nidx] != Unreachable)
                {
                    continue;
                }

                if (!isWalkable(nx, ny))
                {
                    continue;
                }

                field.Distances[nidx] = dist + 1;
                queue.Enqueue(nidx);
            }
        }

        return field;
    }

    /// <summary>
    /// Indique la prochaine case a emprunter pour se rapprocher d'une source, ou null si
    /// la cellule courante est une source (distance 0) ou injoignable.
    /// </summary>
    public (int x, int y)? NextStep(int fromX, int fromY)
    {
        if (!InBounds(fromX, fromY))
        {
            return null;
        }

        var current = Get(fromX, fromY);
        if (current <= 0)
        {
            return null;
        }

        (int x, int y)? best = null;
        var bestDist = current;
        ReadOnlySpan<(int dx, int dy)> neighbors = [(1, 0), (0, -1), (-1, 0), (0, 1)];
        foreach (var (dx, dy) in neighbors)
        {
            var nx = fromX + dx;
            var ny = fromY + dy;
            var d = Get(nx, ny);
            if (d >= 0 && d < bestDist)
            {
                bestDist = d;
                best = (nx, ny);
            }
        }

        return best;
    }

    /// <summary>Nombre de cellules atteignables (distance >= 0).</summary>
    public int ReachableCount
    {
        get
        {
            var count = 0;
            foreach (var d in Distances)
            {
                if (d >= 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
