"""Cache de séries temporelles de l'API REST (ECHOS-044).

Les séries servies par ``/api/runs/{id}/metrics`` sont **mises en cache**
(run, moteur, métrique) et réutilisées tant que le contenu du magasin n'a pas
changé : l'invalidation est pilotée par ``AnalyticsStore.ingest_version``
(incrémentée à chaque écriture du pipeline), sans dépendre du temps ni d'un
recalcul complet. Le cache est **borné** (LRU) — les séries longues ne
s'accumulent jamais en mémoire — et thread-safe (FastAPI gère plusieurs
requêtes de front).
"""

from __future__ import annotations

from collections import OrderedDict
from threading import Lock
from typing import Callable

from echos.storage.sqlite import AnalyticsStore


class SeriesCache:
    """Cache LRU de séries par ``(namespace, run_id, engine, metric)``.

    Deux namespaces : ``value`` (séries de valeurs) et ``measured`` (séries de
    provenance par tick). Borné et thread-safe (FastAPI sert plusieurs requêtes
    de front).
    """

    def __init__(self, capacity: int = 256) -> None:
        if capacity <= 0:
            raise ValueError("capacity doit être > 0")
        self._capacity = capacity
        self._entries: "OrderedDict[tuple[str, str, str, str], tuple[int, object]]" = (
            OrderedDict()
        )
        self._lock = Lock()

    def series(
        self,
        store: AnalyticsStore,
        run_id: str,
        engine: str,
        metric: str,
        *,
        loader: Callable[[], object] | None = None,
    ) -> object:
        """Retourne la série en cache ou la calcule via ``loader`` / le magasin.

        La série est invalidée si ``store.ingest_version`` a changé depuis sa
        mise en cache (écriture d'un nouveau tick, d'un contexte, etc.).
        """
        return self._cached(
            (run_id, engine, metric),
            store,
            loader or (lambda: store.metric_series(run_id, engine, metric)),
        )

    def provenance(
        self,
        store: AnalyticsStore,
        run_id: str,
        engine: str,
        metric: str,
    ) -> object:
        """Série de provenance ``(tick, measured)`` en cache (P0).

        Même politique d'invalidation que :meth:`series` ; l'entrée est disjointe
        (préfixe de clé) pour ne jamais confondre valeurs et drapeaux.
        """
        return self._cached(
            (run_id, engine, metric),
            store,
            lambda: store.measured_series(run_id, engine, metric),
            namespace="measured",
        )

    def _cached(
        self,
        key: tuple[str, str, str],
        store: AnalyticsStore,
        loader: Callable[[], object],
        namespace: str = "value",
    ) -> object:
        cache_key = (namespace, *key)
        version = store.ingest_version
        with self._lock:
            entry = self._entries.pop(cache_key, None)
            if entry is not None and entry[0] == version:
                self._entries[cache_key] = entry
                return entry[1]

        value = loader()

        with self._lock:
            self._entries[cache_key] = (version, value)
            self._entries.move_to_end(cache_key)
            while len(self._entries) > self._capacity:
                self._entries.popitem(last=False)
        return value

    def __len__(self) -> int:
        with self._lock:
            return len(self._entries)
