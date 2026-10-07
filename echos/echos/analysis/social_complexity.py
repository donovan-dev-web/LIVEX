"""Moteur 3 — SocialComplexityMetrics (complexité sociale).

Mesure la structure des réseaux de relations (METRICS_SPEC.md §4) à partir des
relations de confiance exposées par les agents (``trust``, U2). Les 7 métriques
sont calculées de façon déterministe sur le graphe de confiance ; l'absence
d'historique (stabilité des communautés) replie sur 0.0.
"""

from __future__ import annotations

from ._common import (
    agent_ids,
    agents_of,
    alive_count,
    community_sizes,
    mean,
    neighbors_of,
    safe_ratio,
    trust_edges,
    trust_levels,
    variance,
)

ENGINE_NAME = "SocialComplexityMetrics"

METRICS = (
    "AverageTrustLevel",
    "TrustVariance",
    "NetworkDensity",
    "ClusteringCoefficient",
    "AverageOutDegree",
    "NumberOfCommunities",
    "CommunitySizeMatch",
)


def _community_history_published(snapshot: dict) -> bool:
    """Fenêtre de communautés réellement publiée (même vide = observation)."""
    history = snapshot.get("communityHistory")
    return isinstance(history, list) and bool(history)


REQUIRES = {"CommunitySizeMatch": _community_history_published}
"""``CommunitySizeMatch`` compare la taille des communautés dans le temps.

Elle dépend donc de ``communityHistory``, absent du snapshot instantané : sans
fenêtre, elle retombait sur 1.0 (stabilité parfaite par défaut) et déclenchait
faux sur un monde sans aucune communauté. Les six autres métriques lisent le
graphe de confiance du tick courant.

**Nom retenu (P1)** : la mesure compare des **tailles**, jamais des membres :
deux communautés disjointes de même taille sont comptées « stables ». Elle
porte donc un nom qui décrivait ce qu'elle mesure réellement ; une stabilité
**d'identité** (Jaccard entre partitions consécutives) exigerait que le
contrat de ``communityHistory`` publie les membres — lacune tracée dans
l'inventaire dimension → contrat → vue.
"""


def _local_clustering(neighbors: dict[str, set[str]]) -> float:
    """Coefficient de clustering local moyen (triangles fermés / possibles).

    Le graphe est traité comme non orienté : une arête ``a—b`` existe dès que
    l'une des deux directions déclare une confiance positive, ce que
    :func:`neighbors_of` ne garantit pas à lui seul. On ferme donc le
    voisinage avant de compter les triangles, faute de quoi une relation
    déclarée d'un seul côté ne compte jamais.
    """
    undirected: dict[str, set[str]] = {
        agent_id: set(peers) for agent_id, peers in neighbors.items()
    }
    for agent_id, peers in neighbors.items():
        for peer in peers:
            undirected.setdefault(peer, set()).add(agent_id)

    coefficients: list[float] = []
    for agent_id in sorted(undirected):
        peers = sorted(undirected[agent_id])
        if len(peers) < 2:
            coefficients.append(0.0)
            continue
        links = sum(
            1
            for i, left in enumerate(peers)
            for right in peers[i + 1:]
            if right in undirected[left]
        )
        possible = len(peers) * (len(peers) - 1) / 2.0
        coefficients.append(safe_ratio(links, possible))
    return mean(coefficients)


def compute(snapshot: dict) -> dict:
    """Calcule les 7 métriques de complexité sociale sur un snapshot SYNE."""
    agents = agents_of(snapshot)
    count = alive_count(snapshot)

    levels = trust_levels(agents)
    edges = trust_edges(agents)
    # Densité d'un graphe **non orienté** simple : le dénominateur est le nombre
    # de paires unordered n(n-1)/2, pas n(n-1). L'ancienne formule bornait la
    # densité à 0.5 sur un graphe complet, ce qui conditionnait toute
    # interprétation d'une comparaison entre runs.
    density = safe_ratio(len(edges), count * (count - 1) / 2.0)

    # Une seule construction du voisinage, réutilisée par le clustering et
    # les degrés (elles observaient auparavant deux copies divergentes).
    neighbors = neighbors_of(agents)
    # Degré sortant normalisé par n-1 : c'est un degré moyen, **pas** une
    # centralité intermédiaire (betweenness) — la spec ancienne le laissait
    # croire. Sur un graphe non orienté, cette moyenne est équivalente à la
    # densité (facteur de normalisation mis à part) : redondance documentée.
    out_degrees = [
        safe_ratio(len(neighbors.get(agent_id, ())), count - 1) for agent_id in agent_ids(agents)
    ]

    community_sizes_list = community_sizes(agents)

    # Part des communautés de l'historique dont la taille est présente dans le
    # partition courant (clé ``communityHistory``, ordre chronologique) — 0.0
    # sans historique. Comparaison de **tailles**, pas de membres.
    community_size_match = 0.0
    history = snapshot.get("communityHistory") or []
    if community_sizes_list and history:
        last = history[-1].get("communities") or []
        current = set(community_sizes_list)
        overlap = sum(1 for size in last if int(size) in current)
        community_size_match = safe_ratio(overlap, len(last))

    return {
        "AverageTrustLevel": mean(levels) if levels else 0.0,
        "TrustVariance": variance(levels) if levels else 0.0,
        "NetworkDensity": density,
        "ClusteringCoefficient": _local_clustering(neighbors),
        "AverageOutDegree": mean(out_degrees) if out_degrees else 0.0,
        "NumberOfCommunities": len(community_sizes_list),
        "CommunitySizeMatch": community_size_match,
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
