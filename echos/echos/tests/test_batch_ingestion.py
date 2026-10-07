"""Ingestion d'un run batch archivé : ``stream.jsonl`` → base analytique (J2B).

Ces tests utilisent un flux écrit par le vrai binaire SYNE
(``--export-stream``) : ils prouvent que l'artefact produit par le moteur est
rejouable par ECHOS avec le contrat du flux live, et non qu'un jeu de données
imaginé par les tests est accepted.

Les tests du ``syne_run`` supposent le moteur réel (``dotnet build Syne.sln
--configuration Release``) : sans ``LIVEX_SYNE_E2E=1`` la fixture est sautée,
comme l'intégration WebSocket, ce qui laisse le job Python pur tourner sans
binaire .NET. Les tests qui n'ont pas besoin du moteur (lecture de flux,
``inspect``, routes de refus) tournent dans tous les cas.
"""

from __future__ import annotations

import json
import os
import shutil
import subprocess
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from echos.api.app import create_app
from echos.ingestion.batch import IngestError, ingest_run_stream
from echos.ingestion.file_client import StreamFileError, inspect, resolve_stream_file
from echos.storage.sqlite import AnalyticsStore

ROOT = Path(__file__).resolve().parents[3]
SYNE_DLL = ROOT / "syne/Simulation.Console/bin/Release/net10.0/Simulation.Console.dll"
SYNE_BINARY = ROOT / "syne/Simulation.Console/bin/Release/net10.0/Simulation.Console"
TICKS = 12


def _free_port() -> int:
    import socket

    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        return int(probe.getsockname()[1])


def _write_run(
    directory: Path,
    *,
    run_id: str = "EXP-ACCEPT-RUN-0001",
    seed: int = 42,
    ticks: int = TICKS,
    truncate_last_segment: bool = False,
) -> Path:
    """Lance un vrai run SYNE supervisé et renvoie son répertoire de données."""
    data_directory = directory / "data"
    logs_directory = directory / "logs"
    logs_directory.mkdir(parents=True, exist_ok=True)
    configuration = directory / "launcher-config.json"
    configuration.write_text(json.dumps({"agents": {"initialCount": 3}}), encoding="utf-8")

    command = [
        str(SYNE_BINARY) if SYNE_BINARY.exists() else "dotnet",
    ]
    if not SYNE_BINARY.exists():
        command.append(str(SYNE_DLL))
    command += [
        "--headless",
        "--instance-id", "syne-ingest",
        "--control-port", str(_free_port()),
        "--work-dir", str(directory),
        "--log-dir", str(logs_directory),
        "--simulation", "reference",
        "--seed", str(seed),
        "--ticks", str(ticks),
        "--config", str(configuration),
        "--export-stream",
        "--run-id", run_id,
        "--autostart",
        "--correlation-id", "echos-ingest-test",
    ]
    environment = {
        **os.environ,
        "LIVEX_SESSION_TOKEN": "ingest-token",
        "LIVEX_CORRELATION_ID": "echos-ingest-test",
    }
    completed = subprocess.run(
        command, capture_output=True, text=True, timeout=300, env=environment
    )
    assert completed.returncode == 0, f"SYNE batch failed: {completed.stderr}\n{completed.stdout}"
    assert (data_directory / "stream.jsonl").is_file()
    assert (data_directory / "result.json").is_file()

    if truncate_last_segment:
        # Scénario réel d'un SYNE tué : le flux s'arrête sur une frontière de
        # segment, `result.json` reste pourtant annonceur de 12 ticks.
        stream = data_directory / "stream.jsonl"
        lines = stream.read_text(encoding="utf-8").splitlines()
        cut = max(
            index
            for index, line in enumerate(lines)
            if json.loads(line)["type"] == "snapshot"
        )
        stream.write_text("\n".join(lines[:cut]) + "\n", encoding="utf-8")

    return data_directory


@pytest.fixture(scope="module")
def syne_run(tmp_path_factory):
    if os.getenv("LIVEX_SYNE_E2E") != "1":
        pytest.skip(
            "requires the real SYNE engine and its Release build; "
            "enabled in the U8 integration CI job"
        )
    directory = tmp_path_factory.mktemp("syne-batch")
    _write_run(directory)
    yield directory
    shutil.rmtree(directory, ignore_errors=True)


@pytest.fixture
def store(tmp_path):
    with AnalyticsStore(tmp_path / "analytics.db") as opened:
        yield opened


def test_the_archive_declares_the_run_the_launcher_asked_for(syne_run):
    described = inspect(resolve_stream_file(syne_run))

    assert described["runId"] == "EXP-ACCEPT-RUN-0001"
    assert described["segments"] == TICKS
    assert described["ticks"] == TICKS
    assert described["frames"] > TICKS  # snapshots + événements par tick


def test_ingestion_records_the_archived_run_and_its_metrics(store, syne_run):
    ingested = ingest_run_stream(store, str(syne_run))

    assert ingested["runId"] == "EXP-ACCEPT-RUN-0001"
    assert ingested["ticks"] == TICKS
    assert ingested["events"] > 0
    assert ingested["metrics"] > 0

    summaries = store.tick_summaries("EXP-ACCEPT-RUN-0001")
    assert [row[1] for row in summaries] == list(range(1, TICKS + 1))

    # Les moteurs ont bien tourné à l'ingestion : la base n'est pas une copie
    # brute du flux, elle porte les mesures ECHOS.
    assert store.latest_metrics("EXP-ACCEPT-RUN-0001")
    assert store.calibration_report("EXP-ACCEPT-RUN-0001") is not None


def test_ingestion_is_refused_twice_instead_of_duplicating_events(store, syne_run):
    first = ingest_run_stream(store, str(syne_run))
    with pytest.raises(IngestError) as duplicate:
        ingest_run_stream(store, str(syne_run))

    assert duplicate.value.status_code == 409
    assert store.count_ticks("EXP-ACCEPT-RUN-0001") == first["ticks"]
    assert len(store.events("EXP-ACCEPT-RUN-0001")) == first["events"]


def test_ingestion_refuses_a_run_directory_without_a_stream(store, tmp_path):
    empty = tmp_path / "no-stream"
    (empty / "data").mkdir(parents=True)

    with pytest.raises(IngestError) as missing:
        ingest_run_stream(store, str(empty))

    assert missing.value.status_code == 404
    assert "stream.jsonl" in str(missing.value)


def test_ingestion_refuses_a_truncated_archive_rather_than_reporting_a_partial_run(store, tmp_path):
    run_directory = tmp_path / "truncated"
    _write_run(run_directory, truncate_last_segment=True)

    with pytest.raises(IngestError) as truncated:
        ingest_run_stream(store, str(run_directory))

    assert truncated.value.status_code in (409, 422)
    assert not store.runs()


def test_ingestion_refuses_a_run_id_that_the_stream_does_not_declare(store, syne_run):
    with pytest.raises(IngestError) as mismatch:
        ingest_run_stream(store, str(syne_run), expected_run_id="EXP-OTHER-RUN-0001")

    assert mismatch.value.status_code == 409
    assert not store.runs()


def test_a_stream_broken_midway_leaves_no_partial_run_behind(store, syne_run, tmp_path):
    """Une ligne illisible **après** plusieurs segments ne doit rien laisser.

    ``inspect`` valide le fichier entier avant toute écriture, mais
    ``aligned_ticks`` — qui relit le flux pendant ``consume`` — peut encore
    refuser un désalignement. Sans purge, les segments déjà écrits resteraient en
    base : le run serait à la fois partiel *et* protégé par la garde
    anti-doublon, donc l'archive valide ne pourrait plus jamais être ingérée.
    """
    broken_directory = tmp_path / "broken-midway"
    shutil.copytree(syne_run, broken_directory)
    stream = broken_directory / "data/stream.jsonl"
    lines = stream.read_text(encoding="utf-8").splitlines()

    # Désaligne un événement du dernier tick : l'erreur ne peut survenir qu'après
    # l'écriture des segments précédents.
    last_snapshot = max(
        index for index, line in enumerate(lines) if json.loads(line)["type"] == "snapshot"
    )
    for index in range(last_snapshot + 1, len(lines)):
        payload = json.loads(lines[index])
        if payload["type"] not in ("snapshot", "tick_summary"):
            payload["tick"] = TICKS + 5
            lines[index] = json.dumps(payload)
            break
    stream.write_text("\n".join(lines) + "\n", encoding="utf-8")

    with pytest.raises(IngestError) as failure:
        ingest_run_stream(store, str(broken_directory))

    assert failure.value.status_code == 422
    assert not store.runs(), "l'ingestion échouée a laissé des lignes en base"
    assert store.count_ticks("EXP-ACCEPT-RUN-0001") == 0


def test_a_valid_archive_can_be_ingested_again_after_a_failed_attempt(store, syne_run):
    """Le refus « déjà enregistré » ne doit pas condamner l'archive.

    Séquence réaliste : une tentative échoue en laissant un run sans tick, puis le
    Launcher rejoue la même archive. Si le résidu n'était pas purgé, cette
    seconde tentative serait refusée alors que la base ne contient rien du run.
    """
    store.record_run("EXP-ACCEPT-RUN-0001", "0.3.0", "42")
    assert store.has_run("EXP-ACCEPT-RUN-0001")
    assert store.count_ticks("EXP-ACCEPT-RUN-0001") == 0

    ingested = ingest_run_stream(store, str(syne_run))

    assert ingested["ticks"] == TICKS
    assert store.count_ticks("EXP-ACCEPT-RUN-0001") == TICKS


def test_a_preexisting_run_with_data_is_never_purged(store, syne_run):
    """La purge ne doit toucher que les résidus, pas un run déjà peuplé.

    Un run du flux live en cours partage la même base : si une ingestion d'archive
    échouait et effaçait ses lignes, elle détruirait des données produites par une
    autre écriture.
    """
    ingest_run_stream(store, str(syne_run))
    before = len(store.events("EXP-ACCEPT-RUN-0001"))

    with pytest.raises(IngestError):
        ingest_run_stream(store, str(syne_run))

    assert store.count_ticks("EXP-ACCEPT-RUN-0001") == TICKS
    assert len(store.events("EXP-ACCEPT-RUN-0001")) == before


def test_inspect_refuses_a_duplicated_tick_summary(tmp_path):
    """Un ``tick_summary`` répété trahit un flux réécrit ou fusionné à tort.

    ``consume`` écrase le résumé du tick (``INSERT OR REPLACE``) : un doublon
    passerait en base sans trace et l'ingestion paraîtrait réussie alors que le
    flux ne décrit pas une boucle SYNE. Un ``set`` l'aurait laissé passer, d'où le
    comptage explicite dans ``inspect``.
    """
    doubled = tmp_path / "stream.jsonl"
    doubled.write_text(
        "".join(
            json.dumps(payload) + "\n"
            for payload in (
                {
                    "type": "snapshot", "version": "0.3.0", "runId": "EXP-DOUBLE",
                    "tick": 1, "simulatedTimeMinutes": 1, "aliveCount": 1,
                },
                {"type": "tick_summary", "tick": 1, "value": {"aliveCount": 1}},
                {"type": "tick_summary", "tick": 1, "value": {"aliveCount": 2}},
            )
        ),
        encoding="utf-8",
    )

    with pytest.raises(StreamFileError) as failure:
        inspect(doubled)

    assert "répété" in str(failure.value)


def test_inspect_rejects_a_file_without_any_snapshot(tmp_path):
    orphan = tmp_path / "stream.jsonl"
    orphan.write_text(
        json.dumps({"type": "tick_summary", "tick": 1, "value": {"aliveCount": 2}}) + "\n",
        encoding="utf-8",
    )

    with pytest.raises(StreamFileError):
        inspect(orphan)


def test_inspect_reports_an_unreadable_line_with_its_number(tmp_path):
    broken = tmp_path / "stream.jsonl"
    broken.write_text(
        '{"type":"snapshot","version":"0.3.0","runId":"r","tick":1,'
        '"simulatedTimeMinutes":1,"aliveCount":1}\n{oops\n',
        encoding="utf-8",
    )

    with pytest.raises(StreamFileError) as failure:
        inspect(broken)

    assert ":2" in str(failure.value)


def test_the_route_records_the_run_and_rejects_a_second_call(store, syne_run):
    client = TestClient(create_app(store))
    payload = {"runId": "EXP-ACCEPT-RUN-0001", "runPath": str(syne_run)}

    response = client.post("/ingest/run", json=payload)

    assert response.status_code == 200, response.text
    assert response.json()["ingested"]["ticks"] == TICKS
    assert client.get("/api/runs").json()["runs"][0]["run_id"] == "EXP-ACCEPT-RUN-0001"

    assert client.post("/ingest/run", json=payload).status_code == 409


def test_the_route_refuses_an_unknown_field_instead_of_ignoring_it(store, syne_run):
    client = TestClient(create_app(store))

    response = client.post(
        "/ingest/run",
        json={"runPath": str(syne_run), "unknown": True},
    )

    assert response.status_code == 422


def test_the_route_reports_a_missing_run_directory(store, tmp_path):
    client = TestClient(create_app(store))

    response = client.post("/ingest/run", json={"runPath": str(tmp_path / "absent")})

    assert response.status_code == 404
    assert not store.runs()


def test_the_same_archive_replayed_in_another_base_gives_the_same_analysis(tmp_path, syne_run):
    """Rejouabilité : le paquet archivé suffit à retrouver exactement l'analyse.

    Une réanalyse faite plus tard à partir du paquet doit rendre les **mêmes
    octets** que celle faite pendant la campagne, sinon le rapport archivé ne
    prouve plus rien.
    """
    from echos.analysis.headless import analyze_run

    first = AnalyticsStore(tmp_path / "first.db")
    second = AnalyticsStore(tmp_path / "second.db")
    try:
        ingest_run_stream(first, str(syne_run))
        ingest_run_stream(second, str(syne_run))

        replayed = analyze_run(first, "EXP-ACCEPT", "EXP-ACCEPT-RUN-0001", str(syne_run))
        original = analyze_run(second, "EXP-ACCEPT", "EXP-ACCEPT-RUN-0001", str(syne_run))
    finally:
        first.close()
        second.close()

    assert replayed == original
    assert any("profiling" not in name for name in (entry["name"] for entry in replayed))
