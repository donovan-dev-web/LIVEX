"""Moteur 5 — FeedbackLoopDetector (boucles de rétroaction).

Identifie les cycles où ``action → conséquence → décision`` se répètent
(METRICS_SPEC.md §6). Heuristique de détection : une boucle = une action
d'un agent répétée **plus de 2 fois** dans une fenêtre glissante (défaut 100
ticks), lue dans la clé optionnelle ``history`` du snapshot (série des
décisions par tick). Champs/historique absents → repli neutre 0.0.
"""

from __future__ import annotations

from collections import Counter

from ._common import clamp, mean

ENGINE_NAME = "FeedbackLoopDetector"

METRICS = (
    "IdentifiedLoops",
    "LoopStrength",
    "SystemStability",
    "CriticalLoops",
    "LoopTypes",
)

WINDOW_SIZE = 100
FREQUENCY_THRESHOLD = 2
AMPLIFICATION_FACTOR = 1.5

_POSITIVE_ACTIONS = frozenset(
    {"Rest", "Eat", "SeekFood", "SeekWater", "Socialize", "Explore"}
)


def _window(snapshot: dict) -> list[dict]:
    """Fenêtre glissante de décisions (MAX ``WINDOW_SIZE`` entrées récentes).

    Les entrées ``history`` sont des ``{"tick", "actions": {agentId: action}}``
    ordonnées chronologiquement ; on ne garde que la fin de la fenêtre.
    """
    history = snapshot.get("history") or []
    return list(history)[-WINDOW_SIZE:]


def _loop_counts(history: list[dict]) -> Counter[tuple[str, str]]:
    """Comptages (agent, action) sur la fenêtre — couvre la détection des boucles."""
    counts: Counter[tuple[str, str]] = Counter()
    for entry in history:
        for agent_id, action in (entry.get("actions") or {}).items():
            counts[(str(agent_id), str(action))] += 1
    return counts


def compute(snapshot: dict) -> dict:
    """Calcule les 5 métriques de détection des boucles de rétroaction."""
    history = _window(snapshot)
    counts = _loop_counts(history)

    # Boucles : (agent, action) répété > FREQUENCY_THRESHOLD fois.
    loops = {
        key: frequency for key, frequency in counts.items()
        if frequency > FREQUENCY_THRESHOLD
    }

    window_size = max(1, len(history))
    loop_strength = (
        mean([frequency / window_size for frequency in loops.values()])
        if loops
        else 0.0
    )

    # Facteur d'amplification : fréquence observée / fréquence uniforme attendue.
    distinct = len(counts)
    expected = window_size / distinct if distinct else window_size
    critical = sum(
        1
        for frequency in loops.values()
        if expected and (frequency / expected) > AMPLIFICATION_FACTOR
    )

    # Stabilité : 1 - divergence par rapport à l'équilibre (distribution uniforme).
    # Historique vide → aucune décision observée → neutre 0.0 (METRICS_SPEC §6).
    if counts:
        total = sum(counts.values())
        probabilities = [count / total for count in counts.values()]
        uniform = 1.0 / distinct
        divergence = sum(abs(prob - uniform) for prob in probabilities)
        system_stability = clamp(1.0 - divergence)
    else:
        system_stability = 0.0

    loop_types = {
        "positive": sum(1 for (_, action) in loops if action in _POSITIVE_ACTIONS),
        "negative": sum(1 for (_, action) in loops if action not in _POSITIVE_ACTIONS),
    }

    return {
        "IdentifiedLoops": len(loops),
        "LoopStrength": loop_strength,
        "SystemStability": system_stability,
        "CriticalLoops": critical,
        "LoopTypes": loop_types,
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
