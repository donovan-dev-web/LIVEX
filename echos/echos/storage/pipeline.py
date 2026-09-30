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
Le contexte ``agents`` suit sa propre cadence ``context_every`` (défaut 20,
environnement ``ECHOS_CONTEXT_EVERY`` — axe C2) : il est de loin le plus lourd
(~208 Ko/tick) et n'est utile en lecture que « le plus récent disponible ».
``parquet_flush_every`` borne la mémoire de la série Parquet : écriture
cumulée toutes les N ticks (au lieu de réécrire le fichier entier à chaque
tick), vidée automatiquement en fin de flux.

**Changement de run en flux** (A2) : un ``reset`` SYNE continue le flux avec
un nouveau ``run_id`` dans la même connexion. Le pipeline détecte le nouveau
``run_id``, enregistre le run **avant** son premier tick (sinon FK error et
ticks perdus), réinitialise les fenêtres glissantes et bâtit le rapport de
calibration du run terminé — chaque run a donc son rapport, pas seulement le
dernier.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator

from echos.analysis import compute_all, provenance
from echos.analysis.calibration import build_calibration_report
from echos.analysis._common import communities, community_sizes
from echos.analysis.feedback_loop_detector import WINDOW_SIZE as _HISTORY_TICKS
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

_EVENT_WINDOW_TICKS = _HISTORY_TICKS
"""Ticks d'événements conservés pour les moteurs (bornes la mémoire du run).

Aligné sur ``FeedbackLoopDetector.WINDOW_SIZE`` (100 ticks) : les trois fenêtres
du contexte glissant couvrent désormais la même durée, ce qui rend les taux
« par 1000 ticks » de ``GroupDynamicsMetrics`` comparables d'un run à l'autre.

La borne est en **ticks** et non en nombre d'événements : avec une borne
d'événements, la durée couverte dépendait de l'activité du monde. Sur un run
chargé, 1000 événements couvraient ~20 ticks (taux de formation ×5) ; sur un run
tranquille, 1000 ticks (taux ÷50). Le même nombre d'événements donnait donc un
taux dépendant de la charge, non du comportement des groupes.
"""

_EVENT_WINDOW_MAX_EVENTS = 2000
"""Borne mémoire dure sur la fenêtre d'événements (indépendante des ticks).

La borne en ticks garantit le sens des métriques ; celle-ci garantit la
mémoire quand un tick unique produit une rafale d'événements (pic de deaths,
conflicts) : sans plafond, 100 ticks très denses transporteraient autant
d'événements que 1000 ticks tranquilles.
"""

DEFAULT_CONTEXT_EVERY = 20
"""Cadence par défaut du contexte ``agents`` (axe C2, campagne-runs).

Le contexte ``agents`` coûte ~208 Ko/tick (croyances, relations) : à chaque
tick, une campagne de 3129 ticks produisait 649 Mo. La cadence ``context_every``
borne cette écriture : ``agents`` n'est persisté que 1 tick sur N (défaut 20,
environnement ``ECHOS_CONTEXT_EVERY``, 1 = comportement historique) plus le
dernier tick du flux. ``groups``/``phenomena``/``profiling`` suivent la cadence
``analysis_every`` (inchangée) — la fraîcheur du contexte servi est documentée
dans CAUSAL_ANALYSIS.md §4 (≤ ``context_every`` ticks).
"""


def _context_every() -> int:
    """Résout ``ECHOS_CONTEXT_EVERY`` (défaut 20, minimum 1)."""
    raw = os.environ.get("ECHOS_CONTEXT_EVERY")
    if not raw:
        return DEFAULT_CONTEXT_EVERY
    try:
        value = int(raw)
    except ValueError:
        return DEFAULT_CONTEXT_EVERY
    return max(1, value)


@dataclass(frozen=True)
class ConsumeResult:
    """Compteurs d'écriture du pipeline (test : idempotence et couverture).

    ``gaps_detected`` (D3) compte les discontinuités de ticks détectées entre
    le dernier tick écrit d'un run et le tick reçu ensuite : elles signalent
    les pertes à la reconnexion ou au reset, sans jamais les masquer.
    """

    ticks_written: int
    events_written: int
    agents_written: int
    metrics_written: int = 0
    contexts_written: int = 0
    decision_traces_written: int = 0
    gaps_detected: int = 0


def _segments(
    client: WsClient, sample_every: int | None
) -> Iterator[tuple[int, TickSegment]]:
    """Segments indexés du flux, échantillonnés 1 sur N (``sample_every``).

    ``sample_every`` négatif ou nul est rejeté ici : ``index % 0`` levait
    ``ZeroDivisionError`` au premier tick, et une valeur négative
    ``index % -3 == 0`` n'est jamais vrai, ce qui faisait ignorer le silence
    du pipeline au lieu de le signaler. Idem pour
    ``parquet_flush_every``, validé dans :func:`consume`.
    """
    if sample_every is not None and sample_every < 1:
        raise ValueError("sample_every doit être >= 1")
    for index, segment in enumerate(aligned_ticks(client)):
        if sample_every is not None and index % sample_every != 0:
            continue
        yield index, segment


def _seed_of(run_id: str) -> str:
    """Dérive le seed du ``run_id`` (repli des flux V0.8/V0.2.0).

    Formats reconnus : ``run-<seed>`` (monde préparé, contrats ≤ V0.2.0) et
    ``run-<seed>-<12hex>`` (format canonique des runs pilotés, contrat V0.2.1).
    Chaîne vide si ni le snapshot ni le ``run_id`` ne portent le seed — jamais
    une erreur fatale (observe-only), signalée par un avertissement d'ingestion.
    """
    if not run_id.startswith("run-"):
        return ""
    body = run_id[4:]
    seed = body.split("-", 1)[0]
    return seed if seed.isdigit() else ""


def _snapshot_for_engines(
    segment: TickSegment, events: list[dict], history: list[dict], event_window: dict
) -> dict:
    """Dict transport camelCase attendu par les moteurs (snapshot + contexte).

    Les moteurs lisent ``agents``/``resources``/``aliveCount`` sur le snapshot
    instantané et trois fenêtres glissantes construites par le pipeline :

    - ``events`` : événements des ``_EVENT_WINDOW_TICKS`` derniers ticks, bornés
      en nombre par ``_EVENT_WINDOW_MAX_EVENTS``, pour ``MessageVolume``, la
      dynamique des groupes et la consommation ;
    - ``eventWindow`` : durée réellement couverte par cette fenêtre d'événements
      (``{"ticks", "from", "to"}``), afin que les taux normalisés utilisent le
      dénominateur observé du pipeline et non l'étendue accidentelle des
      événements présents ;
    - ``history`` : un point par tick (``tick``, ``actions``, ``resources``),
      pour ``FeedbackLoopDetector`` et ``RecoveryTime`` ;
    - ``communityHistory`` : tailles de communautés par tick, pour
      ``CommunityStability``.

    Sans ces fenêtres, ces 7 métriques restaient à leur repli neutre (0.0) sur
    chaque run réel, le snapshot ne portant que le tick courant.
    """
    snapshot = segment.snapshot.model_dump(mode="json", by_alias=True, exclude_none=True)
    snapshot["events"] = events
    snapshot["eventWindow"] = event_window
    snapshot["history"] = history
    snapshot["communityHistory"] = [
        {"tick": entry["tick"], "communities": entry["communities"]} for entry in history
    ]
    return snapshot


class _RollingContext:
    """Fenêtres glissantes transmises aux moteurs (bornes en mémoire).

    Chaque tick poussé est tronqué à sa fenêtre : les moteurs qui isolent un
    événement (``GroupFormationRate``) ou mesurent une durée
    (``InformationDiffusionSpeed``, ``RecoveryTime``) restent ainsi corrects sur
    les runs longs, sans jamais lire l'intégralité du flux.

    La fenêtre d'événements est bornée deux fois — en ticks
    (``_EVENT_WINDOW_TICKS``) puis en nombre (``_EVENT_WINDOW_MAX_EVENTS``) — et
    sa durée réelle est publiée dans le snapshot (``eventWindow``).
    """

    def __init__(
        self,
        *,
        event_window: int = _EVENT_WINDOW_TICKS,
        event_limit: int = _EVENT_WINDOW_MAX_EVENTS,
        history: int = _HISTORY_TICKS,
    ):
        if event_window < 1 or event_limit < 1 or history < 1:
            raise ValueError("les fenêtres du contexte doivent être >= 1")
        self._event_window = event_window
        self._event_limit = event_limit
        self._history_size = history
        self._events: list[dict] = []
        self._entries: list[dict] = []
        self._first_tick: int | None = None
        self._last_tick: int | None = None

    def push(
        self, segment: TickSegment
    ) -> tuple[list[dict], list[dict], dict]:
        """Enregistre le tick courant et renvoie (événements, historique, fenêtre).

        Les ticks manquants (échantillonnage ``sample_every``) ne créent pas de
        trou dans les fenêtres : l'historique est indexé par tick réellement
        observé, et les fenêtres se bornent par taille.
        """
        if self._first_tick is None:
            self._first_tick = segment.tick
        self._last_tick = segment.tick
        self._events.extend(
            event.model_dump(mode="json", by_alias=True, exclude_none=True)
            for event in segment.events
        )
        floor = segment.tick - self._event_window + 1
        if self._events and int(self._events[0].get("tick") or 0) < floor:
            self._events = [
                event
                for event in self._events
                if int(event.get("tick") or 0) >= floor
            ]
        del self._events[: -self._event_limit]

        self._entries.append(
            {
                "tick": segment.tick,
                "actions": _actions_of(segment),
                "resources": _resources_of(segment.snapshot),
                "communities": _community_sizes_of(segment.snapshot),
            }
        )
        del self._entries[: -self._history_size]
        return self._events, self._entries, self.event_window()

    def event_window(self) -> dict:
        """Fenêtre d'observation déclarée, en ticks.

        ``{"ticks", "from", "to"}`` décrit ce que le pipeline a **réellement
        observé**, pas l'étendue des événements présents : deux runs contenant
        le même nombre d'événements mais d'activité différente déclarent la
        même fenêtre dès qu'ils ont la même durée. C'est ce qui rend les taux
        « par 1000 ticks » de ``GroupDynamicsMetrics`` comparables d'un run à
        l'autre.

        La fenêtre est bornée par ``_EVENT_WINDOW_TICKS`` et cantonnée au run
        (``from`` ne précède jamais le premier tick vu). Elle vaut au minimum
        1 tick : jamais 0, donc jamais de division par zéro en aval.
        """
        to = self._last_tick
        if to is None:
            return {"ticks": 1, "from": None, "to": None}
        from_tick = to - self._event_window + 1
        if self._first_tick is not None:
            from_tick = max(from_tick, self._first_tick)
        return {"ticks": max(1, to - from_tick + 1), "from": from_tick, "to": to}


def _actions_of(segment: TickSegment) -> dict[str, str]:
    """Décisions du tick : ``{agentId: action}`` pour le détecteur de boucles."""
    return {
        str(event.agent_id): str(event.action)
        for event in segment.events
        if event.type == _DECISION_TYPE
    }


def _resources_of(snapshot) -> list[dict]:
    """Réserves ``{quantity, capacity}`` du tick, pour ``RecoveryTime``."""
    return [
        {
            "type": getattr(resource, "type", None) or resource.id,
            "quantity": resource.quantity,
            "capacity": resource.capacity,
        }
        for resource in snapshot.resources or []
    ]


def _community_sizes_of(snapshot) -> list[int]:
    """Tailles des communautés du tick, pour ``CommunityStability``."""
    return community_sizes(_agents_of(snapshot))


def _agents_of(snapshot) -> list[dict]:
    """Agents du snapshot en dict camelCase, une seule conversion par tick."""
    return [
        agent.model_dump(mode="json", by_alias=True, exclude_none=True)
        for agent in snapshot.agents or []
    ]


def _groups_of(agents: list[dict]) -> list[dict]:
    """Communautés actives en ordre déterministe.

    Chaque groupe : ``label`` (communauté), ``members`` (ids triés) et
    ``size``. Aucune liaison de confiance → liste vide (aucun groupe). Les
    singletons sont exclus : une entité isolée n'est pas une communauté, et les
    compter ici contredisait ``communities()`` côté moteurs.

    Le ``label`` est le plus petit identifiant du groupe, non l'indice renvoyé
    par la propagation d'étiquettes : celui-ci se décale dès qu'un nœud change
    de communauté, ce qui faisait bouger les identifiants de groupe affichés par
    l'UI sans raison observable. Le plus petit membre est, lui, stable tant que
    le groupe ne se recompose pas.
    """
    return [
        {"label": members[0], "members": members, "size": len(members)}
        for members in communities(agents)
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

    Métadonnées (``run_id``, ``version``, ``seed``) portées par le snapshot
    (seed transporté, contrat V0.2.1) avec repli de dérivation depuis le
    ``run_id`` ; l'écriture Parquet est optionnelle via ``parquet_path``.
    ``analysis_every`` (scheduler, défaut 1) planifie les 8 moteurs et les
    contextes sur 1 tick sur N — le reste du pipeline (résumés, événements,
    traces de décision, série Parquet) reste écrit à chaque tick. Depuis le
    jalon ph5 : traces de décision ``decision_made`` (``decision_traces``,
    ECHOS-051), profilage par moteur (ECHOS-052) et, si ``logger`` est fourni,
    journalisation structurée JSON Lines (ECHOS-050).
    """
    if analysis_every < 1:
        raise ValueError("analysis_every doit être >= 1")
    if parquet_flush_every is not None and parquet_flush_every < 1:
        raise ValueError("parquet_flush_every doit être >= 1")
    context_every = _context_every()
    ticks_written = 0
    events_written = 0
    agents_written = 0
    metrics_written = 0
    contexts_written = 0
    decision_traces_written = 0
    gaps_detected = 0
    run_known = False
    run_id: str | None = None
    last_tick_seen: int | None = None
    pending_agents: list[AgentSeriesRow] = []
    context = _RollingContext()
    last_segment: TickSegment | None = None
    last_agents_written = False

    def _seed_from(snapshot) -> str:
        """Seed du snapshot (transporté, contrat V0.2.1) ou repli ``_seed_of``.

        Ni l'un ni l'autre : chaîne vide **et** avertissement d'ingestion —
        jamais une erreur fatale (ECHOS reste observe-only).
        """
        if snapshot.seed is not None:
            return str(snapshot.seed)
        seed = _seed_of(snapshot.run_id)
        if not seed and logger is not None:
            logger.info(
                f"ingestion: seed absent du snapshot et du run_id "
                f"{snapshot.run_id!r} — comparabilité inter-runs limitée"
            )
        return seed

    def _close_run(finished_run_id: str) -> None:
        """Bâtit et sauvegarde le rapport de calibration d'un run terminé (A2).

        Appelé à chaque changement de run_id **et** en fin de flux : un run
        interrompu par un reset SYNE doit lui aussi produire son rapport, là où
        l'ancien bloc final ne couvrait que le dernier run_id vu (le rapport de
        calibration des runs 1..n-1 n'était jamais écrit).
        """
        report = build_calibration_report(
            finished_run_id,
            store.tick_summaries(finished_run_id),
            store.events(finished_run_id),
            store.metrics_all(finished_run_id),
        )
        if report is not None:
            store.save_calibration_report(finished_run_id, report)

    for _index, segment in _segments(client, sample_every):
        snapshot = segment.snapshot

        # A2 — changement de run_id détecté en flux (reset SYNE dans la même
        # connexion) : le nouveau run est enregistré **avant** son premier tick
        # (sinon append_tick_bundle lève FOREIGN KEY constraint failed et les
        # ticks du run sont perdus jusqu'à la reconnexion), les fenêtres
        # glissantes sont réinitialisées (sinon elles contaminent les métriques
        # du nouveau run) et le rapport de calibration du run terminé est écrit.
        if not run_known or snapshot.run_id != run_id:
            if run_known and run_id is not None:
                _close_run(run_id)
            run_id = snapshot.run_id
            store.record_run(
                snapshot.run_id,
                snapshot.version,
                _seed_from(snapshot),
            )
            run_known = True
            context = _RollingContext()
            if parquet_path is not None and pending_agents:
                _flush_agent_series(parquet_path, pending_agents)
                pending_agents = []

        # D3 — trou de ticks détecté (reconnexion, reset inter-run, échantillonnage
        # amont) : le dernier tick vu du flux ne précède pas le tick reçu de 1.
        # Le rattrapage (replay SYNE) reste hors périmètre V0.1, mais la perte
        # est comptée (``ConsumeResult.gaps_detected``) et journalisée quand un
        # logger est fourni — jamais masquée.
        if last_tick_seen is not None and segment.tick != last_tick_seen + 1:
            gaps_detected += 1
            if logger is not None:
                logger.info(
                    f"ingestion: trou de ticks détecté sur {snapshot.run_id} : "
                    f"attendu {last_tick_seen + 1}, reçu {segment.tick}"
                )

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
        window_events, window_history, event_window = context.push(segment)

        # The engine snapshot is a large JSON dump of the world: build it only
        # when analysis runs on this tick or a decision trace needs its context.
        at_cadence = _index % analysis_every == 0
        engine_snapshot = (
            _snapshot_for_engines(
                segment, window_events, window_history, event_window
            )
            if (at_cadence or has_decision)
            else None
        )
        metrics: dict[str, dict] = {}
        contexts: dict[str, object] = {}
        measured: dict[str, dict[str, bool]] = {}
        if at_cadence:
            assert engine_snapshot is not None
            markers = ProfileMarkers()
            metrics = compute_all(engine_snapshot, profile=markers)
            # Provenance : quelles métriques ont réellement été mesurées plutôt
            # que retomber sur leur repli neutre faute de données.
            measured = provenance(engine_snapshot)
            profile = markers.summary()
            emergence = metrics.get("EmergenceIndicators") or {}
            # C2 — cadence du contexte ``agents`` : ~208 Ko/tick en font de loin
            # le contexte le plus lourd. Il n'est persisté que 1 tick sur
            # ``context_every`` (plus le dernier tick du flux), là où les autres
            # contextes suivent ``analysis_every``. ``context_every=1`` (env
            # ``ECHOS_CONTEXT_EVERY=1``) rétablit le comportement historique.
            at_context_cadence = _index % context_every == 0
            last_agents_written = at_context_cadence
            if at_context_cadence:
                contexts = {
                    "phenomena": {
                        "detected": emergence.get("DetectedPhenomena", []),
                        "disclaimer": emergence.get("Disclaimer", ""),
                    },
                    "agents": engine_snapshot.get("agents") or [],
                    "groups": _groups_of(engine_snapshot.get("agents") or []),
                    "profiling": profile,
                }
            else:
                contexts = {
                    "phenomena": {
                        "detected": emergence.get("DetectedPhenomena", []),
                        "disclaimer": emergence.get("Disclaimer", ""),
                    },
                    "profiling": profile,
                }
            contexts_written += len(contexts)

            if logger is not None:
                logger.structured(snapshot.run_id, segment.tick, metrics)
                logger.profiling(snapshot.run_id, segment.tick, profile)

        traces = []
        for event in segment.events:
            events_written += 1

            if event.type == _DECISION_TYPE:
                trace = build_decision_trace(
                    snapshot.run_id, segment.tick, event,
                    engine_snapshot or _snapshot_for_engines(
                        segment, window_events, window_history, event_window
                    ),
                )
                traces.append(trace)
                decision_traces_written += 1
                if logger is not None:
                    logger.decision(trace)

        metrics_written += store.append_tick_bundle(
            tick_record, metrics, contexts, events, traces, measured
        )
        ticks_written += 1
        last_tick_seen = segment.tick
        last_segment = segment

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

    # C2 — le dernier tick du flux porte **toujours** son contexte ``agents`` :
    # les lectures « le plus récent disponible » (``/beliefs``,
    # ``/relationships``, analyse causale) ne reculent jamais d'une cadence
    # quand le flux s'arrête sur un tick hors cadence.
    if run_known and run_id is not None and not last_agents_written and last_segment is not None:
        store.append_tick_context(
            run_id,
            last_segment.tick,
            "agents",
            _agents_of(last_segment.snapshot),
        )
        contexts_written += 1

    if ticks_written and run_id is not None:
        _close_run(run_id)

    return ConsumeResult(
        ticks_written,
        events_written,
        agents_written,
        metrics_written,
        contexts_written,
        decision_traces_written,
        gaps_detected,
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
