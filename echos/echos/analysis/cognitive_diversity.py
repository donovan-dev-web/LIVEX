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
    mean,
    shannon,
    variance,
    activity_of,
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

_POSITIVE_ACTIONS = frozenset({"Rest", "Eat", "Socialize", "Explore", "SeekFood"})


def _belief_facts(agents: list[dict]) -> list[tuple[str, str, str, float]]:
    """Croyances agrégées (subject, predicate, value, confidence) par agent."""
    facts: list[tuple[str, str, str, float]] = []
    for agent in agents:
        for belief in agent.get("beliefs") or []:
            facts.append(
                (
                    str(belief.get("subject")),
                    str(belief.get("predicate")),
                    str(belief.get("value")),
                    float(belief.get("confidence") or 0.0),
                )
            )
    return facts


def _belief_disagreement(agents: list[dict]) -> float:
    """% moyen d'entités qui divergent sur un même fait.

    Pour chaque (sujet, prédicat) observé par ≥ 2 croyances avec ≥ 2 valeurs
    distinctes, part des croyances qui s'écartent de la valeur majoritaire,
    moyennée sur les faits concernés. 0.0 si aucun fait ne prête à divergence.
    """
    by_fact: dict[tuple[str, str], list[tuple[str, float]]] = {}
    for subject, predicate, value, _confidence in _belief_facts(agents):
        by_fact.setdefault((subject, predicate), []).append((value, _confidence))

    disagreements: list[float] = []
    for values in by_fact.values():
        value_counter = Counter(value for value, _ in values)
        if len(value_counter) < 2:
            continue
        majority = max(value_counter, key=lambda v: (value_counter[v], v))
        diverging = sum(1 for value, _ in values if value != majority)
        if len(values) > 0:
            disagreements.append(diverging / len(values))

    return mean(disagreements)


def _goal_kinds(agents: list[dict]) -> list[str]:
    """Types d'objectifs actifs (repli sur l'action courante, Idle sinon)."""
    return [activity_of(agent) for agent in agents]


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
    facts = _belief_facts(agents)
    belief_counter = Counter((subject, predicate, value) for subject, predicate, value, _ in facts)

    goal_counter = Counter(_goal_kinds(agents))
    goal_convergence = (
        max(goal_counter.values()) / count if count and goal_counter else 0.0
    )

    actions = [activity_of(agent) for agent in agents]
    decision_counter = Counter(actions)

    return {
        "BeliefDiversity": shannon(belief_counter),
        "BeliefDisagreement": _belief_disagreement(agents),
        "BeliefConfidenceVariance": (
            variance([confidence for _, _, _, confidence in facts]) if facts else 0.0
        ),
        "GoalDiversity": shannon(goal_counter),
        "GoalConvergence": goal_convergence,
        "DecisionDiversity": (
            len(decision_counter) / count if count else 0.0
        ),
        "IntentionStability": mean(_goal_ages(agents)),
        "TraitExpressionDiversity": _trait_expression_variance(agents),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
