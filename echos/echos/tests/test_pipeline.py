"""Pipeline d'ingestion (ECHOS-011→013) : flux → SQLite + Parquet, en
bout-en-bout sur un vrai serveur WebSocket in-process."""

import json
import threading
import time
from pathlib import Path

from websockets.sync.server import serve

from echos import storage
from echos.ingestion import WsClient
from echos.instrumentation.logging import EchosLogger
from echos.storage.sqlite import AnalyticsStore

FIXTURES = Path(__file__).resolve().parent / "fixtures"


def _variant(name: str, tick: int) -> str:
    data = json.loads((FIXTURES / name).read_text())
    data["tick"] = tick
    return json.dumps(data)


def _script(ticks: int = 3) -> list:
    payloads = []
    for tick in range(1, ticks + 1):
        payloads.append(_variant("world_snapshot_v01.json", tick))
        payloads.append(_variant("tick_summary_v01.json", tick))
        payloads.append(_variant("decision_made_v01.json", tick))
    return payloads


def _in_process_server(frames: list) -> tuple[int, threading.Thread]:
    """Serveur WebSocket réel en thread ; renvoie (port, thread)."""
    slot: list = []

    def _serve() -> None:
        def handler(connection):
            for payload in frames:
                connection.send(payload)

        with serve(handler, "127.0.0.1", 0) as server:
            slot.append(server)
            server.serve_forever()

    thread = threading.Thread(target=_serve, daemon=True)
    thread.start()
    for _ in range(100):
        if slot:
            break
        time.sleep(0.02)
    assert slot, "serveur in-process non démarré"
    return slot[0].socket.getsockname()[1], thread


def test_consume_from_real_server_writes_sqlite_and_parquet(tmp_path, monkeypatch):
    port, thread = _in_process_server(_script(3))
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")

    db_path = tmp_path / "analyse.db"
    parquet_path = tmp_path / "agents.parquet"

    with AnalyticsStore(db_path) as store:
        result = storage.consume(
            client,
            store,
            parquet_path=parquet_path,
        )
        assert result.ticks_written == 3
        assert result.events_written == 6  # 2 événements par tick
        assert result.agents_written == 6  # 2 agents × 3 ticks
        assert result.metrics_written > 0  # métriques des 8 moteurs par tick
        # C2 : ``agents`` suit sa cadence (ticks 1 et 3) ; phenomena/profiling
        # restent à l'analyse (3) et ``groups`` suit ``agents`` → 2×2 + 3 + 2 = 9.
        assert (
            result.contexts_written == 9
        )  # (agents+groups) 2 ticks + phenomena 3 + profiling 3
        assert (
            result.decision_traces_written == 3
        )  # 1 décision decision_made par tick
        assert store.count_ticks("run-7") == 3
        assert len(store.events("run-7")) == 6
        assert len(store.tick_summaries("run-7")) == 3
        assert store.latest_metrics("run-7")  # dernières métriques présentes
        assert store.latest_context("run-7", "agents") is not None
        assert store.latest_context("run-7", "phenomena") is not None
        assert store.latest_context("run-7", "groups") is not None
        assert store.latest_context("run-7", "profiling") is not None
        report = store.calibration_report("run-7")
        assert report is not None
        assert report["status"] == "complete"
        assert report["ticks"] == {"count": 3, "first": 1, "last": 3}
        assert report["needs"]["energy"]["count"] == 3
        decision_traces = store.decision_traces("run-7")
        assert len(decision_traces) == 3
        assert decision_traces[0]["chosen_action"] == "SeekWater"
        assert decision_traces[0]["utility"] == 61.7933890505355
        assert [trace["tick"] for trace in decision_traces] == [1, 2, 3]

    series = list(storage.read_agent_series(parquet_path))
    assert len(series) == 6
    assert {row.tick for row in series} == {1, 2, 3}

    with storage.AnalyticsStore(db_path) as reopened:
        indexed_ticks = {row[1] for row in reopened.tick_summaries("run-7")}
    assert storage.coherence_errors(series, indexed_ticks) == []


def test_consume_analysis_cadence_schedule(tmp_path, monkeypatch):
    """analysis_every>1 planifie les moteurs/contextes mais garde l'ingestion complète."""
    port, thread = _in_process_server(_script(3))
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")

    db_path = tmp_path / "analyse-cadence.db"
    with AnalyticsStore(db_path) as store:
        result = storage.consume(client, store, analysis_every=2)
        assert result.ticks_written == 3
        assert result.events_written == 6
        assert result.decision_traces_written == 3
        # Ticks analysés 1 et 3. ``agents``+``groups`` au tick 1 (cadence 20),
        # phenomena+profiling aux ticks 1 et 3, plus ``agents`` du dernier tick
        # (3) toujours écrit → 4 + 2 + 1 = 7.
        assert result.contexts_written == 7
        assert result.metrics_written > 0
        assert store.count_ticks("run-7") == 3
        assert len(store.events("run-7")) == 6
        assert len(store.decision_traces("run-7")) == 3
        metrics = store.metrics_all("run-7")
        assert {int(row[0]) for row in metrics} == {1, 3}

    with storage.AnalyticsStore(db_path) as reopened:
        assert reopened.tick_summaries("run-7")[1][1] == 2  # résumé du tick 2 conservé


def test_consume_records_seed_carried_by_the_snapshot(tmp_path):
    """A1 : le seed transporté (contrat V0.2.1) gagne sur la dérivation ``_seed_of``."""
    frames = []
    for tick in range(1, 3):
        snapshot = json.loads((FIXTURES / "world_snapshot_v01.json").read_text())
        snapshot["tick"] = tick
        snapshot["runId"] = "run-12345-0a1b2c3d4e5f"
        snapshot["seed"] = 12345
        frames.append(json.dumps(snapshot))
        frames.append(_variant("tick_summary_v01.json", tick))
        frames.append(_variant("decision_made_v01.json", tick))
    port, thread = _in_process_server(frames)
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")

    with AnalyticsStore(tmp_path / "seed.db") as store:
        storage.consume(client, store)
        runs = {run["run_id"]: run for run in store.runs()}
        assert runs["run-12345-0a1b2c3d4e5f"]["seed"] == "12345"


def test_consume_falls_back_to_seed_of_new_run_id_format(tmp_path):
    """A1 : flux V0.8 (format ``run-<seed>-<12hex>`` sans champ seed) → seed dérivé."""
    frames = []
    for tick in range(1, 3):
        snapshot = json.loads((FIXTURES / "world_snapshot_v01.json").read_text())
        snapshot["tick"] = tick
        snapshot["runId"] = "run-999-123456789abc"
        frames.append(json.dumps(snapshot))
        frames.append(_variant("tick_summary_v01.json", tick))
        frames.append(_variant("decision_made_v01.json", tick))
    port, thread = _in_process_server(frames)
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")

    with AnalyticsStore(tmp_path / "fallback.db") as store:
        storage.consume(client, store)
        runs = {run["run_id"]: run for run in store.runs()}
        assert runs["run-999-123456789abc"]["seed"] == "999"


def test_consume_survives_a_missing_seed_with_a_warning(tmp_path, caplog):
    """A1 : ni snapshot ni run_id porteurs → seed vide, jamais une erreur fatale."""
    frames = []
    for tick in range(1, 3):
        snapshot = json.loads((FIXTURES / "world_snapshot_v01.json").read_text())
        snapshot["tick"] = tick
        snapshot["runId"] = "not-a-run-format"
        frames.append(json.dumps(snapshot))
        frames.append(_variant("tick_summary_v01.json", tick))
        frames.append(_variant("decision_made_v01.json", tick))
    port, thread = _in_process_server(frames)
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")

    with AnalyticsStore(tmp_path / "noseed.db") as store:
        logger = EchosLogger(tmp_path / "logs")
        try:
            storage.consume(client, store, logger=logger)
        finally:
            logger.close()
        runs = {run["run_id"]: run for run in store.runs()}
        assert runs["not-a-run-format"]["seed"] == ""


def test_consume_two_successive_runs_in_one_connection(tmp_path):
    """A2 : reset SYNE dans la même connexion → zéro FK error, deux rapports.

    Régression : ``consume`` n'enregistrait le run qu'au premier snapshot du
    flux (``run_known`` jamais réinitialisé). Un reset change le ``run_id`` en
    pleine connexion : les ticks du nouveau run levaient ``FOREIGN KEY
    constraint failed`` (perte des ticks 1..n jusqu'à la reconnexion) et le
    rapport de calibration des runs interrompus n'était jamais écrit.
    """
    frames = []
    for run_id in ("run-7", "run-8"):
        for tick in range(1, 3):
            snapshot = json.loads((FIXTURES / "world_snapshot_v01.json").read_text())
            snapshot["tick"] = tick
            snapshot["runId"] = run_id
            frames.append(json.dumps(snapshot))
            frames.append(_variant("tick_summary_v01.json", tick))
            frames.append(_variant("decision_made_v01.json", tick))
    port, thread = _in_process_server(frames)
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")

    db_path = tmp_path / "reset.db"
    with AnalyticsStore(db_path) as store:
        result = storage.consume(client, store)
        assert result.ticks_written == 4
        # Chaque run redémarre au tick 1 : vu du pipeline, c'est un trou (le
        # run N redémarre au lieu de continuer run N-1) — signalé, pas masqué.
        assert result.gaps_detected >= 1
        for run_id in ("run-7", "run-8"):
            assert store.count_ticks(run_id) == 2  # 0 tick perdu
            report = store.calibration_report(run_id)
            assert report is not None
            assert report["ticks"] == {"count": 2, "first": 1, "last": 2}
            assert report["outcome"] in {"extinct", "surviving"}


def test_rolling_contexts_are_reset_between_runs(tmp_path):
    """A2 : les fenêtres glissantes ne contaminent pas le run suivant.

    Sans réinitialisation au changement de ``run_id``, l'historique du run 1
    entre dans les métriques fenêtrées du run 2 : les décisions des ticks 1–2
    de deux runs successifs se lisaient comme une boucle de rétroaction d'un
    seul monde. Ici, chaque run redémarre au tick 1 avec la même décision —
    les fenêtres réinitialisées produisent exactement les mêmes métriques
    fenêtrées que le premier run.
    """
    frames = []
    for run_id in ("run-a", "run-b"):
        for tick in range(1, 3):
            snapshot = json.loads((FIXTURES / "world_snapshot_v01.json").read_text())
            snapshot["tick"] = tick
            snapshot["runId"] = run_id
            frames.append(json.dumps(snapshot))
            frames.append(_variant("decision_made_v01.json", tick))
    port, thread = _in_process_server(frames)
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")

    with AnalyticsStore(tmp_path / "windows.db") as store:
        storage.consume(client, store)

        def loop_metrics(run_id: str) -> tuple[float, float]:
            values = {
                (str(row[1]), str(row[2])): row[3]
                for row in store.metrics_all(run_id)
                if int(row[0]) == 2  # dernier tick du run
            }
            return (
                values[("FeedbackLoopDetector", "IdentifiedLoops")],
                values[("FeedbackLoopDetector", "SystemStability")],
            )

        # Le run b rejoue exactement le run a : ses métriques fenêtrées doivent
        # être identiques — preuve que l'historique du run a n'y entre pas.
        assert loop_metrics("run-b") == loop_metrics("run-a")


def test_consume_agents_context_follows_its_own_cadence(tmp_path, monkeypatch):
    """C2 : ``agents`` écrit 1 tick sur N ; le dernier tick est toujours écrit."""
    monkeypatch.setenv("ECHOS_CONTEXT_EVERY", "2")
    port, thread = _in_process_server(_script(4))
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")

    with AnalyticsStore(tmp_path / "cadence.db") as store:
        result = storage.consume(client, store)
        # Ticks en cadence : 1, 3 (+ tick 4, dernier du flux, hors cadence).
        agent_ticks = [row[0] for row in store.observations_for("run-7", "agents")]
        assert agent_ticks == [1, 3, 4]
        # 4 ticks analysés × (phenomena + profiling) + agents/groups (1, 3)
        # = 8 + 4, plus l'``agents`` du dernier tick (4) = 13.
        assert result.contexts_written == 13


def test_consume_requires_positive_analysis_cadence(tmp_path):
    port, thread = _in_process_server(_script(1))
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")
    with AnalyticsStore(tmp_path / "analyse.db") as store:
        try:
            storage.consume(client, store, analysis_every=0)
        except ValueError:
            pass
        else:
            raise AssertionError("analysis_every=0 doit être rejeté")


def test_consume_rejects_non_positive_cadences(tmp_path):
    """``0`` et les valeurs négatives doivent être rejetés, pas acceptés.

    Régression : ``index % 0`` levait ``ZeroDivisionError`` au premier tick, et
    une valeur négative ne divisait jamais : la cadence était silencieusement
    ignorée au lieu d'être signalée à l'appelant.
    """
    port, thread = _in_process_server(_script(1))
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")
    with AnalyticsStore(tmp_path / "cadences.db") as store:
        try:
            for kwargs in (
                {"analysis_every": 0},
                {"analysis_every": -2},
                {"parquet_flush_every": 0},
                {"parquet_flush_every": -1},
            ):
                try:
                    storage.consume(client, store, **kwargs)
                except ValueError:
                    continue
                raise AssertionError(f"{kwargs} doit être rejeté")
        finally:
            client.close()
            thread.join(timeout=5)


def test_engine_context_carries_rolling_windows(tmp_path):
    """Les moteurs fenêtrés reçoivent ``events``/``history``/``communityHistory``.

    Régression : le snapshot ne portait que les événements du tick courant, si
    bien que ``FeedbackLoopDetector`` (5 métriques), ``RecoveryTime`` et
    ``CommunityStability`` retombaient sur leur repli neutre 0.0 à chaque tick
    d'un run réel.
    """
    port, thread = _in_process_server(_script(3))
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")
    try:
        with AnalyticsStore(tmp_path / "context.db") as store:
            storage.consume(client, store)

            # Le détecteur de boucles a désormais de quoi travailler : les
            # décisions des 3 ticks sont dans la fenêtre, donc une boucle est
            # identifiée là où le repli neutre renvoyait 0.
            metrics = {
                (str(row[1]), str(row[2])): row[3]
                for row in store.metrics_all("run-7")
                if int(row[0]) == 3
            }
            assert metrics[("FeedbackLoopDetector", "IdentifiedLoops")] > 0
            assert metrics[("FeedbackLoopDetector", "SystemStability")] > 0
    finally:
        client.close()
        thread.join(timeout=5)


def test_rolling_context_exposes_the_three_engine_windows():
    """``_snapshot_for_engines`` publie bien ``events``/``history``/``communityHistory``.

    Les clés sont le contrat de transport des moteurs fenêtrés ; sans elles,
    sept métriques retombent sur 0.0 à chaque tick d'un run réel.
    """
    from echos.ingestion.models import ExternalEvent, WorldSnapshot
    from echos.storage.pipeline import TickSegment, _snapshot_for_engines, _RollingContext

    snapshot = WorldSnapshot.model_validate(
        json.loads((FIXTURES / "world_snapshot_v01.json").read_text())
    )
    events = [ExternalEvent.model_validate(
        json.loads((FIXTURES / "decision_made_v01.json").read_text())
    )]
    segment = TickSegment(tick=1, snapshot=snapshot, events=events)

    context = _RollingContext()
    window_events, history, event_window = context.push(segment)
    engine_snapshot = _snapshot_for_engines(
        segment, window_events, history, event_window
    )

    assert engine_snapshot["events"] == [
        event.model_dump(mode="json", by_alias=True, exclude_none=True)
        for event in events
    ]
    assert len(engine_snapshot["history"]) == 1
    assert set(engine_snapshot["history"][0]) == {
        "tick", "actions", "resources", "communities"
    }
    assert engine_snapshot["history"][0]["actions"] == {"1": "SeekWater"}
    assert engine_snapshot["communityHistory"] == [
        {"tick": 1, "communities": engine_snapshot["history"][0]["communities"]}
    ]

    # Les fenêtres sont bornées : sans troncature, la mémoire croîtrait avec
    # la durée du run et les moteurs liraient tout l'historique à chaque tick.
    bounded_context = _RollingContext(event_window=2, event_limit=2, history=3)
    for _ in range(10):
        window, entries, _ = bounded_context.push(segment)
    assert len(window) == 2
    assert len(entries) == 3

    # Une fenêtre de taille nulle est rejetée plutôt que silently inerte.
    from echos.storage.pipeline import _RollingContext as RollingContext

    for kwargs in ({"event_window": 0}, {"event_limit": 0}, {"history": 0}):
        try:
            RollingContext(**kwargs)
        except ValueError:
            continue
        raise AssertionError(f"{kwargs} doit être rejeté")


def test_event_window_is_bounded_by_ticks_then_by_events():
    """La fenêtre d'événements est bornée en **ticks**, puis en nombre.

    Borne en nombre seulement (1000 événements), la durée couverte dépendait de
    l'activité : sur un tick chargé elle valait ~20 ticks, sur un run calme
    1000 ticks. Deux runs contenant le même nombre d'événements donnaient donc
    des taux de formation de groupe non comparables.
    """
    from echos.ingestion.models import ExternalEvent, WorldSnapshot
    from echos.ingestion.stream import TickSegment
    from echos.storage.pipeline import _RollingContext

    snapshot = WorldSnapshot.model_validate(
        json.loads((FIXTURES / "world_snapshot_v01.json").read_text())
    )

    def segment(tick: int, events: int) -> TickSegment:
        return TickSegment(
            tick=tick,
            snapshot=snapshot,
            events=[
                ExternalEvent(type="group_formed", tick=tick, agent_id="1")
                for _ in range(events)
            ],
        )

    # 50 ticks, 1 événement par tick : la fenêtre en retire 49.
    tick_bounded = _RollingContext(event_window=10, event_limit=1000)
    for tick in range(1, 51):
        tick_bounded.push(segment(tick, 1))
    events, _, window = tick_bounded.push(segment(51, 1))
    assert len(events) == 10
    assert window["ticks"] == 10
    assert (window["from"], window["to"]) == (42, 51)

    # 5 ticks très denses : la borne mémoire tranche, pas la borne en ticks.
    count_bounded = _RollingContext(event_window=100, event_limit=7)
    for tick in range(1, 6):
        count_bounded.push(segment(tick, 10))
    events, _, window = count_bounded.push(segment(6, 10))
    assert len(events) == 7
    assert window["ticks"] == 6

    # La fenêtre déclarée est bornée par la durée du run : elle ne prétend pas
    # observer 100 ticks au tick 3.
    short_run = _RollingContext(event_window=100)
    for tick in range(1, 4):
        short_run.push(segment(tick, 0))
    assert short_run.event_window() == {"ticks": 3, "from": 1, "to": 3}


def test_group_rate_denominator_uses_the_observed_window():
    """Le dénominateur des taux est la fenêtre déclarée, pas l'étendue des events.

    Deux runs de même durée et contenant chacun un ``group_formed`` au dernier
    tick : la charge du monde diffère, le taux doit être identique. Avant la
    fenêtre déclarée, le dénominateur valait 1 tick dans un cas et 51 dans
    l'autre — soit un facteur 51 sur la même métrique.
    """
    from echos.analysis.group_dynamics import compute

    def rates(events: list[dict], window: dict) -> tuple[float, float]:
        result = compute(
            {"events": events, "eventWindow": window, "agents": []}
        )
        return result["GroupFormationRate"], result["GroupDissolutionRate"]

    calm, _ = rates(
        [{"type": "group_formed", "tick": 51}],
        {"ticks": 51, "from": 1, "to": 51},
    )
    busy, _ = rates(
        [{"type": "group_formed", "tick": tick} for tick in range(1, 52)],
        {"ticks": 51, "from": 1, "to": 51},
    )
    # Le run chargé a réellement formé 50 groupes de plus : son taux doit rester
    # plus élevé, mais selon la durée observée et non selon l'étendue des events.
    assert busy > calm
    assert round(busy - calm, 6) == round(50 * 1000 / 51, 6)

    # Sans fenêtre déclarée, le repli reste l'étendue des événements présents.
    fallback, _ = rates([{"type": "group_formed", "tick": 51}], {})
    assert fallback == 1000.0


def test_consume_persists_metric_provenance(tmp_path):
    """``tick_metrics.measured`` distingue un 0.0 mesuré d'un repli neutre."""
    from echos.storage.sqlite import AnalyticsStore as Store

    store = Store(tmp_path / "provenance.sqlite")
    store.record_run("run-p", "0.1.0", "1")
    store.append_tick_metrics(
        "run-p",
        1,
        {"FeedbackLoopDetector": {"LoopStrength": 0.0},
         "SocialComplexityMetrics": {"NetworkDensity": 0.5}},
        {"FeedbackLoopDetector": {"LoopStrength": False},
         "SocialComplexityMetrics": {"NetworkDensity": True}},
    )
    measured = store.latest_measured("run-p")
    assert measured["FeedbackLoopDetector"]["LoopStrength"] is False
    assert measured["SocialComplexityMetrics"]["NetworkDensity"] is True
    # La valeur reste lisible : la provenance s'ajoute, elle ne remplace rien.
    assert store.latest_metrics("run-p")["FeedbackLoopDetector"]["LoopStrength"] == 0.0
    store.close()
