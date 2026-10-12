"""Relance d'un flux SYNE archivé (JSON Lines) dans le magasin d'analyse.

Le Launcher archive, dans chaque run d'un paquet, le flux d'observabilité
exactement tel que SYNE l'a émis (``data/stream.jsonl``). Ce module permet
d'en repeindre la base analytique **après coup**, sans WebSocket ni SYNE en
cours d'exécution : c'est la condition pour qu'un rapport de campagne reste
rejouable depuis le paquet.

Le contrat de relecture est celui du flux live : les messages passent par
:func:`echos.ingestion.models.parse_message`, l'alignement par
:func:`echos.ingestion.stream.aligned_ticks` et l'écriture par
:func:`echos.storage.pipeline.consume`. Un fichier produit par SYNE et le flux
diffusé sur le port 5180 aboutissent donc au même contenu de base.

Une ligne illisible, un flux désordonné ou un ``result.json`` en désaccord avec
son flux sont refusés : un artefact interrompu ne doit jamais produire une
analyse partielle présentée comme complète.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Iterator

from echos.ingestion.models import InvalidMessageError, Message, parse_message

DEFAULT_STREAM_FILE_NAME = "stream.jsonl"
"""Nom de l'artefact de flux dans le répertoire de données d'un run."""

DATA_DIRECTORY_NAME = "data"
"""Sous-dossier du run où SYNE écrit ses artefacts (``--work-dir/data``)."""

RESULT_FILE_NAME = "result.json"
"""Résumé batch écrit par SYNE à côté du flux — contrôle d'intégrité."""


class StreamFileError(ValueError):
    """Le fichier de flux ne peut pas être relu comme un flux SYNE aligné."""

    def __init__(self, detail: str) -> None:
        super().__init__(detail)
        self.detail = detail


class StreamFileClient:
    """Source de messages SYNE lue dans un fichier JSON Lines.

    Satisfait le contrat structurel attendu par
    :func:`echos.storage.pipeline.consume` — un unique ``__iter__`` qui rend des
    :data:`echos.ingestion.models.Message` — sans réutiliser le transport
    WebSocket.
    """

    def __init__(self, path: str | Path) -> None:
        self._path = Path(path)

    @property
    def path(self) -> Path:
        return self._path

    def __iter__(self) -> Iterator[Message]:
        try:
            handle = self._path.open("r", encoding="utf-8")
        except OSError as exc:
            raise StreamFileError(
                f"flux d'observabilité illisible : {self._path} ({exc})"
            ) from exc

        with handle:
            for number, line in enumerate(handle, start=1):
                stripped = line.strip()
                if not stripped:
                    continue
                try:
                    yield parse_message(stripped)
                except InvalidMessageError as exc:
                    raise StreamFileError(f"{self._path}:{number} : {exc}") from exc


def resolve_stream_file(run_path: str | Path) -> Path:
    """Chemin du flux d'un run, avec un motif exploitable.

    ``run_path`` est le répertoire de travail reçu par SYNE (``--work-dir``) :
    ses artefacts sont dans ``data/``. Le dossier ``data`` est donc la seule
    disposition acceptée — accepter aussi le dossier lui-même créerait deux
    chemins valides vers le même run, dont un seul archiverait les journaux.
    """
    run_directory = Path(run_path).expanduser()
    candidate = run_directory / DATA_DIRECTORY_NAME / DEFAULT_STREAM_FILE_NAME
    if not candidate.is_file():
        raise StreamFileError(
            f"flux d'observabilité absent : {candidate} "
            "(le run n'a pas été exécuté avec --export-stream)"
        )
    return candidate


def snapshot_run_id(path: str | Path) -> str:
    """Identifiant de run porté par le premier snapshot du flux.

    C'est la clé sous laquelle la base analytique enregistrera le run : le
    Launcher fournit cette valeur à SYNE (``--run-id``) et la réutilise ensuite
    pour ses demandes d'analyse, sans table de correspondance.
    """
    described = inspect(path)
    return str(described["runId"])


def _declared_result(stream: Path) -> dict[str, object] | None:
    """Résumé ``result.json`` du run, s'il accompagne le flux.

    Un ``result.json`` présent mais illisible est une erreur et non une absence :
    un répertoire de run partiellement copié ne doit pas passer pour un run
    complet dont la référence a disparu.
    """
    manifest = stream.with_name(RESULT_FILE_NAME)
    if not manifest.is_file():
        return None
    try:
        declared = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise StreamFileError(f"résumé de run illisible : {manifest} ({exc})") from exc
    if not isinstance(declared, dict):
        raise StreamFileError(f"résumé de run illisible : {manifest} (objet attendu)")
    return declared


def inspect(path: str | Path) -> dict[str, object]:
    """Décrit le flux archivé : identité du run, segments, cohérence des ticks.

    Ordre réel émis par SYNE, un segment = ``snapshot`` puis ``tick_summary``
    puis les événements du tick : le ``tick_summary`` est un résumé des
    ressources du monde, pas une clôture de segment.

    Contrôles appliqués au flux :

    - ticks de snapshot **strictement croissants**, sans doublon ;
    - **un** ``tick_summary`` par tick de snapshot.

    Contrôle inter-fichiers : si ``result.json`` accompagne le flux, ses ``ticks``
    et son ``runId`` doivent correspondre. C'est le seul moyen de détecter une
    troncature **entre deux segments** : un JSONL amputé d'une frontière de
    segment est indiscernable d'un run réellement plus court, et aucun examen du
    flux seul ne peut trancher.

    Lève :class:`StreamFileError` dans tous ces cas.
    """
    stream = Path(path)
    run_id: str | None = None
    last_tick: int | None = None
    snapshot_ticks: list[int] = []
    # Comptage et non ``set`` : deux ``tick_summary`` pour le même tick doivent
    # être refusés, et un ensemble les fusionnerait silencieusement. Le nombre de
    # lignes émises est comparé au nombre de snapshots, pas au nombre de ticks
    # distincts — c'est la seule façon de voir un doublon.
    summaries: dict[int, int] = {}
    frames = 0

    for message in StreamFileClient(stream):
        frames += 1
        type_value = getattr(message, "type", None)
        if type_value == "snapshot":
            tick = int(message.tick)
            if last_tick is not None and tick <= last_tick:
                raise StreamFileError(
                    f"flux d'observabilité désordonné : tick {tick} après {last_tick}"
                )
            last_tick = tick
            snapshot_ticks.append(tick)
            if run_id is None:
                run_id = str(message.run_id)
        elif type_value == "tick_summary":
            tick = int(message.tick)
            summaries[tick] = summaries.get(tick, 0) + 1

    if run_id is None or last_tick is None:
        raise StreamFileError(f"flux d'observabilité sans snapshot : {stream}")

    duplicated = sorted(tick for tick, seen in summaries.items() if seen > 1)
    if duplicated:
        raise StreamFileError(
            f"flux d'observabilité avec tick_summary répété pour le(s) tick(s) "
            f"{duplicated} — un seul résumé par snapshot est attendu"
        )

    unpaired = [tick for tick in snapshot_ticks if tick not in summaries]
    if unpaired:
        raise StreamFileError(
            f"flux d'observabilité sans tick_summary pour le(s) tick(s) {unpaired}"
        )
    if len(summaries) != len(snapshot_ticks):
        raise StreamFileError(
            f"flux d'observabilité incohérent : {len(summaries)} tick_summary "
            f"pour {len(snapshot_ticks)} snapshot(s)"
        )

    declared = _declared_result(stream)
    if declared is not None:
        declared_run = declared.get("runId")
        if declared_run is not None and str(declared_run) != run_id:
            raise StreamFileError(
                f"{stream} déclare le run {run_id} alors que {RESULT_FILE_NAME} "
                f"annonce {declared_run} — répertoire de run incohérent"
            )
        declared_ticks = declared.get("ticks")
        if isinstance(declared_ticks, int) and declared_ticks != len(snapshot_ticks):
            raise StreamFileError(
                f"flux d'observabilité tronqué : {RESULT_FILE_NAME} annonce "
                f"{declared_ticks} tick(s), le flux n'en contient que {len(snapshot_ticks)}"
            )

    return {
        "runId": run_id,
        "ticks": last_tick,
        "segments": len(snapshot_ticks),
        "frames": frames,
    }
