# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Comparaison de runs PRISM (P10 / ECHOS-123).

L'acceptation du ticket est explicite : « deux runs identiques divergent à
zéro ; un paramètre changé diverge au tick attendu ». Ces tests couvrent donc
les quatre façons dont deux flux diffèrent — contenu, tick, type et longueur —
puis la surface HTTP.
"""

from __future__ import annotations

import json

import pytest
from fastapi.testclient import TestClient

from echos.analysis.prism_compare import PrismCompareError, compare_prism_runs
from echos.api.app import create_app
from echos.ingestion import PrismReadEnvelope
from echos.storage.sqlite import AnalyticsStore


def _envelope(sequence: int, tick: int, kind: str = "delta", payload=None) -> dict:
    return {
        "protocolVersion": "1.0.0",
        "sequence": sequence,
        "tick": tick,
        "kind": kind,
        "payload": payload if payload is not None else {"entities": []},
    }


def _envelopes(*items: dict) -> list[dict]:
    parsed = [
        PrismReadEnvelope.parse(json.dumps(item)).to_dict() for item in items
    ]
    return parsed


def _persist(
    store: AnalyticsStore,
    run_id: str,
    seed: str,
    content_hash: str,
    items: list[dict],
) -> None:
    store.append_prism_read_stream(run_id, seed, content_hash, items)


def _stream(payload_at_tick_one=None, second_tick: int = 1) -> list[dict]:
    return _envelopes(
        _envelope(0, -1, "world_snapshot"),
        _envelope(1, second_tick, "tick_result", payload_at_tick_one or {"events": []}),
    )


def test_identical_runs_diverge_at_zero(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist(store, "run-a", "42", "content-1", _stream())
        _persist(store, "run-b", "42", "content-1", _stream())

        result = compare_prism_runs(store, "run-a", "run-b")

        assert result["same_content_hash"] is True
        assert result["same_seed"] is True
        assert result["diverged"] is False
        assert result["first_divergence"] is None
        assert result["envelope_counts"] == {"run_a": 2, "run_b": 2}


def test_changed_content_hash_is_reported_before_any_conclusion(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist(store, "run-a", "42", "content-1", _stream())
        _persist(store, "run-b", "42", "content-2", _stream())

        result = compare_prism_runs(store, "run-a", "run-b")

        assert result["same_content_hash"] is False
        assert result["diverged"] is False


def test_changed_payload_diverges_at_the_expected_tick(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist(store, "run-a", "42", "content", _stream())
        _persist(
            store,
            "run-b",
            "42",
            "content",
            _stream(payload_at_tick_one={"events": [{"type": "item_transferred"}]}),
        )

        result = compare_prism_runs(store, "run-a", "run-b")

        assert result["diverged"] is True
        assert result["first_divergence"]["tick"] == 1
        assert result["first_divergence"]["sequence"] == 1
        assert result["first_divergence"]["field"] == "payload"


def test_changed_tick_is_named_as_a_tick_divergence(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist(store, "run-a", "42", "content", _stream())
        _persist(store, "run-b", "42", "content", _stream(second_tick=2))

        result = compare_prism_runs(store, "run-a", "run-b")

        assert result["first_divergence"]["field"] == "tick"
        assert result["first_divergence"]["run_a"] == 1
        assert result["first_divergence"]["run_b"] == 2


def test_changed_kind_is_named_as_a_kind_divergence(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist(store, "run-a", "42", "content", _stream())
        _persist(
            store,
            "run-b",
            "42",
            "content",
            _envelopes(
                _envelope(0, -1, "world_snapshot"),
                _envelope(1, 1, "entity_snapshot"),
            ),
        )

        result = compare_prism_runs(store, "run-a", "run-b")

        assert result["first_divergence"]["field"] == "kind"
        assert result["first_divergence"]["run_b"] == "entity_snapshot"


def test_shorter_stream_reports_the_missing_envelope(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist(store, "run-a", "42", "content", _stream())
        _persist(
            store,
            "run-b",
            "42",
            "content",
            _envelopes(_envelope(0, -1, "world_snapshot")),
        )

        result = compare_prism_runs(store, "run-a", "run-b")

        assert result["first_divergence"]["field"] == "missing_envelope"
        assert result["first_divergence"]["run_a"] == 1
        assert result["first_divergence"]["run_b"] is None


def test_comparison_refuses_a_run_without_a_prism_stream(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist(store, "run-a", "42", "content", _stream())
        store.record_run("syne-run", "0.1.0", "42")

        with pytest.raises(PrismCompareError, match="aucun flux"):
            compare_prism_runs(store, "run-a", "syne-run")


def test_compare_api_exposes_the_divergence_and_rejects_foreign_runs(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist(store, "run-a", "42", "content", _stream())
        _persist(store, "run-b", "42", "content", _stream(second_tick=3))
        store.record_run("syne-run", "0.1.0", "42")
        client = TestClient(create_app(store))

        body = client.get(
            "/api/prism/compare", params={"run_a": "run-a", "run_b": "run-b"}
        )
        assert body.status_code == 200
        payload = body.json()
        assert payload["diverged"] is True
        assert payload["first_divergence"]["field"] == "tick"
        assert set(payload) >= {
            "run_a",
            "run_b",
            "same_content_hash",
            "same_seed",
            "first_divergence",
            "envelope_counts",
        }

        assert client.get(
            "/api/prism/compare", params={"run_a": "run-a", "run_b": "missing"}
        ).status_code == 404
        assert client.get(
            "/api/prism/compare", params={"run_a": "run-a", "run_b": "syne-run"}
        ).status_code == 404
