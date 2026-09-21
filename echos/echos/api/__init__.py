"""API ECHOS — couche applicative FastAPI (port 5000).

Points d'entrée : ``create_app()`` (factory) et ``app`` (module-level pour uvicorn).
"""

from .app import create_app

__all__ = ["create_app"]
