"""Cache LRU des analyses causales de l'API REST (ECHOS-063).

Mirroir du ``SeriesCache`` (ECHOS-044) : les chaînes causales reconstruites
(``causal.build_chain``) sont mises en cache par ``(run_id, agent_id, tick,
depth)`` et réutilisées tant que ``AnalyticsStore.ingest_version`` n'a pas
changé — après un re-run (même run ré-analysé) les entrées sont invalidées et
recalculées : la réponse reste **déterministe** (déterminisme ECHOS-041/045).
Le cache est borné (LRU) et thread-safe (FastAPI sert plusieurs requêtes).
"""

from __future__ import annotations

from collections import OrderedDict
from threading import Lock
from typing import Callable

from echos.storage.sqlite import AnalyticsStore


class CausalCache:
    """Cache LRU de chaînes causales, borné et thread-safe."""

    def __init__(self, capacity: int = 256) -> None:
        if capacity <= 0:
            raise ValueError("capacity doit être > 0")
        self._capacity = capacity
        self._entries: "OrderedDict[tuple[str, str, int, int], tuple[int, object]]" = (
            OrderedDict()
        )
        self._lock = Lock()

    def chain(
        self,
        store: AnalyticsStore,
        run_id: str,
        agent_id: str,
        tick: int,
        depth: int,
        *,
        loader: Callable[[], object],
    ) -> object:
        """Retourne la chaîne en cache ou la reconstruit via ``loader``.

        Invalidée si ``store.ingest_version`` a changé depuis la mise en cache
        (nouvelle écriture du pipeline ⇒ re-analyse déterministe, ECHOS-063).
        """
        key = (run_id, str(agent_id), int(tick), int(depth))
        version = store.ingest_version
        with self._lock:
            entry = self._entries.pop(key, None)
            if entry is not None and entry[0] == version:
                self._entries[key] = entry
                return entry[1]

        value = loader()

        with self._lock:
            self._entries[key] = (version, value)
            self._entries.move_to_end(key)
            while len(self._entries) > self._capacity:
                self._entries.popitem(last=False)
        return value

    def __len__(self) -> int:
        with self._lock:
            return len(self._entries)


__all__ = ["CausalCache"]
