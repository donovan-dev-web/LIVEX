import json
from pathlib import Path

from echos.ingestion import (
    ExternalEvent,
    WorldSnapshot,
    parse_message,
)

FIXTURES = Path(__file__).resolve().parent / "fixtures"


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text(encoding="utf-8"))


def _raw(name: str) -> str:
    return (FIXTURES / name).read_text(encoding="utf-8")


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


def test_u8_snapshot_preserves_seasons_territories_books_and_engine_version():
    from echos.ingestion.models import WorldSnapshot, parse_message

    payload = {
        "type": "snapshot", "version": "0.1.0", "engineVersion": "0.11.0",
        "runId": "run-17", "tick": 12, "simulatedTimeMinutes": 12,
        "aliveCount": 1,
        "agents": [{"id": "1", "species": "Human", "position": {"x": 3, "y": 4},
                    "energy": 80, "hunger": 10, "thirst": 20, "fatigue": 5}],
        "resources": [{"type": "food", "quantity": 75.0}],
        "groups": [], "season": "spring", "seasonIndex": 0, "territories": [],
        "books": [{"id": "b-1", "authorId": 1, "title": "Notes", "content": "Seed",
                   "writtenTick": 8, "readCount": 1, "readers": [2]}],
    }

    message = parse_message(json.dumps(payload))

    assert isinstance(message, WorldSnapshot)
    assert message.engine_version == "0.11.0"
    assert message.resources[0].quantity == 75.0
    # Les identifiants d'agents sont normalisés en ``str`` : SYNE émet
    # ``authorId``/``readers`` en entiers alors que ``agents[].id`` est une
    # chaîne. Le round-trip réémet donc la forme normalisée, pas l'entière.
    assert message.books[0].author_id == "1"
    assert message.books[0].readers == ["2"]
    dumped = message.model_dump(mode="json", by_alias=True, exclude_none=True)
    assert dumped["books"] == [
        {"id": "b-1", "authorId": "1", "title": "Notes", "content": "Seed",
         "writtenTick": 8, "readCount": 1, "readers": ["2"]}
    ]
    assert {key: value for key, value in dumped.items() if key != "books"} == {
        key: value for key, value in payload.items() if key != "books"
    }


def test_u8_book_events_keep_typed_reader_and_write_payloads():
    from echos.ingestion.models import parse_message

    written = parse_message(json.dumps({
        "type": "world.book_written", "tick": 8, "agentId": "1", "targetId": "b-1",
        "value": {"id": "b-1", "title": "Notes", "writtenTick": 8, "cost": 20.0},
    }))
    read = parse_message(json.dumps({
        "type": "world.book_read", "tick": 9, "agentId": "2", "targetId": "b-1",
        "value": {"id": "b-1", "readBenefit": 1.0},
    }))

    assert written.type == "world.book_written"
    assert written.agent_id == "1" and written.target_id == "b-1"
    assert read.type == "world.book_read"
    assert read.value == {"id": "b-1", "readBenefit": 1.0}


def test_group_members_join_agent_ids_despite_the_numeric_transport():
    """Les ids de membres sont normalisés : un groupe rejoint ses agents.

    SYNE sérialise ``agents[].id`` en chaîne mais ``groups[].members`` et
    ``leaderId`` en entiers (``ulong``). Typés en ``int``, ces champs ne
    pouvaient jamais être joints à ``Agent.id`` (``"3" != 3``) : tout
    consommation de ``groups`` aurait produit des identifiants d'une autre
    nature que ceux de ``trust``, ``beliefs`` ou ``events_log.agent_id``.
    """
    from echos.ingestion.models import parse_message

    payload = json.dumps({
        "type": "snapshot", "version": "0.1.0", "runId": "run-19", "tick": 4,
        "simulatedTimeMinutes": 4, "aliveCount": 2,
        "agents": [
            {"id": "1", "species": "Human", "position": {"x": 1, "y": 1},
             "energy": 50, "hunger": 0, "thirst": 0},
            {"id": "2", "species": "Human", "position": {"x": 2, "y": 2},
             "energy": 50, "hunger": 0, "thirst": 0},
        ],
        "groups": [{
            "groupId": 1, "members": [1, 2], "size": 2, "leaderId": 2,
            "bornTick": 0, "cohesion": 0.9, "consensus": 0.8,
        }],
    })

    message = parse_message(payload)
    group = message.groups[0]

    assert group.members == ["1", "2"]
    assert group.leader_id == "2"
    assert sorted(group.members) == sorted(agent.id for agent in message.agents)
    assert group.leader_id in {agent.id for agent in message.agents}
    # group_id reste un entier : c'est l'identité du groupe, jamais un agent.
    assert group.group_id == 1 and isinstance(group.group_id, int)


def test_group_members_are_accepted_in_string_form_too():
    """La forme chaîne reste acceptée (mock, rejeu, snapshots ECHOS)."""
    from echos.ingestion.models import parse_message

    message = parse_message(json.dumps({
        "type": "snapshot", "version": "0.1.0", "runId": "run-20", "tick": 1,
        "simulatedTimeMinutes": 1, "aliveCount": 1,
        "agents": [{"id": "A", "species": "A", "position": {"x": 0, "y": 0},
                    "energy": 1, "hunger": 0, "thirst": 0}],
        "groups": [{"groupId": 1, "members": ["A"], "size": 1, "bornTick": 0,
                    "cohesion": 1.0, "consensus": 1.0}],
    }))

    assert message.groups[0].members == ["A"]
