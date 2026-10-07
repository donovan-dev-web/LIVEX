"""Point d'entrée serveur ECHOS (API seule).

ECHOS ne porte aucune interface : c'est un moteur d'analyse et d'ingestion qui
publie une API REST locale. Le présentatif est le Launcher (ADR-003 Launcher,
ADR-007 : consoles et fenêtre d'analyse natives), qui consomme ces routes depuis
sa propre fenêtre — aucune instance de navigateur n'est nécessaire.

Configuration par variables d'environnement :

- ``ECHOS_HOST`` (défaut ``127.0.0.1``) — jamais exposé hors de la machine ;
- ``ECHOS_PORT`` (défaut ``5000``) ;
- ``ECHOS_ANALYTICS_DB`` — base d'analyse SQLite (sinon routes de données 503) ;
- ``ECHOS_LOG_LEVEL`` (défaut ``info``).

Usage : ``python -m echos.server`` ou le script ``echos-serve``.
"""

from __future__ import annotations

import os

import uvicorn

from echos.api.app import create_app


def main() -> None:
    """Lance uvicorn (API seule) — bloquant, termine à la fermeture."""
    host = os.environ.get("ECHOS_HOST", "127.0.0.1")
    port = int(os.environ.get("ECHOS_PORT", "5000"))
    log_level = os.environ.get("ECHOS_LOG_LEVEL", "info")

    app = create_app()
    server = uvicorn.Server(
        uvicorn.Config(app, host=host, port=port, log_level=log_level)
    )
    app.state.uvicorn_server = server
    server.run()


if __name__ == "__main__":
    main()
