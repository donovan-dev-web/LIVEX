from echos.analysis.calibration import build_calibration_report


def _rows(outcomes: list[int], energy: list[float] | None = None) -> list[tuple]:
    """Résumés de ticks : (run, tick, t_sim, alive, agents, e, h, t, f, déc)."""
    return [
        (
            "run-1",
            tick + 1,
            tick + 1,
            alive,
            alive,
            (energy or [50.0] * len(outcomes))[tick],
            80.0,
            20.0,
            5.0,
            1,
        )
        for tick, alive in enumerate(outcomes)
    ]


def test_report_is_sorted_complete_and_timestamp_free():
    rows = [
        ("run-1", 2, 2, 4, 2, 40.0, 10.0, 20.0, 5.0, 2),
        ("run-1", 1, 1, 3, 2, 60.0, 30.0, 40.0, 15.0, 2),
    ]
    events = [(1, "world.book_read", "2", None, None, None),
              (1, "decision_made", "1", "Explore", None, None)]
    metrics = [(2, "EngineB", "z", 2.0), (1, "EngineA", "a", 1.0)]

    report = build_calibration_report("run-1", rows, events, metrics)

    assert report == build_calibration_report("run-1", rows, events, metrics)
    assert report["ticks"] == {"count": 2, "first": 1, "last": 2}
    assert report["population"] == {"initial": 2, "final": 2, "minimumAlive": 3}
    assert report["needs"]["energy"] == {"count": 2, "min": 40.0, "mean": 50.0, "max": 60.0}
    assert list(report["metrics"]) == ["EngineA", "EngineB"]
    assert report["events"] == {"decision_made": 1, "world.book_read": 1}
    assert "timestamp" not in report
    # A3 : population vivante → surviving, extinctionTick null.
    assert report["outcome"] == "surviving"
    assert report["extinctionTick"] is None
    # B2 : le bloc viability est présent et déterministe ; ces lignes compactes
    # (10 colonnes, sans mean_food/mean_water) donnent un régime de ressources
    # non disponible (les colonnes 10/11 n'existent pas sur ce tuple).
    assert report["viability"]["resourceRegime"]["food"] is None
    assert report["viability"]["resourceRegime"]["water"] is None


def test_report_without_ticks_is_not_created():
    assert build_calibration_report("run-empty", [], [], []) is None


def test_report_flags_extinction_and_first_extinction_tick():
    """A3 : alive_count = 0 au milieu de série → extinct au premier tick à 0."""
    rows = _rows([2, 2, 0, 0])

    report = build_calibration_report("run-1", rows, [], [])

    assert report["outcome"] == "extinct"
    assert report["extinctionTick"] == 3
    assert report["population"]["final"] == 0


def test_report_energy_slope_detects_slow_death_without_extinction():
    """B2 : pente d'énergie négative sur la fenêtre — le cas du run 1 (mort lente)."""
    falling = _rows([2] * 4, energy=[60.0, 55.0, 50.0, 45.0])
    stable = _rows([2] * 4, energy=[50.0, 50.0, 50.0, 50.0])

    falling_report = build_calibration_report("run-1", falling, [], [])
    stable_report = build_calibration_report("run-1", stable, [], [])

    assert falling_report["viability"]["energySlopePerTick"] < -1.0
    assert stable_report["viability"]["energySlopePerTick"] == 0.0


def test_report_action_shares_when_hungry():
    """B2 : distribution des actions des décision_made sur les ticks faim > 70."""
    rows = _rows([2, 2, 2])
    # Le tick 3 a une faim moyenne sous le seuil : ses décisions sont exclues.
    rows[2] = ("run-1", 3, 3, 2, 2, 50.0, 20.0, 20.0, 5.0, 1)
    events = [
        (1, "decision_made", "1", "Eat", None, None),
        (1, "decision_made", "2", "Explore", None, None),
        (2, "decision_made", "1", "Eat", None, None),
        (2, "decision_made", "3", "Explore", None, None),
        # Tick 3 non affamé : ignoré malgré la présence de décisions.
        (3, "decision_made", "4", "Socialize", None, None),
    ]

    report = build_calibration_report("run-1", rows, events, [])

    assert report["viability"]["actionSharesWhenHungry"] == {
        "Eat": 0.5,
        "Explore": 0.5,
    }
    assert report["viability"]["hungryDecisions"] == 4


def test_report_viability_is_bit_stable():
    rows = _rows([2, 2, 2])
    events = [(1, "decision_made", "1", "Eat", None, None)]

    first = build_calibration_report("run-1", rows, events, [])
    second = build_calibration_report("run-1", rows, events, [])

    assert first == second
