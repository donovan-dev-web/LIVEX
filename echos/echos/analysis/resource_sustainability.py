"""Moteur 6 — ResourceSustainabilityMetrics (durabilité des ressources).

Mesure ce qui est **observé** sur les réserves et leur consommation
(METRICS_SPEC.md §7) à partir de la clé ``resources`` du snapshot (réserves des
biotopes), des événements ``resource_consumed`` (ADR-004) et de l'historique
``history`` (réserves par tick).

Refonte P1 (RAPPORT §3.6) : l'ancien ``ResourceToConsumptionRatio`` mélangeait
deux dénominateurs incompatibles — ``quantity / consumed`` (sans unité de
temps, potentiellement > 1 sans borne) et ``quantity / capacity`` (part de
capacité) — puis comparait ce mélange à 0,2 dans ``CriticalityPoints`` : le
seuil n'avait aucun sens uniforme. Les mesures sont désormais séparées :

===========================  ================================================
Mesure                       Ce qu'elle observe
===========================  ================================================
``ResourceFillRatio``        part de **capacité** restante (moyenne des
                             réserves dont la capacité est publiée)
``CriticalResourceCount``    réserves sous 20 % de leur capacité publiée
``ResourceCoverage``         part de réserves dont la capacité est connue
``ConsumptionPerTick``       volume consommé par tick observé (flux)
``RecoveryTime``             durée moyenne d'un cycle critique → ≥ 80 %
``RecoveryEpisodes``         cycles critiques **complets** observés
``UnresolvedCrisisCount``    crises ouvertes (sous 20 %) non résolues en fin
                             de fenêtre
===========================  ================================================

Les crises non résolues et la couverture sont publiées séparément : 0 épisode
complet ne signifie ni « aucune crise » ni « crise non rétablie ».
"""

from __future__ import annotations

from ._common import event_window_published, events_of, event_values, mean, safe_ratio

ENGINE_NAME = "ResourceSustainabilityMetrics"

METRICS = (
    "ResourceFillRatio",
    "CriticalResourceCount",
    "ResourceCoverage",
    "ConsumptionPerTick",
    "RecoveryTime",
    "RecoveryEpisodes",
    "UnresolvedCrisisCount",
)

REQUIRES = {
    "ConsumptionPerTick": event_window_published,
    "RecoveryTime": "history",
    "RecoveryEpisodes": "history",
    "UnresolvedCrisisCount": "history",
}
"""Clés de contexte sans lesquelles la métrique n'est pas mesurée.

Les trois mesures d'épisode lisent ``history`` : sans lui, 0.0 confondait
« aucune crise observée » et « aucun historique ». ``ConsumptionPerTick`` a
besoin d'une fenêtre d'événements publiée (une fenêtre vide = zéro observé).
Les deux mesures de stock lisent le snapshot courant, donc toujours mesurées.
"""

_CRITICAL_RATIO = 0.2
_RECOVERED_RATIO = 0.8


def _capacity_ratio(quantity: float, capacity: float | None) -> float | None:
    """Part de capacité restante, ou ``None`` si la capacité n'est pas publiée.

    Borne haute à 1.0 : une réserve au-delà de sa capacité publiée est
    **saturée** — compter un remplissage de 300 % rendrait les moyennes
    inexploitables et réintroduirait le problème de bornes du ratio ancien.
    """
    if capacity is None or capacity <= 0.0:
        return None
    return max(0.0, min(1.0, quantity / capacity))


def _consumption_by_type(snapshot: dict) -> dict[str, float]:
    """Consommation cumulée par type de ressource (``resource_consumed``)."""
    consumed: dict[str, float] = {}
    for event in events_of(snapshot, event_type="resource_consumed"):
        value = event_values(event)
        resource_type = str(value.get("type") or "unknown")
        consumed[resource_type] = consumed.get(resource_type, 0.0) + float(
            value.get("amount") or 0.0
        )
    return consumed


def _event_window_ticks(snapshot: dict) -> int:
    """Durée réellement observée de la fenêtre d'événements (ticks, min 1)."""
    published = snapshot.get("eventWindow") or {}
    if isinstance(published, dict) and published.get("ticks"):
        return max(1, int(published["ticks"]))
    ticks = [
        int(event.get("tick") or 0)
        for event in snapshot.get("events") or []
    ]
    return max(1, max(ticks) - min(ticks) + 1) if ticks else 1


def _episodes(snapshot: dict) -> tuple[float, int, int, float | None]:
    """``(durée moyenne, cycles complets, crises non résolues, dernier ratio)``.

    Parcours chronologique de ``history`` (``{"tick", "resources": [...]}``) :
    une **crise** s'ouvre quand la part de capacité moyenne passe sous 20 %,
    elle se résout à un retour ≥ 80 %. Les crises encore ouvertes à la fin de
    la fenêtre sont comptées séparément — elles ne valent ni durée 0 ni durée
    observée (censure explicite).
    """
    history = snapshot.get("history") or []
    if not history:
        return 0.0, 0, 0, None

    def ratio_of(entry: dict) -> float | None:
        ratios = []
        for resource in entry.get("resources") or []:
            ratio = _capacity_ratio(
                float(resource.get("quantity") or 0.0),
                (
                    float(resource["capacity"])
                    if resource.get("capacity") is not None
                    else None
                ),
            )
            if ratio is not None:
                ratios.append(ratio)
        return mean(ratios) if ratios else None

    durations: list[float] = []
    unresolved = 0
    crash_tick: int | None = None
    last_ratio: float | None = None
    for entry in sorted(history, key=lambda item: int(item.get("tick") or 0)):
        ratio = ratio_of(entry)
        if ratio is None:
            continue
        last_ratio = ratio
        tick = int(entry.get("tick") or 0)
        if crash_tick is None and ratio < _CRITICAL_RATIO:
            crash_tick = tick
        elif crash_tick is not None and ratio >= _RECOVERED_RATIO:
            durations.append(float(tick - crash_tick))
            crash_tick = None

    if crash_tick is not None:
        unresolved += 1
    return mean(durations) if durations else 0.0, len(durations), unresolved, last_ratio


def compute(snapshot: dict) -> dict:
    """Calcule les 7 métriques de durabilité des ressources sur un snapshot."""
    resources = snapshot.get("resources") or []
    consumed = _consumption_by_type(snapshot)

    fill_ratios: list[float] = []
    critical = 0
    for resource in resources:
        quantity = float(resource.get("quantity") or 0.0)
        capacity = (
            float(resource["capacity"]) if resource.get("capacity") is not None else None
        )
        ratio = _capacity_ratio(quantity, capacity)
        if ratio is None:
            continue
        fill_ratios.append(ratio)
        if ratio < _CRITICAL_RATIO:
            critical += 1

    recovery_time, episodes, unresolved, _ = _episodes(snapshot)
    consumption = sum(consumed.values())

    return {
        # Part de capacité restante, uniquement sur les réserves dont la
        # capacité est publiée ; couverture publiée à côté pour que la moyenne
        # soit lisible (« 2 réserves sur 3 avec capacité connue »).
        "ResourceFillRatio": mean(fill_ratios) if fill_ratios else 0.0,
        "CriticalResourceCount": float(critical),
        "ResourceCoverage": safe_ratio(len(fill_ratios), len(resources)),
        "ConsumptionPerTick": safe_ratio(
            consumption, float(_event_window_ticks(snapshot))
        ),
        "RecoveryTime": recovery_time,
        "RecoveryEpisodes": float(episodes),
        "UnresolvedCrisisCount": float(unresolved),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
