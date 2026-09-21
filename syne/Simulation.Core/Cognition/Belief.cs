using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

/// <summary>
/// Fait : (subject, predicate, value) — DATA_MODEL.md §6. Les trois champs sont
/// comparés sur leur valeur ordinale (insensible à la culture).
/// </summary>
public readonly record struct Fact(string Subject, string Predicate, string Value);

/// <summary>
/// Croyance : interprétation d'un fait avec une confiance 0-1, une source et un
/// cycle de vie (création → confirmation → conflit → décroissance → expiration).
/// </summary>
public sealed class Belief
{
    public Belief(Fact fact, double confidence, string source, ulong bornTick, ulong expiryTick)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (confidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "La confiance doit être dans [0, 1].");
        }

        Fact = fact;
        Confidence = confidence;
        Source = source;
        BornTick = bornTick;
        UpdatedTick = bornTick;
        ExpiryTick = expiryTick;
    }

    public Belief(Fact fact, double confidence, string source, ulong bornTick, ulong updatedTick, ulong expiryTick)
        : this(fact, confidence, source, bornTick, expiryTick)
    {
        UpdatedTick = updatedTick;
    }

    public Fact Fact { get; }

    public double Confidence { get; private set; }

    public string Source { get; }

    public ulong BornTick { get; }

    public ulong UpdatedTick { get; private set; }

    public ulong ExpiryTick { get; }

    public bool IsExpired(ulong currentTick) => currentTick >= ExpiryTick;

    internal void SetConfidence(double value)
    {
        Confidence = value;
    }

    internal void Touch(ulong tick)
    {
        UpdatedTick = tick;
    }
}

/// <summary>
/// Révision des croyances (décision n°12, SYNE-014) :
/// <c>belief = belief + (signal − belief) × strength</c>, avec plafond de
/// variation par snap (« plafond par snap » : un signal trompeur ne peut pas
/// inverser une croyance forte d'un coup).
/// </summary>
public static class BeliefRevision
{
    public static double Revise(double current, double signal, double strength, double maxChangePerSnap)
    {
        if (double.IsNaN(signal) || double.IsInfinity(signal))
        {
            throw new ArgumentOutOfRangeException(nameof(signal), "Le signal doit être un nombre fini.");
        }

        signal = Math.Clamp(signal, 0.0, 1.0);
        double delta = (signal - current) * strength;
        delta = Math.Clamp(delta, -maxChangePerSnap, maxChangePerSnap);
        return Math.Clamp(current + delta, 0.0, 1.0);
    }
}

/// <summary>
/// Ensemble de croyances d'une entité, indexé par fait (ProductName de
/// comparaison ordinale). Les croyances expirent (plafond 0.4) et décroissent
/// temporellement (DATA_MODEL.md §6, COGNITIVE_ARCHITECTURE.md §4).
/// </summary>
public sealed class BeliefSet
{
    private readonly Dictionary<Fact, Belief> _beliefs = new();

    public int Count => _beliefs.Count;

    public bool TryGet(Fact fact, out Belief belief) => _beliefs.TryGetValue(fact, out belief!);

    public IReadOnlyList<Belief> All => _beliefs.Values.ToList();

    internal void Upsert(Belief belief) => _beliefs[belief.Fact] = belief;

    /// <summary>
    /// Applique une observation (COGNITIVE_ARCHITECTURE.md §4) :
    /// <list type="bullet">
    /// <item>fait inconnu → création à la confiance du signal ;</item>
    /// <item>même fait, même source → alignement (+0.2, max 1.0) ;</item>
    /// <item>même fait, source différente → moyenne des confiances ;</item>
    /// <item>fait contradictoire (même sujet + prédicat, valeur différente) →
    ///  conflit (−0.1, min 0.1) sur les croyances concurrentes existantes +
    ///  création d'une nouvelle croyance au signal.</item>
    /// </list>
    /// </summary>
    public void ApplyEvidence(
        Fact fact,
        double signal,
        string source,
        BeliefSettings settings,
        ulong tick)
    {
        if (_beliefs.TryGetValue(fact, out Belief? existing))
        {
            if (existing.Source == source)
            {
                existing.SetConfidence(Math.Min(1.0, existing.Confidence + settings.AlignBonus));
                existing.Touch(tick);
                return;
            }

            if (existing.Confidence >= 0.5)
            {
                existing.SetConfidence((existing.Confidence + Math.Clamp(signal, 0.0, 1.0)) / 2.0);
                existing.Touch(tick);
                return;
            }

            existing.SetConfidence(Math.Max(0.1, existing.Confidence - settings.ConflictPenalty));
            existing.Touch(tick);
            _beliefs[fact] = new Belief(fact, Math.Clamp(signal, 0.0, 1.0), source, tick, tick + settings.ExpiryTicks);
            return;
        }

        PenalizeConflicting(fact, settings, tick);
        _beliefs[fact] = new Belief(fact, Math.Clamp(signal, 0.0, 1.0), source, tick, tick + settings.ExpiryTicks);
    }

    /// <summary>
    /// Conflit : toute croyance portant sur le même sujet + prédicat mais une
    /// valeur différente est pénalisée (data_model.md §6 — deux entités ne
    /// peuvent être observées au même endroit que depuis un signal contradictoire).
    /// </summary>
    private void PenalizeConflicting(Fact fact, BeliefSettings settings, ulong tick)
    {
        foreach (Belief belief in _beliefs.Values)
        {
            if (belief.Fact.Subject == fact.Subject &&
                belief.Fact.Predicate == fact.Predicate &&
                belief.Fact.Value != fact.Value)
            {
                belief.SetConfidence(Math.Max(0.1, belief.Confidence - settings.ConflictPenalty));
                belief.Touch(tick);
            }
        }
    }

    /// <summary>
    /// Passe temporel : décroissance (× <c>timeDecayPerTick</c>) puis, si la
    /// croyance est expirée, plafonnement à <c>expiredCap</c> (0.4).
    /// </summary>
    public void Tick(ulong currentTick, BeliefSettings settings)
    {
        foreach (Belief belief in _beliefs.Values)
        {
            if (belief.UpdatedTick == currentTick)
            {
                continue;
            }

            double value = belief.Confidence * settings.TimeDecayPerTick;
            if (belief.IsExpired(currentTick))
            {
                value = Math.Min(value, settings.ExpiredCap);
            }

            belief.SetConfidence(value);
        }
    }
}