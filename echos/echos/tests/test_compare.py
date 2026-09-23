"""Comparaison de runs (ECHOS-070→072, jalon ph7, preuve J2/J3).

Contrat EXPERIMENT_COMPARISON.md §2 : ``/api/compare`` (méta-métriques de
reproductibilité), stabilité des métriques entre runs (ECHOS-071) et export
comparatif aligné CSV/JSON (ECHOS-072). Les stores sont peuplés par les vrais
moteurs sur la fixture riche ``snapshot_analysis.json`` ; les réponses ne
portent aucune dépendance temporelle (déterminisme ECHOS).
"""

import json
from copy import deepcopy
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from echos.analysis import compute_all
from echos.analysis import reproducibility
from echos.api.app import create_app
from echos.ingestion.models import ExternalEvent
from echos.instrumentation.decision_traces import build_decision_trace
from echos.storage.aggregation import TickRecord
from echos.storage.pipeline import _groups_of
from echos.storage.sqlite import AnalyticsStore

FIXTURES = Path(__file__).resolve().parent / "fixtures"


def _snapshot(tick: int, run_id: str) -> dict:
    data = json.loads((FIXTURES / "snapshot_analysis.json").read_text())
    data["tick"] = tick
    data["runId"] = run_id
    return data


def _populate(
    db: AnalyticsStore,
    run_id: str,
    ticks: int = 3,
    seed: str | None = None,
    version: str = "0.1.0",
    beliefs_extra: dict[str, dict] | None = None,
    trust_override: dict[tuple[str, str], float] | None = None,
) -> None:
    """Peuple un run avec les vrais moteurs (fixture riche en croyances).

    ``beliefs_extra`` : {agent_id → croyance ajoutée} pour diverger côté
    cognitif ; ``trust_override`` : {(émetteur, pair) → poids} pour diverger
    côté réseau social.
    """
    db.record_run(
        run_id, version, seed=(seed if seed is not None else run_id.removeprefix("run-"))
    )
    for tick in range(1, ticks + 1):
        snap = _snapshot(tick, run_id)
        agents = snap.get("agents") or []
        if beliefs_extra or trust_override:
            agents = deepcopy(agents)
            for agent in agents:
                agent_id = agent.get("id")
                if beliefs_extra and agent_id in beliefs_extra:
                    agent.setdefault("beliefs", []).append(beliefs_extra[agent_id])
                if trust_override:
                    for relation in agent.get("trust") or []:
                        key = (agent_id, relation.get("peerId"))
                        if key in trust_override:
                            relation["trust"] = trust_override[key]
        snap["agents"] = agents
        events = snap.get("events") or []
        decision = ExternalEvent(
            type="decision_made",
            tick=tick,
            agent_id="A",
            action="SeekFood",
            cause="hunger=30,thirst=20,fatigue=10.5",
            value={
                "intention": "SeekFood",
                "utility": 0.75,
                "deliberated": True,
            },
        )
        record = TickRecord(
            run_id=run_id,
            version=version,
            tick=tick,
            simulated_time_minutes=tick,
            alive_count=int(snap.get("aliveCount") or len(agents)),
            agent_count=len(agents),
            mean_energy=40.0,
            mean_hunger=30.0,
            mean_thirst=20.0,
            mean_fatigue=5.0,
            decision_count=sum(
                1 for event in events if event.get("type") == "decision_made"
            ),
        )
        db.append_tick(record)
        metrics = compute_all(snap)
        db.append_tick_metrics(run_id, tick, metrics)
        db.append_tick_context(run_id, tick, "agents", agents)
        db.append_tick_context(run_id, tick, "groups", _groups_of(agents))
        trace = build_decision_trace(run_id, tick, decision, snap)
        db.append_decision_trace(run_id, tick, trace)
        emergence = metrics.get("EmergenceIndicators") or {}
        db.append_tick_context(
            run_id,
            tick,
            "phenomena",
            {
                "detected": emergence.get("DetectedPhenomena", []),
                "disclaimer": emergence.get("Disclaimer", ""),
            },
        )


def _client(db: AnalyticsStore | None = None) -> TestClient:
    return TestClient(create_app(db))


def _compare(db: AnalyticsStore, run_a: str, run_b: str) -> dict:
    return _client(db).get("/api/compare", params={"run_a": run_a, "run_b": run_b}).json()


# --- ECHOS-070/071 : reproductibilité --------------------------------------


def test_identical_runs_are_reproducible_and_bit_identical(tmp_path):
    db = AnalyticsStore(tmp_path / "compare.db")
    _populate(db, "run-7")
    _populate(db, "run-77", seed="7")

    summary = _compare(db, "run-7", "run-77")

    assert summary["same_seed"] is True
    assert summary["same_version"] is True
    assert summary["bit_identical"] is True
    assert summary["is_reproducible"] is True
    assert summary["reproducibility_score"] == 1.0
    assert summary["cognitive_diff"] == 0.0
    assert summary["social_diff"] == 0.0
    assert summary["run_a"]["run_id"] == "run-7"
    assert summary["run_b"]["run_id"] == "run-77"


def test_same_content_different_seed_is_bit_identical_but_not_reproducible(tmp_path):
    db = AnalyticsStore(tmp_path / "compare.db")
    _populate(db, "run-7", seed="7")
    _populate(db, "run-8", seed="8")

    summary = _compare(db, "run-7", "run-8")

    assert summary["same_seed"] is False
    assert summary["same_version"] is True
    assert summary["bit_identical"] is True  # même contenu → bit-à-bit
    assert summary["is_reproducible"] is False
    assert summary["reproducibility_score"] == 1.0  # diff cognitive + sociale nulles


def test_divergent_beliefs_raise_cognitive_diff_and_lower_score(tmp_path):
    db = AnalyticsStore(tmp_path / "compare.db")
    _populate(db, "run-7", seed="7")
    extra = {"A": {"subject": "threat", "predicate": "near", "value": "true", "confidence": 0.5}}
    _populate(db, "run-9", seed="9", beliefs_extra=extra)

    summary = _compare(db, "run-7", "run-9")

    assert summary["bit_identical"] is False
    assert summary["cognitive_diff"] > 0.0
    assert summary["is_reproducible"] is False
    expected = 1.0 - (summary["cognitive_diff"] + summary["social_diff"]) / 2.0
    assert summary["reproducibility_score"] == pytest.approx(expected, abs=1e-6)
    assert summary["reproducibility_score"] < 1.0


def test_divergent_trust_raise_social_diff(tmp_path):
    db = AnalyticsStore(tmp_path / "compare.db")
    _populate(db, "run-7", seed="7")
    _populate(db, "run-9", seed="9", trust_override={("A", "B"): 0.05})

    summary = _compare(db, "run-7", "run-9")

    assert summary["bit_identical"] is False
    assert summary["social_diff"] > 0.0
    assert summary["cognitive_diff"] == 0.0
    assert summary["is_reproducible"] is False


def test_reproducibility_metrics_are_stable_between_runs(tmp_path):
    """ECHOS-071 : méta-métriques stables — deux runs issus du même protocole
    produisent exactement les mêmes valeurs (rejeu bit-à-bit côté ECHOS)."""
    db_a = AnalyticsStore(tmp_path / "a.db")
    db_b = AnalyticsStore(tmp_path / "b.db")
    _populate(db_a, "run-7", seed="7")
    _populate(db_b, "run-7", seed="7")

    first = _compare(db_a, "run-7", "run-7")
    second = _compare(db_b, "run-7", "run-7")

    assert first == second
    assert first["reproducibility_score"] == 1.0


def test_compare_response_is_deterministic_across_calls(tmp_path):
    db = AnalyticsStore(tmp_path / "compare.db")
    _populate(db, "run-7", seed="7")
    _populate(db, "run-9", seed="9", beliefs_extra={"A": {"subject": "x", "predicate": "y", "value": "true"}})

    first = _compare(db, "run-7", "run-9")
    second = _compare(db, "run-7", "run-9")

    assert first == second


# --- ECHOS-072 : export comparatif ------------------------------------------


def test_compare_json_export_has_aligned_series(tmp_path):
    db = AnalyticsStore(tmp_path / "compare.db")
    _populate(db, "run-7", seed="7")
    _populate(db, "run-9", seed="9", beliefs_extra={"A": {"subject": "x", "predicate": "y", "value": "true"}})

    body = _client(db).get("/api/compare", params={"run_a": "run-7", "run_b": "run-9"}).json()

    assert body["format"] == "json"
    series = body["series"]
    assert series
    assert series == sorted(series, key=lambda row: (row["tick"], row["engine"], row["metric"]))
    for row in series:
        assert row["diff"] == round(row["run_b"] - row["run_a"], 6)


def test_compare_csv_export_has_header_and_deterministic_rows(tmp_path):
    db = AnalyticsStore(tmp_path / "compare.db")
    _populate(db, "run-7", seed="7")
    _populate(db, "run-9", seed="9", beliefs_extra={"A": {"subject": "x", "predicate": "y", "value": "true"}})

    body = _client(db).get(
        "/api/compare", params={"run_a": "run-7", "run_b": "run-9", "format": "csv"}
    ).json()

    lines = body["body"].split("\r\n")
    assert lines[0] == "tick,engine,metric,run_a_value,run_b_value,diff"
    assert lines[-1] == ""
    assert len(lines) > 2
    assert body["content_type"] == "text/csv"
    assert body["summary"]["run_a"]["run_id"] == "run-7"
    assert body["summary"]["run_b"]["run_id"] == "run-9"


def test_compare_unknown_run_and_bad_format(tmp_path):
    db = AnalyticsStore(tmp_path / "compare.db")
    _populate(db, "run-7")

    client = _client(db)
    assert client.get("/api/compare", params={"run_a": "run-7", "run_b": "ghost"}).status_code == 404
    assert client.get(
        "/api/compare", params={"run_a": "run-7", "run_b": "run-8", "format": "xml"}
    ).status_code == 400
    assert client.get("/api/compare", params={"run_a": "run-7"}).status_code == 422


# --- fonctions pures --------------------------------------------------------


def test_distributions_and_l2_are_pure_and_bounded():
    agents = [
        {"id": "A", "beliefs": [{"subject": "s", "predicate": "p", "value": "true", "confidence": 0.9}]},
        {"id": "B", "beliefs": [{"subject": "s", "predicate": "p", "value": "true", "confidence": 0.5}]},
    ]
    distribution = reproducibility.belief_distribution(agents)
    assert distribution == {"s|p|true": 1.0}
    assert reproducibility.belief_distribution([]) == {}
    assert reproducibility.l2_normalized(distribution, distribution) == 0.0
    assert 0.0 <= reproducibility.l2_normalized({}, {"a|b|c": 1.0}) <= 1.0


def test_social_distribution_is_symmetric_for_reciprocal_relations():
    agents = [
        {"id": "A", "trust": [{"peerId": "B", "trust": 0.8}]},
        {"id": "B", "trust": [{"peerId": "A", "trust": 0.6}]},
    ]
    distribution = reproducibility.social_distribution(agents)
    assert distribution == {"A|B": 1.0}
    assert reproducibility.social_distribution([]) == {}
