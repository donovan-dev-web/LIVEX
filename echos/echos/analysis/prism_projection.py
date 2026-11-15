# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Projection analytique du flux PRISM (P10 / ECHOS-121 et ECHOS-122).

ECHOS ne projette **que** ce que le contrat PRISM publie : les enveloppes
``metrics`` (stocks, matière par ``ruleId``, structures, réservations, effets,
échecs) et les inventaires d'agents des enveloppes ``world_snapshot`` /
``entity_snapshot``. Aucune métrique SYNE V1 (réserves globales, énergie,
faim) n'est reconstituée ici : le contrat PRISM ne les porte pas, et les
inventer rendrait toute comparaison fausse.

Deux moteurs distincts, volontairement hors du registre SYNE
(:mod:`echos.analysis`), pour une raison de contrat : leurs métriques
n'existent pas sur un snapshot SYNE, et les inscrire dans le registre commun
les aurait fait apparaître à 0 sur tout run V1 (régressions silencieuses des
goldens et des indicateurs d'émergence).

``PrismResourceMetrics`` — moteur 6 refondu sur les données PRISM
-----------------------------------------------------------------
Exigence ECHOS-121 : recalculer stocks, production et usure depuis les
snapshots PRISM, **sans** réutiliser les réserves globales SYNE V1. Sources :

- ``stocks`` — quantités détenues par les agents, les conteneurs et le sol ;
- ``matter`` — matière créée/détruite par ``ruleId`` (bilan net) ;
- usure — matière détruite par une règle d'usure/durabilité. La mesure reste
  **non mesurée** tant qu'aucune règle de ce type n'apparaît : un 0 mesuré
  affirmerait « aucune usure », ce que l'absence de la règle ne prouve pas ;
- ``structures``/``reservedStations``/``activeStructureEffects``/``failures``.

``PrismCooperationMetrics`` — les quatre indicateurs de P10 (ECHOS-122)
----------------------------------------------------------------------
``CooperationIndex`` (densité de transferts), ``SpecializationIndex``
(concentration des inventaires par agent), ``InfrastructureIndex``
(avancement × intégrité des structures) et ``StockInequalityIndex`` (Gini des
stocks par agent). Chaque valeur est accompagnée de son drapeau de mesure, ce
qui distingue trois états : **mesuré**, **zéro observé** et **absent** (le plan
d'implémentation l'exige explicitement).

Toutes les fonctions sont **pures** et déterministes : ordre stable par
identifiant trié, aucune consommation de PRNG, aucun horodatage.
"""

from __future__ import annotations

import json
from collections import Counter

from echos.analysis.common import clamp, mean, safe_ratio, shannon_normalized

RESOURCE_ENGINE = "PrismResourceMetrics"
"""Nom du moteur de ressources PRISM (moteur 6 refondu)."""

RESOURCE_METRICS = (
    "TotalStockedQuantity",
    "AgentStockRatio",
    "ContainerStockRatio",
    "GroundStockRatio",
    "MatterCreated",
    "MatterDestroyed",
    "NetMatterBalance",
    "WearDestroyedQuantity",
    "StructureCount",
    "ReservedStations",
    "ActiveStructureEffects",
    "CommandFailureCount",
)
"""Métriques du moteur de ressources (ordre d'émission stable)."""

_WEAR_RULE_MARKERS = ("wear", "usure", "durab")
"""Fragments de ``ruleId`` désignant une destruction par usure/durabilité."""

INDICATOR_ENGINE = "PrismCooperationMetrics"
"""Nom du moteur des quatre indicateurs P10."""

INDICATOR_METRICS = (
    "CooperationIndex",
    "SpecializationIndex",
    "InfrastructureIndex",
    "StockInequalityIndex",
    "TransferEvents",
    "TransferVolume",
    "ContributionEvents",
    "ActiveStructureEffects",
    "AgentStockSample",
)
"""Indicateurs et mesures de soutien (ordre d'émission stable)."""

TRANSFER_EVENT = "item_transferred"
"""Événement PRISM d'un transfert agent → agent (coopération matérielle)."""

_CONTRIBUTION_MARKERS = ("contribut", "site_progress", "site_completed")
"""Types d'événements témoignant d'une contribution à un chantier."""


def _get(mapping: object, key: str, default: object = None) -> object:
    """Lit ``key`` en camelCase puis en PascalCase (transport PRISM).

    ``ProtocolJson`` sérialise en camelCase, mais une enveloppe relue depuis un
    checkpoint ou un rejeu peut porter la forme PascalCase du DTO .NET. Tolérer
    les deux évite qu'une projection entière retombe à zéro sur une différence
    de casse — sans jamais inventer de valeur : la clé absente reste absente.
    """
    if not isinstance(mapping, dict):
        return default
    if key in mapping:
        return mapping[key]
    pascal = key[:1].upper() + key[1:]
    if pascal in mapping:
        return mapping[pascal]
    return default


def _number(mapping: object, key: str, default: float = 0.0) -> float:
    """Nombre d'un champ, 0.0 si absent ou non numérique."""
    value = _get(mapping, key, None)
    if value is None or isinstance(value, bool):
        return default
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def inventory_totals(inventory: object) -> dict[str, float]:
    """Quantités par type d'objet d'un inventaire PRISM.

    Les piles non positives et les lignes sans identifiant d'objet sont
    ignorées : elles ne portent aucune matière détenue.
    """
    totals: dict[str, float] = {}
    for stack in inventory or []:
        item = _get(stack, "itemTypeId", None)
        quantity = _number(stack, "quantity")
        if item is None or quantity <= 0.0:
            continue
        key = str(item)
        totals[key] = totals.get(key, 0.0) + quantity
    return totals


def agent_stock_totals(agents: list[dict]) -> list[float]:
    """Quantités totales détenues par chaque agent (ordre des agents fournis).

    Un agent sans inventaire publié contribue 0.0 : c'est un **zéro observé**
    (il ne détient rien), pas une absence — la provenance de l'indicateur
    porte l'absence d'inventaire, elle, au niveau du tick.
    """
    totals = []
    for agent in agents or []:
        stacks = inventory_totals(_get(agent, "inventory", []) or [])
        totals.append(sum(stacks.values()))
    return totals


def gini(values: list[float]) -> float:
    """Coefficient de Gini (0 = égalité parfaite, 1 = concentration maximale).

    Formule sur valeurs triées : ``G = 2·Σ(i·xᵢ)/(n·Σxᵢ) − (n+1)/n``. Rend 0.0
    sur moins de deux valeurs ou sur un total nul : sans distribution, il n'y a
    pas d'inégalité *mesurable* (le drapeau de mesure porte ce cas).
    """
    n = len(values)
    if n < 2:
        return 0.0
    ordered = sorted(float(value) for value in values)
    total = sum(ordered)
    if total <= 0.0:
        return 0.0
    cumulative = sum(index * value for index, value in enumerate(ordered, start=1))
    return clamp((2.0 * cumulative) / (n * total) - (n + 1) / n)


def _stocks_split(payload: dict) -> tuple[float, float, float]:
    """Sommes (agents, conteneurs, sol) sur les stocks publiés."""
    agents = containers = ground = 0.0
    for stock in payload.get("stocks") or []:
        agents += _number(stock, "agents")
        containers += _number(stock, "containers")
        ground += _number(stock, "groundItems")
    return agents, containers, ground


def _matter_totals(payload: dict) -> tuple[float, float, float, bool]:
    """``(créée, détruite, détruite par usure, usure mesurée)``."""
    created = destroyed = wear = 0.0
    wear_published = False
    for entry in payload.get("matter") or []:
        created += _number(entry, "created")
        destroyed_here = _number(entry, "destroyed")
        destroyed += destroyed_here
        rule_id = str(_get(entry, "ruleId", "") or "").lower()
        if any(marker in rule_id for marker in _WEAR_RULE_MARKERS):
            wear += destroyed_here
            wear_published = True
    return created, destroyed, wear, wear_published


def project_resource_metrics(payload: dict | None) -> tuple[dict[str, float], dict[str, bool]]:
    """Projette une enveloppe ``metrics`` PRISM en métriques de ressources.

    ``payload`` ``None`` (aucune enveloppe ``metrics`` reçue) rend des
    dictionnaires vides : c'est l'état **absent**, distinct d'un zéro observé.
    Sinon toutes les mesures sont considérées mesurées, sauf l'usure, dont la
    mesure dépend de la présence d'une règle d'usure dans la comptabilité.
    """
    if payload is None:
        return {}, {}

    agent_stock, container_stock, ground_stock = _stocks_split(payload)
    total = agent_stock + container_stock + ground_stock
    created, destroyed, wear, wear_published = _matter_totals(payload)

    structures = payload.get("structures") or []
    failures = payload.get("failures") or []

    values = {
        "TotalStockedQuantity": total,
        "AgentStockRatio": safe_ratio(agent_stock, total),
        "ContainerStockRatio": safe_ratio(container_stock, total),
        "GroundStockRatio": safe_ratio(ground_stock, total),
        "MatterCreated": created,
        "MatterDestroyed": destroyed,
        "NetMatterBalance": created - destroyed,
        "WearDestroyedQuantity": wear,
        "StructureCount": float(len(structures)),
        "ReservedStations": _number(payload, "reservedStations"),
        "ActiveStructureEffects": _number(payload, "activeStructureEffects"),
        "CommandFailureCount": sum(_number(failure, "count") for failure in failures),
    }
    measured = {metric: True for metric in RESOURCE_METRICS}
    measured["WearDestroyedQuantity"] = wear_published
    return values, measured


def event_type(event: object) -> str:
    """Type d'un événement PRISM (camelCase ou PascalCase)."""
    return str(_get(event, "type", "") or "")


def event_value(event: object) -> dict:
    """Charge ``value`` d'un événement, qu'elle soit typée ou sérialisée JSON."""
    value = _get(event, "value", None)
    if isinstance(value, str):
        try:
            parsed = json.loads(value)
        except ValueError:
            return {}
        return parsed if isinstance(parsed, dict) else {}
    return value if isinstance(value, dict) else {}


def _event_quantity(event: object) -> float:
    """Quantité d'un événement : ``quantity``, ``amount`` puis ``count``."""
    value = event_value(event)
    for key in ("quantity", "amount", "count"):
        raw = _get(value, key, None)
        if raw is None or isinstance(raw, bool):
            continue
        try:
            return float(raw)
        except (TypeError, ValueError):
            continue
    return 0.0


def _specialization(agents: list[dict]) -> tuple[float, int]:
    """``(spécialisation moyenne, agents portant un inventaire)``.

    Spécialisation d'un agent = ``1 − H_normalisée`` de la distribution de ses
    types d'objets : un agent qui ne détient qu'une sorte d'objet est
    spécialisé (1.0), un agent qui détient toutes les sortes à parts égales ne
    l'est pas (0.0). Moyenne sur les agents **avec inventaire** : les agents
    vides n'ont pas de distribution à mesurer et ne doivent pas tirer la
    moyenne vers le bas.
    """
    scores: list[float] = []
    for agent in agents or []:
        totals = inventory_totals(_get(agent, "inventory", []) or [])
        if not totals:
            continue
        counter: Counter = Counter({key: value for key, value in totals.items()})
        scores.append(clamp(1.0 - shannon_normalized(counter)))
    return (mean(scores) if scores else 0.0), len(scores)


def _infrastructure(payload: dict | None) -> float:
    """Indice d'infrastructure : moyenne d'``avancement × intégrité``.

    Les deux valeurs sont publiées en pour mille (0…1000) : le produit divisé
    par 1 000 000 rend une part dans [0, 1]. Un chantier achevé et intact vaut
    1.0 ; un chantier planifié ou ruiné vaut 0.
    """
    if payload is None:
        return 0.0
    scores: list[float] = []
    for structure in payload.get("structures") or []:
        progress = clamp(_number(structure, "progressPermille") / 1000.0)
        integrity = clamp(_number(structure, "integrityPermille") / 1000.0)
        scores.append(progress * integrity)
    return mean(scores) if scores else 0.0


def project_indicators(
    agents: list[dict],
    events: list[dict],
    metrics_payload: dict | None,
    *,
    agents_published: bool,
    events_published: bool,
) -> tuple[dict[str, float], dict[str, bool]]:
    """Calcule les quatre indicateurs P10 et leurs mesures de soutien.

    ``agents_published`` / ``events_published`` viennent du consommateur : ils
    distinguent « aucune entité / aucun événement dans la fenêtre » (zéro
    observé) de « cette source n'a jamais été publiée sur ce tick » (absent).
    Sans eux, les deux cas s'écriraient identiquement à 0.
    """
    population = [agent for agent in agents or []]
    stock_totals = agent_stock_totals(population)

    transfers = [event for event in events or [] if event_type(event) == TRANSFER_EVENT]
    transfer_volume = sum(_event_quantity(event) for event in transfers)
    contributions = [
        event
        for event in events or []
        if any(marker in event_type(event).lower() for marker in _CONTRIBUTION_MARKERS)
    ]

    specialization, sample = _specialization(population)
    agent_count = max(1, len(population))

    values = {
        # Densité de transferts : un transfert par agent et par tick sature
        # l'indice (1.0). Les transferts sont la seule coopération matérielle
        # que le contrat PRISM publie (``item_transferred``).
        "CooperationIndex": clamp(len(transfers) / agent_count),
        "SpecializationIndex": specialization,
        "InfrastructureIndex": _infrastructure(metrics_payload),
        "StockInequalityIndex": gini(stock_totals),
        "TransferEvents": float(len(transfers)),
        "TransferVolume": transfer_volume,
        "ContributionEvents": float(len(contributions)),
        "ActiveStructureEffects": (
            _number(metrics_payload, "activeStructureEffects")
            if metrics_payload is not None
            else 0.0
        ),
        "AgentStockSample": float(sample),
    }

    metrics_published = metrics_payload is not None
    measured = {
        "CooperationIndex": events_published,
        "SpecializationIndex": agents_published,
        "InfrastructureIndex": metrics_published,
        "StockInequalityIndex": agents_published,
        "TransferEvents": events_published,
        "TransferVolume": events_published,
        "ContributionEvents": events_published,
        "ActiveStructureEffects": metrics_published,
        "AgentStockSample": agents_published,
    }
    return values, measured


__all__ = [
    "INDICATOR_ENGINE",
    "INDICATOR_METRICS",
    "RESOURCE_ENGINE",
    "RESOURCE_METRICS",
    "TRANSFER_EVENT",
    "agent_stock_totals",
    "event_type",
    "event_value",
    "gini",
    "inventory_totals",
    "project_indicators",
    "project_resource_metrics",
]
