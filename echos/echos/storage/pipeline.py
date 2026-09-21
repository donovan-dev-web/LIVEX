"""Pipeline d'ingestion ECHOS vers le stockage d'analyse (ECHOS-011→013).

:func:`consume` enchaîne la boucle réelle : écoute du WebSocket :5180
(ECHOS-010), agrégation par tick sans perte (ECHOS-011), écriture SQLite
(ECHOS-012) et séries Parquet (ECHOS-013). ``sample_every`` (``--sample-every=N``,
API_REST.md §4) limite l'ingestion à 1 tick sur N ; ``None`` garde tout.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterator

from echos.ingestion.stream import TickSegment, aligned_ticks
from echos.ingestion.ws_client import WsClient
from echos.storage.aggregation import TickRecord
from echos.storage.parquet import AgentSeriesRow, agent_rows, read_agent_series
from echos.storage.parquet import write_agent_series
from echos.storage.sqlite import AnalyticsStore


@dataclass(frozen=True)
class ConsumeResult:
    """Compteurs d'écriture du pipeline (test : idempotence et couverture)."""

    ticks_written: int
    events_written: int
    agents_written: int


def _segments(
    client: WsClient, sample_every: int | None
) -> Iterator[tuple[int, TickSegment]]:
    for index, segment in enumerate(aligned_ticks(client)):
        if sample_every is not None and index % sample_every != 0:
            continue
        yield index, segment


def _seed_of(run_id: str) -> str:
    return run_id[4:] if run_id.startswith("run-") else ""


def consume(
    client: WsClient,
    store: AnalyticsStore,
    *,
    sample_every: int | None = None,
    parquet_path: str | Path | None = None,
) -> ConsumeResult:
    """Consomme le flux :5180 et peuple le stockage d'analyse.

    Métadonnées (``run_id``, ``version``, ``seed``) dérivées du premier
    snapshot ; l'écriture Parquet est optionnelle via ``parquet_path``.
    """
    ticks_written = 0
    events_written = 0
    agents_written = 0
    run_known = False

    for _index, segment in _segments(client, sample_every):
        snapshot = segment.snapshot
        if not run_known:
            store.record_run(
                snapshot.run_id,
                snapshot.version,
                _seed_of(snapshot.run_id),
            )
            run_known = True

        store.append_tick(TickRecord.from_segment(segment))
        ticks_written += 1

        for event in segment.events:
            store.append_event(
                snapshot.run_id,
                segment.tick,
                event.type,
                agent_id=event.agent_id,
                target_id=event.target_id,
                action=event.action,
                cause=event.cause,
                value=event.value and _json_dumps(event.value),
            )
            events_written += 1

        if parquet_path is not None:
            rows = agent_rows(segment)
            _extend_agent_series(parquet_path, rows)
            agents_written += len(rows)

    return ConsumeResult(ticks_written, events_written, agents_written)


def _json_dumps(value: dict) -> str:
    import json

    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def _extend_agent_series(
    path: str | Path, rows: list[AgentSeriesRow]
) -> None:
    """Écriture cumulée : réunit les lignes existantes puis les nouvelles."""
    if Path(path).exists():
        existing = list(read_agent_series(str(path)))
        write_agent_series(path, [*existing, *rows])
    else:
        write_agent_series(path, rows)
