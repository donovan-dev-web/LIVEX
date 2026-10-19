#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Benchmark Windows natif de LIVEX : par composant et par scénario.

Pourquoi ce script existe à côté de ``scripts/benchmark.py`` : ce dernier
repose sur ``os.wait4`` / ``os.killpg`` (Unix) pour mesurer le pic RSS et
arrêter les groupes de processus — API absentes sous Windows. Ce harnais
utilise ``psutil`` : pic de working set (``peak_wset``, équivalent Windows de
``ru_maxrss``) et arrêt déterministe par identifiants de processus.

Composants mesurés :

- ``syne``      : moteur seul (``--headless``), débit t/s + pic RSS.
- ``echos``     : ingestion d'un run archivé (``POST /ingest/run``) puis
                  analyse (``POST /analysis/run``) : temps et pic RSS.
- ``syne-mock`` : émission de ticks via l'API de contrôle, débit t/s + RSS.

Scénarios mesurés (pile complète) :

- ``syne+echos``        : SYNE ``--serve`` + API ECHOS + ingestion WebSocket.
- ``launcher+syne``     : timing des tests E2E Launcher sur SYNE publié.
- ``launcher+syne+echos``: timing du parcours J3 (réel SYNE + ECHOS).

Sortie : JSON brut + un résumé Markdown imprimé. Chaque cellule porte son
pic RSS, son débit et son temps, pour que le rapport final distingue mesures
et estimations.
"""

from __future__ import annotations

import argparse
import json
import os
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Callable

import psutil

ROOT = Path(__file__).resolve().parents[1]
PUBLISHED_SYNE = (
    ROOT / "syne" / "Simulation.Console" / "bin" / "Release" / "net10.0"
    / "win-x64" / "publish" / "Simulation.Console.exe"
)
ECHOS_PYTHON = ROOT / "echos" / ".venv" / "Scripts" / "python.exe"
ECHOS_DIR = ROOT / "echos"
MOCK_DIR = ROOT / "syne-mock"


# --------------------------------------------------------------------------- #
# Réseau / attente
# --------------------------------------------------------------------------- #


def _get(url: str, timeout: float = 5.0) -> dict:
    with urllib.request.urlopen(url, timeout=timeout) as response:
        return json.loads(response.read())


def _post(url: str, payload: dict, timeout: float = 120.0) -> dict:
    data = json.dumps(payload).encode()
    request = urllib.request.Request(
        url, data=data, method="POST", headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        body = response.read()
    return json.loads(body) if body else {}


def _wait_for(
    url: str,
    predicate: Callable[[dict], bool],
    timeout: float,
    interval: float = 0.1,
) -> dict:
    deadline = time.monotonic() + timeout
    last: Exception | None = None
    while time.monotonic() < deadline:
        try:
            value = _get(url)
            if predicate(value):
                return value
        except (OSError, ValueError, urllib.error.HTTPError, urllib.error.URLError) as exc:
            last = exc
        time.sleep(interval)
    raise TimeoutError(f"délai dépassé en attendant {url} ({last})")


def _port_busy(port: int) -> bool:
    probe = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    probe.settimeout(0.3)
    try:
        return probe.connect_ex(("127.0.0.1", port)) == 0
    finally:
        probe.close()


# --------------------------------------------------------------------------- #
# Mesure : pic RSS (peak_wset) + arrêt déterministe
# --------------------------------------------------------------------------- #


def _peak_rss_kb(process: subprocess.Popen) -> int:
    """Pic de working set de l'arbre de processus (Ko) ; 0 si injoignable.

    On somme le pic du processus suivi et de ses descendants : sous Windows,
    uvicorn (sans ``--workers``) et node tiennent tantôt le serveur dans le
    processus lancé, tantôt dans un enfant — viser l'arbre évite de rapporter
    un pic tronqué.
    """
    total = 0
    try:
        parent = psutil.Process(process.pid)
    except (psutil.NoSuchProcess, psutil.AccessDenied):
        return 0
    try:
        tree = [parent, *parent.children(recursive=True)]
    except (psutil.NoSuchProcess, psutil.AccessDenied):
        tree = [parent]
    for proc in tree:
        try:
            total += int(proc.memory_info().peak_wset // 1024)
        except (psutil.NoSuchProcess, psutil.AccessDenied, AttributeError):
            continue
    return total


def _stop(process: subprocess.Popen, grace: float = 5.0) -> int:
    """Arrête l'arbre de processus, escalade, restitue le pic RSS (Ko)."""
    peak = _peak_rss_kb(process)
    try:
        parent = psutil.Process(process.pid)
        children = parent.children(recursive=True)
    except psutil.NoSuchProcess:
        return peak
    for proc in [*children, parent]:
        try:
            proc.terminate()
        except psutil.NoSuchProcess:
            pass
    _, alive = psutil.wait_procs([*children, parent], timeout=grace)
    for proc in alive:
        try:
            proc.kill()
        except psutil.NoSuchProcess:
            pass
    return peak


# --------------------------------------------------------------------------- #
# Composant SYNE (headless)
# --------------------------------------------------------------------------- #


def bench_syne(config: dict[int, Path], entities: int, ticks: int, seed: int, timeout: float) -> dict:
    config_path = _write_config(config, entities)
    command = [
        str(PUBLISHED_SYNE), "--config", str(config_path), "--seed", str(seed),
        "--max-ticks", str(ticks), "--headless",
    ]
    started = time.perf_counter()
    process = subprocess.Popen(command, cwd=ROOT)
    peak = 0
    deadline = started + timeout
    while process.poll() is None and time.perf_counter() < deadline:
        peak = max(peak, _peak_rss_kb(process))
        time.sleep(0.02)
    if process.poll() is None:
        elapsed = time.perf_counter() - started
        peak = max(peak, _stop(process))
        timed_out = True
    else:
        elapsed = time.perf_counter() - started
        peak = max(peak, _peak_rss_kb(process))
        timed_out = False
    return {
        "component": "syne", "entities": entities, "ticks": ticks, "seed": seed,
        "wall_s": round(elapsed, 3), "peak_rss_kb": peak,
        "tps": round(ticks / elapsed, 1) if elapsed > 0 and not timed_out else None,
        "timeout": timed_out,
    }


def _write_config(cache: dict[int, Path], entities: int) -> Path:
    if entities in cache:
        return cache[entities]
    path = ROOT / "echos" / "data" / "benchmarks" / f"config-win-{entities}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps({
            "agents": {"initialCount": entities},
            "resources": {"food": {"initial": 10000}, "water": {"initial": 10000}},
        }),
        encoding="utf-8",
    )
    cache[entities] = path
    return path


# --------------------------------------------------------------------------- #
# Scénario SYNE+ECHOS (pile complète)
# --------------------------------------------------------------------------- #


def bench_syne_echos(
    workdir: Path, entities: int, ticks: int, seed: int, tps: int, timeout: float
) -> dict:
    """SYNE ``--serve`` + API ECHOS + ingestion WebSocket (temps réel)."""
    api_port, control_port, observe_port = 5300, 5301, 5302
    for port in (api_port, control_port, observe_port):
        if _port_busy(port):
            raise RuntimeError(f"port {port} occupé — libérer avant de lancer")

    database = workdir / f"bench-{entities}-{ticks}-{seed}.sqlite"
    marker = workdir / f".started-{entities}-{ticks}-{seed}"
    marker.unlink(missing_ok=True)

    api_cmd = [
        str(ECHOS_PYTHON), "-m", "uvicorn", "echos.api.app:app",
        "--app-dir", str(ECHOS_DIR), "--host", "127.0.0.1", "--port", str(api_port),
    ]
    syne_cmd = [
        str(PUBLISHED_SYNE), "--serve", "--serve-port", str(control_port),
        "--observe-port", str(observe_port),
    ]
    ingest_cmd = [str(ECHOS_PYTHON), "-m", "echos.dev_ingest"]

    env = os.environ.copy()
    env.update(
        ECHOS_ANALYTICS_DB=str(database),
        ECHOS_ANALYSIS_EVERY="1",
        SYNE_CONTROL_URL=f"http://127.0.0.1:{control_port}",
        SYNE_OBSERVABILITY_URL=f"ws://127.0.0.1:{observe_port}/",
        LIVEX_WS_STARTED_FILE=str(marker),
        LIVEX_INGEST_ONCE="1",
        PYTHONPATH=str(ECHOS_DIR) + os.pathsep + env.get("PYTHONPATH", ""),
    )

    processes: list[subprocess.Popen] = []
    peaks: dict[str, int] = {}
    pipeline: float | None = None
    total_started = time.perf_counter()
    try:
        processes.append(subprocess.Popen(api_cmd, cwd=ROOT, env=env))
        _wait_for(f"http://127.0.0.1:{api_port}/health",
                  lambda v: v.get("status") == "ok", timeout)

        processes.append(subprocess.Popen(syne_cmd, cwd=ROOT, env=env))
        _wait_for(f"http://127.0.0.1:{control_port}/api/control/status",
                  lambda v: "state" in v, timeout)

        processes.append(subprocess.Popen(ingest_cmd, cwd=ROOT, env=env))
        deadline = time.monotonic() + timeout
        while not marker.exists() and time.monotonic() < deadline:
            time.sleep(0.1)
        if not marker.exists():
            raise TimeoutError("marqueur d'ingestion WS jamais atteint")

        run_started = time.perf_counter()
        started = _post(
            f"http://127.0.0.1:{control_port}/api/control/start",
            {"seed": seed, "maxTicks": ticks,
             "config": {"simulation": {"ticksPerSecond": tps},
                        "agents": {"initialCount": entities}}},
        )
        run_id = started.get("runId")
        _wait_for(f"http://127.0.0.1:{control_port}/api/control/status",
                  lambda v: v.get("state") == "finished" and v.get("tick", 0) >= ticks, timeout)
        syne_run_s = time.perf_counter() - run_started

        # Fin de la chaîne : ECHOS a écrit tous les ticks. C'est le seul repère
        # fiable de fin de pipeline — le rapport de calibration n'est pas
        # persisté sur le chemin live (il l'est par ``/ingest/run`` d'archive).
        _wait_for(f"http://127.0.0.1:{api_port}/api/runs/{run_id}",
                  lambda v: v.get("ticks_count", 0) >= max(ticks - 1, 0), timeout)
        pipeline = time.perf_counter() - run_started

        analysis_s = None
        analysis_started = time.perf_counter()
        _post(f"http://127.0.0.1:{api_port}/analysis/run",
              {"experimentId": f"bench-live-{entities}",
               "runId": run_id, "runPath": str(workdir)}, timeout=timeout)
        analysis_s = round(time.perf_counter() - analysis_started, 3)
    finally:
        marker.unlink(missing_ok=True)
        for label, index in (("api", 0), ("syne", 1), ("ingest", 2)):
            if len(processes) > index:
                peaks[label] = _stop(processes[index])

    total = time.perf_counter() - total_started
    if pipeline is None:
        raise TimeoutError("le run SYNE n'a pas atteint 'finished'")
    return {
        "scenario": "syne+echos", "entities": entities, "ticks": ticks, "seed": seed,
        "pipeline_s": round(pipeline, 3), "syne_run_s": round(syne_run_s, 3),
        "analysis_s": analysis_s, "total_s": round(total, 3),
        "peak_rss_kb": sum(peaks.values()), "peak_api_kb": peaks.get("api", 0),
        "peak_syne_kb": peaks.get("syne", 0), "peak_ingest_kb": peaks.get("ingest", 0),
        "tps": round(ticks / pipeline, 1) if pipeline > 0 else None,
    }


# --------------------------------------------------------------------------- #
# Composant ECHOS (ingestion + analyse d'un run archivé)
# --------------------------------------------------------------------------- #


def _produce_archived_run(workdir: Path, entities: int, ticks: int, seed: int, timeout: float) -> Path:
    """Génère un run archivé SYNE (``<run_dir>/data/stream.jsonl``).

    Le mode batch supervisé (``--autostart --control-port …``) écrit le flux
    d'observabilité dans ``--work-dir/data`` et se termine de lui-même une fois
    le run fini. C'est la disposition exacte que ``/ingest/run`` accepte (le
    ``runPath`` est le ``--work-dir``).
    """
    run_dir = workdir / f"run-{entities}-{ticks}-{seed}"
    if (run_dir / "data" / "stream.jsonl").is_file():
        return run_dir
    (run_dir / "logs").mkdir(parents=True, exist_ok=True)
    config_path = _write_config({}, entities)
    control_port = 5311 + (seed % 200)
    correlation_id = f"bench-{entities}-{ticks}-{seed}"
    command = [
        str(PUBLISHED_SYNE), "--headless",
        "--instance-id", f"bench-{entities}", "--control-port", str(control_port),
        "--work-dir", str(run_dir), "--log-dir", str(run_dir / "logs"),
        "--config", str(config_path), "--seed", str(seed), "--max-ticks", str(ticks),
        "--autostart", "--export-stream", "--correlation-id", correlation_id,
    ]
    env = os.environ.copy()
    env["LIVEX_SESSION_TOKEN"] = "bench-token"
    env["LIVEX_CORRELATION_ID"] = correlation_id
    process = subprocess.Popen(command, cwd=ROOT, env=env)
    try:
        process.wait(timeout=max(timeout, 120))
    except subprocess.TimeoutExpired:
        _stop(process)
        raise TimeoutError(f"batch SYNE non terminé en {timeout} s pour {entities}×{ticks}")
    if not (run_dir / "data" / "stream.jsonl").is_file():
        raise RuntimeError("SYNE n'a pas écrit data/stream.jsonl (batch échoué ?)")
    return run_dir


def bench_echos_ingest(workdir: Path, entities: int, ticks: int, seed: int, timeout: float) -> dict:
    """Ingestion puis analyse d'un run archivé, via l'API ECHOS (port dédié)."""
    api_port = 5310
    if _port_busy(api_port):
        raise RuntimeError(f"port {api_port} occupé")
    run_dir = _produce_archived_run(workdir, entities, ticks, seed, timeout)
    database = workdir / f"echos-ingest-{entities}-{ticks}-{seed}.sqlite"
    api_cmd = [
        str(ECHOS_PYTHON), "-m", "uvicorn", "echos.api.app:app",
        "--app-dir", str(ECHOS_DIR), "--host", "127.0.0.1", "--port", str(api_port),
    ]
    env = os.environ.copy()
    env["ECHOS_ANALYTICS_DB"] = str(database)
    env["PYTHONPATH"] = str(ECHOS_DIR) + os.pathsep + env.get("PYTHONPATH", "")
    process = subprocess.Popen(api_cmd, cwd=ROOT, env=env)
    try:
        _wait_for(f"http://127.0.0.1:{api_port}/health",
                  lambda v: v.get("status") == "ok", timeout)

        ingest_started = time.perf_counter()
        _post(f"http://127.0.0.1:{api_port}/ingest/run",
              {"runPath": str(run_dir)}, timeout=timeout)
        ingest_s = time.perf_counter() - ingest_started

        runs = _get(f"http://127.0.0.1:{api_port}/api/runs")
        run_id = (runs.get("runs") or [{}])[0].get("run_id")

        analysis_s = None
        if run_id:
            analysis_started = time.perf_counter()
            _post(f"http://127.0.0.1:{api_port}/analysis/run",
                  {"experimentId": f"bench-{entities}-{ticks}",
                   "runId": run_id, "runPath": str(run_dir)}, timeout=timeout)
            analysis_s = round(time.perf_counter() - analysis_started, 3)
        peak = _peak_rss_kb(process)
    finally:
        _stop(process)
    return {
        "component": "echos", "entities": entities, "ticks": ticks, "seed": seed,
        "ingest_s": round(ingest_s, 3), "analysis_s": analysis_s, "peak_rss_kb": peak,
        "ingest_tps": round(ticks / ingest_s, 1) if ingest_s > 0 else None,
    }


# --------------------------------------------------------------------------- #
# Composant syne-mock (émission de ticks)
# --------------------------------------------------------------------------- #


def bench_syne_mock(entities: int, ticks: int, timeout: float) -> dict:
    """Démarre le mock, lance un run, mesure le débit d'émission de ticks.

    ``ticksPerSecond`` est poussé très haut : la cadence demandée devient un
    plancher, et le mock émet alors aussi vite qu'il peut — c'est sa capacité
    d'émission qu'on mesure, pas un pacing.
    """
    control_port, data_port = 5320, 5321
    if _port_busy(control_port):
        raise RuntimeError(f"port {control_port} occupé")
    command = [
        "node", "src/cli.js", "--config", "config_prism.json",
        "--control-port", str(control_port), "--data-port", str(data_port), "--no-tick-log",
    ]
    process = subprocess.Popen(command, cwd=MOCK_DIR, stdout=subprocess.DEVNULL,
                               stderr=subprocess.DEVNULL)
    try:
        _wait_for(f"http://127.0.0.1:{control_port}/api/control/status",
                  lambda v: "state" in v, timeout)
        _post(f"http://127.0.0.1:{control_port}/api/control/prepare",
              {"seed": 42, "ticksPerSecond": 100000})
        _post(f"http://127.0.0.1:{control_port}/api/control/ready",
              {"worldVersion": "1.1"})
        started = time.perf_counter()
        _post(f"http://127.0.0.1:{control_port}/api/control/start",
              {"seed": 42, "maxTicks": ticks})
        _wait_for(f"http://127.0.0.1:{control_port}/api/control/status",
                  lambda v: v.get("state") == "finished", timeout, interval=0.005)
        elapsed = time.perf_counter() - started
        peak = _peak_rss_kb(process)
    finally:
        _stop(process)
    return {
        "component": "syne-mock", "agents": entities, "ticks": ticks,
        "wall_s": round(elapsed, 3), "peak_rss_kb": peak,
        "tps": round(ticks / elapsed, 1) if elapsed > 0 else None,
    }


# --------------------------------------------------------------------------- #
# Orchestration
# --------------------------------------------------------------------------- #


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path,
                        default=ROOT / "echos" / "data" / "benchmarks" / "windows-bench.json")
    parser.add_argument("--timeout", type=float, default=300.0)
    parser.add_argument("--quick", action="store_true")
    parser.add_argument(
        "--only", choices=["syne", "echos", "mock", "syne+echos"], action="append",
        help="limite à un composant/scénario (répétable)",
    )
    args = parser.parse_args(argv)

    if not PUBLISHED_SYNE.is_file():
        print(f"Erreur : SYNE publié absent ({PUBLISHED_SYNE})", file=sys.stderr)
        return 2

    workdir = ROOT / "echos" / "data" / "benchmarks" / f".win-work-{int(time.time())}"
    workdir.mkdir(parents=True, exist_ok=True)
    config_cache: dict[int, Path] = {}
    selected = set(args.only or [])
    results: list[dict] = []

    populations = [50, 500] if args.quick else [50, 500, 1000]
    ticks = 100 if args.quick else 300

    def want(name: str) -> bool:
        return not selected or name in selected

    if want("syne"):
        for entities in populations:
            for seed in (12345, 42):
                row = bench_syne(config_cache, entities, ticks, seed, args.timeout)
                results.append(row)
                print(f"[SYNE] {row['entities']} entités · {row['tps']} t/s · "
                      f"RSS {row['peak_rss_kb'] / 1024:.0f} Mo", flush=True)

    if want("mock"):
        for agents in (100,):
            for run_ticks in (200,):
                row = bench_syne_mock(agents, run_ticks, args.timeout)
                results.append(row)
                print(f"[MOCK] {row['agents']} agents · {row['tps']} t/s · "
                      f"RSS {row['peak_rss_kb'] / 1024:.0f} Mo", flush=True)

    if want("echos"):
        for entities, run_ticks in ((50, 100), (100, 100)):
            row = bench_echos_ingest(workdir, entities, run_ticks, 12345, args.timeout)
            results.append(row)
            print(f"[ECHOS] ingest {row['entities']}×{row['ticks']} · "
                  f"{row['ingest_s']} s ({row['ingest_tps']} t/s) · analyse {row['analysis_s']} s · "
                  f"RSS {row['peak_rss_kb'] / 1024:.0f} Mo", flush=True)

    if want("syne+echos"):
        # Cadence soutenue (10 t/s : le pipeline doit suivre sans retard) …
        for entities in (50, 100):
            row = bench_syne_echos(workdir, entities, 100, 12345, 10, args.timeout)
            row["mode"] = "soutenu"
            results.append(row)
            print(f"[SYNE+ECHOS] {row['entities']} entités · run SYNE {row['syne_run_s']} s · "
                  f"pipeline {row['pipeline_s']} s ({row['tps']} t/s) · analyse {row['analysis_s']} s · "
                  f"RSS total {row['peak_rss_kb'] / 1024:.0f} Mo", flush=True)
        # … puis capacité : cadence demandée hors d'atteinte, le débit mesuré
        # est alors celui que la pile tient réellement, ingestion comprise.
        for entities in (100,):
            row = bench_syne_echos(workdir, entities, 200, 12345, 100000, args.timeout)
            row["mode"] = "capacité"
            results.append(row)
            print(f"[SYNE+ECHOS] {row['entities']} entités (capacité) · run SYNE "
                  f"{row['syne_run_s']} s · pipeline {row['pipeline_s']} s ({row['tps']} t/s) · "
                  f"RSS total {row['peak_rss_kb'] / 1024:.0f} Mo", flush=True)

    args.out.parent.mkdir(parents=True, exist_ok=True)
    payload = {"timestamp": time.strftime("%Y-%m-%dT%H:%M:%S"), "results": results}
    args.out.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nRésultats écrits : {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
