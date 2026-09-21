"""Moteur 2 — InformationPropagationMetrics (propagation de l'information).

Mesure la circulation et la dégradation de l'information (METRICS_SPEC.md §3).
S'appuie sur les événements ``message_sent`` (ADR-004) portés par le snapshot
(clé ``events``) : volume, diffusion 80 %, dégradation 10 %/hop, chaîne max et
concentration des hubs. Champs absents → repli neutre 0.0.
"""

from __future__ import annotations

from collections import Counter

from ._common import (
    agents_of,
    alive_count,
    events_of,
    mean,
)

ENGINE_NAME = "InformationPropagationMetrics"

METRICS = (
    "MessageVolume",
    "InformationDiffusionSpeed",
    "RumorAccuracyDegradation",
    "MaxMessageHops",
    "NetworkCentrality",
)

_DECAY_PER_HOP = 0.9


def _messages(snapshot: dict) -> list[dict]:
    return events_of(snapshot, event_type="message_sent")


def compute(snapshot: dict) -> dict:
    """Calcule les 5 métriques de propagation de l'information."""
    count = alive_count(snapshot) or len(agents_of(snapshot))
    messages = _messages(snapshot)

    current_tick = int(snapshot.get("tick") or 0)
    volume = sum(
        1 for message in messages if int(message.get("tick") or 0) == current_tick
    )

    # Diffusion : tick (le plus précoce) où 80 % des entités ont été sources.
    diffusion = 0.0
    if messages and count > 0:
        by_tick: dict[int, set[str]] = {}
        for message in messages:
            by_tick.setdefault(int(message.get("tick") or 0), set()).add(
                str(message.get("agentId"))
            )
        seen: set[str] = set()
        for tick in sorted(by_tick):
            seen |= by_tick[tick]
            if len(seen) >= 0.8 * count:
                diffusion = float(tick)
                break

    hops = [float(message["value"].get("hops", 0.0)) for message in messages
            if message.get("value") and "hops" in message["value"]]
    degradation = (
        mean([1.0 - (_DECAY_PER_HOP ** hop) for hop in hops]) if hops else 0.0
    )

    senders = Counter(str(message.get("agentId")) for message in messages)

    return {
        "MessageVolume": float(volume) / count if count else 0.0,
        "InformationDiffusionSpeed": diffusion,
        "RumorAccuracyDegradation": degradation,
        "MaxMessageHops": max(hops, default=0.0),
        "NetworkCentrality": (
            len(senders) / len(messages) if messages else 0.0
        ),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
