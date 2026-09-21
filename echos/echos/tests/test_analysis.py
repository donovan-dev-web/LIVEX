"""Moteurs de métriques ECHOS (ECHOS-020→026, jalon U2).

Chaque moteur est une fonction pure et déterministe ``compute(snapshot) ->
dict`` sur le dict de transport camelCase (AI/needs__metrics_engines). Les
métriques sont appelées fenêtrées (``history``/``events``/``groupEvents``) ou
instantanées ; sans données, les valeurs neutres 0.0 sont retournées
(ECHOS-006, corset V0.1), sauf documenté par moteur.
"""

import json
import random
from pathlib import Path

import pytest

from echos.analysis import ENGINES, known_engines
from echos.ingestion import WorldSnapshot, parse_message

FIXTURES = Path(__file__).resolve().parent / "fixtures"
GOLDEN = Path(__file__).resolve().parent / "golden"

_APPROX = pytest.approx


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text())


def _golden() -> dict:
    return json.loads((GOLDEN / "analysis_golden.json").read_text())


def _fresh_snapshot() -> dict:
    """Copie profonde (les moteurs ne doivent pas muter leur entrée)."""
    return _load("snapshot_analysis.json")


# ---------------------------------------------------------------------------
# Contrat de registre (7 moteurs, 1 contractuellement par issue ECHOS-020→026)
# ---------------------------------------------------------------------------


def test_registry_exposes_all_seven_engines():
    engines = known_engines()

    assert set(engines) == {
        "CognitiveDiversityMetrics",
        "InformationPropagationMetrics",
        "SocialComplexityMetrics",
        "GoalConvergenceMetrics",
        "FeedbackLoopDetector",
        "ResourceSustainabilityMetrics",
        "GroupDynamicsMetrics",
    }


def test_each_engine_exposes_metrics_and_is_testable_separately():
    for engine in ENGINES:
        assert engine.ENGINE_NAME
        assert len(engine.METRICS) >= 1
        assert callable(engine.compute)


# ---------------------------------------------------------------------------
# Pureté et déterminisme
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("engine", ENGINES, ids=lambda engine: engine.ENGINE_NAME)
def test_compute_is_pure_deterministic(engine):
    snapshot = _fresh_snapshot()

    first = engine.compute(snapshot)
    second = engine.compute(snapshot)

    assert first == second
    # L'entrée n'est pas mutée par le calcul (pureté).
    assert snapshot == _fresh_snapshot()


@pytest.mark.parametrize("engine", ENGINES, ids=lambda engine: engine.ENGINE_NAME)
def test_empty_snapshot_returns_neutral_metrics(engine):
    """Aucune donnée → toutes les métriques neutres (0.0), sans exception."""
    result = engine.compute({})

    assert set(result) == set(engine.METRICS)
    for value in result.values():
        if isinstance(value, dict):
            assert all(isinstance(item, int) for item in value.values())
        else:
            assert isinstance(value, (int, float))


@pytest.mark.parametrize("engine", ENGINES, ids=lambda engine: engine.ENGINE_NAME)
def test_compute_ignores_extra_keys(engine):
    """Clés inconnues (rétro-compat transport) ignorées — contrat V0.1."""
    snapshot = _fresh_snapshot()
    snapshot["futureKey"] = {"anything": [1, 2, 3]}

    assert engine.compute(snapshot) == engine.compute(_fresh_snapshot())


# ---------------------------------------------------------------------------
# Stabilité transport → modèle pydantic → transport (ECHOS-027 golden)
# ---------------------------------------------------------------------------


def test_cognitive_fields_survive_ingestion_roundtrip():
    """Le snapshot U2 (traits/beliefs/goals/trust/memoryCount) se reflète."""
    raw = _load("world_snapshot_u2.json")

    parsed = parse_message(json.dumps(raw))

    assert isinstance(parsed, WorldSnapshot)
    assert parsed.model_dump(mode="json", by_alias=True, exclude_none=True) == raw

    agent = parsed.agents[0]
    assert agent.traits == {"courage": 0.6, "caution": 0.4}
    assert agent.beliefs[0].value == "true"
    assert agent.goals[0].age == 10
    assert agent.trust[0].peer_id == "B"
    assert agent.memory_count == 8


@pytest.mark.parametrize(
    "engine",
    [engine for engine in ENGINES if engine.ENGINE_NAME in _golden()],
    ids=lambda engine: engine.ENGINE_NAME,
)
def test_transport_to_model_snake_case_is_stable(engine):
    """Résultat identique que le dict vienne du transport ou du modèle."""
    raw = _load("snapshot_analysis.json")
    parsed = parse_message(json.dumps(_load("world_snapshot_u2.json")))

    from_transport = engine.compute(raw)
    # Modèle → transport : seuls les champs modélisés transitent (moteurs
    # instantanés) ; les moteurs fenêtrés restent sur le dict de contexte.
    if engine.ENGINE_NAME in {
        "CognitiveDiversityMetrics",
        "SocialComplexityMetrics",
        "GoalConvergenceMetrics",
    }:
        reflected = engine.compute(
            parsed.model_dump(mode="json", by_alias=True, exclude_none=True)
        )
        assert reflected == from_transport


# ---------------------------------------------------------------------------
# Vérification manuelle du scénario (snapshot_analysis.json) — graine stable
# ---------------------------------------------------------------------------

_RICH = _fresh_snapshot()
_RICH["tick"] = 10  # tableau fixe de la fixture (identique au fichier)


def test_cognitive_diversity_hand_computed_values():
    result = _cognitive()

    assert result["BeliefDiversity"] == _APPROX(
        1.9219280948873623, abs=1e-9
    )  # H sur 4 faits (2× true, 2× unique)
    assert result["BeliefDisagreement"] == _APPROX(0.41666666666666663, abs=1e-9)
    assert result["BeliefConfidenceVariance"] == _APPROX(0.02, abs=1e-9)
    assert result["GoalDiversity"] == _APPROX(1.584962500721156, abs=1e-9)
    assert result["GoalConvergence"] == _APPROX(1 / 3, abs=1e-9)
    assert result["DecisionDiversity"] == 1.0  # 3 actions distinctes / 3 agents
    assert result["IntentionStability"] == 7.0  # Âges moyens : (10 + 4) / 2
    assert result["TraitExpressionDiversity"] == _APPROX(0.01277777777777778, abs=1e-12)


def test_information_propagation_hand_computed_values():
    result = _information()

    # 1 message au tick courant (10) sur 3 entités ; 3 sources → centralité 1
    assert result["MessageVolume"] == _APPROX(1 / 3, abs=1e-9)
    assert result["InformationDiffusionSpeed"] == 10.0  # seuil 80 % atteint à t=10
    assert result["RumorAccuracyDegradation"] == _APPROX(0.13, abs=1e-9)
    assert result["MaxMessageHops"] == 2.0
    assert result["NetworkCentrality"] == 1.0


def test_social_complexity_hand_computed_values():
    result = _social()

    assert result["AverageTrustLevel"] == _APPROX(0.5666666666666667, abs=1e-9)
    assert result["TrustVariance"] == _APPROX(0.01555555555555555, abs=1e-9)
    assert result["NetworkDensity"] == _APPROX(1 / 3, abs=1e-9)  # 2 arêtes / 6 paires
    assert result["ClusteringCoefficient"] == 0.0  # pas de triangle
    assert result["AverageCentrality"] == _APPROX(0.5, abs=1e-9)
    assert result["NumberOfCommunities"] == 1  # groupe plein connecté (A-B, A-C)
    assert result["CommunityStability"] == 0.0  # aucun historique de communautés


def test_goal_convergence_hand_computed_values():
    result = _convergence()

    assert result["GlobalGoalAlignment"] == _APPROX(1 / 3, abs=1e-9)
    assert result["GoalDiversity"] == _APPROX(1.584962500721156, abs=1e-9)
    assert result["CooperationPotential"] == _APPROX(1 / 3, abs=1e-9)  # Σ p², 3 types
    assert result["GoalTypeCounts"] == {"Idle": 1, "SeekFood": 1, "SeekWater": 1}


def test_feedback_loop_hand_computed_values():
    result = _loops()

    # 4 boucles distinctes : A/SeekFood (9×), B/SeekWater (9×), C/Idle (7×), C/Sleep (3×)
    assert result["IdentifiedLoops"] == 4
    assert result["LoopStrength"] == _APPROX(0.7, abs=1e-9)
    assert result["SystemStability"] == _APPROX(1 / 3, abs=1e-9)
    assert result["CriticalLoops"] == 4  # toutes amplifiées > 1,5×
    assert result["LoopTypes"] == {"positive": 2, "negative": 2}


def test_resource_sustainability_hand_computed_values():
    result = _resources()

    assert result["ResourceToConsumptionRatio"] == _APPROX(2.5, abs=1e-9)
    assert result["CriticalityPoints"] == 0.0  # aucune réserve sous 20 %
    assert result["RecoveryTime"] == 2.0  # 2 chutes rétablies en 2 ticks en moyenne


def test_group_dynamics_hand_computed_values():
    result = _groups()

    assert result["ActiveGroups"] == 1.0
    assert result["AverageGroupSize"] == 3.0
    assert result["AverageGroupLifetime"] == 15.0
    assert result["GroupFormationRate"] == _APPROX(166.66666666666666, abs=1e-9)  # 1/6 ticks × 1000
    assert result["GroupDissolutionRate"] == _APPROX(166.66666666666666, abs=1e-9)
    assert result["GroupObjectiveSuccessRate"] == 1.0  # dissolution réussie
    assert result["MemberTurnoverRate"] == _APPROX(55.55555555555555, abs=1e-9)


# ---------------------------------------------------------------------------
# Comportement aux limites
# ---------------------------------------------------------------------------


def test_single_belief_has_zero_diversity():
    from echos.analysis import cognitive_diversity

    snapshot = _fresh_snapshot()
    snapshot["agents"] = [
        {
            "id": "A",
            "position": {"x": 0.0, "y": 0.0},
            "energy": 50,
            "hunger": 30,
            "thirst": 20,
            "beliefs": [
                {
                    "subject": "water",
                    "predicate": "safe",
                    "value": "true",
                    "confidence": 0.9,
                }
            ],
        }
    ]
    snapshot["aliveCount"] = 1

    result = cognitive_diversity.compute(snapshot)

    assert result["BeliefDiversity"] == 0.0  # un seul fait → H = 0
    assert result["GoalDiversity"] == 0.0  # un seul objectif (Idle)


def test_no_trust_graph_isolates_every_agent():
    from echos.analysis import group_dynamics, social_complexity

    snapshot = _fresh_snapshot()
    for agent in snapshot["agents"]:
        agent["trust"] = []
    snapshot["groupEvents"] = []
    snapshot["history"] = []

    social = social_complexity.compute(snapshot)
    groups = group_dynamics.compute(snapshot)

    assert social["NetworkDensity"] == 0.0
    assert social["NumberOfCommunities"] == 3  # chaque agent = sa propre île
    assert groups["ActiveGroups"] == 3.0
    assert groups["AverageGroupSize"] == 1.0


def test_feedback_loop_without_history_is_neutral():
    from echos.analysis import feedback_loop_detector

    result = feedback_loop_detector.compute({})

    assert result["IdentifiedLoops"] == 0
    assert result["LoopStrength"] == 0.0
    assert result["SystemStability"] == 0.0  # aucune décision → neutre
    assert result["LoopTypes"] == {"positive": 0, "negative": 0}


# ---------------------------------------------------------------------------
# Golden files de référence (ECHOS-027)
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("engine", ENGINES, ids=lambda engine: engine.ENGINE_NAME)
def test_compute_matches_golden_files(engine):
    golden = _golden()[engine.ENGINE_NAME]

    result = engine.compute(_fresh_snapshot())

    assert set(result) == set(golden)
    for metric, expected in golden.items():
        if isinstance(expected, dict):
            assert result[metric] == expected
        elif isinstance(expected, int):
            assert result[metric] == expected
        else:
            assert result[metric] == _APPROX(expected, rel=1e-9, abs=1e-9)


def test_golden_metrics_are_reproducible_across_runs():
    """Deux exécutions fraîches → golden identique (ECHOS-027 J2)."""
    first = {engine.ENGINE_NAME: engine.compute(_fresh_snapshot()) for engine in ENGINES}
    random.seed(7)  # le calcul ne doit pas dépendre de l'état global
    second = {engine.ENGINE_NAME: engine.compute(_fresh_snapshot()) for engine in ENGINES}

    assert first == second
    assert first == _golden()


# ---------------------------------------------------------------------------
# Helpers (noms explicites pour lisibilité des échecs)
# ---------------------------------------------------------------------------


def _cognitive():
    from echos.analysis import cognitive_diversity

    return cognitive_diversity.compute(_fresh_snapshot())


def _information():
    from echos.analysis import information_propagation

    return information_propagation.compute(_fresh_snapshot())


def _social():
    from echos.analysis import social_complexity

    return social_complexity.compute(_fresh_snapshot())


def _convergence():
    from echos.analysis import goal_convergence

    return goal_convergence.compute(_fresh_snapshot())


def _loops():
    from echos.analysis import feedback_loop_detector

    return feedback_loop_detector.compute(_fresh_snapshot())


def _resources():
    from echos.analysis import resource_sustainability

    return resource_sustainability.compute(_fresh_snapshot())


def _groups():
    from echos.analysis import group_dynamics

    return group_dynamics.compute(_fresh_snapshot())
