"""Run the local SYNE-to-ECHOS ingestion worker for development."""

from __future__ import annotations

import os
import sqlite3
import time
from pathlib import Path

from websockets.exceptions import WebSocketException

from echos.ingestion import WsClient
from echos.ingestion.models import InvalidMessageError
from echos.ingestion.stream import TickAlignmentError
from echos.storage import AnalyticsStore, consume


def main() -> int:
    """Connect to SYNE, ingest its stream, and write a readiness marker."""
    database = os.environ.get(
        "ECHOS_ANALYTICS_DB", "echos/data/livex-analytics.sqlite"
    )
    ws_url = os.environ.get("SYNE_OBSERVABILITY_URL", "ws://127.0.0.1:5180/")
    parquet_path = os.environ.get("ECHOS_PARQUET_PATH") or None
    started_file = os.environ.get("LIVEX_WS_STARTED_FILE")
    stop_after_disconnect = os.environ.get("LIVEX_INGEST_ONCE") == "1"
    if started_file:
        Path(started_file).touch()

    with AnalyticsStore(database) as store:
        while True:
            client = WsClient()
            try:
                client.connect(ws_url)
                print(f"ECHOS connecté au flux SYNE : {ws_url}", flush=True)
                result = consume(client, store, parquet_path=parquet_path)
                if stop_after_disconnect:
                    print(
                        "Ingestion terminée : "
                        f"{result.ticks_written} ticks, {result.events_written} événements.",
                        flush=True,
                    )
                    break
                print(
                    "Ingestion interrompue : "
                    f"{result.ticks_written} ticks, {result.events_written} événements; "
                    "reconnexion...",
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
            # Also back off after a clean disconnect to avoid a reconnect loop.
            time.sleep(0.1)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
