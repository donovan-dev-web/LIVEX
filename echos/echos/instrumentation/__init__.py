"""Instrumentation ECHOS (jalon SYNE ph5 — Logging & instrumentation).

- ``logging`` : journalisation structurée JSON Lines (3 niveaux de
  ``LOGGING_INSTRUMENTATION.md`` : structuré / traces / texte) — ECHOS-050 ;
- ``decision_traces`` : traces de décisions SYNE consommées (schéma
  ``decision_traces``) — ECHOS-051 ;
- ``profiling`` : ``ProfileMarkers`` + ``compute_all_profiled`` (coût par
  moteur, ``LOGGING_INSTRUMENTATION.md`` §5) — ECHOS-052.
"""

from echos.instrumentation.decision_traces import build_decision_trace
from echos.instrumentation.logging import EchosLogger
from echos.instrumentation.profiling import ProfileMarkers, compute_all_profiled

__all__ = [
    "EchosLogger",
    "ProfileMarkers",
    "build_decision_trace",
    "compute_all_profiled",
]
