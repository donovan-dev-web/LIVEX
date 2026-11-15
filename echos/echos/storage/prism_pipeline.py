# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Analyse d'un flux PRISM persisté (P10 / ECHOS-121 et ECHOS-122).

L'ingestion PRISM conserve les enveloppes brutes (v8/v9 : séquence, tick,
``kind``, ``payload``, ``contentHash``). Ce module est le **consommateur** de ce
flux : il le rejoue et en projette les métriques que le contrat autorise
(stocks, production, usure, quatre indicateurs de coopération).

Deux garanties de conception :

- **Aucune écriture dans le monde observé** (règle d'or) : le rejeu lit la base
  d'analyse du composant, jamais le host PRISM.
- **Rejeu idempotent** : les lignes sont réécrites par clé ``(run_id, tick,
  engine, metric)``. Rejouer un run déjà analysé redonne exactement les mêmes
  valeurs, sans doublon — condition d'une comparaison de runs fiable.

Le rejeu est **séquentiel par tick** : les enveloppes d'un même tick sont
appliquées avant calcul (``entity_snapshot`` puis ``tick_result`` puis
``metrics``), et les états d'entités sont reportés d'un tick à l'autre tant
qu'aucune nouvelle enveloppe ne les remplace — un inventaire observé reste
vrai jusqu'à la prochaine observation, jamais remis à zéro par silence.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import TYPE_CHECKING

from echos.analysis.prism_projection import (
    INDICATOR_ENGINE,
    RESOURCE_ENGINE,
    project_indicators,
    project_resource_metrics,
)

if TYPE_CHECKING:
    from echos.storage.sqlite import AnalyticsStore

_INDICATOR_CONTEXT = "prismIndicators"
"""Type de contexte publiant les valeurs **et** leur provenance par tick."""

_ENTITY_KINDS = ("world_snapshot", "entity_snapshot")
"""Enveloppes portant les inventaires d'agents (le monde ne les publie pas
toujours : ``entity_snapshot`` est la source nominale après ``Incarner``)."""


class PrismAnalysisError(ValueError):
    """Analyse demandée sur un run sans flux PRISM persisté."""


@dataclass(frozen=True)
class PrismConsumeResult:
    """Compteurs d'un rejeu d'analyse PRISM (déterministes, sans horodatage)."""

    ticks_analyzed: int
    metrics_written: int
    resource_ticks: int
    indicator_ticks: int
    last_tick: int

    def to_dict(self) -> dict:
        return {
            "ticks_analyzed": self.ticks_analyzed,
            "metrics_written": self.metrics_written,
            "resource_ticks": self.resource_ticks,
            "indicator_ticks": self.indicator_ticks,
            "last_tick": self.last_tick,
        }


def _payload_of(envelope: dict) -> dict:
    """Charge utile d'une enveloppe relue du magasin.

    Deux formes coexistent : l'enveloppe brute (``payload`` = charge utile) et
    l'enveloppe sérialisée en entier dans la colonne ``payload`` par le socle
    v8. Reconnaître la seconde (présence de ``protocolVersion`` et d'un
    ``payload`` imbriqué) évite qu'un consommateur lise l'enveloppe comme une
    charge utile vide — ce qui produisait des métriques toutes nulles et un
    run déclaré « non analysé » alors que le flux était bien conservé.
    """
    payload = envelope.get("payload")
    if not isinstance(payload, dict):
        return {}
    inner = payload.get("payload")
    if "protocolVersion" in payload and isinstance(inner, dict):
        return inner
    return payload


class _PrismState:
    """État reporté entre ticks : agents, événements du tick, métriques PRISM."""

    def __init__(self) -> None:
        self.agents: list[dict] = []
        self.agents_published = False
        self.events: list[dict] = []
        self.events_published = False
        self.metrics_payload: dict | None = None
        self.metrics_tick: int | None = None

    def apply(self, envelope: dict) -> None:
        kind = str(envelope["kind"])
        payload = _payload_of(envelope)

        if kind in _ENTITY_KINDS:
            agents = payload.get("agents")
            if agents is None:
                agents = payload.get("entities")
            if isinstance(agents, list):
                self.agents = [_normalize_agent(agent) for agent in agents]
                self.agents_published = True

        if kind == "tick_result":
            events = payload.get("events")
            if isinstance(events, list):
                # Les événements appartiennent à **ce** tick : une fenêtre
                # publiée mais vide est un zéro observé, mémorisé comme tel.
                self.events = list(events)
                self.events_published = True

        if kind == "metrics":
            self.metrics_payload = payload
            self.metrics_tick = int(envelope["tick"])

    def start_tick(self) -> None:
        """Réinitialise ce qui est propre à un tick (la fenêtre d'événements)."""
        self.events = []
        self.events_published = False

    def sources(self) -> dict:
        return {
            "metricsTick": self.metrics_tick,
            "agentsPublished": self.agents_published,
            "eventsPublished": self.events_published,
            "agentCount": len(self.agents),
        }


def _normalize_agent(agent: object) -> dict:
    """Réduit une entité PRISM à ce que la projection consomme : id, inventaire."""
    if not isinstance(agent, dict):
        return {"id": "", "inventory": []}
    identifier = agent.get("agentId", agent.get("id", agent.get("AgentId")))
    inventory = agent.get("inventory", agent.get("Inventory")) or []
    return {
        "id": "" if identifier is None else str(identifier),
        "inventory": inventory if isinstance(inventory, list) else [],
    }


def consume_prism(
    store: "AnalyticsStore",
    run_id: str,
    *,
    page_size: int = 1000,
) -> PrismConsumeResult:
    """Rejoue le flux PRISM persisté et écrit métriques et indicateurs.

    ``page_size`` borne la mémoire de lecture : les enveloppes sont lues par
    pages ordonnées par séquence, jamais toutes en une fois (un run de 5 000
    ticks en produit des dizaines de milliers).
    """
    if page_size <= 0:
        raise ValueError("page_size must be positive")
    if store.prism_read_summary(run_id) is None:
        raise PrismAnalysisError(
            f"aucun flux de lecture PRISM pour le run : {run_id}"
        )

    state = _PrismState()
    ticks_analyzed = metrics_written = resource_ticks = indicator_ticks = 0
    last_tick = -1
    pending_tick: int | None = None

    def flush(tick: int) -> None:
        nonlocal metrics_written, resource_ticks, indicator_ticks
        resource_values, resource_measured = project_resource_metrics(
            state.metrics_payload
        )
        indicator_values, indicator_measured = project_indicators(
            state.agents,
            state.events,
            state.metrics_payload,
            agents_published=state.agents_published,
            events_published=state.events_published,
        )
        if (
            not resource_values
            and not state.agents_published
            and not state.events_published
        ):
            # Rien d'observable sur ce tick : mieux vaut une absence de ligne
            # qu'un 0 qui se lirait « observé ».
            return
        metrics: dict[str, dict] = {}
        measured: dict[str, dict] = {}
        if resource_values:
            metrics[RESOURCE_ENGINE] = resource_values
            measured[RESOURCE_ENGINE] = resource_measured
        metrics[INDICATOR_ENGINE] = indicator_values
        measured[INDICATOR_ENGINE] = indicator_measured
        metrics_written += store.append_tick_metrics(
            run_id, tick, metrics, measured
        )
        store.append_tick_context(
            run_id,
            tick,
            _INDICATOR_CONTEXT,
            {
                "tick": tick,
                "engines": [engine for engine in metrics],
                "values": metrics,
                "measured": measured,
                "sources": state.sources(),
            },
        )
        if resource_values:
            resource_ticks += 1
        indicator_ticks += 1

    after_sequence = -1
    while True:
        page = store.prism_read_envelopes(
            run_id, after_sequence=after_sequence, limit=page_size
        )
        if not page:
            break
        for envelope in page:
            tick = int(envelope["tick"])
            if pending_tick is not None and tick != pending_tick:
                flush(pending_tick)
                ticks_analyzed += 1
                state.start_tick()
            state.apply(envelope)
            pending_tick = tick
            last_tick = tick
        after_sequence = int(page[-1]["sequence"])
        if len(page) < page_size:
            break

    if pending_tick is not None:
        flush(pending_tick)
        ticks_analyzed += 1

    return PrismConsumeResult(
        ticks_analyzed=ticks_analyzed,
        metrics_written=metrics_written,
        resource_ticks=resource_ticks,
        indicator_ticks=indicator_ticks,
        last_tick=last_tick,
    )


__all__ = ["PrismAnalysisError", "PrismConsumeResult", "consume_prism"]
