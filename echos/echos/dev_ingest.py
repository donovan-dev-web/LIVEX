"""Run the local SYNE-to-ECHOS ingestion worker for development."""

from __future__ import annotations

import os
import sqlite3
import sys
import time
from pathlib import Path

from websockets.exceptions import WebSocketException

from echos.ingestion import WsClient
from echos.ingestion.models import InvalidMessageError
from echos.ingestion.stream import TickAlignmentError
from echos.storage import DEFAULT_CONTEXT_EVERY, AnalyticsStore, consume

_RECONNECT_BASE_DELAY = 0.5
"""Premier délai de reconnexion (s). 0,1 s saturait la console de tentatives."""

_RECONNECT_MAX_DELAY = 30.0
"""Plafond du backoff exponentiel (s)."""


def _reconnect_delay(attempt: int) -> float:
    """Délai avant la tentative ``attempt + 1`` (exponentiel plafonné)."""
    return min(_RECONNECT_MAX_DELAY, _RECONNECT_BASE_DELAY * (2**attempt))


class ConfigurationError(ValueError):
    """Variable d'environnement absente ou inexploitable au démarrage."""


def _env_int(name: str, default: int | None, *, minimum: int = 1) -> int | None:
    """Lit un entier de l'environnement avec un message d'erreur explicite.

    ``int(os.environ[...])`` échouait sur une valeur non numérique avec
    ``ValueError: invalid literal for int() with base 10: 'abc'``, sans nommer
    la variable : impossible de savoir laquelle corriger. Une cadence nulle
    était de plus acceptée ici alors qu'elle est rejetée plus bas par
    ``consume`` (le worker se connectait au flux avant de le découvrir).

    ``minimum`` est appliqué ici : la configuration est validée **avant** la
    connexion, donc avant toute écriture.
    """
    raw = os.environ.get(name)
    if raw is None or raw == "":
        return default
    try:
        value = int(raw)
    except ValueError:
        raise ConfigurationError(
            f"{name} doit être un entier, reçu {raw!r}"
        ) from None
    if value < minimum:
        raise ConfigurationError(
            f"{name} doit être >= {minimum}, reçu {value}"
        )
    return value


def read_config(environ: dict[str, str] | None = None) -> dict:
    """Configuration du worker, validée, depuis l'environnement.

    Dict plat plutôt qu'un dataclass : ce sont les mêmes clés que celles
    documentées pour l'utilisateur, ce qui rend la correspondance
    environnement ↔ configuration directe. Retourne ``database``, ``ws_url``,
    ``parquet_path``, ``analysis_every``, ``parquet_flush_every``,
    ``context_every``, ``started_file`` et ``stop_after_disconnect``.
    """
    if environ is not None:
        previous = dict(os.environ)
        os.environ.clear()
        os.environ.update(environ)
        try:
            return read_config()
        finally:
            os.environ.clear()
            os.environ.update(previous)
    return {
        "database": os.environ.get(
            "ECHOS_ANALYTICS_DB", "echos/data/livex-analytics.sqlite"
        ),
        "ws_url": os.environ.get("SYNE_OBSERVABILITY_URL", "ws://127.0.0.1:5180/"),
        "parquet_path": os.environ.get("ECHOS_PARQUET_PATH") or None,
        "analysis_every": _env_int("ECHOS_ANALYSIS_EVERY", 1),
        # ``context_every`` (C2) : la cadence du contexte ``agents`` est validée
        # ici comme les autres (rejet bruyant avant connexion), puis relue par
        # le pipeline au moment de consommer.
        "context_every": _env_int("ECHOS_CONTEXT_EVERY", DEFAULT_CONTEXT_EVERY),
        "parquet_flush_every": _env_int("ECHOS_PARQUET_FLUSH_EVERY", None),
        "started_file": os.environ.get("LIVEX_WS_STARTED_FILE"),
        "stop_after_disconnect": os.environ.get("LIVEX_INGEST_ONCE") == "1",
    }


def main() -> int:
    """Connect to SYNE, ingest its stream, and write a readiness marker.

    La configuration est lue et validée **avant** l'ouverture de la base et
    avant la connexion : une cadence invalide est signalée immédiatement, au
    lieu d'être découverte au milieu de l'ingestion (ou jamais, si le flux SYNE
    ne démarre pas).
    """
    config = read_config()
    database = config["database"]
    ws_url = config["ws_url"]
    parquet_path = config["parquet_path"]
    analysis_every = config["analysis_every"]
    parquet_flush_every = config["parquet_flush_every"]
    started_file = config["started_file"]
    stop_after_disconnect = config["stop_after_disconnect"]
    if started_file:
        Path(started_file).touch()

    with AnalyticsStore(database) as store:
        attempt = 0
        while True:
            client = WsClient()
            try:
                client.connect(ws_url)
                # Une connexion réussie remet le backoff à zéro : le compteur
                # ne sert qu'à Etsy quand le flux SYNE est indisponible.
                attempt = 0
                print(f"ECHOS connecté au flux SYNE : {ws_url}", flush=True)
                result = consume(
                    client,
                    store,
                    parquet_path=parquet_path,
                    analysis_every=analysis_every,
                    parquet_flush_every=parquet_flush_every,
                )
                if stop_after_disconnect:
                    print(
                        "Ingestion terminée : "
                        f"{result.ticks_written} ticks, {result.events_written} événements"
                        + (
                            f", {result.gaps_detected} trou(s) de ticks détecté(s)"
                            if result.gaps_detected
                            else ""
                        )
                        + ".",
                        flush=True,
                    )
                    break
                print(
                    "Ingestion interrompue : "
                    f"{result.ticks_written} ticks, {result.events_written} événements"
                    + (
                        f", {result.gaps_detected} trou(s) de ticks détecté(s)"
                        if result.gaps_detected
                        else ""
                    )
                    + "; reconnexion...",
                    flush=True,
                )
            except (
                WebSocketException,
                OSError,
                ConnectionError,
                TimeoutError,
                InvalidMessageError,
                TickAlignmentError,
                sqlite3.Error,
                ValueError,
            ) as exc:
                print(
                    f"Ingestion interrompue ({exc}); reconnexion...",
                    flush=True,
                )
            finally:
                client.close()
            delay = _reconnect_delay(attempt)
            attempt = min(attempt + 1, 16)
            print(f"Nouvelle tentative dans {delay:.1f}s.", flush=True)
            time.sleep(delay)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ConfigurationError as error:
        # Sortie 2 : configuration invalide. Distincte de 1 (échec d'ingestion)
        # pour qu'un script de démarrage puisse réagir sans ambiguïté.
        print(f"Configuration ECHOS invalide : {error}", file=sys.stderr, flush=True)
        raise SystemExit(2) from None
