"""Moteur 7 — GroupDynamicsMetrics (dynamique des groupes).

Mesure la formation, la stabilité et la rotation des groupes (METRICS_SPEC.md
§8) à partir des communautés du graphe de confiance (``trust``, U2) et des
événements ``group_formed`` / ``group_dissolved`` (historique optionnel).
Repli neutre 0.0 quand les données font défaut.
"""

from __future__ import annotations

from ._common import (
    alive_count,
    community_sizes,
    events_of,
    mean,
)

ENGINE_NAME = "GroupDynamicsMetrics"

METRICS = (
    "ActiveGroups",
    "AverageGroupSize",
    "AverageGroupLifetime",
    "GroupFormationRate",
    "GroupDissolutionRate",
    "GroupObjectiveSuccessRate",
    "MemberTurnoverRate",
)

_WINDOW_TICKS = 1000


def _members_of(snapshot: dict) -> int:
    return alive_count(snapshot)


def _group_events(snapshot: dict) -> list[dict]:
    """Événements group_formed / group_dissolved (ADR-004) triés par tick."""
    formed = events_of(snapshot, event_type="group_formed")
    dissolved = events_of(snapshot, event_type="group_dissolved")
    return formed + dissolved


def _window_ticks(events: list[dict]) -> int:
    """Durée couverte (ticks) par les événements de groupes (min 1)."""
    ticks = [int(event.get("tick") or 0) for event in events]
    if not ticks:
        return _WINDOW_TICKS
    return max(1, max(ticks) - min(ticks) + 1)


def compute(snapshot: dict) -> dict:
    """Calcule les 7 métriques de dynamique des groupes sur un snapshot."""
    sizes = community_sizes(snapshot.get("agents") or [])
    active_groups = len([size for size in sizes])

    formed = events_of(snapshot, event_type="group_formed")
    dissolved = events_of(snapshot, event_type="group_dissolved")
    window = _window_ticks(_group_events(snapshot))

    lifetimes = [
        float(event.get("value", {}).get("lifetime") or 0.0)
        for event in dissolved
        if event.get("value", {}).get("lifetime") is not None
    ]

    successes = [
        bool(event.get("value", {}).get("success"))
        for event in dissolved
        if event.get("value", {}).get("success") is not None
    ]
    success_rate = mean([1.0 if success else 0.0 for success in successes])

    rotations = [
        float(event.get("value", {}).get("membersOut") or 0.0)
        for event in dissolved
        if event.get("value", {}).get("membersOut") is not None
    ]
    members_in = [
        float(event.get("value", {}).get("membersIn") or 1e-9)
        for event in dissolved
        if event.get("value", {}).get("membersIn") is not None
    ]
    turnover = mean(
        [
            out / max(members, 1e-9) * (_WINDOW_TICKS / window)
            for out, members in zip(rotations, members_in)
        ]
    )

    return {
        "ActiveGroups": float(active_groups),
        "AverageGroupSize": mean(sizes) if sizes else 0.0,
        "AverageGroupLifetime": mean(lifetimes) if lifetimes else 0.0,
        "GroupFormationRate": (
            len(formed) * (_WINDOW_TICKS / window) if formed else 0.0
        ),
        "GroupDissolutionRate": (
            len(dissolved) * (_WINDOW_TICKS / window) if dissolved else 0.0
        ),
        "GroupObjectiveSuccessRate": success_rate,
        "MemberTurnoverRate": turnover,
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
