"""Contrat d'ingestion ECHOS : zéro mesuré / non mesuré / absent / trou (BL-24).

L'audit S-29 conclut que le mécanisme existe déjà (``gaps_detected``, drapeau
``measured`` par métrique **et** par tick, provenance alignée point par point)
et qu'il ne reste à produire que les **tests contractuels bout en bout**. Ce
module les fixe sur les deux voies réelles d'alimentation de la base
analytique, en réutilisant exactement le point d'entrée de production
``echos.storage.pipeline.consume`` :

- **streaming** : un vrai serveur WebSocket in-process + ``WsClient`` (flux
  live :5180) ;
- **batch** : un ``data/stream.jsonl`` rejoué par ``StreamFileClient`` puis par
  le point d'entrée public ``ingest_run_stream`` (J2B).

Les quatre états, tels qu'un consommateur doit pouvoir les distinguer :

1. **zéro mesuré** — une observation réelle de valeur ``0`` : ligne écrite,
   ``measured`` **vrai**, valeur ``0.0`` ;
2. **non mesuré** — la donnée nécessaire à la mesure est absente : ligne
   écrite, ``measured`` **faux**, valeur de repli ``0.0`` (jamais un zéro
   observé) ;
3. **absent** — la métrique n'a **aucune ligne** à un tick pourtant ingéré
   (analyse planifiée 1 tick sur N) : l'axe ``ticks`` ne contient pas ce tick,
   et la cadence est **déclarée** (``conservation.base.analysisEvery``) ;
4. **trou** — le tick lui-même n'a jamais été ingéré : compté par
   ``gaps_detected`` et publié par ``missing_ticks`` / ``completeness``.

Ces tests ne dépendent ni de .NET ni du mock SYNE : ils vérifient le contrat
d'ingestion, pas un émetteur particulier. Les tirages contre le vrai moteur
restent portés par ``test_syne_echos_integration.py`` (``LIVEX_SYNE_E2E=1``) et
``test_syne_mock_integration.py`` (``LIVEX_MOCK_E2E=1``).
"""

from __future__ import annotations

import json
import sqlite3
import threading
import time
from pathlib import Path

import pytest
from fastapi.testclient import TestClient
from websockets.sync.server import serve

from echos import storage
from echos.api.app import create_app
from echos.ingestion import WsClient
from echos.ingestion.batch import ingest_run_stream
from echos.ingestion.file_client import StreamFileClient
from echos.storage.sqlite import AnalyticsStore

FIXTURES = Path(__file__).resolve().parent / "fixtures"

RUN_ID = "run-7"

# Moteur et couple de métriques choisissant, au même tick et sur les mêmes
# données, « zéro observé » et « non mesuré » côte à côte :
# ``BeliefDiversity`` (entropie brute, sans condition de mesure) vaut 0 sur des
# croyances vides **parce que la fenêtre est réelle mais sans fait** ;
# ``BeliefDiversityNorm`` déclare ``REQUIRES`` une mesure sur faits de croyance,
# absents du tirage, donc elle est rendue en repli neutre ``measured=False``.
ENGINE = "CognitiveDiversityMetrics"
ZERO_MEASURED = "BeliefDiversity"
NOT_MEASURED = "BeliefDiversityNorm"


def _variant(name: str, tick: int) -> str:
    data = json.loads((FIXTURES / name).read_text(encoding="utf-8"))
    data["tick"] = tick
    return json.dumps(data)


def _frames(ticks: list[int]) -> list[str]:
    """Mêmes trames pour les deux voies : snapshot + résumé + décision par tick."""
    payloads: list[str] = []
    for tick in ticks:
        payloads.append(_variant("world_snapshot_v01.json", tick))
        payloads.append(_variant("tick_summary_v01.json", tick))
        payloads.append(_variant("decision_made_v01.json", tick))
    return payloads


def _in_process_server(frames: list[str]) -> int:
    slot: list = []

    def _serve() -> None:
        def handler(connection) -> None:
            for payload in frames:
                connection.send(payload)

        with serve(handler, "127.0.0.1", 0) as server:
            slot.append(server)
            server.serve_forever()

    threading.Thread(target=_serve, daemon=True).start()
    for _ in range(100):
        if slot:
            break
        time.sleep(0.02)
    assert slot, "serveur in-process non démarré"
    return int(slot[0].socket.getsockname()[1])


def _stream_run(tmp_path: Path, ticks: list[int], *, analysis_every: int = 1):
    """Voie **streaming** : vrai serveur WebSocket + ``WsClient`` + ``consume``."""
    port = _in_process_server(_frames(ticks))
    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")
    db_path = tmp_path / "stream.db"
    db_path.parent.mkdir(parents=True, exist_ok=True)
    try:
        with AnalyticsStore(db_path) as store:
            result = storage.consume(client, store, analysis_every=analysis_every)
    finally:
        client.close()
    return db_path, result


def _write_archive(tmp_path: Path, ticks: list[int]) -> Path:
    """Répertoire de run archivé : ``<run>/data/stream.jsonl`` comme SYNE l'écrit."""
    run_path = tmp_path / "archive" / RUN_ID
    (run_path / "data").mkdir(parents=True, exist_ok=True)
    (run_path / "data" / "stream.jsonl").write_text(
        "\n".join(_frames(ticks)) + "\n", encoding="utf-8"
    )
    return run_path


def _batch_run(tmp_path: Path, ticks: list[int], *, analysis_every: int = 1):
    """Voie **batch** : ``data/stream.jsonl`` rejoué par ``StreamFileClient``."""
    run_path = _write_archive(tmp_path, ticks)
    stream = run_path / "data" / "stream.jsonl"
    db_path = tmp_path / "batch.db"
    db_path.parent.mkdir(parents=True, exist_ok=True)
    with AnalyticsStore(db_path) as store:
        result = storage.consume(
            StreamFileClient(stream), store, analysis_every=analysis_every
        )
    return db_path, result


# Les deux voies d'ingestion, paramétrées : tout ce qui suit est vérifié sur
# chacune, jamais sur une seule.
PATHS = {"streaming": _stream_run, "batch": _batch_run}


def _client(db_path: Path) -> TestClient:
    return TestClient(create_app(AnalyticsStore(db_path)))


def _series(db_path: Path, engine: str, metric: str) -> list[tuple[int, float, bool]]:
    conn = sqlite3.connect(db_path)
    try:
        return [
            (int(tick), float(value), bool(measured))
            for tick, value, measured in conn.execute(
                "SELECT tick, value, measured FROM tick_metrics "
                "WHERE run_id = ? AND engine = ? AND metric = ? ORDER BY tick",
                (RUN_ID, engine, metric),
            )
        ]
    finally:
        conn.close()


def _canonical_base(db_path: Path) -> list[tuple[str, str, int, float, bool]]:
    conn = sqlite3.connect(db_path)
    try:
        return [
            (str(engine), str(metric), int(tick), round(float(value), 9), bool(measured))
            for engine, metric, tick, value, measured in conn.execute(
                "SELECT engine, metric, tick, value, measured FROM tick_metrics "
                "WHERE run_id = ? ORDER BY engine, metric, tick",
                (RUN_ID,),
            )
        ]
    finally:
        conn.close()


# ---------------------------------------------------------------------------
# 1) Le trou est compté à l'ingestion et publié, jamais comblé.
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("path", sorted(PATHS))
def test_a_lost_tick_is_counted_and_never_filled(tmp_path, path):
    """Ticks 1, 2, 4 : le tick 3 n'a jamais existé — il est publié comme trou."""
    db_path, result = PATHS[path](tmp_path, [1, 2, 4])

    # Compté à l'écriture : une discontinuité entre ticks enregistrés.
    assert result.ticks_written == 3
    assert result.gaps_detected == 1

    body = _client(db_path).get(f"/api/runs/{RUN_ID}/metrics").json()
    # L'axe ne porte que les ticks réellement observés, dans l'ordre.
    assert body["ticks"] == [1, 2, 4]
    # Le tick perdu est publié, jamais interpolé.
    assert body["missing_ticks"] == [3]
    assert body["missing_ticks_count"] == 1

    # Le même fait, vu du monde : le résumé de tick du tick 3 n'existe pas.
    viability = _client(db_path).get(f"/api/runs/{RUN_ID}/viability").json()
    assert viability["completeness"]["ticksObserved"] == 3
    assert viability["completeness"]["missingTicks"] == [3]
    assert viability["completeness"]["missingTickCount"] == 1


# ---------------------------------------------------------------------------
# 2) « Zéro mesuré » et « non mesuré » sont deux états distincts, et la
#    provenance accompagne chaque point de chaque série.
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("path", sorted(PATHS))
def test_zero_measured_and_not_measured_are_distinct(tmp_path, path):
    db_path, _ = PATHS[path](tmp_path, [1, 2, 4])

    zero = _series(db_path, ENGINE, ZERO_MEASURED)
    unmeasured = _series(db_path, ENGINE, NOT_MEASURED)

    # Les deux lignes existent : aucune des deux n'est confondue avec une absence.
    assert [tick for tick, _, _ in zero] == [1, 2, 4]
    assert [tick for tick, _, _ in unmeasured] == [1, 2, 4]

    # Zéro **mesuré** : la valeur nulle est une observation.
    assert all(measured is True and value == 0.0 for _, value, measured in zero)
    # Non mesuré : repli neutre déclaré, porter zéro ici serait une invention.
    assert all(measured is False and value == 0.0 for _, value, measured in unmeasured)

    # Et l'API ne les aplatit pas non plus : ``true`` vs ``false`` par tick.
    body = (
        _client(db_path)
        .get(f"/api/runs/{RUN_ID}/metrics", params={"engine": ENGINE})
        .json()
    )
    assert body["measured_by_tick"][ENGINE][ZERO_MEASURED] == [True, True, True]
    assert body["measured_by_tick"][ENGINE][NOT_MEASURED] == [False, False, False]
    assert body["values"][ENGINE][ZERO_MEASURED] == [0.0, 0.0, 0.0]
    assert body["values"][ENGINE][NOT_MEASURED] == [0.0, 0.0, 0.0]


@pytest.mark.parametrize("path", sorted(PATHS))
def test_provenance_accompanies_every_metric_family(tmp_path, path):
    """Aucune famille de métriques ne voyage sans son masque de provenance."""
    db_path, _ = PATHS[path](tmp_path, [1, 2, 4])

    body = _client(db_path).get(f"/api/runs/{RUN_ID}/metrics").json()
    ticks = body["ticks"]

    assert body["values"], "aucune métrique servie"
    for engine, metrics in body["values"].items():
        # Les couples (moteur, métrique) des valeurs et de la provenance
        # coïncident exactement : une valeur sans provenance est une régression.
        assert set(metrics) == set(body["measured_by_tick"][engine])
        for metric, serie in metrics.items():
            flags = body["measured_by_tick"][engine][metric]
            assert len(serie) == len(ticks)
            assert len(flags) == len(ticks)
            # À cadence d'analyse 1, chaque tick de l'axe porte une observation :
            # ni ``None`` (absence) ni ligne manquante.
            assert all(flag is not None for flag in flags)
            assert all(value is not None for value in serie)


# ---------------------------------------------------------------------------
# 3) « Absent » (mesure non planifiée) et « trou » (donnée perdue) restent
#    distinguables : la cadence est déclarée avec le run.
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("path", sorted(PATHS))
def test_planned_absence_is_not_a_lost_tick(tmp_path, path):
    """Cadence 1 tick sur 2 : le tick 2 est ingéré, sa mesure n'est pas prévue.

    C'est la distinction que le gate de phase 5 exige : « indiquer
    explicitement les données perdues/absentes ». Les deux faits sont portés par
    deux surfaces différentes, et aucune des deux ne les confond :

    - ``/metrics.missing_ticks`` liste les ticks **sans observation de métrique**
      (ici 2 et 3 : la mesure planifiée du 2 et le tick perdu du 3) ;
    - ``/viability.completeness`` liste les ticks **réellement perdus** (le 3
      seulement) et déclare la cadence d'analyse employée.
    """
    db_path, result = PATHS[path](tmp_path, [1, 2, 4], analysis_every=2)

    # Le tick 2 a bien été ingéré : seul le calcul de métriques est planifié.
    assert result.ticks_written == 3
    assert result.gaps_detected == 1

    body = _client(db_path).get(f"/api/runs/{RUN_ID}/metrics").json()
    # L'axe porte les ticks analysés ; le tick 2 n'y est pas *inventé*.
    assert body["ticks"] == [1, 4]
    assert {tick for tick, _, _ in _series(db_path, ENGINE, ZERO_MEASURED)} == {1, 4}
    # Les ticks sans observation de métrique sont publiés : 2 (planifié) et 3 (perdu).
    assert body["missing_ticks"] == [2, 3]
    assert body["missing_ticks_count"] == 2

    viability = _client(db_path).get(f"/api/runs/{RUN_ID}/viability").json()
    completeness = viability["completeness"]
    # Le monde a bien vu le tick 2 : une seule donnée est réellement perdue.
    assert completeness["ticksObserved"] == 3
    assert completeness["missingTicks"] == [3]
    assert completeness["missingTickCount"] == 1
    # Et la cadence qui explique l'absence du tick 2 est déclarée avec le run.
    assert completeness["conservation"]["base"]["analysisEvery"] == 2


# ---------------------------------------------------------------------------
# 4) Les deux voies (streaming et batch) produisent le **même** contenu de base,
#    et ``ingest_run_stream`` ne dévie pas du rejeu direct.
# ---------------------------------------------------------------------------


def test_stream_and_batch_produce_the_same_base_content(tmp_path):
    stream_db, _ = _stream_run(tmp_path / "a", [1, 2, 4])
    batch_db, _ = _batch_run(tmp_path / "b", [1, 2, 4])

    assert _canonical_base(stream_db) == _canonical_base(batch_db)
    assert _canonical_base(stream_db), "base vide : la comparaison ne prouverait rien"


def test_batch_entry_point_matches_direct_replay(tmp_path):
    """``ingest_run_stream`` (J2B) écrit la même base que le rejeu direct du flux."""
    run_path = _write_archive(tmp_path, [1, 2, 4])
    entry_db = tmp_path / "entry.db"
    with AnalyticsStore(entry_db) as store:
        summary = ingest_run_stream(store, str(run_path))

    assert summary["runId"] == RUN_ID
    assert summary["ticks"] == 3
    assert summary["gaps"] == 1  # le trou du tick 3 est rapporté par le point d'entrée

    direct_db, _ = _batch_run(tmp_path / "direct", [1, 2, 4])
    assert _canonical_base(entry_db) == _canonical_base(direct_db)


# ---------------------------------------------------------------------------
# 5) Séparation des responsabilités : l'ingestion ne pilote pas SYNE.
# ---------------------------------------------------------------------------


def test_ingestion_paths_carry_no_control_channel():
    """Aucun chemin d'ingestion ne peut émettre de commande SYNE.

    Le pilotage SYNE est un canal **séparé** et documenté (HTTP :5181, relais
    ``/api/control/*``). Le pipeline d'ingestion ne connaît que des sources de
    messages (WebSocket :5180, fichier ``stream.jsonl``) : il ne peut donc pas
    piloter le moteur en enregistrant un flux — c'est la séparation que le gate
    demande de vérifier, pas de supposer.
    """
    from echos.ingestion import file_client, ws_client
    from echos.storage import pipeline

    for module in (pipeline, file_client, ws_client):
        assert not hasattr(module, "ControlClient")
        assert not hasattr(module, "control_client")
