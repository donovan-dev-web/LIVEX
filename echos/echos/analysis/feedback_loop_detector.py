"""Moteur 5 — FeedbackLoopDetector (répétitions d'action).

**Portée réelle (P1, RAPPORT §3.5)** : le moteur compte des paires
``(agent, action)`` répétées dans une fenêtre glissante de décisions. Il
n'observe ni conséquence, ni relation action → conséquence, ni retour de la
conséquence sur la décision : ce n'est **pas** une boucle causale. Les mots
« boucle », « amplification », « critique » et « stabilité » ont été retirés
de la nomenclature publique ; le nom du moteur est conservé pour stabilité de
contrat, sa nomenclature décrit ce qui est compté.

Décisions P1 (registre des métriques) :

- ``IdentifiedLoops`` → **``RepeatedActionPairs``** : nombre de paires
  (agent, action) répétées plus de ``FREQUENCY_THRESHOLD`` fois ;
- ``LoopStrength`` → **``RepeatedActionShare``** : part moyenne de la fenêtre
  occupée par ces répétitions (fréquence / taille de fenêtre), pas un facteur
  d'amplification mesuré ;
- ``CriticalLoops`` → **``AmplifiedRepetitions``** : paires dont la fréquence
  dépasse de ``AMPLIFICATION_FACTOR`` la fréquence uniforme attendue — un
  écart à une référence théorique, pas un danger observé ;
- ``SystemStability`` → **``ActionDistributionBalance``** : 1 − divergence
  (écart absolu total) à la distribution uniforme. Ce n'est pas une stabilité
  temporelle : la distribution uniforme n'est pas un état d'équilibre établi ;
- ``LoopTypes`` → **``RepeatedActionCounts``** : comptage des actions
  répétées par nom. Les catégories normalisées « positive / négative »,
  codées en dur sans mesure d'effet, sont **retirées** (P2).

Champs/historique absents → repli neutre 0.0 (``measured = false``). Pour une
analyse causale réelle (contexte → option → action → conséquence → récidive),
voir ``echos.analysis.causal`` (ECHOS-061) et `CAUSAL_ANALYSIS.md`.
"""

from __future__ import annotations

from collections import Counter

from ._common import clamp, mean

ENGINE_NAME = "FeedbackLoopDetector"

METRICS = (
    "RepeatedActionPairs",
    "RepeatedActionShare",
    "ActionDistributionBalance",
    "AmplifiedRepetitions",
    "RepeatedActionCounts",
)

REQUIRES = {
    metric: "history"
    for metric in METRICS
    if metric != "RepeatedActionCounts"
}
"""Toutes les métriques numériques lisent ``history``.

Ce sont les métriques qui restaient à 0.0 sur tout run tant que le pipeline ne
publiait pas la fenêtre glissante : sans historique, « aucune répétition
détectée » et « aucune répétition mesurable » étaient indiscernables.

``RepeatedActionCounts`` est exclu car c'est une sortie composite (dict de
comptages), jamais persistée comme métrique numérique.
"""

WINDOW_SIZE = 100
FREQUENCY_THRESHOLD = 2
AMPLIFICATION_FACTOR = 1.5


def _window(snapshot: dict) -> list[dict]:
    """Fenêtre glissante de décisions (MAX ``WINDOW_SIZE`` entrées récentes).

    Les entrées ``history`` sont des ``{"tick", "actions": {agentId: action}}``
    ordonnées chronologiquement ; on ne garde que la fin de la fenêtre.
    """
    history = snapshot.get("history") or []
    return list(history)[-WINDOW_SIZE:]


def _loop_counts(history: list[dict]) -> Counter[tuple[str, str]]:
    """Comptages (agent, action) sur la fenêtre — couvre la détection."""
    counts: Counter[tuple[str, str]] = Counter()
    for entry in history:
        for agent_id, action in (entry.get("actions") or {}).items():
            counts[(str(agent_id), str(action))] += 1
    return counts


def compute(snapshot: dict) -> dict:
    """Calcule les 5 sorties de répétition d'action sur une fenêtre."""
    history = _window(snapshot)
    counts = _loop_counts(history)

    # Paires (agent, action) répétées > FREQUENCY_THRESHOLD fois.
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

    # Écart à la fréquence uniforme théorique (référence théorique, pas un
    # risque observé) : fréquence observée > AMPLIFICATION_FACTOR × attendue.
    distinct = len(counts)
    expected = window_size / distinct if distinct else window_size
    critical = sum(
        1
        for frequency in loops.values()
        if expected and (frequency / expected) > AMPLIFICATION_FACTOR
    )

    # Équilibre de la distribution observée : 1 − Σ|pᵢ − 1/k|, clampé [0,1].
    # Historique vide → aucune décision observée → neutre 0.0.
    if counts:
        total = sum(counts.values())
        probabilities = [count / total for count in counts.values()]
        uniform = 1.0 / distinct
        divergence = sum(abs(prob - uniform) for prob in probabilities)
        balance = clamp(1.0 - divergence)
    else:
        balance = 0.0

    # Comptage des actions répétées, par nom — sans catégorie normative.
    # Invariant vérifié par test : Σ valeurs == RepeatedActionPairs.
    repeated_actions: Counter[str] = Counter()
    for (_agent_id, action) in loops:
        repeated_actions[action] += 1

    return {
        "RepeatedActionPairs": len(loops),
        "RepeatedActionShare": loop_strength,
        "ActionDistributionBalance": balance,
        "AmplifiedRepetitions": critical,
        "RepeatedActionCounts": {
            action: repeated_actions[action] for action in sorted(repeated_actions)
        },
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
