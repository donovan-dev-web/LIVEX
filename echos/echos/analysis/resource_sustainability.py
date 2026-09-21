"""Moteur 6 — ResourceSustainabilityMetrics (durabilité des ressources).

Évalue l'équilibre entre disponibilité et consommation des ressources
(METRICS_SPEC.md §7) à partir de la clé ``resources`` du snapshot (réserves des
biotopes) et des événements ``resource_consumed`` (ADR-004). L'historique
optionnel ``history`` alimente le temps de récupération ; sans données → 0.0.
"""

from __future__ import annotations

from ._common import mean

ENGINE_NAME = "ResourceSustainabilityMetrics"

METRICS = (
    "ResourceToConsumptionRatio",
    "CriticalityPoints",
    "RecoveryTime",
)

_CRITICAL_RATIO = 0.2
_RECOVERED_RATIO = 0.8


def _availability(quantity: float, capacity: float | None, consumed: float) -> float:
    """Ratio ressource disponible / consommation (capacité en repli)."""
    consumption = max(consumed, 1e-9)
    if capacity is None:
        return quantity / consumption
    capacity = max(float(capacity), 1e-9)
    return (quantity / capacity) if consumed <= 0 else (quantity / consumption)


def _consumption_by_type(snapshot: dict) -> dict[str, float]:
    """Consommation cumulée par type de ressource (``resource_consumed``)."""
    consumed: dict[str, float] = {}
    for event in snapshot.get("events") or []:
        if event.get("type") != "resource_consumed":
            continue
        value = event.get("value") or {}
        resource_type = str(value.get("type") or "unknown")
        consumed[resource_type] = consumed.get(resource_type, 0.0) + float(
            value.get("amount") or 0.0
        )
    return consumed


def _recovery_time(snapshot: dict) -> float:
    """Temps moyen (ticks) de retour au-dessus de 80 % après un point critique.

    Parcours chronologique de ``history`` (``{"tick", "resources": [...]}``) :
    mesure chaque chute sous 20 % suivie d'un rétablissement ≥ 80 %. 0.0 si
    aucun point critique ne s'est rétabli dans la fenêtre observée.
    """
    history = snapshot.get("history") or []
    if not history:
        return 0.0

    def ratio_of(entry: dict) -> float | None:
        resources = entry.get("resources") or []
        if not resources:
            return None
        ratios = [
            float(resource["quantity"]) / max(float(resource["capacity"]), 1e-9)
            for resource in resources
            if resource.get("capacity")
        ]
        return mean(ratios) if ratios else None

    recoveries: list[float] = []
    crash_tick: int | None = None
    for entry in sorted(history, key=lambda item: int(item.get("tick") or 0)):
        ratio = ratio_of(entry)
        if ratio is None:
            continue
        tick = int(entry.get("tick") or 0)
        if crash_tick is None and ratio < _CRITICAL_RATIO:
            crash_tick = tick
        elif crash_tick is not None and ratio >= _RECOVERED_RATIO:
            recoveries.append(float(tick - crash_tick))
            crash_tick = None

    return mean(recoveries) if recoveries else 0.0


def compute(snapshot: dict) -> dict:
    """Calcule les 3 métriques de durabilité des ressources sur un snapshot."""
    resources = snapshot.get("resources") or []
    consumed = _consumption_by_type(snapshot)

    ratios: list[float] = []
    criticality = 0
    for resource in resources:
        quantity = float(resource.get("quantity") or 0.0)
        capacity = (
            float(resource["capacity"]) if resource.get("capacity") is not None else None
        )
        resource_type = str(resource.get("type") or resource.get("id") or "unknown")
        ratio = _availability(quantity, capacity, consumed.get(resource_type, 0.0))
        ratios.append(ratio)
        if ratio < _CRITICAL_RATIO:
            criticality += 1

    return {
        "ResourceToConsumptionRatio": mean(ratios) if ratios else 0.0,
        "CriticalityPoints": float(criticality),
        "RecoveryTime": _recovery_time(snapshot),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
