"""Moteur 2 — InformationPropagationMetrics (propagation de l'information).

Mesure la circulation **observée** de l'information (METRICS_SPEC.md §3) à
partir des événements ``message_sent`` (ADR-004) portés par le snapshot (clé
``events``, fenêtre glissante alimentée par le pipeline) : volume, délai de
couverture des émetteurs, dégradation théorique par saut, chaîne max et
concentration des émetteurs.

Frontières (RAPPORT d'analyse §3.2) :

- ``EmitterCoverageDelay`` (ex-``InformationDiffusionSpeed``) observe le délai
  de couverture des **émetteurs**, jamais la réception : ``SenderCoverage``
  l'accompagne pour que la couverture soit lisible (« 18/24 émetteurs »). Aucune
  de ces métriques ne prouve qu'une information ait été reçue ;
- ``TheoreticalHopDecay`` n'est **pas** une mesure de précision : c'est une
  transformation déterministe du nombre de sauts sous l'hypothèse de 10 % de
  perte par saut, publiée comme telle ;
- ``SenderConcentration`` remplace l'ex-``NetworkCentrality``, qui comptait
  les émetteurs distincts par message — l'inverse d'une concentration — et
  inversait donc le sens du phénomène « goulot d'information ».

Champs absents → repli neutre 0.0 (``measured = false``).
"""

from __future__ import annotations

from collections import Counter

from ._common import (
    alive_count,
    event_values,
    event_window_published,
    events_of,
    mean,
    safe_ratio,
)

ENGINE_NAME = "InformationPropagationMetrics"

METRICS = (
    "MessageVolume",
    "EmitterCoverageDelay",
    "TheoreticalHopDecay",
    "MaxMessageHops",
    "SenderConcentration",
    "SenderCoverage",
)

REQUIRES = {
    "EmitterCoverageDelay": event_window_published,
    "TheoreticalHopDecay": event_window_published,
    "MaxMessageHops": event_window_published,
    "SenderConcentration": event_window_published,
    "SenderCoverage": event_window_published,
}
"""Métriques calculées sur la fenêtre d'événements.

``MessageVolume`` est exclue : elle compte le tick courant et reste mesurée même
quand aucun message n'est parti (0.0 y est une mesure réelle, pas un repli).
Les cinq autres sont mesurées **si et seulement si** une fenêtre d'événements a
été publiée : sans fenêtre, 0.0 était indiscernable d'un réseau réellement
inactif ; avec une fenêtre vide, 0.0 est justement « rien observé ».
"""

_DECAY_PER_HOP = 0.9
"""Hypothèse **non calibrée** de perte de fiabilité par saut (10 %)."""

_DIFFUSION_COVERAGE = 0.8
"""Part de la population qui doit avoir émis pour que la couverture soit acquise."""


def _messages(snapshot: dict) -> list[dict]:
    return events_of(snapshot, event_type="message_sent")


def _diffusion_speed(messages: list[dict], count: int) -> float:
    """Délai (ticks) de couverture des émetteurs : 80 % des entités ont émis.

    Mesure l'**amplitude** entre le premier message observé et le tick où le
    seuil de couverture est atteint, pas le numéro de tick absolu. La version
    précédente renvoyait le tick lui-même, ce qui :
    - croissait linéairement avec la longueur du run, donc
      ``CoverageDelay_Norm = 1 - délai/100`` tombait à 0 après le tick 100
      quel que soit le comportement réel du réseau, et
    - rendait ``SystemComplexity`` non borné, cet indicateur ne l'étant pas.

    **Portée** : émissions observées uniquement — ni réception, ni contenu,
    ni compréhension. Fenêtre sans assez de messages ou population nulle →
    0.0, marqué non mesuré par ``REQUIRES`` ; une couverture partielle est
    signalée par ``SenderCoverage``.
    """
    if not messages or count <= 0:
        return 0.0
    by_tick: dict[int, set[str]] = {}
    for message in messages:
        by_tick.setdefault(int(message.get("tick") or 0), set()).add(
            str(message.get("agentId"))
        )
    target = _DIFFUSION_COVERAGE * count
    seen: set[str] = set()
    for tick in sorted(by_tick):
        seen |= by_tick[tick]
        if len(seen) >= target:
            return float(tick - min(by_tick))
    return 0.0


def compute(snapshot: dict) -> dict:
    """Calcule les 5 métriques de propagation de l'information."""
    count = alive_count(snapshot)
    messages = _messages(snapshot)

    current_tick = int(snapshot.get("tick") or 0)
    volume = sum(
        1 for message in messages if int(message.get("tick") or 0) == current_tick
    )

    hops = [
        float(event_values(message)["hops"])
        for message in messages
        if "hops" in event_values(message)
    ]
    degradation = (
        mean([1.0 - (_DECAY_PER_HOP ** hop) for hop in hops]) if hops else 0.0
    )

    senders = Counter(str(message.get("agentId")) for message in messages)

    # Concentration : part du message le plus productif dans le volume total.
    # 1/k si k émetteurs produisent également (aucune concentration), → 1.0 si
    # un seul émetteur concentre tout le volume. La définition est celle de la
    # spec (« concentration des hubs »), et non le ratio inverse d'émetteurs
    # distincts qui l'était jusqu'ici.
    top_share = safe_ratio(float(max(senders.values(), default=0)), len(messages))

    return {
        "MessageVolume": safe_ratio(float(volume), count),
        "EmitterCoverageDelay": _diffusion_speed(messages, count),
        # Transformation théorique sous hypothèse 10 %/saut — jamais une
        # précision observée (aucun contenu vérifié à la réception).
        "TheoreticalHopDecay": degradation,
        "MaxMessageHops": max(hops, default=0.0),
        "SenderConcentration": top_share,
        # Couverture des émetteurs : entités vivantes qui ont émis au moins un
        # message dans la fenêtre. 18/24 = 0.75. Ne prouve aucune réception.
        "SenderCoverage": safe_ratio(len(senders), count),
    }


__all__ = ["ENGINE_NAME", "METRICS", "compute"]
