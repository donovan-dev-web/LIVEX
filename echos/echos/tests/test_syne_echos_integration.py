"""Contract integration U8 against the real SYNE WebSocket emitter.

Set LIVEX_SYNE_E2E=1 after building Simulation.Console in Release. PRISM has no
runtime in this repository yet; the last hop is checked through the documented
ECHOS REST contract consumed by PRISM.
"""

from __future__ import annotations

import json
import os
import socket
import sqlite3
import subprocess
import sys
import threading
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
                raise StreamClosed("connexion fermée après le segment borné")
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
    config_path.write_text(json.dumps({"agents": {"initialCount": 3}}), encoding="utf-8")
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
                "config": json.loads(config_path.read_text(encoding="utf-8")),
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

            # Provenance : le run réel porte les trois fenêtres du pipeline,
            # donc les métriques fenêtrées doivent être mesurées, pas des
            # replis — hors liste blanche explicite, et documentée, de celles
            # que 3 ticks / 9 agents ne permettent pas de mesurer.
            measured = store.latest_measured(run_id)
            latest = store.latest_metrics(run_id)
            assert set(measured) == set(latest)
            surprises = unexpected_unmeasured(measured)
            assert not surprises, surprises

            # Identifiants de groupe : contre le vrai transport SYNE, les
            # membres doivent être des chaînes joignables aux ids d'agents.
            _, agents = store.latest_context(run_id, "agents")
            agent_ids = {str(agent["id"]) for agent in agents}
            _, group_list = store.latest_context(run_id, "groups")
            for group in group_list:
                assert all(isinstance(member, str) for member in group["members"])
                assert set(group["members"]) <= agent_ids

            # The documented PRISM handoff is ECHOS REST; validate a real run through it.
            with TestClient(create_app(store)) as api:
                response = api.get(f"/api/runs/{run_id}/metrics")
                assert response.status_code == 200
                body = response.json()
                assert body["run_id"] == run_id
                assert body["ticks"] == [1, 2, 3]
                assert body["values"]
                # L'API publie la provenance avec les valeurs.
                assert body["measured"] == measured
                assert api.get(f"/api/runs/{run_id}").json()["measured"] == measured
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


def _pump(stream, lines) -> None:
    """Collecte le journal du worker au fil de l'eau (thread daémo)."""

    for line in stream:
        lines.append(line.rstrip("\n"))


def _wait_until(predicate, timeout, interval=0.1) -> bool:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if predicate():
            return True
        time.sleep(interval)
    return bool(predicate())


def _tick_rows(database, run_id) -> int:
    """Lignes de tick_summaries pour un run, -1 si la base est momentanément verrouillée."""

    connection = sqlite3.connect(f"file:{database}?mode=ro", uri=True, timeout=1)
    try:
        cursor = connection.execute(
            "SELECT COUNT(*) FROM tick_summaries WHERE run_id = ?", (run_id,)
        )
        return int(cursor.fetchone()[0])
    except sqlite3.Error:
        return -1
    finally:
        connection.close()


def _start_syne(observe_port, control_port):
    return subprocess.Popen(
        [
            "dotnet", str(SYNE_DLL), "--serve", "--serve-port", str(control_port),
            "--observe-port", str(observe_port),
        ],
        cwd=ROOT,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def _wait_control(control_url, process, timeout=15) -> None:
    deadline = time.monotonic() + timeout
    last_error: Exception | None = None
    while time.monotonic() < deadline and process.poll() is None:
        try:
            _json_request(f"{control_url}/api/control/status", "GET")
            return
        except Exception as exc:  # server is still starting or binding
            last_error = exc
            time.sleep(0.05)
    raise AssertionError(f"contrôle SYNE indisponible : {last_error}")


@pytest.mark.skipif(
    os.getenv("LIVEX_SYNE_E2E") != "1",
    reason="requires Release SYNE build; enabled in the U8 integration CI job",
)
def test_worker_reconnects_after_violent_syne_death(tmp_path):
    """V3 — reprise du worker après mort violente du flux SYNE.

    SIGKILL de SYNE en plein run : le worker doit sortir de son ``recv``,
    se reconnecter avec backoff, puis réingérer le run d'une seconde session
    SYNE redémarrée sur le même port. Les preuves sont le journal du worker
    (constat de fermeture + second « connecté ») et les lignes de la base
    (le second run est écrit) — jamais la seule absence de crash.
    """
    assert SYNE_DLL.exists(), f"Build the SYNE Release console first: {SYNE_DLL}"
    database = tmp_path / "v3-worker.db"
    started_file = tmp_path / "worker-started"
    observe_port = _free_port()
    control_port = _free_port()
    control_url = f"http://127.0.0.1:{control_port}"

    syne = _start_syne(observe_port, control_port)
    worker = None
    worker_lines: list[str] = []
    try:
        _wait_control(control_url, syne)

        # Le worker est connecté AVANT le premier run : il reçoit le
        # world_initialized depuis le début, comme en production (J2).
        env = dict(os.environ)
        env.update(
            {
                "ECHOS_ANALYTICS_DB": str(database),
                "SYNE_OBSERVABILITY_URL": f"ws://127.0.0.1:{observe_port}/",
                "LIVEX_WS_STARTED_FILE": str(started_file),
            }
        )
        worker = subprocess.Popen(
            [sys.executable, "-m", "echos.dev_ingest"],
            cwd=ROOT / "echos",
            env=env,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
        )
        threading.Thread(target=_pump, args=(worker.stdout, worker_lines), daemon=True).start()

        assert _wait_until(lambda: started_file.exists(), 10), (
            "worker jamais démarré :\n" + "\n".join(worker_lines)
        )
        assert _wait_until(
            lambda: any("connecté au flux SYNE" in line for line in worker_lines), 15
        ), "worker jamais connecté :\n" + "\n".join(worker_lines)

        started = _json_request(
            f"{control_url}/api/control/start",
            "POST",
            {"seed": 17, "maxTicks": 200000, "config": {"agents": {"initialCount": 3}}},
        )
        run_one = started["runId"]
        assert _wait_until(lambda: _tick_rows(database, run_one) >= 3, 20), (
            f"run {run_one} non ingéré :\n" + "\n".join(worker_lines)
        )

        # Mort violente : SIGKILL, aucun arrêt propre du flux.
        killed_at = time.monotonic()
        syne.kill()
        syne.wait(timeout=5)

        assert _wait_until(
            lambda: any("reconnexion" in line for line in worker_lines), 10
        ), "worker n'a pas constaté la fermeture du flux :\n" + "\n".join(worker_lines)
        detected_after = time.monotonic() - killed_at
        print(f"V3 reprise : fermeture constatée en {detected_after:.2f}s")
        assert worker.poll() is None, "worker mort pendant la reconnexion"

        # Nouvelle session SYNE sur le même port. On laisse le worker reconnecter
        # AVANT de démarrer le second run : il reçoit le world_initialized du run.
        restarted_at = time.monotonic()
        syne = _start_syne(observe_port, control_port)
        _wait_control(control_url, syne)
        assert _wait_until(
            lambda: sum("connecté au flux SYNE" in line for line in worker_lines) >= 2, 45
        ), "worker non reconnecté après redémarrage :\n" + "\n".join(worker_lines)
        reconnected_after = time.monotonic() - restarted_at
        print(f"V3 reprise : reconnexion après redémarrage en {reconnected_after:.2f}s")

        started_two = _json_request(
            f"{control_url}/api/control/start",
            "POST",
            {"seed": 99, "maxTicks": 200000, "config": {"agents": {"initialCount": 3}}},
        )
        run_two = started_two["runId"]
        assert run_two != run_one, f"identités de run attendues distinctes, reçu {run_one}"
        assert _wait_until(lambda: _tick_rows(database, run_two) >= 1, 20), (
            f"reprise non prouvée : run {run_two} non ingéré après reconnexion :\n"
            + "\n".join(worker_lines)
        )
        assert worker.poll() is None, "worker arrêté après reconnexion"
    finally:
        if worker is not None:
            worker.terminate()
            try:
                worker.wait(timeout=5)
            except subprocess.TimeoutExpired:
                worker.kill()
                worker.wait(timeout=5)
        if syne.poll() is None:
            syne.terminate()
            try:
                syne.wait(timeout=5)
            except subprocess.TimeoutExpired:
                syne.kill()
                syne.wait(timeout=5)
