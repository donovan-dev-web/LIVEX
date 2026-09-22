using Simulation.Core.Actions;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Les 6 désirs V0.1 (DATA_MODEL.md §7) + Idle, puis les actions terminales de
/// besoin Eat/Drink (SYNE-042, décision n°4). Chaque désir découle d'un besoin
/// non satisfait (objectif = état recherché, COGNITIVE_ARCHITECTURE.md §5).
/// <list type="bullet">
///   <item>Les valeurs existantes (0-6) ne changent jamais : l'ordre du catalogue
///    est stable pour le départage déterministe (décision n°22, DETERMINISM.md §5).</item>
///   <item>Eat/Drink (7-8) : actions terminales immédiates, non persistantes
///    (exécutées tant que le besoin est déclenché, jamais en holdover).</item>
/// </list>
/// </summary>
public enum DesireKind
{
    Idle = 0,
    SeekFood = 1,
    SeekWater = 2,
    Rest = 3,
    Flee = 4,
    Socialize = 5,
    Explore = 6,
    Eat = 7,
    Drink = 8,
}

/// <summary>État recherché (objectif) né d'un besoin non satisfait (COGNITIVE_ARCHITECTURE.md §5).</summary>
public readonly record struct Goal(DesireKind Kind, ulong BornTick)
{
    public ulong Age(ulong currentTick) => currentTick >= BornTick ? currentTick - BornTick : 0;
}

/// <summary>
/// Générateur d'objectifs : un désir est créé quand son besoin passe son seuil
/// (SYNE-042, décision n°4 : déclenchement dès « ≥ 50 ») et qu'aucun objectif du
/// même type n'est actif. Pour faim/soif, le désir engendré est l'action terminale
/// <see cref="DesireKind.Eat"/>/<see cref="DesireKind.Drink"/> si la réserve globale
/// correspondante est disponible, sinon le <see cref="DesireKind.SeekFood"/>/
/// <see cref="DesireKind.SeekWater"/> de poursuite (catalogue, SYNE-040) — le
/// déclencheur est donc viable par construction.
/// </summary>
public static class DesireFactory
{
    public static IReadOnlyList<Goal> Generate(
        BodyNeeds needs,
        ulong tick,
        IEnumerable<DesireKind> activeKinds,
        ActionCatalog catalog,
        ResourceStocks stocks)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stocks);

        var goals = new List<Goal>();
        var active = new HashSet<DesireKind>(activeKinds);

        foreach (DesireKind kind in Enum.GetValues<DesireKind>())
        {
            // Les actions terminales Eat/Drink (SYNE-042) ne sont jamais générées
            // directement : elles sont résolues depuis SeekFood/SeekWater via
            // ResolveTerminal (sinon doublons d'objectifs).
            if (kind is DesireKind.Idle or DesireKind.Eat or DesireKind.Drink)
            {
                continue;
            }

            DesireKind candidate = ResolveTerminal(kind, catalog, stocks);
            if (!active.Contains(candidate) && needs.IsTriggered(kind))
            {
                goals.Add(new Goal(candidate, tick));
            }
        }

        return goals;
    }

    /// <summary>
    /// Faim/soif : préfère l'action terminale Eat/Drink si la réserve est
    /// disponible, sinon revient à la poursuite SeekFood/SeekWater.
    /// </summary>
    private static DesireKind ResolveTerminal(DesireKind kind, ActionCatalog catalog, ResourceStocks stocks)
    {
        if (kind == DesireKind.SeekFood)
        {
            return catalog.IsViable(DesireKind.Eat, stocks) ? DesireKind.Eat : DesireKind.SeekFood;
        }

        if (kind == DesireKind.SeekWater)
        {
            return catalog.IsViable(DesireKind.Drink, stocks) ? DesireKind.Drink : DesireKind.SeekWater;
        }

        return kind;
    }
}