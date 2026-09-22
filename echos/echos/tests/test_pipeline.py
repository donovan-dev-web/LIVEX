"""Pipeline d'ingestion (ECHOS-011→013) : flux → SQLite + Parquet, en
bout-en-bout sur un vrai serveur WebSocket in-process."""

import json
import threading
import time
from pathlib import Path

from websockets.sync.server import serve

from echos import storage
from echos.ingestion import WsClient
from echos.storage.sqlite import AnalyticsStore

FIXTURES = Path(__file__).resolve().parent / "fixtures"


def _variant(name: str, tick: int) -> str:
    data = json.loads((FIXTURES / name).read_text())
    data["tick"] = tick
    return json.dumps(data)


def _script(ticks: int = 3) -> list:
    payloads = []
    for tick in range(1, ticks + 1):
        payloads.append(_variant("world_snapshot_v01.json", tick))
        payloads.append(_variant("tick_summary_v01.json", tick))
        payloads.append(_variant("decision_made_v01.json", tick))
    return payloads


def _in_process_server(frames: list) -> tuple[int, threading.Thread]:
    """Serveur WebSocket réel en thread ; renvoie (port, thread)."""
    slot: list = []

    def _serve() -> None:
        def handler(connection):
            for payload in frames:
                connection.send(payload)

        with serve(handler, "127.0.0.1", 0) as server:
            slot.append(server)
            server.serve_forever()

    thread = threading.Thread(target=_serve, daemon=True)
    thread.start()
    for _ in range(100):
        if slot:
            break
        time.sleep(0.02)
    assert slot, "serveur in-process non démarré"
    return slot[0].socket.getsockname()[1], thread


def test_consume_from_real_server_writes_sqlite_and_parquet(tmp_path):
    port, thread = _in_process_server(_script(3))
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")

    db_path = tmp_path / "analyse.db"
    parquet_path = tmp_path / "agents.parquet"

    with AnalyticsStore(db_path) as store:
        result = storage.consume(
            client,
            store,
            parquet_path=parquet_path,
        )
        assert result.ticks_written == 3
        assert result.events_written == 6  # 2 événements par tick
        assert result.agents_written == 6  # 2 agents × 3 ticks
        assert result.metrics_written > 0  # métriques des 8 moteurs par tick
        assert result.contexts_written == 9  # 3 ticks × (agents, groups, phenomena)
        assert store.count_ticks("run-7") == 3
        assert len(store.events("run-7")) == 6
        assert len(store.tick_summaries("run-7")) == 3
        assert store.latest_metrics("run-7")  # dernières métriques présentes
        assert store.latest_context("run-7", "agents") is not None
        assert store.latest_context("run-7", "phenomena") is not None
        assert store.latest_context("run-7", "groups") is not None

    series = list(storage.read_agent_series(parquet_path))
    assert len(series) == 6
    assert {row.tick for row in series} == {1, 2, 3}

    with storage.AnalyticsStore(db_path) as reopened:
        indexed_ticks = {row[1] for row in reopened.tick_summaries("run-7")}
    assert storage.coherence_errors(series, indexed_ticks) == []
