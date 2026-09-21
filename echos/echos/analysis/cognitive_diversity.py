"""Moteur 1 — CognitiveDiversityMetrics (diversité cognitive).

Mesure la séparation des croyances et des comportements entre entités
(METRICS_SPEC.md §2). Squelette : implémentation au jalon U1.
"""

ENGINE_NAME = "CognitiveDiversityMetrics"

METRICS = (
    "BeliefDiversity",
    "BeliefDisagreement",
    "BeliefConfidenceVariance",
    "GoalDiversity",
    "GoalConvergence",
    "DecisionDiversity",
    "IntentionStability",
    "TraitExpressionDiversity",
)


def compute(snapshot: dict) -> dict:
    """Calcule les métriques de diversité cognitive sur un snapshot SYNE."""
    raise NotImplementedError(
        f"Moteur {ENGINE_NAME} non implémenté (jalon U1). "
        f"Métriques : {', '.join(METRICS)}."
    )
