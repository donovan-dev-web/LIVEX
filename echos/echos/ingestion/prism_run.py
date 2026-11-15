# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Ingestion **de production** du flux de lecture PRISM (P10 / ECHOS-120).

Le socle v8 (:mod:`echos.ingestion.prism_stream`) validait un flux *fini* et le
persistait d'un bloc : suffisant pour un test, inutilisable pour un run réel.
Un run PRISM dure des milliers de ticks, produit ses enveloppes au fil de
l'exécution et peut être interrompu (fermeture du host, redémarrage du
consommateur). Ce module apporte les trois propriétés qui manquaient :

- **incrémental** — les enveloppes sont écrites par lots bornés
  (``batch_size``), sans attendre la fin du flux ; la mémoire du consommateur
  ne dépend donc pas de la durée du run ;
- **reprenable** — la continuité de séquence est validée contre ce qui est
  **déjà persisté** (et non contre le lot courant) : une reprise après coupure
  peut rejouer le flux depuis son début ;
- **idempotent** — les séquences déjà stockées sont ignorées, donc rejouer une
  fenêtre chevauchante n'écrit ni doublon ni erreur.

``ingest_prism_run`` est le point d'entrée du cycle de run : il branche cet
ingéreur sur une connexion ``/data/read`` réelle, sans dépendre du Launcher
(exigence explicite de ECHOS-120). L'analyse des enveloppes persistées est un
autre lot (:mod:`echos.storage.prism_pipeline`) : l'ingestion reste purement
« observer et conserver ».
"""

from __future__ import annotations

from collections.abc import Iterable
from typing import TYPE_CHECKING

from echos.ingestion.models import InvalidMessageError
from echos.ingestion.prism_stream import PrismReadEnvelope, prism_envelopes
from echos.ingestion.ws_client import WsClient

if TYPE_CHECKING:
    from echos.storage.sqlite import AnalyticsStore

DEFAULT_BATCH_SIZE = 200
"""Nombre d'enveloppes tamponnées avant une écriture (borne mémoire du lot).

Une écriture par enveloppe coûterait une transaction par message (≈ 1 par tick
et par type) ; un lot de 200 couvre une centaine de ticks de flux PRISM
nominal, ce qui amortit la transaction sans retarder durablement la
persistance : une coupure perd au plus un lot.
"""


class PrismIngestError(ValueError):
    """Ingestion PRISM refusée (identité, continuité ou propriété du run)."""


class PrismRunIngestor:
    """Écrit un flux PRISM en lots, de façon reprenable et idempotente.

    Cycle d'usage :

    .. code-block:: python

        ingestor = PrismRunIngestor(store, "prism-run-1", seed=42, content_hash="abc")
        for envelope in envelopes:      # flux live ou rejeu
            ingestor.ingest([envelope])  # écrit tous les ``batch_size`` reçus
        ingestor.flush()                 # dernier lot partiel
        ingestor.summary()               # état persisté, relu du magasin

    ``ingested`` compte les enveloppes **écrites** par cette instance (les
    séquences déjà présentes en base, ignorées à la reprise, ne sont donc pas
    comptées) ; ``summary`` reflète l'état complet du run en base.
    """

    def __init__(
        self,
        store: "AnalyticsStore",
        run_id: str,
        seed: int,
        content_hash: str,
        batch_size: int = DEFAULT_BATCH_SIZE,
    ) -> None:
        if not str(run_id).strip():
            raise PrismIngestError("run_id must not be empty")
        if not str(content_hash).strip():
            raise PrismIngestError("content_hash must not be empty")
        if seed < 0:
            raise PrismIngestError("seed must be non-negative")
        if batch_size <= 0:
            raise PrismIngestError("batch_size must be positive")
        self._store = store
        self._run_id = str(run_id)
        self._seed = int(seed)
        self._content_hash = str(content_hash)
        self._batch_size = int(batch_size)
        self._buffer: list[PrismReadEnvelope] = []
        self.ingested = 0

    @property
    def run_id(self) -> str:
        return self._run_id

    @property
    def pending(self) -> int:
        """Enveloppes tamponnées qui ne sont pas encore persistées."""
        return len(self._buffer)

    def ingest(self, envelopes: Iterable[PrismReadEnvelope]) -> int:
        """Tamponne des enveloppes et écrit chaque lot complet.

        Retourne le nombre cumulé d'enveloppes écrites par cette instance.
        Lève :class:`InvalidMessageError` (contrat) ou :class:`PrismIngestError`
        (identité du run) — dans les deux cas le lot fautif n'est pas écrit.
        """
        for envelope in envelopes:
            self._buffer.append(envelope)
            if len(self._buffer) >= self._batch_size:
                self.flush()
        return self.ingested

    def flush(self) -> int:
        """Écrit le lot courant ; retourne le nombre d'enveloppes écrites.

        Une exception laisse le tampon intact : l'appelant peut réessayer le
        même lot (les séquences déjà persistées seront ignorées), plutôt que de
        perdre silencieusement les enveloppes d'un lot partiellement écrit.
        """
        if not self._buffer:
            return 0
        rows = [envelope.to_dict() for envelope in self._buffer]
        written = self._store.append_prism_read_envelopes(
            self._run_id,
            str(self._seed),
            self._content_hash,
            rows,
        )
        self._buffer.clear()
        self.ingested += written
        return written

    def summary(self) -> dict:
        """État persisté du run (relu de la base, donc partagé entre reprises)."""
        summary = self._store.prism_read_summary(self._run_id)
        if summary is None:
            return {
                "runId": self._run_id,
                "contentHash": self._content_hash,
                "envelopes": 0,
                "lastSequence": -1,
                "lastTick": -1,
                "kinds": {},
                "ingested": self.ingested,
                "pending": len(self._buffer),
            }
        return {
            **summary,
            "ingested": self.ingested,
            "pending": len(self._buffer),
        }


def ingest_prism_run(
    store: "AnalyticsStore",
    client: WsClient,
    run_id: str,
    seed: int,
    content_hash: str,
    *,
    batch_size: int = DEFAULT_BATCH_SIZE,
    max_envelopes: int | None = None,
) -> dict:
    """Consomme un flux PRISM live et le persiste (cycle de run, ECHOS-120).

    ``client`` doit être **connecté** (l'appelant gère la connexion et les
    tentatives : c'est son rôle de décider quand le host est prêt). Le flux est
    validé enveloppe par enveloppe ; la fermeture du transport est une fin
    normale, traitée comme la fin du run.

    ``max_envelopes`` borne la consommation (harnais et tests) : la lecture
    s'arrête après ce nombre d'enveloppes **reçues**, sans laisser une connexion
    ouverte en arrière-plan.

    Retourne le résumé persisté du run, enrichi de ``resumed`` : vrai si des
    enveloppes étaient déjà en base avant cet appel (reprise après coupure).
    """
    if max_envelopes is not None and max_envelopes <= 0:
        raise PrismIngestError("max_envelopes must be positive")
    ingestor = PrismRunIngestor(
        store,
        run_id,
        seed,
        content_hash,
        batch_size=batch_size,
    )
    resumed = store.prism_read_summary(ingestor.run_id) is not None
    received = 0
    try:
        for envelope in prism_envelopes(client):
            received += 1
            ingestor.ingest([envelope])
            if max_envelopes is not None and received >= max_envelopes:
                break
        ingestor.flush()
    except InvalidMessageError:
        # Le lot valide déjà reçu reste persisté : un message non conforme
        # après N ticks ne doit pas annuler N ticks observés.
        ingestor.flush()
        raise
    summary = ingestor.summary()
    return {**summary, "received": received, "resumed": resumed}


__all__ = [
    "DEFAULT_BATCH_SIZE",
    "PrismIngestError",
    "PrismRunIngestor",
    "ingest_prism_run",
]
