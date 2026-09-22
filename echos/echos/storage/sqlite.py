"""Schéma SQLite d'analyse ECHOS (ECHOS-012, étendu jalon ECHOS ph4).

Base de données **distincte** des tables SYNE : les tables
``runs``/``tick_summaries``/``events_log`` appartiennent à la couche
d'analyse ECHOS (exports, agrégations, résumé par tick) et référencent le
run SYNE via ``run_id`` (``run-<seed>``). Le jalon ph4 (ECHOS-040→044)
ajoute la table ``tick_metrics`` (métriques des 8 moteurs, 1 ligne par
métrique et par tick — lues directement par l'API REST) et ``tick_contexts``
(contexte JSON par tick : agents, groupes, phénomènes — alimente
``/beliefs``, ``/relationships``, ``/groups``, ``/emergent-phenomena``).

Concurrence : la connexion est créée avec ``check_same_thread=False`` (l'API
REST FastAPI sert plusieurs requêtes depuis des threads) et chaque opération
est protégée par un verrou réentrant partagé — les lectures restent
déterministes (ordre stable) quelle que soit l'interleaving.
"""

from __future__ import annotations

import json
import sqlite3
import threading
from pathlib import Path

from echos.storage.aggregation import TickRecord

SCHEMA_VERSION = "3"
"""Version du schéma — toute migration doit la bump + documenter (CHANGELOG).

v3 (jalon ECHOS ph5, ECHOS-051) : table ``decision_traces`` — traces de
décision SYNE consommées (analyse causale, CAUSAL_ANALYSIS.md). Backward
compatible : ``CREATE TABLE IF NOT EXISTS`` étend les bases v2 au prochain
open sans perte de données.
"""

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

CREATE TABLE IF NOT EXISTS tick_metrics (
    run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    tick INTEGER NOT NULL CHECK (tick >= 0),
    engine TEXT NOT NULL,
    metric TEXT NOT NULL,
    value REAL NOT NULL,
    PRIMARY KEY (run_id, tick, engine, metric)
);

CREATE TABLE IF NOT EXISTS tick_contexts (
    run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    tick INTEGER NOT NULL CHECK (tick >= 0),
    context_type TEXT NOT NULL,
    payload TEXT NOT NULL,
    PRIMARY KEY (run_id, tick, context_type)
);

CREATE TABLE IF NOT EXISTS decision_traces (
    run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    tick INTEGER NOT NULL CHECK (tick >= 0),
    agent_id TEXT NOT NULL,
    chosen_action TEXT,
    utility REAL NOT NULL,
    deliberated INTEGER NOT NULL DEFAULT 0,
    interrupted INTEGER NOT NULL DEFAULT 0,
    cause TEXT,
    beliefs_count INTEGER NOT NULL DEFAULT 0,
    goals_count INTEGER NOT NULL DEFAULT 0,
    memory_count INTEGER NOT NULL DEFAULT 0,
    needs TEXT,
    PRIMARY KEY (run_id, tick, agent_id)
);

CREATE INDEX IF NOT EXISTS idx_tick_summaries_run ON tick_summaries(run_id);
CREATE INDEX IF NOT EXISTS idx_events_run_tick ON events_log(run_id, tick);
CREATE INDEX IF NOT EXISTS idx_tick_metrics_run_tick ON tick_metrics(run_id, tick);
CREATE INDEX IF NOT EXISTS idx_tick_contexts_run_tick ON tick_contexts(run_id, tick);
CREATE INDEX IF NOT EXISTS idx_decision_traces_run_tick ON decision_traces(run_id, tick);
"""


class AnalyticsStore:
    """Stockage SQLite d'analyse (1 connexion, autocommit, thread-safe)."""

    def __init__(self, path: str | Path, initialize: bool = True) -> None:
        self.path = str(path)
        self._lock = threading.RLock()
        self._conn = sqlite3.connect(self.path, check_same_thread=False)
        with self._lock:
            self._conn.execute("PRAGMA foreign_keys = ON")
            self._ingest_version = 0
        if initialize:
            self.initialize()

    @property
    def ingest_version(self) -> int:
        """Version logique du contenu — incrémentée à chaque écriture.

        L'API REST s'en sert pour valider le **cache de séries** (ECHOS-044) :
        une série mise en cache n'est réutilisée que si la version n'a pas bougé
        (l'invalidation ne repose donc ni sur le temps ni sur un recalcul).
        """
        with self._lock:
            return self._ingest_version

    def _bump(self) -> None:
        self._ingest_version += 1

    def initialize(self) -> None:
        with self._lock:
            self._conn.executescript(_DDL)
            self._conn.execute(
                "INSERT OR REPLACE INTO _meta (key, value) VALUES ('schema_version', ?)",
                (SCHEMA_VERSION,),
            )
            self._conn.commit()

    def record_run(self, run_id: str, version: str, seed: str | None = None) -> None:
        with self._lock:
            self._conn.execute(
                "INSERT OR IGNORE INTO runs (run_id, version, seed) VALUES (?, ?, ?)",
                (run_id, version, seed or ""),
            )
            self._conn.commit()
            self._bump()

    def append_tick(self, record: TickRecord) -> None:
        with self._lock:
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
            self._bump()

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
        with self._lock:
            self._conn.execute(
                """
                INSERT INTO events_log (
                    run_id, tick, type, agent_id, target_id, action, cause, value
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (run_id, tick, event_type, agent_id, target_id, action, cause, value),
            )
            self._conn.commit()
            self._bump()

    def append_tick_metrics(
        self, run_id: str, tick: int, metrics: dict[str, dict]
    ) -> int:
        """Persiste les métriques calculées d'un tick (ECHOS-040→041).

        Les sorties non numériques des moteurs (``DetectedPhenomena``,
        ``Disclaimer``, compteurs composites...) sont ignorées ici — elles sont
        émissent via :meth:`append_tick_context`. Retourne le nombre de lignes
        écrites (test : couverture du pipeline).
        """
        written = 0
        with self._lock:
            for engine, values in metrics.items():
                for metric, value in values.items():
                    if isinstance(value, bool) or not isinstance(value, (int, float)):
                        continue
                    self._conn.execute(
                        """
                        INSERT INTO tick_metrics (run_id, tick, engine, metric, value)
                        VALUES (?, ?, ?, ?, ?)
                        ON CONFLICT(run_id, tick, engine, metric)
                        DO UPDATE SET value = excluded.value
                        """,
                        (run_id, tick, str(engine), str(metric), float(value)),
                    )
                    written += 1
            self._conn.commit()
            self._bump()
        return written

    def append_tick_context(
        self, run_id: str, tick: int, context_type: str, payload: object
    ) -> None:
        """Persiste un contexte JSON d'un tick (agents, groupes, phénomènes...)."""
        with self._lock:
            self._conn.execute(
                """
                INSERT OR REPLACE INTO tick_contexts (run_id, tick, context_type, payload)
                VALUES (?, ?, ?, ?)
                """,
                (run_id, tick, context_type, _json_dumps(payload)),
            )
            self._conn.commit()
            self._bump()

    def count_ticks(self, run_id: str) -> int:
        with self._lock:
            row = self._conn.execute(
                "SELECT COUNT(*) FROM tick_summaries WHERE run_id = ?", (run_id,)
            ).fetchone()
        return int(row[0]) if row else 0

    def append_decision_trace(self, run_id: str, tick: int, trace: dict) -> None:
        """Persiste la trace de décision d'une entité au tick (ECHOS-051).

        ``trace`` est produit par ``instrumentation.decision_traces.
        build_decision_trace`` (fusion déterministe) ;
        ``needs`` est stocké en JSON compact. Clé ``(run_id, tick, agent_id)``.
        """
        with self._lock:
            self._conn.execute(
                """
                INSERT OR REPLACE INTO decision_traces (
                    run_id, tick, agent_id, chosen_action, utility,
                    deliberated, interrupted, cause,
                    beliefs_count, goals_count, memory_count, needs
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    run_id,
                    int(tick),
                    str(trace["agent_id"]),
                    str(trace["chosen_action"]),
                    float(trace["utility"]),
                    int(bool(trace["deliberated"])),
                    int(bool(trace["interrupted"])),
                    str(trace["cause"]),
                    int(trace["beliefs_count"]),
                    int(trace["goals_count"]),
                    int(trace["memory_count"]),
                    _json_dumps(trace["needs"]),
                ),
            )
            self._conn.commit()
            self._bump()

    def decision_traces(
        self, run_id: str, tick: int | None = None
    ) -> list[dict]:
        """Traces de décision d'un run (tri tick, agent_id) — analyse causale."""
        with self._lock:
            if tick is None:
                rows = self._conn.execute(
                    """
                    SELECT tick, agent_id, chosen_action, utility,
                           deliberated, interrupted, cause,
                           beliefs_count, goals_count, memory_count, needs
                    FROM decision_traces
                    WHERE run_id = ?
                    ORDER BY tick, agent_id
                    """,
                    (run_id,),
                ).fetchall()
            else:
                rows = self._conn.execute(
                    """
                    SELECT tick, agent_id, chosen_action, utility,
                           deliberated, interrupted, cause,
                           beliefs_count, goals_count, memory_count, needs
                    FROM decision_traces
                    WHERE run_id = ? AND tick = ?
                    ORDER BY agent_id
                    """,
                    (run_id, int(tick)),
                ).fetchall()
        return [
            {
                "tick": int(row[0]),
                "agent_id": row[1],
                "chosen_action": row[2],
                "utility": float(row[3]),
                "deliberated": bool(row[4]),
                "interrupted": bool(row[5]),
                "cause": row[6],
                "beliefs_count": int(row[7]),
                "goals_count": int(row[8]),
                "memory_count": int(row[9]),
                "needs": json.loads(row[10]) if row[10] else {},
            }
            for row in rows
        ]

    def latest_tick(self, run_id: str) -> int | None:
        with self._lock:
            row = self._conn.execute(
                "SELECT MAX(tick) FROM tick_summaries WHERE run_id = ?", (run_id,)
            ).fetchone()
        return int(row[0]) if row and row[0] is not None else None

    def tick_summaries(self, run_id: str) -> list[tuple]:
        with self._lock:
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
        with self._lock:
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

    def runs(self) -> list[dict]:
        """Runs enregistrés avec bornes de ticks (ordre déterministe par run_id)."""
        with self._lock:
            rows = self._conn.execute(
                """
                SELECT r.run_id, r.version, r.seed,
                       COUNT(s.tick), MIN(s.tick), MAX(s.tick)
                FROM runs r
                LEFT JOIN tick_summaries s ON s.run_id = r.run_id
                GROUP BY r.run_id
                ORDER BY r.run_id
                """
            ).fetchall()
        return [
            {
                "run_id": row[0],
                "version": row[1],
                "seed": row[2],
                "ticks_count": int(row[3]),
                "first_tick": int(row[4]),
                "last_tick": int(row[5]),
            }
            for row in rows
        ]

    def metrics_all(self, run_id: str) -> list[tuple]:
        """Lignes de métriques complètes du run (tri tick, engine, metric)."""
        with self._lock:
            return list(
                self._conn.execute(
                    """
                    SELECT tick, engine, metric, value FROM tick_metrics
                    WHERE run_id = ?
                    ORDER BY tick, engine, metric
                    """,
                    (run_id,),
                ).fetchall()
            )

    def metric_series(
        self, run_id: str, engine: str, metric: str
    ) -> list[tuple[int, float]]:
        with self._lock:
            return list(
                self._conn.execute(
                    """
                    SELECT tick, value FROM tick_metrics
                    WHERE run_id = ? AND engine = ? AND metric = ?
                    ORDER BY tick
                    """,
                    (run_id, engine, metric),
                ).fetchall()
            )

    def latest_metrics(self, run_id: str) -> dict[str, dict[str, float]]:
        """Dernières métriques calculées (tick max de ``tick_metrics``)."""
        with self._lock:
            row = self._conn.execute(
                "SELECT MAX(tick) FROM tick_metrics WHERE run_id = ?", (run_id,)
            ).fetchone()
            tick = int(row[0]) if row and row[0] is not None else None
            if tick is None:
                return {}
            rows = self._conn.execute(
                """
                SELECT engine, metric, value FROM tick_metrics
                WHERE run_id = ? AND tick = ?
                ORDER BY engine, metric
                """,
                (run_id, tick),
            ).fetchall()
        latest: dict[str, dict[str, float]] = {}
        for engine, metric, value in rows:
            latest.setdefault(engine, {})[metric] = float(value)
        return latest

    def latest_context(self, run_id: str, context_type: str) -> tuple[int, object] | None:
        """Contexte JSON le plus récent (tick, payload) ou ``None``."""
        with self._lock:
            row = self._conn.execute(
                """
                SELECT tick, payload FROM tick_contexts
                WHERE run_id = ? AND context_type = ?
                ORDER BY tick DESC LIMIT 1
                """,
                (run_id, context_type),
            ).fetchone()
        if row is None:
            return None
        return int(row[0]), json.loads(row[1])

    def observations_for(
        self, run_id: str, context_type: str
    ) -> list[tuple[int, object]]:
        """Observations d'un contexte sur toute la série (tri tick croissant)."""
        with self._lock:
            rows = self._conn.execute(
                """
                SELECT tick, payload FROM tick_contexts
                WHERE run_id = ? AND context_type = ?
                ORDER BY tick
                """,
                (run_id, context_type),
            ).fetchall()
        return [(int(tick), json.loads(payload)) for tick, payload in rows]

    def schema_tables(self) -> set[str]:
        with self._lock:
            rows = self._conn.execute(
                "SELECT name FROM sqlite_master WHERE type = 'table'"
            ).fetchall()
        return {row[0] for row in rows if row[0] != "sqlite_sequence"}

    def close(self) -> None:
        with self._lock:
            self._conn.close()

    def __enter__(self) -> "AnalyticsStore":
        return self

    def __exit__(self, *exc: object) -> None:
        self.close()


def _json_dumps(payload: object) -> str:
    """Sérialisation JSON compacte et déterministe (clés triées pour l'export)."""
    return json.dumps(payload, sort_keys=True, separators=(",", ":"))
