"""Moteurs de métriques ECHOS (METRICS_SPEC.md §1 — 7 moteurs).

Chaque moteur est buildable et testable séparément : ``ENGINE_NAME`` (contrat
METRICS_SPEC), ``METRICS`` (nomenclature des métriques), ``compute(snapshot)``
(fonction pure et déterministe du dict de snapshot transport, camelCase).
"""

from importlib import import_module

_MODULES = (
    "cognitive_diversity",
    "information_propagation",
    "social_complexity",
    "goal_convergence",
    "feedback_loop_detector",
    "resource_sustainability",
    "group_dynamics",
)

ENGINES = tuple(import_module(f"{__name__}.{name}") for name in _MODULES)


def known_engines() -> dict[str, tuple[str, ...]]:
    """Registre {nom moteur → métriques} (contrat publié pour l'UI et la CI)."""
    return {engine.ENGINE_NAME: engine.METRICS for engine in ENGINES}


__all__ = ["ENGINES", "known_engines"]
