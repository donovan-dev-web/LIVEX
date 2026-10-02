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

from fastapi import FastAPI, HTTPException, Request
from fastapi.staticfiles import StaticFiles
from starlette.exceptions import HTTPException as StarletteHTTPException

from echos import __version__
from echos.api.routes import register_routes
from echos.storage.sqlite import AnalyticsStore

_ENDPOINTS = [
    "/health",
    "/health/ready",
    "/control/shutdown",
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


def create_app(
    store: AnalyticsStore | None = None, ui_dist: str | None = None
) -> FastAPI:
    """Construit l'application ECHOS (sans effet de bord d'import).

    ``ui_dist`` (chemin du build ``echos-ui/dist``) active le service de
    l'interface statique : c'est le mode production, utilisé par le shell
    Electron. Sans ``ui_dist``, l'application reste une API seule et ``/``
    répond la liste des endpoints (mode tests / intégration).
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

    if ui_dist and os.path.isdir(ui_dist):
        _mount_ui(app, ui_dist)
    else:

        @app.get("/", tags=["system"])
        def root() -> dict:
            return {
                "component": "echos",
                "message": "API ECHOS (FastAPI) — observation & pilotage de SYNE",
                "endpoints": list(_ENDPOINTS),
            }

    return app


class SpaStaticFiles(StaticFiles):
    """Fichiers statiques avec repli *SPA* vers ``index.html``.

    Le routeur de l'interface est côté client (React Router) : une URL comme
    ``/analysis`` n'existe pas sur le disque et doit servir ``index.html``
    pour que le routeur prenne le relais. Le repli est restreint :

    - **chemins sans extension** : un ``.js``/``.css`` manquant renvoie un 404
      franc plutôt que du HTML déguisé en script ;
    - **hors préfixes de l'API** (``api``, ``health``) : une route API inconnue
      doit rester un 404, pas servir l'index.
    """

    _RESERVED = ("api", "health")

    async def get_response(self, path, scope):
        try:
            return await super().get_response(path, scope)
        except StarletteHTTPException as exc:
            last_segment = path.rsplit("/", 1)[-1]
            reserved = path.split("/", 1)[0] in self._RESERVED
            if exc.status_code == 404 and not reserved and "." not in last_segment:
                return await super().get_response("index.html", scope)
            raise


def _mount_ui(app: FastAPI, ui_dist: str) -> None:
    """Monte le build de l'interface à la racine (monté en dernier).

    Les routes API sont enregistrées avant le montage : Starlette résout dans
    l'ordre, donc ``/health`` et ``/api/*`` gardent la priorité sur le
    service statique monté sur ``/``.
    """
    app.mount("/", SpaStaticFiles(directory=ui_dist, html=True), name="ui")


app = create_app()
