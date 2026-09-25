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
import os

from fastapi import Body, FastAPI, HTTPException, Query

from echos.analysis import reproducibility
from echos.analysis.causal import MAX_DEPTH, build_chain, CausalError
from echos.ingestion import ControlClient, ControlError
from echos.storage.sqlite import AnalyticsStore

from .causal_cache import CausalCache
from .series import SeriesCache

_VALID_FORMATS = ("json", "csv")

_CSV_HEADER = ("run_id", "tick", "engine", "metric", "value")


def _require_store(store: AnalyticsStore | None) -> AnalyticsStore:
    if store is None:
        raise HTTPException(
            status_code=503,
            detail="base d'analyse non configurée (variable ECHOS_ANALYTICS_DB)",
        )
    return store


def _resolve_run(store: AnalyticsStore, run_id: str | None) -> str:
    """Sélectionne un run (id explicite ou run le plus récent).

    « Plus récent » = dernière métrique la plus avancée puis identifiant le
    plus petit (déterminisme si égalité). 404 si inconnu ou aucun run.
    """
    runs = store.runs()
    if run_id is not None:
        if not any(run["run_id"] == run_id for run in runs):
            raise HTTPException(status_code=404, detail=f"run inconnu : {run_id}")
        return run_id
    if not runs:
        raise HTTPException(status_code=404, detail="aucun run enregistré")
    return max(runs, key=lambda run: (run["last_tick"] or -1, run["run_id"]))[
        "run_id"
    ]


def _downsample(items: list, every: int) -> list:
    """Sous-échantillonnage index-based (``items[::every]``), ``every >= 1``."""
    return items if every <= 1 else items[::every]


def _metadata(run: dict) -> dict:
    return {
        "run_id": run["run_id"],
        "version": run["version"],
        "seed": run["seed"],
        "ticks_count": run["ticks_count"],
        "first_tick": run["first_tick"],
        "last_tick": run["last_tick"],
    }


def _read_every(every: int | None) -> int:
    value = 1 if every is None else int(every)
    if value < 1:
        raise HTTPException(status_code=400, detail="every doit être >= 1")
    return value


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

    @app.get("/api/runs", tags=["api"])
    def list_runs() -> dict:
        _require_store(store)
        return {"runs": [_metadata(run) for run in store.runs()]}

    @app.get("/api/runs/{run_id}", tags=["api"])
    def run_full(run_id: str) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        runs = {run["run_id"]: run for run in active.runs()}
        run = runs[resolved]
        observations = active.contexts(resolved).get("phenomena", [])
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
            **_metadata(run),
            "metrics": active.latest_metrics(resolved),
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

        pairs = []
        for eng in sorted(latest):
            if engine is not None and eng != engine:
                continue
            for met in sorted(latest[eng]):
                if metric is not None and met != metric:
                    continue
                pairs.append((eng, met))

        by_tick: dict[int, dict[str, dict[str, float]]] = {}
        for eng, met in pairs:
            for tick, value in cache.series(active, resolved, eng, met):
                by_tick.setdefault(tick, {}).setdefault(eng, {})[met] = float(value)

        ticks = sorted(by_tick)
        out_ticks = _downsample(ticks, step)
        expected = set(out_ticks)

        values: dict[str, dict[str, list[float]]] = {}
        for tick in expected:
            for eng in sorted(by_tick.get(tick, {})):
                for met in sorted(by_tick[tick][eng]):
                    values.setdefault(eng, {}).setdefault(met, []).append(
                        by_tick[tick][eng][met]
                    )

        return {
            "run_id": resolved,
            "engine": engine,
            "metric": metric,
            "every": step,
            "ticks": out_ticks,
            "values": values,
            "latest": latest,
            "latest_tick": ticks[-1] if ticks else None,
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
    ) -> dict:
        """Comparaison de runs contrôlés (ECHOS-070→072, EXPERIMENT_COMPARISON.md).

        Méta-métriques de reproductibilité (ECHOS-070/071) : même seed ET même
        version ET contenu bit-à-bit identique ⇒ ``is_reproducible`` ; score de
        reproductibilité ``1.0 - (CognitiveDiff + SocialDiff) / 2`` sinon.
        ``format=csv`` produit l'export comparatif aligné (ECHOS-072).
        Réponses déterministes : aucune horodatation d'émission, tris stables.
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
        observation = active.latest_context(resolved, "agents")
        if observation is None:
            raise HTTPException(status_code=404, detail="aucune observation d'agents")
        tick, agents = observation
        agent = next(
            (item for item in agents if str(item.get("id")) == agent_id), None
        )
        if agent is None:
            raise HTTPException(
                status_code=404,
                detail=f"entité introuvable au tick le plus récent : {agent_id}",
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
        observation = active.latest_context(resolved, "agents")
        if observation is None:
            raise HTTPException(status_code=404, detail="aucune observation d'agents")
        tick, agents = observation
        agent = next(
            (item for item in agents if str(item.get("id")) == agent_id), None
        )
        if agent is None:
            raise HTTPException(
                status_code=404,
                detail=f"entité introuvable au tick le plus récent : {agent_id}",
            )
        trust = agent.get("trust") or []
        return {
            "agent_id": agent_id,
            "run_id": resolved,
            "tick": tick,
            "trust": trust,
            "count": len(trust),
        }

    @app.get("/api/groups", tags=["api"])
    def groups(run_id: str | None = Query(default=None)) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        observation = active.latest_context(resolved, "groups")
        if observation is None:
            return {"run_id": resolved, "tick": -1, "groups": []}
        tick, group_list = observation
        return {"run_id": resolved, "tick": tick, "groups": group_list}

    @app.get("/api/emergent-phenomena", tags=["api"])
    def emergent_phenomena(run_id: str | None = Query(default=None)) -> dict:
        active = _require_store(store)
        resolved = _resolve_run(active, run_id)
        observations = active.contexts(resolved).get("phenomena", [])
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
