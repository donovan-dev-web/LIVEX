"""Schéma SQLite d'analyse ECHOS (ECHOS-012).

Base de données **distincte** des tables SYNE : les tables
``runs``/``tick_summaries``/``events_log`` appartiennent à la couche
d'analyse ECHOS (exports, agrégations, résumé par tick) et référencent le
run SYNE via ``run_id`` (``run-<seed>``).
"""

from __future__ import annotations

import sqlite3
from pathlib import Path

from echos.storage.aggregation import TickRecord

SCHEMA_VERSION = "1"
"""Version du schéma — toute migration doit la bump + documenter (CHANGELOG)."""

_DDL = """
CREATE TABLE IF NOT EXISTS _meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS runs (
    run_id TEXT PRIMARY KEY,
    version TEXT NOT NULL,
    seed TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS tick_summaries (
    run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    tick INTEGER NOT NULL CHECK (tick >= 0),
    simulated_time_minutes INTEGER NOT NULL CHECK (simulated_time_minutes >= 0),
    alive_count INTEGER NOT NULL CHECK (alive_count >= 0),
    agent_count INTEGER NOT NULL CHECK (agent_count >= 0),
    mean_energy REAL NOT NULL,
    mean_hunger REAL NOT NULL,
    mean_thirst REAL NOT NULL,
    mean_fatigue REAL NOT NULL,
    decision_count INTEGER NOT NULL CHECK (decision_count >= 0),
    PRIMARY KEY (run_id, tick)
);

CREATE TABLE IF NOT EXISTS events_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    tick INTEGER NOT NULL CHECK (tick >= 0),
    type TEXT NOT NULL,
    agent_id TEXT,
    target_id TEXT,
    action TEXT,
    cause TEXT,
    value TEXT
);

CREATE INDEX IF NOT EXISTS idx_tick_summaries_run ON tick_summaries(run_id);
CREATE INDEX IF NOT EXISTS idx_events_run_tick ON events_log(run_id, tick);
"""


class AnalyticsStore:
    """Stockage SQLite d'analyse (1 connexion, autocommit, thread-local)."""

    def __init__(self, path: str | Path, initialize: bool = True) -> None:
        self.path = str(path)
        self._conn = sqlite3.connect(self.path)
        self._conn.execute("PRAGMA foreign_keys = ON")
        if initialize:
            self.initialize()

    def initialize(self) -> None:
        self._conn.executescript(_DDL)
        self._conn.execute(
            "INSERT OR REPLACE INTO _meta (key, value) VALUES ('schema_version', ?)",
            (SCHEMA_VERSION,),
        )
        self._conn.commit()

    def record_run(self, run_id: str, version: str, seed: str | None = None) -> None:
        self._conn.execute(
            "INSERT OR IGNORE INTO runs (run_id, version, seed) VALUES (?, ?, ?)",
            (run_id, version, seed or ""),
        )
        self._conn.commit()

    def append_tick(self, record: TickRecord) -> None:
        self._conn.execute(
            """
            INSERT OR REPLACE INTO tick_summaries (
                run_id, tick, simulated_time_minutes, alive_count,
                agent_count, mean_energy, mean_hunger, mean_thirst,
                mean_fatigue, decision_count
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            record.to_row(),
        )
        self._conn.commit()

    def append_event(
        self,
        run_id: str,
        tick: int,
        event_type: str,
        *,
        agent_id: str | None = None,
        target_id: str | None = None,
        action: str | None = None,
        cause: str | None = None,
        value: str | None = None,
    ) -> None:
        self._conn.execute(
            """
            INSERT INTO events_log (
                run_id, tick, type, agent_id, target_id, action, cause, value
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (run_id, tick, event_type, agent_id, target_id, action, cause, value),
        )
        self._conn.commit()

    def count_ticks(self, run_id: str) -> int:
        row = self._conn.execute(
            "SELECT COUNT(*) FROM tick_summaries WHERE run_id = ?", (run_id,)
        ).fetchone()
        return int(row[0]) if row else 0

    def tick_summaries(self, run_id: str) -> list[tuple]:
        return list(
            self._conn.execute(
                """
                SELECT run_id, tick, simulated_time_minutes, alive_count,
                       agent_count, mean_energy, mean_hunger, mean_thirst,
                       mean_fatigue, decision_count
                FROM tick_summaries
                WHERE run_id = ?
                ORDER BY tick
                """,
                (run_id,),
            ).fetchall()
        )

    def events(self, run_id: str) -> list[tuple]:
        return list(
            self._conn.execute(
                """
                SELECT tick, type, agent_id, action, cause, value
                FROM events_log
                WHERE run_id = ?
                ORDER BY tick, id
                """,
                (run_id,),
            ).fetchall()
        )

    def schema_tables(self) -> set[str]:
        rows = self._conn.execute(
            "SELECT name FROM sqlite_master WHERE type = 'table'"
        ).fetchall()
        return {row[0] for row in rows if row[0] != "sqlite_sequence"}

    def close(self) -> None:
        self._conn.close()

    def __enter__(self) -> "AnalyticsStore":
        return self

    def __exit__(self, *exc: object) -> None:
        self.close()
