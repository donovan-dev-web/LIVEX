"""Tests du service statique de l'interface et du point d'entrée serveur.

Couvre le mode production (``ui_dist``) : index à la racine, ressources,
repli SPA du routeur client, priorité des routes API, et ``default_ui_dist``.
"""

from __future__ import annotations

import pathlib

from fastapi.testclient import TestClient

from echos.api.app import create_app
from echos.server import default_ui_dist


def _build_dist(root: pathlib.Path) -> pathlib.Path:
    """Crée un faux build Vite : index.html + une ressource à extension."""
    dist = root / "dist"
    (dist / "assets").mkdir(parents=True)
    (dist / "index.html").write_text("<!doctype html><title>ECHOS UI</title>")
    (dist / "assets" / "app.js").write_text("console.log('echos')")
    return dist


def test_root_serves_the_ui_index(tmp_path):
    client = TestClient(create_app(ui_dist=str(_build_dist(tmp_path))))

    response = client.get("/")

    assert response.status_code == 200
    assert "ECHOS UI" in response.text


def test_static_resource_is_served(tmp_path):
    client = TestClient(create_app(ui_dist=str(_build_dist(tmp_path))))

    response = client.get("/assets/app.js")

    assert response.status_code == 200
    assert "console.log" in response.text


def test_unknown_client_route_falls_back_to_index(tmp_path):
    """Le routeur React sert ``/analysis/run/1`` : le serveur doit renvoyer l'index.

    Sans ce repli, un rechargement (F5) ou un lien profond renvoie 404 alors
    que la route existe côté client.
    """
    client = TestClient(create_app(ui_dist=str(_build_dist(tmp_path))))

    response = client.get("/analysis/run/1")

    assert response.status_code == 200
    assert "ECHOS UI" in response.text


def test_missing_asset_is_a_plain_404(tmp_path):
    """Un ``.js`` absent ne doit pas se déguiser en HTML (sinon erreur de parse)."""
    client = TestClient(create_app(ui_dist=str(_build_dist(tmp_path))))

    response = client.get("/assets/missing.js")

    assert response.status_code == 404


def test_unknown_api_route_stays_a_404(tmp_path):
    """Une route API inconnue reste un 404 : le repli SPA ne doit pas l'avaler."""
    client = TestClient(create_app(ui_dist=str(_build_dist(tmp_path))))

    response = client.get("/api/nexistepas")

    assert response.status_code == 404
    assert response.headers["content-type"].startswith("application/json")


def test_api_routes_keep_priority_over_static_mount(tmp_path):
    """``/health`` et ``/api/*`` restent résolus par l'API, pas par le montage."""
    client = TestClient(create_app(ui_dist=str(_build_dist(tmp_path))))

    health = client.get("/health")
    runs = client.get("/api/runs")

    assert health.status_code == 200
    assert health.json()["component"] == "echos"
    assert runs.status_code != 404


def test_root_returns_endpoints_without_ui():
    """Sans build UI, l'application reste une API seule (mode tests)."""
    client = TestClient(create_app())

    response = client.get("/")

    assert response.status_code == 200
    assert response.json()["component"] == "echos"
    assert "/health" in response.json()["endpoints"]


def test_ui_dist_none_when_directory_absent(monkeypatch):
    monkeypatch.delenv("ECHOS_UI_DIST", raising=False)
    monkeypatch.setattr("echos.server.pathlib.Path.is_dir", lambda self: False)

    assert default_ui_dist() is None


def test_ui_dist_env_override(monkeypatch, tmp_path):
    monkeypatch.setenv("ECHOS_UI_DIST", str(tmp_path / "custom"))

    assert default_ui_dist() == str(tmp_path / "custom")
