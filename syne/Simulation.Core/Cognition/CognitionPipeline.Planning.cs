// SPDX-FileCopyrightText: 2026 Donovan Chartrain
// SPDX-License-Identifier: GPL-3.0-or-later

// Projet : LIVEX (SYNE)
// Auteur : CHARTRAIN Donovan
// Rôle : CognitionPipeline - partie Planning (SYNE-223) : exécutif de plan, cache et replanification.

using Simulation.Core.Actions;
using Simulation.Core.Configuration;
using Simulation.Core.Entities;

namespace Simulation.Core.Cognition;

public sealed partial class CognitionPipeline
{
    private readonly PlannerRuntime? _plannerRuntime;

    /// <summary>
    /// Cache de plans (null quand le planificateur est désactivé) — exposé pour
    /// l'observabilité et les tests.
    /// </summary>
    public PlanCache? PlanCache => _plannerRuntime?.Cache;

    /// <summary>Nombre de replanifications provoquées par un échec d'étape.</summary>
    public long PlanReplans => _plannerRuntime?.Replans ?? 0;

    /// <summary>
    /// Avance le plan de l'esprit et rend l'action <b>réellement</b> exécutée ce
    /// tick. Sans planificateur actif, l'intention délibérée est rendue telle
    /// quelle : la trajectoire de référence reste inchangée.
    /// </summary>
    /// <remarks>
    /// Une poursuite est franchie dès que la cible du plan est à portée
    /// d'interaction (l'agent « est arrivé »), ce qui laisse la main à l'étape
    /// suivante — la consommation sur place. Aucun tirage PRNG, aucune horloge
    /// implicite : le plan et son avancement sont des fonctions de l'état.
    /// </remarks>
    private DesireKind AdvancePlan(
        MindState mind,
        DesireKind intention,
        GoalTarget? target,
        Entity self,
        ulong currentTick)
    {
        if (_plannerRuntime is not { } runtime)
        {
            return intention;
        }

        string key = Planner.Key(intention, target);
        PlannerState? state = mind.Plan;
        if (state is null || !string.Equals(state.Plan.Key, key, StringComparison.Ordinal))
        {
            Plan plan = runtime.Cache.TryGet(key, out Plan cached)
                ? cached
                : runtime.Cache.Put(Planner.Build(intention, target, runtime.Settings.MaxPlanSteps));
            state = new PlannerState(plan, target);
            mind.Plan = state;
        }

        while (state.Step + 1 < state.Plan.Count && StepSatisfied(state, self))
        {
            state.Step++;
            runtime.StepsCompleted++;
        }

        return state.Current.Kind;
    }

    /// <summary>
    /// Une poursuite est satisfaite quand l'agent est à portée de la cible du plan.
    /// Les autres étapes ne se franchissent qu'en s'exécutant (voir
    /// <see cref="ObservePlanResult"/>).
    /// </summary>
    private bool StepSatisfied(PlannerState state, Entity self) =>
        Planner.IsSatisfied(state.Current, state.Target, self.Position, _options.Resources.InteractionRadius);

    /// <summary>
    /// Consomme l'issue de l'action : une étape <c>Blocked</c> invalide le plan et
    /// le retire (replanification à la délibération suivante), une dernière étape
    /// exécutée clôt le plan. Une action interrompue (kind différent de l'étape)
    /// ne touche pas au plan : l'interruption reprend la main, pas la suite.
    /// </summary>
    private void ObservePlanResult(MindState mind, DesireKind executed, ActionResult result)
    {
        if (_plannerRuntime is not { } runtime || mind.Plan is not { } state)
        {
            return;
        }

        if (executed != state.Current.Kind)
        {
            return;
        }

        if (result.Outcome == ActionOutcome.Blocked)
        {
            runtime.Failures++;
            if (runtime.Settings.ReplanOnFailure)
            {
                runtime.Cache.Invalidate(state.Plan.Key);
                mind.Plan = null;
                runtime.Replans++;
            }

            return;
        }

        if (state.Step + 1 >= state.Plan.Count)
        {
            runtime.StepsCompleted++;
            mind.Plan = null;
        }
    }

    /// <summary>Compteurs et cache du planificateur (instancié seulement s'il est activé).</summary>
    private sealed class PlannerRuntime
    {
        public PlannerRuntime(PlannerSettings settings)
        {
            Settings = settings;
            Cache = new PlanCache(settings.CacheCapacity);
        }

        public PlannerSettings Settings { get; }

        public PlanCache Cache { get; }

        public long Replans { get; set; }

        public long Failures { get; set; }

        public long StepsCompleted { get; set; }
    }
}
