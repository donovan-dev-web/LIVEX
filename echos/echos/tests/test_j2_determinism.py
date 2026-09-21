"""Preuve J2 (ECHOS-027) : la chaîne ECHOS (accumulation → moteurs) est
déterministe, bit-à-bit.

La preuve se combine au déterminisme SYNE prouvé côté moteur (seed 7, J1) :
à traces d'entrée identiques, ECHOS produit des scores de métriques
identiques. Ce test rejoue deux fois le scénario de référence
(``snapshot_analysis.json``) et vérifie que :
  1. les deux runs produisent des séries de métriques strictement égales ;
  2. le dernier contexte (tick 10) coïncide avec les golden files (ECHOS-027).
"""

import json
from copy import deepcopy
from pathlib import Path

from echos.analysis import ENGINES

FIXTURES = Path(__file__).resolve().parent / "fixtures"
GOLDEN = Path(__file__).resolve().parent / "golden"

WINDOW = 100


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text())


def contexts(ticks: int) -> list[dict]:
    """Contexte par tick : snapshot du tick + événements passés + fenêtre
    d'historique accumulée (repli des états du Nostradamus).

    Pure et déterministe : mêmes entrées → mêmes contextes.
    """
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


def run(ticks: int) -> dict:
    """Séries de métriques (par moteur, par tick) sur un rejeu complet."""
    series = {engine.ENGINE_NAME: [] for engine in ENGINES}
    for context in contexts(ticks):
        for engine in ENGINES:
            series[engine.ENGINE_NAME].append(engine.compute(context))
    return series


def test_two_replays_produce_bit_identical_series():
    first = run(ticks=10)
    second = run(ticks=10)

    assert first == second
    # Chaque moteur produit bien une série complète (pas de valeur sommaire).
    assert all(len(values) == 10 for values in first.values())


def test_last_tick_series_matches_golden_files():
    golden = _load_golden()
    last = {engine: values[-1] for engine, values in run(ticks=10).items()}

    assert last == golden
    assert _load_golden() == golden


def _load_golden() -> dict:
    return json.loads((GOLDEN / "analysis_golden.json").read_text())
