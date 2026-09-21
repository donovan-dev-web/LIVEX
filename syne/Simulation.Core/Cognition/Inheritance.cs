using Simulation.Core.Configuration;
using Simulation.Core.Entities;

namespace Simulation.Core.Cognition;

/// <summary>
/// Héritage intergénérationnel (SYNE-020, décision n°16, DATA_MODEL.md §6.6) :
/// la naissance repose sur la **fusion consentie** des entités parentes et
/// transmet les **traits** et le **savoir** (mémoire intergénérationnelle +
/// croyances) à l'entité née.
///
/// La structure est figée (décision n°16) ; les paramètres fins (seuil de
/// salience des souvenirs transmis, horizon des croyances) restent configurables
/// pour la calibration V0.1.
/// </summary>
public static class Inheritance
{
    /// <summary>Seuil de salience d'un souvenir parent pour être transmis (V0.1, configurable en calibration).</summary>
    public const double DefaultSalienceThreshold = 0.01;

    /// <summary>
    /// Fusion des traits par moyenne arithmétique par trait (décision n°16,
    /// §6.6.2) — la réadaptation/dominance/mutation restent configurables (V0.1).
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
    /// Mémoire intergénérationnelle (§6.6.3) : réunit les souvenirs des parents
    /// dont la salience dépasse <paramref name="salienceThreshold"/> (évaluée au
    /// tick courant), ré-horodatés au <paramref name="birthTick"/>. L'ordre
    /// d'insertion est déterministe (parents puis tri par StoredAt/Sequence).
    /// </summary>
    public static Memory InheritMemory(
        IEnumerable<MemoryEntry> parentalMemory,
        MemorySettings settings,
        ulong birthTick,
        double salienceThreshold = DefaultSalienceThreshold)
    {
        ArgumentNullException.ThrowIfNull(parentalMemory);
        ArgumentNullException.ThrowIfNull(settings);

        var inherited = new Memory(settings);
        foreach (MemoryEntry entry in parentalMemory
            .OrderBy(memory => memory.StoredAt)
            .ThenBy(memory => memory.Sequence))
        {
            // Salience au moment de la naissance : l'entité naît « présente » à
            // la connaissance des expériences parentes.
            double age = birthTick >= entry.StoredAt ? (double)(birthTick - entry.StoredAt) : 0.0;
            double salience = Math.Exp(-inherited.DecayRateFor(entry.Category) * age);
            if (salience < salienceThreshold)
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