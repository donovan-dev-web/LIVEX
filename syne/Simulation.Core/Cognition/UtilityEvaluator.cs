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
        // Le trait s'appelle « aggression » dans TraitSet (DATA_MODEL.md §3.2) —
        // la clé de configuration « aggressiveness » est mappée par EntityFactory.
        Aggressiveness = traits["aggression"];
    }

    public double Bravery { get; }

    public double Curiosity { get; }

    public double Sociability { get; }

    public double Greed { get; }

    public double Strength { get; }

    public double Speed { get; }

    /// <summary>Agressivité (D7 : dégât d'Attack = 5 × agressivité, §3.15.4).</summary>
    public double Aggressiveness { get; }
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
        DesireKind? currentIntention = null,
        GroupObjective? collectiveObjective = null,
        double? benefitOverride = null)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(factors);

        if (successRate is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(successRate), "Le taux de succès doit être dans [0, 1].");
        }

        // D5 (engagements) : l'objectif dérivé d'un engagement n'est pas porté par
        // un besoin physiologique — son bénéfice est fourni par l'appelant, calculé
        // depuis la confiance envers le demandeur (ADR : priorité « non pas depuis
        // un besoin physiologique mais depuis le niveau de confiance »). La formule
        // U = (benefit − cost − risk) × confidence × personality + urgency reste
        // strictement identique.
        double benefit = benefitOverride ?? BenefitOf(kind, needs, actions);
        if (currentIntention is { } current && current == kind)
        {
            benefit *= actions.Deliberation.AlignBonus;
        }

        // Alignement collectif (SYNE-076) : l'objectif adopté par le groupe
        // rehausse le bénéfice de l'action conforme, pondéré par le consensus
        // atteint et la confiance du membre envers le leader (aucun alignement
        // sans consensus ni confiance — ×1 ; bonification maximale quand les
        // deux valent 1, ×CollectiveAlignBonus).
        if (collectiveObjective is { } collective && collective.Kind == kind)
        {
            double effective = Math.Clamp(collective.Consensus, 0.0, 1.0) *
                               Math.Clamp(collective.LeaderTrust, 0.0, 1.0);
            benefit *= 1.0 + ((actions.Deliberation.CollectiveAlignBonus - 1.0) * effective);
        }

        double cost = CostOf(kind, actions);
        double risk = RiskOf(kind);
        double confidence = 0.5 * (0.5 + (successRate * 0.5));
        double personality = PersonalityModifierOf(kind, factors);
        double urgency = UrgencyOf(kind, needs, goalAge, actions);

        double utility = ((benefit - cost - risk) * confidence * personality) + urgency;
        return new UtilityScore(kind, benefit, cost, risk, confidence, personality, urgency, utility);
    }

    /// <summary>
    /// Bénéfice : satisfaction potentielle d'un besoin (COGNITIVE_ARCHITECTURE.md §6).
    /// <para>
    /// Calibration D1 (29/09/2026) : le bénéfice Eat/Drink était plafonné à 30
    /// (``min(need, 30)``) — à faim saturée, l'urgence (sigmoïde ×20, +10 critique)
    /// ne compensait jamais un bénéfice capé et Eat perdait systématiquement contre
    /// Socialize (60) / Explore : utilité moyenne 13,9 vs 83,7 sur la campagne.
    /// Le plafond devient ``min(need, 100) × 0.6`` (plafond 60, monotone avec le
    /// besoin) : SeekFood ≡ Eat en bénéfice n'incite plus à boucler sur le déplacement,
    /// et l'urgence relative peut basculer l'arbitrage vers l'action qui résout le
    /// besoin. Valeurs constantes posées ici (formule paramétrable par seuils en V0.2).
    /// </para>
    /// </summary>
    public static double BenefitOf(DesireKind kind, BodyNeeds needs, ActionSettings actions)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(actions);

        return kind switch
        {
            DesireKind.SeekFood or DesireKind.Eat => Math.Min(needs.Hunger, 100.0) * 0.6,
            DesireKind.SeekWater or DesireKind.Drink => Math.Min(needs.Thirst, 100.0) * 0.6,
            DesireKind.Rest => Math.Min(needs.Fatigue, 40.0),
            DesireKind.Flee => (1.0 - needs.Safety) * 60.0,
            DesireKind.Socialize => needs.Social * 60.0,
            DesireKind.Explore => Math.Min(needs.Curiosity * 100.0, 30.0),

            // Primitives D7 (engineVersion 0.14.0) : bénéfices dérivés des besoins
            // existants — aucune nouvelle formule d'utilité (ADR D3 : la fonction
            // est inchangée, elle reçoit plus de candidats).
            // Take = pulsion de constitution de stock, PORTÉE PAR L'AVARICE et non
            // par la faim (ADR Inventaire : « un agent avare continue Prendre
            // au-delà de son besoin immédiat tant que poidsActuel < capacitePoids »)
            // — constant, modulé par greed via PersonalityModifierOf : l'agent
            // stocke quand il n'a pas faim, consomme quand la faim l'emporte.
            DesireKind.Take => 15.0,
            // Give = acte social pur (même bénéfice que Socialize).
            DesireKind.Give => needs.Social * 60.0,
            // Trade = résolution différée du besoin (coûte une ressource → ×0.9).
            DesireKind.Trade => Math.Max(needs.Hunger, needs.Thirst) * 0.54,
            // Attack/Defend ne portent aucun besoin en V0.1 (jamais générés —
            // doctrine §9.6.3 point 13) : présents au catalogue (D3 temps 1).
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
            DesireKind.Take => actions.Catalog.Entries["take"].EnergyCost ?? actions.MoveEnergyCost,
            DesireKind.Give => actions.Catalog.Entries["give"].EnergyCost ?? actions.MoveEnergyCost,
            DesireKind.Trade => actions.Catalog.Entries["trade"].EnergyCost ?? actions.MoveEnergyCost,
            DesireKind.Attack => actions.Catalog.Entries["attack"].EnergyCost ?? actions.MoveEnergyCost,
            _ => 0.0,
        };
    }

    /// <summary>Risque de l'action (valeurs statiques par type — V0.1 : aucun danger modélisé hors primitives D7).</summary>
    public static double RiskOf(DesireKind kind) => kind switch
    {
        DesireKind.Attack => 0.60,
        DesireKind.Flee => 0.30,
        DesireKind.Explore => 0.25,
        DesireKind.SeekFood or DesireKind.SeekWater => 0.15,
        DesireKind.Give or DesireKind.Trade => 0.15,
        DesireKind.Socialize => 0.10,
        DesireKind.Eat or DesireKind.Drink => 0.10,
        DesireKind.Take => 0.10,
        DesireKind.Rest => 0.05,
        DesireKind.Defend => 0.0,
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
            DesireKind.Take => 0.5 + factors.Greed,
            DesireKind.Explore => 0.5 + factors.Curiosity,
            DesireKind.Socialize => 0.5 + factors.Sociability,
            DesireKind.Give => 0.5 + factors.Sociability,
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
        ActionSettings actions,
        GroupObjective? collectiveObjective = null)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(factors);
        ArgumentNullException.ThrowIfNull(actions);

        if (currentKind is not { } current || current == candidate.Kind)
        {
            return candidate;
        }

        UtilityScore currentScore = Evaluate(
            current,
            needs,
            factors,
            successRate,
            goalAge,
            actions,
            current,
            collectiveObjective);
        if (candidate.Utility < currentScore.Utility + actions.Deliberation.ActionSwitchMargin)
        {
            return currentScore;
        }

        return candidate;
    }
}