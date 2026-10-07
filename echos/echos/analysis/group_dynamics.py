"""Moteur 7 — GroupDynamicsMetrics (dynamique des groupes).

Mesure deux choses **distinctes** et le dit (METRICS_SPEC.md §8, RAPPORT §3.6) :

1. les **communautés inférées** — communautés du graphe de confiance
   (propagation d'étiquettes sur ``trust``, U2). Ce ne sont pas des groupes
   déclarés par SYNE : elles portent désormais les noms ``InferredCommunities``
   et ``AverageCommunitySize`` pour ne plus être confondues avec les événements
   ``group_formed`` / ``group_dissolved`` ;
2. les **événements de groupes natifs** — ``group_formed`` /
   ``group_dissolved``, avec leurs dénominateurs bruts publiés à côté des taux
   (``FormationCount`` / ``DissolutionCount``) : normaliser une petite fenêtre
   « par 1000 ticks » amplifie mécaniquement les valeurs, d'où l'affichage du
   nombre brut et de la fenêtre observée.

Décisions P1 (registre des métriques) :

- ``GroupObjectiveSuccessRate`` → **``DissolvedGroupSuccessShare``** : la
  moyenne des booléens ``success`` ne porte que sur les groupes **dissous et
  observés**, pas sur tous les groupes — biais de sélection assumé, dénominateur
  (``DissolutionCount``) publié et provenance ``false`` sans dissolution ;
- ``MemberTurnoverRate`` → **``MemberExitsPerDissolution``** : l'ancienne
  mesure appariait ``membersOut`` et ``membersIn`` par ``zip`` (listes non
  garanties alignées), divisait par un dénominateur pouvant être nul puis
  annualisait sur 1000 ticks. Le contrat ne fournit pas l'effectif exposé en
  membres-temps : on publie donc la moyenne **observée** des sorties par
  dissolution plutôt qu'un taux inventé.

Repli neutre 0.0 quand les données font défaut (``measured = false``).
"""

from __future__ import annotations

from ._common import (
    alive_count,
    community_sizes,
    communities,
    event_window_published,
    event_values,
    events_of,
    mean,
    safe_ratio,
)

ENGINE_NAME = "GroupDynamicsMetrics"

METRICS = (
    "InferredCommunities",
    "AverageCommunitySize",
    "CommunityCoverage",
    "AverageGroupLifetime",
    "GroupFormationRate",
    "GroupDissolutionRate",
    "FormationCount",
    "DissolutionCount",
    "DissolvedGroupSuccessShare",
    "MemberExitsPerDissolution",
)


def _dissolution_published(snapshot: dict) -> bool:
    """Une dissolution est-elle observée dans la fenêtre d'événements ?"""
    return any(
        event.get("type") == "group_dissolved"
        for event in snapshot.get("events") or []
    )


REQUIRES = {
    "AverageGroupLifetime": "events",
    "GroupFormationRate": event_window_published,
    "GroupDissolutionRate": event_window_published,
    "FormationCount": event_window_published,
    "DissolutionCount": event_window_published,
    "DissolvedGroupSuccessShare": _dissolution_published,
    "MemberExitsPerDissolution": _dissolution_published,
}
"""Métriques dérivées des événements de groupe.

``InferredCommunities``/``AverageCommunitySize`` lisent les communautés du tick
courant : soumises à la règle des singletons, 0 est une mesure réelle (aucune
communauté). Les taux et comptages sont mesurés dès qu'une **fenêtre** est
publiée — 0 formation dans une fenêtre réelle est un zéro observé. Les deux
mesures par dissolution exigent au moins une dissolution : sans dénominateur,
0.0 serait non interprétable.
"""

_RATE_WINDOW_TICKS = 1000
"""Fenêtre de référence (ticks) des taux normalisés.

Les taux de formation/dissolution sont exprimés « par 1000 ticks » : ils
divisent par la durée **réellement observée**, jamais par une constante, afin
qu'un unique événement isolé ne vaille pas 1000. Le dénominateur brut
(``FormationCount``/``DissolutionCount``) et la fenêtre restent à afficher
à côté : l'amplification d'une fenêtre courte est réelle.
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
    """Calcule les 9 métriques de dynamique des groupes sur un snapshot."""
    sizes = community_sizes(snapshot.get("agents") or [])
    members = sum(len(group) for group in communities(snapshot.get("agents") or []))
    population = alive_count(snapshot)

    # Lecture unique par type : ``events_of`` trie et copie.
    formed = events_of(snapshot, event_type="group_formed")
    dissolved = events_of(snapshot, event_type="group_dissolved")
    window = _window_ticks(snapshot)

    lifetimes = _values_of(dissolved, "lifetime")
    success_rate = mean(_values_of(dissolved, "success"))
    exits = _values_of(dissolved, "membersOut")

    return {
        # Communautés **inférées** du graphe de confiance (pas des groupes
        # natifs SYNE) : le nom le dit, la confusion était structurelle.
        "InferredCommunities": float(len(sizes)),
        "AverageCommunitySize": mean(sizes) if sizes else 0.0,
        # Part de la population rattachée à une communauté de taille ≥ 2 :
        # base défendable et bornée [0,1] pour les indices composites
        # (elle remplace l'ancien terme ``ActiveGroups / 100``).
        "CommunityCoverage": safe_ratio(members, population),
        # Événements de groupes natifs.
        "AverageGroupLifetime": mean(lifetimes) if lifetimes else 0.0,
        "GroupFormationRate": _rate(len(formed), window),
        "GroupDissolutionRate": _rate(len(dissolved), window),
        "FormationCount": float(len(formed)),
        "DissolutionCount": float(len(dissolved)),
        # Part des dissolutions observées marquées ``success`` : dénominateur =
        # nombre de dissolutions dans la fenêtre, biais de sélection assumé.
        "DissolvedGroupSuccessShare": success_rate,
        # Sorties moyennes par dissolution observée — pas un taux annualisé.
        "MemberExitsPerDissolution": mean(exits),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
