# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Câblage du flux PRISM fourni par le Launcher (LAUNCHER-202).

Le Launcher résout les ports de la session et transmet l'URL du flux de lecture
au lancement d'ECHOS (``--prism-stream``). ECHOS ne devine aucun port : sans
adresse, ``POST /ingest/prism`` refuse la demande au lieu de choisir un flux.
"""

from __future__ import annotations

from echos.api.app import create_app
from echos.launcher import parse_args
from echos.storage.sqlite import AnalyticsStore

IDENTITY = {"runId": "prism-session-1", "seed": 4242, "contentHash": "9f2c41ab"}


def test_launcher_accepts_the_prism_stream_argument():
    args = parse_args(
        [
            "--headless",
            "--control-port",
            "5401",
            "--prism-stream",
            "ws://127.0.0.1:5311/data/read",
        ]
    )

    assert args.prism_stream == "ws://127.0.0.1:5311/data/read"


def test_launcher_without_the_argument_leaves_no_stream(monkeypatch):
    monkeypatch.delenv("ECHOS_PRISM_STREAM", raising=False)
    args = parse_args(["--control-port", "5401"])

    assert args.prism_stream is None


def test_ingest_refuses_a_request_without_any_stream(tmp_path, monkeypatch):
    monkeypatch.delenv("ECHOS_PRISM_STREAM", raising=False)
    with AnalyticsStore(tmp_path / "echos.db") as store:
        api = _client(store)
        response = api.post("/ingest/prism", json=IDENTITY)

        assert response.status_code == 400
        assert "aucun flux PRISM" in response.json()["detail"]


def test_ingest_uses_the_stream_resolved_by_the_launcher(tmp_path, monkeypatch):
    monkeypatch.setenv("ECHOS_PRISM_STREAM", "ws://127.0.0.1:1/data/read")
    with AnalyticsStore(tmp_path / "echos.db") as store:
        api = _client(store)
        response = api.post("/ingest/prism", json=IDENTITY)

        assert response.status_code == 200
        # L'adresse retenue est celle du Launcher, publiée telle quelle : le flux
        # est injoignable ici, et c'est le thread d'ingestion qui l'ignore, pas
        # la demande qui échoue.
        assert response.json()["wsUrl"] == "ws://127.0.0.1:1/data/read"


def test_an_explicit_stream_still_wins_over_the_launcher_one(tmp_path, monkeypatch):
    monkeypatch.setenv("ECHOS_PRISM_STREAM", "ws://127.0.0.1:1/data/read")
    with AnalyticsStore(tmp_path / "echos.db") as store:
        api = _client(store)
        response = api.post(
            "/ingest/prism",
            json={**IDENTITY, "wsUrl": "ws://127.0.0.1:2/data/read"},
        )

        assert response.status_code == 200
        assert response.json()["wsUrl"] == "ws://127.0.0.1:2/data/read"


def test_a_remote_stream_is_still_refused(tmp_path, monkeypatch):
    monkeypatch.delenv("ECHOS_PRISM_STREAM", raising=False)
    with AnalyticsStore(tmp_path / "echos.db") as store:
        api = _client(store)
        response = api.post(
            "/ingest/prism",
            json={**IDENTITY, "wsUrl": "ws://example.com/data/read"},
        )

        assert response.status_code == 400
        assert "WebSocket local" in response.json()["detail"]


def _client(store):
    from fastapi.testclient import TestClient

    return TestClient(create_app(store))
