"""Moteur 5 — FeedbackLoopDetector (boucles de rétroaction).

Identifie les cycles action → conséquence → décision (METRICS_SPEC.md §6).
Squelette : implémentation au jalon U1.
"""

ENGINE_NAME = "FeedbackLoopDetector"

METRICS = (
    "IdentifiedLoops",
    "LoopStrength",
    "SystemStability",
    "CriticalLoops",
    "LoopTypes",
)


def compute(snapshot: dict) -> dict:
    """Détecte les boucles de rétroaction dans un snapshot SYNE."""
    raise NotImplementedError(
        f"Moteur {ENGINE_NAME} non implémenté (jalon U1). "
        f"Métriques : {', '.join(METRICS)}."
    )
