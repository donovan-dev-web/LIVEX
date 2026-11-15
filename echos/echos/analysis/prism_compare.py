# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Comparaison de runs PRISM (P10 / ECHOS-123).

Deux runs reproductibles se comparent sur trois axes que le contrat PRISM
 publie explicitement :

- ``contentHash`` — empreinte du pack de contenu compilé : deux runs de packs
  différents ne mesurent pas le même monde, la comparaison doit le dire avant
  toute conclusion ;
- ``seed`` — graine de génération du monde ;
- **premier tick de divergence** — la première enveloppe dont le tick, le
  ``kind`` ou le ``payload`` diffère. C'est la réponse directement exploitable :
  « les runs sont identiques » ou « ils divergent au tick N, sur tel champ ».

La comparaison porte sur le **flux conservé**, pas sur les métriques projetées :
c'est la donnée la plus proche de la source, donc celle qui ne peut pas masquer
une différence derrière une agrégation. Les séquences sont comparées alignées ;
un run plus court est signalé par son enveloppe manquante, jamais par une
divergence de valeur inventée.
"""

from __future__ import annotations

import json
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from echos.storage.sqlite import AnalyticsStore

_DEFAULT_PAGE_SIZE = 500
"""Enveloppes comparées par page (borne mémoire, deux runs en parallèle)."""


class PrismCompareError(ValueError):
    """Comparaison demandée sur un run sans flux PRISM persisté."""


def _canonical(payload: object) -> str:
    """Sérialisation canonique d'un payload (clés triées, sans espace)."""
    return json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False)


def _first_divergence(
    store: "AnalyticsStore",
    run_a: str,
    run_b: str,
    page_size: int = _DEFAULT_PAGE_SIZE,
) -> dict | None:
    """Première enveloppe divergente entre deux flux, ou ``None`` si identiques.

    Le champ rapporté est le premier qui diffère dans l'ordre ``sequence``,
    ``tick``, ``kind``, ``payload`` — un même payload à un tick différent est
    déjà une divergence de tick, et le nommer évite de faire croire à un écart
    de contenu.
    """
    after_sequence = -1
    while True:
        page_a = store.prism_read_envelopes(
            run_a, after_sequence=after_sequence, limit=page_size
        )
        page_b = store.prism_read_envelopes(
            run_b, after_sequence=after_sequence, limit=page_size
        )
        if not page_a and not page_b:
            return None

        common = min(len(page_a), len(page_b))
        for index in range(common):
            envelope_a = page_a[index]
            envelope_b = page_b[index]
            if envelope_a["sequence"] != envelope_b["sequence"]:
                return {
                    "sequence": min(
                        int(envelope_a["sequence"]), int(envelope_b["sequence"])
                    ),
                    "tick": min(int(envelope_a["tick"]), int(envelope_b["tick"])),
                    "field": "sequence",
                    "run_a": envelope_a["sequence"],
                    "run_b": envelope_b["sequence"],
                }
            for field in ("tick", "kind"):
                if envelope_a[field] != envelope_b[field]:
                    return {
                        "sequence": int(envelope_a["sequence"]),
                        "tick": int(envelope_a["tick"]),
                        "field": field,
                        "run_a": envelope_a[field],
                        "run_b": envelope_b[field],
                    }
            if _canonical(envelope_a["payload"]) != _canonical(envelope_b["payload"]):
                return {
                    "sequence": int(envelope_a["sequence"]),
                    "tick": int(envelope_a["tick"]),
                    "field": "payload",
                    "run_a": envelope_a["kind"],
                    "run_b": envelope_b["kind"],
                }

        if len(page_a) != len(page_b):
            # Un flux s'arrête avant l'autre : la divergence est l'enveloppe
            # que le plus court ne possède pas (fin de run atteinte plus tôt).
            longer = page_a if len(page_a) > len(page_b) else page_b
            extra = longer[common]
            returning_a = len(page_a) > len(page_b)
            return {
                "sequence": int(extra["sequence"]),
                "tick": int(extra["tick"]),
                "field": "missing_envelope",
                "run_a": extra["sequence"] if returning_a else None,
                "run_b": None if returning_a else extra["sequence"],
            }

        after_sequence = int(page_a[-1]["sequence"])
        if len(page_a) < page_size:
            return None


def compare_prism_runs(store: "AnalyticsStore", run_a: str, run_b: str) -> dict:
    """Compare deux runs PRISM par ``contentHash``, ``seed`` et premier écart.

    Les deux runs doivent exister **avec** un flux PRISM persisté : comparer un
    run SYNE à un run PRISM n'a pas de sens, et un run PRISM non encore ingéré
    produirait un faux « identique » (deux flux vides).
    """
    identity_a = store.prism_read_identity(run_a)
    identity_b = store.prism_read_identity(run_b)
    for run_id, identity in ((run_a, identity_a), (run_b, identity_b)):
        if identity is None:
            raise PrismCompareError(
                f"aucun flux de lecture PRISM pour le run : {run_id}"
            )

    divergence = _first_divergence(store, run_a, run_b)
    return {
        "run_a": identity_a,
        "run_b": identity_b,
        "same_content_hash": identity_a["contentHash"] == identity_b["contentHash"],
        "same_seed": identity_a["seed"] == identity_b["seed"],
        "diverged": divergence is not None,
        "first_divergence": divergence,
        "envelope_counts": {
            "run_a": identity_a["envelopes"],
            "run_b": identity_b["envelopes"],
        },
        "last_ticks": {
            "run_a": identity_a["lastTick"],
            "run_b": identity_b["lastTick"],
        },
    }


__all__ = ["PrismCompareError", "compare_prism_runs"]
