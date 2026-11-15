namespace Prism.Core.Navigation;

/// <summary>
/// Counters of the path searches a caller ran: how many ran, how many were served by the cache,
/// how many nodes were developed, and how many searches gave up.
/// </summary>
/// <remarks>
/// <para>
/// PRISM-104 : « une recherche qui épuise son budget doit être visible ». Le coût d'un tick ne se
/// lit pas dans le nombre d'agents mais dans le travail perdu, et rien ne le montrait : le profil
/// donnait la durée d'<c>ApplyWrites</c>, pas la part dépensée dans des recherches sans résultat.
/// Les compteurs sont donc tenus par l'appelant (pas de statique : les tests s'exécutent en
/// parallèle) et exposés par le profil opt-in ; désactivés, ils ne coûtent qu'un incrément.
/// </para>
/// <para>
/// Entiers seulement : <c>Prism.Core</c> refuse le flottant et le temps mural (ADR-007).
/// </para>
/// </remarks>
public sealed class PathfindingMetrics
{
    private long _searches;
    private long _cacheHits;
    private long _cacheInvalidations;
    private long _expandedNodes;
    private long _failedSearches;
    private long _budgetExhaustedSearches;

    /// <summary>Recherches réellement exécutées (un cache-hit n'en est pas une).</summary>
    public long Searches => _searches;

    /// <summary>Recherches servies par le cache sans développer un seul nœud.</summary>
    public long CacheHits => _cacheHits;

    /// <summary>Entrées de cache trouvées mais périmées : une région lue a été écrite depuis.</summary>
    public long CacheInvalidations => _cacheInvalidations;

    /// <summary>Somme des nœuds développés par toutes les recherches.</summary>
    public long ExpandedNodes => _expandedNodes;

    /// <summary>Recherches terminées sans chemin alors que le graphe a été épuisé (cible inaccessible).</summary>
    public long FailedSearches => _failedSearches;

    /// <summary>
    /// Recherches abandonnées faute de budget (« recherches épuisant leur budget » du critère
    /// d'acceptation) : le plafond de nœuds a été atteint avant d'atteindre la cible.
    /// </summary>
    public long BudgetExhaustedSearches => _budgetExhaustedSearches;

    /// <summary>Instantané immuable des six compteurs, pour un delta par tick.</summary>
    public PathfindingMetricsSnapshot Snapshot() => new(
        _searches,
        _cacheHits,
        _cacheInvalidations,
        _expandedNodes,
        _failedSearches,
        _budgetExhaustedSearches);

    internal void RecordSearch(PathfindingStatus status, int expandedNodes)
    {
        _searches++;
        _expandedNodes += expandedNodes;
        if (status == PathfindingStatus.NotFound)
        {
            _failedSearches++;
        }
        else if (status == PathfindingStatus.NodeBudgetExceeded)
        {
            _budgetExhaustedSearches++;
        }
    }

    internal void RecordCacheHit() => _cacheHits++;

    internal void RecordCacheInvalidation() => _cacheInvalidations++;
}

/// <summary>
/// Valeur figée des compteurs de <see cref="PathfindingMetrics"/> ; se soustrait pour obtenir le
/// delta d'un tick sans remettre les totaux à zéro.
/// </summary>
public readonly record struct PathfindingMetricsSnapshot(
    long Searches,
    long CacheHits,
    long CacheInvalidations,
    long ExpandedNodes,
    long FailedSearches,
    long BudgetExhaustedSearches)
{
    /// <summary>Recherches sans chemin ni budget restant, en pour-mille du total (0 sans recherche).</summary>
    public int FailureRatePermille => Searches == 0
        ? 0
        : (int)Math.Min(1000, ((FailedSearches + BudgetExhaustedSearches) * 1000) / Searches);

    public static PathfindingMetricsSnapshot operator -(
        PathfindingMetricsSnapshot after,
        PathfindingMetricsSnapshot before) => new(
        after.Searches - before.Searches,
        after.CacheHits - before.CacheHits,
        after.CacheInvalidations - before.CacheInvalidations,
        after.ExpandedNodes - before.ExpandedNodes,
        after.FailedSearches - before.FailedSearches,
        after.BudgetExhaustedSearches - before.BudgetExhaustedSearches);
}
