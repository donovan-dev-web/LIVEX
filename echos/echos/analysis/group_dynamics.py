"""Moteur 7 — GroupDynamicsMetrics (dynamique des groupes).

Mesure la formation, la vie et la dissolution des groupes (METRICS_SPEC.md §8).
Squelette : implémentation au jalon U1.
"""

ENGINE_NAME = "GroupDynamicsMetrics"

METRICS = (
    "ActiveGroups",
    "AverageGroupSize",
    "AverageGroupLifetime",
    "GroupFormationRate",
    "GroupDissolutionRate",
    "GroupObjectiveSuccessRate",
    "MemberTurnoverRate",
)


def compute(snapshot: dict) -> dict:
    """Calcule les métriques de dynamique des groupes sur un snapshot SYNE."""
    raise NotImplementedError(
        f"Moteur {ENGINE_NAME} non implémenté (jalon U1). "
        f"Métriques : {', '.join(METRICS)}."
    )
