"""Reconstruction de chaînes causales (ECHOS-060 → ECHOS-063, jalon ph6).

``build_chain`` reconstruit la chaîne ``Action ← Intention ← Objectif ←
Besoin ← Croyance ← Mémoire ← Perception`` d'une entité à un tick, en mode
**hors ligne** sur les traces persécutées (ADR-002 [Accepted]) :
- source principale : ligne ``decision_traces`` (action, utilité, besoins,
  compteurs BDI) du tick ;
- compléments : événement ``decision_made`` correspondant (intention) et
  messages ``message_received`` (perception) de ``events_log`` ; contexte
  ``agents`` du tick le plus récent ≤ tick (objectifs, croyances, mémoire).

Contraintes de déterminisme (ECHOS-041/045, exports reproductibles) :
- aucun horodatage d'émission, aucun tirage ;
- ordres stables : besoins par (valeur desc, clé), croyances par sujet trié,
  perceptions par (tick desc, événement id desc) ;
- chaque couche de la chaîne contribue **au plus un nœud** ; les multiples
  (croyances, perceptions) sont agrégés dans ``detail``.

Boucles de rétroaction (ECHOS-062, CAUSAL_ANALYSIS.md §4.5.3) :
- **récurrence** : si l'entité a déjà choisi la même action aux ticks
  précédents du run, ``cycle=True`` et ``cycles`` liste les occurrences
  (``{"layer": "Action", "label": …}`` + ``ticks``) — on **n'étend pas**
  l'historique (profondeur d'affichage bornée) ;
- **duplicats intra-chaîne** : si un nœud répète un ``(couche, libellé)`` déjà
  émis, la chaîne s'arrête au seuil du retour (``cutoff``).

Les résultats sont prêts à être **mis en cache** (ECHOS-063, ``CausalCache``)
et invalidés sur ``AnalyticsStore.ingest_version`` (re-run ⇒ re-analyse).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

from echos.storage.sqlite import AnalyticsStore

LAYERS = (
    "Action",
    "Intention",
    "Objectif",
    "Besoin",
    "Croyance",
    "Mémoire",
    "Perception",
)
"""Couches de la chaîne causale, de l'action (profondeur 0) vers la génération."""

DEFAULT_DEPTH = 7
MAX_DEPTH = 12
"""Profondeur d'affichage par défaut et plafond (ECHOS-062, §4.5.3)."""

_IMPERCEPTION = "—"
"""Libellé d'indisponibilité quand la couche n'a pas de donnée persistée."""


@dataclass(frozen=True)
class Node:
    """Nœud d'une chaîne causale : couche, tick, libellé, détail contextuel."""

    layer: str
    tick: int
    label: str
    detail: dict[str, Any] = field(default_factory=dict)


class CausalError(ValueError):
    """Erreur métier de reconstruction (traduite en 4xx par l'API REST)."""


def _trace_for(
    store: AnalyticsStore, run_id: str, agent_id: str, tick: int
) -> dict:
    traces = store.decision_traces(run_id, tick)
    for trace in traces:
        if trace["agent_id"] == agent_id:
            return trace
    raise CausalError(f"aucune trace de décision (run={run_id}, tick={tick}, entité={agent_id})")


def _intention_of(store: AnalyticsStore, run_id: str, agent_id: str, tick: int) -> str | None:
    """Intention de l'événement ``decision_made`` du tick (repli None)."""
    for event in store.events(run_id):
        if (
            event[1] == "decision_made"
            and str(event[2]) == agent_id
            and int(event[0]) == tick
        ):
            value: dict = event[5]
            parsed: Any = value
            if isinstance(value, str):
                import json  # local : lecture pivot d'un champ optionnel

                try:
                    parsed = json.loads(value)
                except ValueError:
                    parsed = {}
            intention = parsed.get("intention") if isinstance(parsed, dict) else None
            return str(intention) if intention else str(event[3] or "")
    return None


def _agent_at(
    store: AnalyticsStore, run_id: str, agent_id: str, tick: int
) -> tuple[int, dict] | None:
    """Snapshot de l'entité au contexte ``agents`` le plus récent ≤ tick."""
    observation = store.context_before(run_id, "agents", tick)
    if observation is None:
        return None
    observed_tick, agents = observation
    agent = next(
        (item for item in agents if str(item.get("id")) == agent_id), None
    )
    if agent is None:
        return None
    return observed_tick, agent


def _perceptions(
    store: AnalyticsStore, run_id: str, agent_id: str, tick: int, limit: int = 3
) -> list[dict]:
    """Messages ``message_received`` perçus par l'entité, les plus récents.

    ``store.events`` est trié par (tick, id) : on parcourt la liste complète
    pour retenir les ``limit`` derniers ≤ tick (ordre stable).
    """
    received: list[dict] = []
    for event in store.events(run_id):
        if (
            event[1] == "message_received"
            and str(event[2]) == agent_id
            and int(event[0]) <= tick
        ):
            value: Any = event[5]
            parsed: Any = value
            if isinstance(value, str):
                import json

                try:
                    parsed = json.loads(value)
                except ValueError:
                    parsed = {}
            detail = parsed if isinstance(parsed, dict) else {}
            received.append(
                {
                    "tick": int(event[0]),
                    "type": str(event[3] or ""),
                    "detail": {str(k): v for k, v in detail.items()},
                }
            )
    return received[-limit:]


def _needs_ranked(needs: dict) -> list[tuple[str, float]]:
    """Besoins triés par (valeur desc, nom asc) — le plus impératif d'abord."""
    return sorted(needs.items(), key=lambda item: (-float(item[1]), str(item[0])))


def _goals_of(agent: dict | None) -> list[str]:
    if not agent:
        return []
    return [str(goal.get("kind") or "") for goal in agent.get("goals") or [] if goal.get("kind")]


def _beliefs_of(agent: dict | None) -> list[str]:
    if not agent:
        return []
    return sorted(
        {
            str(belief.get("subject") or "")
            for belief in agent.get("beliefs") or []
            if belief.get("subject")
        }
    )


def _recurrence(
    store: AnalyticsStore, run_id: str, agent_id: str, action: str, tick: int
) -> list[int]:
    """Ticks antérieurs où l'entité a déjà choisi la même action.

    Fenêtre bornée (16 dernières occurrences) — boucle de rétroaction
    (ECHOS-062, §4.5.3 : comportement répété = boucle, affichage borné).
    Tri stable croissant (déterminisme des exports reproductibles).
    """
    sample = min(16, len(store.decision_traces(run_id)) or 1)
    return [
        int(trace["tick"])
        for trace in store.decision_traces(run_id)[-sample:]
        if int(trace["tick"]) < tick
        and trace["agent_id"] == agent_id
        and trace["chosen_action"] == action
    ]


def _find_cycle(nodes: list[Node]) -> tuple[list[tuple[str, str]], int]:
    """Boucles de rétroaction (ECHOS-062, §4.5.3).

    Retourne ``(cycles, cutoff)`` : les couples ``(couche, libellé)`` répétés
    et l'indice d'arrêt — la position de la **deuxième** occurrence du premier
    cycle (la chaîne affichée s'arrête juste avant le retour). Sans cycle,
    ``cutoff`` vaut ``len(nodes)``.
    """
    first_seen: dict[tuple[str, str], int] = {}
    cycles: list[tuple[str, str]] = []
    cutoff = len(nodes)
    for index, node in enumerate(nodes):
        key = (node.layer, node.label)
        if key in first_seen:
            cycles.append(key)
            cutoff = index
            break
        first_seen[key] = index
    return cycles, cutoff


def build_chain(
    store: AnalyticsStore,
    run_id: str,
    agent_id: str,
    tick: int | None = None,
    depth: int = DEFAULT_DEPTH,
    max_depth: int = MAX_DEPTH,
) -> dict:
    """Reconstruit la chaîne causale hors ligne d'une entité à un tick.

    ``tick`` optionnel : dernier tick portant une trace pour l'entité.
    ``depth`` dans [1, max_depth] borne le nombre de nœuds servis (les plus
    profonds sont tronqués, ``truncated=True``). ``max_depth`` > 12 interdit
    (CAUSAL_ANALYSIS.md §4.5.3 — profondeur d'affichage limitée).

    Retourne un dict stable : ``chain`` (nœuds ``{layer, tick, label,
    detail}``), ``cycle``/``cycles``, ``truncated``, profondeurs demandées et
    servies. La reconstruction est purement lecture — **aucune écriture** dans
    le monde observé (règle d'or §4.10.3).
    """
    if isinstance(depth, bool) or not isinstance(depth, int) or depth < 1:
        raise CausalError("depth doit être un entier >= 1")
    if max_depth > 12:
        raise CausalError("max_depth est plafonné à 12 (CAUSAL_ANALYSIS.md §4.5.3)")

    if tick is None:
        tick = store.latest_decision_tick(run_id, str(agent_id))
        if tick is None:
            raise CausalError(
                f"aucune trace de décision pour l'entité {agent_id} sur le run {run_id}"
            )
    tick = int(tick)

    trace = _trace_for(store, run_id, str(agent_id), tick)
    observation = _agent_at(store, run_id, str(agent_id), tick)
    agent = observation[1] if observation else None

    action = trace["chosen_action"]
    intention = _intention_of(store, run_id, str(agent_id), tick) or action
    goals = _goals_of(agent)
    needs = dict(trace.get("needs") or {})
    ranked = _needs_ranked(needs)
    beliefs = _beliefs_of(agent)
    memory_count = int(trace.get("memory_count") or 0)
    perceptions = _perceptions(store, run_id, str(agent_id), tick)

    nodes: list[Node] = [
        Node(
            layer="Action",
            tick=tick,
            label=action,
            detail={
                "utility": float(trace.get("utility") or 0.0),
                "deliberated": bool(trace.get("deliberated")),
                "interrupted": bool(trace.get("interrupted")),
                "cause": str(trace.get("cause") or ""),
            },
        ),
        Node(
            layer="Intention",
            tick=tick,
            label=intention,
            detail={
                "deliberated": bool(trace.get("deliberated")),
                "interrupted": bool(trace.get("interrupted")),
            },
        ),
        Node(
            layer="Objectif",
            tick=tick,
            label=goals[0] if goals else _IMPERCEPTION,
            detail={"kinds": goals, "goals_count": len(goals)},
        ),
        Node(
            layer="Besoin",
            tick=tick,
            label=f"{ranked[0][0]} ({ranked[0][1]})" if ranked else _IMPERCEPTION,
            detail={"needs": needs},
        ),
        Node(
            layer="Croyance",
            tick=tick,
            label=f"{len(beliefs)} croyance(s)" if beliefs else _IMPERCEPTION,
            detail={"subjects": beliefs, "beliefs_count": len(beliefs)},
        ),
        Node(
            layer="Mémoire",
            tick=tick,
            label=f"{memory_count} souvenir(s)" if memory_count else _IMPERCEPTION,
            detail={"memory_count": memory_count},
        ),
        Node(
            layer="Perception",
            tick=tick,
            label=perceptions[-1]["type"] if perceptions else _IMPERCEPTION,
            detail={"received": perceptions},
        ),
    ]

    within_cycles, cutoff = _find_cycle(nodes)
    available = cutoff

    # Boucle de rétroaction (ECHOS-062) : la même action ayant déjà été
    # choisie aux ticks précédents marque un comportement qui se répète —
    # signalé, sans étendre l'historique (profondeur d'affichage bornée).
    recurrence = _recurrence(store, run_id, str(agent_id), action, tick)
    cycles = within_cycles + [("Action", action)] * (1 if recurrence else 0)
    cycle_ticks: dict[tuple[str, str], list[int]] = {("Action", action): recurrence}

    served_depth = max(1, min(int(depth), available))
    chain = [
        {"layer": node.layer, "tick": node.tick, "label": node.label, "detail": node.detail}
        for node in nodes[: max(1, served_depth)]
    ]

    return {
        "run_id": str(run_id),
        "agent_id": str(agent_id),
        "tick": tick,
        "depth_requested": int(depth),
        "depth_served": len(chain),
        "chain": chain,
        "cycle": bool(cycles),
        "cycles": [
            {
                "layer": layer,
                "label": label,
                **(
                    {"ticks": cycle_ticks[(layer, label)]}
                    if (layer, label) in cycle_ticks
                    else {}
                ),
            }
            for layer, label in cycles
        ],
        "truncated": len(chain) < available or len(nodes) > len(chain),
    }


__all__ = ["LAYERS", "DEFAULT_DEPTH", "MAX_DEPTH", "Node", "CausalError", "build_chain"]
