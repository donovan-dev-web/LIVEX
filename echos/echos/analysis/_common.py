"""Utilitaires déterministes communs aux 8 moteurs (METRICS_SPEC.md §9).

Toutes les fonctions sont **pures** : mêmes entrées → mêmes sorties (aucune
consommation de PRNG, ordres stables par tri sur les identifiants). Elles
tolèrent l'absence de données et retournent 0.0 (neutral) en l'absence de
 mesures, conformément au contrat V0.1 (calibration à venir).

Ce module est aussi le point de partage des lectures du snapshot : les
moteurs qui dérivaient chacun leur propre copie de la même extraction
(voisinage de confiance, objectifs actifs, triplets de croyances) les
consomment ici, ce qui garantit qu'ils observent tous exactement la même
population et rend impossible une divergence silencieuse entre moteurs.
"""

from __future__ import annotations

import json
from collections import Counter
from math import log2

_IDLE = "Idle"
"""Activité de repli : entité sans objectif ni action courante déclarée."""

COMPOSITE_ENGINE_NAME = "EmergenceIndicators"
"""Nom du moteur composite.

Déclaré ici, et non dans ``emergence.py``, pour que le registre central
(``analysis/__init__``) et le moteur lui-même partagent une source unique :
le registre doit savoir quel moteur ne pas exécuter avant le composite
(celle-ci compose à partir des résultats des autres). Une constante locale à
``emergence.py`` créerait une dépendance circulaire à l'import et
réintroduirait le risque de divergence de nom entre les deux côtés.
"""

_BASE_ENGINE_MODULES = (
    "cognitive_diversity",
    "information_propagation",
    "social_complexity",
    "goal_convergence",
    "feedback_loop_detector",
    "resource_sustainability",
    "group_dynamics",
)
"""Modules des moteurs de métriques, dans l'ordre d'émission du registre."""

_ENGINES: tuple = ()


def load_engines() -> tuple:
    """Importe et renvoie les moteurs du registre, dans l'ordre stable.

    Import paresseux et mémoïsé : le registre est construit au premier appel,
    ce qui évite une dépendance circulaire (``emergence`` a besoin du registre
    pour ``compute``). Le composite est ajouté en dernier, puisqu'il compose à
    partir des résultats des autres. ``import_module`` puisant dans
    ``sys.modules``, la mémoïsation ne fige que des objets déjà importés.
    """
    global _ENGINES
    if not _ENGINES:
        from importlib import import_module

        prefix = __package__
        _ENGINES = tuple(
            import_module(f"{prefix}.{name}") for name in _BASE_ENGINE_MODULES
        ) + (import_module(f"{prefix}.emergence"),)
    return _ENGINES


def measured_flags(snapshot: dict, engine: object) -> dict[str, bool]:
    """Drapeaux de mesure d'un moteur, lus dans sa déclaration ``REQUIRES``.

    Chaque moteur déclare, pour chacune de ses métriques, la **clé de contexte**
    dont la présence conditionne la mesure (``history``, ``communityHistory``,
    ``events``). Une métrique sans exigence est toujours mesurée dès que le
    moteur s'exécute ; une métrique exigeant ``history`` est mesurée seulement
    si ``history`` est non vide.

    Cette déclaration est la structure qui manquait pour distinguer une valeur
    calculée d'un repli neutre : sans elle, une métrique fenêtrée absente des
    données s'écrivait en base exactement comme une métrique réellement nulle,
    et l'UI ne pouvait rien afficher de plus.

    Deux formes de condition sont acceptées :

    - une **chaîne** : clé de contexte dont la présence *non vide* conditionne
      la mesure (``history``, ``communityHistory``) ;
    - un **appelable** : prédicat ``snapshot -> bool`` pour les cas où la
      présence brute ne suffit pas. Exemple : une fenêtre d'événements
      **publiée mais vide** (``eventWindow``) est une observation réelle
      (« zéro observé »), là où une fenêtre absente est « non mesuré ».
      C'est la distinction que le plan de tâches P1 impose entre
      *aucune occurrence dans la fenêtre* et *pas assez de couverture*.
    """
    requires = getattr(engine, "REQUIRES", None) or {}
    metrics = tuple(getattr(engine, "METRICS", ()))
    flags: dict[str, bool] = {}
    for metric in metrics:
        if metric not in requires:
            flags[metric] = True
            continue
        condition = requires[metric]
        flags[metric] = bool(condition(snapshot)) if callable(condition) else bool(
            snapshot.get(condition)
        )
    return flags


def event_window_published(snapshot: dict) -> bool:
    """Vrai si le pipeline a publié une fenêtre d'événements pour ce tick.

    Une fenêtre **vide** est une observation (« aucune occurrence dans la
    fenêtre » = zéro observé) ; l'absence de fenêtre est « non mesuré ».
    C'est la distinction que la présence brute de la clé ``events`` ne faisait
    pas : elle confondait un run silencieux avec un run non instrumenté.
    """
    return isinstance(snapshot.get("eventWindow"), dict) or bool(
        snapshot.get("events")
    )


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


def shannon_normalized(counter: Counter) -> float:
    """Entropie de Shannon **normalisée** : H(P) / log₂(k), donc bornée [0, 1].

    ``k`` = nombre de catégories observées (``len(counter)``). Une entropie
    brute n'est **pas** bornée à 1 : elle croît avec le nombre de catégories,
    ce qui rendait impossible d'additionner des entropies hétérogènes dans un
    indice composite (le ``clamp`` final saturait et détruisait toute
    discrimination dans la partie haute). Une seule catégorie → 0.0 ; aucun
    échantillon → 0.0 (aucune diversité observée).

    La normalisation porte sur les catégories **observées** : elle mesure la
    répartition effective, pas le nombre de catégories possibles (non connu du
    moteur).
    """
    if len(counter) < 2:
        return 0.0
    return clamp(shannon(counter) / log2(len(counter)))


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


def safe_ratio(numerator: float, denominator: float) -> float:
    """Rapport borné : 0.0 si le dénominateur s'annule.

    Les moteurs ne doivent jamais produire d'infini : une division par zéro
    était jusqu'ici contournée par un ``max(x, 1e-9)`` qui transformait un
    cas « rien à mesurer » en une valeur de l'ordre de 1e11, qui se propageait
    ensuite dans les moyennes et les seuils de détection.
    """
    return numerator / denominator if denominator else 0.0


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


def activity_of(agent: dict) -> str:
    """Vue déterministe d'activité (objectif courant) d'un agent.

    Privilégie le premier objectif ``goals[].kind`` puis ``currentAction``
    sinon ``Idle`` — l'identifiant sert de clé de regroupement des objectifs.
    """
    goals = agent.get("goals") or []
    if goals:
        kind = goals[0].get("kind")
        if kind:
            return str(kind)
    action = agent.get("currentAction")
    return str(action) if action else _IDLE


def goal_kinds(agents: list[dict]) -> list[str]:
    """Types d'objectifs actifs d'une population (ordre de la liste d'agents)."""
    return [activity_of(agent) for agent in agents]


def trust_edges(agents: list[dict]) -> set[frozenset[str]]:
    """Arêtes de confiance non orientées (pair, pair) pour toute relation > 0.

    Déduplique les arêtes (a→b et b→a fusionnées) pour les métriques de graphe.
    """
    edges: set[frozenset[str]] = set()
    for agent in agents:
        agent_id = str(agent.get("id"))
        for relation in agent.get("trust") or []:
            if float(relation.get("trust") or 0.0) > 0.0:
                peer = str(relation.get("peerId"))
                edges.add(frozenset((agent_id, peer)))
    return edges


def trust_edges_directed(agents: list[dict]) -> set[tuple[str, str]]:
    """Arêtes de confiance orientées (émetteur → pair) pour les relations > 0."""
    edges: set[tuple[str, str]] = set()
    for agent in agents:
        agent_id = str(agent.get("id"))
        for relation in agent.get("trust") or []:
            if float(relation.get("trust") or 0.0) > 0.0:
                edges.add((agent_id, str(relation.get("peerId"))))
    return edges


def trust_levels(agents: list[dict]) -> list[float]:
    """Tous les poids de confiance déclarés (0.0 exclu : relation inexistante)."""
    return [
        float(relation["trust"])
        for agent in agents
        for relation in agent.get("trust") or []
        if relation.get("trust") is not None
    ]


def neighbors_of(agents: list[dict]) -> dict[str, set[str]]:
    """Voisinage de confiance **orienté** ``{agentId → {peerId, …}}``.

    Chaque entité présente dans ``agents`` apparaît dans le dictionnaire, même
    isolée (voisinage vide) : la clé sert d'exposant stable pour les
    centralités, qui doivent porter sur toute la population.
    """
    neighbors: dict[str, set[str]] = {}
    for agent in agents:
        agent_id = str(agent.get("id"))
        peers = neighbors.setdefault(agent_id, set())
        for relation in agent.get("trust") or []:
            if float(relation.get("trust") or 0.0) > 0.0:
                peers.add(str(relation.get("peerId")))
    return neighbors


def agent_ids(agents: list[dict]) -> list[str]:
    """Identifiants d'agents triés (ordre d'itération déterministe)."""
    return sorted({str(agent.get("id")) for agent in agents})


def label_propagation(agents: list[dict]) -> dict[str, str]:
    """Propagation d'étiquettes déterministe → ``{agentId → communauté}``.

    Labels initiaux = identifiant ; mise à jour **asynchrone** (en place) dans
    l'ordre trié des identifiants, étiquette la plus fréquente chez les voisins
    (ex-aequo → plus petite), au plus 10 itérations.

    Les entités **isolées** (aucune relation de confiance) conservent leur
    propre étiquette : ce sont des singletons, pas des communautés. Elles
    restent donc présentes dans la table d'étiquettes — la fonction décrit le
    partitionnement complet — mais sont exclues par :func:`communities`, qui
    est la vue « groupes » réellement mesurée. Sans cette distinction, une
    population sans aucune relation était comptée comme ``N`` communautés et
    déclenchait à tort la détection du phénomène ``CommunityFormation``.
    """
    edges = trust_edges(agents)
    ids = agent_ids(agents)
    neighbors: dict[str, set[str]] = {agent_id: set() for agent_id in ids}
    for edge in edges:
        if len(edge) != 2:
            continue
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


def communities(agents: list[dict]) -> list[list[str]]:
    """Communautés de taille ≥ 2, membres triés, communautés triées.

    Vue « groupes » du partitionnement : les singletons sont exclus (une
    entité seule ne constitue pas un groupe). Ordre lexicographique sur les
    membres → déterminisme d'émission.
    """
    labels = label_propagation(agents)
    buckets: dict[str, list[str]] = {}
    for agent_id in sorted(labels):
        buckets.setdefault(str(labels[agent_id]), []).append(str(agent_id))
    groups = [sorted(members) for members in buckets.values() if len(members) > 1]
    return sorted(groups)


def community_sizes(agents: list[dict]) -> list[int]:
    """Tailles des communautés détectées, ordre décroissant."""
    return sorted((len(members) for members in communities(agents)), reverse=True)


def belief_facts(agents: list[dict]) -> list[tuple[str, str, str, float]]:
    """Croyances agrégées ``(subject, predicate, value, confidence)`` par agent."""
    facts: list[tuple[str, str, str, float]] = []
    for agent in agents:
        for belief in agent.get("beliefs") or []:
            facts.append(
                (
                    str(belief.get("subject")),
                    str(belief.get("predicate")),
                    str(belief.get("value")),
                    float(belief.get("confidence") or 0.0),
                )
            )
    return facts


def decision_actions(snapshot: dict) -> list[str]:
    """Actions effectivement décidées sur le tick, lues dans les événements.

    Source : événements ``decision_made`` du snapshot (``event.action``, repli
    ``value.intention``). C'est la seule lecture qui distingue une **décision**
    d'un **objectif** : les deux notions ne doivent pas être confondues sous la
    même métrique.
    """
    actions: list[str] = []
    for event in events_of(snapshot, event_type="decision_made"):
        action = event.get("action")
        if not action:
            action = (event.get("value") or {}).get("intention")
        if action:
            actions.append(str(action))
    return actions


def events_of(snapshot: dict, event_type: str | None = None) -> list[dict]:
    """Événements du snapshot (clé ``events``), éventuellement filtrés par type.

    Les événements sont ordonnés par ``tick`` puis ``agentId`` (déterminisme).
    """
    events = [dict(event) for event in snapshot.get("events") or []]
    if event_type is not None:
        events = [event for event in events if event.get("type") == event_type]
    return sorted(
        events,
        key=lambda event: (int(event.get("tick") or 0), str(event.get("agentId", ""))),
    )


def event_values(event: dict) -> dict:
    """Charge ``value`` d'un événement, qu'il soit déjà typé ou sérialisé."""
    value = event.get("value")
    if isinstance(value, str):
        try:
            parsed = json.loads(value)
        except ValueError:
            return {}
        return parsed if isinstance(parsed, dict) else {}
    return value if isinstance(value, dict) else {}
