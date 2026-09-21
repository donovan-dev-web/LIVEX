"""Moteur 6 — ResourceSustainabilityMetrics (durabilité des ressources).

Mesure l'équilibre entre consommation et production (METRICS_SPEC.md §7).
Squelette : implémentation au jalon U1.
"""

ENGINE_NAME = "ResourceSustainabilityMetrics"

METRICS = (
    "ResourceToConsumptionRatio",
    "CriticalityPoints",
    "RecoveryTime",
)


def compute(snapshot: dict) -> dict:
    """Calcule les métriques de durabilité des ressources sur un snapshot SYNE."""
    raise NotImplementedError(
        f"Moteur {ENGINE_NAME} non implémenté (jalon U1). "
        f"Métriques : {', '.join(METRICS)}."
    )
