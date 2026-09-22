"""Preuve J3 (ECHOS-030/033, jalon U3) : le score d'émergence est stable
entre runs identiques et borné sur [0, 1].

Complète la preuve J2 (9 moteurs : 7 de métriques + EmergenceIndicators) en
prouvant que le rejeu du scénario de référence produit une série d'indicateurs
d'émergence strictement identique (bit-à-bit) et que chaque score reste dans
l'intervalle contractuel [0, 1].
"""

import json
from copy import deepcopy
from pathlib import Path

from echos.analysis import emergence

FIXTURES = Path(__file__).resolve().parent / "fixtures"
GOLDEN = Path(__file__).resolve().parent / "golden"

WINDOW = 100


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text())


def contexts(ticks: int) -> list[dict]:
    """Contexte par tick (snapshot du tick + événements/fenêtre accumulés)."""
    final = _load("snapshot_analysis.json")
    history = final["history"][-WINDOW:]
    events = final["events"]
    contexts = []
    for tick in range(1, ticks + 1):
        context = deepcopy(final)
        context["history"] = [entry for entry in history if int(entry["tick"]) <= tick]
        context["events"] = [event for event in events if int(event["tick"]) <= tick]
        contexts.append(context)
    return contexts


def run(ticks: int) -> list[dict]:
    """Série d'indicateurs d'émergence (un par tick) sur un rejeu complet."""
    return [emergence.compute(context) for context in contexts(ticks)]


def test_two_replays_produce_bit_identical_emergence_series():
    first = run(ticks=10)
    second = run(ticks=10)

    assert first == second


def test_emergence_score_is_stable_and_bounded_on_replay():
    series = run(ticks=10)

    for frame in series:
        assert 0.0 <= frame["EmergenceScore"] <= 1.0


def test_last_emergence_frame_matches_golden():
    golden = json.loads((GOLDEN / "analysis_golden.json").read_text())
    last = run(ticks=10)[-1]

    assert last == golden["EmergenceIndicators"]
