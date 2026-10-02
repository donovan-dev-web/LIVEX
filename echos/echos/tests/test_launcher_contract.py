from __future__ import annotations

import json
import os
import socket
import subprocess
import time
from pathlib import Path
from types import SimpleNamespace

import httpx
from fastapi.testclient import TestClient

from echos.api.app import create_app
from echos.launcher import parse_args
from echos.storage.sqlite import AnalyticsStore


def _free_port() -> int:
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def test_launcher_adapter_accepts_shared_arguments():
    args = parse_args(
        [
            "--headless",
            "--instance-id",
            "echos-test",
            "--control-port",
            "50123",
            "--work-dir",
            "work",
            "--log-dir",
            "logs",
            "--correlation-id",
            "correlation-test",
        ]
    )

    assert args.headless is True
    assert args.instance_id == "echos-test"
    assert args.control_port == 50123
    assert args.work_dir == "work"
    assert args.log_dir == "logs"


def test_manifest_describes_linux_adapter_and_database_readiness():
    root = Path(__file__).resolve().parents[2]
    manifest = json.loads((root / "component.json").read_text())

    assert manifest["id"] == "echos"
    assert manifest["executable"]["linux"] == "echos-launcher"
    assert (root / manifest["executable"]["linux"]).stat().st_mode & 0o111 == 0o111
    assert manifest["health"]["path"] == "/health/ready"


def test_health_distinguishes_process_liveness_from_analytics_readiness(monkeypatch):
    monkeypatch.delenv("ECHOS_ANALYTICS_DB", raising=False)
    client = TestClient(create_app())

    assert client.get("/health").status_code == 200
    assert client.get("/health/ready").status_code == 503
    assert client.get("/api/runs").status_code == 503


def test_readiness_and_authenticated_shutdown_require_live_launcher_capabilities(monkeypatch):
    monkeypatch.setenv("LIVEX_SESSION_TOKEN", "test-session")
    store = AnalyticsStore(":memory:")
    app = create_app(store=store)
    server = SimpleNamespace(should_exit=False)
    app.state.uvicorn_server = server
    client = TestClient(app)

    assert client.get("/health/ready").status_code == 200
    assert client.post("/control/shutdown").status_code == 401
    response = client.post(
        "/control/shutdown",
        headers={"authorization": "Bearer test-session"},
    )

    assert response.status_code == 200
    assert response.json() == {"status": "shutting_down"}
    assert server.should_exit is True
    store.close()


def test_launcher_adapter_starts_on_allocated_port_and_shuts_down():
    port = _free_port()
    token = "echos-launcher-test"
    root = Path(__file__).resolve().parents[2]
    adapter = root / "echos-launcher"
    process = subprocess.Popen(
        [
            str(adapter),
            "--headless",
            "--instance-id",
            "echos-test",
            "--control-port",
            str(port),
            "--work-dir",
            "unused",
            "--log-dir",
            "unused",
            "--correlation-id",
            "test-correlation",
        ],
        env={
            **os.environ,
            "ECHOS_ANALYTICS_DB": ":memory:",
            "LIVEX_SESSION_TOKEN": token,
        },
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    endpoint = f"http://127.0.0.1:{port}"
    try:
        ready = False
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline and not ready:
            try:
                ready = httpx.get(f"{endpoint}/health/ready", timeout=0.25).status_code == 200
            except httpx.HTTPError:
                time.sleep(0.05)
        assert ready, "ECHOS should bind the allocated port and become ready with a database"
        assert httpx.post(f"{endpoint}/control/shutdown", timeout=2).status_code == 401
        response = httpx.post(
            f"{endpoint}/control/shutdown",
            headers={"authorization": f"Bearer {token}"},
            timeout=2,
        )
        assert response.status_code == 200
        assert process.wait(timeout=5) == 0
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=5)
