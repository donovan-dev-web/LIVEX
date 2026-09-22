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

from fastapi import FastAPI, HTTPException, Query

from echos.storage.sqlite import AnalyticsStore

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
    """Attache les endpoints d'analyse ECHOS à l'application."""
    cache = SeriesCache()

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
        phenomena = active.latest_context(resolved, "phenomena") or (
            -1,
            {"detected": [], "disclaimer": ""},
        )
        return {
            **_metadata(run),
            "metrics": active.latest_metrics(resolved),
            "phenomena": phenomena[1],
        }

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
        observation = active.latest_context(resolved, "phenomena")
        if observation is None:
            return {
                "run_id": resolved,
                "tick": -1,
                "phenomena": [],
                "disclaimer": "",
            }
        tick, phenomena = observation
        return {
            "run_id": resolved,
            "tick": tick,
            "phenomena": phenomena.get("detected") or [],
            "disclaimer": phenomena.get("disclaimer") or "",
        }

    app.state.series_cache = cache
