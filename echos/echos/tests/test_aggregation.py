"""Agrégation par tick sans perte (ECHOS-011) : résumé déterministe d'un
segment, conservation de tous les ticks et sous-échantillonnage paramétrable."""

import json
from pathlib import Path

from echos.ingestion import WsClient, aligned_ticks
from echos.storage.aggregation import TickRecord, downsample, summarize

FIXTURES = Path(__file__).resolve().parent / "fixtures"


class FakeTransport:
    """Transport simulé rejouant le script (déterministe)."""

    def __init__(self, payloads: list) -> None:
        self._queue = list(payloads)

    def recv(self, timeout: float | None = None) -> str | None:
        return self._queue.pop(0) if self._queue else None

    def send(self, payload: str | bytes) -> None:
        pass

    def close(self) -> None:
        pass


def _variant(name: str, tick: int) -> str:
    data = json.loads((FIXTURES / name).read_text())
    data["tick"] = tick
    return json.dumps(data)


def _script(ticks: int = 3) -> list:
    payloads = []
    for tick in range(1, ticks + 1):
        payloads.append(_variant("world_snapshot_v01.json", tick))
        payloads.append(_variant("tick_summary_v01.json", tick))
        payloads.append(_variant("decision_made_v01.json", tick))
    return payloads


def _client(payloads: list) -> WsClient:
    client = WsClient(transport=FakeTransport(payloads))
    client.connect("ws://127.0.0.1:5180")
    return client


def test_tick_record_is_deterministic_from_segment():
    client = _client(_script(1))

    segments = list(aligned_ticks(client))
    (segment,) = segments
    record = TickRecord.from_segment(segment)

    assert record.run_id == "run-7"
    assert record.version == "0.1.0"
    assert record.tick == 1
    assert record.simulated_time_minutes == 1
    assert record.alive_count == 2
    assert record.agent_count == 2
    assert record.mean_energy == 40.0
    assert record.mean_hunger == 100.0
    assert record.mean_thirst == 100.0
    assert record.mean_fatigue == 61.49999999999977  # strict (déterministe)
    assert record.decision_count == 1
    assert record.actions == (("SeekWater", 2),)


def test_two_runs_produce_identical_records():
    """La réduction est déterministe : deux lectures donnent la même ligne."""

    def _record() -> TickRecord:
        client = _client(_script(1))
        return TickRecord.from_segment(next(iter(aligned_ticks(client))))

    assert _record().to_row() == _record().to_row()


def test_summarize_keeps_every_tick_without_sampling():
    records = list(summarize(_client(_script(3))))

    assert [record.tick for record in records] == [1, 2, 3]
    assert all(record.decision_count == 1 for record in records)


def test_summarize_sample_every_keeps_one_tick_over_n():
    records = list(summarize(_client(_script(4)), sample_every=2))

    assert [record.tick for record in records] == [1, 3]


def test_downsample_keeps_first_tick_and_every_nth():
    records = list(summarize(_client(_script(5))))

    sampled = downsample(records, every=2)

    assert [record.tick for record in sampled] == [1, 3, 5]


def test_downsample_every_one_keeps_all():
    records = list(summarize(_client(_script(2))))

    assert downsample(records, every=1) == records
