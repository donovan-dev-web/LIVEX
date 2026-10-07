"""Moteur 1 — CognitiveDiversityMetrics (diversité cognitive).

Mesure la séparation des croyances et des comportements entre entités
(METRICS_SPEC.md §2). Les métriques sont calculées de façon **déterministe**
sur les croyances, objectifs, décisions et traits exposés par les agents SYNE
(contract U2, camelCase) avec repli neutre (0.0) si donnée absente.

Décisions P1 (registre des métriques) :

- ``DecisionDiversity`` → **``ActionDiversity``** : le ratio « types d'action
  / entités » n'est pas une diversité de distribution et changeait de
  sémantique selon le repli sur les objectifs. La mesure retenue est
  l'entropie de Shannon **normalisée** des décisions observées au tick,
  accompagnée de son dénominateur ``DecisionCount``. Bouts et actions sont
  désormais séparés : plus aucun repli silencieux d'un compte sur l'autre ;
- ``IntentionStability`` → **``AverageGoalAge``** : l'âge moyen d'un objectif
  est une durée, pas une stabilité (qui supposerait un suivi d'identité dans
  le temps) ;
- ``GoalCoverage`` expose la part d'entités réellement dotées d'un objectif
  déclaré, pour distinguer « distribution de buts » et « repli sur l'action
  courante ».
"""

from __future__ import annotations

from collections import Counter

from ._common import (
    agents_of,
    alive_count,
    belief_facts,
    decision_actions,
    event_window_published,
    goal_kinds,
    mean,
    safe_ratio,
    shannon,
    shannon_normalized,
    variance,
)

ENGINE_NAME = "CognitiveDiversityMetrics"

METRICS = (
    "BeliefDiversity",
    "BeliefDiversityNorm",
    "BeliefDisagreement",
    "BeliefConfidenceVariance",
    "GoalDiversity",
    "GoalDiversityNorm",
    "GoalConvergence",
    "GoalCoverage",
    "ActionDiversity",
    "DecisionCount",
    "AverageGoalAge",
    "TraitExpressionDiversity",
)


def _has_goal(agent: dict) -> bool:
    return bool(agent.get("goals"))


def _goal_observed(snapshot: dict) -> bool:
    """Vrai si au moins une entité déclare un objectif à ce tick.

    Sans objectif déclaré, ``goal_kinds`` retombe sur ``currentAction`` /
    ``Idle`` : la distribution observée décrit alors des **actions**, pas des
    buts. Signaler « non mesuré » évite de présenter ce repli comme une
    distribution d'objectifs.
    """
    return any(_has_goal(agent) for agent in agents_of(snapshot))


REQUIRES = {
    "GoalDiversity": _goal_observed,
    "GoalDiversityNorm": _goal_observed,
    "BeliefDiversityNorm": lambda snapshot: bool(belief_facts(agents_of(snapshot))),
    "ActionDiversity": event_window_published,
    "DecisionCount": event_window_published,
}
"""Provenance des métriques à repli (zéro observé ≠ donnée absente).

``ActionDiversity``/``DecisionCount`` sont mesurés dès qu'une fenêtre
 d'événements est publiée : 0 décision dans une fenêtre réelle est un **zéro
observé**, pas une donnée absente. ``GoalDiversity`` est non mesurée quand
aucune entité ne déclare d'objectif — la distribution se construirait alors
sur le repli ``currentAction``/``Idle``, c'est-à-dire sur des actions.
"""


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
    """Calcule les 12 métriques de diversité cognitive sur un snapshot SYNE."""
    agents = agents_of(snapshot)
    count = alive_count(snapshot)
    facts = belief_facts(agents)
    belief_counter = Counter((subject, predicate, value) for subject, predicate, value, _ in facts)

    goal_counter = Counter(goal_kinds(agents))
    goal_convergence = safe_ratio(max(goal_counter.values()), count) if goal_counter else 0.0
    goal_coverage = safe_ratio(sum(1 for agent in agents if _has_goal(agent)), count)

    # ActionDiversity porte sur les **décisions observées** (``decision_made``),
    # jamais sur les objectifs : les deux notions sont désormais séparées, et
    # l'entropie normalisée est bornée [0, 1] quelle que soit la taille du
    # répertoire d'actions. Aucune décision ce tick → 0.0 et ``measured=false``
    # (REQUIRES), pour ne pas présenter « rien à mesurer » comme « aucun choix ».
    decisions = decision_actions(snapshot)
    decision_counter = Counter(decisions)

    return {
        "BeliefDiversity": shannon(belief_counter),
        # Version normalisée [0,1] : c'est elle qui alimente les composites.
        # L'entropie brute (bits) croît avec le nombre d'énoncés distincts et
        # ne peut pas être additionnée à d'autres termes sans saturer le clamp.
        "BeliefDiversityNorm": shannon_normalized(belief_counter),
        "BeliefDisagreement": _belief_disagreement(agents),
        "BeliefConfidenceVariance": (
            variance([confidence for _, _, _, confidence in facts]) if facts else 0.0
        ),
        "GoalDiversity": shannon(goal_counter),
        "GoalDiversityNorm": shannon_normalized(goal_counter),
        "GoalConvergence": goal_convergence,
        "GoalCoverage": goal_coverage,
        "ActionDiversity": shannon_normalized(decision_counter),
        "DecisionCount": float(len(decisions)),
        "AverageGoalAge": mean(_goal_ages(agents)),
        "TraitExpressionDiversity": _trait_expression_variance(agents),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
