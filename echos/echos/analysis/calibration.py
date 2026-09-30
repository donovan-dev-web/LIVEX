"""Post-run calibration evidence (SYNE-131).

This module summarizes observed runs into deterministic, read-only evidence.
It never changes simulator configuration: parameter choices stay explicit and
require a reviewed calibration decision.

Depuis le jalan viabilité (B2, campagne-runs 29/09/2026), le rapport porte
aussi le résultat de population (``outcome``/``extinctionTick``, A3) et un
bloc ``viability`` (pente d'énergie, part des actions Eat/Drink quand la faim
est saturée, régime des ressources) : la mort lente — une énergie moyenne qui
descend sans extinction — devient un signal de premier citoyen, au lieu d'être
découverte à la lecture des séries.
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


def _least_squares_slope(values: list[float]) -> float:
    """Pente (moindres carrés) d'une série ordonnée, par pas d'index.

    La série étant échantillonnée à cadence constante, l'index fait office de
    tick : la pente est donc exprimée par tick. Constante nulle si la série
    est plate ou trop courte (``len < 2``).
    """
    count = len(values)
    if count < 2:
        return 0.0
    mean_x = (count - 1) / 2
    mean_y = fsum(values) / count
    variance = fsum((index - mean_x) ** 2 for index in range(count))
    if variance == 0.0:
        return 0.0
    covariance = fsum(
        (index - mean_x) * (value - mean_y) for index, value in enumerate(values)
    )
    return covariance / variance


_EAT_OR_DRINK_ACTIONS = frozenset({"Eat", "Drink", "SeekFood", "SeekWater"})
_HUNGRY_THRESHOLD = 70.0
_VIABILITY_WINDOW = 200
"""Fenêtre (ticks) de la pente d'énergie : les 200 derniers ticks observés,
comme les critères d'acceptation de la campagne (§4-B1 du plan)."""


def _population_outcome(ticks: list[tuple]) -> dict[str, Any]:
    """Résultat de population au dernier tick observé (A3).

    ``outcome`` vaut ``extinct`` dès qu'un tick observe ``alive_count == 0``
    (l'extinction est définitive en V0.1 : aucun re-peuplement), ``surviving``
    sinon ; ``extinctionTick`` porte le premier tick d'extinction, ``None``
    sinon. Fonction pure des résumés existants : le rapport reste
    déterministe et timestamp-free (SYNE-131).
    """
    extinction_tick = next(
        (int(row[1]) for row in ticks if int(row[3]) == 0), None
    )
    return {
        "outcome": "extinct" if extinction_tick is not None else "surviving",
        "extinctionTick": extinction_tick,
    }


def _viability_block(
    ticks: list[tuple], events: list[tuple]
) -> dict[str, Any]:
    """Signaux de viabilité observés (B2, observe-only).

    - ``energySlopePerTick`` : pente de ``mean_energy`` sur les derniers ticks —
      détecte la mort lente **même sans extinction** (le cas du run 1) ;
    - ``actionSharesWhenHungry`` : distribution des actions des
      ``decision_made`` sur les ticks où ``mean_hunger > 70`` — expose la
      sous-priorisation Eat/Drink constatée (9,7 % des décisions) ;
    - ``resourceRegime`` : stats min/moy/max des séries ``food``/``water``.
    """
    energy_series = [float(row[5]) for row in ticks]
    window = energy_series[-_VIABILITY_WINDOW:]

    hungry_counts: Counter[str] = Counter()
    hungry_ticks = {
        int(row[1]) for row in ticks if float(row[6]) > _HUNGRY_THRESHOLD
    }
    if hungry_ticks:
        for event in events:
            if event[1] != "decision_made" or event[0] not in hungry_ticks:
                continue
            action = event[3] or "Unknown"
            hungry_counts[action] += 1
    total_hungry = sum(hungry_counts.values())
    action_shares = {
        action: round(count / total_hungry, 8)
        for action, count in sorted(hungry_counts.items())
    }

    return {
        "windowTicks": min(_VIABILITY_WINDOW, len(energy_series)),
        "energySlopePerTick": round(_least_squares_slope(window), 8),
        "actionSharesWhenHungry": action_shares,
        "hungryDecisions": total_hungry,
        "resourceRegime": {
            "food": _resource_stats(ticks, column=10),
            "water": _resource_stats(ticks, column=11),
        },
    }


def _resource_stats(ticks: list[tuple], column: int) -> dict[str, float | int] | None:
    """Stats d'une colonne optionnelle des résumés (``food``/``water``).

    Les résumés historiques (schéma d'origine : 10 colonnes, sans
    ``mean_food``/``mean_water``) omettent ces colonnes : le bloc renvoie
    ``null`` plutôt que de lever — le rapport reste produisible sur toute
    base existante. La détection porte sur la **longueur** de ligne, pas sur
    l'absence : une réserve réellement à 0.0 doit rester une valeur observée.
    """
    if any(len(row) <= column for row in ticks):
        return None
    values = [float(row[column]) for row in ticks if row[column] is not None]
    return _stats(values) if values else None


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
        "schemaVersion": 2,
        "runId": run_id,
        "status": "complete",
        **_population_outcome(ticks),
        "ticks": {"count": len(ticks), "first": int(ticks[0][1]), "last": int(ticks[-1][1])},
        "population": {
            "initial": int(ticks[0][4]),
            "final": int(ticks[-1][4]),
            "minimumAlive": min(int(row[3]) for row in ticks),
        },
        "needs": needs,
        "viability": _viability_block(ticks, events),
        "events": {key: event_counts[key] for key in sorted(event_counts)},
        "metrics": grouped_metrics,
        "calibrationPolicy": "observe-only; configuration changes require a reviewed decision",
    }
