"""API ECHOS — couche applicative FastAPI (port 5000).

Points d'entrée : ``create_app()`` (factory, store injectable via
``ECHOS_ANALYTICS_DB``) et ``app`` (module-level pour uvicorn).
``register_routes(app, store)`` attache les endpoints d'analyse (ECHOS-040→044).
"""

from .app import create_app
from .routes import register_routes

__all__ = ["create_app", "register_routes"]
