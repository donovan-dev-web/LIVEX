"""Tests du point d'entrée serveur.

ECHOS est une **API seule** (ADR-003 ECHOS révisé, ADR-007 Launcher) : aucune
page n'est servie, le présentatif est le Launcher. Ces tests fixent ce contrat —
racine qui publie les endpoints, absence de tout repli SPA, priorité des routes
API — afin qu'une réintroduction silencieuse d'un montage statique échoue ici.
"""

from __future__ import annotations

from fastapi.testclient import TestClient

from echos.api.app import create_app


def test_root_returns_endpoints():
    """La racine publie la liste des endpoints : c'est le contrat du consommateur."""
    client = TestClient(create_app())

    response = client.get("/")

    assert response.status_code == 200
    assert response.json()["component"] == "echos"
    assert "/health" in response.json()["endpoints"]


def test_api_routes_keep_priority():
    """``/health`` et ``/api/*`` répondent par l'API, indépendamment de toute ressource."""
    client = TestClient(create_app())

    health = client.get("/health")
    runs = client.get("/api/runs")

    assert health.status_code == 200
    assert health.json()["component"] == "echos"
    assert runs.status_code != 404


def test_no_interface_is_served():
    """Aucune page n'est servie : une route de navigateur reste un 404 JSON.

    Sans ce test, un montage statique réintroduit par erreur passerait inaperçu :
    ECHOS n'a plus ni build d'interface ni shell de bureau.
    """
    client = TestClient(create_app())

    for path in ("/analysis/run/1", "/index.html", "/assets/app.js"):
        response = client.get(path)
        assert response.status_code == 404, path
        assert response.headers["content-type"].startswith("application/json"), path


def test_unknown_api_route_stays_a_404():
    """Une route API inconnue reste un 404 explicite, sans repli silencieux."""
    client = TestClient(create_app())

    response = client.get("/api/nexistepas")

    assert response.status_code == 404
    assert response.headers["content-type"].startswith("application/json")
