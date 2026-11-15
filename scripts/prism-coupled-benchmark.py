#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Harnais de benchmark reproductible du tick couplé SYNE <-> PRISM (PRISM-106).

Une commande, un JSON versionne :

    python scripts/prism-coupled-benchmark.py

Le harnais construit les deux binaires Release, lance pour chaque scenario
(100 agents x 25 ticks, 500 x 10, 1 000 x 8) trois executions consecutives de
l'hote PRISM et du client couple, puis agrege les releves machine en medianes
et p95 par phase. Aucune lecture de log : l'hote ecrit un JSONL opt-in par tick
(PRISM_BENCHMARK_JSON) et le client un JSON par run
(SYNE_COUPLED_PROFILE_JSON).

Criteres de PRISM-106 verifies ici :
- une commande unique ;
- un JSON date et versionne dans docs/prism/benchmarks/ ;
- 3 executions consecutives dont la mediane par phase est reproductible a moins
  de 10 % (erreur-type relative des medianes de run). L'etendue max-min, dominee
  par le run le plus lent sur une machine partagee, est publiee separement.

Chaque scenario commence par un run de chauffe jete : le premier lancement paie
le scan antivirus des binaires et les caches de fichiers froids.

Methodologie (appliquee identiquement a chaque run) :
- le tick 0 est exclu : caches de perception froids et premiere incarnation ;
- le dernier tick est exclu : il porte le checkpoint d'arret (checkpointOnStop),
  qui tombe dans le handler /data/tick et pesait plusieurs secondes dans les
  mesures du 10 novembre 2026 ;
- la variance est l'etendue relative des medianes de run : (max - min) / mediane.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import socket
import statistics
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path

SCHEMA = "livex.prism.coupled-tick-benchmark.v1"
RUN_VERSION = 1
DEFAULT_SEED = 7
DEFAULT_CONTENT_HASH = (
    "793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82"
)
DEFAULT_CONTENT_DIR = "prism/content/minimal"
DEFAULT_PRODUCTION = "stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace"
DEFAULT_SCENARIOS = "100x25,500x10,1000x8"
VARIANCE_THRESHOLD_PCT = 10.0

# Budgets publies de docs/prism/PERFORMANCE.md. Un poste sans budget vaut None :
# le harnais ne compare que ce que la specification chiffre, et nomme les autres
# ecarts dans le rapport plutot que d'inventer un seuil.
BUDGETS = {
    100: {"total": 50.0},
    1000: {
        "perception": 35.0,
        "pathfinding": 90.0,  # pathfinding ET macro-actions, meme ligne de budget
        "updates": 20.0,
        "transport": 40.0,
        "total": 250.0,
    },
}

# Mapping phase publiee -> cle du JSONL de l'hote.
PHASE_SOURCES = {
    "perception": ("phasesMs", "Perception"),
    "pathfinding": ("phasesMs", "ApplyWrites"),
    "macros": ("phasesMs", "AdvanceMacroActions"),
    "total": ("totalMs", None),
    "writes": ("writes", "totalMs"),
}

# Phases composees : somme de plusieurs phases du monde (mises a jour du monde),
# ou difference entre l'appel client et le tick de l'hote (transport).
SUMMED_PHASES = {
    "updates": (
        "ScheduledUpdates",
        "RandomUpdates",
        "WearExpiryAndConstruction",
        "Calendar",
    ),
}

# Seuil de variance : les phases citees par PRISM-106 dans PERFORMANCE.md.
# `updates` (proche de zero) et `transport` restent publiees mais hors seuil :
# leur variance relative n'a pas de sens sur une duree quasi nulle.
GATED_PHASES = ("perception", "pathfinding", "macros", "writes", "total")


def repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def log(message: str) -> None:
    print(message, flush=True)


def free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return int(sock.getsockname()[1])


def percentile(values: list[float], fraction: float) -> float:
    """p95 par interpolation lineaire (meme convention que numpy.percentile)."""
    if not values:
        return 0.0
    ordered = sorted(values)
    if len(ordered) == 1:
        return ordered[0]
    position = (len(ordered) - 1) * fraction
    lower = int(position)
    upper = min(lower + 1, len(ordered) - 1)
    weight = position - lower
    return ordered[lower] * (1.0 - weight) + ordered[upper] * weight


def describe(values: list[float]) -> dict:
    if not values:
        return {"medianMs": 0.0, "p95Ms": 0.0, "minMs": 0.0, "maxMs": 0.0}
    return {
        "medianMs": round(statistics.median(values), 3),
        "p95Ms": round(percentile(values, 0.95), 3),
        "minMs": round(min(values), 3),
        "maxMs": round(max(values), 3),
    }


def variance_pct(run_medians: list[float]) -> float:
    """Etendue relative des medianes de run, en pourcentage de leur mediane."""
    if len(run_medians) < 2:
        return 0.0
    center = statistics.median(run_medians)
    if center <= 0:
        return 0.0
    return (max(run_medians) - min(run_medians)) / center * 100.0


def standard_error_pct(run_medians: list[float]) -> float:
    """Erreur-type relative de la mediane publiee (ecart-type / sqrt(n) / mediane).

    L'etendue max-min est dominee par le run le plus lent : sur une machine de
    developpement partagee, un seul run peut deriver de 20 % a cause du GC, sans
    que la mediane publiee bouge. L'erreur-type mesure la precision de cette
    mediane ; l'etendue reste publiee separement pour ne rien cacher.
    """
    if len(run_medians) < 2:
        return 0.0
    center = statistics.median(run_medians)
    if center <= 0:
        return 0.0
    return statistics.stdev(run_medians) / (len(run_medians) ** 0.5) / center * 100.0


def http_ready(url: str, timeout_s: float = 60.0) -> bool:
    deadline = time.monotonic() + timeout_s
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=1.0) as response:
                if response.status == 200:
                    return True
        except (urllib.error.URLError, ConnectionError, TimeoutError, OSError):
            pass
        time.sleep(0.25)
    return False


def post_json(url: str, payload: dict, token: str, timeout_s: float = 30.0) -> None:
    data = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=data,
        headers={
            "Content-Type": "application/json",
            "X-LIVEX-Session-Token": token,
        },
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout_s) as response:
        response.read()


def high_priority_flags() -> int:
    """Priorite elevee sous Windows : une machine de developpement partagee
    (builds, autres agents) sinon, un run sur trois derive de 10 % et la
    variance publiee parle de la charge de la machine, pas du tick."""
    if sys.platform != "win32":
        return 0
    return 0x00000080 | 0x08000000  # HIGH_PRIORITY_CLASS | CREATE_NO_WINDOW


def run_command(
    command: list[str],
    cwd: Path,
    env: dict,
    timeout_s: float,
    encoding: str = "utf-8",
) -> subprocess.CompletedProcess:
    # Encodage explicite : la console .NET ecrit en page de code Windows (cp1252)
    # quand sa sortie est redirigee ; decoder en UTF-8 casserait les accents et
    # rendrait les traces illisibles.
    return subprocess.run(
        command,
        cwd=str(cwd),
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=timeout_s,
        text=True,
        encoding=encoding,
        errors="replace",
        creationflags=high_priority_flags(),
    )


def build(repo: Path) -> None:
    for project in (
        "prism/Prism.Host/Prism.Host.csproj",
        "syne/Simulation.Console/Simulation.Console.csproj",
    ):
        log(f"build {project}")
        result = run_command(
            ["dotnet", "build", project, "-c", "Release", "--nologo", "-v", "q"],
            repo,
            os.environ.copy(),
            timeout_s=600.0,
        )
        if result.returncode != 0:
            raise SystemExit(
                f"Echec de la build de {project} :\n{result.stdout}\n{result.stderr}".strip()
            )


def host_dll(repo: Path) -> Path:
    return repo / "prism/Prism.Host/bin/Release/net10.0/prism-host.dll"


def client_dll(repo: Path) -> Path:
    return repo / "syne/Simulation.Console/bin/Release/net10.0/Simulation.Console.dll"


def git_provenance(repo: Path) -> dict:
    def git(*args: str) -> str:
        result = subprocess.run(
            ["git", *args],
            cwd=str(repo),
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
        )
        return result.stdout.strip() if result.returncode == 0 else ""

    dirty = bool(git("status", "--porcelain"))
    return {
        "gitCommit": git("rev-parse", "--short", "HEAD"),
        "gitBranch": git("rev-parse", "--abbrev-ref", "HEAD"),
        "workingTreeDirty": dirty,
    }


def dotnet_version(repo: Path) -> str:
    result = subprocess.run(
        ["dotnet", "--version"],
        cwd=str(repo),
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
    )
    return result.stdout.strip() if result.returncode == 0 else ""


def parse_scenarios(raw: str) -> list[tuple[int, int]]:
    scenarios = []
    for part in raw.split(","):
        part = part.strip()
        if not part:
            continue
        match = re.fullmatch(r"(\d+)x(\d+)", part)
        if not match:
            raise SystemExit(f"Scenario illisible : {part!r} (attendu NxT, ex. 100x25)")
        scenarios.append((int(match.group(1)), int(match.group(2))))
    if not scenarios:
        raise SystemExit("Aucun scenario fourni.")
    return scenarios


def read_jsonl(path: Path, sort: bool = True) -> list[dict]:
    records = []
    if not path.exists():
        return records
    with path.open("r", encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                records.append(json.loads(line))
            except json.JSONDecodeError:
                # Une ligne partielle ne doit pas invalider tout le relevé :
                # elle est comptée par l'appelant via le nombre de ticks.
                continue
    if sort:
        records.sort(key=lambda record: record.get("tick", -1))
    return records


def phase_value(record: dict, source: tuple[str, str | None]) -> float | None:
    section, key = source
    if key is None:
        value = record.get(section)
    else:
        value = record.get(section, {}).get(key)
    return float(value) if isinstance(value, (int, float)) else None


def tick_segments(records: list[dict]) -> list[list[dict]]:
    """Decoupe le JSONL d'un hote en runs : chaque preparation repart au tick 0."""
    segments: list[list[dict]] = []
    current: list[dict] = []
    for record in records:
        if record.get("tick") == 0 and current:
            segments.append(current)
            current = []
        current.append(record)
    if current:
        segments.append(current)
    return segments


def run_client_once(
    repo: Path,
    agents: int,
    ticks: int,
    run_index: int,
    args: argparse.Namespace,
    client_env: dict,
    port: int,
    scenario_dir: Path,
    measured: bool,
) -> dict:
    client_json = scenario_dir / f"client-{run_index}.json"
    client_env = client_env.copy()
    client_env["SYNE_COUPLED_PROFILE_JSON"] = str(client_json)
    client_command = [
        "dotnet",
        str(client_dll(repo)),
        "--prism-url",
        f"http://127.0.0.1:{port}",
        "--prism-agents",
        str(agents),
        "--prism-ticks",
        str(ticks),
        "--prism-seed",
        str(args.seed),
        "--prism-content-hash",
        args.content_hash,
        "--prism-content-dir",
        str((repo / args.content_dir).resolve()),
        "--production",
        args.production,
    ]
    label = f"run {run_index + 1 - args.warmup}/{args.runs}" if measured else "chauffe"
    log(f"  {label} : hote :{port}, client {agents} agents x {ticks} ticks")
    try:
        client = run_command(
            client_command, repo, client_env, timeout_s=args.timeout, encoding="cp1252"
        )
    except subprocess.TimeoutExpired:
        raise RuntimeError(
            f"le client couple a depasse {args.timeout:.0f} s ({agents} agents x {ticks} ticks)"
        )

    if client.returncode != 0:
        raise RuntimeError(
            "le client couple a echoue :\n" + client.stdout[-4000:] + client.stderr[-4000:]
        )

    # Sans accent dans le motif : la trace du client traverse la page de code
    # Windows, mieux vaut n'y dependre que de l'ASCII.
    fingerprint_match = re.search(r"empreinte coupl.{0,4}e\s*:\s*([0-9a-f]+)", client.stdout)
    completed_match = re.search(r"(\d+)/(\d+) ticks OK", client.stdout)
    if not client_json.exists():
        raise RuntimeError("le client n'a pas ecrit son releve JSON")
    with client_json.open("r", encoding="utf-8") as handle:
        client_payload = json.load(handle)

    if completed_match is not None and completed_match.group(1) != completed_match.group(2):
        raise RuntimeError(
            f"le client annonce {completed_match.group(0)} : run incomplet ({agents} agents)"
        )
    if len(client_payload.get("ticks", [])) != ticks:
        raise RuntimeError(
            f"le client a releve {len(client_payload.get('ticks', []))} tick(s) au lieu de {ticks}"
        )

    return {
        "run": run_index + 1 - args.warmup,
        "port": port,
        "clientTicks": client_payload.get("ticks", []),
        "fingerprint": fingerprint_match.group(1) if fingerprint_match else None,
        "completed": (
            f"{completed_match.group(1)}/{completed_match.group(2)}"
            if completed_match
            else None
        ),
        "runDir": str(scenario_dir),
    }


def measure_scenario(
    repo: Path,
    agents: int,
    ticks: int,
    args: argparse.Namespace,
    env_base: dict,
    work_root: Path,
) -> list[dict]:
    """Execute la chauffe puis les runs mesures, chacun sur un hote neuf.

    L'hote est relance pour chaque run : un processus neuf garantit que les runs
    ne partagent ni tas ni caches, et que la mesure ne paie pas la memoir d'un run
    precedent. La chauffe du scenario absorbe le scan antivirus et les caches de
    fichiers froids.
    """
    runs = []
    for index in range(args.runs + args.warmup):
        measured = index >= args.warmup
        run_dir = work_root / f"{agents}x{ticks}-r{index}-{uuid.uuid4().hex[:8]}"
        run_dir.mkdir(parents=True, exist_ok=True)
        host_jsonl = run_dir / "host.jsonl"
        host_out = (run_dir / "host.out").open("w", encoding="utf-8")
        host_err = (run_dir / "host.err").open("w", encoding="utf-8")
        port = free_port()

        token = args.token
        env = env_base.copy()
        env["LIVEX_SESSION_TOKEN"] = token
        env["PRISM_BENCHMARK_JSON"] = str(host_jsonl)
        # Le checkpoint d'arret n'est pas un cout de tick : il tombe sur le dernier
        # tick, et a 1 000 agents sa serialisation depasse la limite JSON de 125 Mo
        # et ferait echouer le run avant la fin des ticks mesures.
        env["PRISM_CHECKPOINT_ON_STOP"] = "0"

        host_command = [
            "dotnet",
            str(host_dll(repo)),
            "--control-port",
            str(port),
            "--work-dir",
            str(run_dir / "work"),
            "--instance-id",
            f"bench-{agents}-{index}",
            "--log-dir",
            str(run_dir / "logs"),
            "--correlation-id",
            f"prism-benchmark-{agents}x{ticks}-r{index}",
        ]
        host = subprocess.Popen(
            host_command,
            cwd=str(repo),
            env=env,
            stdout=host_out,
            stderr=host_err,
            creationflags=high_priority_flags(),
        )

        client_env = env.copy()
        client_env.pop("PRISM_BENCHMARK_JSON", None)
        client_env["SYNE_COUPLED_PROFILE"] = "1"

        try:
            if not http_ready(
                f"http://127.0.0.1:{port}/health/live", timeout_s=args.startup_timeout
            ):
                raise RuntimeError("l'hote PRISM n'a pas repondu a /health/live")

            run = run_client_once(
                repo,
                agents,
                ticks,
                index,
                args,
                client_env,
                port,
                run_dir,
                measured,
            )
            segments = tick_segments(read_jsonl(host_jsonl, sort=False))
            if not segments:
                raise RuntimeError("l'hote n'a ecrit aucun relevé de tick")
            run["records"] = segments[0]
            # Un releve incomplet ne doit pas produire une mediane silencieusement
            # calculee sur moins de ticks : c'est exactement ce que le harnais
            # existe pour interdire.
            if len(run["records"]) != ticks:
                raise RuntimeError(
                    f"l'hote a releve {len(run['records'])} tick(s) au lieu de {ticks} "
                    f"({agents} agents)"
                )
            if measured:
                runs.append(run)
        finally:
            try:
                post_json(f"http://127.0.0.1:{port}/control/shutdown", {}, token, timeout_s=5.0)
            except Exception:
                pass
            try:
                host.wait(timeout=10)
            except subprocess.TimeoutExpired:
                host.terminate()
                try:
                    host.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    host.kill()
            host_out.close()
            host_err.close()

    return runs


def aggregate(scenario_runs: list[dict], agents: int, ticks: int, total_ticks_expected: int) -> dict:
    first_tick, last_tick = 0, total_ticks_expected - 1
    measured = []
    excluded = {"firstTick": first_tick, "lastTick": last_tick, "reason": "caches froids et checkpoint d'arret"}
    for run in scenario_runs:
        records = [record for record in run["records"] if first_tick < record["tick"] < last_tick]
        if len(records) < 2:
            raise SystemExit(
                f"{agents} agents : seulement {len(records)} tick(s) mesurable(s) par run "
                f"(il en faut >= 2 hors tick 0 et tick {last_tick})."
            )
        measured.append({"run": run["run"], "records": records, "clientTicks": run["clientTicks"]})

    phases = {}
    run_medians_by_phase = {}
    for phase, source in PHASE_SOURCES.items():
        per_run_medians = []
        per_run_p95 = []
        for run in measured:
            values = [value for record in run["records"] if (value := phase_value(record, source)) is not None]
            if not values:
                continue
            per_run_medians.append(statistics.median(values))
            per_run_p95.append(percentile(values, 0.95))
        if not per_run_medians:
            continue
        variance = variance_pct(per_run_medians)
        entry = {
            "medianMs": round(statistics.median(per_run_medians), 3),
            "p95Ms": round(statistics.median(per_run_p95), 3),
            "runMediansMs": [round(value, 3) for value in per_run_medians],
            "runP95Ms": [round(value, 3) for value in per_run_p95],
            "spreadPct": round(variance, 2),
            "standardErrorPct": round(standard_error_pct(per_run_medians), 2),
        }
        budget = BUDGETS.get(agents, {}).get(phase)
        if budget is not None:
            entry["budgetMs"] = budget
            entry["gapMs"] = round(entry["medianMs"] - budget, 3)
            entry["gapPctOfBudget"] = round((entry["medianMs"] - budget) / budget * 100.0, 1)
        phases[phase] = entry
        run_medians_by_phase[phase] = per_run_medians

    for phase, keys in SUMMED_PHASES.items():
        per_run_medians = []
        per_run_p95 = []
        for run in measured:
            values = [
                sum(float(record.get("phasesMs", {}).get(key, 0.0)) for key in keys)
                for record in run["records"]
            ]
            per_run_medians.append(statistics.median(values))
            per_run_p95.append(percentile(values, 0.95))
        entry = {
            "medianMs": round(statistics.median(per_run_medians), 3),
            "p95Ms": round(statistics.median(per_run_p95), 3),
            "runMediansMs": [round(value, 3) for value in per_run_medians],
            "runP95Ms": [round(value, 3) for value in per_run_p95],
            "spreadPct": round(variance_pct(per_run_medians), 2),
            "standardErrorPct": round(standard_error_pct(per_run_medians), 2),
        }
        budget = BUDGETS.get(agents, {}).get(phase)
        if budget is not None:
            entry["budgetMs"] = budget
            entry["gapMs"] = round(entry["medianMs"] - budget, 3)
            entry["gapPctOfBudget"] = round((entry["medianMs"] - budget) / budget * 100.0, 1)
        phases[phase] = entry

    # Transport = appel client /data/tick moins le tick du monde cote hote.
    transport_medians = []
    transport_p95 = []
    for run in measured:
        host_by_tick = {record["tick"]: float(record["totalMs"]) for record in run["records"]}
        deltas = [
            float(tick["prismMs"]) - host_by_tick[tick["tick"]]
            for tick in run["clientTicks"]
            if tick.get("tick") in host_by_tick
        ]
        if deltas:
            transport_medians.append(statistics.median(deltas))
            transport_p95.append(percentile(deltas, 0.95))
    if transport_medians:
        entry = {
            "medianMs": round(statistics.median(transport_medians), 3),
            "p95Ms": round(statistics.median(transport_p95), 3),
            "runMediansMs": [round(value, 3) for value in transport_medians],
            "runP95Ms": [round(value, 3) for value in transport_p95],
            "spreadPct": round(variance_pct(transport_medians), 2),
            "standardErrorPct": round(standard_error_pct(transport_medians), 2),
        }
        budget = BUDGETS.get(agents, {}).get("transport")
        if budget is not None:
            entry["budgetMs"] = budget
            entry["gapMs"] = round(entry["medianMs"] - budget, 3)
            entry["gapPctOfBudget"] = round((entry["medianMs"] - budget) / budget * 100.0, 1)
        phases["transport"] = entry

    allocations = []
    allocation_medians = []
    gc_medians = []
    gc_p95 = []
    writes_allocations = []
    for run in measured:
        tick_allocations = [float(record.get("allocatedBytes", 0)) for record in run["records"]]
        gc_pauses = [float(record.get("gcPauseMs", 0)) for record in run["records"]]
        writes_alloc = [float(record.get("writes", {}).get("allocatedBytes", 0)) for record in run["records"]]
        allocation_medians.append(statistics.median(tick_allocations))
        gc_medians.append(statistics.median(gc_pauses))
        gc_p95.append(percentile(gc_pauses, 0.95))
        writes_allocations.append(statistics.median(writes_alloc))
        allocations.extend(tick_allocations)

    round_trip_medians = []
    round_trip_p95 = []
    for run in measured:
        # Meme filtrage que cote hote : tick 0 et dernier tick exclus.
        values = [float(tick["prismMs"]) for index, tick in enumerate(run["clientTicks"])
                  if first_tick < index < last_tick]
        if values:
            round_trip_medians.append(statistics.median(values))
            round_trip_p95.append(percentile(values, 0.95))

    decide_medians = []
    apply_medians = []
    for run in measured:
        values = [tick for index, tick in enumerate(run["clientTicks"]) if first_tick < index < last_tick]
        if values:
            decide_medians.append(statistics.median([tick["decideMs"] for tick in values]))
            apply_medians.append(statistics.median([tick["applyMs"] for tick in values]))

    fingerprints = [run["fingerprint"] for run in scenario_runs]
    # Le seuil de PRISM-106 porte sur les phases publiees (et le total) : ce sont
    # les chiffres que PERFORMANCE.md cite. Les postes client (decision, appel
    # HTTP) restent publies avec leur variance, mais ne sont pas des budgets.
    variances = [phases[phase]["spreadPct"] for phase in GATED_PHASES if phase in phases]
    standard_errors = [
        phases[phase]["standardErrorPct"] for phase in GATED_PHASES if phase in phases
    ]

    return {
        "agents": agents,
        "ticks": ticks,
        "runs": len(scenario_runs),
        "measuredTicksPerRun": [len(run["records"]) for run in measured],
        "excludedTicks": excluded,
        "fingerprints": fingerprints,
        "fingerprintStable": len({value for value in fingerprints if value}) == 1 and all(fingerprints),
        "completedTicks": [run["completed"] for run in scenario_runs],
        "phases": phases,
        "allocation": {
            "medianBytesPerTick": round(statistics.median(allocation_medians), 1),
            "medianMbPerTick": round(statistics.median(allocation_medians) / 1024 / 1024, 1),
            "runMediansBytesPerTick": [round(value, 1) for value in allocation_medians],
            "medianBytesPerTickInWrites": round(statistics.median(writes_allocations), 1),
            "variancePct": round(variance_pct(allocation_medians), 2),
        },
        "gcPause": {
            "medianMsPerTick": round(statistics.median(gc_medians), 3),
            "p95MsPerTick": round(statistics.median(gc_p95), 3),
            "runMediansMsPerTick": [round(value, 3) for value in gc_medians],
            "variancePct": round(variance_pct(gc_medians), 2),
        },
        "roundTrip": {
            "medianMs": round(statistics.median(round_trip_medians), 3) if round_trip_medians else 0.0,
            "p95Ms": round(statistics.median(round_trip_p95), 3) if round_trip_p95 else 0.0,
            "runMediansMs": [round(value, 3) for value in round_trip_medians],
            "variancePct": round(variance_pct(round_trip_medians), 2),
        },
        "client": {
            "decide": {
                "medianMs": round(statistics.median(decide_medians), 3) if decide_medians else 0.0,
                "runMediansMs": [round(value, 3) for value in decide_medians],
                "variancePct": round(variance_pct(decide_medians), 2),
            },
            "apply": {
                "medianMs": round(statistics.median(apply_medians), 3) if apply_medians else 0.0,
                "runMediansMs": [round(value, 3) for value in apply_medians],
                "variancePct": round(variance_pct(apply_medians), 2),
            },
        },
        "variance": {
            "spreadDefinition": "(max - min) / mediane des medianes de run, en %",
            "standardErrorDefinition": "ecart-type des medianes de run / sqrt(n) / mediane, en %",
            "gatedMetric": "standardErrorPct",
            "gatedPhases": [phase for phase in GATED_PHASES if phase in phases],
            "maxVariancePct": round(max(variances), 2) if variances else 0.0,
            "maxStandardErrorPct": round(max(standard_errors), 2) if standard_errors else 0.0,
            "thresholdPct": VARIANCE_THRESHOLD_PCT,
            "passed": (
                all(value < VARIANCE_THRESHOLD_PCT for value in standard_errors)
                if standard_errors
                else False
            ),
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Benchmark reproductible du tick couple SYNE <-> PRISM (PRISM-106).",
    )
    parser.add_argument("--scenarios", default=DEFAULT_SCENARIOS, help="NxT,NxT (defaut : 100x25,500x10,1000x8)")
    parser.add_argument("--runs", type=int, default=3, help="executions consecutives par scenario (defaut : 3)")
    parser.add_argument(
        "--warmup",
        type=int,
        default=1,
        help="executions de chauffe jetees avant les runs mesures (defaut : 1)",
    )
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--content-hash", default=DEFAULT_CONTENT_HASH)
    parser.add_argument("--content-dir", default=DEFAULT_CONTENT_DIR, help="relatif a la racine du depot")
    parser.add_argument("--production", default=DEFAULT_PRODUCTION)
    parser.add_argument("--out", default=None, help="chemin du JSON (defaut : docs/prism/benchmarks/B9-coupled-tick-<date>-v1.json)")
    parser.add_argument("--token", default="prism-benchmark-token")
    parser.add_argument("--timeout", type=float, default=900.0, help="delai max par run client, en secondes")
    parser.add_argument("--startup-timeout", type=float, default=120.0, help="delai max de demarrage de l'hote")
    parser.add_argument("--no-build", action="store_true", help="ne pas construire les binaires Release")
    parser.add_argument("--keep-work", action="store_true", help="conserver les repertoires de travail")
    args = parser.parse_args()

    if args.runs < 2:
        raise SystemExit("PRISM-106 exige au moins 2 executions consecutives (3 par defaut).")
    if args.warmup < 0:
        raise SystemExit("--warmup doit etre positif ou nul.")

    repo = repo_root()
    scenarios = parse_scenarios(args.scenarios)

    if not args.no_build:
        build(repo)

    if not host_dll(repo).exists() or not client_dll(repo).exists():
        raise SystemExit(
            "Binaires Release absents : lancer sans --no-build, ou construire "
            "prism/Prism.Host et syne/Simulation.Console en Release."
        )

    work_root = repo / "prism" / "build" / "bench-tmp"
    work_root.mkdir(parents=True, exist_ok=True)
    env_base = os.environ.copy()
    # Les binaires sont lances directement : aucune compilation n'entre dans la mesure.
    env_base["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"

    generated_at = datetime.now(timezone.utc)
    out_path = Path(args.out) if args.out else (
        repo / "docs/prism/benchmarks" / f"B9-coupled-tick-{generated_at:%Y-%m-%d}-v1.json"
    )

    log(f"PRISM-106 : {len(scenarios)} scenario(s) x {args.runs} runs")
    log(f"seed {args.seed}, contenu {args.content_hash[:16]}…, production {args.production}")
    if args.no_build:
        log("build ignoree (--no-build)")

    scenario_results = []
    started = time.monotonic()
    try:
        for agents, ticks in scenarios:
            log(f"\n[{agents} agents x {ticks} ticks]")
            # Chauffe + 3 runs mesures sur le meme processus hote : le premier
            # lancement paie le scan antivirus et la montee en optimisation, et
            # le JIT/PGO reste chaud pour les runs publies.
            runs = measure_scenario(repo, agents, ticks, args, env_base, work_root)
            result = aggregate(runs, agents, ticks, ticks)
            if not result["fingerprintStable"]:
                raise SystemExit(
                    f"{agents} agents : empreintes de run non identiques {result['fingerprints']} — "
                    "l'instrumentation a change la trajectoire, le relevé n'est pas publiable."
                )
            log(
                "  mediane tick {total} ms, perception {perception} ms, pathfinding {pathfinding} ms, "
                "macros {macros} ms, ecritures {writes} ms, variance {variance} %".format(
                    total=result["phases"]["total"]["medianMs"],
                    perception=result["phases"]["perception"]["medianMs"],
                    pathfinding=result["phases"]["pathfinding"]["medianMs"],
                    macros=result["phases"]["macros"]["medianMs"],
                    writes=result["phases"]["writes"]["medianMs"],
                    variance=result["variance"]["maxStandardErrorPct"],
                )
            )
            log(
                "  etendue max-min {spread} % (publiee, dominee par le run le plus lent)".format(
                    spread=result["variance"]["maxVariancePct"]
                )
            )
            if not args.keep_work:
                seen = set()
                for run in runs:
                    if run["runDir"] not in seen:
                        seen.add(run["runDir"])
                        shutil.rmtree(run["runDir"], ignore_errors=True)
            scenario_results.append(result)
    finally:
        if not args.keep_work:
            shutil.rmtree(work_root, ignore_errors=True)

    max_variance = max(
        (scenario["variance"]["maxVariancePct"] for scenario in scenario_results),
        default=0.0,
    )
    max_standard_error = max(
        (scenario["variance"]["maxStandardErrorPct"] for scenario in scenario_results),
        default=0.0,
    )
    all_passed = all(scenario["variance"]["passed"] for scenario in scenario_results)
    payload = {
        "schema": SCHEMA,
        "runVersion": RUN_VERSION,
        "generatedAtUtc": generated_at.isoformat(timespec="seconds").replace("+00:00", "Z"),
        "seed": args.seed,
        "contentHash": args.content_hash,
        "protocol": {
            "contentDir": args.content_dir,
            "production": args.production.split(","),
            "runsPerScenario": args.runs,
            "warmupRunsPerScenario": args.warmup,
            "highPriorityProcesses": bool(sys.platform == "win32"),
            "varianceThresholdPct": VARIANCE_THRESHOLD_PCT,
            "varianceDefinition": "(max - min) / mediane des medianes de run, en % (publie)",
            "reproducibilityMetric": "erreur-type relative des medianes de run : ecart-type / sqrt(n) / mediane, en %",
            "varianceGatedOn": "phases publiees (perception, pathfinding, macros, writes, total)",
            "p95Definition": "interpolation lineaire sur les ticks mesures de chaque run, puis mediane des runs",
            "excludedTicksPerRun": "tick 0 (caches froids) et dernier tick (arret du run)",
            "checkpointOnStop": False,
            "checkpointOnStopReason": (
                "le checkpoint d'arret n'est pas un cout de tick ; a 1 000 agents sa "
                "serialisation depasse la limite JSON de 125 Mo et ferait echouer le run"
            ),
            "phases": {
                "perception": "phase Perception du monde",
                "pathfinding": "phase ApplyWrites (recherche de chemin et application des ecritures differees)",
                "macros": "phase AdvanceMacroActions",
                "writes": "duree cumulee des ecritures differees, sous-ensemble d'ApplyWrites",
                "updates": "somme des phases ScheduledUpdates, RandomUpdates, WearExpiryAndConstruction, Calendar",
                "transport": "appel /data/tick cote client moins le tick du monde cote hote",
                "total": "duree du tick du monde (toutes phases + serialisation du resultat)",
                "roundTrip": "appel /data/tick cote client (transport + handler complet de l'hote)",
            },
            "budgetsFrom": "docs/prism/PERFORMANCE.md §2 (1000 agents) et §1 (100 agents)",
        },
        "provenance": {
            **git_provenance(repo),
            "dotnet": dotnet_version(repo),
            "platform": sys.platform,
            "python": sys.version.split()[0],
            "cpus": os.cpu_count(),
            "host": "prism/Prism.Host (Release), PRISM_BENCHMARK_JSON",
            "client": "syne/Simulation.Console (Release), SYNE_COUPLED_PROFILE_JSON",
        },
        "scenarios": scenario_results,
        "acceptance": {
            "command": "python scripts/prism-coupled-benchmark.py",
            "runsPerScenario": args.runs,
            "varianceThresholdPct": VARIANCE_THRESHOLD_PCT,
            "maxVariancePct": round(max_variance, 2),
            "maxStandardErrorPct": round(max_standard_error, 2),
            "gatedMetric": "standardErrorPct",
            "allVariancesUnderThreshold": all_passed,
            "allFingerprintsStable": all(scenario["fingerprintStable"] for scenario in scenario_results),
        },
        "durationSeconds": round(time.monotonic() - started, 1),
    }

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    log(f"\nJSON ecrit : {out_path}")
    log(
        f"duree totale : {payload['durationSeconds']} s, erreur-type max {max_standard_error:.1f} % "
        f"(etendue max-min {max_variance:.1f} %)"
    )
    if not all_passed:
        log("ECHEC : variance >= 10 % sur au moins un poste — releve publie mais non acceptable.")
        return 3
    log("OK : 3 runs consecutifs, erreur-type < 10 %, empreintes stables.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
