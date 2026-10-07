"""Moteur 4 — GoalConvergenceMetrics (convergence des objectifs).

Mesure l'alignement ou la divergence des objectifs (METRICS_SPEC.md §5).
Les 4 métriques sont déterministes sur la distribution des types d'objectifs
actifs des agents (``goals[].kind``, repli ``currentAction`` / ``Idle``).

Décision P1 : ``CooperationPotential`` → **``GoalCategoryConcordance``**. Le
calcul (Σ p²) est la probabilité que deux tirages indépendants partagent une
catégorie ; il ne teste aucune compatibilité de buts ni aucun acte de
coopération, et la doc affirmait l'inverse (« compatibilité par paire »).
"""

from __future__ import annotations

from collections import Counter

from ._common import (
    agents_of,
    alive_count,
    goal_kinds,
    keyed,
    safe_ratio,
    shannon,
)

ENGINE_NAME = "GoalConvergenceMetrics"

METRICS = (
    "GlobalGoalAlignment",
    "GoalDiversity",
    "GoalCategoryConcordance",
    "GoalTypeCounts",
)


def _goal_observed(snapshot: dict) -> bool:
    """Vrai si au moins une entité déclare un objectif à ce tick.

    Sans objectif, ``goal_kinds`` retombe sur ``currentAction``/``Idle`` : les
    métriques décriraient alors des **actions**. Non mesuré plutôt qu'un repli
    présenté comme une distribution de buts.
    """
    return any(agent.get("goals") for agent in agents_of(snapshot))


REQUIRES = {
    "GlobalGoalAlignment": _goal_observed,
    "GoalDiversity": _goal_observed,
    "GoalCategoryConcordance": _goal_observed,
}
"""Provenance : une distribution sans aucun objectif déclaré n'est pas mesurée."""


def compute(snapshot: dict) -> dict:
    """Calcule les 4 métriques de convergence des objectifs."""
    agents = agents_of(snapshot)
    count = alive_count(snapshot)
    goal_counter = Counter(goal_kinds(agents))

    alignment = safe_ratio(max(goal_counter.values()), count) if goal_counter else 0.0

    # Concordance attendue des catégories : probabilité que deux tirages
    # indépendants tombent sur la même catégorie de buts, Σ p_i².
    # **Ce n'est pas une coopération** : aucune compatibilité par paire n'est
    # testée, aucun comportement de coopération n'est observé (P1 : renommé
    # pour dire ce qu'il calcule réellement).
    total = sum(goal_counter.values())
    concordance = (
        sum(safe_ratio(value, total) ** 2 for value in goal_counter.values())
        if total and len(agents) > 1
        else 0.0
    )

    return {
        "GlobalGoalAlignment": alignment,
        "GoalDiversity": shannon(goal_counter),
        "GoalCategoryConcordance": concordance,
        "GoalTypeCounts": keyed(goal_counter),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
