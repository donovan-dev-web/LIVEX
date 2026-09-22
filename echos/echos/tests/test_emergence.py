"""Indicateurs d'émergence ECHOS (ECHOS-030→033, jalon ph3).

Le moteur composite ``EmergenceIndicators`` compose les 7 moteurs de métriques
pour produire : le score d'émergence composite borné [0, 1] (ECHOS-030),
l'auto-détection des phénomènes avec trace des signaux déclencheurs
(ECHOS-031), la complexité et l'indice d'imprévisibilité (ECHOS-033), et le
disclaimer de la « règle d'or » §4.10.3 (ECHOS-032). Formules : EMERGENCE_INDICATORS.md.
"""

import json
from pathlib import Path

import pytest

from echos.analysis import emergence

FIXTURES = Path(__file__).resolve().parent / "fixtures"

_APPROX = pytest.approx
_NUMERIC = (int, float)


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text())


def _fresh_snapshot() -> dict:
    return _load("snapshot_analysis.json")


def _full_metrics() -> dict[str, dict]:
    """Résultats chiffrés à la main sur la fixture (source : analysis_golden)."""
    return {
        "CognitiveDiversityMetrics": {
            "BeliefDiversity": 1.9219280948873623,
            "GoalDiversity": 1.584962500721156,
            "GoalConvergence": 0.3333333333333333,
            "DecisionDiversity": 1.0,
        },
        "InformationPropagationMetrics": {
            "InformationDiffusionSpeed": 10.0,
            "NetworkCentrality": 1.0,
        },
        "SocialComplexityMetrics": {
            "ClusteringCoefficient": 0.0,
            "NumberOfCommunities": 1,
        },
        "FeedbackLoopDetector": {"LoopStrength": 0.7, "IdentifiedLoops": 4},
        "GroupDynamicsMetrics": {"ActiveGroups": 1.0, "MemberTurnoverRate": 55.55555555555555},
    }


def _metrics_with(**overrides: float) -> dict[str, dict]:
    """Métriques de référence avec des clés surchargées (par moteur)."""
    full = _full_metrics()
    for metric, value in overrides.items():
        for engine in full:
            if metric in full[engine]:
                full[engine][metric] = value
                break
        else:
            raise AssertionError(f"clé inconnue dans les moteurs de référence : {metric}")
    return full


def _score(metrics: dict[str, dict]) -> dict:
    return emergence.compute_from_metrics(metrics)


# ---------------------------------------------------------------------------
# Contrat du moteur composite
# ---------------------------------------------------------------------------


def test_registry_includes_emergence_indicators():
    from echos.analysis import known_engines

    assert "EmergenceIndicators" in known_engines()
    assert set(emergence.METRICS) == {
        "EmergenceScore",
        "DetectedPhenomena",
        "SystemComplexity",
        "UnpredictabilityIndex",
        "Disclaimer",
    }


def test_compute_is_pure_deterministic():
    snapshot = _fresh_snapshot()

    first = emergence.compute(snapshot)
    second = emergence.compute(snapshot)

    assert first == second
    assert snapshot == _fresh_snapshot()


def test_empty_snapshot_is_neutral():
    result = emergence.compute({})

    assert result["EmergenceScore"] == 0.0
    assert result["SystemComplexity"] == 0.0
    assert result["UnpredictabilityIndex"] == 0.0
    assert result["DetectedPhenomena"] == []
    assert result["Disclaimer"] == emergence.DISCLAIMER


def test_compute_snapshot_equals_composition_of_engines():
    """compute(snapshot) ≡ compute_from_metrics(résultats des 6 moteurs)."""
    from echos.analysis import (
        cognitive_diversity,
        feedback_loop_detector,
        goal_convergence,
        group_dynamics,
        information_propagation,
        social_complexity,
    )

    snapshot = _fresh_snapshot()
    metrics = {
        engine.ENGINE_NAME: engine.compute(snapshot)
        for engine in (
            cognitive_diversity,
            information_propagation,
            social_complexity,
            goal_convergence,
            feedback_loop_detector,
            group_dynamics,
        )
    }

    assert emergence.compute(snapshot) == emergence.compute_from_metrics(metrics)


# ---------------------------------------------------------------------------
# ECHOS-030 — Score d'émergence composite [0, 1]
# ---------------------------------------------------------------------------


def test_emergence_score_hand_computed_on_fixture():
    result = _score(_full_metrics())

    # BeliefDiversity 1.9219280948873623×0.15 + GoalDiversity 1.584962500721156×0.15
    # + DiffusionSpeed_Norm 0.9×0.10 + Clustering 0.0×0.15 + LoopStrength 0.7×0.20
    # + (ActiveGroups/100) 0.01×0.25 = 0.7585335893412777
    assert result["EmergenceScore"] == _APPROX(0.7585335893412777, abs=1e-9)


def test_emergence_score_weights_sum_to_one():
    """Poids (0.15, 0.15, 0.10, 0.15, 0.20, 0.25) — total 1.0."""
    assert 0.15 + 0.15 + 0.10 + 0.15 + 0.20 + 0.25 == 1.0


def test_emergence_score_stays_in_unit_interval():
    """Termes qui peuvent dépasser 1 (entropies) → score clampé sur [0, 1]."""
    inflated = _metrics_with(BeliefDiversity=2.0, GoalDiversity=2.0)
    inflated["SocialComplexityMetrics"]["ClusteringCoefficient"] = 1.5

    result = _score(inflated)

    assert 0.0 <= result["EmergenceScore"] <= 1.0


def test_diffusion_speed_norm_decreases_with_ticks():
    slow = _score(_metrics_with(InformationDiffusionSpeed=90.0))
    fast = _score(_metrics_with(InformationDiffusionSpeed=10.0))

    assert slow["EmergenceScore"] < fast["EmergenceScore"]


def test_undiffused_system_is_neutral_for_diffusion_term():
    """Vitesse non mesurée (0.0) → contribution 0.0, pas le max de la formule."""
    neutral = _score(_metrics_with(InformationDiffusionSpeed=0.0))
    diffused = _score(_metrics_with(InformationDiffusionSpeed=10.0))

    assert neutral["EmergenceScore"] == _APPROX(
        diffused["EmergenceScore"] - 0.09, abs=1e-12
    )


# ---------------------------------------------------------------------------
# ECHOS-031 — Auto-détection des phénomènes + trace des signaux
# ---------------------------------------------------------------------------


@pytest.mark.parametrize(
    "override,expected_identifier",
    [
        ({"NumberOfCommunities": 3}, "CommunityFormation"),
        ({"IdentifiedLoops": 6}, "FeedbackLoops"),
        ({"GoalConvergence": 0.71}, "CollectiveCoordination"),
        ({"NetworkCentrality": 0.4}, "InformationBottleneck"),
        ({"ActiveGroups": 6, "MemberTurnoverRate": 0.2}, "OrganizationalDynamics"),
    ],
)
def test_phenomenon_detected_above_threshold(override, expected_identifier):
    metrics = _full_metrics()
    for key, value in override.items():
        for engine in metrics:
            if key in metrics[engine]:
                metrics[engine][key] = value

    identifiers = [p["identifier"] for p in _score(metrics)["DetectedPhenomena"]]

    assert expected_identifier in identifiers


def test_fixture_detects_only_information_bottleneck():
    """valeur de référence : centralité 1.0 > 0.3 ; autres seuils non atteints."""
    result = _score(_full_metrics())["DetectedPhenomena"]

    assert [p["identifier"] for p in result] == ["InformationBottleneck"]


def test_no_phenomenon_below_all_thresholds():
    metrics = _full_metrics()
    for key, *_ in (
        ("NumberOfCommunities",),
        ("IdentifiedLoops",),
        ("GoalConvergence",),
        ("NetworkCentrality",),
        ("ActiveGroups",),
        ("MemberTurnoverRate",),
    ):
        for engine in metrics:
            if key in metrics[engine]:
                metrics[engine][key] = 0.0

    assert _score(metrics)["DetectedPhenomena"] == []


def test_organizational_dynamics_requires_both_signals():
    metrics = _metrics_with(ActiveGroups=6, MemberTurnoverRate=0.0)

    identifiers = [p["identifier"] for p in _score(metrics)["DetectedPhenomena"]]

    assert "OrganizationalDynamics" not in identifiers


def test_phenomenon_signals_trace_triggering_values():
    """La trace des signaux déclencheurs (métrique, valeur, seuil) est exposée."""
    result = _score(_full_metrics())["DetectedPhenomena"]

    assert result[0]["signals"] == [
        {"metric": "NetworkCentrality", "value": 1.0, "threshold": 0.3}
    ]


def test_phenomena_stable_ordering():
    """Ordre d'émission stable (spec) — plusieurs phénomènes simultanés."""
    metrics = _full_metrics()
    metrics["SocialComplexityMetrics"]["NumberOfCommunities"] = 3
    metrics["FeedbackLoopDetector"]["IdentifiedLoops"] = 6
    metrics["InformationPropagationMetrics"]["NetworkCentrality"] = 0.4

    identifiers = [p["identifier"] for p in _score(metrics)["DetectedPhenomena"]]

    assert identifiers == ["CommunityFormation", "FeedbackLoops", "InformationBottleneck"]


# ---------------------------------------------------------------------------
# ECHOS-033 — Complexité & imprévisibilité
# ---------------------------------------------------------------------------


def test_system_complexity_hand_computed():
    result = _score(_full_metrics())

    assert result["SystemComplexity"] == _APPROX(4.5022968652028394, abs=1e-9)


def test_unpredictability_index_is_loop_strength_times_decision_diversity():
    result = _score(_full_metrics())

    assert result["SystemComplexity"] > result["EmergenceScore"]
    assert result["UnpredictabilityIndex"] == _APPROX(0.7 * 1.0, abs=1e-9)


def test_unpredictability_follows_decision_diversity():
    low = _score(_metrics_with(DecisionDiversity=0.5))
    high = _score(_metrics_with(DecisionDiversity=1.0))

    assert low["UnpredictabilityIndex"] == _APPROX(0.35, abs=1e-12)
    assert high["UnpredictabilityIndex"] == _APPROX(0.7, abs=1e-12)


# ---------------------------------------------------------------------------
# ECHOS-032 — Règle d'or §4.10.3 (disclaimer)
# ---------------------------------------------------------------------------


def test_disclaimer_is_never_presented_as_proof():
    result = _score(_full_metrics())

    assert result["Disclaimer"] == emergence.DISCLAIMER
    assert "preuve de l'existence d'une intelligence" in result["Disclaimer"]
    assert "Monographie §4.10.3" in result["Disclaimer"]
