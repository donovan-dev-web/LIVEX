"""Moteur 2 — InformationPropagationMetrics (propagation de l'information).

Mesure la circulation et la dégradation de l'information (METRICS_SPEC.md §3).
Squelette : implémentation au jalon U1.
"""

ENGINE_NAME = "InformationPropagationMetrics"

METRICS = (
    "MessageVolume",
    "InformationDiffusionSpeed",
    "RumorAccuracyDegradation",
    "MaxMessageHops",
    "NetworkCentrality",
)


def compute(snapshot: dict) -> dict:
    """Calcule les métriques de propagation de l'information sur un snapshot SYNE."""
    raise NotImplementedError(
        f"Moteur {ENGINE_NAME} non implémenté (jalon U1). "
        f"Métriques : {', '.join(METRICS)}."
    )
