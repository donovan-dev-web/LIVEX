#!/usr/bin/env python3
"""Campagne de calibration de survie SYNE (ADR-015, itération B1).

Exécute le moteur batch (``--export-stream``) pour une grille
**populations × seeds**, relit le flux d'observabilité écrit par tick et calcule
les signaux de viabilité déjà définis par ADR-015 / ECHOS schemaVersion 2 :

- ``extinctionTick``      : premier tick où ``aliveCount == 0`` ;
- ``energySlopePerTick``  : pente (moindres carrés) de l'énergie moyenne sur les
  200 derniers ticks — détecte la mort lente même sans extinction ;
- ``actionSharesWhenHungry`` : part Eat/Drink des décisions quand la faim moyenne
  > 70 ;
- ``resourceRegime``      : min / moy / max des réserves nourriture et eau ;
- population finale, naissances, morts par cause.

Un **overlay** (fichier JSON partiel, mêmes clés que ``--config``) sert de
candidat de calibration : la campagne écrit par population un fichier de
configuration ``<out>/configs/pop-<n>.json`` = overlay + ``agents.initialCount``
et lance le moteur dessus.

Usage :
    python3 scripts/calibration-campaign.py \\
        --overlay /tmp/syne-calib/cand-a.json \\
        --populations 50,100 --seeds 12345,424242,999 --ticks 2500 \\
        --out /home/devops/syne-calib/cand-a

Les artefacts volumineux (``stream.jsonl``, ~1 Go par run de 2500 ticks)
vivent dans ``--out`` : placez-le sur un disque réel, pas sur tmpfs.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path
from typing import Sequence

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BIN = ROOT / "syne" / "Simulation.Console" / "bin" / "Release" / "net10.0" / "Simulation.Console"
HUNGER_THRESHOLD = 70.0
SLOPE_WINDOW = 200

# Critères d'acceptation de la campagne (ADR-015 + cible 2500 ticks).
TARGET_TICKS = 2500
SLOPE_TOLERANCE = 0.005
MIN_SURVIVOR_RATIO = 0.9


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--config", type=Path, help="surcouche JSON complète passée à --config")
    source.add_argument("--overlay", type=Path, help="overlay de calibration (populations injectées par la campagne)")
    parser.add_argument("--populations", default="50", help="populations séparées par des virgules (défaut 50)")
    parser.add_argument("--seeds", default="12345,424242,999", help="graines séparées par des virgules")
    parser.add_argument("--ticks", type=int, default=TARGET_TICKS, help=f"horizon en ticks (défaut {TARGET_TICKS})")
    parser.add_argument("--out", type=Path, required=True, help="répertoire de campagne (disque réel recommandé)")
    parser.add_argument("--bin", type=Path, default=DEFAULT_BIN)
    parser.add_argument("--keep-stream", action="store_true", help="conserve les stream.jsonl (lourd)")
    return parser.parse_args(argv)


def build_config(overlay: dict, population: int) -> dict:
    config = json.loads(json.dumps(overlay))
    config.setdefault("agents", {})["initialCount"] = population
    return config


def run_one(binary: Path, config_path: Path, seed: int, ticks: int, run_dir: Path) -> float:
    run_dir.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    completed = subprocess.run(
        [
            str(binary),
            "--config", str(config_path),
            "--seed", str(seed),
            "--ticks", str(ticks),
            "--headless",
            "--export-dir", str(run_dir),
            "--export-stream",
        ],
        cwd=ROOT,
        capture_output=True,
        text=True,
    )
    elapsed = time.monotonic() - started
    if completed.returncode != 0:
        raise SystemExit(
            f"run {config_path.name} seed={seed} en échec ({completed.returncode})\n"
            f"stdout:\n{completed.stdout}\nstderr:\n{completed.stderr}"
        )
    return elapsed


def least_squares_slope(values: Sequence[float]) -> float:
    n = len(values)
    if n < 2:
        return 0.0
    mean_x = (n - 1) / 2.0
    mean_y = sum(values) / n
    num = sum((i - mean_x) * (y - mean_y) for i, y in enumerate(values))
    den = sum((i - mean_x) ** 2 for i in range(n))
    return num / den if den else 0.0


def analyse(stream_path: Path) -> dict:
    per_tick: list[dict] = []
    deaths: list[dict] = []
    births = 0
    hungry_ticks = 0
    hungry_actions: dict[str, int] = {}
    action_counts: dict[str, int] = {}
    hungry_this_tick = False
    blocked: dict[str, int] = {}

    with stream_path.open(encoding="utf-8") as handle:
        for line in handle:
            if line.startswith('{"type":"snapshot"'):
                frame = json.loads(line)
                agents = frame.get("agents", [])
                count = len(agents)
                if count:
                    mean_energy = sum(a["energy"] for a in agents) / count
                    mean_hunger = sum(a["hunger"] for a in agents) / count
                    mean_thirst = sum(a["thirst"] for a in agents) / count
                else:
                    mean_energy = mean_hunger = mean_thirst = 0.0
                stocks = {r["type"]: r["quantity"] for r in frame.get("resources", [])}
                per_tick.append(
                    {
                        "tick": frame["tick"],
                        "alive": frame["aliveCount"],
                        "energy": mean_energy,
                        "hunger": mean_hunger,
                        "thirst": mean_thirst,
                        "food": stocks.get("food", 0.0),
                        "water": stocks.get("water", 0.0),
                    }
                )
                hungry_this_tick = mean_hunger > HUNGER_THRESHOLD
                if hungry_this_tick:
                    hungry_ticks += 1
                for action in frame.get("actions", []):
                    if action["outcome"] != "executed":
                        cause = action.get("cause") or action["outcome"]
                        blocked[cause] = blocked.get(cause, 0) + 1
            elif line.startswith('{"type":"decision_made"'):
                frame = json.loads(line)
                name = frame.get("action")
                if name:
                    action_counts[name] = action_counts.get(name, 0) + 1
                    if hungry_this_tick:
                        hungry_actions[name] = hungry_actions.get(name, 0) + 1
            elif line.startswith('{"type":"agent_died"'):
                frame = json.loads(line)
                deaths.append({"tick": frame.get("tick"), "cause": frame.get("cause")})
            elif line.startswith('{"type":"agent_spawned"'):
                births += 1

    if not per_tick:
        raise SystemExit(f"aucun snapshot dans {stream_path}")

    extinction_tick = next((t["tick"] for t in per_tick if t["alive"] == 0), None)
    window = [t["energy"] for t in per_tick[-SLOPE_WINDOW:]]
    hungry_total = sum(hungry_actions.values())
    eat_drink_share = (
        (hungry_actions.get("eat", 0) + hungry_actions.get("drink", 0)) / hungry_total
        if hungry_total
        else 0.0
    )
    death_causes: dict[str, int] = {}
    for death in deaths:
        cause = death.get("cause") or "unknown"
        death_causes[cause] = death_causes.get(cause, 0) + 1

    return {
        "ticks": per_tick[-1]["tick"],
        "aliveInitial": per_tick[0]["alive"],
        "aliveFinal": per_tick[-1]["alive"],
        "extinctionTick": extinction_tick,
        "energyFinal": round(per_tick[-1]["energy"], 3),
        "energySlopePerTick": round(least_squares_slope(window), 5),
        "energyMin": round(min(t["energy"] for t in per_tick), 3),
        "actionSharesWhenHungry": round(eat_drink_share, 4),
        "hungryTicks": hungry_ticks,
        "actionCounts": dict(sorted(action_counts.items(), key=lambda kv: -kv[1])),
        "blocked": dict(sorted(blocked.items(), key=lambda kv: -kv[1])),
        "deaths": len(deaths),
        "deathCauses": death_causes,
        "births": births,
        "resourceRegime": {
            "food": _regime(t["food"] for t in per_tick),
            "water": _regime(t["water"] for t in per_tick),
        },
        "aliveSeries": [t["alive"] for t in per_tick],
        "energySeries": [round(t["energy"], 3) for t in per_tick],
    }


def _regime(values) -> dict:
    series = list(values)
    return {
        "min": round(min(series), 2),
        "mean": round(sum(series) / len(series), 2),
        "max": round(max(series), 2),
    }


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    if not args.bin.exists():
        raise SystemExit(f"binaire introuvable : {args.bin} (dotnet build -c Release d'abord)")
    source = args.config or args.overlay
    if not source.exists():
        raise SystemExit(f"configuration introuvable : {source}")

    overlay = json.loads((args.overlay or args.config).read_text(encoding="utf-8"))
    populations = [int(p) for p in args.populations.split(",") if p.strip()]
    seeds = [int(s) for s in args.seeds.split(",") if s.strip()]

    out: Path = args.out
    if out.exists():
        shutil.rmtree(out)
    (out / "configs").mkdir(parents=True)

    results: dict[str, dict] = {}
    for population in populations:
        config_path = out / "configs" / f"pop-{population}.json"
        config_path.write_text(json.dumps(build_config(overlay, population), indent=2), encoding="utf-8")
        for seed in seeds:
            run_dir = out / f"pop-{population}-seed-{seed}"
            elapsed = run_one(args.bin, config_path, seed, args.ticks, run_dir)
            report = analyse(run_dir / "stream.jsonl")
            report["wallSeconds"] = round(elapsed, 2)
            results[f"{population}/{seed}"] = report
            if not args.keep_stream:
                (run_dir / "stream.jsonl").unlink(missing_ok=True)
            print(
                f"N={population:<4} seed={seed:<8} ticks={report['ticks']:<5} "
                f"vivants={report['aliveFinal']:<4} extinction={report['extinctionTick'] or '-':<6} "
                f"pente_E={report['energySlopePerTick']:+.4f}/tick E_fin={report['energyFinal']:5.1f} "
                f"eat/drink(faim>70)={report['actionSharesWhenHungry']*100:5.1f}% "
                f"morts={report['deaths']} ({report['deathCauses']}) naiss={report['births']} "
                f"food={report['resourceRegime']['food']['min']:.0f}..{report['resourceRegime']['food']['max']:.0f} "
                f"eau={report['resourceRegime']['water']['min']:.0f} "
                f"bloqués={list(report['blocked'].items())[:2]} [{elapsed:.0f}s]",
                flush=True,
            )
            (out / "campaign.json").write_text(json.dumps(results, indent=2))

    print()
    print(f"=== campagne {out.name} | {args.ticks} ticks | {len(populations)} pop × {len(seeds)} seeds ===")
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
        print("CRITÈRES NON ATTEINTS :")
        for failure in failures:
            print(f"  ✗ {failure}")
        return 1

    print("TOUS LES CRITÈRES ATTEINTS : 0 extinction, |pente| < "
          f"{SLOPE_TOLERANCE}/tick, population ≥ {MIN_SURVIVOR_RATIO:.0%} sur {args.ticks} ticks")
    for key, report in results.items():
        print(f"  ✓ {key}: {report['aliveFinal']} vivants, pente {report['energySlopePerTick']:+.4f}/tick")
    return 0


if __name__ == "__main__":
    sys.exit(main())
