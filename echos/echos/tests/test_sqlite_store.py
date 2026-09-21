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
            "events_log",
            "runs",
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
