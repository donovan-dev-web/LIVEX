from fastapi.testclient import TestClient

from echos import __version__
from echos.api.app import create_app
from echos.storage.sqlite import AnalyticsStore


def test_health_returns_ok():
    client = TestClient(create_app())

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json()["status"] == "ok"
    assert response.json()["component"] == "echos"
    assert response.json()["version"] == __version__


def test_root_describes_api_endpoints():
    client = TestClient(create_app())

    response = client.get("/")

    assert response.status_code == 200
    body = response.json()
    assert body["component"] == "echos"
    assert isinstance(body["endpoints"], list)
    assert "/health" in body["endpoints"]
    assert "/api/runs" in body["endpoints"]  # contrat ph4 publié


def test_unknown_route_returns_404(tmp_path):
    client = TestClient(create_app(AnalyticsStore(tmp_path / "analyse.db")))

    assert client.get("/api/runs/inexistant").status_code == 404
    assert client.get("/bogus").status_code == 404
