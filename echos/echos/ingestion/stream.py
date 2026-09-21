"""Consommation du flux SYNE **alignée sur les ticks** (ECHOS-010).

Le contrat d'émission garantit, pour chaque tick : **1 snapshot** puis ses
événements (``tick_summary`` + ``decision_made``/entité). :func:`aligned_ticks`
découpe le flux en segments ``TickSegment`` (1 par tick) et refuse toute
séquence non alignée (événement hors du tick courant → erreur déterministe).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Iterator

from echos.ingestion.models import (
    ExternalEvent,
    InvalidMessageError,
    WorldSnapshot,
)
from echos.ingestion.ws_client import WsClient


class TickAlignmentError(InvalidMessageError):
    """Séquence du flux non alignée sur la boucle de tick SYNE."""


@dataclass(frozen=True)
class TickSegment:
    """Contenu complet d'un tick : snapshot + événements associés."""

    tick: int
    snapshot: WorldSnapshot
    events: list[ExternalEvent] = field(default_factory=list)

    @property
    def agents(self) -> list[object]:
        """Entités observées du tick (désigne les ``Agent`` du snapshot)."""
        return list(self.snapshot.agents)

    @property
    def event_types(self) -> list[str]:
        """Types des événements du tick, dans l'ordre d'arrivée."""
        return [event.type for event in self.events]

    def to_dict(self) -> dict:
        """Dump deterministic (tests golden) du segment complet."""
        return {
            "tick": self.tick,
            "snapshot": self.snapshot.model_dump(mode="json", exclude_none=True),
            "events": [
                event.model_dump(mode="json", exclude_none=True)
                for event in self.events
            ],
        }


def aligned_ticks(client: WsClient) -> Iterator[TickSegment]:
    """Itère les segments par tick (le snapshot déclenche le segment suivant).

    Un événement arrivant avant tout snapshot, ou avec un tick différent du
    snapshot courant, est refusé (``TickAlignmentError``) : le flux doit être
    strictement aligné sur la boucle SYNE (1 snapshot + événements du même tick).
    """
    current: WorldSnapshot | None = None
    events: list[ExternalEvent] = []

    for message in client:
        if isinstance(message, WorldSnapshot):
            if current is not None:
                yield TickSegment(tick=current.tick, snapshot=current, events=events)
            current = message
            events = []
            continue

        if current is None:
            raise TickAlignmentError(
                "event",
                f"événement {message.type} reçu avant le premier snapshot",
            )
        if message.tick != current.tick:
            raise TickAlignmentError(
                "event",
                f"événement {message.type} désaligné "
                f"(tick {message.tick} ≠ snapshot {current.tick})",
            )
        events.append(message)

    if current is not None:
        yield TickSegment(tick=current.tick, snapshot=current, events=events)
