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
import urllib.request
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


class _BoundedTransport:
    """Allow a bounded number of complete ticks, then close at a boundary."""

    def __init__(self, websocket, *, complete_ticks: int, on_stop) -> None:
        self.websocket = websocket
        self.complete_ticks = complete_ticks
        self.on_stop = on_stop
        self.first_tick: int | None = None
        self.ticks_seen = 0

    def recv(self, timeout: float | None = None) -> str:
        payload = self.websocket.recv(timeout=timeout)
        message = json.loads(payload)
        if message.get("type") == "snapshot":
            tick = int(message["tick"])
            if self.first_tick is None:
                self.first_tick = tick
            elif self.ticks_seen >= self.complete_ticks:
                self.on_stop()
                self.websocket.close()
                raise InvalidMessageError(
                    "transport", "connexion fermée après le segment borné"
                )
            self.ticks_seen += 1
        return payload

    def send(self, payload: str | bytes) -> None:
        self.websocket.send(payload)

    def close(self) -> None:
        self.websocket.close()


def _free_port() -> int:
    with socket.socket() as server:
        server.bind(("127.0.0.1", 0))
        return int(server.getsockname()[1])


def _json_request(url: str, method: str, body: dict | None = None) -> dict:
    payload = json.dumps(body or {}).encode()
    request = urllib.request.Request(
        url, data=payload, method=method, headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(request, timeout=2) as response:
        return json.loads(response.read())


@pytest.mark.skipif(
    os.getenv("LIVEX_SYNE_E2E") != "1",
    reason="requires Release SYNE build; enabled in the U8 integration CI job",
)
def test_real_syne_stream_is_ingested_and_served_by_echos(tmp_path):
    assert SYNE_DLL.exists(), f"Build the SYNE Release console first: {SYNE_DLL}"
    config_path = tmp_path / "syne-e2e.json"
    config_path.write_text(json.dumps({"agents": {"initialCount": 3}}))
    port = _free_port()
    control_port = _free_port()
    process = subprocess.Popen(
        [
            "dotnet", str(SYNE_DLL), "--serve", "--serve-port", str(control_port),
            "--observe-port", str(port),
        ],
        cwd=ROOT,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    websocket = None
    websocket_context = None
    try:
        control_url = f"http://127.0.0.1:{control_port}"
        deadline = time.monotonic() + 15
        last_error: Exception | None = None
        while time.monotonic() < deadline and process.poll() is None:
            try:
                _json_request(f"{control_url}/api/control/status", "GET")
                websocket_context = connect(
                    f"ws://127.0.0.1:{port}/", open_timeout=0.2, close_timeout=0.2
                )
                websocket = websocket_context.__enter__()
                break
            except Exception as exc:  # server is still starting or binding
                last_error = exc
                time.sleep(0.02)
        assert websocket is not None, f"SYNE WebSocket did not start: {last_error}"

        started = _json_request(
            f"{control_url}/api/control/start",
            "POST",
            {
                "seed": 17,
                "maxTicks": 200000,
                "config": json.loads(config_path.read_text()),
            },
        )
        assert started["runId"]
        client = WsClient(
            transport=_BoundedTransport(
                websocket,
                complete_ticks=3,
                on_stop=lambda: _json_request(
                    f"{control_url}/api/control/stop", "POST"
                ),
            )
        )
        database = tmp_path / "echos-u8.db"
        with AnalyticsStore(database) as store:
            parquet_path = tmp_path / "syne-u8.parquet"
            result = storage.consume(client, store, parquet_path=parquet_path)
            run = store.runs()[0]
            run_id = run["run_id"]
            assert run_id == started["runId"]
            summaries = store.tick_summaries(run_id)
            traces = store.decision_traces(run_id)
            assert result.ticks_written == store.count_ticks(run_id) == 3
            assert result.agents_written == sum(row[4] for row in summaries) == 9
            assert result.decision_traces_written == len(traces) == 9
            assert result.metrics_written > 0
            assert result.events_written >= 12  # tick_summary + decision/action per entity
            assert store.latest_metrics(run_id)
            assert store.latest_context(run_id, "agents") is not None
            assert [row[1] for row in summaries] == sorted(row[1] for row in summaries)
            assert [row[1] for row in summaries] == [1, 2, 3]

            # The documented PRISM handoff is ECHOS REST; validate a real run through it.
            with TestClient(create_app(store)) as api:
                response = api.get(f"/api/runs/{run_id}/metrics")
                assert response.status_code == 200
                body = response.json()
                assert body["run_id"] == run_id
                assert body["ticks"] == [1, 2, 3]
                assert body["values"]
                calibration = api.get(f"/api/runs/{run_id}/calibration")
                assert calibration.status_code == 200
                assert calibration.json()["status"] == "complete"
                assert calibration.json()["ticks"]["count"] == 3
        status = _json_request(f"{control_url}/api/control/status", "GET")
        assert status["state"] == "idle"
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
