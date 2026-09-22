namespace Simulation.Core.Cognition;

/// <summary>
/// Résolution probabiliste des conflits de priorités (SYNE-033, décision n°22,
/// SYSTEMS_SPEC.md §5) : quand plusieurs candidats se disputent le maximum
/// d'utilité (marge <c>conflictTieMargin</c>), le vainqueur est tiré selon la
/// <b>force × confiance</b> de chaque désir — <c>p = force × confidence / Σ</c> —
/// sans aucun arbitraire d'ancienneté.
///
/// Déterminisme : le tirage dérive d'un hash SplitMix64 stable de
/// (entityId, tick, kinds) — aucune consommation du PRNG global ; à force nulle
/// des deux côtés, l'ordre du catalogue (stable) tranche.
/// </summary>
public static class PriorityConflictResolver
{
    /// <summary>Résout un tournoi pair-à-pair déterministe parmi les candidats contestés.</summary>
    public static UtilityScore Resolve(
        IReadOnlyList<UtilityScore> candidates,
        BodyNeeds needs,
        ulong entityId,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(needs);

        if (candidates.Count == 0)
        {
            throw new ArgumentException("Aucun candidat à résoudre.", nameof(candidates));
        }

        UtilityScore winner = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            winner = Contest(winner, candidates[i], needs, entityId, tick);
        }

        return winner;
    }

    private static UtilityScore Contest(UtilityScore a, UtilityScore b, BodyNeeds needs, ulong entityId, ulong tick)
    {
        double strengthA = needs.Drive(a.Kind) * a.Confidence;
        double strengthB = needs.Drive(b.Kind) * b.Confidence;

        if (strengthA + strengthB <= 0.0)
        {
            return a.Kind < b.Kind ? a : b;
        }

        double probabilityA = strengthA / (strengthA + strengthB);
        double draw = Draw(entityId, tick, a.Kind, b.Kind);
        return draw < probabilityA ? a : b;
    }

    /// <summary>Tirage déterministe (SplitMix64, sans passerelle RNG) dans [0, 1).</summary>
    private static double Draw(ulong entityId, ulong tick, DesireKind a, DesireKind b)
    {
        ulong h = entityId ^ (tick * 0x9E3779B97F4A7C15UL);
        h ^= (ulong)a * 0xBF58476D1CE4E5B9UL;
        h ^= (ulong)b * 0x94D049BB133111EBUL;
        h ^= h >> 30;
        h *= 0xBF58476D1CE4E5B9UL;
        h ^= h >> 27;
        h *= 0x94D049BB133111EBUL;
        h ^= h >> 31;

        return (h % 10000) / 10000.0;
    }
}