"""Moteur 7 — GroupDynamicsMetrics (dynamique des groupes).

Mesure la formation, la stabilité et la rotation des groupes (METRICS_SPEC.md
§8) à partir des communautés du graphe de confiance (``trust``, U2) et des
événements ``group_formed`` / ``group_dissolved``. Repli neutre 0.0 quand les
données font défaut.
"""

from __future__ import annotations

from ._common import (
    community_sizes,
    event_values,
    events_of,
    mean,
    safe_ratio,
)

ENGINE_NAME = "GroupDynamicsMetrics"

METRICS = (
    "ActiveGroups",
    "AverageGroupSize",
    "AverageGroupLifetime",
    "GroupFormationRate",
    "GroupDissolutionRate",
    "GroupObjectiveSuccessRate",
    "MemberTurnoverRate",
)

REQUIRES = {
    "AverageGroupLifetime": "events",
    "GroupFormationRate": "events",
    "GroupDissolutionRate": "events",
    "GroupObjectiveSuccessRate": "events",
    "MemberTurnoverRate": "events",
}
"""Métriques dérivées des événements de groupe.

``ActiveGroups``/``AverageGroupSize`` lisent les communautés du tick courant :
soumises à la règle des singletons, 0 est une mesure réelle (aucune
communauté). Les cinq autres reposent sur ``group_formed``/``group_dissolved``
et retombaient sur 0.0 — un taux de formation nul observable, pas un repli.
"""

_RATE_WINDOW_TICKS = 1000
"""Fenêtre de référence (ticks) des taux normalisés.

Les taux de formation/dissolution sont exprimés « par 1000 ticks » : ils
divisent par la durée **réellement observée**, jamais par une constante, afin
qu'un unique événement isolé ne vaille pas 1000.
"""


def _rate(event_count: int, window: int) -> float:
    """Taux d'événements normalisé sur ``_RATE_WINDOW_TICKS``.

    ``window`` est la durée couverte par la fenêtre d'observation, en ticks.
    Une fenêtre de moins d'un tick (tous les événements sur un seul tick) est
    ramenée à 1 pour éviter la division par zéro.
    """
    if event_count <= 0:
        return 0.0
    return event_count * safe_ratio(float(_RATE_WINDOW_TICKS), float(max(window, 1)))


def _window_ticks(snapshot: dict) -> int:
    """Durée **observée** (ticks) servant de dénominateur aux taux.

    Trois sources, par ordre de préférence :

    1. ``eventWindow.ticks`` — durée que le pipeline **a observé** (fenêtre
       glissante bornée en ticks). C'est la source retenue : elle ne dépend pas
       de l'activité du monde, donc deux runs comparables donnent des taux
       comparables.
    2. l'étendue des ticks présents dans ``events`` — repli pour un contexte
       construit hors pipeline (tests, rejeu), où aucun ``eventWindow`` n'est
       publié.
    3. ``1`` — aucun événement : dénominateur minimal, jamais 0.

    Le repli 2 est celui qui rendait le taux dépendant de la charge : sur un
    tick très chargé, l'étendue des événements tombait à 1 tick et chaque
    formation valait 1000, alors que l'observation couvrait une hundred ticks.
    """
    published = snapshot.get("eventWindow") or {}
    if isinstance(published, dict) and published.get("ticks"):
        return max(1, int(published["ticks"]))
    ticks = [int(event.get("tick") or 0) for event in snapshot.get("events") or []]
    return max(1, max(ticks) - min(ticks) + 1) if ticks else 1


def _values_of(events: list[dict], key: str) -> list[float]:
    """Valeurs ``event.value[key]`` présentes, converties en flottants.

    Les booléens sont acceptés (0.0/1.0) : ``success`` est un drapeau, pas une
    mesure, et l'exclure vidait le taux de succès des dissolutions.
    """
    out: list[float] = []
    for event in events:
        value = event_values(event).get(key)
        if isinstance(value, (int, float, bool)):
            out.append(float(value))
    return out


def compute(snapshot: dict) -> dict:
    """Calcule les 7 métriques de dynamique des groupes sur un snapshot."""
    sizes = community_sizes(snapshot.get("agents") or [])

    # Lecture unique par type : ``events_of`` trie et copie, l'appeler quatre
    # fois (dont deux via _group_events) quadruplait le coût sans rien ajouter.
    formed = events_of(snapshot, event_type="group_formed")
    dissolved = events_of(snapshot, event_type="group_dissolved")
    window = _window_ticks(snapshot)

    lifetimes = _values_of(dissolved, "lifetime")
    success_rate = mean(_values_of(dissolved, "success"))

    # Rotation : sortants rapportés aux membres, normalisé sur la fenêtre.
    members_out = _values_of(dissolved, "membersOut")
    members_in = _values_of(dissolved, "membersIn")
    turnover = mean(
        [
            out / max(members, 1e-9) * safe_ratio(float(_RATE_WINDOW_TICKS), float(window))
            for out, members in zip(members_out, members_in)
        ]
    )

    return {
        "ActiveGroups": float(len(sizes)),
        "AverageGroupSize": mean(sizes) if sizes else 0.0,
        "AverageGroupLifetime": mean(lifetimes) if lifetimes else 0.0,
        "GroupFormationRate": _rate(len(formed), window),
        "GroupDissolutionRate": _rate(len(dissolved), window),
        "GroupObjectiveSuccessRate": success_rate,
        "MemberTurnoverRate": turnover,
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
