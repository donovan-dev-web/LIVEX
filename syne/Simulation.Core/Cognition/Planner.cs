// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : Planner - plan, cache borné déterministe et état de plan par esprit (SYNE-223).

using Simulation.Core.Actions;
using Simulation.Core.Configuration;
using Simulation.Core.World;

namespace Simulation.Core.Cognition;

/// <summary>
/// Une étape de plan (SYNE-223) : l'action à exécuter et l'étiquette de ce qu'elle
/// vise (cible connue, « food:node-3 » par exemple) — la trace dit <b>pourquoi</b>
/// telle étape apparaît dans la suite.
/// </summary>
public readonly record struct PlanStep(DesireKind Kind, string Label);

/// <summary>
/// Plan d'action (SYNE-223) : suite <b>ordonnée</b> d'étapes dérivée d'une
/// intention, identifiée par une <b>clé</b> stable (intention + cible) qui sert au
/// cache et à l'invalidation.
/// </summary>
public sealed class Plan
{
    public Plan(string key, IReadOnlyList<PlanStep> steps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
        {
            throw new ArgumentException("Un plan doit porter au moins une étape.", nameof(steps));
        }

        Key = key;
        Steps = steps;
    }

    /// <summary>Clé stable : <c>&lt;intention&gt;@&lt;cible|-></c>.</summary>
    public string Key { get; }

    public IReadOnlyList<PlanStep> Steps { get; }

    public int Count => Steps.Count;
}

/// <summary>
/// Cache de plans (SYNE-223) : borné, déterministe (aucun tirage, ordre
/// d'insertion strict) et <b>invalidable</b> — un plan dont une étape a échoué n'a
/// plus de raison d'être réutilisé. La capacité 0 désactive le cache sans changer
/// le comportement (les plans sont reconstruits à chaque fois).
/// </summary>
public sealed class PlanCache
{
    private readonly Dictionary<string, Plan> _entries = new(StringComparer.Ordinal);

    /// <summary>Ordre de réutilisation : la tête est la moins récemment utilisée.</summary>
    private readonly List<string> _order = [];

    public PlanCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public int Count => _entries.Count;

    /// <summary>Consultations servies par le cache.</summary>
    public long Hits { get; private set; }

    /// <summary>Consultations non servies (absent ou capacité nulle).</summary>
    public long Misses { get; private set; }

    /// <summary>Plans évincés faute de place.</summary>
    public long Evictions { get; private set; }

    /// <summary>Plans invalidés explicitement (échec d'étape).</summary>
    public long Invalidations { get; private set; }

    public bool TryGet(string key, out Plan plan)
    {
        if (Capacity > 0 && _entries.TryGetValue(key, out Plan? found))
        {
            Touch(key);
            Hits++;
            plan = found;
            return true;
        }

        Misses++;
        plan = null!;
        return false;
    }

    /// <summary>Enregistre un plan (le plus ancien cède la place au besoin).</summary>
    public Plan Put(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (Capacity == 0)
        {
            return plan;
        }

        if (_entries.ContainsKey(plan.Key))
        {
            _entries[plan.Key] = plan;
            Touch(plan.Key);
            return plan;
        }

        while (_entries.Count >= Capacity && _order.Count > 0)
        {
            string oldest = _order[0];
            _order.RemoveAt(0);
            _entries.Remove(oldest);
            Evictions++;
        }

        _entries[plan.Key] = plan;
        _order.Add(plan.Key);
        return plan;
    }

    /// <summary>Retire un plan ; vrai s'il était présent.</summary>
    public bool Invalidate(string key)
    {
        if (!_entries.Remove(key))
        {
            return false;
        }

        _order.Remove(key);
        Invalidations++;
        return true;
    }

    public void Clear()
    {
        _entries.Clear();
        _order.Clear();
    }

    private void Touch(string key)
    {
        int index = _order.IndexOf(key);
        if (index >= 0)
        {
            _order.RemoveAt(index);
        }

        _order.Add(key);
    }
}

/// <summary>
/// État de plan d'un esprit (SYNE-223) : le plan courant et l'étape atteinte.
/// <b>Transitoire</b> — il n'est pas persisté : après une reprise, le plan est
/// redérivé à la délibération suivante à partir des mêmes entrées (aucun tirage
/// PRNG n'intervient), donc l'état sauvegardé reste suffisant.
/// </summary>
public sealed class PlannerState
{
    public PlannerState(Plan plan, GoalTarget? target = null, int step = 0)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Plan = plan;
        Target = target;
        Step = step;
    }

    public Plan Plan { get; set; }

    /// <summary>Cible visée par le plan (null : intention sans cible connue).</summary>
    public GoalTarget? Target { get; }

    /// <summary>Indice de l'étape en cours.</summary>
    public int Step { get; set; }

    /// <summary>Étape courante (jamais hors bornes : le plan est non vide).</summary>
    public PlanStep Current => Plan.Steps[Math.Clamp(Step, 0, Plan.Count - 1)];
}

/// <summary>
/// Constructeur de plans (SYNE-223) : transforme une intention en suite d'étapes
/// avec le <b>vocabulaire d'actions existant</b> — aucune action nouvelle, aucun
/// tirage. La décomposition livrée est la chaîne de survie localisée :
/// aller vers la cible connue, puis consommer sur place.
/// </summary>
public static class Planner
{
    /// <summary>
    /// Intention de <b>poursuite</b> (déplacement vers une cible connue) : seule
    /// catégorie dont le franchissement est automatique — « l'agent est arrivé ».
    /// Source unique de la liste (le pipeline y délègue).
    /// </summary>
    public static bool IsPursuit(DesireKind kind) => kind is
        DesireKind.SeekFood or DesireKind.SeekWater or DesireKind.Explore
        or DesireKind.Socialize or DesireKind.Flee
        // Sécurité alimentaire (SYNE-222) : aller vers la source est un déplacement.
        or DesireKind.SecureFood;

    /// <summary>
    /// Une étape est-elle déjà franchie ? Oui pour une poursuite dont la cible est
    /// à portée d'interaction ; une étape non-poursuite ne se franchit qu'en
    /// s'exécutant.
    /// </summary>
    public static bool IsSatisfied(PlanStep step, GoalTarget? target, Position self, double interactionRadius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(interactionRadius);
        return IsPursuit(step.Kind)
            && target is { } aimed
            && self.DistanceTo(aimed.Position) <= interactionRadius;
    }

    /// <summary>Clé stable d'une intention : <c>&lt;intention&gt;@&lt;cible|-></c>.</summary>
    public static string Key(DesireKind kind, GoalTarget? target) =>
        string.Concat(kind.ToString(), "@", target is { } aimed ? aimed.Canonical : "-");

    /// <summary>
    /// Plan pour une intention. Une intention de recherche portant une cible donne
    /// deux étapes (poursuite puis action terminale) ; toute autre intention donne
    /// un plan d'une étape, ce qui laisse l'exécutif et le cache uniformes.
    /// </summary>
    public static Plan Build(DesireKind kind, GoalTarget? target, int maxSteps)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSteps, 1);

        string label = target is { } aimed ? aimed.Canonical : kind.ToString();
        DesireKind? terminal = kind switch
        {
            DesireKind.SeekFood => DesireKind.Eat,
            DesireKind.SeekWater => DesireKind.Drink,
            // Sécurité alimentaire (SYNE-222) : la chaîne instrumentale est
            // « aller à la source connue, puis prendre » — c'est l'étape qui
            // remplit la réserve, la consommation reste Eat.
            DesireKind.SecureFood => DesireKind.Take,
            _ => null,
        };

        var steps = new List<PlanStep>(2) { new(kind, label) };

        // L'étape terminale n'a de sens qu'avec une cible : sans destination
        // connue, la poursuite ne peut jamais être « franchie » et l'étape de
        // consommation resterait lettre morte dans le plan.
        if (target is not null && terminal is { } next && next != kind)
        {
            steps.Add(new PlanStep(next, label));
        }

        if (steps.Count > maxSteps)
        {
            steps.RemoveRange(maxSteps, steps.Count - maxSteps);
        }

        return new Plan(Key(kind, target), steps);
    }
}
