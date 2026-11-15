# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Ingestion de production du flux PRISM (P10 / ECHOS-120).

Ce que ces tests verrouillent, et qui manquait au socle v8 : l'ingestion d'un
flux **long et interruptible**. Un flux fini écrit en une transaction ne prouve
ni la reprise après coupure, ni l'idempotence d'un rejeu, ni la continuité
validée contre la base plutôt que contre le lot courant.
"""

from __future__ import annotations

import json

import pytest
from fastapi.testclient import TestClient

from echos.api.app import create_app
from echos.ingestion import PrismReadEnvelope, WsClient
from echos.ingestion.prism_run import (
    PrismIngestError,
    PrismRunIngestor,
    ingest_prism_run,
)
from echos.storage.sqlite import AnalyticsStore


class FakeTransport:
    """Transport déterministe : sert les payloads fournis puis signale la fin."""

    def __init__(self, payloads: list[str]) -> None:
        self.payloads = list(payloads)

    def recv(self, timeout: float | None = None) -> str | None:
        return self.payloads.pop(0) if self.payloads else None

    def send(self, payload: str | bytes) -> None:
        pass

    def close(self) -> None:
        pass


def _envelope(
    sequence: int,
    tick: int,
    kind: str = "world_snapshot",
    payload: dict | None = None,
) -> dict:
    return {
        "protocolVersion": "1.0.0",
        "sequence": sequence,
        "tick": tick,
        "kind": kind,
        "payload": payload if payload is not None else {"entities": []},
    }


def _parsed(*envelopes: dict) -> list[PrismReadEnvelope]:
    return [PrismReadEnvelope.parse(json.dumps(item)) for item in envelopes]


def test_ingestor_writes_batches_and_resumes_after_a_cut(tmp_path):
    database = tmp_path / "echos.db"
    with AnalyticsStore(database) as store:
        ingestor = PrismRunIngestor(store, "prism-run-cut", 7, "hash-a", batch_size=2)
        ingestor.ingest(
            _parsed(
                _envelope(0, -1),
                _envelope(1, 0, "delta"),
                _envelope(2, 1, "delta"),
            )
        )

        # Lot de 2 écrit, troisième enveloppe encore tamponnée : la mémoire est
        # bornée, la persistance n'attend pas la fin du flux.
        assert ingestor.ingested == 2
        assert ingestor.pending == 1
        assert store.prism_read_summary("prism-run-cut")["envelopes"] == 2
        ingestor.flush()
        assert store.prism_read_summary("prism-run-cut")["envelopes"] == 3

    # Reprise : le consommateur rejoue le flux depuis son début, la base ignore
    # les séquences déjà présentes et n'accepte que la suite.
    with AnalyticsStore(database) as store:
        resumed = PrismRunIngestor(store, "prism-run-cut", 7, "hash-a", batch_size=10)
        resumed.ingest(
            _parsed(
                _envelope(0, -1),
                _envelope(1, 0, "delta"),
                _envelope(3, 2, "delta"),
            )
        )
        resumed.flush()

        assert resumed.ingested == 1
        summary = store.prism_read_summary("prism-run-cut")
        assert summary["envelopes"] == 4
        assert summary["lastSequence"] == 3
        assert summary["lastTick"] == 2


def test_replayed_window_is_idempotent(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        envelopes = _parsed(
            _envelope(0, -1),
            _envelope(1, 0, "tick_result"),
        )
        first = PrismRunIngestor(store, "prism-run-replay", 3, "hash", batch_size=10)
        first.ingest(envelopes)
        first.flush()
        assert first.ingested == 2

        second = PrismRunIngestor(store, "prism-run-replay", 3, "hash", batch_size=10)
        second.ingest(envelopes)
        second.flush()

        assert second.ingested == 0
        assert store.prism_read_summary("prism-run-replay")["envelopes"] == 2


def test_sequence_gap_without_snapshot_is_rejected_and_writes_nothing(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        ingestor = PrismRunIngestor(store, "prism-run-gap", 7, "hash", batch_size=10)
        ingestor.ingest(
            _parsed(
                _envelope(0, -1),
                _envelope(2, 0, "delta"),
            )
        )

        with pytest.raises(ValueError, match="sequence gap"):
            ingestor.flush()

        assert store.has_run("prism-run-gap") is False


def test_ingestor_refuses_a_different_identity_on_the_same_run(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        first = PrismRunIngestor(store, "prism-run-owned", 7, "hash-a", batch_size=10)
        first.ingest(_parsed(_envelope(0, -1)))
        first.flush()

        other_hash = PrismRunIngestor(
            store, "prism-run-owned", 7, "hash-b", batch_size=10
        )
        other_hash.ingest(_parsed(_envelope(1, 0, "delta")))
        with pytest.raises(ValueError, match="content hash"):
            other_hash.flush()

        other_seed = PrismRunIngestor(
            store, "prism-run-owned", 8, "hash-a", batch_size=10
        )
        other_seed.ingest(_parsed(_envelope(1, 0, "delta")))
        with pytest.raises(ValueError, match="different seed"):
            other_seed.flush()


def test_ingestor_validates_its_own_identity(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        with pytest.raises(PrismIngestError, match="run_id"):
            PrismRunIngestor(store, "   ", 1, "hash")
        with pytest.raises(PrismIngestError, match="content_hash"):
            PrismRunIngestor(store, "run", 1, "  ")
        with pytest.raises(PrismIngestError, match="seed"):
            PrismRunIngestor(store, "run", -1, "hash")
        with pytest.raises(PrismIngestError, match="batch_size"):
            PrismRunIngestor(store, "run", 1, "hash", batch_size=0)


def test_live_run_ingestion_feeds_the_prism_stream_api(tmp_path):
    payloads = [
        json.dumps(_envelope(0, -1)),
        json.dumps(_envelope(1, 0, "tick_result", {"events": []})),
    ]
    client = WsClient(FakeTransport(payloads))

    with AnalyticsStore(tmp_path / "echos.db") as store:
        result = ingest_prism_run(store, client, "prism-live", 11, "hash-b")

        assert result["envelopes"] == 2
        assert result["received"] == 2
        assert result["resumed"] is False
        assert result["lastSequence"] == 1

        api = TestClient(create_app(store))
        body = api.get("/api/runs/prism-live/prism-stream").json()
        assert body["summary"]["contentHash"] == "hash-b"
        assert [item["sequence"] for item in body["envelopes"]] == [0, 1]


def test_live_run_ingestion_stops_at_the_requested_bound(tmp_path):
    payloads = [
        json.dumps(_envelope(0, -1)),
        json.dumps(_envelope(1, 0, "delta")),
        json.dumps(_envelope(2, 1, "delta")),
    ]
    client = WsClient(FakeTransport(payloads))

    with AnalyticsStore(tmp_path / "echos.db") as store:
        result = ingest_prism_run(
            store, client, "prism-bounded", 5, "hash", batch_size=1, max_envelopes=2
        )

        assert result["received"] == 2
        assert result["envelopes"] == 2
        assert store.prism_read_summary("prism-bounded")["lastSequence"] == 1
