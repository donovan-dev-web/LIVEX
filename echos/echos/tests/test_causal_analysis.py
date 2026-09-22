"""Analyse causale (ECHOS-060 → ECHOS-063, jalon ph6, preuve J6).

Reconstruction de chaînes causales ``Action ← Intention ← Objectif ← Besoin
← Croyance ← Mémoire ← Perception`` sur les traces persistées (ADR-002
[Accepted] : calcul hors ligne, PAS temps réel), détection des boucles de
rétroaction (récurrence de l'action) et cache LRU invalidé sur la version
d'ingestion — plus l'endpoint ``/causal-chains`` (404/422/503, déterminisme).

Les stores sont peuplés via ``build_decision_trace``/``append_decision_trace``
(vrais moteurs SYNE simulés) + événements ``decision_made``/``message_received``
+ contexte ``agents`` — aucune écriture dans le monde observé.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from echos.analysis.causal import (
    LAYERS,
    MAX_DEPTH,
    Node,
    CausalError,
    build_chain,
)
from echos.api.app import create_app
from echos.api.causal_cache import CausalCache
from echos.storage.aggregation import TickRecord
from echos.storage.sqlite import AnalyticsStore

FIXTURES = Path(__file__).resolve().parent / "fixtures"

_AGENT = {
    "id": "A",
    "goals": [{"kind": "SeekFood"}, {"kind": "Eat"}],
    "beliefs": [{"subject": "food-1"}, {"subject": "food-1"}, {"subject": "water-2"}],
    "memoryCount": 4,
}


def _trace(tick: int, action: str = "SeekFood") -> dict:
    return {
        "run_id": "run-7",
        "tick": tick,
        "agent_id": "A",
        "chosen_action": action,
        "utility": 0.75,
        "deliberated": True,
        "interrupted": False,
        "cause": "hunger=80,thirst=20,fatigue=5",
        "beliefs_count": 2,
        "goals_count": 2,
        "memory_count": 4,
        "needs": {"hunger": 80.0, "thirst": 20.0, "fatigue": 5.0},
    }


def _populated(
    store: AnalyticsStore,
    run_id: str = "run-7",
    ticks: int = 3,
    action: str = "SeekFood",
) -> None:
    """Peuple un run avec traces/contextes/événements — pré-requis J6."""
    store.record_run(run_id, "0.1.0", seed="7")
    for tick in range(1, ticks + 1):
        record = TickRecord(
            run_id=run_id,
            version="0.1.0",
            tick=tick,
            simulated_time_minutes=tick,
            alive_count=1,
            agent_count=1,
            mean_energy=40.0,
            mean_hunger=30.0,
            mean_thirst=20.0,
            mean_fatigue=5.0,
            decision_count=1,
        )
        store.append_tick(record)
        store.append_tick_context(run_id, tick, "agents", [_AGENT])
        store.append_decision_trace(run_id, tick, _trace(tick, action))
        store.append_event(
            run_id,
            tick,
            "decision_made",
            agent_id="A",
            action=action,
            value=json.dumps(
                {"intention": action, "utility": 0.75, "deliberated": True}
            ),
        )
        store.append_event(
            run_id,
            tick,
            "message_received",
            agent_id="A",
            action="Information",
            value=json.dumps({"messageId": 7 * 1000 + tick}),
        )


def _client(store: AnalyticsStore | None = None) -> TestClient:
    return TestClient(create_app(store))


# ---------------------------------------------------------------------------
# Reconstruction de chaîne (ECHOS-061)
# ---------------------------------------------------------------------------


def test_build_chain_default_tick_is_latest(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db)

    chain = build_chain(db, "run-7", "A")

    assert chain["tick"] == 3
    assert chain["depth_served"] == 7
    assert [node["layer"] for node in chain["chain"]] == list(LAYERS)
    assert chain["chain"][0]["layer"] == "Action"
    assert chain["chain"][0]["label"] == "SeekFood"
    assert chain["chain"][-1]["layer"] == "Perception"
    assert chain["chain"][-1]["label"] == "Information"
    assert chain["truncated"] is False


def test_build_chain_layer_order_and_details():
    nodes = [
        Node("Action", 3, "SeekFood", {"utility": 0.75}),
        Node("Intention", 3, "SeekFood", {}),
        Node("Objectif", 3, "SeekFood", {"kinds": ["SeekFood", "Eat"], "goals_count": 2}),
        Node("Besoin", 3, "hunger (80.0)", {"needs": {"hunger": 80.0}}),
        Node("Croyance", 3, "2 croyance(s)", {"subjects": ["food-1", "water-2"]}),
        Node("Mémoire", 3, "4 souvenir(s)", {"memory_count": 4}),
        Node("Perception", 3, "Information", {"received": []}),
    ]
    assert [node.layer for node in nodes] == list(LAYERS)


def test_build_chain_needs_are_ranked_max_first(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db)

    chain = build_chain(db, "run-7", "A", tick=3)

    besoin = chain["chain"][3]
    assert besoin["layer"] == "Besoin"
    assert besoin["label"] == "hunger (80.0)"
    assert chain["chain"][4]["detail"]["subjects"] == ["food-1", "water-2"]
    assert chain["chain"][2]["detail"]["kinds"] == ["SeekFood", "Eat"]
    assert chain["chain"][5]["label"] == "4 souvenir(s)"


def test_build_chain_depth_truncates(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db)

    chain = build_chain(db, "run-7", "A", tick=3, depth=3)

    assert chain["depth_requested"] == 3
    assert chain["depth_served"] == 3
    assert chain["truncated"] is True
    assert len(chain["chain"]) == 3


def test_build_chain_invalid_depth_raises(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db)
    with pytest.raises(CausalError):
        build_chain(db, "run-7", "A", depth=0)
    with pytest.raises(CausalError):
        build_chain(db, "run-7", "A", max_depth=13)


def test_build_chain_no_trace_raises(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db)
    with pytest.raises(CausalError):
        build_chain(db, "run-7", "Ghost", tick=3)


def test_build_chain_unknown_run_raises(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    with pytest.raises(CausalError):
        build_chain(db, "run-999", "A")


# ---------------------------------------------------------------------------
# Boucles de rétroaction (ECHOS-062)
# ---------------------------------------------------------------------------


def test_recurrent_action_marks_cycle(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db, ticks=3)
    # A a déjà choisi SeekFood aux ticks 1 et 2 → boucle de rétroaction au tick 3.
    chain = build_chain(db, "run-7", "A", tick=3)

    assert chain["cycle"] is True
    assert chain["cycles"] == [{"layer": "Action", "label": "SeekFood", "ticks": [1, 2]}]


def test_first_decision_has_no_cycle(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db, ticks=3, action="Explore")

    chain = build_chain(db, "run-7", "A", tick=1)

    assert chain["cycle"] is False
    assert chain["cycles"] == []
    assert chain["chain"][0]["label"] == "Explore"


# ---------------------------------------------------------------------------
# Cache & invalidation (ECHOS-063)
# ---------------------------------------------------------------------------


def test_causal_cache_serves_and_invalidates_on_ingest_version(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db, ticks=1)
    cache = CausalCache()
    calls = []

    def loader():
        calls.append(1)
        return build_chain(db, "run-7", "A", tick=1)

    first = cache.chain(db, "run-7", "A", tick=1, depth=7, loader=loader)
    second = cache.chain(db, "run-7", "A", tick=1, depth=7, loader=loader)
    assert first is second
    assert len(calls) == 1

    db.append_event("run-7", 9, "message_sent", agent_id="A")
    third = cache.chain(db, "run-7", "A", tick=1, depth=7, loader=loader)
    assert third is not first
    assert len(calls) == 2


def test_causal_cache_is_lru_bounded(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db, ticks=3)
    cache = CausalCache(capacity=2)

    cache.chain(db, "run-7", "A", 1, 7, loader=lambda: {"tick": 1})
    cache.chain(db, "run-7", "A", 2, 7, loader=lambda: {"tick": 2})
    cache.chain(db, "run-7", "A", 3, 7, loader=lambda: {"tick": 3})

    assert len(cache) == 2


# ---------------------------------------------------------------------------
# Endpoint REST /causal-chains (ECHOS-061 → ECHOS-063)
# ---------------------------------------------------------------------------


def test_endpoint_causal_chains_returns_chain(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db, ticks=3)

    body = _client(db).get("/api/runs/run-7/causal-chains/A").json()

    assert body["run_id"] == "run-7"
    assert body["agent_id"] == "A"
    assert body["tick"] == 3
    assert [node["layer"] for node in body["chain"]] == list(LAYERS)
    assert body["cycle"] is True  # action reprise aux ticks 1 et 2


def test_endpoint_causal_chains_depth_is_honored(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db, ticks=1)

    body = _client(db).get("/api/runs/run-7/causal-chains/A", params={"depth": 4}).json()

    assert body["depth_requested"] == 4
    assert body["depth_served"] == 4
    assert body["truncated"] is True


def test_endpoint_causal_chains_validation_errors(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populated(db, ticks=1)
    client = _client(db)

    assert (
        client.get("/api/runs/run-7/causal-chains/A", params={"depth": 0}).status_code
        == 422
    )
    assert (
        client.get(
            "/api/runs/run-7/causal-chains/A", params={"depth": MAX_DEPTH + 1}
        ).status_code
        == 422
    )
    assert client.get("/api/runs/run-7/causal-chains/Ghost").status_code == 404
    assert client.get("/api/runs/run-999/causal-chains/A").status_code == 404
    assert client.get("/api/runs/run-7/causal-chains/A", params={"tick": 42}).status_code == 404


def test_endpoint_causal_chains_requires_store():
    assert _client(None).get("/api/runs/run-7/causal-chains/A").status_code == 503


def test_endpoint_causal_chains_is_deterministic(tmp_path):
    left = AnalyticsStore(tmp_path / "a.db")
    _populated(left, ticks=3)
    right = AnalyticsStore(tmp_path / "b.db")
    _populated(right, ticks=3)

    body_a = _client(left).get("/api/runs/run-7/causal-chains/A").json()
    body_b = _client(right).get("/api/runs/run-7/causal-chains/A").json()

    assert body_a == body_b
    assert json.dumps(body_a, sort_keys=True) == json.dumps(body_b, sort_keys=True)
