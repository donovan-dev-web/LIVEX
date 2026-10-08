#!/usr/bin/env python3
"""Campagne V3 — cadence, backpressure, lag et pertes (ROADMAP-V01 étape 6).

Scénario (aucun long-run : arbitrage A2) :

1. SYNE ``--serve`` avec un run piloté par le contrôle HTTP :5181, cadence
   ``simulation.ticksPerSecond`` demandée à ``--ticks-per-second``.
2. Worker ``echos.dev_ingest`` réel branché sur le flux d'observation.
3. Phase « nominal » : cadence atteinte, lag d'ingestion (tick moteur − tick
   en base), trous de ticks.
4. Phase « consommateur figé » (SIGSTOP du worker) : la cadence moteur doit
   rester stable — le flux ne doit jamais rétroagir sur le moteur.
5. Phase « reprise » (SIGCONT) : le rattrapage, les pertes éventuelles et le
   nouveau lag sont mesurés.

Résultats : JSON dans ``docs/campaign-runs/v3/`` (hors git) + récapitulatif
sur stdout, à consolider dans RAPPORT-ELEMENTS-OUVERTS.md §5.3.

Usage : scripts/v3-campaign.py [--ticks-per-second 100] [--agents 50]
       [--phase-seconds 10]
"""

from __future__ import annotations

import argparse
import json
import os
import platform
import signal
import socket
import sqlite3
import statistics
import subprocess
import sys
import tempfile
import threading
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SYNE_DLL = ROOT / "syne/Simulation.Console/bin/Release/net10.0/Simulation.Console.dll"
OUT_DIR = ROOT / "docs" / "campaign-runs" / "v3"


def free_port() -> int:
    with socket.socket() as server:
        server.bind(("127.0.0.1", 0))
        return int(server.getsockname()[1])


def request(url: str, method: str = "GET", body: dict | None = None, timeout: float = 5) -> dict:
    payload = json.dumps(body or {}).encode()
    req = urllib.request.Request(
        url, data=payload, method=method, headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(req, timeout=timeout) as response:
        return json.loads(response.read())


def wait_control(control_url: str, process: subprocess.Popen, timeout: float = 20) -> None:
    deadline = time.monotonic() + timeout
    last: Exception | None = None
    while time.monotonic() < deadline and process.poll() is None:
        try:
            request(f"{control_url}/api/control/status")
            return
        except Exception as exc:  # démarrage en cours
            last = exc
            time.sleep(0.05)
    raise SystemExit(f"contrôle SYNE indisponible : {last}")


def db_tick(database: Path, run_id: str) -> int:
    """Dernier tick en base pour le run, -1 si la base est verrouillée."""
    try:
        connection = sqlite3.connect(f"file:{database}?mode=ro", uri=True, timeout=1)
        try:
            row = connection.execute(
                "SELECT COALESCE(MAX(tick), 0) FROM tick_summaries WHERE run_id = ?",
                (run_id,),
            ).fetchone()
            return int(row[0])
        finally:
            connection.close()
    except sqlite3.Error:
        return -1


def db_gaps(database: Path, run_id: str) -> int:
    """Nombre de ruptures dans la séquence de ticks en base (pertes)."""
    try:
        connection = sqlite3.connect(f"file:{database}?mode=ro", uri=True, timeout=1)
        try:
            ticks = [
                int(row[0])
                for row in connection.execute(
                    "SELECT tick FROM tick_summaries WHERE run_id = ? ORDER BY tick",
                    (run_id,),
                )
            ]
        finally:
            connection.close()
    except sqlite3.Error:
        return -1
    return sum(1 for previous, current in zip(ticks, ticks[1:]) if current - previous > 1)


def pump(stream, lines: list[str]) -> None:
    for line in stream:
        lines.append(line.rstrip("\n"))


def sample_phase(
    control_url: str, database: Path, run_id: str, seconds: float, interval: float = 0.1
) -> list[dict]:
    """Échantillonne (horloge, tick moteur, tick base) pendant ``seconds``."""
    samples: list[dict] = []
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        now = time.monotonic()
        try:
            status = request(f"{control_url}/api/control/status", timeout=2)
        except Exception:
            time.sleep(interval)
            continue
        samples.append(
            {
                "t": now,
                "engine_tick": int(status.get("tick", 0)),
                "db_tick": db_tick(database, run_id),
            }
        )
        time.sleep(interval)
    return samples


def cadence(samples: list[dict]) -> float | None:
    """Ticks/s atteints sur la phase (premier→dernier échantillon)."""
    usable = [s for s in samples if s["engine_tick"] >= 0]
    if len(usable) < 2:
        return None
    span = usable[-1]["t"] - usable[0]["t"]
    if span <= 0:
        return None
    return (usable[-1]["engine_tick"] - usable[0]["engine_tick"]) / span


def lag_stats(samples: list[dict]) -> dict:
    lags = [s["engine_tick"] - s["db_tick"] for s in samples if s["db_tick"] >= 0]
    if not lags:
        return {"median": None, "p95": None, "max": None}
    ordered = sorted(lags)
    return {
        "median": statistics.median(ordered),
        "p95": ordered[min(len(ordered) - 1, int(len(ordered) * 0.95))],
        "max": ordered[-1],
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ticks-per-second", type=int, default=100)
    parser.add_argument("--agents", type=int, default=50)
    parser.add_argument("--phase-seconds", type=float, default=10.0)
    args = parser.parse_args(argv)

    if not SYNE_DLL.exists():
        raise SystemExit(f"publiez d'abord SYNE en Release : {SYNE_DLL}")

    work = Path(tempfile.mkdtemp(prefix="livex-v3-"))
    database = work / "v3-ingest.sqlite"
    started_file = work / "worker-started"
    observe_port = free_port()
    control_port = free_port()
    control_url = f"http://127.0.0.1:{control_port}"

    results: dict = {
        "generated_at": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
        "machine": {
            "system": platform.system(),
            "release": platform.release(),
            "processor": platform.processor(),
            "cpu_count": os.cpu_count(),
        },
        "requested_ticks_per_second": args.ticks_per_second,
        "agents": args.agents,
        "phase_seconds": args.phase_seconds,
    }

    syne = subprocess.Popen(
        [
            "dotnet", str(SYNE_DLL), "--serve",
            "--serve-port", str(control_port),
            "--observe-port", str(observe_port),
        ],
        cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
    )
    worker: subprocess.Popen | None = None
    worker_lines: list[str] = []
    try:
        wait_control(control_url, syne)
        started = request(
            f"{control_url}/api/control/start", "POST",
            {
                "seed": 4242,
                "maxTicks": 10_000_000,
                "config": {
                    "agents": {"initialCount": args.agents},
                    "simulation": {"ticksPerSecond": args.ticks_per_second},
                },
            },
        )
        run_one = started["runId"]

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
            cwd=ROOT / "echos", env=env,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
        )
        threading.Thread(target=pump, args=(worker.stdout, worker_lines), daemon=True).start()

        deadline = time.monotonic() + 15
        while time.monotonic() < deadline and not any(
            "connecté au flux SYNE" in line for line in worker_lines
        ):
            time.sleep(0.1)
        assert any("connecté au flux SYNE" in line for line in worker_lines), (
            "worker non connecté :\n" + "\n".join(worker_lines)
        )

        # Phase 1 — nominal.
        nominal = sample_phase(control_url, database, run_one, args.phase_seconds)

        # Phase 2 — consommateur figé : le moteur ne doit pas rétroagir.
        os.kill(worker.pid, signal.SIGSTOP)
        frozen = sample_phase(control_url, database, run_one, args.phase_seconds)

        # Phase 3 — reprise du consommateur.
        os.kill(worker.pid, signal.SIGCONT)
        recovery = sample_phase(control_url, database, run_one, args.phase_seconds)

        request(f"{control_url}/api/control/stop", "POST", timeout=10)
        results["run_id"] = run_one
        results["phases"] = {
            "nominal": {
                "engine_cadence_ticks_per_s": cadence(nominal),
                "lag_ticks": lag_stats(nominal),
            },
            "consumer_frozen": {
                "engine_cadence_ticks_per_s": cadence(frozen),
                "lag_ticks": lag_stats(frozen),
            },
            "recovery": {
                "engine_cadence_ticks_per_s": cadence(recovery),
                "lag_ticks": lag_stats(recovery),
            },
        }
        # Laisse au worker le temps d'écrire ce qu'il a en vol avant la fermeture.
        time.sleep(2)
        results["gaps_total"] = db_gaps(database, run_one)
        results["ticks_in_database"] = db_tick(database, run_one)
        results["worker_alive_after_phases"] = worker.poll() is None
        results["worker_log"] = list(worker_lines)
        results["phase_trajectories"] = {
            name: [
                {"tick": s["engine_tick"], "db": s["db_tick"],
                 "dt": round(s["t"] - phase[0]["t"], 2) if phase else None}
                for s in phase
            ]
            for name, phase in (
                ("nominal", nominal), ("consumer_frozen", frozen),
                ("recovery", recovery),
            )
        }
    finally:
        if worker is not None:
            if worker.poll() is None and worker.pid:
                try:
                    os.kill(worker.pid, signal.SIGCONT)
                except ProcessLookupError:
                    pass
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

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out_path = OUT_DIR / f"results-{args.ticks_per_second}tps.json"
    out_path.write_text(json.dumps(results, indent=2, ensure_ascii=False) + "\n")

    requested = args.ticks_per_second
    print(f"=== V3 — cadence demandée : {requested} ticks/s ===")
    for name, phase in results["phases"].items():
        achieved = phase["engine_cadence_ticks_per_s"]
        lag = phase["lag_ticks"]
        if achieved is None:
            print(f"{name:16s} moteur=n/a")
        else:
            print(f"{name:16s} moteur={achieved:.1f} t/s ({achieved / requested:.2f}×)")
        print(f"{'':16s} lag médian={lag['median']} p95={lag['p95']} max={lag['max']} (ticks)")
    print(f"pertes (trous)   : {results['gaps_total']}")
    print(f"ticks en base    : {results['ticks_in_database']}")
    print(f"worker survivant : {results['worker_alive_after_phases']}")
    print(f"écrit : {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
