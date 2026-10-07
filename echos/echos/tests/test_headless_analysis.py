import base64
import json

import pytest
from fastapi.testclient import TestClient

from echos.api.app import create_app
from echos.storage.aggregation import TickRecord
from echos.storage.sqlite import AnalyticsStore


def _store(path, run_ids=("run-1", "run-2")):
    store = AnalyticsStore(path)
    for index, run_id in enumerate(run_ids, start=1):
        store.record_run(run_id, "0.1.0", seed=str(index))
        store.append_tick(
            TickRecord(
                run_id=run_id,
                version="0.1.0",
                tick=1,
                simulated_time_minutes=1,
                alive_count=2,
                agent_count=2,
                mean_energy=4.0 + index,
                mean_hunger=3.0,
                mean_thirst=2.0,
                mean_fatigue=1.0,
                decision_count=1,
            )
        )
        store.append_tick_metrics(
            run_id,
            1,
            {
                "EmergenceIndicators": {
                    "EmergenceScore": index / 10,
                    "Label": "not a numeric metric",
                }
            },
        )
        store.append_tick_context(
            run_id,
            1,
            "phenomena",
            {
                "detected": [
                    {
                        "identifier": "CommunityFormation",
                        "label": "Community formation",
                        "description": "A persisted observation.",
                        "signals": [],
                    }
                ],
                "disclaimer": "observe-only",
            },
        )
    return store


def _decode(file):
    return base64.b64decode(file["content"]).decode("utf-8")


def test_headless_run_returns_deterministic_retrievable_files(tmp_path):
    store = _store(tmp_path / "analytics.db", ("run-1",))
    run_path = tmp_path / "run-1"
    run_path.mkdir()
    client = TestClient(create_app(store=store))
    payload = {
        "experimentId": "experiment-1",
        "runId": "run-1",
        "runPath": str(run_path),
    }

    first = client.post("/analysis/run", json=payload)
    second = client.post("/analysis/run", json=payload)

    assert first.status_code == second.status_code == 200
    assert first.json() == second.json()
    files = first.json()["files"]
    assert [file["name"] for file in files] == ["run-run-1.json", "run-run-1.md"]
    report = json.loads(_decode(files[0]))
    assert report["run"]["runId"] == "run-1"
    assert report["experimentId"] == "experiment-1"
    assert "# ECHOS run `run-1`" in _decode(files[1])
    store.close()


def test_experiment_analysis_uses_only_manifest_runs_and_report_is_stable(tmp_path):
    store = _store(tmp_path / "analytics.db")
    experiment_path = tmp_path / "experiment-1"
    experiment_path.mkdir()
    (experiment_path / "experiment.json").write_text(
        json.dumps({"experimentId": "experiment-1", "runIds": ["run-2", "run-1"]}),
        encoding="utf-8",
    )
    client = TestClient(create_app(store=store))
    payload = {"experimentId": "experiment-1", "experimentPath": str(experiment_path)}

    first = client.post("/analysis/experiment", json=payload)
    second = client.post("/analysis/experiment", json=payload)
    assert first.status_code == second.status_code == 200
    assert first.json() == second.json()
    [file] = first.json()["files"]
    assert file["name"] == "experiment-experiment-1.json"
    result = json.loads(_decode(file))
    assert result["experiment"]["runIds"] == ["run-1", "run-2"]
    assert result["experiment"]["runCount"] == 2
    aggregate = result["metrics"]["latestAcrossRuns"]["EmergenceIndicators"]["EmergenceScore"]
    assert aggregate["count"] == 2
    assert aggregate["mean"] == pytest.approx(0.15)
    assert aggregate["min"] == 0.1
    assert aggregate["max"] == 0.2
    assert aggregate["values"] == [
        {"runId": "run-1", "value": 0.1},
        {"runId": "run-2", "value": 0.2},
    ]
    assert result["detectedPhenomena"][0]["runCount"] == 2

    report = client.post("/analysis/report", json=payload)
    report_again = client.post("/analysis/report", json=payload)
    assert report.status_code == report_again.status_code == 200
    assert report.json() == report_again.json()
    assert "# ECHOS experiment `experiment-1`" in report.json()["report"]
    assert "Community formation" in report.json()["report"]
    store.close()


def test_headless_analysis_reports_unavailable_inputs_and_database(tmp_path):
    request = {
        "experimentId": "experiment-1",
        "runId": "run-1",
        "runPath": str(tmp_path / "missing-run"),
    }
    assert TestClient(create_app()).post("/analysis/run", json=request).status_code == 503

    store = _store(tmp_path / "analytics.db", ("run-1",))
    client = TestClient(create_app(store=store))
    assert client.post("/analysis/run", json=request).status_code == 404
    request["runPath"] = str(tmp_path)
    request["runId"] = "unknown-run"
    assert client.post("/analysis/run", json=request).status_code == 404
    store.record_run("empty-run", "0.1.0", seed="empty")
    assert client.post(
        "/analysis/run",
        json={**request, "runId": "empty-run"},
    ).status_code == 409
    assert client.post(
        "/analysis/run",
        json={**request, "runId": "run/unsafe"},
    ).status_code == 422
    store.close()


def test_experiment_manifest_and_database_fail_explicitly(tmp_path):
    store = _store(tmp_path / "analytics.db", ("run-1",))
    client = TestClient(create_app(store=store))
    experiment_path = tmp_path / "experiment"
    experiment_path.mkdir()
    payload = {"experimentId": "experiment-1", "experimentPath": str(experiment_path)}

    assert client.post("/analysis/experiment", json=payload).status_code == 422
    (experiment_path / "experiment.json").write_text(
        json.dumps({"experimentId": "experiment-1", "runIds": ["missing-run"]}),
        encoding="utf-8",
    )
    unknown = client.post("/analysis/experiment", json=payload)
    assert unknown.status_code == 404
    assert "missing-run" in unknown.json()["detail"]
    assert client.post(
        "/analysis/report",
        json={**payload, "experimentId": "different-experiment"},
    ).status_code == 422
    store.close()


def test_readiness_requires_a_queryable_analytics_store(tmp_path):
    store = _store(tmp_path / "analytics.db", ("run-1",))
    client = TestClient(create_app(store=store))
    assert client.get("/health/ready").status_code == 200

    store.close()

    unavailable = client.get("/health/ready")
    assert unavailable.status_code == 503
    assert "indisponible" in unavailable.json()["detail"]
