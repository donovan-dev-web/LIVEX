"""Indicateurs d'émergence ECHOS (ECHOS-030→033, jalon ph3).

Le moteur composite ``EmergenceIndicators`` compose les moteurs de métriques
pour produire : le score d'émergence composite borné [0, 1] (ECHOS-030),
ses **contributions décomposées** (refonte P2), l'auto-détection des signaux
avec trace des valeurs déclencheuses (ECHOS-031), la complexité (ECHOS-033) et
le disclaimer de la « règle d'or » §4.10.3 (ECHOS-032).

Formules : EMERGENCE_INDICATORS.md. Statut du score : **exploratoire** (poids
[HÉRITÉ], non calibrés).
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
    """Résultats des moteurs sur les composantes du score (source : golden).

    Toutes les clés listées font partie de ``COMPOSITE_DEPENDENCIES`` : une
    seule est retirée et la provenance de la sortie correspondante passe à
    « non mesuré » (test dédié).
    """
    return {
        "CognitiveDiversityMetrics": {
            "BeliefDiversity": 1.9219280948873623,
            "BeliefDiversityNorm": 0.5,
            "GoalDiversity": 1.584962500721156,
            "GoalDiversityNorm": 0.5,
            "GoalConvergence": 0.3333333333333333,
        },
        "InformationPropagationMetrics": {
            "EmitterCoverageDelay": 50.0,
            "SenderConcentration": 0.3333333333333333,
        },
        "SocialComplexityMetrics": {
            "ClusteringCoefficient": 0.5,
            "NumberOfCommunities": 1,
        },
        "FeedbackLoopDetector": {
            "RepeatedActionShare": 0.5,
            "RepeatedActionPairs": 4,
        },
        "GroupDynamicsMetrics": {
            "InferredCommunities": 1.0,
            "CommunityCoverage": 0.5,
            "MemberExitsPerDissolution": 1.0,
        },
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
        "ContributionBeliefDiversity",
        "ContributionGoalDiversity",
        "ContributionEmitterCoverage",
        "ContributionClustering",
        "ContributionRepeatedActions",
        "ContributionCommunityCoverage",
        "DetectedPhenomena",
        "SystemComplexity",
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
    for metric in emergence.WEIGHTS:
        assert result[metric] == 0.0
    assert result["DetectedPhenomena"] == []
    assert result["Disclaimer"] == emergence.DISCLAIMER


def test_compute_snapshot_equals_composition_of_engines():
    """compute(snapshot) ≡ compute_from_metrics(résultats des moteurs)."""
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

    # Toutes les composantes valent 0,5 et les poids somment à 1,0 :
    # 0,5 × (0.15 + 0.15 + 0.10 + 0.15 + 0.20 + 0.25) = 0,5.
    assert result["EmergenceScore"] == _APPROX(0.5, abs=1e-9)
    assert result["ContributionBeliefDiversity"] == _APPROX(0.075, abs=1e-9)
    assert result["ContributionEmitterCoverage"] == _APPROX(0.05, abs=1e-9)


def test_emergence_score_weights_sum_to_one():
    """Poids (0.15, 0.15, 0.10, 0.15, 0.20, 0.25) — total 1.0."""
    assert _APPROX(sum(emergence.WEIGHTS.values()), abs=1e-12) == 1.0


def test_contributions_sum_to_the_score():
    """Σ contributions == EmergenceScore : le score est décomposable tel quel."""
    for metrics in (
        _full_metrics(),
        _metrics_with(BeliefDiversityNorm=0.12, CommunityCoverage=0.97),
        _metrics_with(EmitterCoverageDelay=90.0, RepeatedActionShare=1.0),
    ):
        result = _score(metrics)
        total = sum(result[metric] for metric in emergence.WEIGHTS)
        assert total == _APPROX(result["EmergenceScore"], abs=1e-12)


def test_raw_entropy_never_inflates_the_score():
    """Le score lit les grandeurs normalisées, pas les entropies en bits.

    Régression : une entropie brute de 50 bits entrait telle quelle dans la
    somme et la saturait dans le ``clamp`` final — le score perdait toute
    discrimination dans la partie haute.
    """
    base = _score(_full_metrics())
    inflated = _score(_metrics_with(BeliefDiversity=50.0, GoalDiversity=50.0))

    assert inflated["EmergenceScore"] == _APPROX(base["EmergenceScore"], abs=1e-12)


def test_emergence_score_stays_in_unit_interval():
    """Composantes hors bornes → score clampé sur [0, 1] (garde)."""
    inflated = _metrics_with(BeliefDiversityNorm=2.0, GoalDiversityNorm=2.0)
    inflated["SocialComplexityMetrics"]["ClusteringCoefficient"] = 1.5

    result = _score(inflated)

    assert 0.0 <= result["EmergenceScore"] <= 1.0


def test_diffusion_delay_norm_decreases_with_ticks():
    slow = _score(_metrics_with(EmitterCoverageDelay=90.0))
    fast = _score(_metrics_with(EmitterCoverageDelay=10.0))

    assert slow["EmergenceScore"] < fast["EmergenceScore"]


def test_undiffused_system_is_neutral_for_diffusion_term():
    """Délai non mesuré (0.0) → contribution 0.0, pas le max de la formule."""
    neutral = _score(_metrics_with(EmitterCoverageDelay=0.0))
    diffused = _score(_metrics_with(EmitterCoverageDelay=10.0))

    assert neutral["EmergenceScore"] == _APPROX(
        diffused["EmergenceScore"] - 0.09, abs=1e-12
    )


# ---------------------------------------------------------------------------
# ECHOS-031 — Auto-détection des signaux + trace des valeurs déclencheuses
# ---------------------------------------------------------------------------


@pytest.mark.parametrize(
    "override,expected_identifier",
    [
        ({"NumberOfCommunities": 3}, "CommunityFormation"),
        ({"RepeatedActionPairs": 6}, "FeedbackLoops"),
        ({"GoalConvergence": 0.71}, "CollectiveCoordination"),
        ({"SenderConcentration": 0.4}, "InformationBottleneck"),
        ({"InferredCommunities": 6, "MemberExitsPerDissolution": 1.2},
         "OrganizationalDynamics"),
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
    """Valeur de référence : concentration 1/3 > 0,3 ; autres seuils non atteints."""
    result = _score(_full_metrics())["DetectedPhenomena"]

    assert [p["identifier"] for p in result] == ["InformationBottleneck"]


def test_information_bottleneck_follows_concentration_not_diversity():
    """Le sens du détecteur est corrigé : plus d'émetteurs distincts ⇒ moins
    de concentration, donc pas de « goulot ».

    Régression (P0) : ``NetworkCentrality`` comptait les émetteurs distincts
    par message et le seuil ``> 0.3`` se déclenchait donc dès trois messages —
    exactement l'inverse de la description du phénomène.
    """
    concentrated = _score(_metrics_with(SenderConcentration=0.9))
    spread = _score(_metrics_with(SenderConcentration=0.05))

    assert [p["identifier"] for p in concentrated["DetectedPhenomena"]] == [
        "InformationBottleneck"
    ]
    assert [p["identifier"] for p in spread["DetectedPhenomena"]] == []


def test_no_phenomenon_below_all_thresholds():
    metrics = _full_metrics()
    for key in (
        "NumberOfCommunities",
        "RepeatedActionPairs",
        "GoalConvergence",
        "SenderConcentration",
        "InferredCommunities",
        "MemberExitsPerDissolution",
    ):
        for engine in metrics:
            if key in metrics[engine]:
                metrics[engine][key] = 0.0

    assert _score(metrics)["DetectedPhenomena"] == []


def test_organizational_dynamics_requires_both_signals():
    metrics = _metrics_with(InferredCommunities=6, MemberExitsPerDissolution=0.0)

    identifiers = [p["identifier"] for p in _score(metrics)["DetectedPhenomena"]]

    assert "OrganizationalDynamics" not in identifiers


def test_phenomenon_signals_trace_triggering_values():
    """La trace des signaux déclencheurs (métrique, valeur, seuil) est exposée."""
    result = _score(_full_metrics())["DetectedPhenomena"]

    assert result[0]["signals"] == [
        {
            "metric": "SenderConcentration",
            "value": 0.3333333333333333,
            "threshold": 0.3,
        }
    ]


def test_phenomena_labels_are_requalified():
    """Les libellés décrivent l'observation, pas une conclusion (P0)."""
    metrics = _full_metrics()
    metrics["SocialComplexityMetrics"]["NumberOfCommunities"] = 3
    metrics["FeedbackLoopDetector"]["RepeatedActionPairs"] = 6

    detected = {p["identifier"]: p for p in _score(metrics)["DetectedPhenomena"]}

    assert "sans référence temporelle" in detected["CommunityFormation"]["description"]
    assert "pas une boucle causale" in detected["FeedbackLoops"]["description"]


def test_phenomena_stable_ordering():
    """Ordre d'émission stable (spec) — plusieurs signaux simultanés."""
    metrics = _full_metrics()
    metrics["SocialComplexityMetrics"]["NumberOfCommunities"] = 3
    metrics["FeedbackLoopDetector"]["RepeatedActionPairs"] = 6
    metrics["InformationPropagationMetrics"]["SenderConcentration"] = 0.4

    identifiers = [p["identifier"] for p in _score(metrics)["DetectedPhenomena"]]

    assert identifiers == ["CommunityFormation", "FeedbackLoops", "InformationBottleneck"]


# ---------------------------------------------------------------------------
# ECHOS-033 — Complexité du système
# ---------------------------------------------------------------------------


def test_system_complexity_hand_computed():
    result = _score(_full_metrics())

    # (croyances 0,5 + objectifs 0,5 + couverture 0,5) / 3 = 0,5.
    assert result["SystemComplexity"] == _APPROX(0.5, abs=1e-9)


def test_system_complexity_is_bounded_by_one():
    """L'indicateur ne doit pas croître avec la durée du run.

    Régression : la formule intégrait la vitesse de diffusion brute (un nombre
    de ticks non borné), ce qui produisait 10, 100, 1000… selon la longueur du
    run. La formule canonique compose trois grandeurs bornées [0, 1].
    """
    metrics = _full_metrics()
    metrics["CognitiveDiversityMetrics"]["BeliefDiversityNorm"] = 1.0
    metrics["CognitiveDiversityMetrics"]["GoalDiversityNorm"] = 1.0
    metrics["InformationPropagationMetrics"]["EmitterCoverageDelay"] = 5000.0
    long_run = _score(metrics)["SystemComplexity"]

    # (1 + 1 + 0) / 3 : au-delà de l'horizon, la contribution de diffusion est
    # neutre (0,0) — jamais négative, jamais croissante avec les ticks.
    assert long_run == _APPROX(2 / 3, abs=1e-9)
    assert 0.0 <= long_run <= 1.0

    metrics["InformationPropagationMetrics"]["EmitterCoverageDelay"] = 1000.0
    longer_run = _score(metrics)["SystemComplexity"]
    assert longer_run == _APPROX(long_run, abs=1e-12)


def test_unpredictability_index_was_removed():
    """Décision P0 : l'ancien indice mesurait un produit de fréquences, pas
    l'imprévisibilité, qui exigerait une baseline prédictive hors échantillon."""
    result = _score(_full_metrics())

    assert "UnpredictabilityIndex" not in result
    assert "UnpredictabilityIndex" not in emergence.METRICS


# ---------------------------------------------------------------------------
# ECHOS-032 — Règle d'or §4.10.3 (disclaimer)
# ---------------------------------------------------------------------------


def test_disclaimer_is_never_presented_as_proof():
    result = _score(_full_metrics())

    assert result["Disclaimer"] == emergence.DISCLAIMER
    assert "preuve de l'existence d'une intelligence" in result["Disclaimer"]
    assert "Monographie §4.10.3" in result["Disclaimer"]
