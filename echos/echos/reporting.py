"""Deterministic reports for completed ECHOS runs.

The report is deliberately assembled from :class:`AnalyticsStore` read APIs:
it is suitable for archival, diffing, and feeding other tools without
depending on SQLite row layouts or wall-clock timestamps.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from echos.analysis.calibration import build_calibration_report
from echos.storage.sqlite import AnalyticsStore


def _event(row: tuple) -> dict[str, Any]:
    tick, event_type, agent_id, action, cause, value = row
    parsed: Any = value
    if isinstance(value, str):
        try:
            parsed = json.loads(value)
        except json.JSONDecodeError:
            parsed = value
    return {
        "tick": int(tick),
        "type": event_type,
        "agentId": agent_id,
        "action": action,
        "cause": cause,
        "value": parsed,
    }


def _tick(row: tuple) -> dict[str, Any]:
    keys = (
        "runId", "tick", "simulatedTimeMinutes", "aliveCount", "agentCount",
        "meanEnergy", "meanHunger", "meanThirst", "meanFatigue", "decisionCount",
    )
    return dict(zip(keys, (row[0], int(row[1]), int(row[2]), int(row[3]),
                           int(row[4]), float(row[5]), float(row[6]),
                           float(row[7]), float(row[8]), int(row[9]))))


def _detected_phenomena(contexts: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Aggregate detected phenomena across ticks for human comparison."""
    by_identifier: dict[str, dict[str, Any]] = {}
    for observation in contexts:
        payload = observation["payload"]
        if not isinstance(payload, dict):
            continue
        for phenomenon in payload.get("detected") or []:
            identifier = str(phenomenon.get("identifier", ""))
            if not identifier:
                continue
            item = by_identifier.setdefault(
                identifier,
                {
                    "identifier": identifier,
                    "label": phenomenon.get("label", identifier),
                    "description": phenomenon.get("description", ""),
                    "firstTick": observation["tick"],
                    "lastTick": observation["tick"],
                    "occurrences": 0,
                    "signals": phenomenon.get("signals") or [],
                },
            )
            item["firstTick"] = min(item["firstTick"], observation["tick"])
            item["lastTick"] = max(item["lastTick"], observation["tick"])
            item["occurrences"] += 1
            item["signals"] = phenomenon.get("signals") or item["signals"]
    return sorted(by_identifier.values(), key=lambda item: item["identifier"])


def build_report(
    store: AnalyticsStore, run_id: str, *, seed_override: int | str | None = None
) -> dict[str, Any]:
    """Build the complete, timestamp-free report for one ingested run."""
    metadata = next((run for run in store.runs() if run["run_id"] == run_id), None)
    if metadata is None:
        raise ValueError(f"unknown run: {run_id}")
    summaries = store.tick_summaries(run_id)
    if not summaries:
        raise ValueError(f"run has no completed ticks: {run_id}")

    ticks = [_tick(row) for row in summaries]
    events = [_event(row) for row in store.events(run_id)]
    metrics_all = store.metrics_all(run_id)
    series: dict[str, dict[str, list[dict[str, Any]]]] = {}
    for tick, engine, metric, value in metrics_all:
        series.setdefault(engine, {}).setdefault(metric, []).append(
            {"tick": int(tick), "value": float(value)}
        )

    contexts = {
        context_type: [
            {"tick": int(tick), "payload": payload}
            for tick, payload in observations
        ]
        for context_type, observations in store.contexts(run_id).items()
    }
    calibration = store.calibration_report(run_id)
    if calibration is None:
        calibration = build_calibration_report(
            run_id, summaries, store.events(run_id), metrics_all
        )

    seed = metadata["seed"] if seed_override is None else str(seed_override)
    phenomenon_contexts = contexts.get("phenomena", [])
    return {
        "schemaVersion": 1,
        "run": {
            "runId": metadata["run_id"],
            "version": metadata["version"],
            "seed": seed,
            "status": "complete",
            "tickRange": {
                "first": int(metadata["first_tick"]),
                "last": int(metadata["last_tick"]),
                "count": int(metadata["ticks_count"]),
            },
        },
        "ticks": ticks,
        "events": events,
        "metrics": {"latest": store.latest_metrics(run_id), "series": series},
        "decisionTraces": store.decision_traces(run_id),
        "contexts": contexts,
        "phenomena": phenomenon_contexts,
        "detectedPhenomena": _detected_phenomena(phenomenon_contexts),
        "calibration": calibration,
    }


def json_report(
    store: AnalyticsStore, run_id: str, *, seed_override: int | str | None = None
) -> str:
    """Return canonical JSON (stable key and collection ordering)."""
    return json.dumps(
        build_report(store, run_id, seed_override=seed_override),
        ensure_ascii=False, sort_keys=True,
        separators=(",", ":"),
    )


generate_json_report = json_report


def markdown_report(
    store: AnalyticsStore, run_id: str, *, seed_override: int | str | None = None
) -> str:
    """Render a compact, human-readable report from the canonical model."""
    report = build_report(store, run_id, seed_override=seed_override)
    run = report["run"]
    event_counts: dict[str, int] = {}
    for event in report["events"]:
        event_counts[event["type"]] = event_counts.get(event["type"], 0) + 1
    notable_types = {
        "agent_died",
        "agent_spawned",
        "group_formed",
        "group_dissolved",
        "conflict",
        "message_sent",
    }
    all_notable_events = [
        event for event in report["events"] if event["type"] in notable_types
    ]
    notable_events = all_notable_events[:50]
    latest_metrics = report["metrics"]["latest"]
    selected_metrics = [
        ("Emergence score", "EmergenceIndicators", "EmergenceScore"),
        ("Complexité système", "EmergenceIndicators", "SystemComplexity"),
        ("Diversité croyances", "CognitiveDiversityMetrics", "BeliefDiversity"),
        ("Diversité objectifs", "CognitiveDiversityMetrics", "GoalDiversity"),
        ("Groupes actifs", "GroupDynamicsMetrics", "ActiveGroups"),
        ("Volume messages", "InformationPropagationMetrics", "MessageVolume"),
        ("Confiance moyenne", "SocialComplexityMetrics", "AverageTrustLevel"),
    ]
    lines = [
        f"# ECHOS run `{run['runId']}`",
        "",
        "## Synthèse",
        "",
        f"- **Statut :** {run['status']}",
        f"- **Version :** `{run['version']}`",
        f"- **Seed :** `{run['seed']}`",
        f"- **Ticks :** {run['tickRange']['first']}–{run['tickRange']['last']} "
        f"({run['tickRange']['count']})",
        "",
        "## État de la population",
        "",
        "| Indicateur | Valeur |",
        "|---|---:|",
        f"| Population initiale | {report['calibration']['population']['initial']} |",
        f"| Population finale | {report['calibration']['population']['final']} |",
        f"| Minimum d'entités vivantes | {report['calibration']['population']['minimumAlive']} |",
        f"| Énergie moyenne finale | {report['ticks'][-1]['meanEnergy']:.4g} |",
        f"| Faim moyenne finale | {report['ticks'][-1]['meanHunger']:.4g} |",
        f"| Soif moyenne finale | {report['ticks'][-1]['meanThirst']:.4g} |",
        f"| Fatigue moyenne finale | {report['ticks'][-1]['meanFatigue']:.4g} |",
        "",
        "## Événements",
        "",
        f"**{len(report['events'])} événements** enregistrés, dont "
        f"**{len(report['decisionTraces'])} décisions**.",
        "",
        "| Type | Nombre |",
        "|---|---:|",
    ]
    for event_type, count in sorted(event_counts.items()):
        lines.append(f"| `{event_type}` | {count} |")
    lines += [
        "",
        "### Événements pertinents",
        "",
    ]
    if notable_events:
        for event in notable_events:
            detail = event["action"] or event["cause"] or ""
            lines.append(f"- **tick {event['tick']}** `{event['type']}` {detail}".rstrip())
    else:
        lines.append("_Aucun événement de changement majeur détecté dans ce run._")
    lines += [
        "",
        "## Métriques principales",
        "",
        "| Métrique | Valeur finale |",
        "|---|---:|",
    ]
    for label, engine, metric in selected_metrics:
        value = latest_metrics.get(engine, {}).get(metric)
        lines.append(f"| {label} | {value if value is not None else '—'} |")
    lines += [
        "",
        "## Évolution par tick",
        "",
        "| Tick | Vivants | Énergie | Émergence | Groupes | Messages |",
        "|---:|---:|---:|---:|---:|---:|",
    ]
    series = report["metrics"]["series"]
    tick_rows = report["ticks"]
    if len(tick_rows) > 20:
        indexes = sorted({round(index * (len(tick_rows) - 1) / 19) for index in range(20)})
        tick_rows = [report["ticks"][index] for index in indexes]
    for tick in tick_rows:
        def series_value(engine: str, metric: str) -> str:
            values = series.get(engine, {}).get(metric, [])
            match = next((item["value"] for item in values if item["tick"] == tick["tick"]), None)
            return "—" if match is None else f"{match:.4g}"

        lines.append(
            f"| {tick['tick']} | {tick['aliveCount']} | {tick['meanEnergy']:.4g} | "
            f"{series_value('EmergenceIndicators', 'EmergenceScore')} | "
            f"{series_value('GroupDynamicsMetrics', 'ActiveGroups')} | "
            f"{series_value('InformationPropagationMetrics', 'MessageVolume')} |"
        )
    lines += [
        "",
        "## Calibration ECHOS",
        "",
        f"- Politique : {report['calibration'].get('calibrationPolicy', '—')}",
        f"- Phénomènes détectés : {len(report.get('detectedPhenomena', []))}",
        (
            f"- Événements pertinents affichés : {len(notable_events)}"
            f"/{len(all_notable_events)}"
            if len(notable_events) < len(all_notable_events)
            else f"- Événements pertinents : {len(notable_events)}"
        ),
        "",
        "### Liste des phénomènes détectés",
        "",
    ]
    detected = report.get("detectedPhenomena", [])
    if detected:
        lines.extend([
            "| Phénomène | Première détection | Dernière détection | Occurrences |",
            "|---|---:|---:|---:|",
        ])
        for phenomenon in detected:
            lines.append(
                f"| **{phenomenon['label']}** (`{phenomenon['identifier']}`) | "
                f"{phenomenon['firstTick']} | {phenomenon['lastTick']} | "
                f"{phenomenon['occurrences']} |"
            )
            if phenomenon["description"]:
                lines.append(f"  \n  _{phenomenon['description']}_")
    else:
        lines.append("_Aucun phénomène détecté sur les ticks ingérés._")
    lines += [
        "",
        "Les occurrences et signaux détaillés par tick restent disponibles dans "
        "`contexts.phenomena` du fichier JSON.",
        "",
        "## Traces et données détaillées",
        "",
        "Le fichier JSON associé contient l'intégralité des événements, métriques, "
        "contextes, traces de décision et données de calibration. Ce document "
        "Markdown conserve uniquement la synthèse utile à la comparaison humaine.",
        "",
        f"- Rapport complet : `{run['runId']}.json`",
        "",
    ]
    return "\n".join(lines)


generate_markdown_report = markdown_report


def write_reports(
    store: AnalyticsStore,
    run_id: str,
    output_dir: str | Path,
    *,
    seed_override: int | str | None = None,
) -> tuple[Path, Path]:
    """Write deterministic ``.json`` and ``.md`` files and return their paths."""
    directory = Path(output_dir)
    directory.mkdir(parents=True, exist_ok=True)
    json_path = directory / f"{run_id}.json"
    markdown_path = directory / f"{run_id}.md"
    json_path.write_text(
        json_report(store, run_id, seed_override=seed_override) + "\n",
        encoding="utf-8",
    )
    markdown_path.write_text(
        markdown_report(store, run_id, seed_override=seed_override),
        encoding="utf-8",
    )
    return json_path, markdown_path


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate an ECHOS completed-run report")
    parser.add_argument("database", type=Path)
    parser.add_argument("run_id")
    parser.add_argument("-o", "--output-dir", type=Path, default=Path("."))
    parser.add_argument("--seed", help="seed à archiver si le flux ne la transporte pas")
    args = parser.parse_args(argv)
    with AnalyticsStore(args.database) as store:
        write_reports(
            store, args.run_id, args.output_dir, seed_override=args.seed
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
