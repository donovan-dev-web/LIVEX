#!/usr/bin/env python3
"""Campagne V2′ par le chemin ``reset`` (ROADMAP-V01 étape 2).

La campagne ADR-016 du 07/10 n'a validé que le chemin ``prepare`` + ``start``.
Ce script rejoue le même protocole (50 et 100 agents × 2500 ticks × 3 seeds)
en enchaînant les runs **via ``POST /api/control/reset``** : le premier run part
par ``start``, les suivants par ``reset`` — population et profil compris, puisque
``reset`` accepte désormais une surcouche ``config`` (correctif du chemin reset,
commit ``ef878f21``).

Le flux d'observabilité est consommé en direct sur WebSocket et segmenté par
``runId`` (porté par les snapshots) ; l'analyse réutilise ``analyse()`` de
``calibration-campaign.py`` pour rester strictement comparable à la campagne du
07/10. Critères ADR-016 inchangés :

1. zéro extinction ;
2. |pente d'énergie moyenne| < 0,005/tick (200 derniers ticks) ;
3. population finale ≥ 90 % de la population initiale.

Usage :
    python3 scripts/reset-campaign.py \\
        --populations 50,100 --seeds 12345,424242,999 --ticks 2500 \\
        --out docs/campaign-runs/v2r-reset
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import subprocess
import sys
import threading
import time
from pathlib import Path
from typing import Sequence

import requests
from websockets.sync.client import connect as ws_connect

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BIN = ROOT / "syne" / "Simulation.Console" / "bin" / "Release" / "net10.0" / "Simulation.Console"
CALIBRATION_SCRIPT = ROOT / "scripts" / "calibration-campaign.py"

# Critères ADR-016 (identiques à calibration-campaign.py, mais population
# initiale stricte : la campagne du 07/10 a constaté 0 mort sur 6/6).
SLOPE_TOLERANCE = 0.005
MIN_SURVIVOR_RATIO = 0.9
SETTLE_SECONDS = 3.0
POLL_SECONDS = 2.0


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--populations", default="50,100", help="populations séparées par des virgules")
    parser.add_argument("--seeds", default="12345,424242,999", help="graines séparées par des virgules")
    parser.add_argument("--ticks", type=int, default=2500, help="horizon en ticks (défaut 2500)")
    parser.add_argument("--out", type=Path, required=True, help="répertoire de campagne (disque réel recommandé)")
    parser.add_argument("--bin", type=Path, default=DEFAULT_BIN)
    parser.add_argument("--serve-port", type=int, default=5181)
    parser.add_argument("--observe-port", type=int, default=5180)
    parser.add_argument("--run-timeout", type=float, default=1800.0, help="délai maximum par run (s)")
    parser.add_argument("--keep-stream", action="store_true", help="conserve les flux segmentés (lourd)")
    return parser.parse_args(argv)


def load_analyse():
    """Réutilise analyse() de calibration-campaign.py (signaux identiques)."""
    spec = importlib.util.spec_from_file_location("calibration_campaign", CALIBRATION_SCRIPT)
    if spec is None or spec.loader is None:
        raise SystemExit(f"script introuvable : {CALIBRATION_SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.analyse


class Recorder:
    """Consomme le WebSocket d'observabilité et segmente les trames par runId.

    Les snapshots portent ``runId`` ; les événements (``decision_made``,
    ``agent_died``...) n'en portent pas et sont rattachés au run courant — sans
    ambiguïté car le snapshot précède toujours les événements de son tick et que
    les runs sont séquentiels.
    """

    def __init__(self, uri: str, out: Path):
        self._uri = uri
        self._out = out
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._run, name="ws-recorder", daemon=True)
        self._handle = None
        self._current_run: str | None = None
        self._received: dict[str, int] = {}
        self.error: BaseException | None = None

    def start(self) -> None:
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        self._thread.join(timeout=10)
        self._close()

    def _close(self) -> None:
        if self._handle is not None:
            self._handle.close()
            self._handle = None

    def _path(self, run_id: str) -> Path:
        safe = "".join(c if c.isalnum() or c in "-_" else "_" for c in run_id)
        return self._out / "streams" / f"{safe}.jsonl"

    def _run(self) -> None:
        (self._out / "streams").mkdir(parents=True, exist_ok=True)
        while not self._stop.is_set():
            try:
                with ws_connect(self._uri, max_size=16 * 1024 * 1024, open_timeout=10) as ws:
                    while not self._stop.is_set():
                        try:
                            message = ws.recv(timeout=5)
                        except TimeoutError:
                            continue  # le serveur n'a rien émis : on reste branché
                        if isinstance(message, bytes):
                            message = message.decode("utf-8", "replace")
                        self._write(message)
            except Exception as exc:  # noqa: BLE001 — reconnexion bornée
                if self._stop.is_set():
                    return
                self.error = exc
                time.sleep(0.5)
        self._close()

    def _write(self, message: str) -> None:
        run_id = None
        if message.startswith('{"type":"snapshot"'):
            try:
                run_id = json.loads(message).get("runId")
            except json.JSONDecodeError:
                run_id = None
        if run_id:
            if run_id != self._current_run:
                self._close()
                self._current_run = run_id
                self._received.setdefault(run_id, 0)
            handle = self._handle
            if handle is None:
                handle = open(self._path(run_id), "a", encoding="utf-8")
                self._handle = handle
            handle.write(message + "\n")
            self._received[run_id] = self._received.get(run_id, 0) + 1
        elif self._handle is not None:
            self._handle.write(message + "\n")

    def flush(self) -> None:
        if self._handle is not None:
            self._handle.flush()

    def frames_for(self, run_id: str) -> int:
        return self._received.get(run_id, 0)


def wait_ready(base: str, timeout: float = 30.0) -> dict:
    deadline = time.monotonic() + timeout
    last_error: Exception | None = None
    while time.monotonic() < deadline:
        try:
            response = requests.get(f"{base}/api/control/status", timeout=2)
            if response.ok:
                return response.json()
        except requests.RequestException as exc:
            last_error = exc
        time.sleep(0.3)
    raise SystemExit(f"serveur SYNE indisponible après {timeout}s : {last_error}")


def post(base: str, action: str, payload: dict) -> dict:
    response = requests.post(f"{base}/api/control/{action}", json=payload, timeout=30)
    body = response.json()
    if not response.ok or body.get("ok") is False:
        raise SystemExit(f"{action} en échec (HTTP {response.status_code}) : {body}")
    return body


def wait_finished(base: str, timeout: float) -> dict:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        status = requests.get(f"{base}/api/control/status", timeout=5).json()
        state = status.get("state")
        if state == "finished":
            return status
        if state in {"idle"}:
            raise SystemExit(f"run interrompu (état {state}) avant horizon")
        time.sleep(POLL_SECONDS)
    raise SystemExit(f"run non terminé en {timeout:.0f}s")


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    if not args.bin.exists():
        raise SystemExit(f"binaire introuvable : {args.bin} — dotnet build syne/Syne.sln -c Release d'abord")

    populations = [int(p) for p in args.populations.split(",") if p.strip()]
    seeds = [int(s) for s in args.seeds.split(",") if s.strip()]
    analyse = load_analyse()

    out: Path = args.out
    out.mkdir(parents=True, exist_ok=True)
    (out / "syne-serve.log").write_text("", encoding="utf-8")

    serve_cmd = [
        str(args.bin),
        "--serve",
        "--serve-port", str(args.serve_port),
        "--observe-port", str(args.observe_port),
    ]
    log = open(out / "syne-serve.log", "a", encoding="utf-8")
    syne = subprocess.Popen(serve_cmd, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
    base = f"http://127.0.0.1:{args.serve_port}"
    recorder: Recorder | None = None
    results: dict[str, dict] = {}
    chain: list[str] = []

    try:
        wait_ready(base)
        recorder = Recorder(f"ws://127.0.0.1:{args.observe_port}/", out)
        recorder.start()
        time.sleep(0.5)

        first = True
        for population in populations:
            for seed in seeds:
                payload = {
                    "seed": seed,
                    "maxTicks": args.ticks,
                    "config": {"agents": {"initialCount": population}},
                }
                action = "start" if first else "reset"
                body = post(base, action, payload)
                run_id = body["runId"]
                chain.append(f"{action}:{population}/{seed}")
                print(f"[{action}] pop={population} seed={seed} runId={run_id} — enchaînement…", flush=True)

                status = wait_finished(base, args.run_timeout)
                time.sleep(SETTLE_SECONDS)
                recorder.flush()

                stream = recorder._path(run_id)
                if not stream.exists() or recorder.frames_for(run_id) == 0:
                    raise SystemExit(f"aucun flux enregistré pour {run_id} ( WS ?) — voir {out / 'syne-serve.log'}")

                report = analyse(stream)
                report["runId"] = run_id
                report["chainedBy"] = action
                report["observedFrames"] = recorder.frames_for(run_id)
                report["finalState"] = status.get("state")
                results[f"{population}/{seed}"] = report
                if not args.keep_stream:
                    stream.unlink(missing_ok=True)

                print(
                    f"    vivants={report['aliveFinal']}/{population} "
                    f"extinction={report['extinctionTick'] or '-'} "
                    f"pente_E={report['energySlopePerTick']:+.4f}/tick "
                    f"morts={report['deaths']} naiss={report['births']} "
                    f"[état {report['finalState']}]", flush=True,
                )
                (out / "campaign.json").write_text(
                    json.dumps({"chain": chain, "results": results}, indent=2), encoding="utf-8")
                first = False

        print()
        print(f"=== campagne reset {out.name} | {args.ticks} ticks | "
              f"{len(populations)} pop × {len(seeds)} seeds | chaîne : {' → '.join(chain)} ===")

        failures = []
        for key, report in results.items():
            population = int(key.split("/")[0])
            if report["extinctionTick"] is not None:
                failures.append(f"{key} : extinction t{report['extinctionTick']} ({report['deathCauses']})")
            elif abs(report["energySlopePerTick"]) >= SLOPE_TOLERANCE:
                failures.append(f"{key} : pente énergie {report['energySlopePerTick']:+.4f}/tick (|pente| ≥ {SLOPE_TOLERANCE})")
            elif report["aliveFinal"] < population * MIN_SURVIVOR_RATIO:
                failures.append(f"{key} : population {report['aliveFinal']}/{population} (< {MIN_SURVIVOR_RATIO:.0%})")

        if failures:
            print("CRITÈRES ADR-016 NON ATTEINTS :")
            for failure in failures:
                print(f"  ✗ {failure}")
            return 1

        print(f"TOUS LES CRITÈRES ATTEINTS (par reset) : 0 extinction, |pente| < "
              f"{SLOPE_TOLERANCE}/tick, population ≥ {MIN_SURVIVOR_RATIO:.0%} sur {len(results)} runs")
        for key, report in results.items():
            print(f"  ✓ {key}: {report['aliveFinal']} vivants, pente {report['energySlopePerTick']:+.4f}/tick, "
                  f"morts={report['deaths']}")
        return 0
    finally:
        if recorder is not None:
            recorder.stop()
        syne.terminate()
        try:
            syne.wait(timeout=15)
        except subprocess.TimeoutExpired:
            syne.kill()
        log.close()


if __name__ == "__main__":
    sys.exit(main())
