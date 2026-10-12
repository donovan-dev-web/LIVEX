#!/usr/bin/env python3
"""Exécute une simulation SYNE déterministe et génère son rapport ECHOS.

Ce module est délibérément indépendant de la pile de développement
navigateur : il ne démarre que l'API ECHOS, le serveur de contrôle et
d'observabilité SYNE et le consommateur WebSocket, puis les arrête tous
les trois à la fin du run (ou en cas d'erreur).
"""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import shlex
import signal
import subprocess
import sys
import time
from typing import Sequence
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen
from urllib.parse import urlsplit


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DB = ROOT / "echos" / "data" / "livex-analytics.sqlite"
DEFAULT_OUTPUT = ROOT / "echos" / "data" / "reports"
PROFILES = {
    "raw": ROOT / "configs" / "simulation" / "raw.json",
    "reference": ROOT / "configs" / "simulation" / "reference.json",
}


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Lance un run SYNE contrôlé, attend son ingestion ECHOS et génère le rapport.",
    )
    parser.add_argument("seed", type=_seed, help="seed entière non négative")
    parser.add_argument("ticks", type=_ticks, help="nombre de ticks (> 0)")
    parser.add_argument(
        "--ticks-per-second",
        type=_ticks_per_second,
        default=10,
        help="cadence SYNE du run batch (défaut: 10; augmenter seulement si ECHOS suit)",
    )
    parser.add_argument(
        "--analysis-every",
        type=_ticks_per_second,
        default=1,
        help=(
            "planifie l'analyse ECHOS (8 moteurs + contextes) 1 tick sur N "
            "(défaut: 1 = chaque tick; N>1 réduit la RAM/CPU au détriment de la "
            "granularité des séries métriques)"
        ),
    )
    parser.add_argument(
        "--parquet",
        action=argparse.BooleanOptionalAction,
        default=True,
        help=(
            "conserve la trace .parquet complète (remplace le .json); le rapport "
            ".md est toujours généré (défaut: activé)"
        ),
    )
    parser.add_argument("--database", type=Path, default=DEFAULT_DB, help="base SQLite ECHOS")
    parser.add_argument(
        "--config",
        type=Path,
        help="fichier JSON de configuration SYNE à fusionner avec les défauts",
    )
    parser.add_argument(
        "--profile",
        choices=sorted(PROFILES),
        default="reference",
        help="profil fourni par le dépôt (défaut: reference; remplace --config)",
    )
    parser.add_argument(
        "--output-dir", type=Path, default=DEFAULT_OUTPUT, help="répertoire des rapports"
    )
    parser.add_argument("--api-url", default="http://127.0.0.1:5000")
    parser.add_argument("--control-url", default="http://127.0.0.1:5181")
    parser.add_argument("--syne-bin", type=Path, help="binaire Simulation.Console déjà compilé")
    parser.add_argument(
        "--report-generator",
        help="commande du générateur (défaut: python -m echos.reporting)",
    )
    parser.add_argument("--timeout", type=_seconds, default=300.0, help="délai maximal en secondes")
    parser.add_argument(
        "--drain-timeout",
        type=_seconds,
        help="délai maximal d'ingestion ECHOS (défaut: --timeout)",
    )
    parser.add_argument("--poll-interval", type=_seconds, default=0.2, help=argparse.SUPPRESS)
    return parser.parse_args(argv)


def _seed(value: str) -> int:
    try:
        parsed = int(value, 10)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("seed doit être une entière") from exc
    if parsed < 0:
        raise argparse.ArgumentTypeError("seed doit être >= 0")
    return parsed


def _ticks(value: str) -> int:
    try:
        parsed = int(value, 10)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("ticks doit être une entière") from exc
    if parsed <= 0:
        raise argparse.ArgumentTypeError("ticks doit être > 0")
    return parsed


def _seconds(value: str) -> float:
    try:
        parsed = float(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("le délai doit être numérique") from exc
    if parsed <= 0:
        raise argparse.ArgumentTypeError("le délai doit être > 0")
    return parsed


def _ticks_per_second(value: str) -> int:
    try:
        parsed = int(value, 10)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("la cadence doit être une entière") from exc
    if parsed <= 0:
        raise argparse.ArgumentTypeError("la cadence doit être > 0")
    return parsed


def request_json(url: str, method: str = "GET", payload: dict | None = None) -> dict:
    data = None if payload is None else json.dumps(payload).encode()
    request = Request(url, data=data, method=method, headers={"Content-Type": "application/json"})
    with urlopen(request, timeout=5) as response:
        body = response.read()
    value = json.loads(body)
    if not isinstance(value, dict):
        raise ValueError(f"réponse JSON inattendue de {url}")
    return value


def wait_for(url: str, predicate, processes: Sequence[subprocess.Popen], timeout: float, interval: float) -> dict:
    deadline = time.monotonic() + timeout
    last_error: Exception | None = None
    while time.monotonic() < deadline:
        for process in processes:
            if process.poll() is not None:
                raise RuntimeError(f"le processus {process.args!r} s'est arrêté ({process.returncode})")
        try:
            value = request_json(url)
            if predicate(value):
                return value
        except (OSError, ValueError, HTTPError, URLError) as exc:
            last_error = exc
        time.sleep(interval)
    detail = f" ({last_error})" if last_error else ""
    raise TimeoutError(f"délai dépassé en attendant {url}{detail}")


def wait_for_path(
    path: Path, processes: Sequence[subprocess.Popen], timeout: float, interval: float
) -> None:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        for process in processes:
            if process.poll() is not None:
                raise RuntimeError(f"le processus {process.args!r} s'est arrêté ({process.returncode})")
        if path.exists():
            return
        time.sleep(interval)
    raise TimeoutError(f"délai dépassé en attendant {path}")


def _python() -> str:
    candidate = ROOT / "echos" / ".venv" / "bin" / "python"
    return str(candidate) if candidate.exists() else sys.executable


def _terminate(processes: Sequence[subprocess.Popen]) -> None:
    for process in reversed(processes):
        if process.poll() is None:
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except (ProcessLookupError, PermissionError):
                process.terminate()
    deadline = time.monotonic() + 5
    for process in processes:
        if process.poll() is None:
            try:
                process.wait(timeout=max(0.1, deadline - time.monotonic()))
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except (ProcessLookupError, PermissionError):
                    process.kill()
                process.wait()


def _interrupt(process: subprocess.Popen) -> None:
    """Stop a console server gracefully so WebSocket clients receive close."""
    if process.poll() is not None:
        return
    try:
        os.killpg(process.pid, signal.SIGINT)
        process.wait(timeout=10)
    except (ProcessLookupError, PermissionError, subprocess.TimeoutExpired):
        _terminate([process])


def _command(args: argparse.Namespace) -> tuple[list[str], list[str], list[str]]:
    python = _python()
    api_port = urlsplit(args.api_url).port or 5000
    control_port = urlsplit(args.control_url).port or 5181
    api = [python, "-m", "uvicorn", "echos.api.app:app", "--app-dir", str(ROOT / "echos"),
           "--host", "127.0.0.1", "--port", str(api_port)]
    syne = [str(args.syne_bin or ROOT / "syne" / "Simulation.Console" / "bin" /
                "Release" / "net10.0" / "Simulation.Console"), "--serve",
            "--serve-port", str(control_port), "--observe-port", "5180"]
    ingest = [python, "-m", "echos.dev_ingest"]
    return api, syne, ingest


def run(args: argparse.Namespace) -> int:
    args.database.parent.mkdir(parents=True, exist_ok=True)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    marker = args.output_dir / f".ingest-started-{os.getpid()}"
    marker.unlink(missing_ok=True)
    agents_trace = args.output_dir / f".agents-{os.getpid()}.parquet"
    agents_trace.unlink(missing_ok=True)
    api, syne, ingest = _command(args)
    environment = os.environ.copy()
    environment.update(
        ECHOS_ANALYTICS_DB=str(args.database),
        ECHOS_ANALYSIS_EVERY=str(args.analysis_every),
        # La série Parquet par agent (ECHOS-013) est conservée comme trace
        # complète supplémentaire : écrite dans un chemin temporaire tant que le
        # run_id est inconnu, puis renommée en <run_id>.agents.parquet une fois
        # le run identifié.
        ECHOS_PARQUET_PATH=str(agents_trace) if args.parquet else "",
        SYNE_CONTROL_URL=args.control_url,
        SYNE_OBSERVABILITY_URL="ws://127.0.0.1:5180/",
        LIVEX_WS_STARTED_FILE=str(marker),
        PYTHONPATH=str(ROOT / "echos") + os.pathsep + environment.get("PYTHONPATH", ""),
        # Un batch terminé n'a qu'un flux WebSocket fini. Se reconnecter après
        # la fermeture du serveur ne créerait qu'une boucle infinie bruyante.
        LIVEX_INGEST_ONCE="1",
    )
    processes: list[subprocess.Popen] = []
    try:
        processes.append(subprocess.Popen(api, cwd=ROOT, env=environment, start_new_session=True))
        wait_for(f"{args.api_url}/health", lambda value: value.get("status") == "ok",
                 processes, args.timeout, args.poll_interval)

        if not Path(syne[0]).is_file():
            raise FileNotFoundError(f"binaire SYNE absent : {syne[0]} (compilez scripts/dev-stack.sh)")
        processes.append(subprocess.Popen(syne, cwd=ROOT, env=environment, start_new_session=True))
        wait_for(f"{args.control_url}/api/control/status", lambda value: "state" in value,
                 processes, args.timeout, args.poll_interval)

        processes.append(subprocess.Popen(ingest, cwd=ROOT, env=environment, start_new_session=True))
        wait_for_path(marker, processes, args.timeout, args.poll_interval)

        config: dict = {
            "simulation": {"ticksPerSecond": args.ticks_per_second}
        }
        config_path = args.config
        if args.config is None and args.profile is not None:
            config_path = PROFILES[args.profile]
        if config_path is not None:
            try:
                supplied = json.loads(config_path.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError) as exc:
                raise ValueError(f"configuration invalide : {config_path}") from exc
            if not isinstance(supplied, dict):
                raise ValueError("la configuration batch doit être un objet JSON")
            config = _merge_config(config, supplied)
        started = request_json(
            f"{args.control_url}/api/control/start",
            "POST",
            {"seed": args.seed, "maxTicks": args.ticks, "config": config},
        )
        run_id = started.get("runId")
        if not isinstance(run_id, str) or not run_id:
            raise RuntimeError(f"SYNE n'a pas renvoyé de runId : {started}")
        status = wait_for(
            f"{args.control_url}/api/control/status",
            lambda value: value.get("state") == "finished" and value.get("tick", 0) >= args.ticks,
            processes, args.timeout, args.poll_interval,
        )
        if processes[2].poll() is not None:
            raise RuntimeError(
                "ECHOS a fermé le flux WebSocket avant la fin du run; "
                "aucun rapport complet ne peut être généré"
            )
        # Le flux groupe un tick quand le snapshot suivant arrive : le dernier
        # tick n'est donc vidé qu'à la fermeture du WebSocket. On draine d'abord
        # chaque snapshot précédent tant que SYNE est connecté ; sinon la fermeture
        # de la socket peut jeter des trames en attente de transport.
        drain_timeout = args.drain_timeout or args.timeout
        try:
            wait_for(
                f"{args.api_url}/api/runs/{run_id}",
                lambda value: value.get("ticks_count", 0) >= max(args.ticks - 1, 0),
                [processes[0], processes[2]],
                drain_timeout,
                args.poll_interval,
            )
        except TimeoutError as exc:
            try:
                observed = request_json(f"{args.api_url}/api/runs/{run_id}")
                ticks_count = observed.get("ticks_count", "inconnu")
            except (OSError, ValueError, HTTPError, URLError):
                ticks_count = "inconnu"
            raise TimeoutError(
                f"drainage ECHOS incomplet pour le run {run_id}: "
                f"{ticks_count}/{args.ticks - 1} ticks préalables ingérés après "
                f"{drain_timeout:g}s"
            ) from exc
        _interrupt(processes[1])
        # La calibration est écrite par le pipeline d'ingestion après que le
        # WebSocket fermé a vidé le dernier tick.
        wait_for(f"{args.api_url}/api/runs/{run_id}/calibration",
                 lambda value: value.get("status") == "complete",
                 [processes[0]], args.timeout, args.poll_interval)
        # Le rapport est désormais durable ; on arrête le consommateur avant sa
        # reconnexion normale pour éviter le bruit de connexion refusée au nettoyage.
        _terminate([processes[2]])
        if args.parquet and agents_trace.exists():
            agents_trace.replace(args.output_dir / f"{run_id}.agents.parquet")

        generator = (
            shlex.split(args.report_generator)
            if args.report_generator
            else [_python(), "-m", "echos.reporting"]
        )
        generator += [
            str(args.database),
            run_id,
            "--output-dir",
            str(args.output_dir),
            "--seed",
            str(args.seed),
        ]
        if args.parquet:
            generator.append("--parquet")
        completed = subprocess.run(generator, cwd=ROOT, env=environment, check=False)
        if completed.returncode:
            raise RuntimeError(f"générateur de rapport arrêté avec le code {completed.returncode}")
        artifacts = [str(args.output_dir / f"{run_id}.{ext}") for ext in ("parquet", "md")]
        if args.parquet:
            artifacts.append(str(args.output_dir / f"{run_id}.agents.parquet"))
        print(f"Run {run_id} terminé à {status.get('tick')} ticks; artefacts dans {args.output_dir}:")
        for artifact in artifacts:
            print(f"  - {artifact}")
        return 0
    finally:
        marker.unlink(missing_ok=True)
        agents_trace.unlink(missing_ok=True)
        _terminate(processes)


def main(argv: Sequence[str] | None = None) -> int:
    try:
        return run(parse_args(argv))
    except KeyboardInterrupt:
        print("\nRun interrompu par l'utilisateur.", file=sys.stderr)
        return 130
    except (OSError, RuntimeError, TimeoutError, ValueError) as exc:
        print(f"Erreur: {exc}", file=sys.stderr)
        return 1


def _merge_config(base: dict, override: dict) -> dict:
    merged = dict(base)
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(merged.get(key), dict):
            merged[key] = _merge_config(merged[key], value)
        else:
            merged[key] = value
    return merged


if __name__ == "__main__":
    raise SystemExit(main())
