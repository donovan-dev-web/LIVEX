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
import zlib
from base64 import b64decode, b64encode
from pathlib import Path

from echos.storage.aggregation import TickRecord

SCHEMA_VERSION = "5"
"""Version du schéma — toute migration doit la bump + documenter (CHANGELOG).

v3 (jalon ECHOS ph5, ECHOS-051) : table ``decision_traces``.
v4 (SYNE-131/U8) : table ``calibration_reports`` — résumé déterministe
post-run, migration additive sans perte de données.
v5 : colonne ``tick_metrics.measured`` — provenance des valeurs. Migration
additive : les bases existantes reçoivent la colonne avec ``DEFAULT 1``, donc
toutes les valeurs déjà enregistrées restent considérées comme mesurées (elles
l'étaient : aucun repli neutre n'était distinguable à l'époque).
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
    measured INTEGER NOT NULL DEFAULT 1 CHECK (measured IN (0, 1)),
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

CREATE TABLE IF NOT EXISTS calibration_reports (
    run_id TEXT PRIMARY KEY REFERENCES runs(run_id) ON DELETE CASCADE,
    report_json TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_tick_summaries_run ON tick_summaries(run_id);
CREATE INDEX IF NOT EXISTS idx_events_run_tick ON events_log(run_id, tick);
CREATE INDEX IF NOT EXISTS idx_tick_metrics_run_tick ON tick_metrics(run_id, tick);
CREATE INDEX IF NOT EXISTS idx_tick_contexts_run_tick ON tick_contexts(run_id, tick);
CREATE INDEX IF NOT EXISTS idx_decision_traces_run_tick ON decision_traces(run_id, tick);
"""


class AnalyticsStore:
    """Stockage SQLite d'analyse (1 connexion, thread-safe).

    Les méthodes historiques restent commit-at-a-time pour compatibilité.
    ``append_tick_bundle`` permet au pipeline d'écrire toutes les données
    dérivées d'un tick dans une seule transaction.
    """

    def __init__(self, path: str | Path, initialize: bool = True) -> None:
        self.path = str(path)
        self._lock = threading.RLock()
        self._conn = sqlite3.connect(self.path, check_same_thread=False)
        with self._lock:
            self._conn.execute("PRAGMA foreign_keys = ON")
            self._conn.execute("PRAGMA busy_timeout = 5000")
            self._conn.execute("PRAGMA journal_mode = WAL")
            self._conn.execute("PRAGMA synchronous = NORMAL")
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
            self._migrate_measured_column()
            self._conn.execute(
                "INSERT OR REPLACE INTO _meta (key, value) VALUES ('schema_version', ?)",
                (SCHEMA_VERSION,),
            )
            self._conn.commit()

    def _migrate_measured_column(self) -> None:
        """Ajoute ``tick_metrics.measured`` aux bases créées avant la v5.

        ``CREATE TABLE IF NOT EXISTS`` ne modifie pas une table existante : sans
        ce ``ALTER``, toute base pré-v5 levait ``no such column: measured`` dès
        la première écriture de métrique. ``DEFAULT 1`` conserve les valeurs
        déjà enregistrées comme mesurées.
        """
        columns = {
            str(row[1])
            for row in self._conn.execute("PRAGMA table_info(tick_metrics)").fetchall()
        }
        if "measured" not in columns:
            self._conn.execute(
                "ALTER TABLE tick_metrics ADD COLUMN measured INTEGER NOT NULL "
                "DEFAULT 1 CHECK (measured IN (0, 1))"
            )

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
        self,
        run_id: str,
        tick: int,
        metrics: dict[str, dict],
        measured: dict[str, dict[str, bool]] | None = None,
    ) -> int:
        """Persiste les métriques calculées d'un tick (ECHOS-040→041).

        Les sorties non numériques des moteurs (``DetectedPhenomena``,
        ``Disclaimer``, compteurs composites...) sont ignorées ici — elles sont
        émissent via :meth:`append_tick_context`. Retourne le nombre de lignes
        écrites (test : couverture du pipeline).

        ``measured`` porte la provenance (``analysis.provenance``) : sans elle,
        la colonne vaut 1 (mesurée), comportement d'avant la v5.
        """
        written = 0
        with self._lock:
            for engine, values in metrics.items():
                flags = (measured or {}).get(engine) or {}
                for metric, value in values.items():
                    if isinstance(value, bool) or not isinstance(value, (int, float)):
                        continue
                    self._conn.execute(
                        """
                        INSERT INTO tick_metrics
                            (run_id, tick, engine, metric, value, measured)
                        VALUES (?, ?, ?, ?, ?, ?)
                        ON CONFLICT(run_id, tick, engine, metric)
                        DO UPDATE SET
                            value = excluded.value,
                            measured = excluded.measured
                        """,
                        (
                            run_id,
                            tick,
                            str(engine),
                            str(metric),
                            float(value),
                            1 if flags.get(metric, True) else 0,
                        ),
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
                (run_id, tick, context_type, _encode_context(payload)),
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

    def append_tick_bundle(
        self,
        record: TickRecord,
        metrics: dict[str, dict],
        contexts: dict[str, object],
        events: list[tuple[str, str | None, str | None, str | None, str | None, str | None]],
        decision_traces: list[dict],
        measured: dict[str, dict[str, bool]] | None = None,
    ) -> int:
        """Write all SQLite rows produced for one tick atomically."""
        metric_rows: list[tuple[str, int, str, str, float, int]] = []
        for engine, values in metrics.items():
            flags = (measured or {}).get(engine) or {}
            for metric, value in values.items():
                if isinstance(value, bool) or not isinstance(value, (int, float)):
                    continue
                metric_rows.append(
                    (
                        record.run_id,
                        record.tick,
                        str(engine),
                        str(metric),
                        float(value),
                        1 if flags.get(metric, True) else 0,
                    )
                )
        with self._lock:
            try:
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
                if metric_rows:
                    self._conn.executemany(
                        """
                        INSERT INTO tick_metrics
                            (run_id, tick, engine, metric, value, measured)
                        VALUES (?, ?, ?, ?, ?, ?)
                        ON CONFLICT(run_id, tick, engine, metric)
                        DO UPDATE SET
                            value = excluded.value,
                            measured = excluded.measured
                        """,
                        metric_rows,
                    )
                self._conn.executemany(
                    """
                    INSERT OR REPLACE INTO tick_contexts
                    (run_id, tick, context_type, payload) VALUES (?, ?, ?, ?)
                    """,
                    [
                        (record.run_id, record.tick, kind, _encode_context(payload))
                        for kind, payload in contexts.items()
                    ],
                )
                if events:
                    self._conn.executemany(
                        """
                        INSERT INTO events_log
                        (run_id, tick, type, agent_id, target_id, action, cause, value)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                        """,
                        [
                            (record.run_id, record.tick, event_type, agent_id,
                             target_id, action, cause, value)
                            for event_type, agent_id, target_id, action, cause, value in events
                        ],
                    )
                if decision_traces:
                    self._conn.executemany(
                        """
                        INSERT OR REPLACE INTO decision_traces (
                            run_id, tick, agent_id, chosen_action, utility,
                            deliberated, interrupted, cause, beliefs_count,
                            goals_count, memory_count, needs
                        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                        """,
                        [
                            (
                                record.run_id, record.tick, str(trace["agent_id"]),
                                str(trace["chosen_action"]), float(trace["utility"]),
                                int(bool(trace["deliberated"])), int(bool(trace["interrupted"])),
                                str(trace["cause"]), int(trace["beliefs_count"]),
                                int(trace["goals_count"]), int(trace["memory_count"]),
                                _json_dumps(trace["needs"]),
                            )
                            for trace in decision_traces
                        ],
                    )
                self._conn.commit()
            except Exception:
                self._conn.rollback()
                raise
            self._bump()
        return len(metric_rows)

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

    def latest_decision_tick(self, run_id: str, agent_id: str) -> int | None:
        """Dernier tick portant une trace de décision pour une entité.

        Endpoint ``causal-chains`` sans ``?tick=`` (ECHOS-061) : l'analyse
        s'ouvre sur la décision la plus récente de l'entité sur le run.
        """
        with self._lock:
            row = self._conn.execute(
                "SELECT MAX(tick) FROM decision_traces WHERE run_id = ? AND agent_id = ?",
                (run_id, agent_id),
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
        """Événements du run en tuple compact ``(tick, type, agent_id, action,
        cause, value)`` — tri ``(tick, id)`` (ordre d'émission stable)."""
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

    def event_records(self, run_id: str) -> list[dict]:
        """Événements du run en dicts (**tous** les champs, ``target_id`` inclus).

        La forme tuple de :meth:`events` omet ``target_id``, ce qui interdit
        toute comparaison de contenu exhaustive (empreinte bit-à-bit). Les noms
        de clés explicites suppriment tout indexage positionnel fragile côté
        appelants.
        """
        with self._lock:
            rows = self._conn.execute(
                """
                SELECT tick, type, agent_id, target_id, action, cause, value
                FROM events_log
                WHERE run_id = ?
                ORDER BY tick, id
                """,
                (run_id,),
            ).fetchall()
        return [
            {
                "tick": int(row[0]),
                "type": row[1],
                "agent_id": row[2],
                "target_id": row[3],
                "action": row[4],
                "cause": row[5],
                "value": row[6],
            }
            for row in rows
        ]

    def events_by_type(
        self, run_id: str, event_type: str, *, max_tick: int | None = None
    ) -> list[dict]:
        """Événements d'un type donné, en dicts, triés par ``(tick, id)``.

        ``max_tick`` borne la lecture au tick inclus : l'analyse causale ne
        consulte ainsi que la fenêtre qui la concerne au lieu de recharger
        l'intégralité du journal à chaque requête.
        """
        query = """
            SELECT tick, type, agent_id, target_id, action, cause, value
            FROM events_log
            WHERE run_id = ? AND type = ?
        """
        params: list[object] = [run_id, event_type]
        if max_tick is not None:
            query += " AND tick <= ?"
            params.append(int(max_tick))
        query += " ORDER BY tick, id"
        with self._lock:
            rows = self._conn.execute(query, params).fetchall()
        return [
            {
                "tick": int(row[0]),
                "type": row[1],
                "agent_id": row[2],
                "target_id": row[3],
                "action": row[4],
                "cause": row[5],
                "value": row[6],
            }
            for row in rows
        ]

    def save_calibration_report(self, run_id: str, report: dict) -> None:
        """Persist one deterministic post-run calibration report."""
        with self._lock:
            self._conn.execute(
                "INSERT OR REPLACE INTO calibration_reports (run_id, report_json) VALUES (?, ?)",
                (run_id, json.dumps(report, sort_keys=True, separators=(",", ":"))),
            )
            self._conn.commit()
            self._bump()

    def calibration_report(self, run_id: str) -> dict | None:
        """Read a stored post-run calibration report, if available."""
        with self._lock:
            row = self._conn.execute(
                "SELECT report_json FROM calibration_reports WHERE run_id = ?", (run_id,)
            ).fetchone()
        return json.loads(row[0]) if row is not None else None

    def runs(self) -> list[dict]:
        """Runs enregistrés avec bornes de ticks (ordre déterministe par run_id).

        Un run est créé par ``record_run`` *avant* l'écriture de son premier
        tick : le LEFT JOIN renvoie donc ``NULL`` sur les bornes. Les runs sans
        tick exposent ``first_tick``/``last_tick`` à ``None`` (et non 0, qui
        désignerait un tick réel) pour que les appelants puissent distinguer
        « aucun tick » de « tick 0 ».
        """
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
                "first_tick": int(row[4]) if row[4] is not None else None,
                "last_tick": int(row[5]) if row[5] is not None else None,
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

    def metric_names(
        self,
        run_id: str,
        *,
        engine: str | None = None,
        metric: str | None = None,
    ) -> list[tuple[str, str]]:
        """Couples (moteur, métrique) distincts du run, triés.

        La découverte porte sur l'ensemble des ticks : une métrique présente à
        un ancien instant mais absente du dernier reste donc découvrable, ce
        que ``latest_metrics`` ne permettait pas.
        """
        query = "SELECT DISTINCT engine, metric FROM tick_metrics WHERE run_id = ?"
        params: list[object] = [run_id]
        if engine is not None:
            query += " AND engine = ?"
            params.append(engine)
        if metric is not None:
            query += " AND metric = ?"
            params.append(metric)
        query += " ORDER BY engine, metric"
        with self._lock:
            rows = self._conn.execute(query, params).fetchall()
        return [(str(row[0]), str(row[1])) for row in rows]

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
        return self._latest_metric_column(run_id, "value")

    def latest_measured(self, run_id: str) -> dict[str, dict[str, bool]]:
        """Provenance des dernières métriques (drappeaux ``measured``).

        Miroir de :meth:`latest_metrics` sur la colonne ``measured`` : l'API
        sert ainsi la valeur **et** le fait qu'elle ait été mesurée, ce qui
        permet à l'UI de distinguer un 0.0 observé d'un repli neutre.
        """
        return self._latest_metric_column(run_id, "measured")

    def _latest_metric_column(
        self, run_id: str, column: str
    ) -> dict[str, dict[str, float]]:
        with self._lock:
            row = self._conn.execute(
                "SELECT MAX(tick) FROM tick_metrics WHERE run_id = ?", (run_id,)
            ).fetchone()
            tick = int(row[0]) if row and row[0] is not None else None
            if tick is None:
                return {}
            rows = self._conn.execute(
                f"""
                SELECT engine, metric, {column} FROM tick_metrics
                WHERE run_id = ? AND tick = ?
                ORDER BY engine, metric
                """,  # noqa: S608 — colonne d'une liste interne fermée
                (run_id, tick),
            ).fetchall()
        latest: dict[str, dict[str, float]] = {}
        for engine, metric, value in rows:
            latest.setdefault(engine, {})[metric] = (
                bool(value) if column == "measured" else float(value)
            )
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
        return int(row[0]), _decode_context(row[1])

    def context_before(
        self, run_id: str, context_type: str, tick: int
    ) -> tuple[int, object] | None:
        """Contexte JSON le plus récent avec ``tick <= tick`` ou ``None``.

        Lecture déterministe pour l'analyse causale (ECHOS-061) : le contexte
        ``agents`` au-plus-près de la décision — ordre stable ``tick DESC``.
        """
        with self._lock:
            row = self._conn.execute(
                """
                SELECT tick, payload FROM tick_contexts
                WHERE run_id = ? AND context_type = ? AND tick <= ?
                ORDER BY tick DESC LIMIT 1
                """,
                (run_id, context_type, int(tick)),
            ).fetchone()
        if row is None:
            return None
        return int(row[0]), _decode_context(row[1])

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
        return [(int(tick), _decode_context(payload)) for tick, payload in rows]

    def contexts(self, run_id: str) -> dict[str, list[tuple[int, object]]]:
        """All recorded contexts, grouped by type in stable order.

        This is the read-side counterpart to ``append_tick_context`` and is
        intentionally kept as a storage API so exporters do not need to know
        the SQLite schema.
        """
        with self._lock:
            rows = self._conn.execute(
                """
                SELECT context_type, tick, payload FROM tick_contexts
                WHERE run_id = ?
                ORDER BY context_type, tick
                """,
                (run_id,),
            ).fetchall()
        result: dict[str, list[tuple[int, object]]] = {}
        for context_type, tick, payload in rows:
            result.setdefault(str(context_type), []).append(
                (int(tick), _decode_context(payload))
            )
        return result

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


def _encode_context(payload: object) -> str:
    """Encode contexts compactly; retain JSON fallback compatibility on reads.

    Agent snapshots contain growing belief lists and were consuming ~1.6 MB per
    tick at the 800-tick failure point. Deflate keeps the existing JSON payload
    contract at the storage API boundary while reducing SQLite write pressure.
    """
    raw = _json_dumps(payload).encode("utf-8")
    compressed = zlib.compress(raw, level=1)
    if len(compressed) >= len(raw):
        return raw.decode("utf-8")
    return "z:" + b64encode(compressed).decode("ascii")


def _decode_context(payload: str | bytes) -> object:
    """Read compressed contexts and legacy plain JSON rows."""
    text = payload.decode("utf-8") if isinstance(payload, bytes) else payload
    if text.startswith("z:"):
        text = zlib.decompress(b64decode(text[2:])).decode("utf-8")
    return json.loads(text)
