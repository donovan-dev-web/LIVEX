"""Moteur 1 — CognitiveDiversityMetrics (diversité cognitive).

Mesure la séparation des croyances et des comportements entre entités
(METRICS_SPEC.md §2). Les 8 métriques sont calculées de façon **déterministe**
sur les croyances, objectifs, actions et traits exposés par les agents SYNE
(contract U2, camelCase) avec repli neutre (0.0) si donnée absente.
"""

from __future__ import annotations

from collections import Counter

from ._common import (
    agents_of,
    alive_count,
    belief_facts,
    decision_actions,
    goal_kinds,
    mean,
    safe_ratio,
    shannon,
    variance,
)

ENGINE_NAME = "CognitiveDiversityMetrics"

METRICS = (
    "BeliefDiversity",
    "BeliefDisagreement",
    "BeliefConfidenceVariance",
    "GoalDiversity",
    "GoalConvergence",
    "DecisionDiversity",
    "IntentionStability",
    "TraitExpressionDiversity",
)


def _belief_disagreement(agents: list[dict]) -> float:
    """% moyen d'entités qui divergent sur un même fait.

    Pour chaque (sujet, prédicat) observé par ≥ 2 valeurs distinctes, part des
    croyances qui s'écartent de la valeur majoritaire, moyennée sur les faits
    concernés. 0.0 si aucun fait ne prête à divergence.

    L'ex-aequo est tranché par la valeur lexicographiquement **la plus petite**
    (et non la plus grande) : le choix majoritaire doit être celui du contrat,
    pas celui d'un ``max`` sur un tuple arbitraire.
    """
    by_fact: dict[tuple[str, str], list[str]] = {}
    for subject, predicate, value, _confidence in belief_facts(agents):
        by_fact.setdefault((subject, predicate), []).append(value)

    disagreements: list[float] = []
    for values in by_fact.values():
        value_counter = Counter(values)
        if len(value_counter) < 2:
            continue
        majority = min(value_counter, key=lambda value: (-value_counter[value], value))
        disagreements.append(
            safe_ratio(sum(1 for value in values if value != majority), len(values))
        )

    return mean(disagreements)


def _goal_ages(agents: list[dict]) -> list[float]:
    """Âges des objectifs (durée d'engagement, ``goals[].age``)."""
    return [
        float(goal.get("age") or 0.0)
        for agent in agents
        for goal in agent.get("goals") or []
    ]


def _trait_expression_variance(agents: list[dict]) -> float:
    """Variance moyenne des comportements selon les traits.

    Pour chaque trait, variance des valeurs sur la population, moyennée sur les
    traits présents. Un seul agent ou absence de traits → 0.0.
    """
    trait_names: dict[str, list[float]] = {}
    for agent in agents:
        for name, value in (agent.get("traits") or {}).items():
            trait_names.setdefault(str(name), []).append(float(value))
    if not trait_names:
        return 0.0
    return mean([variance(values) for values in trait_names.values()])


def compute(snapshot: dict) -> dict:
    """Calcule les 8 métriques de diversité cognitive sur un snapshot SYNE."""
    agents = agents_of(snapshot)
    count = alive_count(snapshot)
    facts = belief_facts(agents)
    belief_counter = Counter((subject, predicate, value) for subject, predicate, value, _ in facts)

    goal_counter = Counter(goal_kinds(agents))
    goal_convergence = safe_ratio(max(goal_counter.values()), count) if goal_counter else 0.0

    # DecisionDiversity mesure la diversité des **décisions** (``decision_made``),
    # pas celle des objectifs : les deux métriques portaient jusqu'ici sur le
    # même compteur, ce qui rendait l'une redondante de l'autre. Repli sur les
    # objectifs actifs quand le tick ne porte aucune décision observable.
    decisions = decision_actions(snapshot)
    decision_counter = Counter(decisions or goal_kinds(agents))

    return {
        "BeliefDiversity": shannon(belief_counter),
        "BeliefDisagreement": _belief_disagreement(agents),
        "BeliefConfidenceVariance": (
            variance([confidence for _, _, _, confidence in facts]) if facts else 0.0
        ),
        "GoalDiversity": shannon(goal_counter),
        "GoalConvergence": goal_convergence,
        "DecisionDiversity": safe_ratio(len(decision_counter), count),
        "IntentionStability": mean(_goal_ages(agents)),
        "TraitExpressionDiversity": _trait_expression_variance(agents),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
