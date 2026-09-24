"""Contract integration U8 against the real SYNE WebSocket emitter.

Set LIVEX_SYNE_E2E=1 after building Simulation.Console in Release. PRISM has no
runtime in this repository yet; the last hop is checked through the documented
ECHOS REST contract consumed by PRISM.
"""

from __future__ import annotations

import json
import os
import socket
import subprocess
import time
from pathlib import Path

import pytest
from fastapi.testclient import TestClient
from websockets.sync.client import connect

from echos import storage
from echos.api.app import create_app
from echos.ingestion.models import InvalidMessageError
from echos.ingestion.ws_client import WsClient
from echos.storage.sqlite import AnalyticsStore

ROOT = Path(__file__).resolve().parents[3]
SYNE_DLL = ROOT / "syne/Simulation.Console/bin/Release/net10.0/Simulation.Console.dll"


class _OneSegmentTransport:
    """Allow one complete tick through, then end on the next snapshot boundary."""

    def __init__(self, websocket) -> None:
        self.websocket = websocket
        self.first_tick: int | None = None

    def recv(self, timeout: float | None = None) -> str:
        payload = self.websocket.recv(timeout=timeout)
        message = json.loads(payload)
        if message.get("type") == "snapshot":
            tick = int(message["tick"])
            if self.first_tick is None:
                self.first_tick = tick
            else:
                self.websocket.close()
                raise InvalidMessageError("transport", "connexion fermée après un tick")
        return payload

    def send(self, payload: str | bytes) -> None:
        self.websocket.send(payload)

    def close(self) -> None:
        self.websocket.close()


def _free_port() -> int:
    with socket.socket() as server:
        server.bind(("127.0.0.1", 0))
        return int(server.getsockname()[1])


@pytest.mark.skipif(
    os.getenv("LIVEX_SYNE_E2E") != "1",
    reason="requires Release SYNE build; enabled in the U8 integration CI job",
)
def test_real_syne_stream_is_ingested_and_served_by_echos(tmp_path):
    assert SYNE_DLL.exists(), f"Build the SYNE Release console first: {SYNE_DLL}"
    config_path = tmp_path / "syne-e2e.json"
    config_path.write_text(json.dumps({"agents": {"initialCount": 3}}))
    port = _free_port()
    process = subprocess.Popen(
        [
            "dotnet", str(SYNE_DLL), "--observe", "--observe-port", str(port),
            "--seed", "17", "--max-ticks", "200000", "--headless",
            "--config", str(config_path),
        ],
        cwd=ROOT,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    websocket = None
    websocket_context = None
    try:
        deadline = time.monotonic() + 15
        last_error: Exception | None = None
        while time.monotonic() < deadline and process.poll() is None:
            try:
                websocket_context = connect(
                    f"ws://127.0.0.1:{port}/", open_timeout=0.2, close_timeout=0.2
                )
                websocket = websocket_context.__enter__()
                break
            except Exception as exc:  # server is still starting or binding
                last_error = exc
                time.sleep(0.02)
        assert websocket is not None, f"SYNE WebSocket did not start: {last_error}"

        client = WsClient(transport=_OneSegmentTransport(websocket))
        database = tmp_path / "echos-u8.db"
        with AnalyticsStore(database) as store:
            parquet_path = tmp_path / "syne-u8.parquet"
            result = storage.consume(client, store, parquet_path=parquet_path)
            run = store.runs()[0]
            run_id = run["run_id"]
            summaries = store.tick_summaries(run_id)
            traces = store.decision_traces(run_id)
            assert result.ticks_written == store.count_ticks(run_id) == 1
            assert result.agents_written == summaries[0][4] == 3
            assert result.decision_traces_written == len(traces) == 3
            assert result.metrics_written > 0
            assert result.events_written >= 4  # tick_summary + decision/action per entity
            assert store.latest_metrics(run_id)
            assert store.latest_context(run_id, "agents") is not None

            # The documented PRISM handoff is ECHOS REST; validate a real run through it.
            with TestClient(create_app(store)) as api:
                response = api.get(f"/api/runs/{run_id}/metrics")
                assert response.status_code == 200
                body = response.json()
                assert body["run_id"] == run_id
                assert body["ticks"] == [summaries[0][1]]
                assert body["values"]
                calibration = api.get(f"/api/runs/{run_id}/calibration")
                assert calibration.status_code == 200
                assert calibration.json()["status"] == "complete"
                assert calibration.json()["ticks"]["count"] == 1
    finally:
        if websocket is not None:
            websocket.close()
        if websocket_context is not None:
            websocket_context.__exit__(None, None, None)
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
