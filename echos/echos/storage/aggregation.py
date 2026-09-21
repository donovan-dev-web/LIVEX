"""Agrégation incrémentale par tick (ECHOS-011).

Chaque :class:`TickSegment` (ECHOS-010) est réduit de façon **déterministe**
en un :class:`TickRecord` : la série couvre **tous** les ticks (sans perte) et
le sous-échantillonnage ``downsample(records, every)`` est explicite à la
lecture (API_REST.md §4, ``?every=N``).
"""

from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
from statistics import mean
from typing import Iterable, Iterator

from echos.ingestion.stream import TickSegment, aligned_ticks
from echos.ingestion.ws_client import WsClient


@dataclass(frozen=True)
class TickRecord:
    """Résumé déterministe d'un tick (1 ligne par tick en SQLite)."""

    run_id: str
    version: str
    tick: int
    simulated_time_minutes: int
    alive_count: int
    agent_count: int
    mean_energy: float
    mean_hunger: float
    mean_thirst: float
    mean_fatigue: float
    decision_count: int
    actions: tuple[tuple[str, int], ...] = ()

    @classmethod
    def from_segment(cls, segment: TickSegment) -> "TickRecord":
        """Réduit un segment (snapshot + événements) en un résumé de tick."""
        snapshot = segment.snapshot
        agents = snapshot.agents
        hunger = [agent.hunger for agent in agents]
        thirst = [agent.thirst for agent in agents]
        energy = [agent.energy for agent in agents]
        fatigue = [agent.fatigue or 0.0 for agent in agents]
        decisions = [e for e in segment.events if e.type == "decision_made"]
        actions = Counter(
            agent.current_action or "Idle" for agent in agents
        )

        return cls(
            run_id=snapshot.run_id,
            version=snapshot.version,
            tick=snapshot.tick,
            simulated_time_minutes=snapshot.simulated_time_minutes,
            alive_count=snapshot.alive_count,
            agent_count=len(agents),
            mean_energy=_mean(energy),
            mean_hunger=_mean(hunger),
            mean_thirst=_mean(thirst),
            mean_fatigue=_mean(fatigue),
            decision_count=len(decisions),
            actions=tuple(sorted(actions.items())),
        )

    def to_row(self) -> tuple:
        """Vue ligne (ordre des colonnes de ``tick_summaries``, ECHOS-012)."""
        return (
            self.run_id,
            self.tick,
            self.simulated_time_minutes,
            self.alive_count,
            self.agent_count,
            self.mean_energy,
            self.mean_hunger,
            self.mean_thirst,
            self.mean_fatigue,
            self.decision_count,
        )


def summarize(
    client: WsClient, sample_every: int | None = None
) -> Iterator[TickRecord]:
    """Résume le flux en :class:`TickRecord` durables (1 par tick).

    ``sample_every`` (``--sample-every=N``, API_REST.md §4) : ne conserve que
    les ticks de rang multiple strict de ``N`` ; ``None`` (défaut) conserve
    **tous** les ticks (agrégation sans perte).
    """
    for index, segment in enumerate(aligned_ticks(client)):
        if sample_every is not None and index % sample_every != 0:
            continue
        yield TickRecord.from_segment(segment)


def downsample(records: Iterable[TickRecord], every: int) -> list[TickRecord]:
    """Sous-échantillonnage de lecture : 1 tick sur ``every`` (index-based)."""
    if every <= 1:
        return list(records)
    ordered = list(records)
    return [ordered[index] for index in range(0, len(ordered), every)]


def _mean(values: list[float]) -> float:
    return mean(values) if values else 0.0
