"""Consommation alignée sur les ticks (ECHOS-010) : découpage en segments,
erreurs de désalignement, et consommation sur **vrai serveur WebSocket**
(serveur Python in-process rejouant le contrat V0.1 de SYNE)."""

import json
import threading
import time
from pathlib import Path

import pytest

from echos.ingestion import (
    TickAlignmentError,
    WsClient,
    aligned_ticks,
)

FIXTURES = Path(__file__).resolve().parent / "fixtures"
GOLDEN = Path(__file__).resolve().parent / "golden"


def _raw(name: str) -> str:
    return (FIXTURES / name).read_text()


def _variant(name: str, tick: int) -> str:
    data = json.loads(_raw(name))
    data["tick"] = tick
    return json.dumps(data)


def _script() -> list[str]:
    """Chaîne V0.1 d'un run : 2 ticks, tick 1 avec 1 décision, tick 2 avec
    2 décisions (vérification multi-événements par tick)."""
    return [
        _variant("world_snapshot_v01.json", 1),
        _variant("tick_summary_v01.json", 1),
        _variant("decision_made_v01.json", 1),
        _variant("world_snapshot_v01.json", 2),
        _variant("tick_summary_v01.json", 2),
        _variant("decision_made_v01.json", 2),
        _variant("decision_made_v01.json", 2),
    ]


class FakeTransport:
    """Transport simulé rejouant le script (déterministe)."""

    def __init__(self, payloads: list[str]) -> None:
        self._queue = list(payloads)

    def recv(self, timeout: float | None = None) -> str | None:
        return self._queue.pop(0) if self._queue else None

    def send(self, payload: str | bytes) -> None:
        pass

    def close(self) -> None:
        pass


def _client(payloads: list[str], real: bool = False) -> WsClient:
    if real:
        return WsClient()
    return WsClient(transport=FakeTransport(payloads))


def test_aligned_ticks_splits_stream_into_segments_in_order():
    client = _client(_script())
    client.connect("ws://127.0.0.1:5180")

    segments = list(aligned_ticks(client))

    assert [segment.tick for segment in segments] == [1, 2]
    assert segments[0].snapshot.alive_count == 2
    assert segments[0].event_types == ["tick_summary", "decision_made"]
    assert segments[1].event_types == ["tick_summary", "decision_made", "decision_made"]


def test_first_segment_matches_golden():
    client = _client(
        [
            _variant("world_snapshot_v01.json", 1),
            _variant("tick_summary_v01.json", 1),
            _variant("decision_made_v01.json", 1),
        ]
    )
    client.connect("ws://127.0.0.1:5180")

    (segment,) = list(aligned_ticks(client))

    expected = json.loads((GOLDEN / "segment_tick1_v01.json").read_text())
    assert segment.to_dict() == expected


def test_single_snapshot_yields_segment_without_events():
    client = _client([_variant("world_snapshot_v01.json", 5)])
    client.connect("ws://127.0.0.1:5180")

    segments = list(aligned_ticks(client))

    assert [segment.tick for segment in segments] == [5]
    assert segments[0].events == []


def test_event_before_first_snapshot_is_rejected():
    client = _client(
        [
            _variant("decision_made_v01.json", 1),
            _variant("world_snapshot_v01.json", 1),
        ]
    )
    client.connect("ws://127.0.0.1:5180")

    with pytest.raises(TickAlignmentError, match="avant le premier snapshot"):
        list(aligned_ticks(client))


def test_misaligned_event_tick_is_rejected():
    client = _client(
        [
            _variant("world_snapshot_v01.json", 1),
            _variant("decision_made_v01.json", 4),
        ]
    )
    client.connect("ws://127.0.0.1:5180")

    with pytest.raises(TickAlignmentError, match="désaligné"):
        list(aligned_ticks(client))


def test_alignment_error_message_is_stable():
    messages = []
    for _ in range(2):
        client = _client(
            [
                _variant("world_snapshot_v01.json", 1),
                _variant("decision_made_v01.json", 4),
            ]
        )
        client.connect("ws://127.0.0.1:5180")
        with pytest.raises(TickAlignmentError) as excinfo:
            list(aligned_ticks(client))
        messages.append(str(excinfo.value))

    assert messages[0] == messages[1]


def _run_in_process_server(frames):
    """Démarre un serveur WebSocket réel en thread ; renvoie (slot, port, thread)."""
    from websockets.sync.server import serve

    slot: list = []

    def _serve() -> None:
        def handler(connection):
            for payload in frames:
                connection.send(payload)

        with serve(handler, "127.0.0.1", 0) as server:
            slot.append(server)
            server.serve_forever()

    thread = threading.Thread(target=_serve, daemon=True)
    thread.start()

    for _ in range(100):
        if slot:
            break
        time.sleep(0.02)
    assert slot, "serveur in-process non démarré"
    port = slot[0].socket.getsockname()[1]
    return slot, port, thread


def test_aligned_ticks_over_real_websocket_server():
    """Consommation E2E sur un serveur WebSocket réel in-process (ECHOS-010)."""
    slot, port, thread = _run_in_process_server(_script())

    client = _client([], real=True)
    client.connect(f"ws://127.0.0.1:{port}/")
    segments = list(aligned_ticks(client))
    client.close()

    assert [segment.tick for segment in segments] == [1, 2]
    assert segments[0].event_types == ["tick_summary", "decision_made"]
    assert segments[0].snapshot.alive_count == 2

    slot[0].shutdown()
    thread.join(timeout=2)
    assert not thread.is_alive()


def test_ws_client_consumes_real_socket_frame_by_frame():
    """Le client réel reçoit snapshot puis événement sur une vraie socket."""
    frames = [
        _variant("world_snapshot_v01.json", 1),
        _variant("decision_made_v01.json", 1),
    ]
    slot, port, thread = _run_in_process_server(frames)

    client = WsClient()
    client.connect(f"ws://127.0.0.1:{port}/")
    first = client.receive()
    second = client.receive()
    client.close()

    assert first.type == "snapshot"
    assert first.tick == 1
    assert second.type == "decision_made"

    slot[0].shutdown()
    thread.join(timeout=2)
    assert not thread.is_alive()
