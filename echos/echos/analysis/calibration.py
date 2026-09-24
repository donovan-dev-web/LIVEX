"""Post-run calibration evidence (SYNE-131).

This module summarizes observed runs into deterministic, read-only evidence.
It never changes simulator configuration: parameter choices stay explicit and
require a reviewed calibration decision.
"""

from __future__ import annotations

from collections import Counter
from math import fsum
from typing import Any


def _stats(values: list[float]) -> dict[str, float | int]:
    return {
        "count": len(values),
        "min": round(min(values), 8),
        "mean": round(fsum(values) / len(values), 8),
        "max": round(max(values), 8),
    }


def build_calibration_report(
    run_id: str,
    tick_summaries: list[tuple],
    events: list[tuple],
    metrics: list[tuple],
) -> dict[str, Any] | None:
    """Build a timestamp-free, stable summary of one completed ingested run."""
    if not tick_summaries:
        return None

    ticks = sorted(tick_summaries, key=lambda row: row[1])
    event_counts = Counter(row[1] for row in events)
    needs = {
        key: _stats([float(row[column]) for row in ticks])
        for key, column in (
            ("energy", 5),
            ("hunger", 6),
            ("thirst", 7),
            ("fatigue", 8),
        )
    }
    metric_values: dict[str, list[float]] = {}
    for _tick, engine, metric, value in metrics:
        metric_values.setdefault(f"{engine}.{metric}", []).append(float(value))

    grouped_metrics: dict[str, dict[str, dict[str, float | int]]] = {}
    for key in sorted(metric_values):
        engine, metric = key.split(".", 1)
        grouped_metrics.setdefault(engine, {})[metric] = _stats(metric_values[key])

    return {
        "schemaVersion": 1,
        "runId": run_id,
        "status": "complete",
        "ticks": {"count": len(ticks), "first": int(ticks[0][1]), "last": int(ticks[-1][1])},
        "population": {
            "initial": int(ticks[0][4]),
            "final": int(ticks[-1][4]),
            "minimumAlive": min(int(row[3]) for row in ticks),
        },
        "needs": needs,
        "events": {key: event_counts[key] for key in sorted(event_counts)},
        "metrics": grouped_metrics,
        "calibrationPolicy": "observe-only; configuration changes require a reviewed decision",
    }
