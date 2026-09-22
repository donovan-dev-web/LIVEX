"""Schéma SQLite d'analyse (ECHOS-012) : structure **stable** et table
d'analyse **distincte** des tables SYNE, lecture déterministe."""

import sqlite3

import pytest

from echos.storage.aggregation import TickRecord
from echos.storage.sqlite import SCHEMA_VERSION, AnalyticsStore


def _record(tick: int = 1) -> TickRecord:
    return TickRecord(
        run_id="run-7",
        version="0.1.0",
        tick=tick,
        simulated_time_minutes=tick,
        alive_count=2,
        agent_count=2,
        mean_energy=40.0,
        mean_hunger=100.0,
        mean_thirst=100.0,
        mean_fatigue=61.5,
        decision_count=1,
        actions=(("SeekWater", 2),),
    )


def test_schema_is_stable_and_distinct_from_syne(tmp_path):
    path = tmp_path / "analyse.db"
    with AnalyticsStore(path) as store:
        assert store.schema_tables() == {
            "_meta",
            "decision_traces",
            "events_log",
            "runs",
            "tick_contexts",
            "tick_metrics",
            "tick_summaries",
        }
        version_row = store._conn.execute(
            "SELECT value FROM _meta WHERE key = 'schema_version'"
        ).fetchone()
        assert version_row and version_row[0] == SCHEMA_VERSION

    assert path.exists()


def test_tick_summary_roundtrip_is_deterministic(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        store.append_tick(_record(1))
        store.append_tick(_record(2))

        assert store.count_ticks("run-7") == 2
        assert store.tick_summaries("run-7") == [
            _record(1).to_row(),
            _record(2).to_row(),
        ]


def test_append_same_tick_is_idempotent(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        store.append_tick(_record(1))
        store.append_tick(_record(1))

        assert store.count_ticks("run-7") == 1


def test_record_run_is_idempotent(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        store.record_run("run-7", "0.1.0", seed="7")

        rows = store._conn.execute("SELECT COUNT(*) FROM runs").fetchone()
        assert rows[0] == 1


def test_events_log_roundtrip(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        store.append_event(
            "run-7",
            1,
            "decision_made",
            agent_id="1",
            action="SeekWater",
            cause="hunger=100,thirst=100,fatigue=61.5",
            value='{"intention":"SeekWater","utility":61.79}',
        )

        assert store.events("run-7") == [
            (1, "decision_made", "1", "SeekWater",
             "hunger=100,thirst=100,fatigue=61.5",
             '{"intention":"SeekWater","utility":61.79}'),
        ]


def test_foreign_keys_enforced(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        with pytest.raises(sqlite3.IntegrityError):
            store.append_tick(_record())  # sans record_run préalable


def test_tick_metrics_roundtrip_is_deterministic(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        written = store.append_tick_metrics(
            "run-7",
            1,
            {"Engine": {"Alpha": 0.5, "Beta": 1.0, "Composites": ["x", "y"]}},
        )

        # les sorties non numériques sont ignorées (contexte, pas métrique)
        assert written == 2
        assert list(store.metrics_all("run-7")) == [
            (1, "Engine", "Alpha", 0.5),
            (1, "Engine", "Beta", 1.0),
        ]
        assert store.metric_series("run-7", "Engine", "Alpha") == [(1, 0.5)]
        assert store.latest_metrics("run-7") == {
            "Engine": {"Alpha": 0.5, "Beta": 1.0}
        }


def test_tick_metrics_are_replaced_on_resend(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        store.append_tick_metrics("run-7", 1, {"E": {"M": 1.0}})
        store.append_tick_metrics("run-7", 1, {"E": {"M": 2.0}})

        assert store.metric_series("run-7", "E", "M") == [(1, 2.0)]
        # ré-écriture sans gain de données → pas de nouveau tick, version le reste
        assert store.count_ticks("run-7") == 0


def test_tick_contexts_roundtrip_and_latest(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        store.append_tick_context("run-7", 1, "agents", [{"id": "A", "beliefs": []}])
        store.append_tick_context(
            "run-7", 2, "agents", [{"id": "B", "beliefs": [{"subject": "w"}]}]
        )

        assert store.latest_context("run-7", "agents") == (
            2,
            [{"id": "B", "beliefs": [{"subject": "w"}]}],
        )
        assert len(store.observations_for("run-7", "agents")) == 2


def test_ingest_version_bumps_and_drives_cache(tmp_path):
    store = AnalyticsStore(tmp_path / "analyse.db")
    store.record_run("run-7", "0.1.0", seed="7")
    before = store.ingest_version

    store.append_tick(_record(1))
    tick_version = store.ingest_version
    assert tick_version > before

    store.append_tick_metrics("run-7", 1, {"E": {"M": 1.0}})
    assert store.ingest_version > tick_version

    store.append_tick_context("run-7", 1, "agents", [])
    assert store.ingest_version > tick_version
    store.close()


def test_close_then_reopen_keeps_data_and_schema(tmp_path):
    path = tmp_path / "analyse.db"
    store = AnalyticsStore(path)
    store.record_run("run-7", "0.1.0", seed="7")
    store.append_tick(_record())
    tables = store.schema_tables()
    store.close()

    with AnalyticsStore(path) as reopened:
        assert reopened.count_ticks("run-7") == 1
        assert reopened.schema_tables() == tables
