"""Moteurs de métriques ECHOS (METRICS_SPEC.md §1 — 7 moteurs + indicateurs).

Chaque moteur est buildable et testable séparément : ``ENGINE_NAME`` (contrat
METRICS_SPEC), ``METRICS`` (nomenclature des métriques), ``compute(snapshot)``
(fonction pure et déterministe du dict de snapshot transport, camelCase).
``EmergenceIndicators`` (ECHOS-030→033) est le moteur composite : il exécute
les 7 moteurs puis compose le score d'émergence et les phénomènes (EMERGENCE_INDICATORS.md).
"""

from importlib import import_module

from contextlib import nullcontext
from typing import Any

_MODULES = (
    "cognitive_diversity",
    "information_propagation",
    "social_complexity",
    "goal_convergence",
    "feedback_loop_detector",
    "resource_sustainability",
    "group_dynamics",
    "emergence",
)

ENGINES = tuple(import_module(f"{__name__}.{name}") for name in _MODULES)


def known_engines() -> dict[str, tuple[str, ...]]:
    """Registre {nom moteur → métriques} (contrat publié pour l'UI et la CI)."""
    return {engine.ENGINE_NAME: engine.METRICS for engine in ENGINES}


def compute_all(snapshot: dict, profile: Any = None) -> dict[str, dict]:
    """Exécute les 8 moteurs sur un snapshot (7 métriques + EmergenceIndicators).

    Retourne ``{ENGINE_NAME: {METRIC: value}}`` dans l'ordre stable du registre
    (déterminisme d'émission). ``EmergenceIndicators`` réutilise les résultats
    déjà calculés des 6 moteurs entrants (``compute_from_metrics`` — pas de
    double calcul interne). Contrat des moteurs inchangé : fonction pure, hideuse
    des données manquantes (repli neutre 0.0). L'intégration ECHOS ph4 (API
    REST, pipeline d'ingestion) consomme ce résultat agrégé.

    ``profile`` (optionnel, ECHOS-052) : objet à context-manager
    ``measure(name)`` — les moteurs sont exécutés dans ce marqueur sans
    modifier la valeur retournée (``ProfileMarkers`` de
    ``echos.instrumentation.profiling``).
    """
    results: dict[str, dict] = {}
    composite: object | None = None
    for engine in ENGINES:
        if engine.ENGINE_NAME == "EmergenceIndicators":
            composite = engine
            continue
        with _marker(profile, engine.ENGINE_NAME):
            results[engine.ENGINE_NAME] = engine.compute(snapshot)
    if composite is not None:
        with _marker(profile, composite.ENGINE_NAME):
            results[composite.ENGINE_NAME] = composite.compute_from_metrics(results)
    return results


def _marker(profile: Any, name: str) -> Any:
    return profile.measure(name) if profile is not None else nullcontext()


__all__ = ["ENGINES", "compute_all", "known_engines"]
