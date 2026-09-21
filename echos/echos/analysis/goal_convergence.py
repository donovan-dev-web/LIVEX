"""Moteur 4 — GoalConvergenceMetrics (convergence des objectifs).

Mesure l'alignement ou la divergence des objectifs (METRICS_SPEC.md §5).
Squelette : implémentation au jalon U1.
"""

ENGINE_NAME = "GoalConvergenceMetrics"

METRICS = (
    "GlobalGoalAlignment",
    "GoalDiversity",
    "CooperationPotential",
    "GoalTypeCounts",
)


def compute(snapshot: dict) -> dict:
    """Calcule les métriques de convergence des objectifs sur un snapshot SYNE."""
    raise NotImplementedError(
        f"Moteur {ENGINE_NAME} non implémenté (jalon U1). "
        f"Métriques : {', '.join(METRICS)}."
    )
