"""Adaptateur Launcher (Linux) pour le serveur ECHOS configuré par l'environnement.

Projette les arguments partagés du Launcher sur l'environnement ECHOS, et
applique une base analytique par défaut quand l'opérateur n'en impose aucune —
l'environnement du Launcher est minimal, et une sonde répondant 503 ferait
échouer le démarrage.
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    """Accept the shared Launcher arguments without treating them as ECHOS options."""
    parser = argparse.ArgumentParser(description="Start ECHOS for the LIVEX Launcher.")
    parser.add_argument("--headless", action="store_true")
    parser.add_argument("--instance-id")
    parser.add_argument("--control-port", type=int)
    parser.add_argument("--work-dir")
    parser.add_argument("--log-dir")
    parser.add_argument("--correlation-id")
    return parser.parse_args(argv)


def data_root() -> Path:
    """Racine des données LIVEX : ``LIVEX_DATA``, sinon le profil utilisateur.

    Même règle que celle du Launcher : les deux processus doivent désigner le
    même emplacement, faute de quoi ils ne verraient pas les mêmes analyses.
    """
    configured = os.environ.get("LIVEX_DATA")
    if configured and configured.strip():
        return Path(configured)
    return Path.home() / ".livex-data"


def ensure_analytics_db() -> None:
    """Impose la base d'analyse par défaut quand l'opérateur n'en fournit aucune.

    Le Launcher démarre ses composants avec un environnement minimal (jeton de
    session, corrélation, racine d'installation) : sans ``ECHOS_ANALYTICS_DB``, la
    sonde ``/health/ready`` répond 503 et le Launcher déclare ECHOS « Défaillant »
    une fois le délai de démarrage dépassé. La base par défaut vit dans la racine
    des données, **hors du répertoire de travail** : celui-ci est propre à chaque
    instance, une base par instance fragmenterait l'analyse. Une valeur imposée par
    l'opérateur reste prioritaire ; une base par défaut inutilisable laisse la
    variable absente, donc la sonde publie son 503 explicite au lieu d'un échec muet.
    """
    if os.environ.get("ECHOS_ANALYTICS_DB"):
        return

    database = data_root() / "echos" / "analytics.sqlite"
    try:
        database.parent.mkdir(parents=True, exist_ok=True)
    except OSError as exc:
        print(f"base d'analyse par défaut inutilisable : {exc}", file=sys.stderr)
        return

    os.environ["ECHOS_ANALYTICS_DB"] = str(database)


def main(argv: list[str] | None = None) -> None:
    args = parse_args(argv)
    os.environ["ECHOS_HOST"] = "127.0.0.1"
    if args.control_port is not None:
        if not 1 <= args.control_port <= 65535:
            raise SystemExit("--control-port must be between 1 and 65535")
        os.environ["ECHOS_PORT"] = str(args.control_port)
    ensure_analytics_db()
    from echos.server import main as serve

    serve()


if __name__ == "__main__":
    main()
