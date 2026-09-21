import json
from pathlib import Path

from echos.ingestion import (
    ExternalEvent,
    WorldSnapshot,
    parse_message,
)

FIXTURES = Path(__file__).resolve().parent / "fixtures"


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text())


def _raw(name: str) -> str:
    return (FIXTURES / name).read_text()


def test_world_snapshot_v01_parses_without_health():
    message = parse_message(_raw("world_snapshot_v01.json"))

    assert isinstance(message, WorldSnapshot)
    assert message.tick == 1
    assert message.run_id == "run-7"
    assert message.alive_count == 2
    agent = message.agents[0]
    assert agent.health is None
    assert agent.species == "Entité A"
    assert agent.fatigue == 61.49999999999977
    assert agent.current_action == "SeekWater"
    assert message.model_dump(mode="json", by_alias=True, exclude_none=True) == _load(
        "world_snapshot_v01.json"
    )


def test_doc_example_with_health_still_parses():
    message = parse_message(_raw("world_snapshot.json"))

    assert isinstance(message, WorldSnapshot)
    assert message.agents[0].health == 80
    assert message.agents[0].species is None


def test_decision_made_v01_parses_intention_and_utility():
    message = parse_message(_raw("decision_made_v01.json"))

    assert isinstance(message, ExternalEvent)
    assert message.type == "decision_made"
    assert message.agent_id == "1"
    assert message.action == "SeekWater"
    assert message.value == {"intention": "SeekWater", "utility": 61.7933890505355}
    assert message.model_dump(mode="json", by_alias=True, exclude_none=True) == _load(
        "decision_made_v01.json"
    )


def test_tick_summary_v01_parses_alive_count():
    message = parse_message(_raw("tick_summary_v01.json"))

    assert isinstance(message, ExternalEvent)
    assert message.type == "tick_summary"
    assert message.value == {"aliveCount": 2}
