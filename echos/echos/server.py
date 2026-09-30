"""Point d'entrée serveur ECHOS (API + interface statique).

C'est le processus lancé par le shell Electron : il sert à la fois l'API
REST et le build de ``echos-ui``, sur la **même origine**. L'interface peut
donc appeler ``/api/*`` en relatif, sans CORS ni URL absolue à configurer.

Configuration par variables d'environnement :

- ``ECHOS_HOST`` (défaut ``127.0.0.1``) — jamais exposé hors de la machine ;
- ``ECHOS_PORT`` (défaut ``5000``) — le shell Electron impose un port libre ;
- ``ECHOS_ANALYTICS_DB`` — base d'analyse SQLite (sinon routes de données 503) ;
- ``ECHOS_UI_DIST`` — dossier du build de l'interface ; à défaut, le
  ``echos-ui/dist`` du dépôt est utilisé s'il existe ;
- ``ECHOS_LOG_LEVEL`` (défaut ``info``).

Usage : ``python -m echos.server`` ou le script ``echos-serve``.
"""

from __future__ import annotations

import os
import pathlib

import uvicorn

from echos.api.app import create_app


def default_ui_dist() -> str | None:
    """Dossier du build UI : ``ECHOS_UI_DIST`` sinon ``echos/echos-ui/dist``."""
    explicit = os.environ.get("ECHOS_UI_DIST")
    if explicit:
        return explicit
    candidate = pathlib.Path(__file__).resolve().parents[1] / "echos-ui" / "dist"
    return str(candidate) if candidate.is_dir() else None


def main() -> None:
    """Lance uvicorn (API + UI) — bloquant, termine à la fermeture."""
    host = os.environ.get("ECHOS_HOST", "127.0.0.1")
    port = int(os.environ.get("ECHOS_PORT", "5000"))
    log_level = os.environ.get("ECHOS_LOG_LEVEL", "info")

    app = create_app(ui_dist=default_ui_dist())
    uvicorn.run(app, host=host, port=port, log_level=log_level)


if __name__ == "__main__":
    main()
