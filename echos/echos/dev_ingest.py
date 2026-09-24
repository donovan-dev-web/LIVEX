"""Run the local SYNE-to-ECHOS ingestion worker for development."""

from __future__ import annotations

import os
import time
from pathlib import Path

from echos.ingestion import WsClient
from echos.storage import AnalyticsStore, consume


def main() -> int:
    """Connect to SYNE, ingest its stream, and write a readiness marker."""
    database = os.environ.get(
        "ECHOS_ANALYTICS_DB", "echos/data/livex-analytics.sqlite"
    )
    ws_url = os.environ.get("SYNE_OBSERVABILITY_URL", "ws://127.0.0.1:5180/")
    parquet_path = os.environ.get("ECHOS_PARQUET_PATH") or None
    started_file = os.environ.get("LIVEX_WS_STARTED_FILE")
    if started_file:
        Path(started_file).touch()

    while True:
        client = WsClient()
        try:
            client.connect(ws_url)
            break
        except OSError:
            print(f"SYNE WebSocket en attente : {ws_url}", flush=True)
            time.sleep(0.1)

    print(f"ECHOS connecté au flux SYNE : {ws_url}", flush=True)
    try:
        with AnalyticsStore(database) as store:
            result = consume(client, store, parquet_path=parquet_path)
        print(
            "Ingestion terminée : "
            f"{result.ticks_written} ticks, {result.events_written} événements",
            flush=True,
        )
    finally:
        client.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
