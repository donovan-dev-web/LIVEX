# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Traces de décision SYNE consommées par ECHOS (ECHOS-051).

``build_decision_trace`` transforme un événement ``decision_made`` (contrat
SYNE) en ligne au schéma ``decision_traces``
action choisie, utilité, drapeaux de
délibération/interruption, cause, contexte BDI (croyances, objectifs,
mémoire) et besoins lus sur le snapshot — brique de l'analyse causale
Fusion déterministe : clés fixes, tri stable.

Honnêteté des données (BL-09, S-28 / D-03 / B-10) : ``utility`` est **nullable**.
Une absence (``decisionState`` ``none``) ne porte aucune utilité, et un report
(``holdover``) publie le score de sa décision d'origine sous ``held_utility``
avec son ``decision_tick`` — jamais sous ``utility`` du tick courant, et jamais
``0.0`` (un zéro observé et une absence de mesure ne se confondent pas).
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


def _optional_number(value: Any) -> float | None:
    """Nombre publié, sinon ``None`` — **jamais** ``0.0`` pour une absence.

    BL-09 (S-28 / D-03 / B-10) : SYNE publie ``utility: null`` quand aucune
    décision n'a eu lieu au tick courant et ``heldUtility`` pour le score
    reporté. Substituer ``0.0`` ici confondait un zéro observé et une absence de
    mesure — la correction est faite à la source, elle ne doit pas être défaite
    à l'ingestion.
    """
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return None
    return float(value)


def _optional_int(value: Any) -> int | None:
    """Entier publié (tick d'origine d'un report), sinon ``None``."""
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return None
    return int(value)


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

    state = value.get("decisionState")

    return {
        "run_id": str(run_id),
        "tick": int(tick),
        "agent_id": agent_id,
        "chosen_action": str(action),
        # Utilite **nullable** : None pour une absence (decisionState ``none``) et
        # pour un report, dont le score est conservé sous ``held_utility``.
        "utility": _optional_number(value.get("utility")),
        "held_utility": _optional_number(value.get("heldUtility")),
        "decision_state": str(state) if state else None,
        "decision_tick": _optional_int(value.get("decisionTick")),
        "deliberated": bool(value.get("deliberated", False)),
        "interrupted": bool(value.get("interrupted", False)),
        "cause": str(cause or ""),
        "beliefs_count": _count_of(agent, "beliefs"),
        "goals_count": _count_of(agent, "goals"),
        "memory_count": int(agent.get("memoryCount", 0)) if agent else 0,
        "needs": _needs_of(agent),
    }
