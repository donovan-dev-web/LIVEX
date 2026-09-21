"""Utilitaires déterministes communs aux 7 moteurs (METRICS_SPEC.md §9).

Toutes les fonctions sont **pures** : mêmes entrées → mêmes sorties (aucune
consommation de PRNG, ordres stables par tri sur les identifiants). Elles
tolèrent l'absence de données et retournent 0.0 (neutral) en l'absence de
mesures, conformément au contrat V0.1 (calibration à venir).
"""

from __future__ import annotations

from collections import Counter
from math import log2


def shannon(counter: Counter) -> float:
    """Entropie de Shannon H(P) = -Σ p·log2(p) d'une distribution de comptages.

    Sur un compteur vide → 0.0 (aucune diversité mesurable).
    """
    total = sum(counter.values())
    if total <= 0:
        return 0.0
    return -sum(
        (count / total) * log2(count / total) for count in counter.values() if count > 0
    )


def mean(values: list[float]) -> float:
    """Moyenne arithmétique (0.0 sur une liste vide)."""
    return (sum(values) / len(values)) if values else 0.0


def variance(values: list[float]) -> float:
    """Variance de population (ddof=0) ; 0.0 sur une liste vide ou d'un élément."""
    if len(values) < 2:
        return 0.0
    m = mean(values)
    return sum((value - m) ** 2 for value in values) / len(values)


def clamp(value: float, low: float = 0.0, high: float = 1.0) -> float:
    """Borne une valeur dans [low, high]."""
    return max(low, min(high, value))


def keyed(counter: Counter) -> dict[str, int]:
    """Vue triée (clé ordinale) d'un compteur — ordre d'émission déterministe."""
    return {key: counter[key] for key in sorted(counter, key=str)}


def agents_of(snapshot: dict) -> list[dict]:
    """Agents du snapshot (transport camelCase, clé ``agents``)."""
    return list(snapshot.get("agents") or [])


def alive_count(snapshot: dict) -> int:
    """Nombre d'entités vivantes (``aliveCount``, ou longueur des agents en repli)."""
    value = snapshot.get("aliveCount")
    return int(value) if value is not None else len(agents_of(snapshot))


def activity_of(snapshot: dict) -> str:
    """Vue déterministe d'activité (objectif courant) d'un agent.

    Privilégie le premier objectif ``goals[].kind`` puis ``currentAction``
    sinon ``Idle`` — l'identifiant sert de clé de regroupement des décisions.
    """
    goals = snapshot.get("goals") or []
    if goals and goals[0].get("kind"):
        return str(goals[0]["kind"])
    action = snapshot.get("currentAction")
    return str(action) if action else "Idle"


def trust_edges(agents: list[dict]) -> set[frozenset[str]]:
    """Arêtes de confiance non orientées (pair, pair) pour toute relation > 0.

    Déduplique les arêtes (a→b et b→a fusionnées) pour les métriques de graphe.
    """
    edges: set[frozenset[str]] = set()
    for agent in agents:
        agent_id = str(agent.get("id"))
        for relation in agent.get("trust") or []:
            if (relation.get("trust") or 0.0) > 0.0:
                peer = str(relation.get("peerId"))
                edges.add(frozenset((min(agent_id, peer), max(agent_id, peer))))
    return edges


def trust_edges_directed(agents: list[dict]) -> set[tuple[str, str]]:
    """Arêtes de confiance orientées (émetteur → pair) pour les relations > 0."""
    edges: set[tuple[str, str]] = set()
    for agent in agents:
        agent_id = str(agent.get("id"))
        for relation in agent.get("trust") or []:
            if (relation.get("trust") or 0.0) > 0.0:
                edges.add((agent_id, str(relation.get("peerId"))))
    return edges


def label_propagation(agents: list[dict]) -> dict[str, str]:
    """Détection de communautés déterministe par propagation d'étiquettes.

    Labels initiaux = identifiant ; mise à jour **asynchrone** (en place) dans
    l'ordre trié des identifiants, étiquette la plus fréquente chez les voisins
    (ex-aequo → plus petite), au plus 10 itérations — convergente sur les
    graphes non orientés. Retourne {agentId → communauté (label)}.
    """
    edges = trust_edges(agents)
    ids = sorted({agent_id for agent in agents for agent_id in (str(agent.get("id")),)})
    neighbors: dict[str, set[str]] = {agent_id: set() for agent_id in ids}
    for edge in edges:
        a, b = tuple(edge)
        if a in neighbors and b in neighbors:
            neighbors[a].add(b)
            neighbors[b].add(a)

    labels = {agent_id: agent_id for agent_id in ids}
    for _ in range(10):
        changed = False
        for agent_id in ids:
            label_counts: Counter[str] = Counter()
            for neighbor in sorted(neighbors[agent_id]):
                label_counts[labels[neighbor]] += 1
            if not label_counts:
                continue
            chosen = min(
                label
                for label, count in label_counts.items()
                if count == max(label_counts.values())
            )
            if chosen != labels[agent_id]:
                labels[agent_id] = chosen
                changed = True
        if not changed:
            break

    return labels


def community_sizes(agents: list[dict]) -> list[int]:
    """Tailles des communautés (groupes) détectées, ordre décroissant stable."""
    labels = label_propagation(agents)
    sizes = sorted((Counter(labels.values()).values()), reverse=True)
    return [int(size) for size in sizes]


def events_of(snapshot: dict, event_type: str | None = None) -> list[dict]:
    """Événements du snapshot (clé ``events``), éventuellement filtrés par type.

    Les événements sont ordonnés par ``tick`` puis ``agentId`` (déterminisme).
    """
    events = [dict(event) for event in snapshot.get("events") or []]
    if event_type is not None:
        events = [event for event in events if event.get("type") == event_type]
    return sorted(
        events,
        key=lambda event: (int(event.get("tick", 0)), str(event.get("agentId", ""))),
    )
