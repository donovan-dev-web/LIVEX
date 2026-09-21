import json
from pathlib import Path

import pytest
from pydantic import ValidationError

from echos.ingestion import (
    ExternalEvent,
    InvalidMessageError,
    WorldSnapshot,
    parse_message,
)

FIXTURES = Path(__file__).resolve().parent / "fixtures"
GOLDEN = Path(__file__).resolve().parent / "golden"


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text())


def _golden(name: str) -> dict:
    return json.loads((GOLDEN / name).read_text())


def test_world_snapshot_parse_matches_golden():
    message = parse_message((FIXTURES / "world_snapshot.json").read_text())

    assert isinstance(message, WorldSnapshot)
    assert message.model_dump(mode="json", exclude_none=True) == _golden(
        "world_snapshot.json"
    )
    assert message.run_id == "run-abc"
    assert message.tick == 5010
    assert message.simulated_time_minutes == 5010
    assert message.alive_count == 98


def test_world_snapshot_preserves_camelcase_contract():
    message = parse_message((FIXTURES / "world_snapshot.json").read_text())

    assert message.model_dump(mode="json", by_alias=True, exclude_none=True) == _load(
        "world_snapshot.json"
    )


def test_external_event_parse_matches_golden():
    message = parse_message((FIXTURES / "external_event.json").read_text())

    assert isinstance(message, ExternalEvent)
    assert message.model_dump(mode="json") == _golden("external_event.json")
    assert message.type == "decision_made"
    assert message.action == "Eat"
    assert message.value == {"utility": 15.5}


def test_external_event_preserves_camelcase_contract():
    message = parse_message((FIXTURES / "external_event.json").read_text())

    assert message.model_dump(mode="json", by_alias=True, exclude_none=True) == _load(
        "external_event.json"
    )


def test_event_optional_fields_default_to_none():
    event = parse_message('{"type": "agent_spawned", "tick": 7}')

    assert isinstance(event, ExternalEvent)
    assert event.agent_id is None
    assert event.target_id is None
    assert event.action is None
    assert event.cause is None
    assert event.value is None


def test_snapshot_type_is_reserved_to_world_snapshot():
    with pytest.raises(ValidationError):
        ExternalEvent.model_validate({"type": "snapshot", "tick": 1})


def test_snapshot_missing_contract_fields_is_rejected():
    with pytest.raises(InvalidMessageError):
        parse_message('{"type": "snapshot"}')


def _parse_invalid_fixture():
    try:
        parse_message((FIXTURES / "invalid_message.json").read_text())
    except InvalidMessageError as exc:
        return exc
    raise AssertionError("payload invalide non rejeté")


def test_invalid_fixture_raises_deterministic_error():
    first = _parse_invalid_fixture()
    second = _parse_invalid_fixture()

    assert isinstance(first, InvalidMessageError)
    assert str(first) == str(second)
    assert "tick" in str(first)


def test_bytes_payload_is_accepted():
    raw = (FIXTURES / "world_snapshot.json").read_bytes()

    message = parse_message(raw)

    assert isinstance(message, WorldSnapshot)
    assert message.model_dump(mode="json", exclude_none=True) == _golden("world_snapshot.json")


def test_non_object_json_is_rejected():
    with pytest.raises(InvalidMessageError, match="objet JSON"):
        parse_message("[1, 2, 3]")


def test_malformed_json_is_rejected():
    with pytest.raises(InvalidMessageError, match="JSON"):
        parse_message("pas du json")


def test_negative_tick_is_rejected():
    payload = {"type": "decision_made", "tick": -3}

    with pytest.raises(InvalidMessageError, match="tick"):
        parse_message(json.dumps(payload))
