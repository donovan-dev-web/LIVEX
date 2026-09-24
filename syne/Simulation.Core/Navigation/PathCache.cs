using Simulation.Core.World;

namespace Simulation.Core.Navigation;

/// <summary>
/// Cache LRU de chemins (SYNE-077, Monographie §6.2.12) : mémoïsation d'un
/// calcul A* par paire (cellule source, cellule but) afin d'éviter de re-planifier
/// le même chemin à chaque tick de déplacement. Borné : la plus ancienne entrée
/// est évincée au-delà de <see cref="Capacity"/>.
///
/// Déterminisme : l'ordre d'accès est l'ordre d'appel (lui-même déterministe) ;
/// le remplacement et l'éviction ne consomment aucun tirage du PRNG global.
/// </summary>
public sealed class PathCache
{
    private readonly int _capacity;
    private readonly Dictionary<(int Sx, int Sy, int Tx, int Ty), IReadOnlyList<Position>> _entries = new();
    private readonly LinkedList<(int Sx, int Sy, int Tx, int Ty)> _order = new();

    public PathCache(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "La capacité du cache de chemins doit être >= 0.");
        }

        _capacity = capacity;
    }

    public int Capacity => _capacity;

    public int Count => _entries.Count;

    /// <summary>
    /// Purge complète du cache (SYNE-071) : appelée quand le monde re-rasterise sa
    /// grille bloquée après une pose/retrait de construction — les chemins mémorisés
    /// ne sont plus valides.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
        _order.Clear();
    }

    /// <summary>Récupère le chemin mémorisé (déplace la clé en queue LRU), sinon <c>null</c>.</summary>
    public IReadOnlyList<Position>? TryGet(int sx, int sy, int tx, int ty)
    {
        var key = (sx, sy, tx, ty);
        if (_capacity == 0 || !_entries.TryGetValue(key, out IReadOnlyList<Position>? path))
        {
            return null;
        }

        _order.Remove(key);
        _order.AddLast(key);
        return path;
    }

    /// <summary>
    /// Mémorise un chemin (source exclue, but inclus). Une même clé est remplacée
    /// et re-marquée récemment utilisée ; l'éviction retire la clé la plus ancienne.
    /// </summary>
    public void Add(int sx, int sy, int tx, int ty, IReadOnlyList<Position> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (_capacity == 0)
        {
            return;
        }

        var key = (sx, sy, tx, ty);
        if (_entries.TryGetValue(key, out _))
        {
            _entries[key] = positions;
            _order.Remove(key);
        }
        else
        {
            _entries[key] = positions;
        }

        _order.AddLast(key);
        while (_order.Count > _capacity)
        {
            LinkedListNode<(int Sx, int Sy, int Tx, int Ty)>? oldest = _order.First;
            if (oldest is null)
            {
                break;
            }

            _entries.Remove(oldest.Value);
            _order.RemoveFirst();
        }
    }
}