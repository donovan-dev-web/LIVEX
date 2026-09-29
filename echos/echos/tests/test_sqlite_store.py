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
            "calibration_reports",
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


def test_tick_bundle_commits_all_outputs_and_bumps_once(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        before = store.ingest_version
        written = store.append_tick_bundle(
            _record(),
            {"Engine": {"score": 1.5}},
            {"agents": [{"id": "A"}]},
            [("message_sent", "A", "B", None, None, None)],
            [],
        )

        assert written == 1
        assert store.ingest_version == before + 1
        assert store.count_ticks("run-7") == 1
        assert store.metrics_all("run-7") == [(1, "Engine", "score", 1.5)]
        assert store.latest_context("run-7", "agents") == (1, [{"id": "A"}])
        assert len(store.events("run-7")) == 1


def test_tick_bundle_rolls_back_when_one_output_fails(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        with pytest.raises(KeyError):
            store.append_tick_bundle(
                _record(),
                {"Engine": {"score": 1.5}},
                {"agents": []},
                [],
                [{"agent_id": "A"}],
            )

        assert store.count_ticks("run-7") == 0
        assert store.metrics_all("run-7") == []
        assert store.latest_context("run-7", "agents") is None


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


def test_large_tick_context_is_compressed_without_changing_api(tmp_path):
    agents = [
        {
            "id": str(index),
            "beliefs": [
                {"subject": "world", "predicate": "state", "value": "stable"}
            ]
            * 100,
        }
        for index in range(20)
    ]
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        store.append_tick_context("run-7", 1, "agents", agents)

        raw = store._conn.execute(
            "SELECT payload FROM tick_contexts WHERE run_id = 'run-7'"
        ).fetchone()[0]
        assert raw.startswith("z:")
        assert store.latest_context("run-7", "agents") == (1, agents)


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


def test_runs_handles_a_run_without_tick(tmp_path):
    """Un run enregistré mais sans tick ne doit pas casser la lecture.

    Régression : ``runs()`` convertit ``first_tick``/``last_tick`` avec
    ``int()``, ce qui levait ``TypeError: int() argument ... 'NoneType'`` sur
    une base où un run était créé puis lu avant toute ingestion.
    """
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")

        runs = store.runs()

        assert [run["run_id"] for run in runs] == ["run-7"]
        assert runs[0]["first_tick"] is None
        assert runs[0]["last_tick"] is None

        # Le run reste lisible après ingestion.
        store.append_tick(_record(1))
        assert store.runs()[0]["last_tick"] == 1


def test_metric_names_spans_the_whole_run(tmp_path):
    """``metric_names`` découvre la nommenclature de tout le run.

    ``latest_metrics`` ne voit que le dernier tick : une métrique disparue
    depuis (aucun événement à ce tick) était donc invisible alors que sa série
    existait.
    """
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        store.append_tick_metrics("run-7", 1, {"E": {"A": 1.0, "B": 2.0}})
        store.append_tick_metrics("run-7", 2, {"E": {"A": 3.0}})

        assert store.metric_names("run-7") == [("E", "A"), ("E", "B")]
        assert store.metric_names("run-7", metric="B") == [("E", "B")]
        assert store.metric_names("run-7", engine="E", metric="A") == [("E", "A")]
        assert store.metric_names("run-7", engine="Ghost") == []
        # Le dernier instant, lui, ne porte plus que A.
        assert set(store.latest_metrics("run-7")["E"]) == {"A"}


def test_event_records_and_events_by_type_are_deterministic(tmp_path):
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        store.record_run("run-7", "0.1.0", seed="7")
        for tick in (2, 1):
            store.append_event(
                "run-7", tick, "decision_made", agent_id="A", action="Eat",
                value='{"intention":"Eat"}',
            )
        store.append_event("run-7", 3, "message_received", agent_id="B")

        records = store.event_records("run-7")
        assert [record["tick"] for record in records] == [1, 2, 3]
        assert records[0] == {
            "tick": 1,
            "type": "decision_made",
            "agent_id": "A",
            "target_id": None,
            "action": "Eat",
            "cause": None,
            "value": '{"intention":"Eat"}',
        }

        decided = store.events_by_type("run-7", "decision_made")
        assert [record["tick"] for record in decided] == [1, 2]
        # La borne ``max_tick`` évite de recharger tout le journal.
        bounded = store.events_by_type("run-7", "decision_made", max_tick=1)
        assert [record["tick"] for record in bounded] == [1]
        assert store.events_by_type("run-7", "ghost") == []


_PRE_V5_TICK_METRICS = """
CREATE TABLE tick_metrics (
    run_id TEXT NOT NULL,
    tick INTEGER NOT NULL CHECK (tick >= 0),
    engine TEXT NOT NULL,
    metric TEXT NOT NULL,
    value REAL NOT NULL,
    PRIMARY KEY (run_id, tick, engine, metric)
);
"""


def test_a_database_written_before_v5_is_migrated_without_losing_rows(tmp_path):
    """Une base existante doit survivre au passage en v5.

    ``CREATE TABLE IF NOT EXISTS`` ne touche pas une table déjà présente : sans
    migration explicite, rouvrir une base v4 levait ``no such column: measured``
    à la première écriture de métrique, et une base de production becomes
    inutilisable au déploiement de la version qui ajoute la provenance.
    """
    path = tmp_path / "avant-v5.db"
    legacy = sqlite3.connect(path)
    legacy.executescript(
        "CREATE TABLE runs ("
        " run_id TEXT PRIMARY KEY, version TEXT NOT NULL, seed TEXT, "
        " started_at TEXT, last_tick INTEGER, last_ingest_version INTEGER);"
        "INSERT INTO runs VALUES ('run-avant', '0.1.0', '7', '2026-01-01', 2, 1);"
        + _PRE_V5_TICK_METRICS
        + "INSERT INTO tick_metrics VALUES ('run-avant', 1, 'GroupDynamics', "
        "'GroupFormationRate', 0.5);"
        "INSERT INTO tick_metrics VALUES ('run-avant', 1, 'EmergenceIndicators', "
        "'RecoveryTime', 0.0);"
    )
    legacy.commit()
    legacy.close()

    with AnalyticsStore(path) as store:
        assert store.schema_tables() >= {"tick_metrics", "runs"}
        version_row = store._conn.execute(
            "SELECT value FROM _meta WHERE key = 'schema_version'"
        ).fetchone()
        assert version_row and version_row[0] == SCHEMA_VERSION

        # Les lignes d'origine sont intactes...
        assert store.latest_metrics("run-avant") == {
            "GroupDynamics": {"GroupFormationRate": 0.5},
            "EmergenceIndicators": {"RecoveryTime": 0.0},
        }
        # ...et une valeur 0.0 pré-existante est lue comme mesurée : la migration
        # ne réécrit pas l'interprétation des données déjà enregistrées.
        assert store.latest_measured("run-avant") == {
            "GroupDynamics": {"GroupFormationRate": True},
            "EmergenceIndicators": {"RecoveryTime": True},
        }
        assert [row["run_id"] for row in store.runs()] == ["run-avant"]

        # Écrire après migration : c'est le cas qui levait ``no such column``.
        store.append_tick_metrics(
            "run-avant",
            2,
            {"EmergenceIndicators": {"EmergenceScore": 0.75}},
            measured={"EmergenceIndicators": {"EmergenceScore": False}},
        )
        assert store.latest_measured("run-avant")["EmergenceIndicators"] == {
            "EmergenceScore": False,
        }


def test_migrated_column_rejects_a_non_boolean_measured_value(tmp_path):
    path = tmp_path / "contrainte.db"
    sqlite3.connect(path).executescript(
        "CREATE TABLE runs ("
        " run_id TEXT PRIMARY KEY, version TEXT NOT NULL, seed TEXT, "
        " started_at TEXT, last_tick INTEGER, last_ingest_version INTEGER);"
        "INSERT INTO runs VALUES ('run-x', '0.1.0', '7', '2026-01-01', 1, 1);"
        + _PRE_V5_TICK_METRICS
    ).connection.commit()

    with AnalyticsStore(path) as store:
        with pytest.raises(sqlite3.IntegrityError):
            store._conn.execute(
                "INSERT INTO tick_metrics VALUES ('run-x', 1, 'E', 'M', 1.0, 2)"
            )
        store._conn.rollback()
