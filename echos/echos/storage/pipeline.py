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

**Planification (scheduler)** : ``analysis_every=N`` exécute les moteurs et
écrit métriques/contextes 1 tick sur N (déterminisme : indiciel, N stable).
Les données d'ingestion (``tick_summaries``, ``events_log``,
``decision_traces``, Parquet agents) restent écrites **à chaque tick**.
``parquet_flush_every`` borne la mémoire de la série Parquet : écriture
cumulée toutes les N ticks (au lieu de réécrire le fichier entier à chaque
tick), vidée automatiquement en fin de flux.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterator

from echos.analysis import compute_all
from echos.analysis.calibration import build_calibration_report
from echos.analysis._common import label_propagation
from echos.ingestion.stream import TickSegment, aligned_ticks
from echos.ingestion.ws_client import WsClient
from echos.instrumentation.decision_traces import build_decision_trace
from echos.instrumentation.logging import EchosLogger
from echos.instrumentation.profiling import ProfileMarkers
from echos.storage.aggregation import TickRecord
from echos.storage.parquet import AgentSeriesRow, agent_rows, read_agent_series
from echos.storage.parquet import write_agent_series
from echos.storage.sqlite import AnalyticsStore

_DECISION_TYPE = "decision_made"


@dataclass(frozen=True)
class ConsumeResult:
    """Compteurs d'écriture du pipeline (test : idempotence et couverture)."""

    ticks_written: int
    events_written: int
    agents_written: int
    metrics_written: int = 0
    contexts_written: int = 0
    decision_traces_written: int = 0


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
    logger: EchosLogger | None = None,
    analysis_every: int = 1,
    parquet_flush_every: int | None = None,
) -> ConsumeResult:
    """Consomme le flux :5180 et peuple le stockage d'analyse.

    Métadonnées (``run_id``, ``version``, ``seed``) dérivées du premier
    snapshot ; l'écriture Parquet est optionnelle via ``parquet_path``.
    ``analysis_every`` (scheduler, défaut 1) planifie les 8 moteurs et les
    contextes sur 1 tick sur N — le reste du pipeline (résumés, événements,
    traces de décision, série Parquet) reste écrit à chaque tick. Depuis le
    jalon ph5 : traces de décision ``decision_made`` (``decision_traces``,
    ECHOS-051), profilage par moteur (ECHOS-052) et, si ``logger`` est fourni,
    journalisation structurée JSON Lines (ECHOS-050).
    """
    if analysis_every < 1:
        raise ValueError("analysis_every doit être >= 1")
    ticks_written = 0
    events_written = 0
    agents_written = 0
    metrics_written = 0
    contexts_written = 0
    decision_traces_written = 0
    run_known = False
    run_id: str | None = None
    pending_agents: list[AgentSeriesRow] = []

    for _index, segment in _segments(client, sample_every):
        snapshot = segment.snapshot
        if not run_known:
            run_id = snapshot.run_id
            store.record_run(
                snapshot.run_id,
                snapshot.version,
                _seed_of(snapshot.run_id),
            )
            run_known = True

        tick_record = TickRecord.from_segment(segment)

        events = [
            (
                event.type,
                event.agent_id,
                event.target_id,
                event.action,
                event.cause,
                event.value and _json_dumps(event.value),
            )
            for event in segment.events
        ]
        has_decision = any(event.type == _DECISION_TYPE for event in segment.events)

        # The engine snapshot is a large JSON dump of the world: build it only
        # when analysis runs on this tick or a decision trace needs its context.
        at_cadence = _index % analysis_every == 0
        engine_snapshot = (
            _snapshot_for_engines(segment) if (at_cadence or has_decision) else None
        )
        metrics: dict[str, dict] = {}
        contexts: dict[str, object] = {}
        if at_cadence:
            assert engine_snapshot is not None
            markers = ProfileMarkers()
            metrics = compute_all(engine_snapshot, profile=markers)
            profile = markers.summary()
            emergence = metrics.get("EmergenceIndicators") or {}
            contexts = {
                "phenomena": {
                    "detected": emergence.get("DetectedPhenomena", []),
                    "disclaimer": emergence.get("Disclaimer", ""),
                },
                "agents": engine_snapshot.get("agents") or [],
                "groups": _groups_of(engine_snapshot.get("agents") or []),
                "profiling": profile,
            }
            contexts_written += 4

            if logger is not None:
                logger.structured(snapshot.run_id, segment.tick, metrics)
                logger.profiling(snapshot.run_id, segment.tick, profile)

        traces = []
        for event in segment.events:
            events_written += 1

            if event.type == _DECISION_TYPE:
                trace = build_decision_trace(
                    snapshot.run_id, segment.tick, event,
                    engine_snapshot or _snapshot_for_engines(segment),
                )
                traces.append(trace)
                decision_traces_written += 1
                if logger is not None:
                    logger.decision(trace)

        metrics_written += store.append_tick_bundle(
            tick_record, metrics, contexts, events, traces
        )
        ticks_written += 1

        if parquet_path is not None:
            rows = agent_rows(segment)
            pending_agents.extend(rows)
            agents_written += len(rows)
            if parquet_flush_every is not None and _index and _index % parquet_flush_every == 0:
                _flush_agent_series(parquet_path, pending_agents)
                pending_agents = []

        if logger is not None:
            logger.debug(
                f"tick={segment.tick} run={snapshot.run_id} "
                f"metrics={metrics_written} decisions={decision_traces_written}"
            )

    if parquet_path is not None and pending_agents:
        _flush_agent_series(parquet_path, pending_agents)

    if ticks_written:
        assert run_id is not None
        report = build_calibration_report(
            run_id,
            store.tick_summaries(run_id),
            store.events(run_id),
            store.metrics_all(run_id),
        )
        if report is not None:
            store.save_calibration_report(run_id, report)

    return ConsumeResult(
        ticks_written,
        events_written,
        agents_written,
        metrics_written,
        contexts_written,
        decision_traces_written,
    )


def _json_dumps(value: dict) -> str:
    import json

    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def _flush_agent_series(
    path: str | Path, rows: list[AgentSeriesRow]
) -> None:
    """Écriture cumulée bornée : réunit les lignes existantes puis les nouvelles.

    Remplace l'ancienne réécriture à chaque tick (O(n²) en lecture/émission) :
    la série est regroupée en mémoire sur ``parquet_flush_every`` ticks puis
    écrite en une passe — bien moins de RAM et d'I/O, ordre toujours stable.
    """
    if Path(path).exists():
        existing = list(read_agent_series(str(path)))
        write_agent_series(path, [*existing, *rows])
    else:
        write_agent_series(path, rows)
