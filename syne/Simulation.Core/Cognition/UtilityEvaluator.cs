using Simulation.Core.Configuration;
using Simulation.Core.Entities;

namespace Simulation.Core.Cognition;

/// <summary>
/// Facteurs de personnalité issus des traits (plage 0-2, neutre 1.0) — entrée
/// de la formule d'utilité (COGNITIVE_ARCHITECTURE.md §6).
/// </summary>
public sealed class AgentFactors
{
    public AgentFactors(TraitSet traits)
    {
        ArgumentNullException.ThrowIfNull(traits);
        Bravery = traits["bravery"];
        Curiosity = traits["curiosity"];
        Sociability = traits["sociability"];
        Greed = traits["greed"];
        Strength = traits["strength"];
        Speed = traits["speed"];
    }

    public double Bravery { get; }

    public double Curiosity { get; }

    public double Sociability { get; }

    public double Greed { get; }

    public double Strength { get; }

    public double Speed { get; }
}

/// <summary>Score d'utilité d'une action candidate (décision n°13, SYNE-010).</summary>
public readonly record struct UtilityScore(
    DesireKind Kind,
    double Benefit,
    double Cost,
    double Risk,
    double Confidence,
    double PersonalityModifier,
    double Urgency,
    double Utility);

/// <summary>
/// Évaluateur d'utilité (COGNITIVE_ARCHITECTURE.md §6, décision n°13) :
/// <c>U = (benefit − cost − risk) × confidence × personalityModifier + urgency</c>.
/// Bonus d'alignement ×1.2 si l'action rejoint l'objectif courant ; sélection par
/// utilité maximale, départage déterministe par ordre du catalogue (SYNE-030).
/// </summary>
public static class UtilityEvaluator
{
    public static UtilityScore Evaluate(
        DesireKind kind,
        BodyNeeds needs,
        AgentFactors factors,
        double successRate,
        ulong goalAge,
        ActionSettings actions,
        DesireKind? currentIntention = null)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(factors);

        if (successRate is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(successRate), "Le taux de succès doit être dans [0, 1].");
        }

        double benefit = BenefitOf(kind, needs, actions);
        if (currentIntention is { } current && current == kind)
        {
            benefit *= actions.Deliberation.AlignBonus;
        }

        double cost = CostOf(kind, actions);
        double risk = RiskOf(kind);
        double confidence = 0.5 * (0.5 + (successRate * 0.5));
        double personality = PersonalityModifierOf(kind, factors);
        double urgency = UrgencyOf(kind, needs, goalAge, actions);

        double utility = ((benefit - cost - risk) * confidence * personality) + urgency;
        return new UtilityScore(kind, benefit, cost, risk, confidence, personality, urgency, utility);
    }

    /// <summary>Bénéfice : satisfaction potentielle d'un besoin (COGNITIVE_ARCHITECTURE.md §6).</summary>
    public static double BenefitOf(DesireKind kind, BodyNeeds needs, ActionSettings actions)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(actions);

        return kind switch
        {
            DesireKind.SeekFood or DesireKind.Eat => Math.Min(needs.Hunger, 30.0),
            DesireKind.SeekWater or DesireKind.Drink => Math.Min(needs.Thirst, 30.0),
            DesireKind.Rest => Math.Min(needs.Fatigue, 40.0),
            DesireKind.Flee => (1.0 - needs.Safety) * 60.0,
            DesireKind.Socialize => needs.Social * 60.0,
            DesireKind.Explore => Math.Min(needs.Curiosity * 100.0, 30.0),
            _ => 0.0,
        };
    }

    /// <summary>Coût de l'action (énergie, temps, risques) — valeurs V0.1 configurables.</summary>
    public static double CostOf(DesireKind kind, ActionSettings actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return kind switch
        {
            DesireKind.SeekFood or DesireKind.SeekWater or DesireKind.Flee or DesireKind.Socialize or DesireKind.Explore
                => actions.MoveEnergyCost,
            DesireKind.Eat => actions.Catalog.Entries["eat"].EnergyCost ?? actions.MoveEnergyCost,
            DesireKind.Drink => actions.Catalog.Entries["drink"].EnergyCost ?? actions.MoveEnergyCost,
            _ => 0.0,
        };
    }

    /// <summary>Risque de l'action (aucun danger modélisé en V0.1 — valeurs statiques par type).</summary>
    public static double RiskOf(DesireKind kind) => kind switch
    {
        DesireKind.Flee => 0.30,
        DesireKind.Explore => 0.25,
        DesireKind.SeekFood or DesireKind.SeekWater => 0.15,
        DesireKind.Socialize => 0.10,
        DesireKind.Eat or DesireKind.Drink => 0.10,
        DesireKind.Rest => 0.05,
        _ => 0.0,
    };

    /// <summary>
    /// Modulateur de personnalité (min 0.1) : Gather par Greed, Explore par
    /// Curiosity, Social par Sociability, actions risqueuses par Bravery.
    /// </summary>
    public static double PersonalityModifierOf(DesireKind kind, AgentFactors factors)
    {
        double modifier = kind switch
        {
            DesireKind.SeekFood or DesireKind.SeekWater or DesireKind.Eat or DesireKind.Drink => 0.5 + factors.Greed,
            DesireKind.Explore => 0.5 + factors.Curiosity,
            DesireKind.Socialize => 0.5 + factors.Sociability,
            DesireKind.Flee => 0.5 + (2.0 - factors.Bravery),
            _ => 1.0,
        };

        return Math.Max(0.1, modifier);
    }

    /// <summary>
    /// Urgence : sigmoïde <c>1 / (1 + exp(−0.1 × (need − 50))) × 20</c>, +5 si
    /// l'objectif a plus de 100 ticks, +10 si l'état est critique (défaut
    /// faim &gt; 85 ou énergie &lt; 10, agents.interruption.* — COGNITIVE_ARCHITECTURE.md §6).
    /// </summary>
    public static double UrgencyOf(DesireKind kind, BodyNeeds needs, ulong goalAge, ActionSettings actions)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(actions);

        double drive = needs.Drive(kind);
        double sigmoid = 1.0 / (1.0 + Math.Exp(-0.1 * (drive - 50.0)));
        double urgency = sigmoid * 20.0;
        if (goalAge > 100)
        {
            urgency += 5.0;
        }

        if (needs.IsCriticalFor(actions.Interruption))
        {
            urgency += 10.0;
        }

        return urgency;
    }

    /// <summary>Meilleur candidat : utilité maximale ; à égalité, premier dans l'ordre du catalogue.</summary>
    public static UtilityScore Best(IEnumerable<UtilityScore> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        UtilityScore? best = null;
        foreach (UtilityScore score in scores)
        {
            if (best is null ||
                score.Utility > best.Value.Utility + 1e-12 ||
                (Math.Abs(score.Utility - best.Value.Utility) <= 1e-12 && score.Kind < best.Value.Kind))
            {
                best = score;
            }
        }

        return best is null
            ? throw new InvalidOperationException("Aucun score d'utilité fourni à la sélection.")
            : best.Value;
    }

    /// <summary>
    /// Hystérésis anti-oscillation (COGNITIVE_ARCHITECTURE.md §6) : ne passer à
    /// une nouvelle action que si elle dépasse l'action courante de
    /// <c>actionSwitchMargin</c> (défaut 0.05). Sinon l'action courante est
    /// conservée (son score est re-évalué à l'état courant).
    /// </summary>
    public static UtilityScore ApplyActionSwitchMargin(
        UtilityScore candidate,
        DesireKind? currentKind,
        BodyNeeds needs,
        AgentFactors factors,
        double successRate,
        ulong goalAge,
        ActionSettings actions)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(factors);
        ArgumentNullException.ThrowIfNull(actions);

        if (currentKind is not { } current || current == candidate.Kind)
        {
            return candidate;
        }

        UtilityScore currentScore = Evaluate(current, needs, factors, successRate, goalAge, actions, current);
        if (candidate.Utility < currentScore.Utility + actions.Deliberation.ActionSwitchMargin)
        {
            return currentScore;
        }

        return candidate;
    }
}