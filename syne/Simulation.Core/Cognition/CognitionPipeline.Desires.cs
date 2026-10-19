// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : CognitionPipeline - partie Desires.

namespace Simulation.Core.Cognition;

using System.Globalization;
using Simulation.Core.Actions;
using Simulation.Core.Communication;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;
using Simulation.Core.Perception;
using Simulation.Core.Performance;
using Simulation.Core.Population;
using Simulation.Core.Social;

public sealed partial class CognitionPipeline
{

    /// <summary>
    /// Objectif du tick : conserve l'intention courante (et donc sa
    /// <b>destination persistante</b>) tant que le type est identique et que la
    /// cible reste cru ; sinon l'objectif est ré-ancré sur la cible dérivée de la
    /// connaissance — ou sans cible si l'agent ne sait rien (B-01/B-02).
    /// </summary>
    private Goal BuildGoal(
        DesireKind kind,
        MindState mind,
        CognitionKnowledge? knowledge,
        bool knowledgeFresh,
        ulong currentTick,
        World.Position self)
    {
        // Holdover sans connaissance rafraîchie : l'intention est conservée
        // <b>telle quelle</b>, cible comprise — on ne « perd » pas une destination
        // simplement parce qu'on n'a pas relu le monde ce tick.
        if (!knowledgeFresh && mind.Intention is { } held && held.Kind == kind)
        {
            return held;
        }

        GoalTarget? target = ResolveTarget(kind, knowledge, self);

        if (mind.Intention is { } current && current.Kind == kind)
        {
            if (current.Target is { } kept && target is { } derived &&
                string.Equals(kept.Label, derived.Label, StringComparison.Ordinal))
            {
                return current;
            }

            if (current.Target is null && target is null)
            {
                return current;
            }
        }

        return new Goal(kind, currentTick, target);
    }


    /// <summary>Cible concrète d'une action, dérivée de la connaissance active.</summary>
    private GoalTarget? ResolveTarget(DesireKind kind, CognitionKnowledge? knowledge, World.Position self) =>
        kind switch
        {
            DesireKind.Flee => knowledge?.FleeDestination(self, _options.Agents.Perception.Radius * 0.5, _world.Size),
            _ => knowledge?.TargetFor(kind),
        };

    /// <summary>
    /// Faits justificatifs d'un choix : provenance, sujet, confiance et âge de la
    /// croyance qui porte la cible (ou mention explicite d'une cible <b>mémorée</b>
    /// faute de croyance vive). C'est ce qui permet de remonter la décision jusqu'à
    /// ses entrées (D-06).
    /// </summary>
    internal static IReadOnlyList<DecisionEvidence> EvidenceFor(MindState mind, GoalTarget? target)
    {
        if (target is not { } aimed)
        {
            return [];
        }

        string subject = aimed.TargetType == "node" ? $"node-{aimed.Label}" : aimed.Label;
        var evidence = new List<DecisionEvidence>();
        foreach (Belief belief in mind.Beliefs.AllInPlace)
        {
            if (!string.Equals(belief.Fact.Subject, subject, StringComparison.Ordinal))
            {
                continue;
            }

            evidence.Add(new DecisionEvidence(
                belief.Source,
                $"{belief.Fact.Subject}.{belief.Fact.Predicate}",
                belief.Confidence,
                belief.UpdatedTick));
        }

        if (evidence.Count == 0)
        {
            evidence.Add(new DecisionEvidence("memory", subject, aimed.Confidence, aimed.ObservedTick));
        }

        return evidence;
    }


    /// <summary>
    /// Score de saillance (D2) : généralisation O(k) des conditions d'interruption
    /// — lit les deltas déjà produits par les étapes 1-3 sans recalculer la boucle
    /// cognitive. Une condition d'interruption remplie déclenche la reconsidération
    /// indépendamment du seuil (RETOURNER SEUIL_MAX, invalidation incluse).
    /// </summary>
    private double ComputeSalience(MindState mind, ActionSettings actions)
    {
        double score = 0.0;
        IReadOnlyList<DesireKind> triggered = TriggeredNeeds(mind, _options.Agents.Needs);
        foreach (DesireKind kind in triggered)
        {
            if (!mind.TriggeredAtLastDeliberation.Contains(kind))
            {
                score += actions.Salience.NeedThresholdWeight;
            }
        }

        // Condition d'interruption remplie (critique) : reconsidération forcée.
        if (mind.Needs.IsCriticalFor(actions.Interruption))
        {
            return double.MaxValue;
        }

        return score;
    }


    private IReadOnlyList<DesireKind> TriggeredNeeds(MindState mind, NeedsSettings needs)
    {
        var triggered = new List<DesireKind>();
        foreach (DesireKind kind in Enum.GetValues<DesireKind>())
        {
            if (kind is DesireKind.Idle or DesireKind.Take or DesireKind.Give or DesireKind.Trade
                or DesireKind.Attack or DesireKind.Defend)
            {
                continue;
            }

            if (mind.Needs.IsTriggered(kind, needs))
            {
                triggered.Add(kind);
            }
        }

        return triggered;
    }


    /// <summary>Fréquence de délibération configurable (décalée par entité pour lisser la charge).</summary>
    private bool ShouldDeliberate(Entity entity, ulong currentTick) =>
        (currentTick + entity.Id.Value) % (ulong)_options.Agents.Actions.Deliberation.IntervalTicks == 0;


    /// <summary>
    /// Objectifs encore actifs : besoin encore déclenché ou objectif récemment
    /// défini (poursuite), <b>et cible encore crue</b>. Une cible dont la croyance a
    /// expiré n'est plus un objectif : il est abandonné et la délibération ré-ancre
    /// l'agent (B-01 : « abandon / révision » d'une cible devenue invalide). Les
    /// actions terminales Eat/Drink ne persistent pas et exigent la source à portée.
    /// </summary>
    private IReadOnlyList<Goal> ActiveGoals(MindState mind, Entity self, ulong currentTick, CognitionKnowledge? knowledge)
    {
        if (mind.Intention is not { } intention)
        {
            return [];
        }

        // Objectif <b>accompli</b> (B-01/B-02) : une poursuite dont la cible connue
        // est atteinte cesse d'être un objectif — sans quoi elle serait rejouée à
        // chaque délibération (le déplacement vers un point qu'on occupe), et
        // l'hystérésis anti-oscillation comparerait l'action terminale au
        // <i>même</i> objectif sous sa forme « poursuite » (à utilité égale, la
        // poursuite l'emportait : l'agent campait sur la source sans jamais
        // consommer). La satisfaction d'arrivée est appliquée par l'exécuteur ; la
        // délibération suivante repart de la connaissance et choisit l'action
        // terminale (Eat/Drink) ou un nouvel objectif.
        if (IsPursuit(intention.Kind) && TargetReached(self, mind))
        {
            return [];
        }

        bool stillNeeded = mind.Needs.IsTriggered(intention.Kind, _options.Agents.Needs);
        bool recent = intention.Age(currentTick) < 100;
        bool viable = intention.Kind switch
        {
            DesireKind.Eat => knowledge is { } knownFood ? knownFood.AtFoodSource : _catalog.IsViable(DesireKind.Eat, _stocks),
            DesireKind.Drink => knowledge is { } knownWater ? knownWater.AtWaterSource : _catalog.IsViable(DesireKind.Drink, _stocks),
            _ => true,
        };
        bool targetKnown = knowledge is null ||
                           intention.Target is not { } aimed ||
                           TargetStillKnown(aimed, knowledge.Value);

        return (stillNeeded || (recent && !IsTerminal(intention.Kind))) && viable && targetKnown ? [intention] : [];
    }


    /// <summary>
    /// La cible connue de l'intention est-elle <b>atteinte</b> (à portée
    /// d'interaction) ? C'est la condition d'accomplissement d'une poursuite : le
    /// but cesse alors d'être poursuivi, l'action terminale peut être choisie.
    /// </summary>
    private bool TargetReached(Entity self, MindState mind) =>
        mind.Intention is { Target: { } target } &&
        self.Position.DistanceTo(target.Position) <= _options.Resources.InteractionRadius;


    /// <summary>Une poursuite (déplacement vers une cible connue) s'oppose aux actions terminales, qui ne persistent pas.</summary>
    private static bool IsPursuit(DesireKind kind) => kind switch
    {
        DesireKind.SeekFood or DesireKind.SeekWater or DesireKind.Explore
            or DesireKind.Socialize or DesireKind.Flee => true,
        _ => false,
    };


    /// <summary>La cible crue de l'intention est-elle encore présente dans la connaissance active ?</summary>
    private static bool TargetStillKnown(GoalTarget target, CognitionKnowledge knowledge) =>
        string.Equals(knowledge.FoodSource?.Label, target.Label, StringComparison.Ordinal) ||
        string.Equals(knowledge.WaterSource?.Label, target.Label, StringComparison.Ordinal) ||
        string.Equals(knowledge.Landmark?.Label, target.Label, StringComparison.Ordinal) ||
        string.Equals(knowledge.Peer?.Label, target.Label, StringComparison.Ordinal);


    private static bool IsTerminal(DesireKind kind) => kind is DesireKind.Eat or DesireKind.Drink;


    /// <summary>
    /// Repli d'une action terminale poursuivie (holdover) : la source n'est plus à
    /// portée → poursuite ciblée si l'agent connaît un gisement, prospection sinon.
    /// </summary>
    private DesireKind HoldoverFallback(DesireKind kind, CognitionKnowledge? knowledge)
    {
        if (knowledge is { } known)
        {
            if (kind == DesireKind.Eat && !known.AtFoodSource)
            {
                return known.KnowsFood ? DesireKind.SeekFood : DesireKind.Explore;
            }

            if (kind == DesireKind.Drink && !known.AtWaterSource)
            {
                return known.KnowsWater ? DesireKind.SeekWater : DesireKind.Explore;
            }

            return kind;
        }


        if (kind == DesireKind.Eat && !_catalog.IsViable(DesireKind.Eat, _stocks))
        {
            return DesireKind.SeekFood;
        }

        if (kind == DesireKind.Drink && !_catalog.IsViable(DesireKind.Drink, _stocks))
        {
            return DesireKind.SeekWater;
        }

        return kind;
    }

    /// <summary>
    /// Évalue toutes les actions candidates et sélectionne (SYNE-030/031/033) :
    /// utilité maximale ; à conflit (candidats à moins de <c>conflictTieMargin</c>
    /// du maximum), résolution probabiliste force × confiance ;
    /// puis hystérésis anti-oscillation (<c>actionSwitchMargin</c>).
    /// Chaque candidat reçoit la confiance épistémique de sa cible
    /// (<see cref="CognitionKnowledge.EpistemicFor"/>) : deux agents à état
    /// physiologique identique mais à connaissances différentes évaluent donc
    /// différemment — c'est la porte du gate de la Phase 2.
    /// </summary>
    private (IReadOnlyList<UtilityScore> Scores, UtilityScore Best) Deliberate(
        MindState mind,
        IReadOnlyList<Goal> candidates,
        ulong currentTick,
        ulong entityId,
        CognitionKnowledge? knowledge,
        DesireKind? currentIntention)
    {
        AgentFactors factors;
        if (mind.Factors is { } resolved)
        {
            factors = resolved;
        }
        else
        {
            factors = new AgentFactors(TraitSet.NeutralAll);
        }

        ActionSettings actions = _options.Agents.Actions;
        double? Epistemic(DesireKind kind) => knowledge?.EpistemicFor(kind);

        var scores = new List<UtilityScore>(candidates.Count + 1)
        {
            UtilityEvaluator.Evaluate(
                DesireKind.Idle,
                mind.Needs,
                factors,
                mind.SuccessRate(DesireKind.Idle),
                goalAge: 0,
                actions),
        };

        foreach (Goal goal in candidates)
        {
            if (goal.Kind == DesireKind.Idle)
            {
                continue;
            }

            scores.Add(UtilityEvaluator.Evaluate(
                goal.Kind,
                mind.Needs,
                factors,
                mind.SuccessRate(goal.Kind),
                goal.Age(currentTick),
                actions,
                currentIntention,
                knowledgeConfidence: Epistemic(goal.Kind)));
        }

        // Engagements (D5) : chaque engagement actif ajoute un candidat d'aide
        // (Socialize) dont le bénéfice dérive de la confiance envers le demandeur
        // (40 × confiance — jusqu'à 40, comparable à Socialize saturé) au lieu d'un
        // besoin physiologique. La formule d'utilité est inchangée (benefitOverride).
        if (_options.Agents.Actions.Commitments.Enabled)
        {
            foreach (Commitment commitment in mind.Commitments)
            {
                if (!commitment.IsActive(currentTick))
                {
                    continue;
                }

                double trust = mind.Trust.TrustWith(commitment.ToEntityId);
                scores.Add(UtilityEvaluator.Evaluate(
                    DesireKind.Socialize,
                    mind.Needs,
                    factors,
                    mind.SuccessRate(DesireKind.Socialize),
                    goalAge: 0,
                    actions,
                    mind.Intention?.Kind,
                    benefitOverride: 40.0 * trust));
            }
        }

        double maximum = scores.Max(score => score.Utility);
        var contested = scores.Where(score => maximum - score.Utility <= actions.Deliberation.ConflictTieMargin).ToList();
        UtilityScore selected = contested.Count > 1
            ? PriorityConflictResolver.Resolve(contested, mind.Needs, entityId, currentTick)
            : contested[0];

        if (currentIntention is { } current)
        {
            UtilityScore adjusted = UtilityEvaluator.ApplyActionSwitchMargin(
                selected,
                current,
                mind.Needs,
                factors,
                mind.SuccessRate(current),
                mind.Intention?.Age(currentTick) ?? 0UL,
                actions,
                Epistemic(current));

            if (adjusted.Kind != selected.Kind)
            {
                scores.Add(adjusted);
                selected = adjusted;
            }
        }

        return (scores, selected);
    }

}
