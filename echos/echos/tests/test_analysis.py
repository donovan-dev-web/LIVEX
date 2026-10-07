"""Moteurs de métriques ECHOS (ECHOS-020→026, jalon U2).

Chaque moteur est une fonction pure et déterministe ``compute(snapshot) ->
dict`` sur le dict de transport camelCase (AI/needs__metrics_engines). Les
métriques sont appelées fenêtrées (``history``/``events``/``groupEvents``) ou
instantanées ; sans données, les valeurs neutres 0.0 sont retournées
(ECHOS-006, corset V0.1), sauf documenté par moteur.
"""

import json
import random
from pathlib import Path

import pytest

from echos.analysis import ENGINES, emergence, known_engines
from echos.ingestion import WorldSnapshot, parse_message

FIXTURES = Path(__file__).resolve().parent / "fixtures"
GOLDEN = Path(__file__).resolve().parent / "golden"

_APPROX = pytest.approx

_CONTEXT_KEYS = ("events", "history", "communityHistory")
"""Clés de fenêtre construites par le pipeline, hors modèle ``WorldSnapshot``."""


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text())


def _golden() -> dict:
    return json.loads((GOLDEN / "analysis_golden.json").read_text())


def _fresh_snapshot() -> dict:
    """Copie profonde (les moteurs ne doivent pas muter leur entrée)."""
    return _load("snapshot_analysis.json")


# ---------------------------------------------------------------------------
# Contrat de registre (7 moteurs, 1 contractuellement par issue ECHOS-020→026)
# ---------------------------------------------------------------------------


def test_registry_exposes_all_eight_engines():
    engines = known_engines()

    assert set(engines) == {
        "CognitiveDiversityMetrics",
        "InformationPropagationMetrics",
        "SocialComplexityMetrics",
        "GoalConvergenceMetrics",
        "FeedbackLoopDetector",
        "ResourceSustainabilityMetrics",
        "GroupDynamicsMetrics",
        "EmergenceIndicators",
    }


def test_each_engine_exposes_metrics_and_is_testable_separately():
    for engine in ENGINES:
        assert engine.ENGINE_NAME
        assert len(engine.METRICS) >= 1
        assert callable(engine.compute)


# ---------------------------------------------------------------------------
# Pureté et déterminisme
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("engine", ENGINES, ids=lambda engine: engine.ENGINE_NAME)
def test_compute_is_pure_deterministic(engine):
    snapshot = _fresh_snapshot()

    first = engine.compute(snapshot)
    second = engine.compute(snapshot)

    assert first == second
    # L'entrée n'est pas mutée par le calcul (pureté).
    assert snapshot == _fresh_snapshot()


@pytest.mark.parametrize("engine", ENGINES, ids=lambda engine: engine.ENGINE_NAME)
def test_empty_snapshot_returns_neutral_metrics(engine):
    """Aucune donnée → toutes les métriques neutres (0.0), sans exception."""
    result = engine.compute({})

    assert set(result) == set(engine.METRICS)
    for value in result.values():
        if isinstance(value, dict):
            assert all(isinstance(item, int) for item in value.values())
        elif isinstance(value, list):
            # EmergenceIndicators : aucun phénomène sur données vides (ECHOS-031).
            assert value == []
        elif isinstance(value, str):
            # EmergenceIndicators : disclaimer invariant présent (ECHOS-032).
            assert value == emergence.DISCLAIMER
        else:
            assert isinstance(value, (int, float))


@pytest.mark.parametrize("engine", ENGINES, ids=lambda engine: engine.ENGINE_NAME)
def test_compute_ignores_extra_keys(engine):
    """Clés inconnues (rétro-compat transport) ignorées — contrat V0.1."""
    snapshot = _fresh_snapshot()
    snapshot["futureKey"] = {"anything": [1, 2, 3]}

    assert engine.compute(snapshot) == engine.compute(_fresh_snapshot())


# ---------------------------------------------------------------------------
# Stabilité transport → modèle pydantic → transport (ECHOS-027 golden)
# ---------------------------------------------------------------------------


def test_cognitive_fields_survive_ingestion_roundtrip():
    """Le snapshot U2 (traits/beliefs/goals/trust/memoryCount) se reflète."""
    raw = _load("world_snapshot_u2.json")

    parsed = parse_message(json.dumps(raw))

    assert isinstance(parsed, WorldSnapshot)
    assert parsed.model_dump(mode="json", by_alias=True, exclude_none=True) == raw

    agent = parsed.agents[0]
    assert agent.traits == {"courage": 0.6, "caution": 0.4}
    assert agent.beliefs[0].value == "true"
    assert agent.goals[0].age == 10
    assert agent.trust[0].peer_id == "B"
    assert agent.memory_count == 8


@pytest.mark.parametrize(
    "engine",
    [engine for engine in ENGINES if engine.ENGINE_NAME in _golden()],
    ids=lambda engine: engine.ENGINE_NAME,
)
def test_transport_to_model_snake_case_is_stable(engine):
    """Résultat identique que le dict vienne du transport ou du modèle.

    Le modèle pydantic ne porte que l'état du monde ; les fenêtres de contexte
    (``events``, ``history``, ``communityHistory``) sont construites par le
    pipeline et lui sont réinjectées après le round-trip. Sans cette
    réinjection, tout moteur fenêtré retombait sur son repli neutre et le test
    comparait deux jeux de données différents.
    """
    raw = _load("snapshot_analysis.json")
    modeled = parse_message(json.dumps(raw))

    # Clés de contexte que le transport ajoute au snapshot (hors modèle).
    context = {key: raw[key] for key in _CONTEXT_KEYS if key in raw}

    from_transport = engine.compute(raw)
    reflected = engine.compute(
        {**modeled.model_dump(mode="json", by_alias=True, exclude_none=True), **context}
    )

    assert reflected == from_transport


# ---------------------------------------------------------------------------
# Vérification manuelle du scénario (snapshot_analysis.json) — graine stable
# ---------------------------------------------------------------------------

_RICH = _fresh_snapshot()
_RICH["tick"] = 10  # tableau fixe de la fixture (identique au fichier)


def test_cognitive_diversity_hand_computed_values():
    result = _cognitive()

    assert result["BeliefDiversity"] == _APPROX(
        1.9219280948873623, abs=1e-9
    )  # H sur 4 faits (2× true, 2× unique)
    # Version normalisée [0,1] : H / log₂(4 catégories distinctes).
    assert result["BeliefDiversityNorm"] == _APPROX(0.9609640474436811, abs=1e-9)
    assert result["BeliefDisagreement"] == _APPROX(0.41666666666666663, abs=1e-9)
    assert result["BeliefConfidenceVariance"] == _APPROX(0.02, abs=1e-9)
    assert result["GoalDiversity"] == _APPROX(1.584962500721156, abs=1e-9)
    # 3 catégories observées → entropie normalisée à sa valeur maximale.
    assert result["GoalDiversityNorm"] == _APPROX(1.0, abs=1e-9)
    assert result["GoalConvergence"] == _APPROX(1 / 3, abs=1e-9)
    # A et B déclarent un objectif, C non : la couverture expose le repli.
    assert result["GoalCoverage"] == _APPROX(2 / 3, abs=1e-9)
    # Aucune décision dans la fenêtre du fixture : zéro observé, mesuré.
    assert result["ActionDiversity"] == 0.0
    assert result["DecisionCount"] == 0.0
    assert result["AverageGoalAge"] == 7.0  # Âges moyens : (10 + 4) / 2
    assert result["TraitExpressionDiversity"] == _APPROX(0.01277777777777778, abs=1e-12)


def test_information_propagation_hand_computed_values():
    result = _information()

    # 1 message au tick courant (10) sur 3 entités ; couverture émetteurs 3/3
    assert result["MessageVolume"] == _APPROX(1 / 3, abs=1e-9)
    # Délai de couverture des émetteurs = amplitude entre le 1er message et le
    # seuil 80 %, et non le numéro de tick (qui dépendrait de la longueur du run).
    assert result["EmitterCoverageDelay"] == 5.0  # seuils atteints à t=5
    # Transformation théorique sous hypothèse 10 %/saut — pas une précision.
    assert result["TheoreticalHopDecay"] == _APPROX(0.13, abs=1e-9)
    assert result["MaxMessageHops"] == 2.0
    # 3 messages, 3 émetteurs distincts → part du premier émetteur = 1/3.
    assert result["SenderConcentration"] == _APPROX(1 / 3, abs=1e-9)
    assert result["SenderCoverage"] == _APPROX(1.0, abs=1e-9)


def test_social_complexity_hand_computed_values():
    result = _social()

    assert result["AverageTrustLevel"] == _APPROX(0.5666666666666667, abs=1e-9)
    assert result["TrustVariance"] == _APPROX(0.01555555555555555, abs=1e-9)
    # Densité non orientée : 2 arêtes sur les 3 paires possibles de 3 entités
    # (l'ancien dénominateur n(n-1) = 6 bornait artificiellement à 0,5 max).
    assert result["NetworkDensity"] == _APPROX(2 / 3, abs=1e-9)
    assert result["ClusteringCoefficient"] == 0.0  # pas de triangle
    # Degré sortant moyen normalisé par n-1 — pas une centralité intermédiaire.
    assert result["AverageOutDegree"] == _APPROX(0.5, abs=1e-9)
    assert result["NumberOfCommunities"] == 1  # groupe plein connecté (A-B, A-C)
    # La communauté de 3 agents est présente à l'identique au dernier tick de
    # l'historique de communautés : tailles identiques (comparaison de tailles).
    assert result["CommunitySizeMatch"] == 1.0


def test_goal_convergence_hand_computed_values():
    result = _convergence()

    assert result["GlobalGoalAlignment"] == _APPROX(1 / 3, abs=1e-9)
    assert result["GoalDiversity"] == _APPROX(1.584962500721156, abs=1e-9)
    # Σ p² sur 3 catégories équivalentes : concordance attendue, pas coopération.
    assert result["GoalCategoryConcordance"] == _APPROX(1 / 3, abs=1e-9)
    assert result["GoalTypeCounts"] == {"Idle": 1, "SeekFood": 1, "SeekWater": 1}


def test_feedback_loop_hand_computed_values():
    result = _loops()

    # 4 paires (agent, action) répétées : A/SeekFood (9×), B/SeekWater (9×),
    # C/Idle (7×), C/Sleep (3×) — des répétitions, pas des boucles causales.
    assert result["RepeatedActionPairs"] == 4
    assert result["RepeatedActionShare"] == _APPROX(0.7, abs=1e-9)
    assert result["ActionDistributionBalance"] == _APPROX(1 / 3, abs=1e-9)
    assert result["AmplifiedRepetitions"] == 4  # toutes > 1,5× la fréquence uniforme
    # Comptage par nom d'action, sans catégorie normative positive/négative ;
    # invariant : la somme vaut RepeatedActionPairs.
    assert result["RepeatedActionCounts"] == {
        "Idle": 1,
        "SeekFood": 1,
        "SeekWater": 1,
        "Sleep": 1,
    }
    assert sum(result["RepeatedActionCounts"].values()) == result["RepeatedActionPairs"]


def test_resource_sustainability_hand_computed_values():
    result = _resources()

    # Part de capacité restante : food 30/100 = 0,3 et water 50/200 = 0,25.
    assert result["ResourceFillRatio"] == _APPROX(0.275, abs=1e-9)
    assert result["CriticalResourceCount"] == 0.0  # aucune réserve sous 20 %
    # Les deux réserves publient leur capacité → couverture totale.
    assert result["ResourceCoverage"] == _APPROX(1.0, abs=1e-9)
    # Flux : consommation cumulée rapportée à la fenêtre réellement observée.
    assert result["ConsumptionPerTick"] == _APPROX(35 / 6, abs=1e-9)
    assert result["RecoveryTime"] == 2.0  # 2 chutes rétablies en 2 ticks en moyenne
    assert result["RecoveryEpisodes"] == 2.0  # les deux crises sont complètes
    assert result["UnresolvedCrisisCount"] == 0.0  # aucune crise ouverte en fin


def test_group_dynamics_hand_computed_values():
    result = _groups()

    # Communautés **inférées** du graphe de confiance, pas groupes déclarés.
    assert result["InferredCommunities"] == 1.0
    assert result["AverageCommunitySize"] == 3.0
    # Les 3 entités vivantes appartiennent à la communauté → couverture 1,0.
    assert result["CommunityCoverage"] == 1.0
    # Événements de groupes natifs, avec leurs dénominateurs bruts publiés.
    assert result["AverageGroupLifetime"] == 15.0
    assert result["GroupFormationRate"] == _APPROX(166.66666666666666, abs=1e-9)  # 1/6 ticks × 1000
    assert result["GroupDissolutionRate"] == _APPROX(166.66666666666666, abs=1e-9)
    assert result["FormationCount"] == 1.0
    assert result["DissolutionCount"] == 1.0
    assert result["DissolvedGroupSuccessShare"] == 1.0  # dissolution réussie
    # 1 sortie pour 1 dissolution observée — pas un taux annualisé.
    assert result["MemberExitsPerDissolution"] == 1.0


# ---------------------------------------------------------------------------
# Comportement aux limites
# ---------------------------------------------------------------------------


def test_single_belief_has_zero_diversity():
    from echos.analysis import cognitive_diversity

    snapshot = _fresh_snapshot()
    snapshot["agents"] = [
        {
            "id": "A",
            "position": {"x": 0.0, "y": 0.0},
            "energy": 50,
            "hunger": 30,
            "thirst": 20,
            "beliefs": [
                {
                    "subject": "water",
                    "predicate": "safe",
                    "value": "true",
                    "confidence": 0.9,
                }
            ],
        }
    ]
    snapshot["aliveCount"] = 1

    result = cognitive_diversity.compute(snapshot)

    assert result["BeliefDiversity"] == 0.0  # un seul fait → H = 0
    assert result["GoalDiversity"] == 0.0  # un seul objectif (Idle)


def test_no_trust_graph_isolates_every_agent():
    from echos.analysis import group_dynamics, social_complexity

    snapshot = _fresh_snapshot()
    for agent in snapshot["agents"]:
        agent["trust"] = []
    snapshot["groupEvents"] = []
    snapshot["history"] = []

    social = social_complexity.compute(snapshot)
    groups = group_dynamics.compute(snapshot)

    assert social["NetworkDensity"] == 0.0
    # Aucun lien de confiance → aucune communauté : une entité isolée n'est pas
    # une communauté. Compter chaque agent comme une île faisait remonter
    # NumberOfCommunities à 3 et déclenchait à tort CommunityFormation puis
    # CollectiveCoordination sur un réseau sans aucune relation.
    assert social["NumberOfCommunities"] == 0
    assert groups["InferredCommunities"] == 0.0
    assert groups["AverageCommunitySize"] == 0.0
    assert groups["CommunityCoverage"] == 0.0


def test_self_trust_relation_does_not_create_a_community():
    from echos.analysis import social_complexity

    snapshot = _fresh_snapshot()
    for agent in snapshot["agents"]:
        agent["trust"] = [{"peerId": agent["id"], "trust": 1.0}]

    result = social_complexity.compute(snapshot)

    # Une relation de confiance à soi-même n'est pas une arête : elle ne crée
    # aucune communauté de taille ≥ 2.
    assert result["NumberOfCommunities"] == 0


def test_feedback_loop_without_history_is_neutral():
    from echos.analysis import feedback_loop_detector

    result = feedback_loop_detector.compute({})

    assert result["RepeatedActionPairs"] == 0
    assert result["RepeatedActionShare"] == 0.0
    assert result["ActionDistributionBalance"] == 0.0  # aucune décision → neutre
    assert result["RepeatedActionCounts"] == {}


def test_resource_availability_is_bounded_without_capacity():
    """Une part de capacité ne doit jamais sortir de [0, 1].

    Régression : ``quantity / consumed`` (sans borne, dénominateur de nature
    différente) était mélangée à ``quantity / capacity`` puis comparée à 0,2 :
    le seuil n'avait aucun sens uniforme. La mesure de remplissage ne porte
    que sur la **capacité** ; la consommation a sa propre mesure de flux.
    """
    from echos.analysis.resource_sustainability import _capacity_ratio

    assert _capacity_ratio(100.0, None) is None  # capacité non publiée
    assert _capacity_ratio(100.0, 200.0) == 0.5
    # Une réserve au-delà de sa capacité publiée est saturée, pas « à 300 % ».
    assert _capacity_ratio(300.0, 100.0) == 1.0
    assert _capacity_ratio(100.0, 0.0) is None
    assert _capacity_ratio(0.0, None) is None


def test_resource_engine_stays_bounded_on_incomplete_snapshot():
    from echos.analysis import resource_sustainability

    snapshot = {
        "resources": [
            {"id": "water", "type": "water", "quantity": 100.0},  # sans capacity
            {"id": "food", "type": "food", "quantity": 10.0, "capacity": 100.0},
        ],
    }
    result = resource_sustainability.compute(snapshot)

    # Seule la réserve « food » a une capacité publiée : 10/100 = 0,1.
    assert 0.0 <= result["ResourceFillRatio"] <= 1.0
    assert result["ResourceFillRatio"] == _APPROX(0.1, abs=1e-9)
    # La couverture dit explicitement qu'une réserve sur deux est exploitable.
    assert result["ResourceCoverage"] == _APPROX(0.5, abs=1e-9)
    # Seule la réserve « food » est sous le seuil critique de 20 %.
    assert result["CriticalResourceCount"] == 1.0
    assert result["RecoveryTime"] == 0.0  # pas d'historique
    assert result["RecoveryEpisodes"] == 0.0
    assert result["UnresolvedCrisisCount"] == 0.0


def test_recovery_time_uses_history_when_present():
    from echos.analysis import resource_sustainability

    snapshot = {
        "resources": [{"type": "water", "quantity": 50.0, "capacity": 100.0}],
        "history": [
            {"tick": 1, "resources": [{"type": "water", "quantity": 90.0, "capacity": 100.0}]},
            {"tick": 2, "resources": [{"type": "water", "quantity": 5.0, "capacity": 100.0}]},
            {"tick": 3, "resources": [{"type": "water", "quantity": 10.0, "capacity": 100.0}]},
            {"tick": 5, "resources": [{"type": "water", "quantity": 95.0, "capacity": 100.0}]},
        ],
    }
    # Chute sous 20 % au tick 2, rétablissement ≥ 80 % au tick 5 → 3 ticks.
    result = resource_sustainability.compute(snapshot)
    assert result["RecoveryTime"] == 3.0
    assert result["RecoveryEpisodes"] == 1.0
    assert result["UnresolvedCrisisCount"] == 0.0


def test_unresolved_crisis_is_counted_separately_from_recovered_ones():
    """Une crise encore ouverte en fin de fenêtre n'est ni durée 0 ni ignorée.

    Le zéro de ``RecoveryTime`` confondait « aucune crise » et « crise non
    rétablie » (censure) : les deux sont désormais publiés séparément.
    """
    from echos.analysis import resource_sustainability

    snapshot = {
        "resources": [{"type": "water", "quantity": 5.0, "capacity": 100.0}],
        "history": [
            {"tick": 1, "resources": [{"type": "water", "quantity": 90.0, "capacity": 100.0}]},
            {"tick": 2, "resources": [{"type": "water", "quantity": 5.0, "capacity": 100.0}]},
            {"tick": 3, "resources": [{"type": "water", "quantity": 8.0, "capacity": 100.0}]},
        ],
    }
    result = resource_sustainability.compute(snapshot)

    assert result["RecoveryTime"] == 0.0  # aucun cycle complet
    assert result["RecoveryEpisodes"] == 0.0
    assert result["UnresolvedCrisisCount"] == 1.0  # crise ouverte au dernier tick


def test_consumption_per_tick_uses_the_observed_window():
    """Le flux est rapporté à la durée observée, pas cumulé indéfiniment."""
    from echos.analysis import resource_sustainability

    snapshot = {
        "resources": [],
        "eventWindow": {"ticks": 10, "from": 1, "to": 10},
        "events": [
            {"type": "resource_consumed", "tick": 3, "value": {"type": "food", "amount": 4}},
            {"type": "resource_consumed", "tick": 8, "value": {"type": "food", "amount": 6}},
        ],
    }
    result = resource_sustainability.compute(snapshot)

    assert result["ConsumptionPerTick"] == _APPROX(1.0, abs=1e-9)  # 10 / 10 ticks
    # Ressources absentes → couverture 0, et non « tout va bien ».
    assert result["ResourceCoverage"] == 0.0


def test_action_diversity_counts_decisions_not_goals():
    """``ActionDiversity`` doit varier avec les décisions observées.

    Régression : ``DecisionDiversity`` était calculée sur les types
    d'objectifs, ce qui la rendait redondante avec ``GoalDiversity`` et
    insensible aux décisions ; l'ancien repli silencieux sur les buts est
    désormais un état « non mesuré » distinct.
    """
    from echos.analysis import cognitive_diversity

    base = {
        "aliveCount": 2,
        "agents": [
            {"id": "A", "goals": [{"kind": "SeekFood"}]},
            {"id": "B", "goals": [{"kind": "SeekFood"}]},
        ],
    }
    # Aucune fenêtre d'événements : rien à mesurer (l'ancien repli donnait 0.5
    # sur les objectifs, c'est-à-dire une mesure de buts sous un nom d'action).
    assert cognitive_diversity.compute(base)["ActionDiversity"] == 0.0
    from echos.analysis._common import measured_flags

    assert measured_flags(base, cognitive_diversity)["ActionDiversity"] is False

    mixed = dict(base)
    mixed["events"] = [
        {"type": "decision_made", "agentId": "A", "action": "SeekFood"},
        {"type": "decision_made", "agentId": "B", "action": "Rest"},
    ]
    # Deux décisions, deux actions distinctes → entropie normalisée maximale.
    assert cognitive_diversity.compute(mixed)["ActionDiversity"] == 1.0
    assert cognitive_diversity.compute(mixed)["DecisionCount"] == 2.0


def test_single_group_event_does_not_saturate_the_rate():
    """Un événement isolé ne doit pas valoir « 1000 par 1000 ticks ».

    Régression : la fenêtre de dénominateur était calculée sur les seuls
    événements de groupe. Un unique ``group_formed`` donnait une fenêtre de
    1 tick et un taux de 1000, indépendamment de la durée d'observation.
    """
    from echos.analysis import group_dynamics

    single = {
        "agents": [{"id": "A"}, {"id": "B"}],
        "events": [{"type": "group_formed", "tick": 5, "value": {"size": 2}}],
    }
    assert group_dynamics.compute(single)["GroupFormationRate"] == 1000.0

    # Sur une fenêtre d'observation de 20 ticks, le même événement isolé vaut
    # 50 par 1000 ticks et non 1000.
    windowed = dict(single)
    windowed["events"] = single["events"] + [
        {"type": "message_sent", "tick": tick} for tick in range(1, 21)
    ]
    assert group_dynamics.compute(windowed)["GroupFormationRate"] == 50.0


def test_group_success_rate_reads_boolean_flags():
    from echos.analysis import group_dynamics

    snapshot = {
        "agents": [{"id": "A"}, {"id": "B"}],
        "events": [
            {"type": "group_dissolved", "tick": 1, "value": {"success": True, "lifetime": 5}},
            {"type": "group_dissolved", "tick": 2, "value": {"success": False, "lifetime": 3}},
        ],
    }
    result = group_dynamics.compute(snapshot)

    assert result["DissolvedGroupSuccessShare"] == 0.5
    assert result["AverageGroupLifetime"] == 4.0
    assert result["DissolutionCount"] == 2.0
    assert result["MemberExitsPerDissolution"] == 0.0  # memberOut absent du contrat


# ---------------------------------------------------------------------------
# Golden files de référence (ECHOS-027)
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("engine", ENGINES, ids=lambda engine: engine.ENGINE_NAME)
def test_compute_matches_golden_files(engine):
    golden = _golden()[engine.ENGINE_NAME]

    result = engine.compute(_fresh_snapshot())

    assert set(result) == set(golden)
    for metric, expected in golden.items():
        if isinstance(expected, (dict, list, str)):
            assert result[metric] == expected
        elif isinstance(expected, int):
            assert result[metric] == expected
        else:
            assert result[metric] == _APPROX(expected, rel=1e-9, abs=1e-9)


def test_golden_metrics_are_reproducible_across_runs():
    """Deux exécutions fraîches → golden identique (ECHOS-027 J2)."""
    first = {engine.ENGINE_NAME: engine.compute(_fresh_snapshot()) for engine in ENGINES}
    random.seed(7)  # le calcul ne doit pas dépendre de l'état global
    second = {engine.ENGINE_NAME: engine.compute(_fresh_snapshot()) for engine in ENGINES}

    assert first == second
    assert first == _golden()


# ---------------------------------------------------------------------------
# Helpers (noms explicites pour lisibilité des échecs)
# ---------------------------------------------------------------------------


def _cognitive():
    from echos.analysis import cognitive_diversity

    return cognitive_diversity.compute(_fresh_snapshot())


def _information():
    from echos.analysis import information_propagation

    return information_propagation.compute(_fresh_snapshot())


def _social():
    from echos.analysis import social_complexity

    return social_complexity.compute(_fresh_snapshot())


def _convergence():
    from echos.analysis import goal_convergence

    return goal_convergence.compute(_fresh_snapshot())


def _loops():
    from echos.analysis import feedback_loop_detector

    return feedback_loop_detector.compute(_fresh_snapshot())


def _resources():
    from echos.analysis import resource_sustainability

    return resource_sustainability.compute(_fresh_snapshot())


def _groups():
    from echos.analysis import group_dynamics

    return group_dynamics.compute(_fresh_snapshot())


# ---------------------------------------------------------------------------
# Provenance : distinguer une valeur mesurée d'un repli neutre
# ---------------------------------------------------------------------------


def test_provenance_marks_windowed_metrics_as_unmeasured_without_windows():
    """Sans fenêtres, les métriques fenêtrées sont déclarées non mesurées.

    C'est le défaut structurel que les 7 métriques mortes ont exploité : leur
    repli 0.0 s'écrivait en base exactement comme un 0.0 observé.
    """
    from echos.analysis import provenance

    snapshot = _fresh_snapshot()
    bare = {
        key: value
        for key, value in snapshot.items()
        if key not in ("history", "communityHistory", "events", "eventWindow")
    }
    flags = provenance(bare)

    assert flags["FeedbackLoopDetector"]["RepeatedActionShare"] is False
    assert flags["ResourceSustainabilityMetrics"]["RecoveryTime"] is False
    assert flags["SocialComplexityMetrics"]["CommunitySizeMatch"] is False
    assert flags["GroupDynamicsMetrics"]["GroupFormationRate"] is False
    # Le composite propage : un score d'émergence bâti sur des replis n'est pas
    # un score mesuré.
    assert flags["EmergenceIndicators"]["EmergenceScore"] is False
    # Les moteurs purement instantanés restent mesurés.
    assert flags["CognitiveDiversityMetrics"]["BeliefDiversity"] is True
    assert flags["GoalConvergenceMetrics"]["GlobalGoalAlignment"] is True
    assert flags["SocialComplexityMetrics"]["NetworkDensity"] is True


def test_provenance_is_complete_with_the_pipeline_windows():
    """Avec les fenêtres du pipeline, tout est mesuré et déclaré."""
    from echos.analysis import provenance

    flags = provenance(_fresh_snapshot())
    unmeasured = {
        engine: sorted(m for m, ok in metrics.items() if not ok)
        for engine, metrics in flags.items()
    }
    assert unmeasured == {
        engine: [] for engine in sorted(flags)
    }, unmeasured


def test_provenance_ignores_a_zero_valued_window():
    """Une fenêtre présente mais vide n'est pas « observée ».

    Le cas ``CommunitySizeMatch`` : sans communautés, l'historique peut contenir
    des ticks avec des tailles vides. La fenêtre est vide — c'est la condition
    pour que la métrique ait été calculée sur des données.
    """
    from echos.analysis import provenance

    snapshot = _fresh_snapshot()
    snapshot["communityHistory"] = []
    flags = provenance(snapshot)
    assert flags["SocialComplexityMetrics"]["CommunitySizeMatch"] is False
    # Les autres moteurs ne dépendent pas de cette clé.
    assert flags["CognitiveDiversityMetrics"]["GoalDiversity"] is True


def test_measured_flags_covers_every_declared_metric():
    """Aucun moteur n'a de métrique sans drapeau : la couverture est totale."""
    from echos.analysis import provenance

    snapshot = _fresh_snapshot()
    flags = provenance(snapshot)
    for engine in ENGINES:
        assert set(flags[engine.ENGINE_NAME]) == set(engine.METRICS)
