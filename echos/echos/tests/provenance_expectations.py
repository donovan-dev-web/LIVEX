"""Métriques légitimement non mesurées sur les tirages d'intégration courts.

Le catalogue (``echos.analysis.catalog``) décrit ce qu'une métrique mesure et à
quelle condition ; les tirages d'intégration — 3 ticks, 9 agents — ne réunissent
pas toutes ces conditions. Six métriques y sont donc servies comme non mesurées,
ce qui vaut mieux qu'un repli silencieux présenté pour une mesure :

- ``BeliefDiversityNorm`` normalise par ``log₂(k)`` sur les catégories de
  croyances observées : ce tir n'en produit pas de façon exploitable ;
- les cinq autres sont déclarées ``exploratory`` au catalogue (poids non
  calibrés par les scénarios de référence, fenêtres de dissolution ou de sortie
  de membre jamais ouvertes sur 3 ticks).

Cette liste blanche est **explicite et ne peut que rétrécir** : toute métrique
non mesurée qui ny figure pas est une régression de provenance, et toute
métrique qui nen serait plus non mesurée doit en sortir pour que la liste ne
devienne pas un sac de recharge silencieux.
"""

from __future__ import annotations

EXPECTED_UNMEASURED: dict[str, frozenset[str]] = {
    "CognitiveDiversityMetrics": frozenset({"BeliefDiversityNorm"}),
    "EmergenceIndicators": frozenset(
        {"ContributionBeliefDiversity", "EmergenceScore", "SystemComplexity"}
    ),
    "GroupDynamicsMetrics": frozenset(
        {"DissolvedGroupSuccessShare", "MemberExitsPerDissolution"}
    ),
}


def unexpected_unmeasured(
    measured: dict[str, dict[str, bool]],
) -> dict[str, list[str]]:
    """Métriques non mesurées **hors** liste blanche, indexées par moteur."""
    surprises: dict[str, list[str]] = {}
    for engine, flags in measured.items():
        unmeasured = {metric for metric, ok in flags.items() if not ok}
        extra = unmeasured - EXPECTED_UNMEASURED.get(engine, frozenset())
        if extra:
            surprises[engine] = sorted(extra)
    return surprises
