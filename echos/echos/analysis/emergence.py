"""Moteur 8 — EmergenceIndicators (indicateurs d'émergence, ECHOS-030→033).

Composite (EMERGENCE_INDICATORS.md) : ``compute(snapshot)`` exécute les
7 moteurs de métriques puis compose les indicateurs ; ``compute_from_metrics``
compose directement à partir de résultats déjà calculés (miroir de la méthode
``Calculate`` du prototype C#). Fonctions **pures** et déterministes : aucun
PRNG, ordre stable (phénomènes par seuils, signaux dans l'ordre de la spec).

Conventions V0.1 (cf. METRICS_SPEC.md §1, ECHOS-006) :
- données manquantes → valeurs neutres 0.0 ; ``DetectedPhenomena`` devient vide ;
- ``Disclaimer`` invariant (ECHOS-032, Monographie §4.10.3) ;
- ``DiffusionSpeed_Norm = clamp(1 - InformationDiffusionSpeed / 100, 0, 1)`` avec
  neutralité étendue : vitesse non mesurée (absente ou ≤ 0 — « jamais diffusé »)
  → contribution 0.0 plutôt que le max de la formule (diffusion instantanée, non
  observable). La formule s'applique aux vitesses mesurées (> 0).
"""

from __future__ import annotations

from . import (
    cognitive_diversity,
    feedback_loop_detector,
    goal_convergence,
    group_dynamics,
    information_propagation,
    social_complexity,
)
from ._common import clamp

ENGINE_NAME = "EmergenceIndicators"

METRICS = (
    "EmergenceScore",
    "DetectedPhenomena",
    "SystemComplexity",
    "UnpredictabilityIndex",
    "Disclaimer",
)

DISCLAIMER = (
    "ECHOS ne doit jamais transformer une métrique en vérité scientifique : "
    "un score d'émergence ou une valeur de centralité reste une mesure "
    "particulière d'un phénomène, jamais une preuve de l'existence d'une "
    "intelligence ou d'une société (Monographie §4.10.3, ECHOS-032)."
)

# Moteurs de métriques entrants du score composite et des phénomènes (ordre de
# première priorité lors de l'aplatissement — ex. ``GoalDiversity`` est fournie
# par les moteurs cognitif et de convergence, valeurs identiques).
_METRIC_ENGINES = (
    "CognitiveDiversityMetrics",
    "InformationPropagationMetrics",
    "SocialComplexityMetrics",
    "GoalConvergenceMetrics",
    "FeedbackLoopDetector",
    "GroupDynamicsMetrics",
)

# Spécification des phénomènes auto-détectés (ECHOS-031) : identifiant stable
# (contrat public), libellé français, description et signaux (métrique, seuil).
# Condition de détection = toutes les valeurs strictement supérieures aux seuils.
_PHENOMENA = (
    {
        "identifier": "CommunityFormation",
        "label": "Formation de communauté",
        "description": (
            "Plus de deux communautés distinctes émergent du graphe de confiance."
        ),
        "signals": (("NumberOfCommunities", 2),),
    },
    {
        "identifier": "FeedbackLoops",
        "label": "Dynamiques de rétroaction complexes",
        "description": "Plus de cinq boucles de rétroaction distinctes sont actives.",
        "signals": (("IdentifiedLoops", 5),),
    },
    {
        "identifier": "CollectiveCoordination",
        "label": "Coordination collective",
        "description": "Les objectifs des entités convergent fortement.",
        "signals": (("GoalConvergence", 0.7),),
    },
    {
        "identifier": "InformationBottleneck",
        "label": "Goulot d'information",
        "description": (
            "Une part dominante de la communication transite par peu d'émetteurs."
        ),
        "signals": (("NetworkCentrality", 0.3),),
    },
    {
        "identifier": "OrganizationalDynamics",
        "label": "Dynamiques organisationnelles",
        "description": (
            "Des groupes actifs et une rotation de membres soutenue indiquent une "
            "organisation éphémère."
        ),
        "signals": (("ActiveGroups", 5), ("MemberTurnoverRate", 0.1)),
    },
)

__all__ = [
    "ENGINE_NAME",
    "METRICS",
    "DISCLAIMER",
    "compute",
    "compute_from_metrics",
]


def _engine_results(snapshot: dict) -> dict[str, dict]:
    """Résultats des 6 moteurs entrants pour un snapshot (repli neutre absent)."""
    engines = (
        cognitive_diversity,
        information_propagation,
        social_complexity,
        goal_convergence,
        feedback_loop_detector,
        group_dynamics,
    )
    return {engine.ENGINE_NAME: engine.compute(snapshot) for engine in engines}


def _flatten(metrics: dict[str, dict]) -> dict[str, float]:
    """Aplatit {moteur → {métrique → valeur}} en {métrique → valeur}.

    Ne retient que les valeurs numériques (les sorties composites telles que
    ``LoopTypes`` ou ``GoalTypeCounts`` ne participent pas aux indicateurs).
    """
    flat: dict[str, float] = {}
    for name in _METRIC_ENGINES:
        for key, value in (metrics.get(name) or {}).items():
            if isinstance(value, (int, float)) and not isinstance(value, bool):
                flat[key] = float(value)
    return flat


def _metric(flat: dict[str, float], name: str, default: float = 0.0) -> float:
    return flat.get(name, default)


def _detected_phenomena(flat: dict[str, float]) -> list[dict]:
    """Phénomènes dont tous les signaux déclencheurs sont au-dessus du seuil."""
    detected: list[dict] = []
    for phenomenon in _PHENOMENA:
        signals = [
            {
                "metric": metric,
                "value": _metric(flat, metric),
                "threshold": threshold,
            }
            for metric, threshold in phenomenon["signals"]
        ]
        if all(signal["value"] > signal["threshold"] for signal in signals):
            detected.append(
                {
                    "identifier": phenomenon["identifier"],
                    "label": phenomenon["label"],
                    "description": phenomenon["description"],
                    "signals": signals,
                }
            )
    return detected


def compute_from_metrics(metrics: dict[str, dict]) -> dict:
    """Compose les indicateurs d'émergence depuis les résultats des moteurs."""
    flat = _flatten(metrics)

    belief_diversity = _metric(flat, "BeliefDiversity")
    goal_diversity = _metric(flat, "GoalDiversity")
    diffusion_speed = _metric(flat, "InformationDiffusionSpeed")
    clustering = _metric(flat, "ClusteringCoefficient")
    loop_strength = _metric(flat, "LoopStrength")
    active_groups = _metric(flat, "ActiveGroups")
    decision_diversity = _metric(flat, "DecisionDiversity")

    diffusion_norm = (
        clamp(1.0 - diffusion_speed / 100.0) if diffusion_speed > 0.0 else 0.0
    )
    emergence_score = clamp(
        belief_diversity * 0.15
        + goal_diversity * 0.15
        + diffusion_norm * 0.10
        + clustering * 0.15
        + loop_strength * 0.20
        + (active_groups / 100.0) * 0.25
    )
    system_complexity = (belief_diversity + goal_diversity + diffusion_speed) / 3.0
    unpredictability_index = loop_strength * decision_diversity

    return {
        "EmergenceScore": emergence_score,
        "DetectedPhenomena": _detected_phenomena(flat),
        "SystemComplexity": system_complexity,
        "UnpredictabilityIndex": unpredictability_index,
        "Disclaimer": DISCLAIMER,
    }


def compute(snapshot: dict) -> dict:
    """Calcule les indicateurs d'émergence sur un snapshot (7 moteurs internes)."""
    return compute_from_metrics(_engine_results(snapshot))
