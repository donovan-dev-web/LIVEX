"""Pipeline d'ingestion ECHOS vers le stockage d'analyse (ECHOS-011→013, ph4).

:func:`consume` enchaîne la boucle réelle : écoute du WebSocket :5180
(ECHOS-010), agrégation par tick sans perte (ECHOS-011), écriture SQLite
(ECHOS-012) et séries Parquet (ECHOS-013). ``sample_every`` (``--sample-every=N``,
API_REST.md §4) limite l'ingestion à 1 tick sur N ; ``None`` garde tout.
Depuis le jalon ECHOS ph4, chaque tick déclenche aussi les 8 moteurs
(``analysis.compute_all``) et persiste : ``tick_metrics`` (métriques
numériques) et ``tick_contexts`` (agents, groupes, phénomènes) — les métriques
sont donc **calculées à l'ingestion, jamais recalculées à la lecture**
(API_REST.md §4). ``CoherenceResult`` ajoute les compteurs associés.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterator

from echos.analysis import compute_all
from echos.analysis._common import label_propagation
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
    metrics_written: int = 0
    contexts_written: int = 0


def _segments(
    client: WsClient, sample_every: int | None
) -> Iterator[tuple[int, TickSegment]]:
    for index, segment in enumerate(aligned_ticks(client)):
        if sample_every is not None and index % sample_every != 0:
            continue
        yield index, segment


def _seed_of(run_id: str) -> str:
    return run_id[4:] if run_id.startswith("run-") else ""


def _snapshot_for_engines(segment: TickSegment) -> dict:
    """Dict transport camelCase attendu par les moteurs (snapshot + événements).

    Les moteurs lisent ``agents``/``resources``/``aliveCount`` sur le snapshot
    et ``events`` (``decision_made``, ``message_sent``, ``group_formed``...)
    au niveau racine — lisible par ``compute_all`` sans données manquantes.
    """
    snapshot = segment.snapshot.model_dump(mode="json", by_alias=True, exclude_none=True)
    snapshot["events"] = [
        event.model_dump(mode="json", by_alias=True, exclude_none=True)
        for event in segment.events
    ]
    return snapshot


def _groups_of(agents: list[dict]) -> list[dict]:
    """Communautés actives (attribution d'étiquettes) en ordre déterministe.

    Chaque groupe : ``label`` (communauté), ``members`` (ids triés) et
    ``size``. Aucune liaison de confiance → liste vide (aucun groupe).
    """
    labels = label_propagation(agents)
    if not labels:
        return []
    buckets: dict[str, list[str]] = {}
    for agent_id in sorted(labels):
        buckets.setdefault(str(labels[agent_id]), []).append(str(agent_id))
    return [
        {"label": label, "members": members, "size": len(members)}
        for label, members in sorted(buckets.items())
    ]


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
    Chaque tick écrit aussi les métriques des 8 moteurs (``tick_metrics``)
    et les contextes ``agents``/``groups``/``phenomena`` (``tick_contexts``).
    """
    ticks_written = 0
    events_written = 0
    agents_written = 0
    metrics_written = 0
    contexts_written = 0
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

        engine_snapshot = _snapshot_for_engines(segment)
        metrics = compute_all(engine_snapshot)
        metrics_written += store.append_tick_metrics(
            snapshot.run_id, segment.tick, metrics
        )

        emergence = metrics.get("EmergenceIndicators") or {}
        store.append_tick_context(
            snapshot.run_id,
            segment.tick,
            "phenomena",
            {
                "detected": emergence.get("DetectedPhenomena", []),
                "disclaimer": emergence.get("Disclaimer", ""),
            },
        )
        store.append_tick_context(
            snapshot.run_id,
            segment.tick,
            "agents",
            engine_snapshot.get("agents") or [],
        )
        store.append_tick_context(
            snapshot.run_id,
            segment.tick,
            "groups",
            _groups_of(engine_snapshot.get("agents") or []),
        )
        contexts_written += 3

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

    return ConsumeResult(
        ticks_written,
        events_written,
        agents_written,
        metrics_written,
        contexts_written,
    )


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
