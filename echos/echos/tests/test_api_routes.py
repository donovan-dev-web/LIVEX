"""API REST ECHOS (ECHOS-040→045, jalon ph4, preuve J5).

Contrat API_REST.md : liste de runs, métriques + séries (``?every=N`` et
cache), export reproductible (JSON/CSV), croyances/relations par entité,
groupes et phénomènes émergents, déterminisme des réponses, 404/400/422/503.
Les stores sont peuplés par les vrais moteurs (``compute_all``) sur la fixture
``snapshot_analysis.json`` — aucune écriture dans le monde observé.
"""

import json
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from echos.analysis import compute_all
from echos.api.app import create_app
from echos.api.series import SeriesCache
from echos.storage.aggregation import TickRecord
from echos.storage.pipeline import _groups_of
from echos.storage.sqlite import AnalyticsStore

FIXTURES = Path(__file__).resolve().parent / "fixtures"


def _snapshot(tick: int, run_id: str) -> dict:
    data = json.loads((FIXTURES / "snapshot_analysis.json").read_text())
    data["tick"] = tick
    data["runId"] = run_id
    return data


def _populate(db: AnalyticsStore, run_id: str, ticks: int = 3) -> None:
    """Peuple un store avec les vrais moteurs (fixture riche en croyances)."""
    db.record_run(run_id, "0.1.0", seed=run_id.removeprefix("run-"))
    for tick in range(1, ticks + 1):
        snap = _snapshot(tick, run_id)
        agents = snap.get("agents") or []
        events = snap.get("events") or []
        record = TickRecord(
            run_id=run_id,
            version="0.1.0",
            tick=tick,
            simulated_time_minutes=tick,
            alive_count=int(snap.get("aliveCount") or len(agents)),
            agent_count=len(agents),
            mean_energy=40.0,
            mean_hunger=30.0,
            mean_thirst=20.0,
            mean_fatigue=5.0,
            decision_count=sum(
                1 for event in events if event.get("type") == "decision_made"
            ),
        )
        db.append_tick(record)
        metrics = compute_all(snap)
        db.append_tick_metrics(run_id, tick, metrics)
        db.append_tick_context(run_id, tick, "agents", agents)
        db.append_tick_context(run_id, tick, "groups", _groups_of(agents))
        emergence = metrics.get("EmergenceIndicators") or {}
        db.append_tick_context(
            run_id,
            tick,
            "phenomena",
            {
                "detected": emergence.get("DetectedPhenomena", []),
                "disclaimer": emergence.get("Disclaimer", ""),
            },
        )


def _client(db: AnalyticsStore | None = None) -> TestClient:
    return TestClient(create_app(db))


def test_list_runs_returns_metadata(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=3)
    body = _client(db).get("/api/runs").json()

    assert len(body["runs"]) == 1
    run = body["runs"][0]
    assert run["run_id"] == "run-7"
    assert run["version"] == "0.1.0"
    assert run["seed"] == "7"
    assert run["ticks_count"] == 3
    assert run["first_tick"] == 1
    assert run["last_tick"] == 3


def test_run_full_returns_latest_metrics_and_phenomena(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    client = _client(db)

    body = client.get("/api/runs/run-7").json()
    assert body["run_id"] == "run-7"
    assert "EmergenceIndicators" in body["metrics"]
    assert body["metrics"]["EmergenceIndicators"]["EmergenceScore"] >= 0.0
    assert body["phenomena"]["detected"]  # la fixture déclenche des phénomènes
    assert body["phenomena"]["disclaimer"]


def test_run_full_unknown_run_returns_404(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    assert _client(db).get("/api/runs/ghost").status_code == 404


def test_metrics_series_contract_and_downsampling(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=5)
    client = _client(db)

    body = client.get("/api/runs/run-7/metrics").json()
    assert body["run_id"] == "run-7"
    assert body["ticks"] == [1, 2, 3, 4, 5]
    assert body["every"] == 1
    score = body["values"]["EmergenceIndicators"]["EmergenceScore"]
    assert len(score) == 5
    assert body["latest"]["EmergenceIndicators"]

    sampled = client.get("/api/runs/run-7/metrics", params={"every": 2}).json()
    assert sampled["ticks"] == [1, 3, 5]
    assert len(sampled["values"]["EmergenceIndicators"]["EmergenceScore"]) == 3

    filtered = client.get(
        "/api/runs/run-7/metrics",
        params={"engine": "EmergenceIndicators", "metric": "SystemComplexity"},
    ).json()
    assert list(filtered["values"]) == ["EmergenceIndicators"]
    assert list(filtered["values"]["EmergenceIndicators"]) == ["SystemComplexity"]


def test_metrics_invalid_every_is_rejected(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    response = _client(db).get("/api/runs/run-7/metrics", params={"every": 0})
    assert response.status_code == 422


def test_export_json_is_deterministic_and_sorted(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    client = _client(db)

    first = client.get("/api/runs/run-7/export").json()
    second = client.get("/api/runs/run-7/export").json()
    assert first == second  # reproductible : aucune dépendance temporelle
    assert first["run_id"] == "run-7"
    assert first["format"] == "json"
    rows = first["rows"]
    assert rows == sorted(
        rows, key=lambda row: (row["tick"], row["engine"], row["metric"])
    )
    assert rows and rows[0] == second["rows"][0]


def test_export_csv_has_header_and_deterministic_rows(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    client = _client(db)

    body = client.get("/api/runs/run-7/export", params={"format": "csv"}).json()
    lines = body["body"].split("\r\n")
    assert lines[0] == "run_id,tick,engine,metric,value"
    assert lines[1].startswith("run-7,1,")
    assert lines[-1] == ""  # fin RFC 4180
    assert body["content_type"] == "text/csv"

    bogus = client.get("/api/runs/run-7/export", params={"format": "xml"}).json()
    assert bogus["detail"]  # 400 handlée → détail documenté


def test_beliefs_and_relationships_are_non_intrusive(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    client = _client(db)

    beliefs = client.get("/api/beliefs/A").json()
    assert beliefs["agent_id"] == "A"
    assert beliefs["run_id"] == "run-7"
    assert beliefs["tick"] == 3
    assert beliefs["beliefs"]  # croyances de la fixture

    relationships = client.get("/api/relationships/A").json()
    assert relationships["agent_id"] == "A"
    assert relationships["count"] >= 1
    assert relationships["trust"]

    assert client.get("/api/beliefs/ghost").status_code == 404
    assert client.get("/api/relationships/ghost").status_code == 404


def test_groups_and_phenomena_endpoints(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    client = _client(db)

    groups = client.get("/api/groups").json()
    assert groups["run_id"] == "run-7"
    assert groups["groups"]  # communautés du graphe de confiance
    assert all("members" in group and "size" in group for group in groups["groups"])

    phenomena = client.get("/api/emergent-phenomena").json()
    assert phenomena["run_id"] == "run-7"
    assert phenomena["phenomena"]
    assert phenomena["disclaimer"]


def test_default_run_is_most_recent(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-2", ticks=2)
    _populate(db, "run-1", ticks=1)
    response = _client(db).get("/api/groups").json()

    assert response["run_id"] == "run-2"


def test_unknown_run_resolves_to_404_everywhere(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    client = _client(db)

    assert client.get("/api/runs/ghost/metrics").status_code == 404
    assert client.get("/api/runs/ghost/export").status_code == 404
    assert client.get("/api/beliefs/A", params={"run_id": "ghost"}).status_code == 404
    assert client.get("/api/groups", params={"run_id": "ghost"}).status_code == 404


def test_empty_store_default_run_returns_404(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    client = _client(db)

    assert client.get("/api/groups").status_code == 404
    assert client.get("/api/emergent-phenomena").status_code == 404


def test_data_routes_return_503_without_store(monkeypatch):
    monkeypatch.delenv("ECHOS_ANALYTICS_DB", raising=False)
    client = _client(None)

    assert client.get("/api/runs").status_code == 503
    assert client.get("/api/runs/run-7/metrics").status_code == 503
    assert client.get("/health").status_code == 200  # santé toujours disponible


def test_series_cache_reuses_until_invalidation(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    cache = SeriesCache()

    calls = []

    def loader() -> list:
        calls.append(1)
        return db.metric_series("run-7", "E", "M")

    first = cache.series(db, "run-7", "E", "M", loader=loader)
    second = cache.series(db, "run-7", "E", "M", loader=loader)
    assert first is second
    assert len(calls) == 1  # servie en cache tant que la version ne bouge pas

    db.append_tick_context("run-7", 9, "agents", [])
    cache.series(db, "run-7", "E", "M", loader=loader)
    assert len(calls) == 2  # nouvelle écriture → invalidation → recalcul


def test_series_cache_is_bounded_lru():
    from types import SimpleNamespace

    store = SimpleNamespace(ingest_version=0)
    cache = SeriesCache(capacity=2)
    calls = []

    for key in ("a", "b"):
        cache.series(store, "run", key, "m", loader=lambda key=key: calls.append(key))
    cache.series(store, "run", "c", "m", loader=lambda: calls.append("c"))

    assert len(cache) == 2


def test_series_cache_rejects_zero_capacity():
    with pytest.raises(ValueError):
        SeriesCache(capacity=0)


def test_create_app_reads_analytics_db_env(monkeypatch, tmp_path):
    db_path = tmp_path / "env.db"
    db = AnalyticsStore(db_path)
    _populate(db, "run-7", ticks=1)
    db.close()

    monkeypatch.setenv("ECHOS_ANALYTICS_DB", str(db_path))
    body = _client(None).get("/api/runs").json()
    assert body["runs"][0]["run_id"] == "run-7"
