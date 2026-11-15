using Prism.Core.World;

namespace Prism.Core.Perception;

/// <summary>
/// Index spatial des agents et des objets au sol, par case agrégée de 16 × 16 (PRISM-141).
///
/// Avant cet index, percevoir un agent parcourait <b>tous</b> les agents pour tester un rayon, et
/// copiait <b>tous</b> les objets au sol : à 1 000 agents, un million de tests de rayon et autant
/// de copies d'objets par tick, alors que le rayon proche ne couvre que quelques cases.
///
/// Un seau n'est qu'un <b>filtre</b> : l'appelant applique ensuite le test de rayon exact, donc
/// l'ensemble des candidats est celui d'un balayage complet. L'index ne peut pas changer les
/// entrées observées, seulement le nombre de cases visitées pour les trouver.
///
/// Les seaux sont réutilisés d'un tick à l'autre (listes remises dans un pool), pour que la
/// reconstruction par tick ne soit pas elle-même une source d'allocations.
/// </summary>
public sealed class PerceptionSpatialIndex
{
    /// <summary>16 × 16 cases : un rayon proche usuel couvre 1 à 9 seaux.</summary>
    private const int BucketShift = 4;

    private readonly Dictionary<long, List<AgentBody>> _agentBuckets = [];
    private readonly Dictionary<long, List<GroundItem>> _itemBuckets = [];
    private readonly List<List<AgentBody>> _agentPool = [];
    private readonly List<List<GroundItem>> _itemPool = [];

    /// <summary>Nombre de seaux d'agents occupés au dernier <see cref="Rebuild"/> (diagnostic).</summary>
    public int AgentBucketCount => _agentBuckets.Count;

    /// <summary>Nombre de seaux d'objets occupés au dernier <see cref="Rebuild"/> (diagnostic).</summary>
    public int ItemBucketCount => _itemBuckets.Count;

    /// <summary>
    /// Reconstruit l'index depuis l'état courant. Coût linéaire en agents + objets, une fois par
    /// tick, au lieu d'un parcours complet par agent.
    /// </summary>
    public void Rebuild(IReadOnlyList<AgentBody> agents, GroundItemStore groundItems)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(groundItems);

        RecycleAgentBuckets();
        foreach (var agent in agents)
        {
            Bucket(_agentBuckets, _agentPool, agent.Position.CellX, agent.Position.CellY).Add(agent);
        }

        RecycleItemBuckets();
        foreach (var item in groundItems.Items)
        {
            Bucket(_itemBuckets, _itemPool, item.Position.X, item.Position.Y).Add(item);
        }
    }

    /// <summary>
    /// Remplit <paramref name="destination"/> avec les agents des seaux couvrant le disque
    /// demandé. Les candidats ne sont pas filtrés finement : l'appelant applique son test de
    /// rayon, ce qui garde une seule définition de « à portée ».
    /// </summary>
    public void CollectAgents(int centerX, int centerY, int radius, List<AgentBody> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Collect(_agentBuckets, centerX, centerY, radius, destination);
    }

    /// <summary>Idem pour les objets au sol.</summary>
    public void CollectItems(int centerX, int centerY, int radius, List<GroundItem> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Collect(_itemBuckets, centerX, centerY, radius, destination);
    }

    private void RecycleAgentBuckets()
    {
        foreach (var bucket in _agentBuckets.Values)
        {
            bucket.Clear();
            _agentPool.Add(bucket);
        }

        _agentBuckets.Clear();
    }

    private void RecycleItemBuckets()
    {
        foreach (var bucket in _itemBuckets.Values)
        {
            bucket.Clear();
            _itemPool.Add(bucket);
        }

        _itemBuckets.Clear();
    }

    private static List<T> Bucket<T>(
        Dictionary<long, List<T>> buckets,
        List<List<T>> pool,
        int x,
        int y)
    {
        var key = Key(x >> BucketShift, y >> BucketShift);
        if (buckets.TryGetValue(key, out var bucket))
        {
            return bucket;
        }

        // Le pool rend la reconstruction par tick sans allocation après la première passe : les
        // listes d'un tick précédent sont vidées puis réutilisées.
        if (pool.Count > 0)
        {
            bucket = pool[^1];
            pool.RemoveAt(pool.Count - 1);
        }
        else
        {
            bucket = [];
        }

        buckets.Add(key, bucket);
        return bucket;
    }

    private static void Collect<T>(
        Dictionary<long, List<T>> buckets,
        int centerX,
        int centerY,
        int radius,
        List<T> destination)
    {
        destination.Clear();
        if (buckets.Count == 0 || radius < 0)
        {
            return;
        }

        // Un disque de rayon r tient dans le carré [c-r, c+r] ; on ne visite que les seaux de ce
        // carré. La borne basse est arrondie vers le bas et la haute vers le haut, donc aucun
        // seau partiellement couvert n'est oublié.
        int minX = (centerX - radius) >> BucketShift;
        int maxX = (centerX + radius) >> BucketShift;
        int minY = (centerY - radius) >> BucketShift;
        int maxY = (centerY + radius) >> BucketShift;
        for (var bucketY = minY; bucketY <= maxY; bucketY++)
        {
            for (var bucketX = minX; bucketX <= maxX; bucketX++)
            {
                if (buckets.TryGetValue(Key(bucketX, bucketY), out var bucket))
                {
                    destination.AddRange(bucket);
                }
            }
        }
    }

    private static long Key(int bucketX, int bucketY) =>
        ((long)bucketX << 32) ^ (uint)bucketY;
}
