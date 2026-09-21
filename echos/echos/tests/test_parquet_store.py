"""Séries lourdes en Parquet (ECHOS-013) : roundtrip columnar et cohérence
de jointure SQLite ↔ Parquet sur la clé (tick, agent)."""

import json
from pathlib import Path

from echos.ingestion import WsClient, aligned_ticks
from echos.storage.parquet import (
    agent_rows,
    coherence_errors,
    read_agent_series,
    write_agent_series,
)

FIXTURES = Path(__file__).resolve().parent / "fixtures"


class FakeTransport:
    def __init__(self, payloads: list) -> None:
        self._queue = list(payloads)

    def recv(self, timeout: float | None = None) -> str | None:
        return self._queue.pop(0) if self._queue else None

    def send(self, payload: str | bytes) -> None:
        pass

    def close(self) -> None:
        pass


def _variant(name: str, tick: int) -> str:
    data = json.loads((FIXTURES / name).read_text())
    data["tick"] = tick
    return json.dumps(data)


def _segment(tick: int = 1):
    client = WsClient(
        transport=FakeTransport([_variant("world_snapshot_v01.json", tick)])
    )
    client.connect("ws://127.0.0.1:5180")
    return next(iter(aligned_ticks(client)))


def test_agent_rows_are_deterministic_from_segment():
    rows = agent_rows(_segment(1))

    assert len(rows) == 2
    first = rows[0]
    assert first.run_id == "run-7"
    assert first.tick == 1
    assert first.agent_id == "1"
    assert first.energy == 40.0
    assert first.hunger == 100.0
    assert first.fatigue == 61.49999999999977
    assert first.current_action == "SeekWater"
    assert first.species == "Entité A"


def test_parquet_write_read_roundtrip(tmp_path):
    rows = agent_rows(_segment(1))
    path = tmp_path / "agents.parquet"

    write_agent_series(path, rows)

    assert path.exists()
    assert list(read_agent_series(path)) == rows


def test_parquet_rewrite_overwrites_previous_file(tmp_path):
    path = tmp_path / "agents.parquet"
    write_agent_series(path, agent_rows(_segment(1)))
    write_agent_series(path, agent_rows(_segment(2)))

    reloaded = list(read_agent_series(path))
    assert all(row.tick == 2 for row in reloaded)
    assert len(reloaded) == 2


def test_coherence_with_sqlite_indexed_ticks():
    rows = agent_rows(_segment(3))

    assert coherence_errors(rows, indexed_ticks={3}) == []
    errors = coherence_errors(rows, indexed_ticks={1, 2})
    assert len(errors) == len(rows)
    assert all("absent de SQLite" in error for error in errors)
