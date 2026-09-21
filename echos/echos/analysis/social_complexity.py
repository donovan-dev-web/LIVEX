"""Moteur 3 — SocialComplexityMetrics (complexité sociale).

Mesure la structure des réseaux de relations (METRICS_SPEC.md §4).
Squelette : implémentation au jalon U1.
"""

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


def compute(snapshot: dict) -> dict:
    """Calcule les métriques de complexité sociale sur un snapshot SYNE."""
    raise NotImplementedError(
        f"Moteur {ENGINE_NAME} non implémenté (jalon U1). "
        f"Métriques : {', '.join(METRICS)}."
    )
