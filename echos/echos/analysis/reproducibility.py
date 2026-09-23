"""Méta-métriques de reproductibilité entre runs (ECHOS-070→072, jalon ph7).

Comparaison de runs contrôlés (EXPERIMENT_COMPARISON.md §2, METRICS_SPEC.md
§10) :

- ``IsReproducible`` : même **seed** ET même **version** du moteur (SYNE) ET
  contenu observé **bit-à-bit identique** (empreinte SHA-256 canonique) ;
- ``ReproducibilityScore`` : ``1.0`` si reproductible, sinon
  ``1.0 - (CognitiveDiff + SocialDiff) / 2`` ;
- ``CognitiveDiff`` : distance L2 **normalisée** entre les distributions de
  croyances de la population (dernier contexte ``agents`` de chaque run) ;
- ``SocialDiff`` : distance L2 normalisée entre les réseaux de confiance.

Les fonctions pures (distributions, distance, score) ne dépendent que de
données explicites : mêmes entrées → mêmes sorties, ordres stables par tri.
Les helpers liés au ``AnalyticsStore`` (empreinte de contenu, comparaison,
séries comparatives alignées) restent déterministes : aucune dépendance
temporelle, tris (tick, engine, metric) — prérequis de la **stabilité des
méta-métriques entre runs** (ECHOS-071, rejeux J2/J3).
"""

from __future__ import annotations

import hashlib
import json

from echos.storage.sqlite import AnalyticsStore

_DIFF_NORMALIZER = 2.0  # deux distributions → distance L2 maximale = √2


def belief_distribution(agents: list[dict]) -> dict[str, float]:
    """Distribution (somme = 1) des croyances de la population, clés triées.

    Chaque croyance (``subject|predicate|value``) compte pour un ; le total
    est normalisé à 1 afin que les distributions soient comparables entre
    populations de tailles différentes. Dict vide sur population sans
    croyances (diff nulle).
    """
    counts: dict[str, int] = {}
    for agent in agents:
        for belief in agent.get("beliefs") or []:
            key = "|".join(
                (
                    str(belief.get("subject") or ""),
                    str(belief.get("predicate") or ""),
                    str(belief.get("value") or ""),
                )
            )
            counts[key] = counts.get(key, 0) + 1
    total = sum(counts.values())
    if total <= 0:
        return {}
    return {key: count / total for key, count in sorted(counts.items())}


def social_distribution(agents: list[dict]) -> dict[str, float]:
    """Distribution (somme = 1) des poids de confiance entre paires.

    Poids d'une paire = moyenne des reliances des deux directions (ou de la
    direction unique) ; les arêtes de confiance positive seulement. Clés
    ``min|max`` triées — réseau non orienté déterministe.
    """
    pair_weights: dict[str, list[float]] = {}
    for agent in agents:
        agent_id = str(agent.get("id"))
        for relation in agent.get("trust") or []:
            weight = float(relation.get("trust") or 0.0)
            peer = str(relation.get("peerId") or "")
            if weight <= 0.0 or not peer:
                continue
            key = "|".join(sorted((agent_id, peer)))
            pair_weights.setdefault(key, []).append(weight)
    if not pair_weights:
        return {}
    means = {
        key: sum(weights) / len(weights) for key, weights in pair_weights.items()
    }
    total = sum(means.values())
    if total <= 0:
        return {}
    return {key: mean / total for key, mean in sorted(means.items())}


def l2_normalized(distribution_a: dict[str, float], distribution_b: dict[str, float]) -> float:
    """Distance L2 normalisée entre deux distributions (bornée [0, 1]).

    Union des clés (les clés manquantes valent 0.0) ; deux distributions
    opposées atteignent ``√2`` → normalisation par ``√2``.
    """
    keys = sorted(set(distribution_a) | set(distribution_b))
    if not keys:
        return 0.0
    squared = sum(
        (distribution_a.get(key, 0.0) - distribution_b.get(key, 0.0)) ** 2
        for key in keys
    )
    return min(1.0, (squared / _DIFF_NORMALIZER) ** 0.5)


def _canonical_content(store: AnalyticsStore, run_id: str) -> dict:
    """Vue canonique du contenu d'un run (sans l'identité ``run_id``).

    Deux runs du même protocole ne diffèrent que par leur étiquette ``run_id``
    : celle-ci est donc exclue afin que l'empreinte reflète le **contenu**
    observé (séries de métriques, résumés de tick, événements, contextes,
    traces de décision) — la reproduction bit-à-bit se compare sur cette vue.
    """
    return {
        "metrics": [
            {"tick": tick, "engine": engine, "metric": metric, "value": value}
            for tick, engine, metric, value in store.metrics_all(run_id)
        ],
        "summaries": [
            {
                "tick": row[1],
                "simulated_time_minutes": row[2],
                "alive_count": row[3],
                "agent_count": row[4],
                "mean_energy": row[5],
                "mean_hunger": row[6],
                "mean_thirst": row[7],
                "mean_fatigue": row[8],
                "decision_count": row[9],
            }
            for row in store.tick_summaries(run_id)
        ],
        "events": [
            {
                "tick": tick,
                "type": event_type,
                "agent_id": agent_id,
                "target_id": target_id,
                "action": action,
                "cause": cause,
                "value": value,
            }
            for tick, event_type, agent_id, target_id, action, cause, value in store.events(
                run_id
            )
        ],
        "contexts": {
            context_type: [
                {"tick": tick, "payload": payload}
                for tick, payload in store.observations_for(run_id, context_type)
            ]
            for context_type in ("agents", "groups", "phenomena")
        },
        "decisions": store.decision_traces(run_id),
    }


def content_fingerprint(store: AnalyticsStore, run_id: str) -> str:
    """Empreinte SHA-256 (hex) du contenu d'un run — base du bit-à-bit."""
    blob = json.dumps(
        _canonical_content(store, run_id),
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(blob).hexdigest()


def compare(store: AnalyticsStore, run_a: str, run_b: str) -> dict:
    """Méta-métriques de reproductibilité entre deux runs.

    Les runs doivent exister (vérifiés côté route) ; l'ordre des deux
    arguments n'influe pas sur ``is_reproducible`` ni sur les distances.
    """
    meta = {run["run_id"]: run for run in store.runs()}
    agents_a = store.latest_context(run_a, "agents")
    agents_b = store.latest_context(run_b, "agents")
    cognitive_diff = l2_normalized(
        belief_distribution(agents_a[1] if agents_a else []),
        belief_distribution(agents_b[1] if agents_b else []),
    )
    social_diff = l2_normalized(
        social_distribution(agents_a[1] if agents_a else []),
        social_distribution(agents_b[1] if agents_b else []),
    )
    same_seed = meta[run_a]["seed"] == meta[run_b]["seed"]
    same_version = meta[run_a]["version"] == meta[run_b]["version"]
    identical = content_fingerprint(store, run_a) == content_fingerprint(store, run_b)
    is_reproducible = same_seed and same_version and identical
    return {
        "run_a": meta[run_a],
        "run_b": meta[run_b],
        "same_seed": same_seed,
        "same_version": same_version,
        "bit_identical": identical,
        "is_reproducible": is_reproducible,
        "reproducibility_score": (
            1.0
            if is_reproducible
            else round(1.0 - (cognitive_diff + social_diff) / _DIFF_NORMALIZER, 6)
        ),
        "cognitive_diff": round(cognitive_diff, 6),
        "social_diff": round(social_diff, 6),
    }


def aligned_series(store: AnalyticsStore, run_a: str, run_b: str) -> list[dict]:
    """Séries de métriques comparatives alignées (ECHOS-072).

    Jointure sur les ticks/métriques communs : ``{tick, engine, metric,
    run_a, run_b, diff}``, tri (tick, engine, metric), diff = valeur_B −
    valeur_A. La série de référence est celle du run B (les valeurs se liront
    « le run A est en avance/retard de diff sur le run B »).
    """
    index_b = {(tick, engine, metric): value
               for tick, engine, metric, value in store.metrics_all(run_b)}
    seen: set[tuple[int, str, str]] = set()
    out: list[dict] = []
    for tick, engine, metric, value in store.metrics_all(run_a):
        key = (tick, engine, metric)
        if key in seen or key not in index_b:
            continue
        seen.add(key)
        other = index_b[key]
        out.append(
            {
                "tick": tick,
                "engine": engine,
                "metric": metric,
                "run_a": value,
                "run_b": other,
                "diff": round(other - value, 6),
            }
        )
    return sorted(out, key=lambda row: (row["tick"], row["engine"], row["metric"]))
