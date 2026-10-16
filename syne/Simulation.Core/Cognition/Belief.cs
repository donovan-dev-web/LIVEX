// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Fait et croyance : interprétation d'un fait avec une confiance 0-1 et un cycle de vie.

using Simulation.Core.Configuration;

namespace Simulation.Core.Cognition;

/// <summary>
/// Fait : (subject, predicate, value). Les trois champs sont
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

    public Fact Fact { get; private set; }

    public double Confidence { get; private set; }

    public string Source { get; private set; }

    public ulong BornTick { get; }

    public ulong UpdatedTick { get; private set; }

    /// <summary>
    /// Tick d'expiration — <b>réarmé à chaque confirmation</b>
    /// (<see cref="Confirm"/>) : une croyance réobservée ne peut pas expirer alors
    /// que son objet est sous les yeux de l'agent.
    /// </summary>
    public ulong ExpiryTick { get; private set; }

    public bool IsExpired(ulong currentTick) => currentTick >= ExpiryTick;

    internal void SetConfidence(double value)
    {
        Confidence = value;
    }

    internal void Touch(ulong tick)
    {
        UpdatedTick = tick;
    }

    /// <summary>Confirmation : la croyance vient d'être réobservée — horodatage et expiration réarmés.</summary>
    internal void Confirm(ulong tick, ulong expiryTicks)
    {
        UpdatedTick = tick;
        ExpiryTick = tick + expiryTicks;
    }

    /// <summary>
    /// Remplacement de la valeur crue (sujet mobile : « X était en P, X est
    /// maintenant en P' ») : la croyance reste celle du <b>sujet</b> et du prédicat,
    /// sa confiance est révisée depuis l'ancienne, sa source et son cycle de vie sont
    /// réarmés. L'ancienne valeur n'est pas conservée comme croyance parallèle
    /// (S-09 : une seule croyance courante par sujet et prédicat).
    /// </summary>
    internal void Supersede(Fact fact, double confidence, string source, ulong tick, ulong expiryTick)
    {
        ArgumentNullException.ThrowIfNull(source);
        Fact = fact;
        Confidence = Math.Clamp(confidence, 0.0, 1.0);
        Source = source;
        UpdatedTick = tick;
        ExpiryTick = expiryTick;
    }
}

/// <summary>
/// Révision des croyances (SYNE-014) :
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
/// Ensemble de croyances d'une entité : <b>une croyance courante par couple
/// (sujet, prédicat)</b> — « où est X », « quel est l'état du gisement N ». Les
/// croyances expirent (plafond 0.4) et décroissent temporellement.
///
/// <para>
/// <b>Réalignement S-08/S-09 (déplacement, conflit, âge)</b> : l'ensemble était
/// indexé par <i>fait complet</i>, si bien qu'un sujet qui se déplace créait une
/// nouvelle croyance par position observée — les anciennes restaient, pénalisées
/// mais vivantes, et chaque nouveau fait déclenchait un balayage de
/// <i>toutes</i> les croyances pour pénaliser les « contradictoires ». À densité
/// (agents rassemblés autour d'un gisement), la taille de l'ensemble et le coût
/// par observation croissaient sans borne. La croyance représente bien ici
/// l'<b>état courant</b> de l'agent sur un sujet : une observation d'une autre
/// valeur <b>remplace</b> la précédente, après pénalité de conflit, en révisant sa
/// confiance (la croyance antérieure sert de <i>prior</i>) — c'est la formule de
/// révision du chapitre XI, et le paramètre <c>ConflictPenalty</c> redevient
/// effectif pour un sujet mobile.
/// </para>
/// </summary>
public sealed class BeliefSet
{
    private readonly Dictionary<(string Subject, string Predicate), Belief> _beliefs = new();

    /// <summary>Nombre de sujets crus (une croyance courante par sujet et prédicat).</summary>
    public int Count => _beliefs.Count;

    /// <summary>Croyance portant <b>exactement</b> ce fait (sujet, prédicat et valeur).</summary>
    public bool TryGet(Fact fact, out Belief belief) =>
        _beliefs.TryGetValue((fact.Subject, fact.Predicate), out belief!) &&
        string.Equals(belief.Fact.Value, fact.Value, StringComparison.Ordinal);

    /// <summary>Croyance <b>courante</b> sur un sujet et un prédicat, quelle que soit la valeur crue.</summary>
    public bool TryGetCurrent(string subject, string predicate, out Belief belief) =>
        _beliefs.TryGetValue((subject, predicate), out belief!);

    public IReadOnlyList<Belief> All => _beliefs.Values.ToList();

    /// <summary>
    /// Croyances sans copie (parcours chaud : construction de la connaissance,
    /// recherche de faits justificatifs). Ordre d'insertion du dictionnaire —
    /// identique à <see cref="All"/> ; réservé aux parcours qui ne comparent que des
    /// minima (l'ordre ne tranche alors que des égalités stables).
    /// </summary>
    internal IReadOnlyCollection<Belief> AllInPlace => _beliefs.Values;

    /// <summary>Toutes les croyances triées par fait (sujet, prédicat, valeur — ordinal, déterminisme d'émission).</summary>
    public IReadOnlyList<Belief> OrderedByFact() =>
        All
            .OrderBy(belief => belief.Fact.Subject, StringComparer.Ordinal)
            .ThenBy(belief => belief.Fact.Predicate, StringComparer.Ordinal)
            .ThenBy(belief => belief.Fact.Value, StringComparer.Ordinal)
            .ToList();

    internal void Upsert(Belief belief) => _beliefs[(belief.Fact.Subject, belief.Fact.Predicate)] = belief;

    /// <summary>
    /// Applique une observation :
    /// <list type="bullet">
    /// <item>fait inconnu → création à la confiance du signal ;</item>
    /// <item>même fait, même source → alignement (+0.2, max 1.0) ;</item>
    /// <item>même fait, source différente → moyenne des confiances ;</item>
    /// <item>fait contradictoire (même sujet + prédicat, valeur différente) →
    ///  conflit (−0.1, min 0.1) sur la croyance courante, puis
    ///  <b>remplacement</b> de sa valeur à la confiance révisée (S-09 : une seule
    ///  croyance courante par sujet et prédicat, l'ancienne valeur ne survit pas).</item>
    /// </list>
    /// </summary>
    public void ApplyEvidence(
        Fact fact,
        double signal,
        string source,
        BeliefSettings settings,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (TryGetCurrent(fact.Subject, fact.Predicate, out Belief? known))
        {
            if (string.Equals(known.Fact.Value, fact.Value, StringComparison.Ordinal))
            {
                if (known.Source == source)
                {
                    known.SetConfidence(Math.Min(1.0, known.Confidence + settings.AlignBonus));
                    known.Touch(tick);
                    return;
                }

                if (known.Confidence >= 0.5)
                {
                    known.SetConfidence((known.Confidence + Math.Clamp(signal, 0.0, 1.0)) / 2.0);
                    known.Touch(tick);
                    return;
                }
            }

            // Valeur différente : le sujet a changé d'état (déplacement) ou le signal
            // contredit la croyance. Une seule croyance courante par (sujet, prédicat)
            // (S-09) : la croyance antérieure est pénalisée (ConflictPenalty) et sert
            // de <i>prior</i> à la révision bornée vers le nouveau signal — la
            // pénalité de conflit reste ainsi <b>effective</b> alors que l'ancienne
            // valeur, elle, ne survit pas comme croyance parallèle.
            double prior = Math.Max(0.1, known.Confidence - settings.ConflictPenalty);
            known.Supersede(
                fact,
                BeliefRevision.Revise(
                    prior,
                    Math.Clamp(signal, 0.0, 1.0),
                    settings.UpdateStrength,
                    settings.MaxChangePerSnap),
                source,
                tick,
                tick + settings.ExpiryTicks);
            return;
        }

        _beliefs[(fact.Subject, fact.Predicate)] = new Belief(fact, Math.Clamp(signal, 0.0, 1.0), source, tick, tick + settings.ExpiryTicks);
    }

    /// <summary>
    /// Révision d'une croyance depuis une <b>observation</b> — contrat du
    /// chapitre XI : <c>belief = belief + (signal − belief) × UpdateStrength</c>,
    /// avec plafond de variation par snap (<c>MaxChangePerSnap</c>). C'est le
    /// chemin utilisé par le pipeline cognitif (perception, réception de message),
    /// là où <see cref="ApplyEvidence"/> applique les règles historiques
    /// (bonus de source identique, moyenne, pénalité de conflit) — les paramètres
    /// de révision ne doivent pas rester inertes (S-08).
    /// <para>
    /// Fait inconnu : création à la confiance du signal. Fait connu : révision
    /// par la formule, expiration réarmée (confirmation) et pénalité des faits
    /// contradictoires (même sujet + prédicat, valeur différente).
    /// </para>
    /// </summary>
    public void ApplyObservation(
        Fact fact,
        double signal,
        string source,
        BeliefSettings settings,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(settings);
        double clamped = Math.Clamp(double.IsFinite(signal) ? signal : 0.0, 0.0, 1.0);

        if (TryGetCurrent(fact.Subject, fact.Predicate, out Belief? current))
        {
            bool sameValue = string.Equals(current.Fact.Value, fact.Value, StringComparison.Ordinal);
            double prior = current.Confidence;
            if (!sameValue)
            {
                // Incohérence entre l'observation et la croyance courante (sujet
                // mobile, ou signal contradictoire) : pénalité de conflit sur la
                // croyance antérieure, qui sert ensuite de <i>prior</i> à la révision.
                prior = Math.Max(0.1, prior - settings.ConflictPenalty);
            }

            current.Supersede(fact, BeliefRevision.Revise(
                prior,
                clamped,
                settings.UpdateStrength,
                settings.MaxChangePerSnap), source, tick, tick + settings.ExpiryTicks);
            return;
        }

        _beliefs[(fact.Subject, fact.Predicate)] = new Belief(fact, clamped, source, tick, tick + settings.ExpiryTicks);
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

    /// <summary>Restauration complète de l'ensemble (persistance bit-à-bit).</summary>
    internal void RestoreState(IEnumerable<Belief> beliefs)
    {
        ArgumentNullException.ThrowIfNull(beliefs);
        _beliefs.Clear();
        foreach (Belief belief in beliefs)
        {
            _beliefs[(belief.Fact.Subject, belief.Fact.Predicate)] = belief;
        }
    }
}