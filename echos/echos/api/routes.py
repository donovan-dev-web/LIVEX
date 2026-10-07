"""Routes REST ECHOS (ECHOS-040→044, API_REST.md).

:func:`register_routes` attache les endpoints d'analyse à un
``AnalyticsStore`` (SQLite). Si ``store`` est ``None`` (API démarrée sans base
d'analyse, variable ``ECHOS_ANALYTICS_DB``), les endpoints de données
répondent **503** — le contrat reste découvert (OpenAPI) et testé.

Conventions de déterminisme (ECHOS-027/045, exports reproductibles) :
- aucun horodatage d'émission dans les réponses/export ;
- séries triées par tick, ordres stables par identifiant ;
- sous-échantillonnage ``?every=N`` index-based (``ticks[::every]``) ;
- séries servies par le cache ``SeriesCache`` invalidé sur version du magasin.
"""

from __future__ import annotations

import csv
import io
import logging
import os
import re
import threading
import time
from urllib.parse import urlparse

from fastapi import Body, FastAPI, HTTPException, Query
from pydantic import BaseModel, ConfigDict, Field, field_validator

from echos.analysis import reproducibility
from echos.analysis.catalog import catalog
from echos.analysis.causal import MAX_DEPTH, build_chain, CausalError
from echos.analysis.headless import (
    AnalysisInputError,
    analyze_experiment,
    analyze_run,
    generate_experiment_report,
)
from echos.ingestion import ControlClient, ControlError
from echos.ingestion.batch import IngestError, ingest_run_stream
from echos.ingestion.ws_client import WsClient
from echos.storage.pipeline import consume
from echos.storage.sqlite import AnalyticsStore

from .causal_cache import CausalCache
from .series import SeriesCache

_LOGGER = logging.getLogger("echos.api.live")

_VALID_FORMATS = ("json", "csv")

_CSV_HEADER = ("run_id", "tick", "engine", "metric", "value")
_IDENTIFIER = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
_MAX_MISSING_TICKS = 500
"""Nombre maximal de ticks manquants détaillés dans la réponse (garde-poids)."""

_EXTINCTION_CHRONOLOGY = 50
"""Nombre maximal d'événements publiés avant l'extinction (bornage d'affichage)."""

_MAX_COMPARE_RUNS = 12
"""Nombre maximal de runs comparés en une synthèse (bornage de calcul)."""

_MAX_EVENT_ROWS = 2000
"""Nombre maximal d'événements détaillés par réponse (bornage de lecture)."""

_DEFAULT_EVENT_ROWS = 500
"""Valeur par défaut de ``limit`` sur ``/api/runs/{id}/events``."""


class _HeadlessRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    experimentId: str = Field(min_length=1)

    @field_validator("experimentId")
    @classmethod
    def validate_experiment_id(cls, value: str) -> str:
        if not _IDENTIFIER.fullmatch(value):
            raise ValueError("experimentId must be a safe identifier")
        return value


class _AnalyzeRunRequest(_HeadlessRequest):
    runId: str = Field(min_length=1)
    runPath: str = Field(min_length=1)

    @field_validator("runId")
    @classmethod
    def validate_run_id(cls, value: str) -> str:
        if not _IDENTIFIER.fullmatch(value):
            raise ValueError("runId must be a safe identifier")
        return value


class _ExperimentRequest(_HeadlessRequest):
    experimentPath: str = Field(min_length=1)


class _IngestRunRequest(BaseModel):
    """Demande d'enregistrement d'un run batch archivé.

    ``runId`` est optionnel : le flux exporté par SYNE porte lui-même son
    identité (``--run-id``), qui fait autorité. Quand il est fourni, il doit
    correspondre — une divergence signifie que le Launcher et le fichier ne
    parlent pas du même run, et l'ingestion est refusée.
    """

    model_config = ConfigDict(extra="forbid")

    runPath: str = Field(min_length=1)
    runId: str | None = Field(default=None, min_length=1)

    @field_validator("runId")
    @classmethod
    def validate_run_id(cls, value: str | None) -> str | None:
        if value is not None and not _IDENTIFIER.fullmatch(value):
            raise ValueError("runId must be a safe identifier")
        return value


class _IngestLiveRequest(BaseModel):
    """Demande de consommation du flux WebSocket live SYNE (temps réel).

    Sans ce chemin, un run piloté par le Launcher n'apparaît dans
    ``/api/runs`` qu'à la fin de la campagne (ingestion d'archive) : aucune
    analyse temps réel n'est possible pendant l'exécution.
    """

    model_config = ConfigDict(extra="forbid")

    wsUrl: str = Field(min_length=1)


def _headless(operation, *args):
    try:
        return operation(*args)
    except AnalysisInputError as exc:
        raise HTTPException(status_code=exc.status_code, detail=str(exc)) from exc


def _require_store(store: AnalyticsStore | None) -> AnalyticsStore:
    if store is None:
        raise HTTPException(
            status_code=503,
            detail="base d'analyse non configurée (variable ECHOS_ANALYTICS_DB)",
        )
    return store


def _resolve_run(store: AnalyticsStore, run_id: str | None) -> str:
    """Sélectionne un run (id explicite ou run le plus récent).

    « Plus récent » = dernier tick le plus avancé, puis identifiant le **plus
    petit** (déterminisme si égalité). 404 si inconnu ou aucun run.

    Régression : le tri portait sur ``(last_tick, run_id)`` via ``max``, ce qui
    choisissait le **plus grand** identifiant à tick égal, à l'inverse de ce
    qu'annonçait la docstring et de ce que le test attendait.
    """
    runs = store.runs()
    if run_id is not None:
        if not any(run["run_id"] == run_id for run in runs):
            raise HTTPException(status_code=404, detail=f"run inconnu : {run_id}")
        return run_id
    if not runs:
        raise HTTPException(status_code=404, detail="aucun run enregistré")
    # Tri par identifiant croissant puis ``max`` : à tick égal, ``max`` conserve
    # le **premier** élément rencontré, donc le plus petit identifiant. Un run
    # sans tick (``last_tick`` nul) passe avant tout run réellement observé.
    ordered = sorted(runs, key=lambda run: str(run["run_id"]))
    return max(ordered, key=lambda run: run["last_tick"] or -1)["run_id"]


def _agents_observation(
    store: AnalyticsStore,
    run_id: str,
    tick: int | None = None,
) -> tuple[int, object] | None:
    """Contexte ``agents`` le plus récent contenant **au moins une entité**.

    Le dernier contexte d'un run peut être vide : le pipeline écrit toujours le
    contexte du dernier tick du flux, extinction comprise (population nulle).
    Les vues « entités » (``/beliefs``, ``/relationships``, ``/trust-graph``,
    ``/world``) doivent alors reculer d'une cadence — lecture « le plus récent
    disponible » — plutôt que d'afficher « aucune entité observée » à tort.

    ``tick`` borne la recherche au plus proche demandé (``<= tick``) ; ``None``
    prend le plus récent. ``None`` renvoyé = aucun contexte peuplé disponible.
    """
    observations = store.observations_for(run_id, "agents")
    if tick is not None:
        observations = [item for item in observations if item[0] <= tick]
    for observation in reversed(observations):
        if observation[1]:
            return observation
    return None


def _agents_observation_for(
    store: AnalyticsStore,
    run_id: str,
    agent_id: str,
    tick: int | None = None,
) -> tuple[int, object] | None:
    """Contexte ``agents`` le plus récent **contenant l'entité** demandée.

    Une entité peut disparaître des derniers contextes (mort avant le dernier
    tick échantillonné) sans que ses croyances cessent d'avoir été publiées :
    on recule jusqu'au contexte qui la porte, jamais au-delà du run.
    """
    observations = store.observations_for(run_id, "agents")
    if tick is not None:
        observations = [item for item in observations if item[0] <= tick]
    for observation in reversed(observations):
        agents = observation[1] or []
        if any(str(item.get("id")) == agent_id for item in agents):
            return observation
    return None


def _groups_observation(
    store: AnalyticsStore,
    run_id: str,
    tick: int | None = None,
) -> tuple[int, object] | None:
    """Contexte ``groups`` le plus récent contenant au moins une communauté.

    Même repli que :mod:`_agents_observation` : le dernier contexte d'un run
    éteint est vide, les communautés observées plus tôt restent publiables.
    """
    observations = store.observations_for(run_id, "groups")
    if tick is not None:
        observations = [item for item in observations if item[0] <= tick]
    for observation in reversed(observations):
        if observation[1]:
            return observation
    return None


def _metric_pairs(
    store: AnalyticsStore,
    run_id: str,
    engine: str | None,
    metric: str | None,
) -> list[tuple[str, str]]:
    """Couples (moteur, métrique) du run entier, filtrés, en ordre stable.

    ``SELECT DISTINCT`` sur ``tick_metrics`` : la nommenclature découverte est
    celle de la série disponible, pas celle du dernier instant.
    """
    return [
        (str(eng), str(met))
        for eng, met in store.metric_names(run_id, engine=engine, metric=metric)
    ]


def _downsample(items: list, every: int) -> list:
    """Sous-échantillonnage index-based (``items[::every]``), ``every >= 1``."""
    return items if every <= 1 else items[::every]


def _metadata(store: AnalyticsStore, run: dict) -> dict:
    """Métadonnées d'un run, enrichies du résultat de population (A3).

    ``outcome``/``extinction_tick`` permettent à un observateur (UI, campagne)
    de draper « run fini avec écosystème mort » sans recalculer lui-même le
    résultat depuis les séries. ``unknown``/``null`` sur un run sans tick.

    ``conservation`` (P1) est le niveau de conservation **réellement configuré**
    pour ce run, persisté par le pipeline : il permet d'annoncer la fidélité
    (replay échantillonné ou haute fidélité) avant toute lecture, sans
    l'inventer côté interface.
    """
    outcome = store.population_outcome(run["run_id"])
    conservation = store.latest_context(run["run_id"], "conservation")
    return {
        "run_id": run["run_id"],
        "version": run["version"],
        "seed": run["seed"],
        "ticks_count": run["ticks_count"],
        "first_tick": run["first_tick"],
        "last_tick": run["last_tick"],
        "outcome": outcome["outcome"],
        "extinction_tick": outcome["extinction_tick"],
        "conservation": conservation[1] if conservation else None,
    }


def _read_every(every: int | None) -> int:
    value = 1 if every is None else int(every)
    if value < 1:
        raise HTTPException(status_code=400, detail="every doit être >= 1")
    return value


def _extinction_chronology(
    store: AnalyticsStore, run_id: str, extinction_tick: int | None
) -> list[dict]:
    """Dernières observations avant l'extinction (P3 — RAPPORT §7.3).

    Une **chronologie descriptive** : tick, type d'événement, agent, action et
    cause telles que transportées par SYNE. Aucune inférence causale n'est
    produite ici — la cause publiée est celle du moteur, les facteurs associés
    restent des observations à mettre côte à côte.
    """
    if extinction_tick is None:
        return []
    rows = [
        row
        for row in store.events(run_id)
        if int(row[0]) <= extinction_tick
    ][-_EXTINCTION_CHRONOLOGY:]
    return [
        {
            "tick": int(tick),
            "type": str(event_type),
            "agent_id": agent_id,
            "action": action,
            "cause": cause,
        }
        for tick, event_type, agent_id, action, cause, _value in rows
    ]


def register_routes(app: FastAPI, store: AnalyticsStore | None) -> None:
    """Attache les endpoints ECHOS et le relais local de contrôle SYNE."""
    control_base_url = os.environ.get(
        "SYNE_CONTROL_URL", "http://127.0.0.1:5181"
    )
    cache = SeriesCache()
    causal_cache = CausalCache()

    @app.get("/api/control/status", tags=["control"])
    def control_status() -> dict:
        """Relaye l'état du serveur SYNE sans exposer son port au navigateur."""
        try:
            with ControlClient(base_url=control_base_url) as control:
                return control.status()
        except ControlError as exc:
            _raise_control_error(exc)

    @app.post("/api/control/{action}", tags=["control"])
    def control_command(
        action: str, payload: dict | None = Body(default=None)
    ) -> dict:
        """Relaye une commande locale au contrat HTTP SYNE :5181."""
        if action not in {"start", "pause", "resume", "stop", "reset"}:
            raise HTTPException(status_code=404, detail="commande de contrôle inconnue")
        options = payload or {}
        try:
            with ControlClient(base_url=control_base_url) as control:
                if action == "start":
                    return control.start(
                        seed=options.get("seed"),
                        config=options.get("config"),
                        max_ticks=options.get("maxTicks"),
                    )
                if action == "pause":
                    return control.pause()
                if action == "resume":
                    return control.resume()
                if action == "stop":
                    return control.stop()
                return control.reset(
                    seed=options.get("seed"),
                    run_id=options.get("runId"),
                    max_ticks=options.get("maxTicks"),
                )
        except ControlError as exc:
            _raise_control_error(exc)

    @app.post("/analysis/run", tags=["analysis"])
    def headless_analyze_run(request: _AnalyzeRunRequest) -> dict:
        """Return reproducible analysis artifacts for a stored run."""
        active = _require_store(store)
        files = _headless(
            analyze_run,
            active,
            request.experimentId,
            request.runId,
            request.runPath,
        )
        return {"files": files}

    @app.post("/analysis/experiment", tags=["analysis"])
    def headless_analyze_experiment(request: _ExperimentRequest) -> dict:
        """Aggregate the explicit run set in an experiment manifest."""
        active = _require_store(store)
        return {
            "files": [
                _headless(
                    analyze_experiment,
                    active,
                    request.experimentId,
                    request.experimentPath,
                )
            ]
        }

    @app.post("/analysis/report", tags=["analysis"])
    def headless_generate_report(request: _ExperimentRequest) -> dict:
        """Generate the deterministic Markdown report for an experiment."""
        active = _require_store(store)
        return {
            "report": _headless(
                generate_experiment_report,
                active,
                request.experimentId,
                request.experimentPath,
            )
        }

    @app.post("/ingest/run", tags=["ingestion"])
    def ingest_archived_run(request: _IngestRunRequest) -> dict:
        """Record an archived SYNE batch stream into the analytics store.

        Second path to the store, complementing the live :5180 stream: the run
        directory written by a supervised batch and archived in a ``.livexp``
        package. Analysis then works without SYNE running.
        """
        active = _require_store(store)
        try:
            return {"ingested": ingest_run_stream(
                active,
                request.runPath,
                expected_run_id=request.runId,
            )}
        except IngestError as exc:
            raise HTTPException(status_code=exc.status_code, detail=str(exc)) from exc

    live_state: dict[str, object] = {"thread": None, "url": None}
    live_lock = threading.Lock()

    @app.post("/ingest/live", tags=["ingestion"])
    def ingest_live(request: _IngestLiveRequest) -> dict:
        """Consomme le flux WebSocket live SYNE en tâche de fond (temps réel).

        Le Launcher ouvre ``--observe-port`` sur le run supervisé ; sans
        consommateur, le run n'apparaît dans ``/api/runs`` qu'à la fin de la
        campagne et la fenêtre d'analyse ne peut rien montrer pendant
        l'exécution. Un seul consommateur à la fois : la même adresse est un
        no-op, une autre adresse pendant qu'un consommateur tourne est un 409.

        Échec, rejet ou interruption du flux n'est jamais fatal (ECHOS reste
        observe-only) : le run reste enregistré jusqu'au tick atteint.
        """
        active = _require_store(store)
        url = request.wsUrl.strip()
        parsed = urlparse(url)
        if parsed.scheme not in {"ws", "wss"} or parsed.hostname not in {
            "127.0.0.1",
            "localhost",
            "::1",
        }:
            raise HTTPException(
                status_code=400,
                detail="wsUrl doit être un WebSocket local (127.0.0.1)",
            )

        with live_lock:
            current = live_state.get("thread")
            if isinstance(current, threading.Thread) and current.is_alive():
                if live_state.get("url") == url:
                    return {
                        "started": False,
                        "wsUrl": url,
                        "reason": "consommateur déjà en cours sur cette adresse",
                    }
                raise HTTPException(
                    status_code=409,
                    detail="un consommateur de flux live est déjà en cours",
                )

            def _run() -> None:
                client = WsClient()
                try:
                    # Le port d'observation du run s'ouvre après le lancement du
                    # moteur : on réessaie brièvement (10 s) avant d'abandonner,
                    # sinon la course départ/connexion perdrait le run entier.
                    for attempt in range(40):
                        try:
                            client.connect(url)
                            break
                        except Exception:
                            if attempt == 39:
                                raise
                            time.sleep(0.25)
                    result = consume(client, active)
                    _LOGGER.info(
                        "flux live terminé (%s) : %s tick(s), %s événement(s)",
                        url,
                        result.ticks_written,
                        result.events_written,
                    )
                except Exception as exc:  # noqa: BLE001 — observateur jamais fatal
                    _LOGGER.warning("consommation du flux live interrompue (%s): %s", url, exc)
                finally:
                    client.close()

            thread = threading.Thread(
                target=_run, name="echos-live-ingest", daemon=True
            )
            live_state["thread"] = thread
            live_state["url"] = url
            thread.start()

        return {"started": True, "wsUrl": url}

    @app.get("/api/runs", tags=["api"])
    def list_runs() -> dict:
        active = _require_store(store)
        return {"runs": [_metadata(active, run) for run in active.runs()]}

    @app.get("/api/runs/{run_id}", tags=["api"])
    def run_full(run_id: str) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        runs = {run["run_id"]: run for run in active.runs()}
        run = runs[resolved]
        # C1 — seuls les phénomènes sont consommés ici : charger tous les
        # contextes (dont ``agents`` ≈ 208 Ko/tick) provoquait des timeouts
        # > 20 s, voire des OOM-kills, sur un run complet.
        observations = active.observations_for(resolved, "phenomena")
        detected: dict[str, dict] = {}
        disclaimer = ""
        first_tick = -1
        last_tick = -1
        for tick, payload in observations:
            first_tick = tick if first_tick < 0 else min(first_tick, tick)
            last_tick = max(last_tick, tick)
            disclaimer = payload.get("disclaimer") or disclaimer
            for phenomenon in payload.get("detected") or []:
                identifier = phenomenon.get("identifier")
                if not identifier:
                    continue
                item = detected.setdefault(
                    identifier,
                    {
                        **phenomenon,
                        "firstTick": tick,
                        "lastTick": tick,
                        "occurrences": 0,
                    },
                )
                item["firstTick"] = min(item["firstTick"], tick)
                item["lastTick"] = max(item["lastTick"], tick)
                item["occurrences"] += 1
        return {
            **_metadata(active, run),
            "metrics": active.latest_metrics(resolved),
            "measured": active.latest_measured(resolved),
            "phenomena": {
                "detected": sorted(detected.values(), key=lambda item: item["identifier"]),
                "disclaimer": disclaimer,
                "first_tick": first_tick,
                "last_tick": last_tick,
            },
        }

    @app.get("/api/runs/{run_id}/calibration", tags=["api"])
    def run_calibration(run_id: str) -> dict:
        """Read the deterministic post-run calibration evidence (SYNE-131)."""
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        report = active.calibration_report(resolved)
        if report is None:
            raise HTTPException(status_code=404, detail="rapport de calibration indisponible")
        return report

    @app.get("/api/runs/{run_id}/metrics", tags=["api"])
    def run_metrics(
        run_id: str,
        engine: str | None = Query(default=None),
        metric: str | None = Query(default=None),
        every: int | None = Query(default=None, ge=1),
    ) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        step = _read_every(every if every is not None else 1)
        latest = active.latest_metrics(resolved)

        # Le jeu de couples (moteur, métrique) est découvert sur **tout** le
        # run, pas seulement sur le dernier tick : une métrique présente à un
        # ancien tick et disparue du dernier (ex. ``GroupFormationRate`` sur un
        # tick sans événement) était invisible alors que sa série existait.
        pairs = _metric_pairs(active, resolved, engine, metric)

        # Séries **et** provenance par tick, lues ensemble : elles décrivent le
        # même ensemble de ticks, ce qui permet d'aligner masque et valeurs
        # (P0 — l'ancien contrat ne publiait que les drapeaux du dernier tick).
        values_by_tick: dict[tuple[str, str], dict[int, float]] = {}
        flags_by_tick: dict[tuple[str, str], dict[int, bool]] = {}
        observed: set[int] = set()
        for eng, met in pairs:
            raw_values = cache.series(active, resolved, eng, met)
            raw_flags = cache.provenance(active, resolved, eng, met)
            serie = {int(tick): float(value) for tick, value in raw_values}
            flags = {int(tick): bool(flag) for tick, flag in raw_flags}
            values_by_tick[(eng, met)] = serie
            flags_by_tick[(eng, met)] = flags
            observed.update(serie)

        all_ticks = sorted(observed)
        out_ticks = _downsample(all_ticks, step)

        # Alignement strict sur ``ticks`` : un tick sans observation produit
        # ``null`` (trou visible), jamais une valeur décalée. Les tableaux ont
        # tous la même longueur que ``ticks``, ce qui rend le masque de
        # provenance lisible point par point.
        values: dict[str, dict[str, list[float | None]]] = {}
        measured_by_tick: dict[str, dict[str, list[bool | None]]] = {}
        for (eng, met), serie in values_by_tick.items():
            values.setdefault(eng, {})[met] = [
                serie.get(tick) if tick in serie else None for tick in out_ticks
            ]
            flags = flags_by_tick[(eng, met)]
            measured_by_tick.setdefault(eng, {})[met] = [
                flags.get(tick) if tick in flags else None for tick in out_ticks
            ]

        # Ticks absents entre le premier et le dernier observé : les lacunes
        # sont publiées, jamais comblées (l'axe est en ticks réels côté UI).
        missing = (
            [tick for tick in range(all_ticks[0], all_ticks[-1] + 1) if tick not in observed]
            if all_ticks
            else []
        )

        return {
            "run_id": resolved,
            "engine": engine,
            "metric": metric,
            "every": step,
            "ticks": out_ticks,
            "values": values,
            # Provenance alignée par tick : ``null`` = tick sans observation,
            # ``false`` = repli neutre (jamais un zéro observé), ``true`` = mesuré.
            "measured_by_tick": measured_by_tick,
            "missing_ticks": missing[:_MAX_MISSING_TICKS],
            "missing_ticks_count": len(missing),
            "latest": latest,
            # Provenance du **dernier** tick seulement (contrat historique,
            # conservé pour compatibilité — utiliser ``measured_by_tick``).
            "measured": active.latest_measured(resolved),
            "latest_tick": all_ticks[-1] if all_ticks else None,
        }

    @app.get("/api/experiments/summary", tags=["api"])
    def experiments_summary(
        runs: str = Query(..., description="identifiants de runs, séparés par des virgules"),
        engine: str | None = Query(default=None),
        metric: str | None = Query(default=None),
    ) -> dict:
        """Synthèse multi-runs (P3 — RAPPORT §4.7, §6.1.8).

        Compare plusieurs runs **avec leur contexte de contrôle** (version,
        graine, issue, niveau de conservation) et publie une dispersion
        descriptive par métrique : valeur au dernier tick observé de chaque
        run, min/max/écart observé et nombre de runs où la mesure existe.

        Choix explicites :

        - comparaison à un **instant propre à chaque run** (dernier tick) :
          deux runs de longueurs différentes ne partagent pas nécessairement le
          même tick — les métadonnées le disent ;
        - agrégats calculés **ici**, avec leurs dénominateurs (``runs``), jamais
          côté interface ;
        - un écart entre runs n'est pas un effet : il faut des runs répétés et
          des conditions comparables pour conclure.
        """
        active = _require_store(store)
        requested = [item.strip() for item in runs.split(",") if item.strip()]
        if len(requested) < 2:
            raise HTTPException(
                status_code=400, detail="au moins deux runs sont nécessaires pour comparer"
            )
        if len(requested) > _MAX_COMPARE_RUNS:
            raise HTTPException(
                status_code=400,
                detail=f"maximum {_MAX_COMPARE_RUNS} runs par comparaison",
            )

        resolved_ids = [_resolve_run(active, run_id) for run_id in requested]
        runs_map = {run["run_id"]: run for run in active.runs()}

        metadatas = [_metadata(active, runs_map[run_id]) for run_id in resolved_ids]
        latest_values = {run_id: active.latest_metrics(run_id) for run_id in resolved_ids}
        latest_flags = {run_id: active.latest_measured(run_id) for run_id in resolved_ids}

        pairs = sorted({
            (str(eng), str(met))
            for run_id in resolved_ids
            for eng, met in active.metric_names(run_id, engine=engine, metric=metric)
        })

        metrics: dict[str, dict[str, dict]] = {}
        for eng, met in pairs:
            values = {
                run_id: latest_values[run_id].get(eng, {}).get(met)
                for run_id in resolved_ids
            }
            measured_in = [
                run_id
                for run_id in resolved_ids
                if values[run_id] is not None
                and latest_flags[run_id].get(eng, {}).get(met, True)
            ]
            present = [value for value in values.values() if value is not None]
            metrics.setdefault(eng, {})[met] = {
                "values": values,
                "runsObserved": len(present),
                "runsMeasured": len(measured_in),
                "min": min(present) if present else None,
                "max": max(present) if present else None,
                "mean": (sum(present) / len(present)) if present else None,
                "spread": (max(present) - min(present)) if present else None,
            }

        return {
            "run_ids": resolved_ids,
            "runs": metadatas,
            "metrics": metrics,
            "metricCount": len(pairs),
            "note": (
                "Valeur au dernier tick observé de chaque run ; dispersion "
                "descriptive (min/max/écart) sans taille d'effet ni test. Un "
                "écart entre runs n'est pas un effet : comparer des conditions "
                "et des graines explicitement contrôlées."
            ),
        }

    @app.get("/api/metrics/catalog", tags=["api"])
    def metrics_catalog() -> dict:
        """Catalogue versionné des métriques (P1 — RAPPORT §8).

        Définitions, unités, domaines, populations, fenêtres, statuts, avertissements
        et renommages : le Launcher **restaure** ce contenu, il ne redéfinit rien.
        """
        return catalog()

    @app.get("/api/runs/{run_id}/viability", tags=["api"])
    def run_viability(
        run_id: str,
        every: int | None = Query(default=None, ge=1),
    ) -> dict:
        """Profil de viabilité d'un run (P1/P3 — RAPPORT §7.3).

        Résumés de tick déjà conservés (population, besoins, régime des
        ressources) + rapport de calibration post-run, **sans agrégat
        synthétique** : chaque dimension est publiée avec ses valeurs sources.
        Aucune projection : « aucune extinction observée jusqu'au tick N » est
        un fait, la stabilité n'est jamais déclarée.
        """
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        step = _read_every(every if every is not None else 1)

        rows = sorted(active.tick_summaries(resolved), key=lambda row: int(row[1]))
        kept = _downsample(rows, step)
        runs = {run["run_id"]: run for run in active.runs()}
        metadata = _metadata(active, runs[resolved])

        def series(column: int) -> list[list[float]] | None:
            """Série ``(tick, valeur)`` si la colonne existe (schéma ≥ v6)."""
            if any(len(row) <= column or row[column] is None for row in kept):
                return None
            return [[int(row[1]), float(row[column])] for row in kept]

        alive = [int(row[3]) for row in rows]
        extinction_tick = next(
            (int(row[1]) for row in rows if int(row[3]) == 0), None
        )
        conservation = active.latest_context(resolved, "conservation")
        report = active.calibration_report(resolved)
        ticks_present = {int(row[1]) for row in rows}
        missing = (
            [
                tick
                for tick in range(int(rows[0][1]), int(rows[-1][1]) + 1)
                if tick not in ticks_present
            ]
            if rows
            else []
        )

        return {
            **metadata,
            "every": step,
            "population": {
                "initial": int(rows[0][4]) if rows else None,
                "final": alive[-1] if alive else None,
                "minimumAlive": min(alive) if alive else None,
                "extinctionTick": extinction_tick,
                "series": series(3),
            },
            "needs": {
                "energy": series(5),
                "hunger": series(6),
                "thirst": series(7),
                "fatigue": series(8),
            },
            "resources": {"food": series(10), "water": series(11)},
            "decisions": series(9),
            "completeness": {
                "ticksObserved": len(rows),
                "missingTickCount": len(missing),
                "missingTicks": missing[:_MAX_MISSING_TICKS],
                "conservation": conservation[1] if conservation else None,
                "calibrationAvailable": report is not None,
            },
            # Rapport post-run (viabilité, pente d'énergie, actions sous faim,
            # régime des ressources) — séparé du temps réel par construction.
            "calibration": report,
            "viability": (report or {}).get("viability"),
            # Chronologie descriptive avant l'extinction (P3) : observations,
            # jamais une cause racine inventée.
            "extinctionChronology": _extinction_chronology(
                active, resolved, extinction_tick
            ),
        }

    @app.get("/api/runs/{run_id}/events", tags=["api"])
    def run_events(
        run_id: str,
        event_type: str | None = Query(
            default=None, alias="type", description="filtre sur le type d'événement"
        ),
        limit: int = Query(default=_DEFAULT_EVENT_ROWS, ge=1, le=_MAX_EVENT_ROWS),
    ) -> dict:
        """Journal d'événements publié (P3 — annotations de la vue temporelle).

        Lecture du journal déjà persisté, **bornée** : au plus ``limit``
        événements détaillés (plafond ``_MAX_EVENT_ROWS``), ``total`` donnant le
        nombre réel de lignes du filtre, et ``types`` listant les types
        disponibles avec leur comptage. L'ordre est celui du journal
        ``(tick, ordre d'émission)`` — déterministe pour une base donnée.

        Aucune interprétation : le service transporte ``type``, entité, action
        et cause **telles que SYNE les a émises** ; jamais il ne déduit une
        cause racine ni ne priorise un événement.
        """
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        rows = active.events(resolved)
        if event_type is not None:
            rows = [row for row in rows if str(row[1]) == event_type]

        counts: dict[str, int] = {}
        for row in rows:
            key = str(row[1])
            counts[key] = counts.get(key, 0) + 1

        return {
            "run_id": resolved,
            "total": len(rows),
            "limit": limit,
            "types": [
                {"type": name, "count": counts[name]} for name in sorted(counts)
            ],
            "events": [
                {
                    "tick": int(tick),
                    "type": str(event_name),
                    "agent_id": agent_id,
                    "action": action,
                    "cause": cause,
                }
                for tick, event_name, agent_id, action, cause, _value in rows[:limit]
            ],
        }

    @app.get("/api/runs/{run_id}/export", tags=["api"])
    def run_export(
        run_id: str,
        format: str = Query(default="json"),
    ) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        if format not in _VALID_FORMATS:
            raise HTTPException(
                status_code=400,
                detail=f"format inconnu : {format} (json|csv)",
            )

        rows = active.metrics_all(resolved)
        if format == "json":
            return {
                "run_id": resolved,
                "rows": [
                    {"tick": tick, "engine": engine, "metric": metric, "value": value}
                    for tick, engine, metric, value in rows
                ],
                "format": "json",
            }

        buffer = io.StringIO()
        writer = csv.writer(buffer, lineterminator="\r\n")
        writer.writerow(_CSV_HEADER)
        for tick, engine, metric, value in rows:
            writer.writerow((resolved, tick, engine, metric, value))
        return {"run_id": resolved, "content_type": "text/csv", "body": buffer.getvalue()}

    @app.get("/api/runs/{run_id}/decisions", tags=["api"])
    def run_decisions(run_id: str) -> dict:
        """Traces de décision du run (schéma ``decision_traces``, ECHOS-051).

        Tri stable ``(tick, agent_id)`` ; l'URL d'export timestamp-free.
        """
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        return {
            "run_id": resolved,
            "decisions": active.decision_traces(resolved),
        }

    @app.get("/api/compare", tags=["api"])
    def compare_runs(
        run_a: str = Query(...),
        run_b: str = Query(...),
        format: str = Query(default="json"),
        light: bool = Query(default=False),
    ) -> dict:
        """Comparaison de runs contrôlés (ECHOS-070→072, EXPERIMENT_COMPARISON.md).

        Méta-métriques de reproductibilité (ECHOS-070/071) : même seed ET même
        version ET contenu bit-à-bit identique ⇒ ``is_reproducible`` ; score de
        reproductibilité ``1.0 - (CognitiveDiff + SocialDiff) / 2`` sinon.
        ``format=csv`` produit l'export comparatif aligné (ECHOS-072).
        ``light=1`` (C3) renvoie le seul ``summary`` — sans séries ni empreinte
        bit-à-bit — : l'empreinte charge séries + événements + contextes +
        traces des deux runs, ce qui est lourd **par conception** ; l'UI
        l'utilise par défaut. Défaut sans ``light`` : comportement complet
        conservé. Réponses déterministes : aucune horodatation d'émission,
        tris stables.
        """
        active = _require_store(store)
        if format not in _VALID_FORMATS:
            raise HTTPException(
                status_code=400,
                detail=f"format inconnu : {format} (json|csv)",
            )
        known = {run["run_id"] for run in active.runs()}
        for run_id in (run_a, run_b):
            if run_id not in known:
                raise HTTPException(status_code=404, detail=f"run inconnu : {run_id}")

        if light and format == "json":
            # ``light`` ne concerne que la réponse JSON : l'export CSV a besoin
            # des séries alignées par construction.
            return {**reproducibility.summary(active, run_a, run_b), "format": "light"}

        summary = reproducibility.compare(active, run_a, run_b)
        if format == "csv":
            buffer = io.StringIO()
            writer = csv.writer(buffer, lineterminator="\r\n")
            writer.writerow(("tick", "engine", "metric", "run_a_value", "run_b_value", "diff"))
            for row in reproducibility.aligned_series(active, run_a, run_b):
                writer.writerow(
                    (
                        row["tick"],
                        row["engine"],
                        row["metric"],
                        row["run_a"],
                        row["run_b"],
                        row["diff"],
                    )
                )
            return {
                "summary": summary,
                "content_type": "text/csv",
                "body": buffer.getvalue(),
            }

        return {
            **summary,
            "series": reproducibility.aligned_series(active, run_a, run_b),
            "format": "json",
        }

    @app.get("/api/runs/{run_id}/causal-chains/{agent_id}", tags=["api"])
    def causal_chains(
        run_id: str,
        agent_id: str,
        tick: int | None = Query(default=None, ge=0),
        depth: int = Query(default=7, ge=1, le=MAX_DEPTH),
    ) -> dict:
        """Chaîne causale d'une entité à un tick (ECHOS-061 → ECHOS-063).

        Reconstruction **hors ligne** sur ``decision_traces`` + ``events_log``
        + contexte ``agents`` (ADR-002 [Accepted], CAUSAL_ANALYSIS.md §4) :
        Action → Intention → Objectif → Besoin → Croyance → Mémoire →
        Perception. ``tick`` optionnel (dernier tick tracé de l'entité) ;
        ``depth`` > ``MAX_DEPTH`` (12) rejeté côté FastAPI (422). Réponse
        servie par ``CausalCache`` invalidé sur ``ingest_version`` (ECHOS-063).
        """
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)

        resolved_tick = tick
        if resolved_tick is None:
            resolved_tick = active.latest_decision_tick(resolved, str(agent_id))
        if resolved_tick is None:
            raise HTTPException(
                status_code=404,
                detail=f"aucune trace de décision pour l'entité {agent_id} sur le run {resolved}",
            )

        try:
            chain = causal_cache.chain(
                active,
                resolved,
                str(agent_id),
                int(resolved_tick),
                int(depth),
                loader=lambda: build_chain(
                    active, resolved, str(agent_id), int(resolved_tick), int(depth)
                ),
            )
        except CausalError as exc:
            raise HTTPException(status_code=404, detail=str(exc))
        return chain  # dict déterministe (tri stable intégré à build_chain)

    @app.get("/api/beliefs/{agent_id}", tags=["api"])
    def beliefs(agent_id: str, run_id: str | None = Query(default=None)) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        # Repli « le plus récent disponible » : le dernier contexte peut être
        # vide (extinction) ou ne plus contenir l'entité (elle est morte avant
        # le dernier tick échantillonné) — on recule d'une cadence.
        observation = _agents_observation_for(active, resolved, agent_id)
        if observation is None:
            if active.latest_context(resolved, "agents") is None:
                raise HTTPException(status_code=404, detail="aucune observation d'agents")
            raise HTTPException(
                status_code=404,
                detail=(
                    f"entité introuvable dans les contextes agents du run "
                    f"{resolved} : {agent_id}"
                ),
            )
        tick, agents = observation
        agent = next(
            (item for item in agents if str(item.get("id")) == agent_id), None
        )
        if agent is None:  # invariant : le contexte choisi contient l'entité
            raise HTTPException(
                status_code=404,
                detail=f"entité introuvable : {agent_id}",
            )
        return {
            "agent_id": agent_id,
            "run_id": resolved,
            "tick": tick,
            "beliefs": agent.get("beliefs") or [],
        }

    @app.get("/api/relationships/{agent_id}", tags=["api"])
    def relationships(
        agent_id: str, run_id: str | None = Query(default=None)
    ) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        # Même repli que /beliefs : dernier contexte **contenant l'entité**.
        observation = _agents_observation_for(active, resolved, agent_id)
        if observation is None:
            if active.latest_context(resolved, "agents") is None:
                raise HTTPException(status_code=404, detail="aucune observation d'agents")
            raise HTTPException(
                status_code=404,
                detail=(
                    f"entité introuvable dans les contextes agents du run "
                    f"{resolved} : {agent_id}"
                ),
            )
        tick, agents = observation
        agent = next(
            (item for item in agents if str(item.get("id")) == agent_id), None
        )
        if agent is None:  # invariant : le contexte choisi contient l'entité
            raise HTTPException(
                status_code=404,
                detail=f"entité introuvable : {agent_id}",
            )
        trust = agent.get("trust") or []
        return {
            "agent_id": agent_id,
            "run_id": resolved,
            "tick": tick,
            "trust": trust,
            "count": len(trust),
        }

    @app.get("/api/world", tags=["api"])
    def world_view(
        run_id: str | None = Query(default=None),
        tick: int | None = Query(default=None, ge=0),
    ) -> dict:
        """Description de monde + observation au tick demandé (vue 2D).

        Lecture seule (ADR-003) : ``world`` vient du contexte ``world`` écrit à
        l'ingestion depuis ``world_initialized`` ; ``agents``/``groups``/
        ``resources`` sont les contextes les plus proches **au plus fort** du
        tick demandé, publiés tels qu'observés. Aucun agrégat n'est calculé.
        """
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        observation = active.latest_context(resolved, "agents")
        effective = tick if tick is not None else (observation[0] if observation else -1)
        world_ctx = None
        if effective >= 0:
            world_ctx = active.context_before(resolved, "world", effective)
        if world_ctx is None:
            world_ctx = active.latest_context(resolved, "world")
        # Repli « le plus récent disponible » : le contexte du tick demandé peut
        # être vide (extinction, cadence d'échantillonnage) — on recule d'une
        # cadence plutôt que d'afficher un monde sans entité à tort.
        agents = (
            _agents_observation(active, resolved, effective)
            if effective >= 0
            else None
        )
        group_ctx = (
            _groups_observation(active, resolved, effective)
            if effective >= 0
            else None
        )
        resource_ctx = (
            active.context_before(resolved, "resources", effective)
            if effective >= 0
            else None
        )
        return {
            "run_id": resolved,
            "tick": agents[0] if agents else -1,
            "world_tick": world_ctx[0] if world_ctx else -1,
            "world": world_ctx[1] if world_ctx else None,
            "agents": agents[1] if agents else [],
            "groups": group_ctx[1] if group_ctx else [],
            "resources": resource_ctx[1] if resource_ctx else [],
        }

    @app.get("/api/trust-graph", tags=["api"])
    def trust_graph(
        run_id: str | None = Query(default=None),
        tick: int | None = Query(default=None, ge=0),
    ) -> dict:
        """Graphe de confiance observé au tick demandé.

        Nœuds = entités du contexte ``agents`` ; arêtes = relations ``trust``
        que chaque entité publie (``peerId``/``trust``), transmises telles quelles.
        Seule la **forme** du graphe est assemblée ici : aucune valeur n'est
        agrégée, moyennée ou estimée (ADR-003 : ECHOS calcule, le Launcher présente).
        """
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        # Repli « le plus récent disponible » : le dernier contexte ``agents``
        # d'un run éteint est vide (population nulle) — les entités observées
        # plus tôt restent publiables, avec leur tick d'observation exact.
        observation = _agents_observation(
            active, resolved, None if tick is None else tick
        )
        if observation is None:
            return {"run_id": resolved, "tick": -1, "nodes": [], "edges": []}
        effective, agents = observation

        group_ctx = _groups_observation(active, resolved, effective)
        label_of: dict[str, object] = {}
        if group_ctx is not None:
            for group in group_ctx[1] or []:
                for member in group.get("members") or []:
                    label_of[str(member)] = group.get("label")

        nodes: list[dict] = []
        edges: list[dict] = []
        for agent in agents or []:
            agent_id = str(agent.get("id"))
            position = agent.get("position") or {}
            nodes.append({
                "id": agent_id,
                "x": position.get("x"),
                "y": position.get("y"),
                "energy": agent.get("energy"),
                "hunger": agent.get("hunger"),
                "thirst": agent.get("thirst"),
                "action": agent.get("currentAction"),
                "group": label_of.get(agent_id),
            })
            for relation in agent.get("trust") or []:
                peer = relation.get("peerId")
                if peer is None:
                    continue
                edges.append({
                    "source": agent_id,
                    "target": str(peer),
                    "weight": relation.get("trust"),
                })
        return {"run_id": resolved, "tick": effective, "nodes": nodes, "edges": edges}

    @app.get("/api/groups", tags=["api"])
    def groups(run_id: str | None = Query(default=None)) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        # Repli : le dernier contexte d'un run éteint ne porte plus aucune
        # communauté ; les plus récentes observées restent publiées, avec leur
        # tick exact. À défaut de contexte peuplé, le plus récent (vide) fait foi.
        observation = _groups_observation(active, resolved)
        if observation is None:
            fallback = active.latest_context(resolved, "groups")
            if fallback is None:
                return {"run_id": resolved, "tick": -1, "groups": []}
            observation = fallback
        tick, group_list = observation
        return {"run_id": resolved, "tick": tick, "groups": group_list}

    @app.get("/api/emergent-phenomena", tags=["api"])
    def emergent_phenomena(run_id: str | None = Query(default=None)) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        # C1 — lecture ciblée (voir run_full) : fin des timeouts/OOM sur run dense.
        observations = active.observations_for(resolved, "phenomena")
        if not observations:
            return {
                "run_id": resolved,
                "tick": -1,
                "phenomena": [],
                "disclaimer": "",
            }
        detected: dict[str, dict] = {}
        disclaimer = ""
        for tick, payload in observations:
            disclaimer = payload.get("disclaimer") or disclaimer
            for phenomenon in payload.get("detected") or []:
                identifier = phenomenon.get("identifier")
                if not identifier:
                    continue
                item = detected.setdefault(
                    identifier,
                    {**phenomenon, "firstTick": tick, "lastTick": tick, "occurrences": 0},
                )
                item["firstTick"] = min(item["firstTick"], tick)
                item["lastTick"] = max(item["lastTick"], tick)
                item["occurrences"] += 1
        return {
            "run_id": resolved,
            "tick": observations[-1][0],
            "phenomena": sorted(detected.values(), key=lambda item: item["identifier"]),
            "disclaimer": disclaimer,
        }

    app.state.series_cache = cache


def _raise_control_error(error: ControlError) -> None:
    """Traduit les indisponibilités SYNE en erreurs explicites côté ECHOS."""
    if error.status == "transport":
        raise HTTPException(
            status_code=503,
            detail=(
                "serveur de contrôle SYNE indisponible ; démarrez SYNE avec --serve "
                "(port 5181)"
            ),
        ) from error
    raise HTTPException(
        status_code=int(error.status), detail=error.detail or "commande SYNE refusée"
    ) from error
