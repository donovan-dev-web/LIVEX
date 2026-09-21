import pytest

from echos.analysis import ENGINES, known_engines


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


@pytest.mark.parametrize("engine", ENGINES, ids=lambda engine: engine.ENGINE_NAME)
def test_engine_compute_is_deferred_to_u1(engine):
    with pytest.raises(NotImplementedError, match=engine.ENGINE_NAME):
        engine.compute({})
