"""Stable headless analysis operations for Launcher integrations."""

from __future__ import annotations

import base64
import json
import math
from pathlib import Path
from typing import Any

from echos.reporting import build_report, markdown_report
from echos.storage.sqlite import AnalyticsStore


class AnalysisInputError(ValueError):
    """An explicit, caller-actionable error in an analysis request."""

    def __init__(self, status_code: int, detail: str) -> None:
        super().__init__(detail)
        self.status_code = status_code


def _directory(value: str, label: str) -> Path:
    try:
        path = Path(value).expanduser().resolve(strict=True)
    except (OSError, RuntimeError) as exc:
        raise AnalysisInputError(404, f"{label} directory is unavailable: {value}") from exc
    if not path.is_dir():
        raise AnalysisInputError(400, f"{label} path is not a directory: {value}")
    return path


def _run_ids(store: AnalyticsStore, run_ids: list[str]) -> list[str]:
    known = {str(run["run_id"]) for run in store.runs()}
    unknown = sorted(set(run_ids) - known)
    if unknown:
        raise AnalysisInputError(404, f"unknown run(s): {', '.join(unknown)}")
    empty = sorted(
        run_id for run_id in set(run_ids)
        if not any(row[0] == run_id for row in store.tick_summaries(run_id))
    )
    if empty:
        raise AnalysisInputError(409, f"run(s) have no completed ticks: {', '.join(empty)}")
    return sorted(run_ids)


def _run_report(store: AnalyticsStore, run_id: str) -> dict[str, Any]:
    try:
        return build_report(store, run_id)
    except ValueError as exc:
        raise AnalysisInputError(409, str(exc)) from exc


def _json_bytes(name: str, payload: object) -> dict[str, str]:
    try:
        content = json.dumps(
            payload,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        ).encode("utf-8")
    except (TypeError, ValueError) as exc:
        raise AnalysisInputError(
            409, "analysis output contains values that cannot be encoded as JSON"
        ) from exc
    return {"name": name, "content": base64.b64encode(content).decode("ascii")}


def _text_bytes(name: str, content: str) -> dict[str, str]:
    return {"name": name, "content": base64.b64encode(content.encode("utf-8")).decode("ascii")}


def analyze_run(
    store: AnalyticsStore,
    experiment_id: str,
    run_id: str,
    run_path: str,
) -> list[dict[str, str]]:
    """Build deterministic run JSON and Markdown from the configured store."""
    _directory(run_path, "run")
    _run_ids(store, [run_id])
    report = _run_report(store, run_id)
    report["experimentId"] = experiment_id
    return [
        _json_bytes(f"run-{run_id}.json", report),
        _text_bytes(
            f"run-{run_id}.md",
            markdown_report(store, run_id, trace_artifact="json"),
        ),
    ]


def _experiment_manifest(path: Path, experiment_id: str) -> list[str]:
    manifest_path = path / "experiment.json"
    try:
        resolved = manifest_path.resolve(strict=True)
        resolved.relative_to(path)
        manifest = json.loads(resolved.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise AnalysisInputError(
            422, f"experiment manifest is required: {manifest_path}"
        ) from exc
    except (OSError, RuntimeError, ValueError, json.JSONDecodeError) as exc:
        raise AnalysisInputError(
            422, f"invalid experiment manifest: {manifest_path}"
        ) from exc
    if not isinstance(manifest, dict) or manifest.get("experimentId") != experiment_id:
        raise AnalysisInputError(422, "experiment manifest experimentId does not match request")
    run_ids = manifest.get("runIds")
    if (
        not isinstance(run_ids, list)
        or not run_ids
        or any(
            not isinstance(item, str) or not item.strip()
            for item in run_ids
        )
    ):
        raise AnalysisInputError(422, "experiment manifest must contain a non-empty runIds array")
    if len(set(run_ids)) != len(run_ids):
        raise AnalysisInputError(422, "experiment manifest runIds must be unique")
    return sorted(run_ids)


def _aggregate_reports(
    store: AnalyticsStore, experiment_id: str, run_ids: list[str]
) -> dict[str, Any]:
    reports = [_run_report(store, run_id) for run_id in run_ids]
    by_metric: dict[str, dict[str, list[dict[str, Any]]]] = {}
    phenomenon_runs: dict[str, dict[str, Any]] = {}

    for report in reports:
        run = report["run"]
        run_id = run["runId"]
        for engine, metrics in report["metrics"]["latest"].items():
            for metric, value in metrics.items():
                if isinstance(value, bool) or not isinstance(value, (int, float)):
                    continue
                numeric = float(value)
                if not math.isfinite(numeric):
                    continue
                by_metric.setdefault(engine, {}).setdefault(metric, []).append(
                    {"runId": run_id, "value": numeric}
                )
        for phenomenon in report["detectedPhenomena"]:
            identifier = phenomenon["identifier"]
            aggregate = phenomenon_runs.setdefault(
                identifier,
                {
                    "identifier": identifier,
                    "label": phenomenon["label"],
                    "description": phenomenon["description"],
                    "runIds": [],
                    "occurrences": 0,
                },
            )
            aggregate["runIds"].append(run_id)
            aggregate["occurrences"] += int(phenomenon["occurrences"])

    metrics: dict[str, dict[str, dict[str, Any]]] = {}
    for engine, metric_values in sorted(by_metric.items()):
        metrics[engine] = {}
        for metric, values in sorted(metric_values.items()):
            values.sort(key=lambda item: item["runId"])
            numbers = [item["value"] for item in values]
            metrics[engine][metric] = {
                "count": len(numbers),
                "mean": sum(numbers) / len(numbers),
                "min": min(numbers),
                "max": max(numbers),
                "values": values,
            }

    phenomena = sorted(phenomenon_runs.values(), key=lambda item: item["identifier"])
    for phenomenon in phenomena:
        phenomenon["runIds"].sort()
        phenomenon["runCount"] = len(phenomenon["runIds"])

    return {
        "schemaVersion": 1,
        "experiment": {
            "experimentId": experiment_id,
            "runCount": len(reports),
            "runIds": run_ids,
        },
        "runs": [
            {
                "run": report["run"],
                "latestMetrics": report["metrics"]["latest"],
                "detectedPhenomena": report["detectedPhenomena"],
            }
            for report in reports
        ],
        "metrics": {"latestAcrossRuns": metrics},
        "detectedPhenomena": phenomena,
    }


def analyze_experiment(
    store: AnalyticsStore,
    experiment_id: str,
    experiment_path: str,
) -> dict[str, str]:
    """Aggregate only the run IDs explicitly listed by an experiment manifest."""
    path = _directory(experiment_path, "experiment")
    run_ids = _experiment_manifest(path, experiment_id)
    run_ids = _run_ids(store, run_ids)
    result = _aggregate_reports(store, experiment_id, run_ids)
    return _json_bytes(f"experiment-{experiment_id}.json", result)


def generate_experiment_report(
    store: AnalyticsStore,
    experiment_id: str,
    experiment_path: str,
) -> str:
    """Render a deterministic Markdown report from stored experiment results."""
    file = analyze_experiment(store, experiment_id, experiment_path)
    result = json.loads(base64.b64decode(file["content"]).decode("utf-8"))
    experiment = result["experiment"]
    lines = [
        f"# ECHOS experiment `{experiment_id}`",
        "",
        f"- **Runs analysed:** {experiment['runCount']}",
        f"- **Run IDs:** {', '.join(experiment['runIds'])}",
        "",
        "## Detected phenomena",
        "",
    ]
    if result["detectedPhenomena"]:
        lines.extend(
            [
                "| Phenomenon | Runs | Occurrences |",
                "|---|---:|---:|",
            ]
        )
        for item in result["detectedPhenomena"]:
            lines.append(
                f"| {item['label']} (`{item['identifier']}`) | "
                f"{item['runCount']} | {item['occurrences']} |"
            )
    else:
        lines.append("_No detected phenomena were recorded._")
    lines += ["", "## Latest metrics across runs", ""]
    metric_rows = [
        (engine, metric, aggregate)
        for engine, metrics in result["metrics"]["latestAcrossRuns"].items()
        for metric, aggregate in metrics.items()
    ]
    if metric_rows:
        lines.extend(
            [
                "| Engine | Metric | Runs | Mean | Min | Max |",
                "|---|---|---:|---:|---:|---:|",
            ]
        )
        for engine, metric, aggregate in metric_rows:
            lines.append(
                f"| `{engine}` | `{metric}` | {aggregate['count']} | "
                f"{aggregate['mean']:.8g} | {aggregate['min']:.8g} | "
                f"{aggregate['max']:.8g} |"
            )
    else:
        lines.append("_No numeric latest metrics were recorded._")
    return "\n".join(lines) + "\n"
