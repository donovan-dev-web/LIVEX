"""Consommateur WebSocket :5180 de SYNE (API_CONTRACTS.md §2).

Transport injectable — les tests utilisent un faux transport alimenté par les
fixtures golden (réception déterministe) ; en production, :class:`WsClient`
ouvre la connexion réelle via ``websockets.sync.client``.
"""

from __future__ import annotations

from typing import Protocol

from echos.ingestion.models import Message, InvalidMessageError, parse_message


class WsTransport(Protocol):
    """Interface de transport : connexion WebSocket réelle ou simulée."""

    def recv(self, timeout: float | None = None) -> str | bytes:
        """Blocage jusqu'à réception d'un message (JSON camelCase)."""
        ...

    def send(self, payload: str | bytes) -> None:
        """Envoi d'un payload brut."""
        ...

    def close(self) -> None:
        """Fermeture propre de la connexion."""
        ...


class _WebsocketsTransport:
    """Transport réel sur ``websockets.sync.client`` (URL ws://127.0.0.1:5180)."""

    def __init__(self, url: str) -> None:
        from websockets.sync.client import connect

        self._websocket = connect(url)

    def recv(self, timeout: float | None = None) -> str | bytes:
        return self._websocket.recv(timeout=timeout)

    def send(self, payload: str | bytes) -> None:
        self._websocket.send(payload)

    def close(self) -> None:
        self._websocket.close()


class WsClient:
    """Client de réception des messages SYNE (snapshot + event).

    Usage :

        with WsClient() as client:
            client.connect("ws://127.0.0.1:5180")
            for message in client:
                ...  # WorldSnapshot | ExternalEvent
    """

    def __init__(self, transport: WsTransport | None = None) -> None:
        self._transport = transport

    def connect(self, url: str) -> None:
        """Établit la connexion (vide si un transport simulé est injecté)."""
        if self._transport is None:
            self._transport = _WebsocketsTransport(url)

    def receive(self, timeout: float | None = None) -> Message:
        """Reçoit et valide le prochain message de façon déterministe."""
        if self._transport is None:
            raise InvalidMessageError("transport", "client non connecté")
        payload = self._transport.recv(timeout=timeout)
        if payload is None:
            raise InvalidMessageError("transport", "connexion fermée (None)")
        return parse_message(payload)

    def send(self, payload: str | bytes) -> None:
        """Envoi brut (contrôle applicatif réservé au HTTP :5181)."""
        if self._transport is None:
            raise InvalidMessageError("transport", "client non connecté")
        self._transport.send(payload)

    def close(self) -> None:
        """Ferme la connexion (idempotent, sûr si aucune connexion)."""
        if self._transport is not None:
            self._transport.close()

    def __iter__(self) -> "WsClient":
        return self

    def __next__(self) -> Message:
        try:
            return self.receive()
        except InvalidMessageError as exc:
            if exc.detail.startswith("connexion fermée"):
                raise StopIteration from exc
            raise

    def __enter__(self) -> "WsClient":
        return self

    def __exit__(self, *exc: object) -> None:
        self.close()
