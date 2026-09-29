"""Moteur 2 — InformationPropagationMetrics (propagation de l'information).

Mesure la circulation et la dégradation de l'information (METRICS_SPEC.md §3).
S'appuie sur les événements ``message_sent`` (ADR-004) portés par le snapshot
(clé ``events``, fenêtre glissante alimentée par le pipeline) : volume,
diffusion 80 %, dégradation 10 %/hop, chaîne max et concentration des hubs.
Champs absents → repli neutre 0.0.
"""

from __future__ import annotations

from collections import Counter

from ._common import (
    alive_count,
    event_values,
    events_of,
    mean,
    safe_ratio,
)

ENGINE_NAME = "InformationPropagationMetrics"

METRICS = (
    "MessageVolume",
    "InformationDiffusionSpeed",
    "RumorAccuracyDegradation",
    "MaxMessageHops",
    "NetworkCentrality",
)

REQUIRES = {
    "InformationDiffusionSpeed": "events",
    "RumorAccuracyDegradation": "events",
    "MaxMessageHops": "events",
    "NetworkCentrality": "events",
}
"""Métriques calculées sur la fenêtre d'événements.

``MessageVolume`` est exclue : elle compte le tick courant et reste mesurée même
quand aucun message n'est parti (0.0 y est une mesure réelle, pas un repli).
Les quatre autres renvoient 0.0 faute d'événements, ce qui était
indiscernable d'un réseau réellement inactif.
"""

_DECAY_PER_HOP = 0.9
_DIFFUSION_COVERAGE = 0.8
"""Part de la population qui doit avoir émis pour que la diffusion soit acquise."""


def _messages(snapshot: dict) -> list[dict]:
    return events_of(snapshot, event_type="message_sent")


def _diffusion_speed(messages: list[dict], count: int) -> float:
    """Durée (en ticks) de propagation jusqu'à 80 % d'émetteurs distincts.

    Mesure l'**amplitude** entre le premier message observé et le tick où le
    seuil de couverture est atteint, pas le numéro de tick absolu. La version
    précédente renvoyait le tick lui-même, ce qui :
    - croissait linéairement avec la longueur du run, donc
      ``DiffusionSpeed_Norm = 1 - speed/100`` tombait à 0 après le tick 100
      quel que soit le comportement réel du réseau, et
    -       rendait ``SystemComplexity`` non borné, cet indicateur ne l'étant pas.

    Fenêtre sans assez de messages ou population nulle → 0.0 (non mesuré).
    """
    if not messages or count <= 0:
        return 0.0
    by_tick: dict[int, set[str]] = {}
    for message in messages:
        by_tick.setdefault(int(message.get("tick") or 0), set()).add(
            str(message.get("agentId"))
        )
    target = _DIFFUSION_COVERAGE * count
    seen: set[str] = set()
    for tick in sorted(by_tick):
        seen |= by_tick[tick]
        if len(seen) >= target:
            return float(tick - min(by_tick))
    return 0.0


def compute(snapshot: dict) -> dict:
    """Calcule les 5 métriques de propagation de l'information."""
    count = alive_count(snapshot)
    messages = _messages(snapshot)

    current_tick = int(snapshot.get("tick") or 0)
    volume = sum(
        1 for message in messages if int(message.get("tick") or 0) == current_tick
    )

    hops = [
        float(event_values(message)["hops"])
        for message in messages
        if "hops" in event_values(message)
    ]
    degradation = (
        mean([1.0 - (_DECAY_PER_HOP ** hop) for hop in hops]) if hops else 0.0
    )

    senders = Counter(str(message.get("agentId")) for message in messages)

    return {
        "MessageVolume": safe_ratio(float(volume), count),
        "InformationDiffusionSpeed": _diffusion_speed(messages, count),
        "RumorAccuracyDegradation": degradation,
        "MaxMessageHops": max(hops, default=0.0),
        # Part d'émetteurs par rapport au nombre de messages : 1.0 = chaque
        # message vient d'un auteur distinct (aucun émetteur dominant).
        "NetworkCentrality": safe_ratio(len(senders), len(messages)),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
