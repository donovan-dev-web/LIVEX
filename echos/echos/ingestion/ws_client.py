"""Consommateur WebSocket :5180 de SYNE (API_CONTRACTS.md §2).

Transport injectable — les tests utilisent un faux transport alimenté par les
fixtures golden (réception déterministe) ; en production, :class:`WsClient`
ouvre la connexion réelle via ``websockets.sync.client``.
"""

from __future__ import annotations

import queue
import threading
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

        # A long batch can queue many event frames while ECHOS computes its
        # metrics. Disable the small default receive queue so backpressure is
        # applied by the socket instead of terminating the stream mid-run.
        self._websocket = connect(url, max_queue=None, max_size=16 * 1024 * 1024)
        self._messages: queue.Queue[object] = queue.Queue()
        self._closed = threading.Event()
        self._receiver = threading.Thread(target=self._receive_loop, daemon=True)
        self._receiver.start()

    def _receive_loop(self) -> None:
        try:
            while not self._closed.is_set():
                self._messages.put(self._websocket.recv())
        except Exception as exc:
            self._messages.put(exc)

    def recv(self, timeout: float | None = None) -> str | bytes:
        try:
            value = self._messages.get(timeout=timeout)
        except queue.Empty:
            raise TimeoutError("délai dépassé en attendant une trame WebSocket") from None
        if isinstance(value, Exception):
            raise value
        return value

    def send(self, payload: str | bytes) -> None:
        self._websocket.send(payload)

    def close(self) -> None:
        self._closed.set()
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
        except Exception as exc:  # transport réel : fermeture ≙ fin de flux
            try:
                from websockets.exceptions import ConnectionClosed

                is_closed = isinstance(exc, ConnectionClosed)
            except ImportError:
                is_closed = False
            if is_closed:
                raise StopIteration from exc
            raise

    def __enter__(self) -> "WsClient":
        return self

    def __exit__(self, *exc: object) -> None:
        self.close()
