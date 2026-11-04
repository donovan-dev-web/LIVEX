# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

import json

import pytest
from fastapi.testclient import TestClient

from echos.api.app import create_app
from echos.ingestion import (
    InvalidMessageError,
    PrismReadEnvelope,
    WsClient,
    ingest_prism_stream,
    prism_envelopes,
)
from echos.storage.sqlite import AnalyticsStore


class FakeTransport:
    def __init__(self, payloads: list[str]) -> None:
        self.payloads = list(payloads)

    def recv(self, timeout: float | None = None) -> str | None:
        return self.payloads.pop(0) if self.payloads else None

    def send(self, payload: str | bytes) -> None:
        pass

    def close(self) -> None:
        pass


def _envelope(sequence: int, tick: int, kind: str = "world_snapshot") -> dict:
    return {
        "protocolVersion": "1.0.0",
        "sequence": sequence,
        "tick": tick,
        "kind": kind,
        "payload": {"entities": []},
    }


def test_prism_envelope_parses_camel_case_and_rejects_incompatible_versions():
    parsed = PrismReadEnvelope.parse(json.dumps(_envelope(0, -1)))

    assert parsed.protocol_version == "1.0.0"
    assert parsed.to_dict() == _envelope(0, -1)
    incompatible = _envelope(0, 0)
    incompatible["protocolVersion"] = "2.0.0"
    with pytest.raises(InvalidMessageError, match="incompatible"):
        PrismReadEnvelope.parse(json.dumps(incompatible))

    invalid_sequence = _envelope(0, 0)
    invalid_sequence["sequence"] = "0"
    with pytest.raises(InvalidMessageError):
        PrismReadEnvelope.parse(json.dumps(invalid_sequence))


def test_prism_envelope_rejects_empty_kind_and_out_of_range_tick():
    empty_kind = _envelope(0, 0, " ")
    with pytest.raises(InvalidMessageError, match="kind est vide"):
        PrismReadEnvelope.parse(json.dumps(empty_kind))

    invalid_tick = _envelope(0, -2)
    with pytest.raises(InvalidMessageError):
        PrismReadEnvelope.parse(json.dumps(invalid_tick))


def test_prism_stream_ingestion_persists_order_and_summary(tmp_path):
    payloads = [json.dumps(_envelope(0, -1)), json.dumps(_envelope(1, 1, "delta"))]
    client = WsClient(FakeTransport(payloads))

    with AnalyticsStore(tmp_path / "echos.db") as store:
        result = ingest_prism_stream(
            store, "prism-run-1", 42, "abc123", prism_envelopes(client)
        )

        assert result == {
            "runId": "prism-run-1",
            "contentHash": "abc123",
            "envelopes": 2,
            "lastSequence": 1,
            "lastTick": 1,
        }
        assert store.prism_read_summary("prism-run-1") == {
            "runId": "prism-run-1",
            "envelopes": 2,
            "lastSequence": 1,
            "lastTick": 1,
            "contentHash": "abc123",
            "kinds": {"delta": 1, "world_snapshot": 1},
        }
        stored = store._conn.execute(
            "SELECT sequence, payload FROM prism_read_envelopes "
            "WHERE run_id = ? ORDER BY sequence",
            ("prism-run-1",),
        ).fetchall()
        assert [row[0] for row in stored] == [0, 1]
        assert json.loads(stored[1][1]) == _envelope(1, 1, "delta")


def test_prism_stream_rejects_sequence_gap_without_snapshot_resync(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        with pytest.raises(InvalidMessageError, match="saut de séquence"):
            ingest_prism_stream(
                store,
                "prism-run-gap",
                42,
                "abc123",
                [
                    PrismReadEnvelope.parse(json.dumps(_envelope(1, 1))),
                    PrismReadEnvelope.parse(json.dumps(_envelope(3, 2, "delta"))),
                ],
            )

        assert store.has_run("prism-run-gap") is False


def test_prism_stream_allows_late_subscription_and_snapshot_resync(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        envelopes = [
            PrismReadEnvelope.parse(json.dumps(_envelope(8, 3))),
            PrismReadEnvelope.parse(json.dumps(_envelope(10, 4))),
        ]
        ingest_prism_stream(store, "prism-run-resync", 42, "abc123", envelopes)
        assert store.prism_read_summary("prism-run-resync")["lastSequence"] == 10


def test_prism_stream_rejects_tick_regression_without_persisting(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        envelopes = [
            PrismReadEnvelope.parse(json.dumps(_envelope(0, 2))),
            PrismReadEnvelope.parse(json.dumps(_envelope(1, 1, "delta"))),
        ]
        with pytest.raises(InvalidMessageError, match="tick décroissant"):
            ingest_prism_stream(store, "prism-run-ticks", 42, "abc123", envelopes)
        assert store.has_run("prism-run-ticks") is False


def test_prism_stream_is_not_overwritten_on_duplicate_run(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        envelope = PrismReadEnvelope.parse(json.dumps(_envelope(0, 0)))
        ingest_prism_stream(store, "prism-run-duplicate", 42, "abc123", [envelope])

        with pytest.raises(ValueError, match="already exists"):
            ingest_prism_stream(
                store, "prism-run-duplicate", 42, "abc123", [envelope]
            )

        assert store.prism_read_summary("prism-run-duplicate")["envelopes"] == 1


def test_prism_stream_api_pages_persisted_envelopes(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        envelopes = [
            PrismReadEnvelope.parse(json.dumps(_envelope(0, -1))),
            PrismReadEnvelope.parse(json.dumps(_envelope(1, 1, "tick_result"))),
        ]
        ingest_prism_stream(store, "prism-run-api", 42, "abc123", envelopes)
        client = TestClient(create_app(store))

        first = client.get("/api/runs/prism-run-api/prism-stream", params={"limit": 1})
        second = client.get(
            "/api/runs/prism-run-api/prism-stream",
            params={"after_sequence": 0, "limit": 1},
        )

        assert first.status_code == 200
        assert first.json()["has_more"] is True
        assert first.json()["envelopes"][0]["sequence"] == 0
        assert second.json()["has_more"] is False
        assert second.json()["envelopes"][0]["sequence"] == 1
        assert second.json()["envelopes"][0]["contentHash"] == "abc123"
        assert client.get(
            "/api/runs/prism-run-api/prism-stream",
            params={"limit": 5001},
        ).status_code == 422
        assert client.get(
            "/api/runs/prism-run-api/prism-stream",
            params={"after_sequence": -2},
        ).status_code == 422


def test_prism_stream_api_returns_not_found_for_other_or_unknown_runs(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        store.record_run("syne-run", "0.1.0", "7")
        client = TestClient(create_app(store))

        assert client.get("/api/runs/syne-run/prism-stream").status_code == 404
        assert client.get("/api/runs/missing/prism-stream").status_code == 404
