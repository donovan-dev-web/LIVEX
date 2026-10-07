"""Catalogue de métriques versionné (P1) et profil de viabilité (P1/P3).

Le catalogue est le contrat documentaire entre ECHOS et le Launcher : ECHOS
publie les définitions, unités, populations, fenêtres, statuts et renommages ;
le Launcher les restitue sans redéfinir quoi que ce soit. Les tests ci-dessous
verrouillent la **couverture** (toute métrique publiée est documentée) et la
**complétude** des fiches — une fiche incomplète redevient une promesse
supérieure à la donnée, exactement ce que l'audition critique.
"""

import json
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from echos.analysis import ENGINES
from echos.analysis.catalog import (
    CATALOG_VERSION,
    METRICS_CATALOG,
    catalog,
    lookup,
)
from echos.api.app import create_app
from echos.storage.aggregation import TickRecord
from echos.storage.sqlite import AnalyticsStore

FIXTURES = Path(__file__).resolve().parent / "fixtures"

_REQUIRED = (
    "id",
    "engine",
    "label",
    "unit",
    "domain",
    "definition",
    "calculation",
    "population",
    "window",
    "direction",
    "status",
    "states",
    "warning",
    "visual",
)

_STATUS = {"measured", "exploratory", "suspended"}

_RENAMES = {
    "NetworkCentrality": "SenderConcentration",
    "InformationDiffusionSpeed": "EmitterCoverageDelay",
    "RumorAccuracyDegradation": "TheoreticalHopDecay",
    "CooperationPotential": "GoalCategoryConcordance",
    "DecisionDiversity": "ActionDiversity",
    "IntentionStability": "AverageGoalAge",
    "IdentifiedLoops": "RepeatedActionPairs",
    "LoopStrength": "RepeatedActionShare",
    "CriticalLoops": "AmplifiedRepetitions",
    "SystemStability": "ActionDistributionBalance",
    "LoopTypes": "RepeatedActionCounts",
    "ActiveGroups": "InferredCommunities",
    "AverageGroupSize": "AverageCommunitySize",
    "GroupObjectiveSuccessRate": "DissolvedGroupSuccessShare",
    "MemberTurnoverRate": "MemberExitsPerDissolution",
    "ResourceToConsumptionRatio": "ResourceFillRatio",
    "CriticalityPoints": "CriticalResourceCount",
    "AverageCentrality": "AverageOutDegree",
    "CommunityStability": "CommunitySizeMatch",
}


def test_every_published_metric_is_catalogued():
    """Aucune métrique publiée sans fiche : une mesure non documentée redevient
    une promesse supérieure à la donnée."""
    missing = [
        (engine.ENGINE_NAME, metric)
        for engine in ENGINES
        for metric in engine.METRICS
        if lookup(engine.ENGINE_NAME, metric) is None
    ]

    assert missing == []


def test_catalog_has_no_orphan_entry():
    """Et inversement : aucune fiche pour une métrique qui n'existe plus."""
    known = {
        (engine.ENGINE_NAME, metric)
        for engine in ENGINES
        for metric in engine.METRICS
    }
    orphans = [
        (entry["engine"], entry["id"])
        for entry in METRICS_CATALOG
        if (entry["engine"], entry["id"]) not in known
    ]

    assert orphans == []


def test_every_entry_is_complete_and_typed():
    for entry in METRICS_CATALOG:
        absent = [key for key in _REQUIRED if not entry.get(key)]
        assert not absent, f"{entry['id']} : champs vides {absent}"
        assert entry["status"] in _STATUS, entry["id"]
        assert entry["states"], entry["id"]
        # Le libellé est français et l'unité explicite (jamais « valeur »).
        assert entry["unit"] and entry["unit"] != "valeur", entry["id"]
        assert entry["domain"], entry["id"]


def test_renames_are_traceable_for_historical_series():
    """Chaque renommage de l'audition est tracé : les séries historiques ne
    deviennent pas silencieusement introuvables."""
    for old, new in _RENAMES.items():
        entries = [
            entry for entry in METRICS_CATALOG if old in entry.get("renamedFrom", [])
        ]
        assert entries, f"{old} → {new} absent du catalogue"
        assert entries[0]["id"] == new


def test_catalog_is_versioned_and_deterministic():
    first = catalog()
    second = catalog()

    assert first == second
    assert first["version"] == CATALOG_VERSION
    assert first["metricCount"] == len(first["metrics"])
    # Chaque version documente ses changements : comparer deux versions
    # incompatibles doit être possible, pas silencieux.
    assert {change["version"] for change in first["history"]} >= {"1.0.0", "2.0.0"}


def test_catalog_endpoint_publishes_the_contract(tmp_path):
    client = TestClient(create_app(None))

    body = client.get("/api/metrics/catalog").json()

    assert body["version"] == CATALOG_VERSION
    assert body["metricCount"] == len(body["metrics"])
    sender = next(entry for entry in body["metrics"] if entry["id"] == "SenderConcentration")
    assert sender["renamedFrom"] == ["NetworkCentrality"]
    assert sender["unit"] == "fraction"


# ---------------------------------------------------------------------------
# Profil de viabilité (P1/P3) : séries de résumés + rapport post-run
# ---------------------------------------------------------------------------


def _populate(
    db: AnalyticsStore, run_id: str, alive_series: tuple[int, ...] = (5, 4, 0)
) -> None:
    db.record_run(run_id, "0.1.0", seed="7")
    for tick, alive in enumerate(alive_series, start=1):
        db.append_tick(
            TickRecord(
                run_id=run_id,
                version="0.1.0",
                tick=tick,
                simulated_time_minutes=tick,
                alive_count=alive,
                agent_count=5,
                mean_energy=60.0 - 20 * tick,
                mean_hunger=10.0 + 5 * tick,
                mean_thirst=12.0,
                mean_fatigue=3.0,
                decision_count=2,
            )
        )


def test_viability_endpoint_exposes_source_series_without_synthetic_score(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-v")

    body = TestClient(create_app(db)).get("/api/runs/run-v/viability").json()

    assert body["outcome"] == "extinct"
    assert body["extinction_tick"] == 3
    assert body["population"]["initial"] == 5
    assert body["population"]["minimumAlive"] == 0
    assert body["population"]["series"] == [[1, 5], [2, 4], [3, 0]]
    # Dimensions publiées séparément — aucun « StabilityIndex » opaque.
    assert body["needs"]["energy"] == [[1, 40.0], [2, 20.0], [3, 0.0]]
    assert set(body) >= {"population", "needs", "resources", "completeness", "viability"}
    assert "stabilityIndex" not in json.dumps(body)
    assert body["completeness"]["missingTickCount"] == 0


def test_viability_publishes_a_descriptive_extinction_chronology(tmp_path):
    """P3 : chronologie des observations avant l'extinction, sans cause inventée."""
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-x")  # extinction au tick 3
    db.append_event(
        "run-x", 1, "decision_made", agent_id="a", action="Eat", cause="hunger=90"
    )
    db.append_event("run-x", 3, "agent_died", agent_id="a", cause="exhaustion")
    db.append_event("run-x", 5, "message_sent", agent_id="b")

    body = TestClient(create_app(db)).get("/api/runs/run-x/viability").json()

    chronology = body["extinctionChronology"]
    # Seules les observations **avant** l'extinction, dans l'ordre du tick.
    assert [row["tick"] for row in chronology] == [1, 3]
    assert chronology[-1]["type"] == "agent_died"
    # La cause transportée par SYNE est publiée telle quelle, sans inference.
    assert chronology[-1]["cause"] == "exhaustion"


def test_viability_without_extinction_has_no_chronology(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-s", alive_series=(5, 4, 3))
    db.append_event("run-s", 1, "message_sent", agent_id="a")

    body = TestClient(create_app(db)).get("/api/runs/run-s/viability").json()

    assert body["extinction_tick"] is None
    assert body["extinctionChronology"] == []


def test_experiments_summary_compares_runs_with_their_context(tmp_path):
    """P3 : comparaison multi-run avec graines, versions et dispersion."""
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-a")
    _populate(db, "run-b")
    # Un troisième run sans métrique : l'absence est publiée, pas comblée.
    db.record_run("run-c", "0.2.0", seed="c")
    db.append_tick_metrics(
        "run-a", 3, {"CognitiveDiversityMetrics": {"BeliefDiversity": 1.5}}
    )
    db.append_tick_metrics(
        "run-b", 3, {"CognitiveDiversityMetrics": {"BeliefDiversity": 1.7}}
    )

    client = TestClient(create_app(db))
    body = client.get(
        "/api/experiments/summary", params={"runs": "run-a,run-b,run-c"}
    ).json()

    assert body["run_ids"] == ["run-a", "run-b", "run-c"]
    assert [run["seed"] for run in body["runs"]] == ["7", "7", "c"]
    assert [run["version"] for run in body["runs"]] == ["0.1.0", "0.1.0", "0.2.0"]
    # Dispersion calculée côté ECHOS, avec dénominateur de runs.
    energy = body["metrics"]["CognitiveDiversityMetrics"]["BeliefDiversity"]
    assert energy["runsObserved"] == 2  # run-c n'a aucune métrique
    assert energy["values"]["run-c"] is None
    assert energy["min"] == 1.5
    assert energy["max"] == 1.7
    assert energy["mean"] == pytest.approx(1.6, abs=1e-9)
    # Écart observé, pas une taille d'effet.
    assert energy["spread"] == pytest.approx(0.2, abs=1e-9)
    assert "note" in body


def test_experiments_summary_requires_at_least_two_runs(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-a")

    response = TestClient(create_app(db)).get(
        "/api/experiments/summary", params={"runs": "run-a"}
    )

    assert response.status_code == 400


def test_viability_endpoint_reports_gaps_and_missing_columns(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    db.record_run("run-g", "0.1.0", seed="7")
    for tick in (1, 4):
        db.append_tick(
            TickRecord(
                run_id="run-g",
                version="0.1.0",
                tick=tick,
                simulated_time_minutes=tick,
                alive_count=3,
                agent_count=3,
                mean_energy=50.0,
                mean_hunger=10.0,
                mean_thirst=10.0,
                mean_fatigue=1.0,
                decision_count=1,
            )
        )

    body = TestClient(create_app(db)).get("/api/runs/run-g/viability").json()

    # Les trous sont publiés, jamais comblés : les coordonnées de tick restent
    # réelles et aucun état intermédiaire n'est interpolé entre 1 et 4.
    assert body["completeness"]["missingTicks"] == [2, 3]
    assert body["completeness"]["missingTickCount"] == 2
    assert body["population"]["series"] == [[1, 3], [4, 3]]
    assert body["needs"]["energy"] == [[1, 50.0], [4, 50.0]]
    # Un schéma ancien sans colonnes de ressources publie ``null`` (absent)
    # plutôt qu'un zéro inventé : le test de non-régression de colonne vit dans
    # ``test_calibration`` (``_resource_stats``).
