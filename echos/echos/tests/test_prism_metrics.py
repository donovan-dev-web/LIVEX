# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Moteur 6 refondu et quatre indicateurs P10 (ECHOS-121 / ECHOS-122).

Les tests couvrent deux niveaux : les fonctions de projection (pures, calcul
exact et provenance) et le rejeu complet d'un flux persisté, qui prouve que les
valeurs arrivent réellement en base interrogeable — c'est la sortie exigée par
le plan d'implémentation (« quatre indicateurs calculés sur un run réel »).
"""

from __future__ import annotations

import pytest
from fastapi.testclient import TestClient

from echos.analysis.prism_projection import (
    INDICATOR_ENGINE,
    RESOURCE_ENGINE,
    gini,
    project_indicators,
    project_resource_metrics,
)
from echos.api.app import create_app
from echos.ingestion import PrismReadEnvelope
from echos.storage.prism_pipeline import PrismAnalysisError, consume_prism
from echos.storage.sqlite import AnalyticsStore

METRICS_PAYLOAD = {
    "tick": 2,
    "stocks": [
        {"itemTypeId": 1, "agents": 4, "containers": 2, "groundItems": 0},
        {"itemTypeId": 2, "agents": 0, "containers": 0, "groundItems": 1},
    ],
    "matter": [
        {"itemTypeId": 1, "ruleId": "harvest", "created": 10, "destroyed": 2},
        {"itemTypeId": 1, "ruleId": "tool_wear", "created": 0, "destroyed": 3},
    ],
    "structures": [
        {
            "siteId": "s1",
            "templateId": "shelter",
            "progressPermille": 500,
            "integrityPermille": 800,
            "effectsActive": True,
        }
    ],
    "reservedStations": 1,
    "activeStructureEffects": 2,
    "failures": [{"reason": "support_required", "count": 3}],
}


def _envelope(sequence: int, tick: int, kind: str, payload: dict) -> PrismReadEnvelope:
    import json

    return PrismReadEnvelope.parse(
        json.dumps(
            {
                "protocolVersion": "1.0.0",
                "sequence": sequence,
                "tick": tick,
                "kind": kind,
                "payload": payload,
            }
        )
    )


def test_resource_metrics_are_projected_from_the_prism_metrics_envelope():
    values, measured = project_resource_metrics(METRICS_PAYLOAD)

    assert values["TotalStockedQuantity"] == 7.0
    assert values["AgentStockRatio"] == pytest.approx(4 / 7)
    assert values["ContainerStockRatio"] == pytest.approx(2 / 7)
    assert values["GroundStockRatio"] == pytest.approx(1 / 7)
    assert values["MatterCreated"] == 10.0
    assert values["MatterDestroyed"] == 5.0
    assert values["NetMatterBalance"] == 5.0
    assert values["WearDestroyedQuantity"] == 3.0
    assert measured["WearDestroyedQuantity"] is True
    assert values["StructureCount"] == 1.0
    assert values["ReservedStations"] == 1.0
    assert values["ActiveStructureEffects"] == 2.0
    assert values["CommandFailureCount"] == 3.0


def test_wear_is_unmeasured_when_no_wear_rule_is_accounted():
    payload = {
        **METRICS_PAYLOAD,
        "matter": [{"itemTypeId": 1, "ruleId": "harvest", "created": 4, "destroyed": 1}],
    }

    values, measured = project_resource_metrics(payload)

    # 0 sans règle d'usure n'est **pas** « aucune usure » : la mesure est
    # déclarée absente, ce que le drapeau de provenance publie.
    assert values["WearDestroyedQuantity"] == 0.0
    assert measured["WearDestroyedQuantity"] is False


def test_resource_metrics_absent_without_any_metrics_envelope():
    values, measured = project_resource_metrics(None)

    assert values == {}
    assert measured == {}


def test_gini_bounds_equality_and_concentration():
    assert gini([]) == 0.0
    assert gini([5.0]) == 0.0
    assert gini([2.0, 2.0, 2.0]) == pytest.approx(0.0)
    assert gini([0.0, 4.0]) == pytest.approx(0.5)
    assert 0.0 <= gini([1.0, 2.0, 3.0, 40.0]) < 1.0


def test_four_indicators_are_computed_from_agents_events_and_structures():
    agents = [
        {"id": "1", "inventory": [{"itemTypeId": 1, "quantity": 3}]},
        {"id": "2", "inventory": [{"itemTypeId": 2, "quantity": 1}]},
    ]
    events = [{"type": "item_transferred", "value": {"quantity": 2}}]

    values, measured = project_indicators(
        agents,
        events,
        METRICS_PAYLOAD,
        agents_published=True,
        events_published=True,
    )

    # Coopération : un transfert pour deux agents → 0.5 (saturation à un
    # transfert par agent et par tick).
    assert values["CooperationIndex"] == pytest.approx(0.5)
    assert values["TransferEvents"] == 1.0
    assert values["TransferVolume"] == 2.0
    # Spécialisation : chaque agent ne détient qu'une sorte d'objet → 1.0.
    assert values["SpecializationIndex"] == pytest.approx(1.0)
    # Infrastructures : avancement 0.5 × intégrité 0.8 = 0.4.
    assert values["InfrastructureIndex"] == pytest.approx(0.4)
    # Inégalité : Gini de [3, 1] = 0.25.
    assert values["StockInequalityIndex"] == pytest.approx(0.25)
    assert values["AgentStockSample"] == 2.0
    assert all(measured.values())


def test_unpublished_sources_are_reported_absent_not_zero_observed():
    values, measured = project_indicators(
        [],
        [],
        None,
        agents_published=False,
        events_published=False,
    )

    assert values["CooperationIndex"] == 0.0
    assert measured["CooperationIndex"] is False
    assert measured["InfrastructureIndex"] is False
    # Une fenêtre d'événements **publiée et vide** est un zéro observé.
    published, measured_published = project_indicators(
        [], [], None, agents_published=True, events_published=True
    )
    assert published["TransferEvents"] == 0.0
    assert measured_published["TransferEvents"] is True
    assert measured_published["InfrastructureIndex"] is False


def _persist_stream(store: AnalyticsStore, run_id: str, content_hash: str) -> None:
    envelopes = [
        _envelope(0, -1, "world_snapshot", {}),
        _envelope(
            1,
            0,
            "entity_snapshot",
            {
                "tick": 0,
                "agents": [
                    {"agentId": 1, "inventory": [{"itemTypeId": 1, "quantity": 2}]},
                    {"agentId": 2, "inventory": [{"itemTypeId": 1, "quantity": 4}]},
                ],
                "groundItems": [],
            },
        ),
        _envelope(
            2,
            0,
            "tick_result",
            {"events": [{"type": "item_transferred", "value": {"quantity": 1}}]},
        ),
        _envelope(3, 0, "metrics", METRICS_PAYLOAD),
    ]
    store.append_prism_read_stream(
        run_id, "7", content_hash, [envelope.to_dict() for envelope in envelopes]
    )


def test_consume_prism_replays_the_stream_into_queryable_metrics(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist_stream(store, "prism-run-analysis", "hash")

        result = consume_prism(store, "prism-run-analysis")

        assert result.ticks_analyzed == 2
        assert result.indicator_ticks == 1
        assert result.resource_ticks == 1
        assert result.metrics_written > 0

        metrics = store.latest_metrics("prism-run-analysis")
        assert metrics[RESOURCE_ENGINE]["MatterCreated"] == 10.0
        assert metrics[INDICATOR_ENGINE]["CooperationIndex"] == pytest.approx(0.5)
        assert metrics[INDICATOR_ENGINE]["StockInequalityIndex"] == pytest.approx(
            1 / 6
        )

        context = store.latest_context("prism-run-analysis", "prismIndicators")
        assert context is not None
        _, payload = context
        assert payload["sources"]["metricsTick"] == 0
        assert payload["sources"]["agentCount"] == 2

        api = TestClient(create_app(store))
        body = api.get("/api/runs/prism-run-analysis/prism-indicators").json()
        assert body["tick"] == 0
        states = {(row["engine"], row["metric"]): row["state"] for row in body["provenance"]}
        assert states[(INDICATOR_ENGINE, "CooperationIndex")] == "measured"
        assert states[(INDICATOR_ENGINE, "TransferEvents")] == "measured"


def test_consume_prism_is_idempotent(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist_stream(store, "prism-run-idempotent", "hash")

        first = consume_prism(store, "prism-run-idempotent")
        snapshot_first = store.metrics_all("prism-run-idempotent")
        second = consume_prism(store, "prism-run-idempotent")
        snapshot_second = store.metrics_all("prism-run-idempotent")

        assert first.metrics_written == second.metrics_written
        assert snapshot_first == snapshot_second


def test_consume_prism_requires_a_persisted_prism_stream(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        with pytest.raises(PrismAnalysisError, match="aucun flux"):
            consume_prism(store, "unknown-run")


def test_indicators_api_distinguishes_absent_from_observed_zero(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        # Un tick avec événements publiés mais **aucun inventaire** : les
        # indicateurs d'inventaire sont absents, ceux d'événements mesurés.
        envelopes = [
            _envelope(0, -1, "world_snapshot", {}),
            _envelope(1, 0, "tick_result", {"events": []}),
        ]
        store.append_prism_read_stream(
            "prism-run-partial", "7", "hash",
            [envelope.to_dict() for envelope in envelopes],
        )
        consume_prism(store, "prism-run-partial")

        body = TestClient(create_app(store)).get(
            "/api/runs/prism-run-partial/prism-indicators"
        ).json()
        states = {
            (row["engine"], row["metric"]): row["state"]
            for row in body["provenance"]
        }

        assert states[(INDICATOR_ENGINE, "TransferEvents")] == "observed_zero"
        assert states[(INDICATOR_ENGINE, "SpecializationIndex")] == "absent"
        assert states[(INDICATOR_ENGINE, "InfrastructureIndex")] == "absent"


def test_indicators_api_requires_a_run_that_was_analyzed(tmp_path):
    with AnalyticsStore(tmp_path / "echos.db") as store:
        _persist_stream(store, "prism-run-unanalyzed", "hash")
        api = TestClient(create_app(store))

        response = api.get("/api/runs/prism-run-unanalyzed/prism-indicators")

        assert response.status_code == 404
        assert "prism-analysis" in response.json()["detail"]
        assert api.get("/api/runs/unknown/prism-indicators").status_code == 404
        assert api.post("/api/runs/unknown/prism-analysis").status_code == 404
