"""Fabrique de l'application FastAPI ECHOS (port 5000, ADR-001 ECHOS).

``create_app`` accepte un ``AnalyticsStore`` (base d'analyse ECHOS) pour les
endpoints REST ; à défaut d'instance injectée, la variable d'environnement
``ECHOS_ANALYTICS_DB`` (chemin du fichier SQLite) est consultée. Sans base
configurée, les routes de données répondent 503 (contrat toutefois publié).
L'API reste **lecture seule** : aucune écriture dans le monde observé
(règle d'or §4.10.3, ECHOS ph4).
"""

from __future__ import annotations

import hmac
import os
import sqlite3

from fastapi import FastAPI, HTTPException, Request

from echos import __version__
from echos.api.routes import register_routes
from echos.storage.sqlite import AnalyticsStore

_ENDPOINTS = [
    "/health",
    "/health/ready",
    "/control/shutdown",
    "/analysis/run",
    "/analysis/experiment",
    "/analysis/report",
    "/api/runs",
    "/api/control/status",
    "/api/control/{action}",
    "/api/runs/{id}",
    "/api/runs/{id}/metrics",
    "/api/runs/{id}/calibration",
    "/api/runs/{id}/export",
    "/api/runs/{id}/decisions",
    "/api/beliefs/{agentId}",
    "/api/relationships/{agentId}",
    "/api/groups",
    "/api/emergent-phenomena",
    "/api/compare",
    "/api/runs/{id}/causal-chains/{agentId}",
]


def create_app(store: AnalyticsStore | None = None) -> FastAPI:
    """Construit l'application ECHOS (sans effet de bord d'import).

    L'application est une **API seule** : ECHOS ne porte aucune interface, aucun
    conteneur de page (ADR-003 ECHOS révisé, ADR-007 Launcher). ``/`` répond la
    liste des endpoints, et le présentatif — le Launcher — consomme ces routes.
    """
    if store is None:
        db_path = os.environ.get("ECHOS_ANALYTICS_DB")
        if db_path:
            store = AnalyticsStore(db_path)

    app = FastAPI(
        title="ECHOS — Observation LIVEX",
        description="API ECHOS : analyse, observation et pilotage de SYNE.",
        version=__version__,
    )

    register_routes(app, store)

    @app.get("/health", tags=["system"])
    def health() -> dict:
        return {"status": "ok", "component": "echos", "version": __version__}

    @app.get("/health/ready", tags=["system"])
    def ready() -> dict:
        if store is None:
            raise HTTPException(
                status_code=503,
                detail="base d'analyse non configurée (variable ECHOS_ANALYTICS_DB)",
            )
        try:
            store.runs()
        except sqlite3.Error as exc:
            raise HTTPException(
                status_code=503,
                detail="base d'analyse indisponible ou schéma invalide",
            ) from exc
        return {"status": "ok", "component": "echos", "analytics": "ready"}

    @app.post("/control/shutdown", tags=["system"])
    def shutdown(request: Request) -> dict:
        expected = os.environ.get("LIVEX_SESSION_TOKEN")
        authorization = request.headers.get("authorization", "")
        supplied = authorization.removeprefix("Bearer ")
        authorized = authorization.startswith("Bearer ") and hmac.compare_digest(
            supplied, expected or ""
        )
        if not authorized:
            raise HTTPException(status_code=401, detail="Jeton de session absent ou invalide.")
        server = getattr(request.app.state, "uvicorn_server", None)
        if server is None:
            raise HTTPException(status_code=503, detail="Serveur ASGI sans contrôleur d'arrêt.")
        server.should_exit = True
        return {"status": "shutting_down"}

    @app.get("/", tags=["system"])
    def root() -> dict:
        return {
            "component": "echos",
            "message": "API ECHOS (FastAPI) — observation & pilotage de SYNE",
            "endpoints": list(_ENDPOINTS),
        }

    return app


app = create_app()
