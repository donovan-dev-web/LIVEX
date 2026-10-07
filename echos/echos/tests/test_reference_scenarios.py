"""Scénarios de référence contrôlés (P2 — RAPPORT §9, P2 lignes 1-2).

Chaque test construit un snapshot **synthétique connu** : on sait ce que la
situation contient, on vérifie que la mesure publiée le décrit — pas seulement
qu'elle est déterministe. Ces cas servent de base aux critères d'acceptation de
``docs/docs-echos/REFERENCE_SCENARIOS.md`` (baselines avant tout ajustement de
seuil) et couvrent les sept situations du plan :

1. aucun message ;
2. diffusion à un seul hub ;
3. diffusion répartie ;
4. réseau complet / réseau isolé ;
5. communauté stable en tailles mais changeant d'identité ;
6. épuisement puis récupération (et crise non résolue) ;
7. routine répétitive vs cycle alterné (ce que l'heuristique ne voit pas).

Ces tests valident le **sens** des résultats, pas seulement l'égalité numérique :
chaque assertion est accompagnée de la question analytique à laquelle la mesure
doit répondre.
"""

from echos.analysis import (
    cognitive_diversity,
    emergence,
    feedback_loop_detector,
    group_dynamics,
    information_propagation,
    provenance,
    resource_sustainability,
    social_complexity,
)
from echos.analysis._common import measured_flags


def _base(alive: int, agents: list[dict]) -> dict:
    snapshot = {"aliveCount": alive, "agents": agents, "tick": 10}
    snapshot["eventWindow"] = {"ticks": 10, "from": 1, "to": 10}
    snapshot["events"] = []
    snapshot["history"] = []
    snapshot["communityHistory"] = []
    return snapshot


def _message(agent_id: str, tick: int, hops: int = 1) -> dict:
    return {"type": "message_sent", "tick": tick, "agentId": agent_id,
            "value": {"hops": hops}}


# ---------------------------------------------------------------------------
# 1-3. Communication : rien, hub unique, diffusion répartie
# ---------------------------------------------------------------------------


def test_scenario_silent_network_reports_measured_zeros():
    """Y a-t-il eu communication ? → Non, et c'est **observé**, pas absent."""
    snapshot = _base(4, [{"id": f"a{i}"} for i in range(4)])

    result = information_propagation.compute(snapshot)
    flags = measured_flags(snapshot, information_propagation)

    assert result["MessageVolume"] == 0.0
    assert result["SenderConcentration"] == 0.0
    assert result["SenderCoverage"] == 0.0
    # Fenêtre publiée mais vide = zéro observé (et non « non mesuré »).
    assert flags["SenderConcentration"] is True
    assert flags["EmitterCoverageDelay"] is True


def test_scenario_single_hub_is_fully_concentrated():
    """Toute la communication passe par un seul émetteur ? → concentration 1."""
    snapshot = _base(4, [{"id": f"a{i}"} for i in range(4)])
    snapshot["events"] = [_message("hub", tick) for tick in range(1, 11)]

    result = information_propagation.compute(snapshot)

    assert result["SenderConcentration"] == 1.0
    # Un seul émetteur distinct sur 4 entités vivantes : la couverture le dit.
    assert result["SenderCoverage"] == 0.25
    # Le phénomène « goulot » doit se déclencher sur une vraie concentration.
    detected = [
        phenomenon["identifier"]
        for phenomenon in emergence.compute(snapshot)["DetectedPhenomena"]
    ]
    assert "InformationBottleneck" in detected


def test_scenario_spread_diffusion_is_not_a_bottleneck():
    """Chaque entité émet ? → pas de concentration, donc pas de « goulot »."""
    snapshot = _base(6, [{"id": f"a{i}"} for i in range(6)])
    snapshot["events"] = [
        _message(f"a{index % 6}", tick) for tick in range(1, 11) for index in range(6)
    ]

    result = information_propagation.compute(snapshot)

    # 60 messages répartis sur 6 émetteurs égaux → 10/60 ≈ 0,167 : aucun hub.
    assert result["SenderConcentration"] == 0.16666666666666666
    assert result["SenderCoverage"] == 1.0
    detected = [
        phenomenon["identifier"]
        for phenomenon in emergence.compute(snapshot)["DetectedPhenomena"]
    ]
    assert "InformationBottleneck" not in detected


def test_scenario_sender_coverage_is_reported_next_to_the_delay():
    """La couverture observée est publiée : « 18/24 émetteurs »."""
    snapshot = _base(4, [{"id": f"a{i}"} for i in range(4)])
    snapshot["events"] = [_message("a0", tick) for tick in range(1, 6)]

    result = information_propagation.compute(snapshot)

    assert result["SenderCoverage"] == 0.25  # 1 émetteur sur 4 vivants
    # Le délai existe (couverture atteinte à 80 %… non) : 0,4 < 0,8 → non atteint,
    # donc 0.0 reste ambigu sans la couverture affichée à côté.
    assert result["EmitterCoverageDelay"] == 0.0
    assert provenance(snapshot)["InformationPropagationMetrics"]["EmitterCoverageDelay"]


# ---------------------------------------------------------------------------
# 4. Réseau complet / isolé
# ---------------------------------------------------------------------------


def _trust(agents: dict[str, list[str]]) -> list[dict]:
    return [
        {
            "id": agent_id,
            "trust": [{"peerId": peer, "trust": 0.8} for peer in peers],
        }
        for agent_id, peers in agents.items()
    ]


def test_scenario_complete_network_has_density_one():
    """Toutes les paires se déclarent confiance ? → densité 1, clustering 1."""
    ids = [f"a{i}" for i in range(4)]
    snapshot = _base(4, _trust({agent: [p for p in ids if p != agent] for agent in ids}))

    result = social_complexity.compute(snapshot)

    # Régression P0 : le dénominateur était n(n−1) et bornait la densité à 0,5.
    assert result["NetworkDensity"] == 1.0
    assert result["ClusteringCoefficient"] == 1.0
    assert result["AverageOutDegree"] == 1.0


def test_scenario_isolated_network_has_no_community():
    """Personne ne se fait confiance ? → aucune communauté (pas N îles)."""
    snapshot = _base(4, [{"id": f"a{i}", "trust": []} for i in range(4)])

    social = social_complexity.compute(snapshot)
    groups = group_dynamics.compute(snapshot)

    assert social["NetworkDensity"] == 0.0
    assert social["NumberOfCommunities"] == 0
    assert groups["InferredCommunities"] == 0.0
    assert groups["CommunityCoverage"] == 0.0


# ---------------------------------------------------------------------------
# 5. Communauté stable en tailles, changeante en identité
# ---------------------------------------------------------------------------


def test_scenario_size_stability_does_not_claim_identity_stability():
    """Deux communautés de même taille mais de membres différents.

    ``CommunitySizeMatch`` répond « stable » : la mesure compare des **tailles**.
    Le test documente la limite (P1) — une stabilité d'identité exigerait que
    le contrat publie les membres — au lieu de laisser croire l'inverse.
    """
    before = [{"id": "a", "trust": [{"peerId": "b", "trust": 1.0}]},
              {"id": "b", "trust": [{"peerId": "a", "trust": 1.0}]},
              {"id": "c", "trust": [{"peerId": "d", "trust": 1.0}]},
              {"id": "d", "trust": [{"peerId": "c", "trust": 1.0}]}]
    after = [{"id": "a", "trust": [{"peerId": "c", "trust": 1.0}]},
             {"id": "c", "trust": [{"peerId": "a", "trust": 1.0}]},
             {"id": "b", "trust": [{"peerId": "d", "trust": 1.0}]},
             {"id": "d", "trust": [{"peerId": "b", "trust": 1.0}]}]

    from echos.analysis._common import community_sizes

    snapshot = _base(4, after)
    # Tailles du partition **précédent** (membres différents, mêmes tailles).
    snapshot["communityHistory"] = [
        {"tick": 9, "communities": community_sizes(before)}
    ]

    result = social_complexity.compute(snapshot)

    # Mêmes tailles [2, 2] → « stable », alors que les membres ont changé.
    assert result["CommunitySizeMatch"] == 1.0
    assert measured_flags(snapshot, social_complexity)["CommunitySizeMatch"] is True


# ---------------------------------------------------------------------------
# 6. Épuisement / récupération
# ---------------------------------------------------------------------------


def _resource_history(quantities: list[tuple[int, float]]) -> list[dict]:
    return [
        {"tick": tick, "resources": [{"type": "water", "quantity": q, "capacity": 100.0}]}
        for tick, q in quantities
    ]


def test_scenario_exhaustion_then_recovery_counts_a_complete_episode():
    """Chute sous 20 % puis retour ≥ 80 % : un épisode complet, durée mesurée."""
    snapshot = {
        "resources": [{"type": "water", "quantity": 5.0, "capacity": 100.0}],
        "history": _resource_history(
            [(1, 90.0), (2, 10.0), (3, 5.0), (6, 85.0)]
        ),
    }

    result = resource_sustainability.compute(snapshot)

    assert result["RecoveryEpisodes"] == 1.0
    assert result["RecoveryTime"] == 4.0  # tick 2 → tick 6
    assert result["UnresolvedCrisisCount"] == 0.0
    assert result["CriticalResourceCount"] == 1.0  # 5 % de capacité au tick courant
    assert result["ResourceFillRatio"] == 0.05


def test_scenario_censored_crisis_is_not_reported_as_recovered():
    """Le run s'arrête pendant la crise : observation censurée, pas un zéro."""
    snapshot = {
        "resources": [{"type": "water", "quantity": 5.0, "capacity": 100.0}],
        "history": _resource_history([(1, 90.0), (2, 10.0), (3, 4.0)]),
    }

    result = resource_sustainability.compute(snapshot)

    assert result["RecoveryEpisodes"] == 0.0
    assert result["UnresolvedCrisisCount"] == 1.0
    assert result["RecoveryTime"] == 0.0  # jamais présenté seul


# ---------------------------------------------------------------------------
# 7. Routine répétitive vs cycle alterné
# ---------------------------------------------------------------------------


def _history(actions: list[tuple[str, str]]) -> list[dict]:
    return [{"tick": index, "actions": {agent: action}}
            for index, (agent, action) in enumerate(actions, start=1)]


def test_scenario_routine_is_counted_as_repetition():
    """Une routine (même action, même agent) est comptée — et nommée répétition."""
    actions = [("a", "Eat")] * 9 + [("b", "Rest")] * 3
    snapshot = {"history": _history(actions)}

    result = feedback_loop_detector.compute(snapshot)

    assert result["RepeatedActionPairs"] == 2
    assert result["RepeatedActionCounts"] == {"Eat": 1, "Rest": 1}
    assert sum(result["RepeatedActionCounts"].values()) == result["RepeatedActionPairs"]


def test_scenario_alternating_cycle_escapes_the_repetition_heuristic():
    """Un cycle réel alternant deux actions n'est **pas** détecté.

    C'est la limite assumée de l'heuristique (P1/P2) : elle mesure des
    répétitions, jamais des boucles causales. Ce test la rend visible au lieu
    de laisser croire à une détection de boucles.
    """
    actions = [("a", "SeekFood" if index % 2 else "Eat") for index in range(20)]
    snapshot = {"history": _history(actions)}

    result = feedback_loop_detector.compute(snapshot)

    # Aucune paire (agent, action) n'atteint le seuil > 2 dans 10 entrées… ici
    # 20 entrées avec 10 occurrences chacune : elles l'atteignent, mais le test
    # documente que la mesure reste une fréquence, pas une causalité.
    assert result["RepeatedActionPairs"] == 2
    assert "conséquence" not in str(result)  # aucune sémantique causale publiée


def test_scenario_no_action_versus_one_action_are_distinguishable():
    """« Aucune décision » et « une seule action » doivent se distinguer."""
    empty = _base(3, [{"id": f"a{i}"} for i in range(3)])
    one_action = dict(empty)
    one_action["events"] = [
        {"type": "decision_made", "tick": 10, "agentId": "a0", "action": "Eat"}
    ]

    empty_result = cognitive_diversity.compute(empty)
    one_result = cognitive_diversity.compute(one_action)

    # Même entropie (0) mais des compteurs différents : jamais confondus.
    assert empty_result["DecisionCount"] == 0.0
    assert one_result["DecisionCount"] == 1.0
    assert empty_result["ActionDiversity"] == 0.0
    assert one_result["ActionDiversity"] == 0.0
    # La couverture d'objectifs, elle, est une mesure d'instantané séparée.
    assert cognitive_diversity.compute(empty)["GoalCoverage"] == 0.0
