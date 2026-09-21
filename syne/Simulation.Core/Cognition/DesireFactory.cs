namespace Simulation.Core.Cognition;

/// <summary>
/// Les 6 désirs V0.1 (DATA_MODEL.md §7) + Idle. Chaque désir découle d'un besoin
/// non satisfait (objectif = état recherché, COGNITIVE_ARCHITECTURE.md §5).
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
}

/// <summary>État recherché (objectif) né d'un besoin non satisfait (COGNITIVE_ARCHITECTURE.md §5).</summary>
public readonly record struct Goal(DesireKind Kind, ulong BornTick)
{
    public ulong Age(ulong currentTick) => currentTick >= BornTick ? currentTick - BornTick : 0;
}

/// <summary>
/// Générateur d'objectifs : un désir est créé quand son besoin passe son seuil
/// (DATA_MODEL.md §7) et qu'aucun objectif du même type n'est actif.
/// </summary>
public static class DesireFactory
{
    public static IReadOnlyList<Goal> Generate(BodyNeeds needs, ulong tick, IEnumerable<DesireKind> activeKinds)
    {
        ArgumentNullException.ThrowIfNull(needs);

        var goals = new List<Goal>();
        var active = new HashSet<DesireKind>(activeKinds);

        foreach (DesireKind kind in Enum.GetValues<DesireKind>())
        {
            if (kind == DesireKind.Idle)
            {
                continue;
            }

            if (!active.Contains(kind) && needs.IsTriggered(kind))
            {
                goals.Add(new Goal(kind, tick));
            }
        }

        return goals;
    }
}