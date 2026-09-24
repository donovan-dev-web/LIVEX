from echos.analysis.calibration import build_calibration_report


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


def test_report_without_ticks_is_not_created():
    assert build_calibration_report("run-empty", [], [], []) is None
