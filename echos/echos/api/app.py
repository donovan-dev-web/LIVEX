"""Fabrique de l'application FastAPI ECHOS (port 5000, ADR-001 ECHOS)."""

from fastapi import FastAPI

from echos import __version__


def create_app() -> FastAPI:
    """Construit l'application ECHOS (sans effet de bord d'import)."""
    app = FastAPI(
        title="ECHOS — Observation LIVEX",
        description="API ECHOS : analyse, observation et pilotage de SYNE.",
        version=__version__,
    )

    @app.get("/health", tags=["system"])
    def health() -> dict:
        return {"status": "ok", "component": "echos", "version": __version__}

    @app.get("/", tags=["system"])
    def root() -> dict:
        return {
            "component": "echos",
            "message": "API ECHOS (FastAPI) — observation & pilotage de SYNE",
            "endpoints": ["/health", "/api/runs/*", "/api/compare"],
        }

    return app


app = create_app()
