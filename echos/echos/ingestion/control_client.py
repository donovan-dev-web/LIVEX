"""Client de contrôle HTTP :5181 de SYNE (API_CONTRACTS.md §3).

Endpoints : ``POST /api/control/start|pause|resume|reset``, binding local
``127.0.0.1:5181``. Transport injectable (tests : ``httpx.MockTransport``,
déterminisme des requêtes) ; défaut : client ``httpx`` réel.
"""

from __future__ import annotations

import httpx

DEFAULT_BASE_URL = "http://127.0.0.1:5181"


class ControlError(Exception):
    """Échec d'une commande de contrôle (transport ou statut HTTP non 2xx)."""

    def __init__(self, endpoint: str, status: str | int, detail: str) -> None:
        super().__init__(
            f"/api/control/{endpoint} a échoué ({status}) : {detail}"
        )
        self.endpoint = endpoint
        self.status = status
        self.detail = detail


class ControlClient:
    """Pilotage SYNE via HTTP :5181 (start, pause, resume, reset)."""

    def __init__(
        self,
        base_url: str = DEFAULT_BASE_URL,
        transport: httpx.BaseTransport | None = None,
    ) -> None:
        self._base_url = base_url.rstrip("/")
        self._http = httpx.Client(
            timeout=5.0, transport=transport, follow_redirects=False
        )

    def start(
        self,
        seed: int | None = None,
        config: dict | None = None,
        max_ticks: int | None = None,
    ) -> dict:
        """POST /api/control/start — ``{ seed, config }`` (optionnels)."""
        payload: dict = {}
        if seed is not None:
            payload["seed"] = seed
        if config is not None:
            payload["config"] = config
        if max_ticks is not None:
            payload["maxTicks"] = max_ticks
        return self._post("start", payload)

    def pause(self) -> dict:
        """POST /api/control/pause — corps vide."""
        return self._post("pause", None)

    def resume(self) -> dict:
        """POST /api/control/resume — corps vide."""
        return self._post("resume", None)

    def stop(self) -> dict:
        """POST /api/control/stop — arrête le run courant."""
        return self._post("stop", None)

    def reset(
        self,
        seed: int | None = None,
        run_id: str | None = None,
        max_ticks: int | None = None,
    ) -> dict:
        """POST /api/control/reset — options facultatives du contrat SYNE."""
        payload: dict = {}
        if seed is not None:
            payload["seed"] = seed
        if run_id is not None:
            payload["runId"] = run_id
        if max_ticks is not None:
            payload["maxTicks"] = max_ticks
        return self._post("reset", payload)

    def _post(self, endpoint: str, payload: dict | None) -> dict:
        url = f"{self._base_url}/api/control/{endpoint}"
        try:
            response = self._http.post(url, json=payload)
        except httpx.HTTPError as exc:
            raise ControlError(endpoint, "transport", str(exc)) from exc
        if response.status_code // 100 != 2:
            raise ControlError(endpoint, response.status_code, response.text)
        return response.json() if response.content else {}

    def status(self) -> dict:
        """GET /api/control/status — état actuel du serveur SYNE."""
        try:
            response = self._http.get(f"{self._base_url}/api/control/status")
        except httpx.HTTPError as exc:
            raise ControlError("status", "transport", str(exc)) from exc
        if response.status_code // 100 != 2:
            raise ControlError("status", response.status_code, response.text)
        return response.json() if response.content else {}

    def close(self) -> None:
        self._http.close()

    def __enter__(self) -> "ControlClient":
        return self

    def __exit__(self, *exc: object) -> None:
        self.close()
