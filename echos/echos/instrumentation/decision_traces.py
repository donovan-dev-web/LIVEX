"""Traces de décision SYNE consommées par ECHOS (ECHOS-051).

``build_decision_trace`` transforme un événement ``decision_made`` (contrat
SYNE, API_CONTRACTS.md §2.2) en ligne au schéma ``decision_traces``
(LOGGING_INSTRUMENTATION.md §1/§3) : action choisie, utilité, drapeaux de
délibération/interruption, cause, contexte BDI (croyances, objectifs,
mémoire) et besoins lus sur le snapshot — brique de l'analyse causale
(CAUSAL_ANALYSIS.md). Fusion déterministe : clés fixes, tri stable.
"""

from __future__ import annotations

from typing import Any

_DECISION_TYPE = "decision_made"


def _needs_of(agent: dict | None) -> dict[str, float]:
    """Besoins d'une entité lus sur le snapshot (champs units hérités)."""
    if not agent:
        return {}
    needs: dict[str, float] = {}
    for key in ("hunger", "thirst", "fatigue", "energy"):
        value = agent.get(key)
        if isinstance(value, (int, float)) and not isinstance(value, bool):
            needs[key] = float(value)
    return needs


def _count_of(agent: dict | None, key: str) -> int:
    """Taille d'une séquence d'un agent ; 0 si absente/invalide."""
    if not agent:
        return 0
    values = agent.get(key)
    return len(values) if isinstance(values, list) else 0


def build_decision_trace(
    run_id: str,
    tick: int,
    event: Any,
    engine_snapshot: dict[str, Any],
) -> dict[str, Any]:
    """Construit la trace d'un événement ``decision_made`` du tick courant.

    ``event`` est un :class:`echos.ingestion.models.ExternalEvent` (les
    attributs ``agent_id``/``action``/``cause``/``value`` sont forcés).
    Le contexte BDI est lu sur ``engine_snapshot`` (``agents``) — jamais
    d'écriture dans le monde observé.
    """
    if str(getattr(event, "type", "")) != _DECISION_TYPE:
        raise ValueError(f"attendu {_DECISION_TYPE!r}, reçu {getattr(event, 'type', None)!r}")

    agent_id = str(getattr(event, "agent_id", "") or "")
    value = dict(getattr(event, "value", None) or {})
    cause = getattr(event, "cause", None)
    action = getattr(event, "action", None) or value.get("intention") or "Idle"

    agent = next(
        (candidate for candidate in (engine_snapshot.get("agents") or [])
         if str(candidate.get("id")) == agent_id),
        None,
    )

    return {
        "run_id": str(run_id),
        "tick": int(tick),
        "agent_id": agent_id,
        "chosen_action": str(action),
        "utility": float(value.get("utility", 0.0) or 0.0),
        "deliberated": bool(value.get("deliberated", False)),
        "interrupted": bool(value.get("interrupted", False)),
        "cause": str(cause or ""),
        "beliefs_count": _count_of(agent, "beliefs"),
        "goals_count": _count_of(agent, "goals"),
        "memory_count": int(agent.get("memoryCount", 0)) if agent else 0,
        "needs": _needs_of(agent),
    }
