"""Moteur 8 — EmergenceIndicators (indicateurs d'émergence, ECHOS-030→033).

Composite (EMERGENCE_INDICATORS.md) : ``compute(snapshot)`` exécute les
moteurs de métriques puis compose les indicateurs ; ``compute_from_metrics``
compose directement à partir de résultats déjà calculés (miroir de la méthode
``Calculate`` du prototype C#). Fonctions **pures** et déterministes : aucun
PRNG, ordre stable (phénomènes par seuils, signaux dans l'ordre de la spec).

Refonte P2 (RAPPORT §3.7) — trois corrections structurelles :

1. **Composantes normalisées** : les entropies de Shannon brutes (bits)
   n'étaient pas bornées à 1 ; additionner des bits, un clustering [0,1] et un
   nombre de groupes produisait une somme qui saturait dans le ``clamp`` final
   et perdait toute discrimination dans la partie haute. Chaque composante est
   désormais une grandeur **[0, 1]** avec une base documentée, et la somme
   pondérée (poids Σ = 1.0) est déjà bornée — le ``clamp`` n'est plus qu'une
   garde.
2. **Contributions publiées** : chaque terme du score est émis comme métrique
   ``Contribution*``. Σ contributions == ``EmergenceScore`` (invariant testé) :
   le score se décompose dans n'importe quelle interface, sans réexécution.
3. **Provenance par dépendances réelles** : la provenance du composite est
   calculée à partir de ``COMPOSITE_DEPENDENCIES`` (moteur ``__init__``),
   seulement sur les métriques qui entrent réellement dans chaque sortie, au
   lieu d'exiger l'intégralité de tous les moteurs entrants.

Statut : **exploratoire**. Les poids (0.15/0.15/0.10/0.15/0.20/0.25) sont
[HÉRITÉ] de la Monographie, non calibrés sur des runs de référence et sans
analyse de sensibilité ; le score n'est pas une mesure établie d'émergence
(``Disclaimer`` invariant, Monographie §4.10.3, ECHOS-032).

Conventions V0.1 (cf. METRICS_SPEC.md §1, ECHOS-006) :
- données manquantes → valeurs neutres 0.0 ; ``DetectedPhenomena`` devient vide ;
- ``Disclaimer`` invariant (ECHOS-032) ;
- ``CoverageDelay_Norm = clamp(1 - EmitterCoverageDelay / 100, 0, 1)`` avec
  neutralité étendue : délai non mesuré (absent ou ≤ 0) → contribution 0.0
  plutôt que le max de la formule (couverture instantanée, inobservable) ;
- ``SystemComplexity`` est borné et composé de grandeurs normalisées : la
  diffusion y entre via ``CoverageDelay_Norm`` et non en ticks bruts, sans
  quoi l'indicateur croissait linéairement avec la durée du run.

L'ancien ``UnpredictabilityIndex`` (= ``LoopStrength × DecisionDiversity``)
a été **retiré** : le produit d'une fréquence de répétition et d'un ratio
d'actions distinctes ne mesure pas l'imprévisibilité, qui exigerait une
baseline prédictive hors échantillon (décision P0 du registre).
"""

from __future__ import annotations

from ._common import COMPOSITE_ENGINE_NAME, clamp, load_engines

ENGINE_NAME = COMPOSITE_ENGINE_NAME

WEIGHTS = {
    "ContributionBeliefDiversity": 0.15,
    "ContributionGoalDiversity": 0.15,
    "ContributionEmitterCoverage": 0.10,
    "ContributionClustering": 0.15,
    "ContributionRepeatedActions": 0.20,
    "ContributionCommunityCoverage": 0.25,
}
"""Poids des composantes du score [HÉRITÉ Monographie §4.4], Σ = 1.0.

Non calibrés : aucune campagne de référence ni analyse de sensibilité ne les
a validés. Ils sont publiés (et non codés en dur dans la formule) pour que la
sensibilité puisse être étudiée sans changer silencieusement la formule.
"""

METRICS = (
    "EmergenceScore",
    *WEIGHTS.keys(),
    "DetectedPhenomena",
    "SystemComplexity",
    "Disclaimer",
)

DISCLAIMER = (
    "ECHOS ne doit jamais transformer une métrique en vérité scientifique : "
    "un score d'émergence ou une valeur de centralité reste une mesure "
    "particulière d'un phénomène, jamais une preuve de l'existence d'une "
    "intelligence ou d'une société (Monographie §4.10.3, ECHOS-032)."
)

_COMPOSITE_METRICS = (
    "EmergenceScore",
    *WEIGHTS.keys(),
    "SystemComplexity",
    "DetectedPhenomena",
)
"""Métriques composites dont la valeur dérive d'autres moteurs.

Leur provenance est donc **propagée** : sans ce mécanisme, un ``EmergenceScore``
calculé sur des moteurs à 0.0 de repli était indiscernable d'un score mesuré
(voir §Provenance de ``METRICS_SPEC``).
"""

COMPOSITE_DEPENDENCIES: dict[str, tuple[str, ...]] = {
    "EmergenceScore": tuple(WEIGHTS),
    "ContributionBeliefDiversity": ("BeliefDiversityNorm",),
    "ContributionGoalDiversity": ("GoalDiversityNorm",),
    "ContributionEmitterCoverage": ("EmitterCoverageDelay",),
    "ContributionClustering": ("ClusteringCoefficient",),
    "ContributionRepeatedActions": ("RepeatedActionShare",),
    "ContributionCommunityCoverage": ("CommunityCoverage",),
    "SystemComplexity": (
        "BeliefDiversityNorm",
        "GoalDiversityNorm",
        "EmitterCoverageDelay",
    ),
    "DetectedPhenomena": (),  # complété après déclaration des phénomènes
}
"""Dépendances réelles de chaque sortie composite (métriques aplaties).

La provenance d'une sortie n'exige que **ses** dépendances : exiger toutes les
métriques de tous les moteurs entrants marquait « non mesuré » un score entier
à cause d'une métrique incidente qui n'entre pas dans sa formule.
``Disclaimer`` est absent : c'est une constante textuelle, toujours publiée.
"""

# Spécification des signaux auto-détectés (ECHOS-031) : identifiant stable
# (clé de contrat public), libellé français, description et signaux (métrique,
# seuil). Les **libellés et descriptions** sont requalifiés (P0) : les
# identifiants restent stables pour ne pas casser les consommateurs, les
# libellés décrivent ce qui est réellement observé. Seuils non calibrés :
# ils viennent de la spec héritée et n'ont fait l'objet d'aucune campagne de
# référence — une absence de détection ne signifie pas l'absence du
# phénomène.
_PHENOMENA = (
    {
        "identifier": "CommunityFormation",
        "label": "Seuil de communautés franchi",
        "description": (
            "Plus de deux communautés distinctes sont présentes dans le graphe "
            "de confiance au tick observé. C'est un comptage, sans référence "
            "temporelle : il n'observe pas une formation."
        ),
        "signals": (("NumberOfCommunities", 2),),
    },
    {
        "identifier": "FeedbackLoops",
        "label": "Répétitions d'action soutenues",
        "description": (
            "Plus de cinq paires (agent, action) se répètent dans la fenêtre de "
            "décisions. Aucune conséquence n'est observée : ce n'est pas une "
            "boucle causale."
        ),
        "signals": (("RepeatedActionPairs", 5),),
    },
    {
        "identifier": "CollectiveCoordination",
        "label": "Convergence forte des objectifs",
        "description": (
            "Plus de 70 % des entités déclarent le même objectif principal. "
            "C'est une concordance de catégories, pas la preuve d'une "
            "coordination."
        ),
        "signals": (("GoalConvergence", 0.7),),
    },
    {
        "identifier": "InformationBottleneck",
        "label": "Concentration des émetteurs",
        "description": (
            "Un émetteur concentre plus de 30 % des messages observés dans la "
            "fenêtre. Seuil non calibré ; la mesure porte sur les émissions, "
            "jamais sur les réceptions."
        ),
        "signals": (("SenderConcentration", 0.3),),
    },
    {
        "identifier": "OrganizationalDynamics",
        "label": "Communautés nombreuses et sorties de membres",
        "description": (
            "Plus de cinq communautés inférées du graphe de confiance et plus "
            "d'une sortie de membre par dissolution observée. Les communautés "
            "sont inférées, pas déclarées par SYNE."
        ),
        "signals": (("InferredCommunities", 5), ("MemberExitsPerDissolution", 1)),
    },
)

COMPOSITE_DEPENDENCIES["DetectedPhenomena"] = tuple(
    sorted({
        metric
        for phenomenon in _PHENOMENA
        for metric, _threshold in phenomenon["signals"]
    })
)
# Le score somme les contributions : ses dépendances sont l'union de celles
# des contributions (les noms de contributions ne sont pas persistés comme
# métriques de moteurs).
COMPOSITE_DEPENDENCIES["EmergenceScore"] = tuple(
    sorted({
        dependency
        for metric in WEIGHTS
        for dependency in COMPOSITE_DEPENDENCIES[metric]
    })
)

_DIFFUSION_HORIZON = 100.0
"""Amplitude (ticks) au-delà de laquelle la couverture est considérée nulle.

``EmitterCoverageDelay`` mesure une durée en ticks et non un tick absolu :
la normalisation ``1 - délai / horizon`` interpole entre « couvert en un tick »
(1.0) et « non couvert dans le contexte observé » (0.0). Horizon [HÉRITÉ],
non calibré.
"""

# Moteurs de métriques entrants du score composite et des phénomènes (ordre de
# première priorité lors de l'aplatissement — ex. ``GoalDiversity`` est fournie
# par les moteurs cognitif et de convergence, valeurs identiques).
_METRIC_ENGINES = (
    "CognitiveDiversityMetrics",
    "InformationPropagationMetrics",
    "SocialComplexityMetrics",
    "GoalConvergenceMetrics",
    "FeedbackLoopDetector",
    "GroupDynamicsMetrics",
)

__all__ = [
    "ENGINE_NAME",
    "METRICS",
    "WEIGHTS",
    "DISCLAIMER",
    "COMPOSITE_DEPENDENCIES",
    "compute",
    "compute_from_metrics",
]


def _engine_results(snapshot: dict) -> dict[str, dict]:
    """Résultats des moteurs entrants pour un snapshot (repli neutre absent).

    La liste est dérivée du registre central : la hard-coder ici faisait
    diverger ``compute`` du chemin normalisé ``compute_all`` dès qu'un moteur
    était ajouté au registre.
    """
    return {
        engine.ENGINE_NAME: engine.compute(snapshot)
        for engine in load_engines()
        if engine.ENGINE_NAME != COMPOSITE_ENGINE_NAME
    }


def _flatten(metrics: dict[str, dict]) -> dict[str, float]:
    """Aplatit {moteur → {métrique → valeur}} en {métrique → valeur}.

    Ne retient que les valeurs numériques (les sorties composites telles que
    ``RepeatedActionCounts`` ou ``GoalTypeCounts`` ne participent pas aux
    indicateurs).
    """
    flat: dict[str, float] = {}
    for name in _METRIC_ENGINES:
        for key, value in (metrics.get(name) or {}).items():
            if isinstance(value, (int, float)) and not isinstance(value, bool):
                flat[key] = float(value)
    return flat


def _metric(flat: dict[str, float], name: str, default: float = 0.0) -> float:
    return flat.get(name, default)


def _detected_phenomena(flat: dict[str, float]) -> list[dict]:
    """Phénomènes dont tous les signaux déclencheurs sont au-dessus du seuil."""
    detected: list[dict] = []
    for phenomenon in _PHENOMENA:
        signals = [
            {
                "metric": metric,
                "value": _metric(flat, metric),
                "threshold": threshold,
            }
            for metric, threshold in phenomenon["signals"]
        ]
        if all(signal["value"] > signal["threshold"] for signal in signals):
            detected.append(
                {
                    "identifier": phenomenon["identifier"],
                    "label": phenomenon["label"],
                    "description": phenomenon["description"],
                    "signals": signals,
                }
            )
    return detected


def compute_from_metrics(metrics: dict[str, dict]) -> dict:
    """Compose les indicateurs d'émergence depuis les résultats des moteurs."""
    flat = _flatten(metrics)

    belief_norm = clamp(_metric(flat, "BeliefDiversityNorm"))
    goal_norm = clamp(_metric(flat, "GoalDiversityNorm"))
    clustering = clamp(_metric(flat, "ClusteringCoefficient"))
    repeated_share = clamp(_metric(flat, "RepeatedActionShare"))
    community_coverage = clamp(_metric(flat, "CommunityCoverage"))
    delay = _metric(flat, "EmitterCoverageDelay")

    # Couverture normalisée : plus le seuil des 80 % d'émetteurs est atteint
    # vite (faible amplitude en ticks), plus la contribution est forte. Non
    # mesurée (0 message) → 0.0.
    delay_norm = clamp(1.0 - delay / _DIFFUSION_HORIZON) if delay > 0.0 else 0.0

    components = {
        "ContributionBeliefDiversity": belief_norm,
        "ContributionGoalDiversity": goal_norm,
        "ContributionEmitterCoverage": delay_norm,
        "ContributionClustering": clustering,
        "ContributionRepeatedActions": repeated_share,
        "ContributionCommunityCoverage": community_coverage,
    }
    contributions = {
        metric: round(WEIGHTS[metric] * value, 12)
        for metric, value in components.items()
    }

    # Chaque composante est [0,1] et les poids somment à 1.0 : la somme est
    # déjà dans [0,1], le clamp ne sert que de garde (plus de saturation).
    emergence_score = clamp(sum(contributions.values()))
    # Formule canonique bornée (décision P2, EMERGENCE_INDICATORS §3) : trois
    # grandeurs normalisées, plus de ticks bruts.
    system_complexity = clamp((belief_norm + goal_norm + delay_norm) / 3.0)

    result = {
        "EmergenceScore": emergence_score,
        **contributions,
        "DetectedPhenomena": _detected_phenomena(flat),
        "SystemComplexity": system_complexity,
        "Disclaimer": DISCLAIMER,
    }
    return result


def compute(snapshot: dict) -> dict:
    """Calcule les indicateurs d'émergence sur un snapshot (moteurs internes)."""
    return compute_from_metrics(_engine_results(snapshot))
