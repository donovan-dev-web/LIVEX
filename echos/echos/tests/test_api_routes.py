"""API REST ECHOS (ECHOS-040→045, jalon ph4, preuve J5).

Contrat API_REST.md : liste de runs, métriques + séries (``?every=N`` et
cache), export reproductible (JSON/CSV), croyances/relations par entité,
groupes, phénomènes émergents et traces de décision (ECHOS-051),
déterminisme des réponses, 404/400/422/503.
Les stores sont peuplés par les vrais moteurs (``compute_all``) sur la fixture
``snapshot_analysis.json`` — aucune écriture dans le monde observé.
"""

import json
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from echos.analysis import compute_all, provenance
from echos.analysis import reproducibility
from echos.api.app import create_app
from echos.api.series import SeriesCache
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


def _populate(db: AnalyticsStore, run_id: str, ticks: int = 3) -> None:
    """Peuple un store avec les vrais moteurs (fixture riche en croyances)."""
    db.record_run(run_id, "0.1.0", seed=run_id.removeprefix("run-"))
    for tick in range(1, ticks + 1):
        snap = _snapshot(tick, run_id)
        agents = snap.get("agents") or []
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
            version="0.1.0",
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
        db.append_tick_metrics(run_id, tick, metrics, provenance(snap))
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


def test_list_runs_returns_metadata(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=3)
    body = _client(db).get("/api/runs").json()

    assert len(body["runs"]) == 1
    run = body["runs"][0]
    assert run["run_id"] == "run-7"
    assert run["version"] == "0.1.0"
    assert run["seed"] == "7"
    assert run["ticks_count"] == 3
    assert run["first_tick"] == 1
    assert run["last_tick"] == 3
    # A3 : le résultat de population voyage avec les métadonnées.
    assert run["outcome"] == "surviving"
    assert run["extinction_tick"] is None
    # P1 : le niveau de conservation du run est publiée avec lui (absente des
    # bases écrites avant son introduction → ``null``, jamais inventée).
    assert run["conservation"] is None


def test_run_metadata_publishes_the_conservation_level(tmp_path):
    """P1 : fidélité de conservation lue dans le run, pas déduite par l'UI."""
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-c", ticks=1)
    db.append_tick_context(
        "run-c", 0, "conservation",
        {
            "level": "high_fidelity",
            "base": {"tickMetrics": True},
            "sampledDetails": {"agentContextEvery": 1, "agentContext": "every_tick"},
            "highFidelityReplay": True,
        },
    )

    run = _client(db).get("/api/runs").json()["runs"][0]

    assert run["conservation"]["level"] == "high_fidelity"
    assert run["conservation"]["highFidelityReplay"] is True


def test_run_metadata_expose_extinction(tmp_path):
    """A3 : un run dont une série observe alive_count = 0 est drapé « extinct »."""
    db = AnalyticsStore(tmp_path / "api.db")
    db.record_run("run-dead", "0.1.0", seed="9")
    db.append_tick(
        TickRecord(
            run_id="run-dead", version="0.1.0", tick=1, simulated_time_minutes=1,
            alive_count=2, agent_count=2, mean_energy=40.0, mean_hunger=30.0,
            mean_thirst=20.0, mean_fatigue=5.0, decision_count=1,
        )
    )
    db.append_tick(
        TickRecord(
            run_id="run-dead", version="0.1.0", tick=2, simulated_time_minutes=2,
            alive_count=0, agent_count=0, mean_energy=0.0, mean_hunger=0.0,
            mean_thirst=0.0, mean_fatigue=0.0, decision_count=0,
        )
    )

    body = _client(db).get("/api/runs/run-dead").json()

    assert body["outcome"] == "extinct"
    assert body["extinction_tick"] == 2


def test_compare_light_mode_skips_the_fingerprint(tmp_path, monkeypatch):
    """C3 : ``light=1`` renvoie le summary sans calculer l'empreinte bit-à-bit."""
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    _populate(db, "run-77")

    called = []
    original = reproducibility.content_fingerprint
    monkeypatch.setattr(
        reproducibility,
        "content_fingerprint",
        lambda *args, **kwargs: (called.append(1), original(*args, **kwargs))[1],
    )

    light = _client(db).get(
        "/api/compare", params={"run_a": "run-7", "run_b": "run-77", "light": 1}
    ).json()

    assert called == []  # l'empreinte (lourde par conception) n'a pas été chargée
    assert light["same_seed"] is False  # seeds dérivés des ids, volontairement différents
    assert light["bit_identical"] is None
    assert light["is_reproducible"] is None
    assert "series" not in light
    assert light["format"] == "light"

    # Défaut conservé : sans ``light``, l'empreinte est calculée.
    full = _client(db).get(
        "/api/compare", params={"run_a": "run-7", "run_b": "run-77"}
    ).json()
    assert called  # la comparaison complète passe bien par l'empreinte
    assert full["bit_identical"] is True
    assert full["is_reproducible"] is False  # seeds différents (77 ≠ 7)


def test_run_full_returns_latest_metrics_and_phenomena(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    client = _client(db)

    body = client.get("/api/runs/run-7").json()
    assert body["run_id"] == "run-7"
    assert "EmergenceIndicators" in body["metrics"]
    assert body["metrics"]["EmergenceIndicators"]["EmergenceScore"] >= 0.0
    assert body["phenomena"]["detected"]  # la fixture déclenche des phénomènes
    assert body["phenomena"]["disclaimer"]


def test_run_full_unknown_run_returns_404(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    assert _client(db).get("/api/runs/ghost").status_code == 404


def test_metrics_series_contract_and_downsampling(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=5)
    client = _client(db)

    body = client.get("/api/runs/run-7/metrics").json()
    assert body["run_id"] == "run-7"
    assert body["ticks"] == [1, 2, 3, 4, 5]
    assert body["every"] == 1
    assert body["latest_tick"] == 5
    score = body["values"]["EmergenceIndicators"]["EmergenceScore"]
    assert len(score) == 5
    assert body["latest"]["EmergenceIndicators"]

    sampled = client.get("/api/runs/run-7/metrics", params={"every": 2}).json()
    assert sampled["ticks"] == [1, 3, 5]
    assert len(sampled["values"]["EmergenceIndicators"]["EmergenceScore"]) == 3

    filtered = client.get(
        "/api/runs/run-7/metrics",
        params={"engine": "EmergenceIndicators", "metric": "SystemComplexity"},
    ).json()
    assert list(filtered["values"]) == ["EmergenceIndicators"]
    assert list(filtered["values"]["EmergenceIndicators"]) == ["SystemComplexity"]


def test_metrics_invalid_every_is_rejected(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    response = _client(db).get("/api/runs/run-7/metrics", params={"every": 0})
    assert response.status_code == 422


def test_export_json_is_deterministic_and_sorted(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    client = _client(db)

    first = client.get("/api/runs/run-7/export").json()
    second = client.get("/api/runs/run-7/export").json()
    assert first == second  # reproductible : aucune dépendance temporelle
    assert first["run_id"] == "run-7"
    assert first["format"] == "json"
    rows = first["rows"]
    assert rows == sorted(
        rows, key=lambda row: (row["tick"], row["engine"], row["metric"])
    )
    assert rows and rows[0] == second["rows"][0]


def test_export_csv_has_header_and_deterministic_rows(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    client = _client(db)

    body = client.get("/api/runs/run-7/export", params={"format": "csv"}).json()
    lines = body["body"].split("\r\n")
    assert lines[0] == "run_id,tick,engine,metric,value"
    assert lines[1].startswith("run-7,1,")
    assert lines[-1] == ""  # fin RFC 4180
    assert body["content_type"] == "text/csv"

    bogus = client.get("/api/runs/run-7/export", params={"format": "xml"}).json()
    assert bogus["detail"]  # 400 handlée → détail documenté


def test_beliefs_and_relationships_are_non_intrusive(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    client = _client(db)

    beliefs = client.get("/api/beliefs/A").json()
    assert beliefs["agent_id"] == "A"
    assert beliefs["run_id"] == "run-7"
    assert beliefs["tick"] == 3
    assert beliefs["beliefs"]  # croyances de la fixture

    relationships = client.get("/api/relationships/A").json()
    assert relationships["agent_id"] == "A"
    assert relationships["count"] >= 1
    assert relationships["trust"]

    assert client.get("/api/beliefs/ghost").status_code == 404
    assert client.get("/api/relationships/ghost").status_code == 404


def test_groups_and_phenomena_endpoints(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    client = _client(db)

    groups = client.get("/api/groups").json()
    assert groups["run_id"] == "run-7"
    assert groups["groups"]  # communautés du graphe de confiance
    assert all("members" in group and "size" in group for group in groups["groups"])

    phenomena = client.get("/api/emergent-phenomena").json()
    assert phenomena["run_id"] == "run-7"
    assert phenomena["phenomena"]
    assert phenomena["disclaimer"]


def test_decisions_endpoint_returns_deterministic_traces(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    client = _client(db)

    first = client.get("/api/runs/run-7/decisions").json()
    second = client.get("/api/runs/run-7/decisions").json()
    assert first == second  # reproductible (aucun horodatage d'émission)
    assert first["run_id"] == "run-7"
    traces = first["decisions"]
    assert [trace["tick"] for trace in traces] == [1, 2]
    assert [trace["agent_id"] for trace in traces] == ["A", "A"]
    assert traces[0]["chosen_action"] == "SeekFood"
    assert traces[0]["utility"] == 0.75
    assert traces[0]["deliberated"] is True
    assert traces[0]["interrupted"] is False
    assert traces[0]["beliefs_count"] == 2  # croyances de l'entité A (fixture)
    assert traces[0]["goals_count"] == 1
    assert traces[0]["memory_count"] == 8
    assert traces[0]["needs"]["hunger"] == 30.0
    assert traces[0]["needs"]["energy"] == 50.0

    assert client.get("/api/runs/ghost/decisions").status_code == 404


def test_default_run_is_most_recent(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-2", ticks=2)
    _populate(db, "run-1", ticks=1)
    response = _client(db).get("/api/groups").json()

    assert response["run_id"] == "run-2"


def test_default_run_tie_breaks_on_smallest_id(tmp_path):
    """À dernier tick égal, le run retenu est le plus petit identifiant.

    Régression : le tri portait sur ``(last_tick, run_id)`` via ``max``, ce qui
    retenait le **plus grand** identifiant, à l'inverse du contrat documenté.
    """
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-a", ticks=2)
    _populate(db, "run-b", ticks=2)
    _populate(db, "run-c", ticks=2)

    assert _client(db).get("/api/groups").json()["run_id"] == "run-a"


def test_default_run_prefers_the_furthest_tick(tmp_path):
    """Le tick le plus avancé l'emporte sur l'ordre des identifiants."""
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-z", ticks=1)
    _populate(db, "run-a", ticks=3)

    assert _client(db).get("/api/groups").json()["run_id"] == "run-a"


def test_metrics_series_exposes_metrics_absent_from_the_last_tick(tmp_path):
    """La découverte de séries porte sur tout le run, pas sur le dernier tick.

    Régression : les couples (moteur, métrique) étaient lus dans
    ``latest_metrics``. Une métrique présente à un ancien tick et disparue
    depuis (aucun événement à ce tick) n'était plus servie, alors que sa série
    existait en base.
    """
    db = AnalyticsStore(tmp_path / "api.db")
    db.record_run("run-7", "0.1.0", seed="7")
    db.append_tick_metrics("run-7", 1, {"E": {"Ephemeral": 5.0, "Stable": 1.0}})
    db.append_tick_metrics("run-7", 2, {"E": {"Stable": 2.0}})

    body = _client(db).get("/api/runs/run-7/metrics", params={"engine": "E"}).json()

    # « Ephemeral » n'est plus au tick 2 mais sa série reste découvrable :
    # le tableau est aligné sur ``ticks`` et le tick absent est un trou (null),
    # jamais une valeur décalée.
    assert set(body["values"]["E"]) == {"Ephemeral", "Stable"}
    assert body["ticks"] == [1, 2]
    assert body["values"]["E"]["Ephemeral"] == [5.0, None]
    assert body["values"]["E"]["Stable"] == [1.0, 2.0]
    # Masque de provenance aligné sur la même longueur que chaque série.
    assert body["measured_by_tick"]["E"]["Ephemeral"] == [True, None]
    assert body["measured_by_tick"]["E"]["Stable"] == [True, True]
    # « latest » reste le dernier instant.
    assert body["latest"]["E"] == {"Stable": 2.0}


def test_metrics_publish_gaps_and_per_tick_provenance(tmp_path):
    """P0 : trous de ticks **et** provenance alignée sur la série.

    Un drapeau ``measured`` au dernier tick ne prouve pas que les ticks
    antérieurs étaient mesurés : la réponse publie désormais le masque par
    tick (même longueur que chaque série) et la liste des ticks manquants,
    sans jamais combler un trou.
    """
    db = AnalyticsStore(tmp_path / "api.db")
    db.record_run("run-gaps", "0.1.0", seed="7")
    db.append_tick_metrics("run-gaps", 1, {"E": {"M": 1.0}}, {"E": {"M": True}})
    db.append_tick_metrics("run-gaps", 2, {"E": {"M": 0.0}}, {"E": {"M": False}})
    db.append_tick_metrics("run-gaps", 5, {"E": {"M": 2.0}}, {"E": {"M": True}})

    body = _client(db).get("/api/runs/run-gaps/metrics").json()

    assert body["ticks"] == [1, 2, 5]
    assert body["values"]["E"]["M"] == [1.0, 0.0, 2.0]
    # Le tick 2 est un repli neutre **observé comme tel**, pas un zéro mesuré.
    assert body["measured_by_tick"]["E"]["M"] == [True, False, True]
    assert body["missing_ticks"] == [3, 4]
    assert body["missing_ticks_count"] == 2
    assert body["latest_tick"] == 5
    # Chaque tableau de provenance a la même longueur que l'axe des ticks.
    assert len(body["measured_by_tick"]["E"]["M"]) == len(body["ticks"])


def test_metrics_keep_real_tick_coordinates_under_downsampling(tmp_path):
    """``every`` conserve les numéros de ticks réels (coordonnées x)."""
    db = AnalyticsStore(tmp_path / "api.db")
    db.record_run("run-steps", "0.1.0", seed="7")
    for tick in (1, 2, 4, 8):
        db.append_tick_metrics("run-steps", tick, {"E": {"M": float(tick)}})

    body = _client(db).get(
        "/api/runs/run-steps/metrics", params={"every": 2}
    ).json()

    # Sous-échantillonnage index-based : les ticks servis restent les ticks réels.
    assert body["ticks"] == [1, 4]
    assert body["values"]["E"]["M"] == [1.0, 4.0]
    assert body["missing_ticks"] == [3, 5, 6, 7]


def test_unknown_run_resolves_to_404_everywhere(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    client = _client(db)

    assert client.get("/api/runs/ghost/metrics").status_code == 404
    assert client.get("/api/runs/ghost/export").status_code == 404
    assert client.get("/api/beliefs/A", params={"run_id": "ghost"}).status_code == 404
    assert client.get("/api/groups", params={"run_id": "ghost"}).status_code == 404


def test_empty_store_default_run_returns_404(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    client = _client(db)

    assert client.get("/api/groups").status_code == 404
    assert client.get("/api/emergent-phenomena").status_code == 404


def test_data_routes_return_503_without_store(monkeypatch):
    monkeypatch.delenv("ECHOS_ANALYTICS_DB", raising=False)
    client = _client(None)

    assert client.get("/api/runs").status_code == 503
    assert client.get("/api/runs/run-7/metrics").status_code == 503
    assert client.get("/health").status_code == 200  # santé toujours disponible


def test_series_cache_reuses_until_invalidation(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    cache = SeriesCache()

    calls = []

    def loader() -> list:
        calls.append(1)
        return db.metric_series("run-7", "E", "M")

    first = cache.series(db, "run-7", "E", "M", loader=loader)
    second = cache.series(db, "run-7", "E", "M", loader=loader)
    assert first is second
    assert len(calls) == 1  # servie en cache tant que la version ne bouge pas

    db.append_tick_context("run-7", 9, "agents", [])
    cache.series(db, "run-7", "E", "M", loader=loader)
    assert len(calls) == 2  # nouvelle écriture → invalidation → recalcul


def test_series_cache_is_bounded_lru():
    from types import SimpleNamespace

    store = SimpleNamespace(ingest_version=0)
    cache = SeriesCache(capacity=2)
    calls = []

    for key in ("a", "b"):
        cache.series(store, "run", key, "m", loader=lambda key=key: calls.append(key))
    cache.series(store, "run", "c", "m", loader=lambda: calls.append("c"))

    assert len(cache) == 2


def test_series_cache_rejects_zero_capacity():
    with pytest.raises(ValueError):
        SeriesCache(capacity=0)


def test_create_app_reads_analytics_db_env(monkeypatch, tmp_path):
    db_path = tmp_path / "env.db"
    db = AnalyticsStore(db_path)
    _populate(db, "run-7", ticks=1)
    db.close()

    monkeypatch.setenv("ECHOS_ANALYTICS_DB", str(db_path))
    body = _client(None).get("/api/runs").json()
    assert body["runs"][0]["run_id"] == "run-7"


def test_run_calibration_returns_post_run_evidence(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    report = {
        "schemaVersion": 1,
        "runId": "run-7",
        "status": "complete",
        "ticks": {"count": 2, "first": 1, "last": 2},
    }
    db.save_calibration_report("run-7", report)

    response = _client(db).get("/api/runs/run-7/calibration")

    assert response.status_code == 200
    assert response.json() == report


def test_run_calibration_is_missing_until_a_run_finishes(tmp_path):
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=1)

    response = _client(db).get("/api/runs/run-7/calibration")

    assert response.status_code == 404


def test_control_start_relays_seed_and_returns_syne_response(monkeypatch):
    calls = []

    class FakeControlClient:
        def __init__(self, base_url):
            assert base_url == "http://127.0.0.1:5181"

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return None

        def start(self, **options):
            calls.append(("start", options))
            return {"ok": True, "runId": "run-12345"}

    monkeypatch.setattr("echos.api.routes.ControlClient", FakeControlClient)
    response = _client().post("/api/control/start", json={"seed": 12345})

    assert response.status_code == 200
    assert response.json() == {"ok": True, "runId": "run-12345"}
    assert calls == [("start", {"seed": 12345, "config": None, "max_ticks": None})]


def test_control_status_relays_syne_state(monkeypatch):
    class FakeControlClient:
        def __init__(self, base_url):
            pass

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return None

        def status(self):
            return {"state": "running", "tick": 12}

    monkeypatch.setattr("echos.api.routes.ControlClient", FakeControlClient)
    response = _client().get("/api/control/status")

    assert response.status_code == 200
    assert response.json() == {"state": "running", "tick": 12}


def test_control_unavailable_returns_actionable_service_unavailable(monkeypatch):
    from echos.ingestion import ControlError

    class OfflineControlClient:
        def __init__(self, base_url):
            pass

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return None

        def start(self, **_options):
            raise ControlError("start", "transport", "connection refused")

    monkeypatch.setattr("echos.api.routes.ControlClient", OfflineControlClient)
    response = _client().post("/api/control/start", json={})

    assert response.status_code == 503
    assert "--serve" in response.json()["detail"]


def test_metrics_expose_provenance_alongside_values(tmp_path):
    """``measured`` accompagne ``latest`` : l'UI peut distinguer les deux."""
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7")
    body = _client(db).get("/api/runs/run-7/metrics").json()

    assert set(body["measured"]) == set(body["latest"])
    # La fixture porte toutes les fenêtres du pipeline : tout est mesuré.
    assert all(
        measured
        for engine_flags in body["measured"].values()
        for measured in engine_flags.values()
    )
    # Les drapeaux sont des booléens, pas des 0/1 numériques.
    assert all(
        isinstance(measured, bool)
        for engine_flags in body["measured"].values()
        for measured in engine_flags.values()
    )

    detail = _client(db).get("/api/runs/run-7").json()
    assert detail["measured"] == body["measured"]


def test_metrics_report_a_neutral_fallback_as_unmeasured(tmp_path):
    """Un run sans fenêtres publie 0.0 **et** ``measured: false``."""
    db = AnalyticsStore(tmp_path / "api.db")
    db.record_run("run-bare", "0.1.0", seed="bare")
    snap = _snapshot(1, "run-bare")
    for key in ("history", "communityHistory", "events", "eventWindow"):
        snap.pop(key, None)
    record = TickRecord(
        run_id="run-bare", version="0.1.0", tick=1, simulated_time_minutes=1,
        alive_count=1, agent_count=1, mean_energy=1.0, mean_hunger=0.0,
        mean_thirst=0.0, mean_fatigue=0.0, decision_count=0,
    )
    db.append_tick(record)
    metrics = compute_all(snap)
    db.append_tick_metrics("run-bare", 1, metrics, provenance(snap))

    body = _client(db).get("/api/runs/run-bare/metrics").json()
    assert body["latest"]["FeedbackLoopDetector"]["RepeatedActionShare"] == 0.0
    assert body["measured"]["FeedbackLoopDetector"]["RepeatedActionShare"] is False
    assert body["measured"]["EmergenceIndicators"]["EmergenceScore"] is False
    # Une métrique instantanée reste mesurée malgré l'absence de fenêtres.
    assert body["measured"]["CognitiveDiversityMetrics"]["BeliefDiversity"] is True


def test_world_endpoint_publishes_description_and_observation(tmp_path):
    """Vue 2D : description publiée au démarrage + observation au tick choisi.

    Lecture seule (ADR-003) : chaque champ vient d'un contexte persisté à
    l'ingestion ; l'endpoint n'agrège et n'estime rien.
    """
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=3)
    db.append_tick_context(
        "run-7",
        0,
        "world",
        {
            "width": 500,
            "height": 500,
            "cellSize": 10.0,
            "obstacles": [{"id": "o-1", "x": 12.0, "y": 24.0, "radius": 4.0}],
            "resources": [
                {"id": "r-1", "kind": "food", "x": 3, "y": 4, "quantity": 10.0}
            ],
        },
    )
    db.append_tick_context(
        "run-7",
        2,
        "resources",
        [{"type": "food", "position": {"x": 5.0, "y": 6.0}, "quantity": 8.0}],
    )
    client = _client(db)

    body = client.get("/api/world").json()
    assert body["run_id"] == "run-7"
    assert body["tick"] == 3  # dernier tick observé par défaut
    assert body["world_tick"] == 0
    assert body["world"]["obstacles"][0]["id"] == "o-1"
    assert body["agents"]
    assert body["groups"]

    at_two = client.get("/api/world", params={"tick": 2}).json()
    assert at_two["tick"] == 2
    # Couche évolutive : les réserves du tick sont celles publiées à l'ingestion.
    assert at_two["resources"][0]["position"]["x"] == 5.0

    # Repli honnête : sans observation, le vide est publié, jamais inventé.
    db.record_run("run-empty", "0.1.0", seed="empty")
    empty = client.get("/api/world", params={"run_id": "run-empty"}).json()
    assert empty["tick"] == -1
    assert empty["world"] is None
    assert empty["agents"] == []

    assert client.get("/api/world", params={"run_id": "ghost"}).status_code == 404


def test_trust_graph_publishes_nodes_and_edges_as_observed(tmp_path):
    """Graphe de confiance : nœuds et arêtes publiés tels qu'observés.

    ``weight`` est le ``trust`` déclaré par l'entité (0–1), sans agrégation ni
    moyenne : seule la forme du graphe est assemblée (ADR-003).
    """
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    client = _client(db)

    body = client.get("/api/trust-graph").json()
    assert body["run_id"] == "run-7"
    assert body["tick"] == 2
    assert body["nodes"]
    assert {"id", "x", "y", "energy", "group"} <= set(body["nodes"][0])
    assert body["edges"]
    identifiers = {node["id"] for node in body["nodes"]}
    assert all(edge["source"] in identifiers for edge in body["edges"])
    assert all(0.0 <= edge["weight"] <= 1.0 for edge in body["edges"])
    # Reproductible : deux lectures, la même réponse.
    assert body == client.get("/api/trust-graph").json()

    at_one = client.get("/api/trust-graph", params={"tick": 1}).json()
    assert at_one["tick"] == 1

    db.record_run("run-empty", "0.1.0", seed="empty")
    empty = client.get("/api/trust-graph", params={"run_id": "run-empty"}).json()
    assert empty == {"run_id": "run-empty", "tick": -1, "nodes": [], "edges": []}


def _populate_events(db: AnalyticsStore, run_id: str) -> None:
    """Trois événements publiés, dont deux au même tick (ordre d'émission)."""
    db.append_event(
        run_id, 1, "decision_made",
        agent_id="A", action="SeekFood", cause="hunger=71",
    )
    db.append_event(run_id, 2, "agent_died", agent_id="B", cause="energy=0")
    db.append_event(run_id, 3, "group_formed", agent_id=None)


def test_events_endpoint_publishes_a_bounded_deterministic_journal(tmp_path):
    """P3 : journal d'événements borné, trié et reproductible.

    Les annotations de la vue temporelle lisent ce journal. Le service ne
    publie ni cause déduite ni priorisation : type, entité, action et cause
    telles qu'émises, avec ``total`` pour ne jamais faire passer la troncature
    pour l'intégralité du journal.
    """
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    _populate_events(db, "run-7")
    client = _client(db)

    body = client.get("/api/runs/run-7/events").json()

    assert body["run_id"] == "run-7"
    assert body["total"] == 3
    assert len(body["events"]) == 3 <= body["limit"]
    ticks = [event["tick"] for event in body["events"]]
    assert ticks == sorted(ticks)
    assert {"tick", "type", "agent_id", "action", "cause"} <= set(body["events"][0])
    # Types triés, avec comptage — la vue peut proposer un filtre sans deviner.
    assert [entry["type"] for entry in body["types"]] == [
        "agent_died", "decision_made", "group_formed",
    ]
    assert [entry["count"] for entry in body["types"]] == [1, 1, 1]
    # Déterminisme : deux lectures, la même réponse.
    assert body == client.get("/api/runs/run-7/events").json()


def test_events_endpoint_filters_by_type_and_bounds_the_limit(tmp_path):
    """Filtre de type, plafond de lecture et erreurs — jamais de journal fantôme."""
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    _populate_events(db, "run-7")
    client = _client(db)

    filtered = client.get(
        "/api/runs/run-7/events", params={"type": "decision_made"}
    ).json()
    assert filtered["total"] == 1
    assert {event["type"] for event in filtered["events"]} == {"decision_made"}
    assert filtered["events"][0]["action"] == "SeekFood"

    truncated = client.get("/api/runs/run-7/events", params={"limit": 1}).json()
    assert len(truncated["events"]) == 1
    assert truncated["total"] > truncated["limit"]

    # Bornes : limit hors plage → 422 ; run inconnu → 404.
    assert client.get("/api/runs/run-7/events", params={"limit": 0}).status_code == 422
    assert client.get("/api/runs/run-nope/events").status_code == 404


def test_entity_views_recede_when_the_last_context_is_empty(tmp_path):
    """Le dernier contexte d'un run éteint est vide : on recule d'une cadence.

    Le pipeline écrit toujours le contexte du dernier tick du flux — extinction
    comprise (population nulle). Sans repli, ``/trust-graph``, ``/groups``,
    ``/world``, ``/beliefs`` et ``/relationships`` publient « aucune entité »
    alors que le run en a observé (lecture « le plus récent disponible »).
    """
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    # Extinction : contexte du dernier tick, sans entité ni communauté.
    db.append_tick_context("run-7", 9, "agents", [])
    db.append_tick_context("run-7", 9, "groups", [])
    client = _client(db)

    graph = client.get("/api/trust-graph", params={"run_id": "run-7"}).json()
    assert graph["tick"] == 2  # tick d'observation réel, pas le tick vide
    assert graph["nodes"]
    assert all(edge["source"] in {n["id"] for n in graph["nodes"]}
               for edge in graph["edges"])

    groups = client.get("/api/groups", params={"run_id": "run-7"}).json()
    assert groups["tick"] == 2
    assert groups["groups"]

    world = client.get("/api/world", params={"run_id": "run-7"}).json()
    assert world["tick"] == 2
    assert world["agents"]
    assert world["groups"]

    beliefs = client.get("/api/beliefs/A", params={"run_id": "run-7"}).json()
    assert beliefs["tick"] == 2
    assert beliefs["beliefs"]
    relations = client.get(
        "/api/relationships/A", params={"run_id": "run-7"}
    ).json()
    assert relations["tick"] == 2


def test_beliefs_recede_to_a_context_that_still_contains_the_entity(tmp_path):
    """Une entité morte avant le dernier contexte peuplé reste lisible.

    Le repli s'arrête au contexte qui **contient** l'entité : jamais de
    croyances inventées, jamais un 404 alors que le run les a publiées.
    """
    db = AnalyticsStore(tmp_path / "api.db")
    _populate(db, "run-7", ticks=2)
    # Contexte peuplé mais sans A : A est mort avant le dernier échantillon.
    survivors = [
        agent for agent in (db.latest_context("run-7", "agents") or (0, []))[1]
        if str(agent.get("id")) != "A"
    ]
    assert survivors  # le fixture observe d'autres entités que A
    db.append_tick_context("run-7", 5, "agents", survivors)
    client = _client(db)

    beliefs = client.get("/api/beliefs/A", params={"run_id": "run-7"}).json()
    assert beliefs["tick"] == 2  # dernier contexte portant A
    assert beliefs["beliefs"]
    alive = client.get("/api/beliefs/B", params={"run_id": "run-7"}).json()
    assert alive["tick"] == 5  # toujours vivante : contexte le plus récent

    # Entité jamais observée sur ce run → 404 explicite, jamais un repli.
    missing = client.get("/api/beliefs/ghost", params={"run_id": "run-7"})
    assert missing.status_code == 404
    assert "introuvable" in missing.json()["detail"]


def test_live_ingest_starts_a_background_consumer(tmp_path, monkeypatch):
    """POST /ingest/live démarre le consommateur du flux SYNE en tâche de fond.

    C'est ce chemin qui rend l'analyse **temps réel** possible : sans lui, un
    run piloté par le Launcher n'apparaît dans /api/runs qu'à la fin de la
    campagne (ingestion d'archive). Même adresse = no-op explicite.
    """
    import threading
    from types import SimpleNamespace

    from echos.api import routes as routes_module

    db = AnalyticsStore(tmp_path / "live.db")
    consumed = threading.Event()
    release = threading.Event()

    class _FakeClient:
        def connect(self, url):
            self.url = url

        def close(self):
            pass

    def _fake_consume(client, store, **kwargs):
        consumed.set()
        release.wait(5.0)
        return SimpleNamespace(ticks_written=1, events_written=0)

    monkeypatch.setattr(routes_module, "WsClient", _FakeClient)
    monkeypatch.setattr(routes_module, "consume", _fake_consume)

    client = _client(db)
    try:
        response = client.post(
            "/ingest/live", json={"wsUrl": "ws://127.0.0.1:5199/"}
        )

        assert response.status_code == 200
        assert response.json()["started"] is True
        assert consumed.wait(2.0), "le consommateur de fond doit démarrer"

        again = client.post(
            "/ingest/live", json={"wsUrl": "ws://127.0.0.1:5199/"}
        )
        assert again.status_code == 200
        assert again.json()["started"] is False
    finally:
        release.set()


def test_live_ingest_refuses_non_local_streams(tmp_path):
    """Seul un WebSocket local est accepté : la route ne consomme pas l'extérieur."""
    db = AnalyticsStore(tmp_path / "live-guard.db")
    client = _client(db)

    response = client.post(
        "/ingest/live", json={"wsUrl": "http://example.com/stream"}
    )

    assert response.status_code == 400
    assert "local" in response.json()["detail"]
