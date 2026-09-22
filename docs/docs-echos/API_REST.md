# API_REST.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `../docs-syne/API_CONTRACTS.md`
**Source Monographie** : §4.7

---

## 1. Positionnement

API REST **locale** d'ECHOS (FastAPI en V0.1 — voir `ARCHITECTURE.md`), port **5000**. Elle sert l'interface ECHOS (intégrée) et expose les données d'analyse. Elle est distincte du contract de contrôle de SYNE (HTTP :5181) et de transport de SYNE (WS :5180). L'API est **lecture seule** : aucune écriture dans le monde observé (règle d'or §4.10.3, ECHOS-042).

### 1.1 Configuration

- La base d'analyse est fournie à `create_app(store)` ; à défaut, la variable d'environnement **`ECHOS_ANALYTICS_DB`** (chemin du fichier SQLite) est lue.
- Sans base configurée, les routes de données répondent **503** (le contrat reste publié dans l'OpenAPI) — `/health` et `/` restent disponibles.

## 2. Les endpoints principaux

| Méthode | Endpoint | Description |
| :-- | :-- | :-- |
| GET | `/health` | Vérification de santé du service |
| GET | `/api/runs` | Liste des runs enregistrés |
| GET | `/api/runs/{id}` | Métriques complètes du run (dernier tick) + phénomènes |
| GET | `/api/runs/{id}/metrics` | Séries de métriques (JSON, `?engine=`, `?metric=`, `?every=N`) |
| GET | `/api/runs/{id}/export` | Export des métriques (`?format=json\|csv`), reproductible |
| GET | `/api/runs/{id}/decisions` | Traces de décision des entités (analyse causale) |
| GET | `/api/beliefs/{agentId}` | Croyances de l'entité au tick le plus récent |
| GET | `/api/relationships/{agentId}` | Réseau de confiance de l'entité |
| GET | `/api/groups` | Liste des groupes actifs (communautés) |
| GET | `/api/emergent-phenomena` | Phénomènes émergents détectés + disclaimer |

(Source : Monographie §4.7.1 — `/api/compare` et `/api/communication-heatmap` restent à venir, jalons ECHOS ph7 / hors V0.1.)

## 3. Contrat des réponses (V0.1, jalons ECHOS ph4 → ph5)

Ordres **déterministes** (aucun PRNG, aucun horodatage d'émission) : runs triés par `run_id`, séries triées par tick, lignes d'export triées `(tick, engine, metric)`.

### 3.1 `GET /api/runs`

```json
{"runs": [{"run_id": "run-7", "version": "0.1.0", "seed": "7",
           "ticks_count": 1200, "first_tick": 1, "last_tick": 1200}]}
```

Sans paramètre `run_id` sur les endpoints suivants, le **run par défaut** est le plus récent (`last_tick` maximal, départage `run_id`).

### 3.2 `GET /api/runs/{id}`

Métadonnées + **dernières métriques** de tous les moteurs (`{engine: {metric: value}}`) + contexte `phenomena` (`detected` + `disclaimer`).

### 3.3 `GET /api/runs/{id}/metrics`

```json
{"run_id": "run-7", "engine": null, "metric": null, "every": 1,
 "ticks": [1, 2, 3],
 "values": {"EmergenceIndicators": {"EmergenceScore": [0.5, 0.52, 0.51]}},
 "latest": {"EmergenceIndicators": {"EmergenceScore": 0.51}}}
```

- `ticks` : union des ticks des séries demandées, **sous-échantillonnés** `ticks[::every]` (`?every=N`, `N ≥ 1` — `N = 0` → 422) ; `values` alignées sur `ticks`.
- `every = 1` (défaut) : série complète ; `?engine=` / `?metric=` restreignent `values`.
- Les séries sont servies par **cache** validé sur `AnalyticsStore.ingest_version` (invalidation à la première écriture post-cache).

### 3.4 `GET /api/runs/{id}/export?format=json|csv`

- `format=json` : `{"run_id", "format": "json", "rows": [{"tick", "engine", "metric", "value"}]}` triés `(tick, engine, metric)`.
- `format=csv` : **RFC 4180** (CRLF), entête `run_id,tick,engine,metric,value`, mêmes lignes.
- **Reproductible** : deux appels à un run inchangé → corps identiques (aucune dépendance temporelle). `format` inconnu → 400.

### 3.5 `GET /api/beliefs/{agentId}` et `GET /api/relationships/{agentId}`

```json
{"agent_id": "A", "run_id": "run-7", "tick": 1200,
 "beliefs": [{"subject": "water", "predicate": "safe", "value": "true", "confidence": 0.9}],
 "// ou →": "trust": [{"peerId": "B", "trust": 0.7}], "count": 1}
```

Croyances / relations de confiance de l'entité **au tick le plus récent** (contexte `agents` du pipeline). Entité absente → 404. Lecture seule.

### 3.6 `GET /api/groups` et `GET /api/emergent-phenomena`

```json
{"run_id": "run-7", "tick": 1200,
 "groups": [{"label": "A", "members": ["A", "B", "C"], "size": 3}]}
{"run_id": "run-7", "tick": 1200,
 "phenomena": [{"identifier": "InformationBottleneck", "label": "…", "signals": […]}],
 "disclaimer": "ECHOS ne doit jamais transformer une métrique en vérité scientifique…"}
```

Communautés actives (propagation d'étiquettes) et phénomènes (ECHOS-031) dérivés du contexte écrit à l'ingestion.

### 3.7 `GET /api/runs/{id}/decisions` (jalon ECHOS ph5, ECHOS-051)

```json
{"run_id": "run-7", "decisions": [
  {"tick": 1, "agent_id": "A", "chosen_action": "SeekFood",
   "utility": 0.75, "deliberated": true, "interrupted": false,
   "cause": "hunger=30,thirst=20,fatigue=10.5",
   "beliefs_count": 2, "goals_count": 1, "memory_count": 8,
   "needs": {"hunger": 30.0, "thirst": 20.0, "fatigue": 10.5, "energy": 50.0}}]
}
```

Traces de décision (table `decision_traces`, schéma v3) — brique de l'analyse
causale (`CAUSAL_ANALYSIS.md`). Tri **déterministe** `(tick, agent_id)` ;
**reproductible** (deux appels → corps identiques) ; run inconnu → 404.

### 3.8 Erreurs

- `404` : run inconnu (explicite ou aucun run) ; entité absente du tick le plus récent.
- `400` : `format` d'export inconnu.
- `422` : `every < 1` (validation OpenAPI).
- `503` : aucun store configuré (`ECHOS_ANALYTICS_DB` non défini).

## 4. La diffusion temps réel

- Prototype : ECHOS diffusait les métriques en temps réel via WebSocket (`ws://localhost:5180/metrics`) vers l'interface.
- **V0.1** : l'interface étant **intégrée à ECHOS**, cette diffusion alimente directement les vues d'analyse (consommation temps réel des `snapshot`/`event` de SYNE + métriques calculées en ligne).

## 5. L'optimisation

- **Agrégation incrémentale** : les métriques sont calculées à chaque snapshot **au moment de l'ingestion** (`consume()` → `analysis.compute_all`) et persistées dans `tick_metrics` — aucun recalcul complet à la lecture (ECHOS ph4). Depuis ph5, le coût par moteur est aussi tracé (contexte `profiling`, ECHOS-052) et les `decision_made` alimentent `decision_traces`. Le contexte `agents` reste servi aux vues lecture seule (croyances/relations).
- **Cache de séries** : `SeriesCache` LRU **borné** (256 entrées, thread-safe) ; les séries par run sont réutilisées et **invalidées sur `ingest_version`** (écriture d'un nouveau tick/contexte), pas sur le temps (ECHOS-044).
- **Parallélisation** : les calculs lourds (co-localisation O(n²), plus proche ressource) sont parallélisés.
- **Sous-échantillonnage** : `--sample-every=N` pour ne garder que 1 snapshot sur N à l'ingestion ; `?every=N` pour retourner des séries sous-échantillonnées à la lecture.

---

## Points restés ouverts dans ce document
- `/api/compare` (jalon ph7, ECHOS-070) et `/api/communication-heatmap` (périmètre UI) ne sont pas implémentés en V0.1.
- Compatibilité de versionnage des réponses à aligner sur `VERSIONING.md` (évolutions additives = MINOR).