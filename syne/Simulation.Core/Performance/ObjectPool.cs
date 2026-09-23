namespace Simulation.Core.Performance;

/// <summary>
/// Pool d'objets réutilisables (SYNE-092, PERFORMANCE.md §5) : réduit la charge
/// GC des structures éphémères (listes de candidats de perception, scores…)
/// sans changer le comportement — un objet rendu est vidé avant ré-emprunt, et
/// l'ordre des éléments produits reste identique à une allocation « fraîche »
/// (aucun PRNG, aucune influence sur la trajectoire — DETERMINISM.md §3).
/// </summary>
/// <typeparam name="T">Type des éléments des conteneurs poolés.</typeparam>
public sealed class ObjectPool<T>
{
    private readonly Stack<List<T>> _items;
    private int _count;
    private long _rentCount;
    private long _returnCount;

    public ObjectPool(int initialCapacity = 16)
    {
        _items = new Stack<List<T>>(Math.Max(1, initialCapacity));
    }

    /// <summary>Nombre d'instances actuellement mises en réserve.</summary>
    public int Count => _count;

    /// <summary>Nombre cumulé d'emprunts (observabilité SYNE-092).</summary>
    public long RentCount => _rentCount;

    /// <summary>Nombre cumulé de restitutions (observabilité SYNE-092).</summary>
    public long ReturnCount => _returnCount;

    /// <summary>
    /// Emprunte une liste vidée. Si une instance est disponible en réserve elle
    /// est réutilisée (après vidage), sinon une nouvelle liste est allouée.
    /// </summary>
    public List<T> Rent()
    {
        _rentCount++;
        if (_count > 0)
        {
            List<T> item = _items.Pop();
            _count--;
            item.Clear();
            return item;
        }

        return [];
    }

    /// <summary>Rend une liste au pool (formatée pour la réutilisation).</summary>
    public void Return(List<T> item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Clear();
        _items.Push(item);
        _count++;
        _returnCount++;
    }
}