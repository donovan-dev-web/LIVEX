"""Contract integration between the SYNE mock (Node) and ECHOS.

The mock speaks the same observability and control contracts as the real SYNE
engine, so ECHOS can be developed, demoed and regression-tested without a .NET
build. This is the counterpart of ``test_syne_echos_integration.py``, which
drives ``Simulation.Console.dll`` and is therefore reserved for the U8 job.

Set ``LIVEX_MOCK_E2E=1`` and install ``syne-mock`` (``npm ci``) to run it. The
mock job in CI sets the flag; ``node`` and the mock must be available.
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
from echos.ingestion.ws_client import StreamClosed
from echos.ingestion.ws_client import WsClient
from echos.storage.sqlite import AnalyticsStore
from echos.tests.provenance_expectations import unexpected_unmeasured

ROOT = Path(__file__).resolve().parents[3]
MOCK = ROOT / "syne-mock"


@pytest.mark.skipif(
    os.getenv("LIVEX_MOCK_E2E") != "1",
    reason="requires the syne-mock Node server; enabled in the mock integration CI job",
)
def test_mock_stream_is_ingested_and_served_by_echos(tmp_path):
    node = "node" if os.name != "nt" else "node.exe"
    assert (MOCK / "node_modules" / "ws").is_dir(), "npm ci in syne-mock first"
    config_path = tmp_path / "mock.json"
    process = None
    websocket = None
    websocket_context = None
    try:
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 0))
            data_port = int(probe.getsockname()[1])
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 0))
            control_port = int(probe.getsockname()[1])
        config_path.write_text(json.dumps({
            "dataPort": data_port,
            "controlPort": control_port,
            "agents": {"initialCount": 3},
        }), encoding="utf-8")
        process = subprocess.Popen(
            [node, "src/cli.js", str(config_path)],
            cwd=MOCK,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        control_url = f"http://127.0.0.1:{control_port}"
        deadline = time.monotonic() + 20
        last_error: Exception | None = None
        while time.monotonic() < deadline and process.poll() is None:
            try:
                _json_request(f"{control_url}/api/control/status", "GET")
                websocket_context = connect(
                    f"ws://127.0.0.1:{data_port}/", open_timeout=0.2, close_timeout=0.2
                )
                websocket = websocket_context.__enter__()
                break
            except Exception as exc:  # server is still starting or binding
                last_error = exc
                time.sleep(0.02)
        assert websocket is not None, f"mock WebSocket did not start: {last_error}"

        started = _json_request(
            f"{control_url}/api/control/start", "POST", {"seed": 17, "maxTicks": 200000}
        )
        assert started["runId"]

        # This is the assertion the ordering fix exists for: ECHOS refuses any
        # event that arrives before the first snapshot or off the current tick
        # (``ingestion.stream.aligned_ticks``).
        client = WsClient(transport=_BoundedTransport(
            websocket,
            complete_ticks=3,
            on_stop=lambda: _json_request(f"{control_url}/api/control/stop", "POST"),
        ))
        database = tmp_path / "echos-mock.db"
        with AnalyticsStore(database) as store:
            parquet_path = tmp_path / "syne-mock.parquet"
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
            assert [row[1] for row in summaries] == [1, 2, 3]
            assert parquet_path.exists(), "the Parquet agent series must be written"

            # Provenance persistée : le mock porte les trois fenêtres du
            # pipeline, donc rien ne doit être signalé comme repli neutre —
            # hors liste blanche explicite, et documentée, de celles que
            # 3 ticks / 9 agents ne permettent pas de mesurer.
            measured = store.latest_measured(run_id)
            assert set(measured) == set(store.latest_metrics(run_id))
            surprises = unexpected_unmeasured(measured)
            assert not surprises, surprises

            with TestClient(create_app(store)) as api:
                response = api.get(f"/api/runs/{run_id}/metrics")
                assert response.status_code == 200
                assert response.json()["ticks"] == [1, 2, 3]
                assert response.json()["values"]
                assert response.json()["measured"] == measured
                calibration = api.get(f"/api/runs/{run_id}/calibration")
                assert calibration.json()["status"] == "complete"
        assert _json_request(f"{control_url}/api/control/status", "GET")["state"] == "idle"
    finally:
        if websocket is not None:
            websocket.close()
        if websocket_context is not None:
            websocket_context.__exit__(None, None, None)
        if process is not None:
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)


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
                raise StreamClosed("connexion fermée après le segment borné")
            self.ticks_seen += 1
        return payload

    def send(self, payload: str | bytes) -> None:
        self.websocket.send(payload)

    def close(self) -> None:
        self.websocket.close()


def _json_request(url: str, method: str, body: dict | None = None) -> dict:
    payload = json.dumps(body or {}).encode()
    request = urllib.request.Request(
        url, data=payload, method=method, headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(request, timeout=2) as response:
        return json.loads(response.read())
