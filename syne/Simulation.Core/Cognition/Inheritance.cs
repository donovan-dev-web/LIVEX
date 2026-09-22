using Simulation.Core.Configuration;
using Simulation.Core.Entities;

namespace Simulation.Core.Cognition;

/// <summary>
/// Héritage intergénérationnel (SYNE-020, décision n°16, DATA_MODEL.md §6.6) :
/// la naissance repose sur la **fusion consentie** des entités parentes et
/// transmet les **traits** et le **savoir** (mémoire intergénérationnelle +
/// croyances) à l'entité née.
///
/// La structure est figée (décision n°16) ; les paramètres fins (réadaptation,
/// dominance, mutation, seuil de salience) restent configurables
/// (<see cref="InheritanceSettings"/>, SYNE-063 — §6.6.3, V0.2).
/// </summary>
public static class Inheritance
{
    /// <summary>Seuil de salience par défaut d'un souvenir parent transmis (V0.1, désormais <see cref="InheritanceSettings.SalienceThreshold"/>).</summary>
    public const double DefaultSalienceThreshold = 0.01;

    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
    private const ulong Mix1 = 0xBF58476D1CE4E5B9UL;
    private const ulong Mix2 = 0x94D049BB133111EBUL;

    /// <summary>
    /// Fusion des traits par moyenne arithmétique par trait (décision n°16,
    /// §6.6.2) — équivalent à <c>FuseTraits(a, b, new InheritanceSettings(), seed)</c>
    /// (dominance 0, mutation 0) : préservé pour la rétro-compatibilité V0.1.
    /// </summary>
    public static TraitSet FuseTraits(TraitSet parentA, TraitSet parentB)
    {
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);

        var fused = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string name in TraitSet.TraitNames)
        {
            double value = (parentA[name] + parentB[name]) / 2.0;
            fused[name] = Math.Clamp(value, TraitSet.Min, TraitSet.Max);
        }

        return new TraitSet(fused);
    }

    /// <summary>
    /// Mécanismes fins (SYNE-063, décision n°16, §6.6.3) — déterministes (hash
    /// SplitMix64 stable, aucun PRNG global) :
    /// <list type="bullet">
    /// <item><b>réadaptation/dominance</b> (<c>dominance</c> ∈ [0, 1]) : à 0, fusion
    ///  égalitaire (moyenne, V0.1) ; au-delà, le trait du parent « exprimant »
    ///  (écart au neutre 1.0 le plus grand) pèse d'autant plus (pleine à 1) ;</item>
    /// <item><b>mutation</b> (<c>mutationRate</c>) : tirage stable par trait via la
    ///  graine <paramref name="seed"/>, perturbation bornée <c>± mutationMagnitude</c>,
    ///  plage [0, 2] respectée.</item>
    /// </list>
    /// </summary>
    public static TraitSet FuseTraits(
        TraitSet parentA,
        TraitSet parentB,
        InheritanceSettings settings,
        ulong seed)
    {
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);
        ArgumentNullException.ThrowIfNull(settings);

        var fused = new Dictionary<string, double>(StringComparer.Ordinal);
        for (int index = 0; index < TraitSet.TraitNames.Count; index++)
        {
            string name = TraitSet.TraitNames[index];
            double mother = parentA[name];
            double father = parentB[name];
            double average = (mother + father) / 2.0;

            double express = Math.Abs(mother - 1.0) >= Math.Abs(father - 1.0) ? mother : father;
            double value = (average * (1.0 - settings.Dominance)) + (express * settings.Dominance);

            if (settings.MutationRate > 0.0)
            {
                double draw = Draw01(seed ^ ((ulong)index * Mix1));
                if (draw < settings.MutationRate)
                {
                    double delta = (Draw01(seed ^ GoldenGamma ^ ((ulong)index * Mix2)) - 0.5) * 2.0 * settings.MutationMagnitude;
                    value += delta;
                }
            }

            fused[name] = Math.Clamp(value, TraitSet.Min, TraitSet.Max);
        }

        return new TraitSet(fused);
    }

    private static double Draw01(ulong z)
    {
        ulong h = SplitMix(z);
        return (h % 10001) / 10000.0;
    }

    private static ulong SplitMix(ulong z)
    {
        z = (z ^ (z >> 30)) * Mix1;
        z = (z ^ (z >> 27)) * Mix2;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// Mémoire intergénérationnelle (§6.6.3) : réunit les souvenirs des parents
    /// dont la salience dépasse <paramref name="salienceThreshold"/> (défaut :
    /// <see cref="InheritanceSettings.SalienceThreshold"/>, évaluée au tick courant),
    /// ré-horodatés au <paramref name="birthTick"/>. L'ordre d'insertion est
    /// déterministe (parents puis tri par StoredAt/Sequence).
    /// </summary>
    public static Memory InheritMemory(
        IEnumerable<MemoryEntry> parentalMemory,
        MemorySettings settings,
        ulong birthTick,
        double? salienceThreshold = null)
    {
        ArgumentNullException.ThrowIfNull(parentalMemory);
        ArgumentNullException.ThrowIfNull(settings);

        double threshold = salienceThreshold ?? DefaultSalienceThreshold;
        var inherited = new Memory(settings);
        foreach (MemoryEntry entry in parentalMemory
            .OrderBy(memory => memory.StoredAt)
            .ThenBy(memory => memory.Sequence))
        {
            // Salience au moment de la naissance : l'entité naît « présente » à
            // la connaissance des expériences parentes.
            double age = birthTick >= entry.StoredAt ? (double)(birthTick - entry.StoredAt) : 0.0;
            double salience = Math.Exp(-inherited.DecayRateFor(entry.Category) * age);
            if (salience < threshold)
            {
                continue;
            }

            inherited.Store(
                entry.Category,
                entry.Source,
                entry.Content,
                entry.Confidence,
                birthTick);
        }

        return inherited;
    }

    /// <summary>
    /// Croyances transmises (§6.6.3 « savoir ») : union des croyances parentes ;
    /// sur un fait identique porté par les deux parents, la confiance la plus
    /// haute est conservée. Source ramenée à « héritage » (traçabilité V0.1).
    /// </summary>
    public static BeliefSet InheritBeliefs(
        IEnumerable<Belief> parentalBeliefs,
        BeliefSettings settings,
        ulong birthTick)
    {
        ArgumentNullException.ThrowIfNull(parentalBeliefs);
        ArgumentNullException.ThrowIfNull(settings);

        var beliefs = new BeliefSet();
        var strongest = new Dictionary<Fact, Belief>(beliefs.Count);
        foreach (Belief belief in parentalBeliefs)
        {
            if (strongest.TryGetValue(belief.Fact, out Belief? existing))
            {
                if (belief.Confidence > existing.Confidence)
                {
                    strongest[belief.Fact] = belief;
                }
            }
            else
            {
                strongest[belief.Fact] = belief;
            }
        }

        foreach (Belief parental in strongest.Values
            .OrderBy(belief => belief.Fact.Subject, StringComparer.Ordinal)
            .ThenBy(belief => belief.Fact.Predicate, StringComparer.Ordinal)
            .ThenBy(belief => belief.Fact.Value, StringComparer.Ordinal))
        {
            beliefs.Upsert(new Belief(
                parental.Fact,
                parental.Confidence,
                "héritage",
                birthTick,
                birthTick + settings.ExpiryTicks));
        }

        return beliefs;
    }
}