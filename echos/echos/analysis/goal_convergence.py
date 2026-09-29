"""Moteur 4 — GoalConvergenceMetrics (convergence des objectifs).

Mesure l'alignement ou la divergence des objectifs (METRICS_SPEC.md §5).
Les 4 métriques sont déterministes sur la distribution des types d'objectifs
actifs des agents (``goals[].kind``, repli ``currentAction`` / ``Idle``).
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
    "CooperationPotential",
    "GoalTypeCounts",
)


def compute(snapshot: dict) -> dict:
    """Calcule les 4 métriques de convergence des objectifs."""
    agents = agents_of(snapshot)
    count = alive_count(snapshot)
    goal_counter = Counter(goal_kinds(agents))

    alignment = safe_ratio(max(goal_counter.values()), count) if goal_counter else 0.0

    # Coopération : probabilité qu'une paire (a, b) partage au moins un objectif,
    # approchée par Σ p_i² (deux tirages indépendants dans la même distribution).
    total = sum(goal_counter.values())
    cooperation = (
        sum(safe_ratio(value, total) ** 2 for value in goal_counter.values())
        if total and len(agents) > 1
        else 0.0
    )

    return {
        "GlobalGoalAlignment": alignment,
        "GoalDiversity": shannon(goal_counter),
        "CooperationPotential": cooperation,
        "GoalTypeCounts": keyed(goal_counter),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
