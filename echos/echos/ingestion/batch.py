"""Peuplement de la base analytique depuis un run batch archivé (J2B).

L'API d'analyse d'ECHOS ne calcule rien : elle lit des runs déjà enregistrés.
Le flux live :5180 n'est qu'un des deux chemins d'alimentation ; l'autre est le
fichier ``data/stream.jsonl`` qu'un run supervisé écrit dans son workspace et que
le Launcher archive dans le paquet. Ce module est ce second chemin.

Il réutilise :func:`echos.storage.pipeline.consume` sans le modifier : les
métriques sont donc **calculées à l'ingestion, jamais recalculées à la lecture**,
et un run relu depuis un paquet produit le même contenu de base qu'un run
observé en direct.
"""

from __future__ import annotations

import sqlite3
from pathlib import Path
from typing import Any

from echos.ingestion.file_client import (
    StreamFileClient,
    StreamFileError,
    inspect,
    resolve_stream_file,
)
from echos.storage.pipeline import ConsumeResult, consume
from echos.storage.sqlite import AnalyticsStore


class IngestError(ValueError):
    """Un run archivé ne peut pas être enregistré tel quel."""

    def __init__(self, status_code: int, detail: str) -> None:
        super().__init__(detail)
        self.status_code = status_code


def _purge(store: AnalyticsStore, run_id: str) -> None:
    """Efface les lignes écrites par une ingestion qui vient d'échouer.

    ``consume`` valide et commite chaque tick séparément : un flux invalidé **en
    milieu de parcours** laisse donc les segments déjà traités dans la base. Sans
    purge, le run resterait à moitié enregistré à jamais, et la garde
    anti-doublon refuserait ensuite l'ingestion de l'archive valide pour une base
    qui ne contient que la moitié du run. Un artefact interrompu ne doit jamais
    devenir une analyse partielle présentée comme complète.

    Best-effort : une base déjà en erreur ne doit pas substituer une erreur SQLite
    à la cause réelle, qui est l'information utile pour l'appelant.
    """
    try:
        store.discard_run(run_id)
    except sqlite3.Error:
        pass


def ingest_run_stream(
    store: AnalyticsStore,
    run_path: str,
    *,
    expected_run_id: str | None = None,
    parquet_path: str | Path | None = None,
) -> dict[str, Any]:
    """Enregistre le flux archivé d'un run et renvoie ce qui a été écrit.

    ``run_path`` est le répertoire de travail du run (``--work-dir``), celui que
    le Launcher archive : le flux se trouve dans son sous-dossier ``data``.

    L'identité fait autorité dans le fichier : ``expected_run_id`` n'est qu'une
    vérification, et une divergence est refusée **avant** toute écriture — un
    flux enregistré sous la mauvaise clé ne serait plus aucun des deux runs
    attendus.

    L'ingestion est **idempotente par refus** : un run déjà présent dans la base
    est une erreur explicite, pas une réécriture. Réécrire doublerait les lignes
    d'événements, qui n'ont pas de clé d'idempotence, et produirait un rapport
    silencieusement faux — le pire état possible pour une analyse.
    """
    try:
        stream = resolve_stream_file(run_path)
        described = inspect(stream)
    except StreamFileError as exc:
        raise IngestError(404 if "absent" in exc.detail else 409, str(exc)) from exc

    run_id = str(described["runId"])
    if expected_run_id is not None and expected_run_id != run_id:
        raise IngestError(
            409,
            f"run demandé {expected_run_id} mais le flux déclare {run_id} — "
            "le répertoire ne contient pas le run demandé",
        )
    if store.count_ticks(run_id) > 0:
        raise IngestError(
            409,
            f"run déjà enregistré : {run_id} "
            f"({store.count_ticks(run_id)} tick(s) présent(s)) — "
            "l'ingestion d'un run existant n'est pas idempotente par réécriture",
        )

    # Un run sans tick mais présent dans la base est un résidu d'ingestion
    # interrompue (voir ``discard_run``) : la garde ci-dessus l'ignore, donc on
    # le purge pour que l'archive reparte d'une base propre. La purge exige aussi
    # zéro événement : c'est le seul état qui n'a produit aucune donnée
    # analysable, donc une ligne ``runs`` purement orpheline. Un run en cours
    # d'ingestion live a déjà son premier tick à ce stade.
    if store.has_run(run_id) and store.count_events(run_id) == 0:
        store.discard_run(run_id)

    try:
        result: ConsumeResult = consume(
            StreamFileClient(stream),
            store,
            parquet_path=parquet_path,
        )
    except StreamFileError as exc:
        _purge(store, run_id)
        raise IngestError(422, str(exc)) from exc
    except ValueError as exc:
        # Désalignement du flux ou paramètre d'ingestion refusé : l'artefact
        # n'est pas conforme au contrat, pas la demande.
        _purge(store, run_id)
        raise IngestError(422, f"flux d'observabilité non conforme : {exc}") from exc

    written = store.count_ticks(run_id)
    if written != int(described["segments"]):
        # L'archive est conforme (elle a passé ``inspect``) mais l'écriture est
        # incomplète : c'est un défaut d'ingestion, pas un artefact invalide. On
        # purge pour ne pas laisser un run tronqué présenté comme complet.
        _purge(store, run_id)
        raise IngestError(
            500,
            f"ingestion incomplète : {written} tick(s) écrit(s) pour "
            f"{described['segments']} segment(s) annoncé(s)",
        )

    return {
        "runId": run_id,
        "ticks": written,
        "horizon": described["ticks"],
        "events": result.events_written,
        "metrics": result.metrics_written,
        "contexts": result.contexts_written,
        "decisionTraces": result.decision_traces_written,
        "gaps": result.gaps_detected,
    }
