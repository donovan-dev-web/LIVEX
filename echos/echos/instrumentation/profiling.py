"""Profilage des moteurs ECHOS (ECHOS-052, LOGGING_INSTRUMENTATION.md §5).

``ProfileMarkers`` mesure les temps d'exécution de chaque moteur
(équivalent Python des *ProfileMarkers* basés sur ``Stopwatch`` de la
spécification) ; ``compute_all_profiled`` exécute les 8 moteurs en traçant
le coût par moteur **sans changer le résultat** (déterminisme de sortie
inchangé) — la sortie texte suit le format §5 :

    Perception    : 240.51 ms total,   0.48 ms avg

Les budgets chiffrés restent des cibles de calibration (ECHOS, ISSUES.md
§points restés ouverts) ; le présent module pose la mesure + un seuil CI
de garde-fou (test ``test_engine_profiling``).
"""

from __future__ import annotations

import time
from contextlib import contextmanager
from typing import Iterator

from echos.analysis import compute_all

_PROFILED_ENGINES = 8


class ProfileMarkers:
    """Marqueurs de chronométrage par moteur (total, nombre d'appels)."""

    def __init__(self) -> None:
        self._total_ms: dict[str, float] = {}
        self._calls: dict[str, int] = {}

    @contextmanager
    def measure(self, name: str) -> Iterator[None]:
        """Chronomètre le bloc : ajout ``elapsed_ms`` au total de ``name``."""
        started = time.perf_counter()
        try:
            yield
        finally:
            elapsed_ms = (time.perf_counter() - started) * 1000.0
            self._total_ms[name] = self._total_ms.get(name, 0.0) + elapsed_ms
            self._calls[name] = self._calls.get(name, 0) + 1

    def summary(self) -> dict[str, dict[str, float | int]]:
        """Résumé trié par moteur : ``{name: {calls, totalMs, avgMs}}``."""
        result: dict[str, dict[str, float | int]] = {}
        for name in sorted(self._calls):
            total = self._total_ms[name]
            calls = self._calls[name]
            result[name] = {
                "calls": calls,
                "totalMs": round(total, 3),
                "avgMs": round(total / calls, 3) if calls else 0.0,
            }
        return result

    def report(self) -> list[tuple[str, float, float]]:
        """Lignes ``(nom, total_ms, avg_ms)`` triées par moteur (format §5)."""
        return [
            (name, float(stats["totalMs"]), float(stats["avgMs"]))
            for name, stats in self.summary().items()
        ]

    def formatted(self) -> list[str]:
        """Sortie texte alignée, format LOGGING_INSTRUMENTATION.md §5."""
        return [
            f"{name:<15} : {total:10.2f} ms total, {avg:8.2f} ms avg"
            for name, total, avg in self.report()
        ]


def compute_all_profiled(snapshot: dict) -> tuple[dict[str, dict], dict[str, dict]]:
    """Exécute les 8 moteurs et retourne ``(résultats, profil par moteur)``.

    Les résultats sont **bit-à-bit identiques** à ``compute_all`` — les
    mesures n'influencent jamais le calcul (déterminisme des moteurs,
    ECHOS-027/J2).
    """
    markers = ProfileMarkers()
    results = compute_all(snapshot, profile=markers)
    return results, markers.summary()


__all__ = ["ProfileMarkers", "compute_all_profiled", "compute_all"]

# Référence : le registre compte 8 moteurs (7 métriques + EmergenceIndicators).
# Le module expose ``_PROFILED_ENGINES`` pour la CI (garde-fou : la liste
# des moteurs profilés doit rester couverte).
