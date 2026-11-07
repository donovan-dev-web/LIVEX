// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : CognitionPipeline - partie couplée (P9) : délibération pilotée par les
//        observations externes, sans perception ni exécution locale.

namespace Simulation.Core.Cognition;

using Simulation.Core.Entities;
using Simulation.Core.Performance;

public sealed partial class CognitionPipeline
{
    /// <summary>
    /// Exécute la <b>délibération seule</b> d'un tick couplé : besoins, objectifs,
    /// utilité, intention et étape de plan pour chaque entité, dans l'ordre des
    /// identifiants. Renvoie les décisions du tick, à traduire en commandes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Différence avec <see cref="Step"/> : rien n'est perçu localement (les
    /// observations viennent de PRISM, via <see cref="ApplyExternalObservations"/>)
    /// et <b>rien n'est exécuté</b> — l'action appartient au monde externe, SYNE ne
    /// fait que la décider. Les conséquences locales (déplacement, prélèvement,
    /// construction) ne sont donc jamais appliquées deux fois.
    /// </para>
    /// <para>
    /// Ce qui relève de la cognition est conservé : avance des besoins, décroissance
    /// et oubli des croyances, de la mémoire spatiale et de la confiance. Ce qui
    /// relève de la population et du monde (communication de masse, groupes,
    /// naissances, mortalité, régénération des réserves) reste hors de ce chemin :
    /// PRISM en est l'autorité, SYNE ne peut pas en inventer une seconde version.
    /// </para>
    /// <para>
    /// La décision renvoyée porte l'action <b>effectivement retenue</b> (étape de
    /// plan courante), pas seulement l'intention délibérée : c'est elle que la
    /// boucle couplée traduit en commande.
    /// </para>
    /// </remarks>
    public IReadOnlyList<DecisionRecord> StepCoupled(ulong currentTick)
    {
        List<Entity> ordered = _world.Entities
            .OrderBy(entity => entity.Id.Value)
            .ToList();

        var decisions = new List<DecisionRecord>(ordered.Count);
        foreach (Entity entity in ordered)
        {
            MindState mind = GetOrCreate(entity.Id.Value, entity);
            mind.Factors ??= new AgentFactors(entity.Traits);
            mind.DeliberatedThisTick = false;
            mind.InterruptedThisTick = false;
            mind.DecisionStateThisTick = DecisionState.None;

            mind.Needs.Advance(_options.Agents.Needs);

            DesireKind chosenKind = Deliberate(mind, entity, currentTick);

            // Une absence de décision n'est pas une commande : `Missing` et `None`
            // restent dehors (même règle que la trace d'observabilité).
            if (mind.LastDecisionRecord is { } record && !record.Missing && record.State != DecisionState.None)
            {
                decisions.Add(record with { Chosen = chosenKind });
            }
        }

        using (TickPhaseScope eventsScope = _budget?.Begin(TickPhase.EventsGroupsPopulation) ?? TickPhaseScope.Noop)
        {
            foreach (MindState mind in _minds.Values)
            {
                mind.Beliefs.Tick(currentTick, _options.Agents.Beliefs);
                mind.Spatial.Tick(currentTick, _options.Agents.Beliefs);
                mind.Trust.Tick();
            }
        }

        return decisions;
    }
}
