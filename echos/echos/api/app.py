"""Fabrique de l'application FastAPI ECHOS (port 5000, ADR-001 ECHOS).

``create_app`` accepte un ``AnalyticsStore`` (base d'analyse ECHOS) pour les
endpoints REST ; à défaut d'instance injectée, la variable d'environnement
``ECHOS_ANALYTICS_DB`` (chemin du fichier SQLite) est consultée. Sans base
configurée, les routes de données répondent 503 (contrat toutefois publié).
L'API reste **lecture seule** : aucune écriture dans le monde observé
(règle d'or §4.10.3, ECHOS ph4).
"""

from __future__ import annotations

import os

from fastapi import FastAPI

from echos import __version__
from echos.api.routes import register_routes
from echos.storage.sqlite import AnalyticsStore

_ENDPOINTS = [
    "/health",
    "/api/runs",
    "/api/runs/{id}",
    "/api/runs/{id}/metrics",
    "/api/runs/{id}/export",
    "/api/beliefs/{agentId}",
    "/api/relationships/{agentId}",
    "/api/groups",
    "/api/emergent-phenomena",
    "/api/runs/{id}/causal-chains/{agentId}",
]


def create_app(store: AnalyticsStore | None = None) -> FastAPI:
    """Construit l'application ECHOS (sans effet de bord d'import)."""
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

    @app.get("/", tags=["system"])
    def root() -> dict:
        return {
            "component": "echos",
            "message": "API ECHOS (FastAPI) — observation & pilotage de SYNE",
            "endpoints": list(_ENDPOINTS),
        }

    return app


app = create_app()
