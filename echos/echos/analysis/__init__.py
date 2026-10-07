"""Moteurs de métriques ECHOS (METRICS_SPEC.md §1 — 7 moteurs + indicateurs).

Chaque moteur est buildable et testable séparément : ``ENGINE_NAME`` (contrat
METRICS_SPEC), ``METRICS`` (nomenclature des métriques), ``compute(snapshot)``
(fonction pure et déterministe du dict de snapshot transport, camelCase).
``EmergenceIndicators`` (ECHOS-030→033) est le moteur composite : il exécute
les 7 moteurs puis compose le score d'émergence et les phénomènes
(EMERGENCE_INDICATORS.md).

Le registre des moteurs vit dans :mod:`echos.analysis._common` (chargement
paresseux) afin que ``emergence`` puisse le consumed sans créer de cycle
d'import ; ce module n'en est que la façade publique.
"""

from contextlib import nullcontext
from typing import Any

from ._common import COMPOSITE_ENGINE_NAME, load_engines, measured_flags

ENGINES = load_engines()

COMPOSITE_ENGINE = next(
    engine for engine in ENGINES if engine.ENGINE_NAME == COMPOSITE_ENGINE_NAME
)
"""Moteur composite, exécuté en dernier à partir des résultats des autres."""


def known_engines() -> dict[str, tuple[str, ...]]:
    """Registre {nom moteur → métriques} (contrat publié pour l'UI et la CI)."""
    return {engine.ENGINE_NAME: engine.METRICS for engine in ENGINES}


def compute_all(snapshot: dict, profile: Any = None) -> dict[str, dict]:
    """Exécute les 8 moteurs sur un snapshot (7 métriques + EmergenceIndicators).

    Retourne ``{ENGINE_NAME: {METRIC: value}}`` dans l'ordre stable du registre
    (déterminisme d'émission). ``EmergenceIndicators`` réutilise les résultats
    déjà calculés des 6 moteurs entrants (``compute_from_metrics`` — pas de
    double calcul interne). Contrat des moteurs inchangé : fonction pure,
    repli neutre 0.0 sur données manquantes. L'intégration ECHOS ph4 (API
    REST, pipeline d'ingestion) consomme ce résultat agrégé.

    ``profile`` (optionnel, ECHOS-052) : objet à context-manager
    ``measure(name)`` — les moteurs sont exécutés dans ce marqueur sans
    modifier la valeur retournée (``ProfileMarkers`` de
    ``echos.instrumentation.profiling``).
    """
    results: dict[str, dict] = {}
    for engine in ENGINES:
        if engine.ENGINE_NAME == COMPOSITE_ENGINE_NAME:
            continue
        with _marker(profile, engine.ENGINE_NAME):
            results[engine.ENGINE_NAME] = engine.compute(snapshot)
    with _marker(profile, COMPOSITE_ENGINE_NAME):
        results[COMPOSITE_ENGINE_NAME] = COMPOSITE_ENGINE.compute_from_metrics(results)
    return results


def provenance(snapshot: dict) -> dict[str, dict[str, bool]]:
    """Drapeaux de mesure par (moteur, métrique) — provenance des valeurs.

    Une métrique est **mesurée** si les données dont elle dépend étaient
    présentes. Sans ce drapeau, rien ne distinguait en base une valeur
    réellement calculée d'un repli neutre 0.0 : c'est ce qui a permis à sept
    métriques (``FeedbackLoopDetector``, ``RecoveryTime``,
    ``CommunityStability``) de rester à 0 sur *tout* run réel, faute de
    contrepartie visible.

    Le composite ``EmergenceIndicators`` propage depuis ses **dépendances
    réelles** (``emergence.COMPOSITE_DEPENDENCIES``) : chaque sortie n'exige
    que les métriques qui entrent dans sa formule. L'ancienne règle (exiger
    *toutes* les métriques de *tous* les moteurs entrants) marquait « non
    mesuré » un score entier à cause d'une métrique incidente qui n'entrait
    pas dans sa formule. Un score d'émergence calculé sur une fenêtre vide
    reste non mesuré ; une contribution isolée reste mesurée si **sa** source
    l'est.

    Contrat des moteurs inchangé : ``compute`` retourne les mêmes valeurs ;
    ``measured_flags`` lit la déclaration ``REQUIRES`` de chaque moteur.
    """
    from .emergence import COMPOSITE_DEPENDENCIES

    flags: dict[str, dict[str, bool]] = {}
    for engine in ENGINES:
        if engine.ENGINE_NAME == COMPOSITE_ENGINE_NAME:
            continue
        flags[engine.ENGINE_NAME] = measured_flags(snapshot, engine)

    flat_measured: dict[str, bool] = {}
    for engine_flags in flags.values():
        flat_measured.update(engine_flags)

    composite_flags: dict[str, bool] = {}
    for metric in COMPOSITE_ENGINE.METRICS:
        if metric == "Disclaimer":
            # Constante textuelle publiée telle quelle : toujours disponible.
            composite_flags[metric] = True
            continue
        dependencies = COMPOSITE_DEPENDENCIES.get(metric)
        if dependencies is None:
            composite_flags[metric] = flat_measured.get(metric, False)
            continue
        composite_flags[metric] = all(
            flat_measured.get(dependency, False) for dependency in dependencies
        )
    flags[COMPOSITE_ENGINE_NAME] = composite_flags
    return flags


def _marker(profile: Any, name: str) -> Any:
    return profile.measure(name) if profile is not None else nullcontext()


__all__ = ["COMPOSITE_ENGINE", "COMPOSITE_ENGINE_NAME", "ENGINES", "compute_all", "known_engines"]
