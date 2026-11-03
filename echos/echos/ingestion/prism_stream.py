# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Validation et ingestion du flux de lecture PRISM (P10 / ECHOS-100)."""

from __future__ import annotations

import json
from collections.abc import Iterable
from typing import TYPE_CHECKING, Any

from pydantic import BaseModel, ConfigDict, Field, ValidationError
from pydantic.alias_generators import to_camel

from echos.ingestion.models import InvalidMessageError
from echos.ingestion.ws_client import WsClient

if TYPE_CHECKING:
    from echos.storage.sqlite import AnalyticsStore


_MAX_INT64 = (1 << 63) - 1


class PrismReadEnvelope(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
        strict=True,
    )

    protocol_version: str
    sequence: int = Field(ge=0, le=_MAX_INT64)
    tick: int = Field(ge=-1, le=_MAX_INT64)
    kind: str = Field(min_length=1)
    payload: dict[str, Any]

    @classmethod
    def parse(cls, raw: str | bytes) -> "PrismReadEnvelope":
        try:
            data = raw if isinstance(raw, str) else raw.decode("utf-8")
            value = json.loads(data)
            envelope = cls.model_validate(value)
        except (UnicodeDecodeError, json.JSONDecodeError, ValidationError) as exc:
            raise InvalidMessageError("prism_read", str(exc)) from exc
        parts = envelope.protocol_version.split(".")
        if (
            len(parts) != 3
            or not all(part.isascii() and part.isdigit() for part in parts)
            or parts[0] != "1"
        ):
            raise InvalidMessageError(
                "prism_read",
                f"version de protocole PRISM incompatible : {envelope.protocol_version}",
            )
        if not envelope.kind.strip():
            raise InvalidMessageError("prism_read", "le champ kind est vide")
        return envelope

    def to_dict(self) -> dict[str, Any]:
        return self.model_dump(mode="json", by_alias=True)


def prism_envelopes(client: WsClient) -> Iterable[PrismReadEnvelope]:
    """Valide les payloads d'une connexion sans les interpréter comme SYNE."""
    for raw in client.raw_messages():
        yield PrismReadEnvelope.parse(raw)


def ingest_prism_stream(
    store: AnalyticsStore,
    run_id: str,
    seed: int,
    content_hash: str,
    envelopes: Iterable[PrismReadEnvelope],
) -> dict[str, Any]:
    """Persiste un flux PRISM fini de manière atomique dans ECHOS."""
    if not run_id.strip():
        raise ValueError("run_id must not be empty")
    if seed < 0:
        raise ValueError("seed must be non-negative")
    if not content_hash.strip():
        raise ValueError("content_hash must not be empty")

    rows = []
    previous_sequence = -1
    previous_tick = -1
    for envelope in envelopes:
        if envelope.sequence <= previous_sequence:
            raise InvalidMessageError(
                "prism_read",
                f"séquence non croissante : {envelope.sequence} après {previous_sequence}",
            )
        if previous_sequence < 0 and envelope.kind != "world_snapshot":
            raise InvalidMessageError(
                "prism_read",
                "le flux doit commencer par un snapshot complet",
            )
        if (
            previous_sequence >= 0
            and envelope.sequence != previous_sequence + 1
            and envelope.kind != "world_snapshot"
        ):
            raise InvalidMessageError(
                "prism_read",
                "un saut de séquence doit être suivi d'un snapshot complet",
            )
        if envelope.tick < previous_tick:
            raise InvalidMessageError(
                "prism_read",
                f"tick décroissant : {envelope.tick} après {previous_tick}",
            )
        rows.append(envelope.to_dict())
        previous_sequence = envelope.sequence
        previous_tick = envelope.tick

    if not rows:
        raise InvalidMessageError("prism_read", "le flux PRISM est vide")
    store.append_prism_read_stream(run_id, str(seed), content_hash, rows)
    return {
        "runId": run_id,
        "contentHash": content_hash,
        "envelopes": len(rows),
        "lastSequence": previous_sequence,
        "lastTick": previous_tick,
    }
