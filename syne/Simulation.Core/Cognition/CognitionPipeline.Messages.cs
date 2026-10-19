// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : CognitionPipeline - partie Messages.

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
    /// Réception d'un message compris → <b>croyance</b> (D-13/S-21). Le contenu
    /// (position d'entité ou état de gisement) devient un fait cru, avec une
    /// provenance explicite (<c>message-&lt;émetteur&gt;</c>) et une confiance
    /// dégradée par la fiabilité de transmission et la confiance relationnelle.
    /// Un message non compris, de soi-même, ou au contenu non interprétable ne
    /// produit <b>aucune</b> croyance.
    /// </summary>
    private void ApplyReceivedMessages(ulong currentTick, IReadOnlyList<Entity> ordered)
    {
        // Ordre d'itération par identifiant croissant, réutilisé de la boucle
        // d'entités (déjà trié) : retrier le dictionnaire des esprits à chaque tick
        // allouait et comparait N éléments pour un ordre identique.
        foreach (Entity entity in ordered)
        {
            ulong entityId = entity.Id.Value;
            MindState mind = _minds[entityId];
            foreach ((Message message, bool understood) in mind.Communication.ReceivedThisTick())
            {
                if (!understood || message.SenderId == entityId)
                {
                    continue;
                }

                if (!KnowledgeBuilder.TryParseSharedObservation(message.Payload, out Fact fact, out double payloadConfidence))
                {
                    continue;
                }

                double reliability = Math.Clamp(
                    message.Confidence * mind.Trust.TrustWith(message.SenderId) * payloadConfidence,
                    0.0,
                    1.0);
                if (reliability <= 0.0)
                {
                    continue;
                }

                mind.Beliefs.ApplyObservation(
                    fact,
                    reliability,
                    $"message-{message.SenderId}",
                    _options.Agents.Beliefs,
                    currentTick);
                mind.Memory.Store(
                    MemoryCategory.Interaction,
                    $"message-{message.SenderId}",
                    message.Payload,
                    reliability,
                    currentTick);
            }
        }
    }

    private void StepEntity(Entity entity, ulong currentTick)
    {
        MindState mind = GetOrCreate(entity.Id.Value, entity);
        mind.Factors ??= new AgentFactors(entity.Traits);
        mind.DeliberatedThisTick = false;
        mind.InterruptedThisTick = false;
        mind.DecisionStateThisTick = DecisionState.None;

        int observed = 0;
        bool announced = false;
        using (TickPhaseScope perceptionScope = _budget?.Begin(TickPhase.Perception) ?? TickPhaseScope.Noop)
        {
            if (_perception.ShouldPerceive(entity, currentTick))
            {
                IReadOnlyList<Observation> observations = _perception.Perceive(entity, currentTick);
                observed = observations.Count;
                AgentFactors shareFactors = mind.Factors ?? new AgentFactors(entity.Traits);

                // Source de provenance calculée <b>une fois par entité et par tick</b> :
                // elle ne dépend que de l'observateur, alors que le chemin de stockage
                // l'exigeait par observation — à densité (des dizaines de voisins
                // perçus), c'était une chaîne interpolée par observation pour une
                // valeur identique. Contenu inchangé (mêmes empreintes, mêmes
                // croyances : la source est comparée par valeur).
                string source = $"perception-{entity.Id.Value}";
                foreach (Observation observation in observations)
                {
                    StoreObservation(mind, entity, observation, currentTick, source);
                    if (!announced && WantsToShare(shareFactors, observation))
                    {
                        EnqueueSharePulse(entity, observation, mind, currentTick);
                        announced = true;
                    }
                }
            }
        }

        using (TickPhaseScope memoryScope = _budget?.Begin(TickPhase.MemoryBeliefs) ?? TickPhaseScope.Noop)
        {
            mind.AcknowledgePerceptions(observed, currentTick);
            mind.Needs.Advance(_options.Agents.Needs);
        }

        ActionSettings actions = _options.Agents.Actions;
        DesireKind chosenKind;
        mind.DefendingThisTick = false;
        mind.SkippedBySalience = false;

        // Connaissance active (Phase 2) : cibles crues issues des croyances puis,
        // en repli, de la mémoire rappelée. Modèle localisé uniquement : hors
        // sources localisées, la décision garde le modèle global historique.
        //
        // Construction <b>paresseuse</b> : elle n'a lieu qu'aux ticks qui en ont
        // besoin (délibération, état critique), pas à chaque tick de chaque agent —
        // la construction parcourt les croyances et peut interroger la mémoire.
        CognitionKnowledge? knowledge = null;
        bool knowledgeFresh = false;

        CognitionKnowledge? Knowledge()
        {
            if (!knowledgeFresh)
            {
                knowledge = _options.Resources.Localized
                    ? KnowledgeBuilder.Build(
                        mind,
                        entity.Position,
                        currentTick,
                        _options.Agents.Beliefs,
                        _options.Resources.InteractionRadius)
                    : null;
                knowledgeFresh = true;
            }

            return knowledge;
        }

        // Demandes d'aide (D5) : émission d'une Request quand la faim est critique
        // — la réponse des pairs crée l'engagement (voir ProcessCommitmentMessages).
        if (_options.Agents.Actions.Commitments.Enabled)
        {
            EnqueueHelpRequest(entity, mind, currentTick);
        }

        using (TickPhaseScope decisionScope = _budget?.Begin(TickPhase.NeedsGoals) ?? TickPhaseScope.Noop)
        {
            // Contrôle de saillance (D2, étape
            // 3bis) : entre deux délibérations planifiées, l'intention est poursuivie
            // SAUF saillance (besoin franchi / condition d'interruption) ou filet de
            // sécurité périodique. Désactivé par défaut (trajectoire de référence).
            bool deliberate = ShouldDeliberate(entity, currentTick);
            if (!deliberate && actions.Salience.Enabled)
            {
                double salience = ComputeSalience(mind, actions);
                mind.LastSalienceScore = salience;
                bool forced = actions.Salience.ForcedReconsiderationTicks > 0
                    && currentTick - mind.LastDeliberationTick >= (ulong)actions.Salience.ForcedReconsiderationTicks;
                if (salience < actions.Salience.ReconsiderThreshold && !forced)
                {
                    mind.SkippedBySalience = true;
                    deliberate = false;
                }
                else
                {
                    mind.SkippedBySalience = false;
                    deliberate = true;
                }
            }

            // Délibération à fréquence configurable : entre deux
            // délibérations, l'intention est conservée (holdover) telle quelle.
            if (deliberate)
            {
                CognitionKnowledge? current = Knowledge();
                IReadOnlyList<Goal> active = ActiveGoals(mind, entity, currentTick, current);
                IReadOnlyList<Goal> generated = DesireFactory.Generate(
                    mind.Needs,
                    currentTick,
                    active.Select(goal => goal.Kind),
                    _catalog,
                    _stocks,
                    current);
                var candidates = new List<Goal>(active.Count + generated.Count);
                candidates.AddRange(active);
                candidates.AddRange(generated);

                // Bibliothèque de plans (D3) : ajoute
                // des candidats de manipulation (Take/Trade) pour les objectifs de
                // besoin — désactivée par défaut (trajectoire de référence).
                IReadOnlyList<Goal> planCandidates = PlanLibrary.GenerateCandidates(
                    candidates, mind, entity, _catalog, _stocks,
                    actions.Inventory, actions.Plans, currentTick);
                candidates.AddRange(planCandidates);

                // Référence d'hystérésis : l'intention courante, <b>sauf</b> si son
                // objectif vient d'être accompli (cible atteinte) — comparer le
                // nouveau choix à un objectif terminé bloquerait l'action terminale.
                DesireKind? hysteresisReference = TargetReached(entity, mind) ? null : mind.Intention?.Kind;
                (IReadOnlyList<UtilityScore> scores, UtilityScore best) = Deliberate(
                    mind, candidates, currentTick, entity.Id.Value, current, hysteresisReference);
                GoalTarget? target = ResolveTarget(best.Kind, current, entity.Position);
                mind.RecordDecision(
                    scores,
                    best,
                    deliberated: true,
                    interrupted: false,
                    currentTick,
                    entity.Id.Value,
                    target,
                    EvidenceFor(mind, target));
                mind.DeliberatedThisTick = true;
                mind.RecordDeliberationState(currentTick, TriggeredNeeds(mind, _options.Agents.Needs));
                chosenKind = best.Kind;
            }
            else
            {
                chosenKind = mind.LastDecision is { } held ? held.Kind : DesireKind.Idle;

                // Holdover : une action terminale Eat/Drink dont la source n'est
                // plus à portée (ou dont la réserve globale s'est épuisée) retombe
                // sur la poursuite ciblée, puis sur la prospection. Le holdover ne
                // reconstruit pas la connaissance (coût) : la cible de l'intention
                // est conservée telle quelle et l'action échouera explicitement
                // (Blocked motivée) si elle n'est plus applicable — la prochaine
                // délibération (≤ interval) ré-ancre l'agent.
                chosenKind = HoldoverFallback(chosenKind, knowledgeFresh ? knowledge : null);

                if (mind.LastDecision is null)
                {
                    // Aucune décision antérieure : le tick est une ABSENCE de
                    // décision (jamais un zéro d'utilité, jamais un score ancien).
                    mind.RecordMissingDecision(currentTick, entity.Id.Value);
                }
                else
                {
                    mind.DecisionStateThisTick = DecisionState.Holdover;
                }
            }

            // Interruptions d'action (SYNE-043) :
            // déclencheur centralisé — un besoin critique dont l'utilité surpasse de >
            // utilityExcessMargin l'action en cours reprend la main, y compris entre
            // deux délibérations. Eat/Drink répondent à la faim critique si la source
            // est connue (gisement à portée, modèle localisé) ou disponible (réserve globale).
            bool criticalNeeds = _options.Agents.Actions.Interruption.Enabled
                && mind.Needs.IsCriticalFor(actions.Interruption);
            if (_options.Agents.Actions.Interruption.Enabled)
            {
                (IReadOnlyList<UtilityScore> scores, UtilityScore? interruption) = _interruption.Evaluate(
                    actions.Interruption,
                    actions,
                    mind.Needs,
                    mind.Factors ?? new AgentFactors(TraitSet.NeutralAll),
                    mind.SuccessRate,
                    chosenKind,
                    mind.Intention,
                    currentTick,
                    entity.Id.Value,
                    knowledge: criticalNeeds ? Knowledge() : null);

                if (interruption is { } chosen)
                {
                    GoalTarget? target = ResolveTarget(chosen.Kind, criticalNeeds ? knowledge : null, entity.Position);
                    mind.RecordDecision(
                        scores,
                        chosen,
                        deliberated: false,
                        interrupted: true,
                        currentTick,
                        entity.Id.Value,
                        target,
                        EvidenceFor(mind, target));
                    mind.DeliberatedThisTick = false;
                    mind.InterruptedThisTick = true;
                    chosenKind = chosen.Kind;
                }
            }
        }

        mind.Intention = BuildGoal(chosenKind, mind, knowledge, knowledgeFresh, currentTick, entity.Position);

        using (TickPhaseScope actionScope = _budget?.Begin(TickPhase.ActionsMovement) ?? TickPhaseScope.Noop)
        {
            ActionResult result = _executor.Execute(entity, mind, chosenKind, currentTick, MindOf);
            mind.RecordAction(result);
        }
    }


    /// <summary>
    /// Demandes d'aide (D5, version minimale) : une entité en faim critique, sans
    /// engagement en attente, émet une pulsation <c>Request</c> publique « help-food »
    /// au plus une fois par 20 ticks (cooldown déterministe par identifiant — la
    /// production réelle de Request remplace la consommation passive de la file).
    /// </summary>
    private void EnqueueHelpRequest(Entity entity, MindState mind, ulong currentTick)
    {
        if (mind.Needs.Hunger <= _options.Agents.Actions.Interruption.CriticalHunger
            || mind.Commitments.Any(commitment => commitment.Status == CommitmentStatus.Pending)
            || (currentTick + entity.Id.Value) % 20 != 0)
        {
            return;
        }

        Message request = CommunicationSystem.CreateMessage(
            entity.Id.Value,
            targetId: null,
            MessageType.Request,
            "help-food",
            currentTick,
            sequence: 2);
        mind.Communication.Enqueue(request, _options.Communication.MaxSendsPerTick);
    }


    /// <summary>
    /// Cycle des messages d'engagement (D5) : création d'un <see cref="Commitment"/>
    /// chez le demandeur qui reçoit une <c>Response</c> positive (borné : au plus un
    /// engagement actif par pair, au plus 3 engagements actifs — impact mémoire
    /// borné), puis réponse positive des entités très sociables
    /// (facteur ≥ 1.0) à la première <c>Request</c> comprise du tick.
    /// </summary>
    private void ProcessCommitmentMessages(ulong currentTick)
    {
        foreach ((ulong entityId, MindState mind) in _minds.OrderBy(pair => pair.Key))
        {
            foreach ((Message message, bool understood) in mind.Communication.ReceivedThisTick())
            {
                if (!understood || message.SenderId == entityId)
                {
                    continue;
                }

                if (message.Type == MessageType.Response
                    && !mind.Commitments.Any(commitment =>
                        commitment.ToEntityId == message.SenderId && commitment.Status == CommitmentStatus.Pending)
                    && mind.Commitments.Count < 3)
                {
                    mind.Commitments.Add(new Commitment(
                        message.SenderId,
                        "survival",
                        currentTick,
                        currentTick + _options.Agents.Actions.Commitments.ExpiryTicks));
                }
            }
        }

        foreach ((ulong entityId, MindState mind) in _minds.OrderBy(pair => pair.Key))
        {
            if ((mind.Factors?.Sociability ?? 0.0) < 1.0)
            {
                continue;
            }

            foreach ((Message message, bool understood) in mind.Communication.ReceivedThisTick())
            {
                if (!understood || message.SenderId == entityId || message.Type != MessageType.Request)
                {
                    continue;
                }

                Message response = CommunicationSystem.CreateMessage(
                    entityId,
                    message.SenderId,
                    MessageType.Response,
                    "yes",
                    currentTick,
                    sequence: 3);
                mind.Communication.Enqueue(response, _options.Communication.MaxSendsPerTick);
                break;
            }
        }
    }


    /// <summary>
    /// Résolution des engagements (D5) : honoré si le prometteur a exécuté
    /// Give/Trade/Socialize ce tick (l'aide promises s'est matérialisée) →
    /// <c>TrustLevel += commitmentBonus</c> ; expiré sans honororation →
    /// <c>TrustLevel -= commitmentPenalty</c> (traité comme rompu).
    /// Les engagements résolus sont purgés (suivi borné). Identifiants croissants.
    /// </summary>
    private void ResolveCommitments(ulong currentTick)
    {
        foreach ((ulong _, MindState mind) in _minds.OrderBy(pair => pair.Key))
        {
            for (int i = mind.Commitments.Count - 1; i >= 0; i--)
            {
                Commitment commitment = mind.Commitments[i];
                if (commitment.Status != CommitmentStatus.Pending)
                {
                    mind.Commitments.RemoveAt(i);
                    continue;
                }

                if (currentTick >= commitment.ExpiryTick)
                {
                    commitment.Expire(currentTick);
                    mind.Trust.Penalize(commitment.ToEntityId, _options.Agents.Trust.CommitmentPenalty);
                    mind.Commitments.RemoveAt(i);
                    continue;
                }

                if (_minds.TryGetValue(commitment.ToEntityId, out MindState? responder)
                    && responder.LastActionResult is { } action
                    && action.Outcome == ActionOutcome.Executed
                    && action.Kind is DesireKind.Give or DesireKind.Trade or DesireKind.Socialize)
                {
                    commitment.Fulfill(currentTick);
                    mind.Trust.Reward(commitment.ToEntityId, _options.Agents.Trust.CommitmentBonus);
                    mind.Commitments.RemoveAt(i);
                }
            }
        }
    }

}
