"""Moteur 3 — SocialComplexityMetrics (complexité sociale).

Mesure la structure des réseaux de relations (METRICS_SPEC.md §4) à partir des
relations de confiance exposées par les agents (``trust``, U2). Les 7 métriques
sont calculées de façon déterministe sur le graphe de confiance ; l'absence
d'historique (stabilité des communautés) replie sur 0.0.
"""

from __future__ import annotations

from ._common import (
    agents_of,
    alive_count,
    mean,
    community_sizes,
    trust_edges,
    variance,
)

ENGINE_NAME = "SocialComplexityMetrics"

METRICS = (
    "AverageTrustLevel",
    "TrustVariance",
    "NetworkDensity",
    "ClusteringCoefficient",
    "AverageCentrality",
    "NumberOfCommunities",
    "CommunityStability",
)

_LOW_TRUST = 0.25


def _neighbors_of(agents: list[dict]) -> dict[str, set[str]]:
    """Voisins de confiance (relations > 0) par identifiant d'agent."""
    neighbors: dict[str, set[str]] = {}
    for agent in agents:
        agent_id = str(agent.get("id"))
        neighbors.setdefault(agent_id, set())
        for relation in agent.get("trust") or []:
            if (relation.get("trust") or 0.0) > 0.0:
                neighbors[agent_id].add(str(relation.get("peerId")))
    return neighbors


def _local_clustering(agents: list[dict]) -> float:
    """Coefficient de clustering local moyen (triangles fermés / possibles)."""
    neighbors = _neighbors_of(agents)
    coefficients: list[float] = []
    for agent_id, nbs in neighbors.items():
        nb = list(nbs)
        if len(nb) < 2:
            coefficients.append(0.0)
            continue
        links = sum(
            1
            for i in range(len(nb))
            for j in range(i + 1, len(nb))
            if nb[j] in neighbors.get(nb[i], set())
        )
        possible = len(nb) * (len(nb) - 1) / 2.0
        coefficients.append(links / possible if possible > 0 else 0.0)
    return mean(coefficients) if coefficients else 0.0


def compute(snapshot: dict) -> dict:
    """Calcule les 7 métriques de complexité sociale sur un snapshot SYNE."""
    agents = agents_of(snapshot)
    count = alive_count(snapshot)

    trust_levels = [
        float(relation["trust"])
        for agent in agents
        for relation in agent.get("trust") or []
        if relation.get("trust") is not None
    ]

    edges = trust_edges(agents)
    possible_pairs = count * (count - 1)
    density = len(edges) / possible_pairs if possible_pairs > 0 else 0.0

    neighbors = _neighbors_of(agents)
    centralities = [
        len(neighbors[agent_id]) / (count - 1)
        for agent_id in neighbors
        if count > 1
    ]

    community_sizes_list = community_sizes(agents)

    # Stabilité : part des communautés réapparues dans le dernier historique
    # (clé ``communityHistory``, ordre chronologique) — 0.0 sans historique.
    history = snapshot.get("communityHistory") or []
    community_stability = 0.0
    if community_sizes_list and history:
        last = history[-1].get("communities") or []
        current = set(community_sizes_list)
        overlap = sum(1 for size in last if int(size) in current)
        community_stability = overlap / max(len(last), 1)

    return {
        "AverageTrustLevel": mean(trust_levels) if trust_levels else 0.0,
        "TrustVariance": variance(trust_levels) if trust_levels else 0.0,
        "NetworkDensity": density,
        "ClusteringCoefficient": _local_clustering(agents),
        "AverageCentrality": mean(centralities) if centralities else 0.0,
        "NumberOfCommunities": len(community_sizes_list),
        "CommunityStability": community_stability,
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
