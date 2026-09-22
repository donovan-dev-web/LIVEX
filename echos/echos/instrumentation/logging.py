"""Journalisation structurée ECHOS (ECHOS-050, LOGGING_INSTRUMENTATION.md).

Trois niveaux alignés sur la spécification :

1. **Structuré** — métriques par tick en JSON Lines (``structured-<run>.jsonl``),
   sérialisation déterministe (clés triées, compacte) — niveau « événements
   structurés » ;
2. **Traces** — décisions SYNE consommées en JSON Lines
   (``decision-traces-<run>.jsonl``) — niveau « traces de décision » ;
3. **Texte** — logs lisibles avec la ``logging`` standard (tag ``SSE-V2``,
   format ``{asctime} [{level}] {message}``).

Le répertoire de logs provient de ``ECHOS_LOG_DIR`` (défaut ``logs``).
Aucune dépendance : standard ``json`` + ``logging``.
"""

from __future__ import annotations

import json
import logging
import os
from datetime import datetime
from pathlib import Path
from typing import Any

_APP_TAG = "SSE-V2"


def _append_jsonl(path: Path, payload: dict[str, Any]) -> None:
    """Écrit une ligne JSON déterministe (clés triées — export reproductible)."""
    with path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(payload, sort_keys=True, separators=(",", ":")))
        handle.write("\n")


class EchosLogger:
    """Bâche les trois niveaux de journalisation dans ``log_dir``."""

    def __init__(self, log_dir: str | Path = "logs") -> None:
        self._dir = Path(log_dir)
        self._dir.mkdir(parents=True, exist_ok=True)
        self._text = logging.getLogger(f"{_APP_TAG}.{id(self)}")
        handler = logging.FileHandler(
            self._dir / f"echos-{datetime.now():%Y-%m-%d}.log",
            encoding="utf-8",
        )
        handler.setFormatter(logging.Formatter("%(asctime)s [%(levelname)s] %(message)s"))
        self._text.addHandler(handler)
        self._text.setLevel(logging.DEBUG)
        self._text.propagate = False

    @classmethod
    def from_env(cls) -> "EchosLogger":
        """Logger configuré par ``ECHOS_LOG_DIR`` (défaut ``logs``)."""
        return cls(os.environ.get("ECHOS_LOG_DIR", "logs"))

    @property
    def log_dir(self) -> Path:
        return self._dir

    def structured(self, run_id: str, tick: int, metrics: dict[str, dict]) -> None:
        """Niveau 1 — métriques du tick en JSON Lines (événements structurés)."""
        _append_jsonl(
            self._dir / f"structured-{run_id}.jsonl",
            {
                "event": "tick_metrics",
                "run_id": run_id,
                "tick": int(tick),
                "metrics": metrics,
            },
        )

    def profiling(self, run_id: str, tick: int, profile: dict[str, dict]) -> None:
        """Niveau 1 — coût par moteur du tick (ECHOS-052, §5)."""
        _append_jsonl(
            self._dir / f"profilage-{run_id}.jsonl",
            {
                "event": "engine_profiling",
                "run_id": run_id,
                "tick": int(tick),
                "engines": profile,
            },
        )

    def decision(self, trace: dict) -> None:
        """Niveau 2 — trace de décision d'une entité (analyse causale)."""
        _append_jsonl(self._dir / f"decision-traces-{trace['run_id']}.jsonl", trace)

    def debug(self, message: str) -> None:
        self._text.debug(f"[{_APP_TAG}] {message}")

    def info(self, message: str) -> None:
        self._text.info(f"[{_APP_TAG}] {message}")
