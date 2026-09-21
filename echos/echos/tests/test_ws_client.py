import json
from pathlib import Path

import pytest

from echos.ingestion import ExternalEvent, InvalidMessageError, WorldSnapshot, WsClient

FIXTURES = Path(__file__).resolve().parent / "fixtures"
GOLDEN = Path(__file__).resolve().parent / "golden"


class FakeTransport:
    """Transport simulé : rejoue les fixtures dans l'ordre, enregistre send/close."""

    def __init__(self, payloads: list[str]) -> None:
        self._queue = list(payloads)
        self.sent: list[str] = []
        self.closed = False

    def recv(self, timeout: float | None = None) -> str | None:
        return self._queue.pop(0) if self._queue else None

    def send(self, payload: str | bytes) -> None:
        self.sent.append(payload.decode() if isinstance(payload, bytes) else payload)

    def close(self) -> None:
        self.closed = True


def _fixture(name: str) -> str:
    return (FIXTURES / name).read_text()


def _expected_stream() -> list[dict]:
    return json.loads((GOLDEN / "stream.json").read_text())


def test_receive_replays_snapshot_then_event_in_golden_order():
    transport = FakeTransport(
        [_fixture("world_snapshot.json"), _fixture("external_event.json")]
    )
    client = WsClient(transport=transport)
    client.connect("ws://127.0.0.1:5180")

    received = [client.receive(), client.receive()]

    assert [
        {"type": message.type, "tick": message.tick} for message in received
    ] == _expected_stream()
    assert isinstance(received[0], WorldSnapshot)
    assert isinstance(received[1], ExternalEvent)


def test_reception_is_deterministic_across_replays():
    stream = _expected_stream()
    for _ in range(10):
        transport = FakeTransport(
            [_fixture("world_snapshot.json"), _fixture("external_event.json")]
        )
        client = WsClient(transport=transport)
        client.connect("ws://127.0.0.1:5180")
        received = [client.receive(), client.receive()]
        assert [
            {"type": message.type, "tick": message.tick} for message in received
        ] == stream


def _error_from_client():
    client = WsClient(transport=FakeTransport([_fixture("invalid_message.json")]))
    client.connect("ws://127.0.0.1:5180")
    try:
        client.receive()
    except InvalidMessageError as exc:
        return exc


def test_receive_invalid_message_raises_stable_error():
    first = _error_from_client()
    second = _error_from_client()

    assert isinstance(first, InvalidMessageError)
    assert str(first) == str(second)
    assert "tick" in str(first)


def test_iteration_yields_messages_until_connection_closed():
    transport = FakeTransport(
        [_fixture("world_snapshot.json"), _fixture("external_event.json")]
    )
    client = WsClient(transport=transport)
    client.connect("ws://127.0.0.1:5180")

    types = [message.type for message in client]

    assert types == ["snapshot", "decision_made"]


def test_send_delegates_to_transport():
    transport = FakeTransport([_fixture("world_snapshot.json")])
    client = WsClient(transport=transport)
    client.connect("ws://127.0.0.1:5180")

    client.send(b"ping")

    assert transport.sent == ["ping"]


def test_close_forwards_to_transport():
    transport = FakeTransport([])
    client = WsClient(transport=transport)
    client.connect("ws://127.0.0.1:5180")

    client.close()

    assert transport.closed is True


def test_context_manager_closes_transport():
    transport = FakeTransport([])
    with WsClient(transport=transport) as client:
        client.connect("ws://127.0.0.1:5180")

    assert transport.closed is True


def test_receive_without_connect_raises():
    client = WsClient()

    with pytest.raises(InvalidMessageError, match="non connecté"):
        client.receive()


def test_send_without_connect_raises():
    client = WsClient()

    with pytest.raises(InvalidMessageError, match="non connecté"):
        client.send("ping")
