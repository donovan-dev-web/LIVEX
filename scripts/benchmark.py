#!/usr/bin/env python3
"""Benchmark réel SYNE vs SYNE + ECHOS (comparatif de performances).

Pour une grille (entités × ticks × seeds), deux scénarios sont mesurés :

- ``syne``   : moteur seul (CLI ``--headless``, sans observabilité) ;
- ``echos``  : pile complète SYNE (``--serve``) + API ECHOS + ingestion
  WebSocket, pilotée via le HTTP :5181 — sans génération de rapport.

Pour chaque cellule : temps mur, pics RSS par processus (et agrégé), débit
de ticks. Sorties : CSV brut + rapport Markdown dans ``--output-dir``.

Mesure RAM : chaque processus est un enfant direct ; son pic RSS est lu via
``os.wait4`` (``ru_maxrss``, Linux) au moment du reaping — pas de dépendance
externe (pas de ``psutil``).
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import signal
import subprocess
import sys
import time
from pathlib import Path
from typing import Callable
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BIN = (
    ROOT / "syne" / "Simulation.Console" / "bin" / "Release" / "net10.0"
    / "Simulation.Console"
)
DEFAULT_OUTPUT = ROOT / "echos" / "data" / "benchmarks"
DEFAULT_PORT = 5181
DEFAULT_OBSERVE_PORT = 5180
DEFAULT_API_PORT = 5000
DEFAULT_SEEDS = (12345, 42)
DEFAULT_ENTITIES = (20, 50, 100)
DEFAULT_TICKS = (100, 400)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--entities",
        type=_csv_ints,
        default=DEFAULT_ENTITIES,
        help=f"populations à tester (défaut: {','.join(map(str, DEFAULT_ENTITIES))})",
    )
    parser.add_argument(
        "--ticks",
        type=_csv_ints,
        default=DEFAULT_TICKS,
        help=f"nombres de ticks (défaut: {','.join(map(str, DEFAULT_TICKS))})",
    )
    parser.add_argument(
        "--seeds",
        type=_csv_ints,
        default=DEFAULT_SEEDS,
        help=f"seeds distinctes (défaut: {','.join(map(str, DEFAULT_SEEDS))})",
    )
    parser.add_argument(
        "--ticks-per-second",
        type=_positive_int,
        default=50,
        help="cadence SYNE du scénario ECHOS (défaut: 50; plus haut = pipeline sous tension)",
    )
    parser.add_argument(
        "--analysis-every",
        type=_positive_int,
        default=1,
        help="planifie l'analyse ECHOS 1 tick sur N (défaut: 1 = chaque tick)",
    )
    parser.add_argument(
        "--parquet",
        action=argparse.BooleanOptionalAction,
        default=False,
        help="active l'écriture de la série agents Parquet dans l'ingestion",
    )
    parser.add_argument(
        "--syne-bin",
        type=Path,
        default=DEFAULT_BIN,
        help="binaire Simulation.Console Release",
    )
    parser.add_argument("--api-url", default=f"http://127.0.0.1:{DEFAULT_API_PORT}")
    parser.add_argument("--control-url", default=f"http://127.0.0.1:{DEFAULT_PORT}")
    parser.add_argument(
        "--database",
        type=Path,
        help="base SQLite du scénario ECHOS (défaut: répertoire de travail temporaire)",
    )
    parser.add_argument(
        "--output-dir", type=Path, default=DEFAULT_OUTPUT, help="répertoire des rapports"
    )
    parser.add_argument(
        "--cell-timeout",
        type=float,
        default=600.0,
        help="temps max par cellule avant abandon (défaut: 600 s)",
    )
    parser.add_argument(
        "--poll-interval", type=float, default=0.1, help=argparse.SUPPRESS
    )
    parser.add_argument(
        "--quick",
        action="store_true",
        help="matrice réduite : 1 seed × entités/ticks minimaux (démos rapides)",
    )
    return parser.parse_args(argv)


def _csv_ints(value: str) -> list[int]:
    try:
        parsed = [int(item, 10) for item in value.split(",") if item.strip()]
    except ValueError as exc:
        raise argparse.ArgumentTypeError("liste d'entiers attendue (ex: 20,50,100)") from exc
    if not parsed:
        raise argparse.ArgumentTypeError("liste vide")
    return parsed


def _positive_int(value: str) -> int:
    parsed = int(value, 10)
    if parsed <= 0:
        raise argparse.ArgumentTypeError("doit être > 0")
    return parsed


# --------------------------------------------------------------------------- #
# Helpers réseau (copie locale du batch : pas de dépendance à echos)
# --------------------------------------------------------------------------- #


def _request_json(url: str, method: str = "GET", payload: dict | None = None) -> dict:
    data = None if payload is None else json.dumps(payload).encode()
    request = Request(url, data=data, method=method, headers={"Content-Type": "application/json"})
    with urlopen(request, timeout=5) as response:
        body = response.read()
    value = json.loads(body)
    if not isinstance(value, dict):
        raise RuntimeError(f"réponse JSON inattendue de {url}")
    return value


def _wait_for(
    url: str,
    predicate: Callable[[dict], bool],
    timeout: float,
    interval: float,
) -> dict:
    deadline = time.monotonic() + timeout
    last_error: Exception | None = None
    while time.monotonic() < deadline:
        try:
            value = _request_json(url)
            if predicate(value):
                return value
        except (OSError, ValueError, HTTPError, URLError) as exc:
            last_error = exc
        time.sleep(interval)
    detail = f" ({last_error})" if last_error else ""
    raise TimeoutError(f"délai dépassé en attendant {url}{detail}")


def _wait_for_path(path: Path, timeout: float, interval: float) -> None:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if path.exists():
            return
        time.sleep(interval)
    raise TimeoutError(f"délai dépassé en attendant {path}")


def _python() -> str:
    candidate = ROOT / "echos" / ".venv" / "bin" / "python"
    return str(candidate) if candidate.exists() else sys.executable


# --------------------------------------------------------------------------- #
# Mesure : process enfants directs + pic RSS via wait4
# --------------------------------------------------------------------------- #


def _alive(pid: int) -> bool:
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False


def _reap_once(pid: int) -> tuple[int, object | None]:
    """Tente de ramasser l'enfant (non bloquant) ; ``(pid, rusage)`` ou ``(0, None)``."""
    try:
        done, _, rusage = os.wait4(pid, os.WNOHANG)
    except ChildProcessError:
        # déjà ramassé ailleurs (ourrepertoire unique) — équivalent "fini".
        return pid, None
    return (done, rusage) if done else (0, None)


def _wait_rusage(pid: int, timeout: float) -> tuple[object | None, bool]:
    """Attend la sortie de l'enfant ; ``(ru_maxrss, ok)``.

    ``ok=False`` si le délai expire (le processus est alors arrêté par l'appelant).
    """
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        done, rusage = _reap_once(pid)
        if done:
            return rusage, True
        time.sleep(0.05)
    return None, False


def _signal_group(pid: int, sig: signal.Signals) -> None:
    try:
        os.killpg(pid, sig)
    except (ProcessLookupError, PermissionError):
        pass


def _stop_and_collect(
    pid: int,
    term_signal: signal.Signals = signal.SIGTERM,
    grace: float = 5.0,
) -> object | None:
    """Envoie le signal, escalade SIGKILL, puis ramasse — restitue ``ru_maxrss``."""
    _signal_group(pid, term_signal)
    deadline = time.monotonic() + grace
    while time.monotonic() < deadline:
        done, rusage = _reap_once(pid)
        if done:
            return rusage
        time.sleep(0.05)
    _signal_group(pid, signal.SIGKILL)
    while True:
        done, rusage = _reap_once(pid)
        if done:
            return rusage
        time.sleep(0.05)


def _interrupt_syne(process: subprocess.Popen) -> None:
    """Arrêt propre du serveur SYNE pour que le WS reçoive close.

    Ne ramasse jamais l'enfant : le pic RSS de SYNE est lu plus tard à la
    phase de collecte (notre ``wait4`` doit être le seul à reaper).
    """
    _signal_group(process.pid, signal.SIGINT)


# --------------------------------------------------------------------------- #
# Scénarios
# --------------------------------------------------------------------------- #


def _config_files(workdir: Path, entities: list[int]) -> dict[int, Path]:
    cache: dict[int, Path] = {}
    for count in entities:
        path = workdir / f"config-{count}.json"
        if not path.exists():
            path.write_text(
                json.dumps(
                    {
                        "agents": {"initialCount": count},
                        "resources": {
                            "food": {"initial": 10000},
                            "water": {"initial": 10000},
                        },
                    }
                ),
                encoding="utf-8",
            )
        cache[count] = path
    return cache


def run_syne_only(
    args: argparse.Namespace,
    bin_path: Path,
    config: Path,
    entity: int,
    ticks: int,
    seed: int,
) -> dict:
    """Moteur seul : CLI headless, temps + pic RSS du processus."""
    command = [
        str(bin_path),
        "--config", str(config),
        "--seed", str(seed),
        "--max-ticks", str(ticks),
        "--headless",
    ]
    started = time.perf_counter()
    process = subprocess.Popen(command, cwd=ROOT, start_new_session=True)
    rusage, ok = _wait_rusage(process.pid, args.cell_timeout)
    elapsed = time.perf_counter() - started
    if not ok:
        rusage = _stop_and_collect(process.pid, term_signal=signal.SIGTERM)
        return {
            "scenario": "syne", "entities": entity, "ticks": ticks, "seed": seed,
            "wall_pipeline_s": round(elapsed, 3), "wall_total_s": round(elapsed, 3),
            "peak_rss_kb": None, "tps": None, "timeout": True,
        }
    peak_kb = int(rusage.ru_maxrss)
    return {
        "scenario": "syne", "entities": entity, "ticks": ticks, "seed": seed,
        "wall_pipeline_s": round(elapsed, 3), "wall_total_s": round(elapsed, 3),
        "peak_rss_kb": peak_kb,
        "tps": round(ticks / elapsed, 1) if elapsed > 0 else None,
        "timeout": False,
    }


def run_syne_echos(
    args: argparse.Namespace,
    bin_path: Path,
    workdir: Path,
    database: Path,
    entity: int,
    ticks: int,
    seed: int,
) -> dict:
    """Pile SYNE + ECHOS complète (API, serveur, ingestion) via le contrôle."""
    python = _python()
    api_port = urlsplit(args.api_url).port or DEFAULT_API_PORT
    control_port = urlsplit(args.control_url).port or DEFAULT_PORT
    marker = workdir / f".ingest-started-{os.getpid()}"
    marker.unlink(missing_ok=True)
    agents_trace = workdir / f".agents-{os.getpid()}.parquet"
    agents_trace.unlink(missing_ok=True)

    api_cmd = [python, "-m", "uvicorn", "echos.api.app:app", "--app-dir", str(ROOT / "echos"),
               "--host", "127.0.0.1", "--port", str(api_port)]
    syne_cmd = [str(bin_path), "--serve", "--serve-port", str(control_port),
                "--observe-port", str(DEFAULT_OBSERVE_PORT)]
    ingest_cmd = [python, "-m", "echos.dev_ingest"]

    environment = os.environ.copy()
    environment.update(
        ECHOS_ANALYTICS_DB=str(database),
        ECHOS_ANALYSIS_EVERY=str(args.analysis_every),
        ECHOS_PARQUET_PATH=str(agents_trace) if args.parquet else "",
        SYNE_CONTROL_URL=args.control_url,
        SYNE_OBSERVABILITY_URL=f"ws://127.0.0.1:{DEFAULT_OBSERVE_PORT}/",
        LIVEX_WS_STARTED_FILE=str(marker),
        PYTHONPATH=str(ROOT / "echos") + os.pathsep + environment.get("PYTHONPATH", ""),
        LIVEX_INGEST_ONCE="1",
    )

    processes: list[subprocess.Popen] = []
    peaks: dict[str, int] = {}
    started_total = time.perf_counter()
    pipeline_elapsed: float | None = None
    try:
        processes.append(
            subprocess.Popen(api_cmd, cwd=ROOT, env=environment, start_new_session=True)
        )
        _wait_for(f"{args.api_url}/health", lambda value: value.get("status") == "ok",
                  args.cell_timeout, args.poll_interval)

        if not Path(syne_cmd[0]).is_file():
            raise FileNotFoundError(f"binaire SYNE absent : {syne_cmd[0]}")
        processes.append(
            subprocess.Popen(syne_cmd, cwd=ROOT, env=environment, start_new_session=True)
        )
        _wait_for(f"{args.control_url}/api/control/status", lambda value: "state" in value,
                  args.cell_timeout, args.poll_interval)

        processes.append(
            subprocess.Popen(ingest_cmd, cwd=ROOT, env=environment, start_new_session=True)
        )
        _wait_for_path(marker, args.cell_timeout, args.poll_interval)

        config: dict = {
            "simulation": {"ticksPerSecond": args.ticks_per_second},
            "agents": {"initialCount": entity},
        }
        started_run = time.perf_counter()
        started = _request_json(
            f"{args.control_url}/api/control/start",
            "POST",
            {"seed": seed, "maxTicks": ticks, "config": config},
        )
        run_id = started.get("runId")
        if not isinstance(run_id, str) or not run_id:
            raise RuntimeError(f"SYNE n'a pas renvoyé de runId : {started}")
        _wait_for(
            f"{args.control_url}/api/control/status",
            lambda value: value.get("state") == "finished" and value.get("tick", 0) >= ticks,
            args.cell_timeout, args.poll_interval,
        )
        drain_timeout = args.cell_timeout
        _wait_for(
            f"{args.api_url}/api/runs/{run_id}",
            lambda value: value.get("ticks_count", 0) >= max(ticks - 1, 0),
            drain_timeout, args.poll_interval,
        )
        _interrupt_syne(processes[1])
        _wait_for(
            f"{args.api_url}/api/runs/{run_id}/calibration",
            lambda value: value.get("status") == "complete",
            args.cell_timeout, args.poll_interval,
        )
        pipeline_elapsed = time.perf_counter() - started_run
    finally:
        # Le nettoyage doit TOUJOURS s'exécuter, y compris sur exception :
        # sinon les processus sont laissés en vie et squattent les ports.
        marker.unlink(missing_ok=True)
        for label, process in (("api", processes[0] if len(processes) > 0 else None),
                               ("syne", processes[1] if len(processes) > 1 else None),
                               ("ingest", processes[2] if len(processes) > 2 else None)):
            if process is None:
                continue
            rusage, ok = _wait_rusage(process.pid, 2.0)
            if not ok:
                rusage = _stop_and_collect(process.pid)
            peaks[label] = int(rusage.ru_maxrss) if rusage is not None else 0
        agents_trace.unlink(missing_ok=True)

    if pipeline_elapsed is None:
        raise TimeoutError(
            "cellule échouée : le run SYNE n'a pas atteint l'état 'finished' dans le délai. "
            "Suggestion : réduire --ticks-per-second (backpressure WS à haute cadence sur "
            "les grandes populations) ou augmenter --cell-timeout."
        )

    total_elapsed = time.perf_counter() - started_total
    total_peak = sum(peaks.values())
    return {
        "scenario": "echos", "entities": entity, "ticks": ticks, "seed": seed,
        "wall_pipeline_s": round(pipeline_elapsed, 3),
        "wall_total_s": round(total_elapsed, 3),
        "peak_rss_kb": total_peak,
        "peak_api_kb": peaks.get("api", 0),
        "peak_syne_kb": peaks.get("syne", 0),
        "peak_ingest_kb": peaks.get("ingest", 0),
        "tps": round(ticks / pipeline_elapsed, 1) if pipeline_elapsed > 0 else None,
        "startup_s": round(max(total_elapsed - pipeline_elapsed, 0.0), 3),
        "timeout": False,
    }


# --------------------------------------------------------------------------- #
# Rapports
# --------------------------------------------------------------------------- #


def _write_markdown(
    rows: list[dict], output_dir: Path, timestamp: str, args: argparse.Namespace
) -> Path:
    path = output_dir / f"benchmark-{timestamp}.md"
    lines = [
        "# Benchmark LIVEX — SYNE vs SYNE + ECHOS",
        "",
        f"- **Date** : {timestamp}",
        f"- **Binaire SYNE** : `{args.syne_bin}`",
        f"- **Cadence ECHOS** : `{args.ticks_per_second}` ticks/s",
        f"- **Analyse ECHOS** : 1 tick sur `{args.analysis_every}`",
        f"- **Série agents Parquet** : {'active' if args.parquet else 'inactive'}",
        f"- **Cellules** : {len(rows)} (scénarios et seeds confondus)",
        "",
        "## Méthodologie",
        "",
        "- **SYNE seul** : CLI `--headless` (moteur, aucune observabilité).",
        "- **SYNE + ECHOS** : `--serve` + API ECHOS + ingestion WebSocket ; temps "
        "pipeline = `POST /api/control/start` → calibration `complete`.",
        "- **RAM** : pic RSS de chaque processus enfant (`os.wait4` / `ru_maxrss`, "
        "Linux, Ko) ; pour ECHOS : *somme* des pics API + SYNE + ingestion.",
        "- **Débit** : ticks ÷ temps pipeline (ECHOS) ou temps mur (SYNE seul).",
        "- **Pacing** : le scénario ECHOS est borné à `ticks_per_second` "
        f"({args.ticks_per_second} t/s) — le « coût ECHOS (×) » mélange donc pacing "
        "et overhead ; comparer aussi la colonne « t/s », la mesure intrinsèque du pipeline.",
        "",
        "> Les temps sont des **moyennes sur les seeds** pour chaque "
        "« entités × ticks ». Voir le CSV brut pour les valeurs seed par seed.",
        "",
        "## Résultats (moyennes par entités × ticks)",
        "",
        "| Entités | Ticks | SYNE mur (s) | ECHOS pipeline (s) | Coût ECHOS (×) | "
        "SYNE RSS (Mo) | ECHOS RSS (Mo) | Coût RSS (×) | SYNE t/s | ECHOS t/s |",
        "|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]

    def mean(rows_filtered: list[dict], key: str) -> float | None:
        values = [
            row[key] for row in rows_filtered
            if row.get(key) is not None and not row.get("timeout")
        ]
        return sum(values) / len(values) if values else None

    cells: list[tuple[int, int]] = []
    for row in rows:
        pair = (row["entities"], row["ticks"])
        if pair not in cells:
            cells.append(pair)
    cells.sort()

    for entity, ticks in cells:
        syne_rows = [
            row for row in rows
            if row["scenario"] == "syne" and row["entities"] == entity and row["ticks"] == ticks
        ]
        echos_rows = [
            row for row in rows
            if row["scenario"] == "echos" and row["entities"] == entity and row["ticks"] == ticks
        ]
        syne_wall = mean(syne_rows, "wall_pipeline_s")
        echos_wall = mean(echos_rows, "wall_pipeline_s")
        syne_rss = mean(syne_rows, "peak_rss_kb")
        echos_rss = mean(echos_rows, "peak_rss_kb")
        syne_tps = mean(syne_rows, "tps")
        echos_tps = mean(echos_rows, "tps")
        ratio_time = (echos_wall / syne_wall) if syne_wall else None
        ratio_rss = (echos_rss / syne_rss) if syne_rss and echos_rss else None

        def fmt(value: float | None, decimals: int = 2) -> str:
            return "—" if value is None else f"{value:.{decimals}f}"

        def ratio_fmt(value: float | None) -> str:
            return "—" if value is None else f"{value:.2f} ×"

        lines.append(
            f"| {entity} | {ticks} | {fmt(syne_wall)} | {fmt(echos_wall)} "
            f"| {ratio_fmt(ratio_time)} | "
            f"| {fmt(syne_rss / 1024 if syne_rss else None)} | "
            f"| {fmt(echos_rss / 1024 if echos_rss else None)} | "
            f"| {ratio_fmt(ratio_rss)} | {fmt(syne_tps, 1)} | {fmt(echos_tps, 1)} |"
        )

    lines += [
        "",
        "## Détail seed par seed (SYNE + ECHOS)",
        "",
        "| Entités | Ticks | Seed | Pipeline (s) | Démarrage (s) | RSS total (Mo) | "
        "RSS API (Mo) | RSS SYNE (Mo) | RSS ingest (Mo) | t/s |",
        "|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for row in sorted(rows, key=lambda r: (r["scenario"], r["entities"], r["ticks"], r["seed"])):
        if row["scenario"] != "echos":
            continue
        rss_total = row["peak_rss_kb"] / 1024 if row["peak_rss_kb"] else None
        api = row.get("peak_api_kb", 0) / 1024
        syne_rss = row.get("peak_syne_kb", 0) / 1024
        ingest_rss = row.get("peak_ingest_kb", 0) / 1024
        lines.append(
            f"| {row['entities']} | {row['ticks']} | {row['seed']} | "
            f"| {row['wall_pipeline_s']:.2f} | {row.get('startup_s', 0)} | "
            f"| {fmt(rss_total)} | {api:.1f} | {syne_rss:.1f} | "
            f"| {ingest_rss:.1f} | {fmt(row['tps'], 1)} |"
        )

    lines += [
        "",
        "## Détail seed par seed (SYNE seul)",
        "",
        "| Entités | Ticks | Seed | Temps (s) | RSS (Mo) | t/s |",
        "|---:|---:|---:|---:|---:|---:|",
    ]
    for row in sorted(rows, key=lambda r: (r["scenario"], r["entities"], r["ticks"], r["seed"])):
        if row["scenario"] != "syne":
            continue
        rss_mb = row["peak_rss_kb"] / 1024 if row["peak_rss_kb"] else None
        lines.append(
            f"| {row['entities']} | {row['ticks']} | {row['seed']} | {row['wall_pipeline_s']:.2f} "
            f"| {fmt(rss_mb)} | {fmt(row['tps'], 1)} |"
        )

    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return path


def _metrics_from_rows(rows: list[dict], scenario: str) -> dict:
    subset = [row for row in rows if row["scenario"] == scenario and not row.get("timeout")]
    if not subset:
        return {}
    walls = [row["wall_pipeline_s"] for row in subset]
    rss = [row["peak_rss_kb"] for row in subset if row.get("peak_rss_kb") is not None]
    return {
        "cells": len(subset),
        "wall_total_s": round(sum(walls), 2),
        "wall_mean_s": round(sum(walls) / len(walls), 2),
        "rss_max_kb": max(rss) if rss else None,
        "rss_mean_kb": round(sum(rss) / len(rss), 2) if rss else None,
    }


def _write_summary(rows: list[dict], output_dir: Path, timestamp: str) -> Path:
    path = output_dir / f"benchmark-{timestamp}-summary.json"
    syne = _metrics_from_rows(rows, "syne")
    echos = _metrics_from_rows(rows, "echos")
    payload = {
        "timestamp": timestamp,
        "syne": syne,
        "echos": echos,
        "overhead": {
            "time_total_s": (
                round(echos["wall_total_s"] - syne["wall_total_s"], 2)
                if syne and echos else None
            ),
            "rss_max_ratio": (
                round(echos["rss_max_kb"] / syne["rss_max_kb"], 2)
                if syne and echos and syne["rss_max_kb"] and echos["rss_max_kb"]
                else None
            ),
            "rss_mean_ratio": (
                round(echos["rss_mean_kb"] / syne["rss_mean_kb"], 2)
                if syne and echos and syne["rss_mean_kb"] and echos["rss_mean_kb"]
                else None
            ),
        },
    }
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path


def _write_csv(rows: list[dict], output_dir: Path, timestamp: str) -> Path:
    path = output_dir / f"benchmark-{timestamp}.csv"
    fields = [
        "scenario", "entities", "ticks", "seed",
        "wall_pipeline_s", "wall_total_s", "startup_s",
        "peak_rss_kb", "peak_api_kb", "peak_syne_kb", "peak_ingest_kb",
        "tps", "timeout",
    ]
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
    return path


# --------------------------------------------------------------------------- #
# Orchestration
# --------------------------------------------------------------------------- #


def _resolve_ports(args: argparse.Namespace) -> list[int]:
    ports = [
        urlsplit(args.api_url).port or DEFAULT_API_PORT,
        urlsplit(args.control_url).port or DEFAULT_PORT,
        DEFAULT_OBSERVE_PORT,
    ]
    return list(dict.fromkeys(ports))


def _port_busy(port: int) -> bool:
    import socket

    probe = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    probe.settimeout(0.3)
    try:
        return probe.connect_ex(("127.0.0.1", port)) == 0
    finally:
        probe.close()


def _assert_ports_free(args: argparse.Namespace) -> None:
    """Échoue immédiatement si la pile benchmark est déjà en cours — évite de
    squatter/déranger une instance existante ou d'être trompé par des orphelins."""
    busy = [port for port in _resolve_ports(args) if _port_busy(port)]
    if busy:
        raise RuntimeError(
            "ports déjà occupés : " + ", ".join(map(str, busy))
            + " — une pile SYNE/ECHOS (ou des orphelins d'un run précédent) tourne encore.\n"
            "Vérifiez/arrêtez avant de relancer : "
            "`ps aux | grep -E 'uvicorn|dev_ingest|Simulation.Console'`."
        )


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    if args.quick:
        args.seeds = DEFAULT_SEEDS[:1]
        args.entities = DEFAULT_ENTITIES[:2]
        args.ticks = DEFAULT_TICKS[:2]
    if not Path(args.syne_bin).is_file():
        print(f"Erreur: binaire SYNE absent : {args.syne_bin} (compilez scripts/dev-stack.sh)",
              file=sys.stderr)
        return 2
    _assert_ports_free(args)

    bin_path = Path(args.syne_bin)
    timestamp = time.strftime("%Y%m%d-%H%M%S")
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    workdir = output_dir / f".bench-work-{timestamp}"
    workdir.mkdir(parents=True, exist_ok=True)

    configs = _config_files(workdir, list(args.entities))
    database = args.database or (workdir / "bench.sqlite")
    database.parent.mkdir(parents=True, exist_ok=True)

    rows: list[dict] = []
    try:
        for entity in args.entities:
            for ticks in args.ticks:
                config_path = configs[entity]
                for seed in args.seeds:
                    label = f"entités={entity} ticks={ticks} seed={seed}"
                    print(f"[SYNE seul] {label}", flush=True)
                    row_syne = run_syne_only(args, bin_path, config_path, entity, ticks, seed)
                    rows.append(row_syne)
                    print(
                        f"  -> {row_syne['wall_pipeline_s']:.2f} s · "
                        f"RSS {row_syne['peak_rss_kb']/1024:.0f} Mo"
                        if row_syne["peak_rss_kb"] else "  -> timeout",
                        flush=True,
                    )
                    time.sleep(0.3)

                    print(f"[SYNE+ECHOS] {label}", flush=True)
                    try:
                        row_echos = run_syne_echos(
                            args, bin_path, workdir, database, entity, ticks, seed
                        )
                    except TimeoutError:
                        raise
                    except Exception as exc:
                        # La pile ECHOS est déjà arrêtée par le finally interne ;
                        # on ne laisse pas un échec de cellule empoisonner la suite.
                        print(
                            f"  -> cellule ignorée ({type(exc).__name__}: {exc})",
                            flush=True,
                        )
                        continue
                    rows.append(row_echos)
                    print(
                        f"  -> pipeline {row_echos['wall_pipeline_s']:.2f} s · "
                        f"RSS total {row_echos['peak_rss_kb']/1024:.0f} Mo · "
                        f"t/s {row_echos['tps']}",
                        flush=True,
                    )
                    time.sleep(0.5)
    except TimeoutError:
        print(
            "\nAbandon : cellule dépassée (TimeoutError). "
            "Aucun processus n'a été laissé en vie.",
            file=sys.stderr,
        )
        raise

    csv_path = _write_csv(rows, output_dir, timestamp)
    md_path = _write_markdown(rows, output_dir, timestamp, args)
    summary_path = _write_summary(rows, output_dir, timestamp)

    # Le répertoire de travail (configs, base temporaire) est conservé
    # pour reproductibilité : se relancer ne dépend que des seeds figées.
    print("\nBenchmark terminé :")
    print(f"  - CSV brut        : {csv_path}")
    print(f"  - Rapport Markdown: {md_path}")
    print(f"  - Résumé JSON     : {summary_path}")
    total_syne = _metrics_from_rows(rows, "syne")
    total_echos = _metrics_from_rows(rows, "echos")
    if total_syne and total_echos:
        print(
            "\nTotaux: "
            f"SYNE {total_syne['wall_total_s']} s / "
            f"{total_syne['rss_max_kb']/1024:.0f} Mo max | "
            f"SYNE+ECHOS {total_echos['wall_total_s']} s / "
            f"{total_echos['rss_max_kb']/1024:.0f} Mo max"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
