# API_REST.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 24 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `../docs-syne/API_CONTRACTS.md`
**Source Monographie** : §4.7

---

## 1. Positionnement

API REST **locale** d'ECHOS (FastAPI en V0.1 — voir `ARCHITECTURE.md`), port **5000**. Elle sert l'interface ECHOS (intégrée) et expose les données d'analyse. Elle est distincte du contract de contrôle de SYNE (HTTP :5181) et de transport de SYNE (WS :5180). L'API est **lecture seule** : aucune écriture dans le monde observé (règle d'or §4.10.3, ECHOS-042).

### 1.1 Configuration

- La base d'analyse est fournie à `create_app(store)` ; à défaut, la variable d'environnement **`ECHOS_ANALYTICS_DB`** (chemin du fichier SQLite) est lue.
- Sans base configurée, les routes de données répondent **503** (le contrat reste publié dans l'OpenAPI) — `/health` et `/` restent disponibles.
- `SYNE_CONTROL_URL` définit le serveur HTTP SYNE relayé par ECHOS (défaut `http://127.0.0.1:5181`). Si SYNE `--serve` est arrêté, les routes de contrôle répondent **503** avec une instruction de démarrage.

## 2. Les endpoints principaux

| Méthode | Endpoint | Description |
| :-- | :-- | :-- |
| GET | `/health` | Vérification de santé du service |
| GET | `/api/control/status` | État du serveur SYNE relayé par ECHOS (HTTP :5181) |
| POST | `/api/control/{action}` | Relais ECHOS→SYNE pour `start`, `pause`, `resume`, `stop`, `reset` |
| GET | `/api/runs` | Liste des runs enregistrés |
| GET | `/api/runs/{id}` | Métriques complètes du run (dernier tick) + phénomènes |
| GET | `/api/runs/{id}/metrics` | Séries de métriques (JSON, `?engine=`, `?metric=`, `?every=N`) |
| GET | `/api/runs/{id}/export` | Export des métriques (`?format=json\|csv`), reproductible |
| GET | `/api/runs/{id}/decisions` | Traces de décision des entités (analyse causale) |
| GET | `/api/runs/{id}/calibration` | Rapport déterministe post-run (ticks, événements, métriques), lecture seule |
| GET | `/api/runs/{id}/causal-chains/{agentId}` | Chaîne causale d'une entité (`?tick=`, `?depth=` ≤ 12) — jalon ph6 |
| GET | `/api/beliefs/{agentId}` | Croyances de l'entité au tick le plus récent |
| GET | `/api/relationships/{agentId}` | Réseau de confiance de l'entité |
| GET | `/api/groups` | Liste des groupes actifs (communautés) |
| GET | `/api/emergent-phenomena` | Phénomènes émergents détectés + disclaimer |
| GET | `/api/compare` | Comparaison de runs : méta-métriques de reproductibilité + séries comparatives (`?run_a=&run_b=&format=json\|csv`) — jalon ph7 |

(Source : Monographie §4.7.1 — `/api/communication-heatmap` reste à venir (périmètre UI, hors V0.1).)

## 3. Contrat des réponses (V0.1, jalons ECHOS ph4 → ph5)

Ordres **déterministes** (aucun PRNG, aucun horodatage d'émission) : runs triés par `run_id`, séries triées par tick, lignes d'export triées `(tick, engine, metric)`.

### 3.1 `GET /api/runs`

```json
{"runs": [{"run_id": "run-7", "version": "0.1.0", "seed": "7",
           "ticks_count": 1200, "first_tick": 1, "last_tick": 1200,
           "outcome": "surviving", "extinction_tick": null}]}
```

Depuis la **viabilité A3** : chaque run expose son résultat de population —
`outcome` ∈ {`surviving`, `extinct`, `unknown`} et `extinction_tick` (premier
tick où `alive_count = 0`, `null` sinon ; `unknown`/`null` sur un run sans
tick). Le résultat est calculé en SQL sur `tick_summaries` (aucune migration
supplémentaire), disponible **pendant** l'ingestion — un observateur peut
drapper « run fini avec écosystème mort » sans recalculer lui-même depuis les
séries. Idem sur `GET /api/runs/{id}`.

Sans paramètre `run_id` sur les endpoints suivants, le **run par défaut** est le plus récent (`last_tick` maximal, départage `run_id`).

L'identité persistée par ECHOS est le `runId` du snapshot SYNE. Depuis le
contrat SYNE **0.2.1**, les runs pilotés portent le format canonique
`run-<seed>-<12hex>` et le champ snapshot `seed` ; ECHOS enregistre le seed
transporté et ne dérive plus depuis `run_id` que **en repli** (flux ≤ 0.2.0).
Seed introuvable des deux côtés → `seed: ""` + avertissement d'ingestion
(jamais une erreur fatale, observe-only).

### 3.2 `GET /api/runs/{id}`

Métadonnées + **dernières métriques** de tous les moteurs (`{engine: {metric: value}}`) + contexte `phenomena` (`detected` + `disclaimer`).

- `measured` : `{engine: {metric: bool}}` — provenance des **dernières** valeurs (voir §3.3).

### 3.3 `GET /api/runs/{id}/metrics`

```json
{"run_id": "run-7", "engine": null, "metric": null, "every": 1,
 "ticks": [1, 2, 3],
 "values": {"EmergenceIndicators": {"EmergenceScore": [0.5, 0.52, 0.51]}},
 "latest": {"EmergenceIndicators": {"EmergenceScore": 0.51}},
 "measured": {"EmergenceIndicators": {"EmergenceScore": true}}}
```

- `ticks` : union des ticks des séries demandées, **sous-échantillonnés** `ticks[::every]` (`?every=N`, `N ≥ 1` — `N = 0` → 422) ; `values` alignées sur `ticks`.
- `every = 1` (défaut) : série complète ; `?engine=` / `?metric=` restreignent `values`.
- `measured` : provenance des valeurs du **dernier** tick, `{engine: {metric: bool}}` aligné sur `latest`. `false` signifie que la valeur est le **repli neutre** du moteur, faute de la fenêtre dont il dépend (pas « une mesure égale au neutre »). Un consommateur qui affiche un chiffre doit traiter `false` comme « non mesuré » et non comme `0`.
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

### 3.8 `GET /api/runs/{id}/causal-chains/{agentId}` (jalon ECHOS ph6, ECHOS-061 → ECHOS-063)

```json
{"run_id": "run-7", "agent_id": "A", "tick": 3, "depth_requested": 7, "depth_served": 7,
 "chain": [
   {"layer": "Action", "tick": 3, "label": "SeekFood", "detail": {"utility": 0.75}},
   {"layer": "Intention", "tick": 3, "label": "SeekFood", "detail": {}},
   {"layer": "Objectif", "tick": 3, "label": "SeekFood",
    "detail": {"kinds": ["SeekFood", "Eat"], "goals_count": 2}},
   {"layer": "Besoin", "tick": 3, "label": "hunger (80.0)",
    "detail": {"needs": {"hunger": 80.0, "thirst": 20.0, "fatigue": 5.0}}},
   {"layer": "Croyance", "tick": 3, "label": "2 croyance(s)",
    "detail": {"subjects": ["food-1", "water-2"], "beliefs_count": 2}},
   {"layer": "Mémoire", "tick": 3, "label": "4 souvenir(s)", "detail": {"memory_count": 4}},
   {"layer": "Perception", "tick": 3, "label": "Information", "detail": {"received": []}}],
 "cycle": true,
 "cycles": [{"layer": "Action", "label": "SeekFood", "ticks": [1, 2]}],
 "truncated": false}
```

Chaîne reconstruite **hors ligne** sur `decision_traces` + `events_log` + contexte
`agents` (ADR-002 : pas de calcul temps réel) — détails par couche dans
`CAUSAL_ANALYSIS.md` §5. `tick` optionnel (dernière décision de l'entité) ;
`depth` ∈ [1, 12] (défaut 7, troncature signalée). **Déterministe** (deux appels
→ corps identiques) ; réponse servie par le cache `CausalCache` invalidé sur
`ingest_version` (re-run ⇒ re-analyse, ECHOS-063).

### 3.9 `GET /api/compare` (jalon ECHOS ph7, ECHOS-070 → 072)

Comparaison de deux runs contrôlés (EXPERIMENT_COMPARISON.md §2, METRICS_SPEC.md §10) :

```json
{"run_a": {"run_id": "run-1", "version": "0.1.0", "seed": "12345"},
 "run_b": {"run_id": "run-2", "version": "0.1.0", "seed": "12345"},
 "same_seed": true, "same_version": true, "bit_identical": true,
 "is_reproducible": true, "reproducibility_score": 1.0,
 "cognitive_diff": 0.0, "social_diff": 0.0,
 "series": [...], "format": "json"}
```

- `?run_a=` / `?run_b=` (obligatoires) : identifiants de runs ; `format` ∈ {`json`, `csv`} (défaut `json`).
- `?light=1` (**C3**) : renvoie le seul résumé (seed/version/score/diffs), **sans** `series` ni calcul d'empreinte — `bit_identical` et `is_reproducible` valent `null` (le verdict exige l'empreinte). Utile pour l'affichage : l'empreinte charge séries + événements + contextes + traces des deux runs, lourd par conception. Défaut sans `light` : comportement complet conservé. `light` n'affecte pas `format=csv` (l'export a besoin des séries).
- `bit_identical` : empreinte **SHA-256** du contenu canonique du run (séries de métriques, résumés de tick, événements, contextes `agents`/`groups`/`phenomena`, traces de décision — sans l'étiquette `run_id`), `echos/analysis/reproducibility.py`.
- `is_reproducible` = même **seed** ∩ même **version** du moteur ∩ contenu **bit-à-bit identique** ; `reproducibility_score` = `1.0` si reproductible, sinon `1.0 − (cognitive_diff + social_diff)/2` (définition EXPERIMENT_COMPARISON.md §2.3).
- `cognitive_diff` / `social_diff` : distances **L2 normalisées** (borne [0, 1]) entre les distributions de croyances, resp. de confiance, de la population au dernier contexte `agents`.
- `format=csv` : export comparatif aligné (ECHOS-072) — colonnes `tick,engine,metric,run_a_value,run_b_value,diff` (jointure sur ticks/métriques communs, `diff = run_b_value − run_a_value`), reproductible (sortie triée).
- **Déterministe** : aucun PRNG, aucun horodatage ; les distributions/clés sont triées.

### 3.10 `GET /api/runs/{id}/calibration` (SYNE-131, U8)

Retourne le rapport post-run persisté dans `calibration_reports` (schéma SQLite v4).
Le JSON contient les résumés de ticks, statistiques de population et besoins,
comptes d'événements et statistiques par moteur. Les clés et listes sont triées
pour garantir un corps reproductible. Le rapport ne contient pas d'horodatage,
ne modifie pas la configuration ou le monde, et ne déclenche aucun recalibrage
automatique. Run sans tick ou inconnu : 404.

Depuis la **viabilité B2 (schemaVersion 2)**, le rapport expose en plus :

- `outcome` ∈ {`surviving`, `extinct`} et `extinctionTick` (premier tick où
  `alive_count = 0`, `null` sinon) — le rapport reste déterministe, fonction
  pure des résumés existants ;
- bloc `viability` : `energySlopePerTick` (pente moindres carrés de
  `mean_energy` sur les 200 derniers ticks — détecte la **mort lente** même
  sans extinction), `actionSharesWhenHungry` (distribution des actions des
  `decision_made` sur les ticks où `mean_hunger > 70`, avec compteur
  `hungryDecisions`), `resourceRegime` (min/moy/max des séries `food`/`water`
  — `null` sur les bases antérieures aux colonnes `mean_food`/`mean_water`,
  schéma v6) ;
- depuis **A2** : le rapport est bâti à la fin de **chaque** run observé, y
  compris ceux interrompus par un `reset` SYNE en pleine connexion (changement
  de `run_id` en flux), et pas seulement pour le dernier run du flux.

### 3.11 Cadence du contexte `agents` (C2)

Le contexte `agents` (~208 Ko/tick — croyances, relations) est persisté 1 tick
sur `context_every` (défaut **20**, environnement `ECHOS_CONTEXT_EVERY`, min 1
= comportement historique) **plus le dernier tick du flux**. Les contextes
`groups`/`phenomena`/`profiling` suivent la cadence `analysis_every`
(inchangée). `latest_context`, `context_before`, `/beliefs` et
`/relationships` lisent « le plus récent disponible » : le contexte servi peut
dater de ≤ `context_every` ticks (documenté dans CAUSAL_ANALYSIS.md §4).
L'empreinte de reproductibilité n'en est pas affectée : la cadence fait partie
du protocole expérimental et deux runs comparés l'utilisent (EXPERIMENT_COMPARISON.md).

### 3.12 Erreurs

- `404` : run inconnu (explicite ou aucun run) ; entité absente du tick le plus récent ; entité sans trace de décision (`causal-chains`).
- `400` : `format` d'export inconnu ou `format` de `/api/compare` hors {`json`, `csv`}.
- `422` : `every < 1`, `depth` hors [1, 12], `run_a`/`run_b` manquants (validation OpenAPI).
- `503` : aucun store configuré (`ECHOS_ANALYTICS_DB` non défini).

### 3.13 Relais de contrôle SYNE

`POST /api/control/{action}` relaie les actions `start`, `pause`, `resume`, `stop`
et `reset` vers le client HTTP SYNE. Les payloads `seed`, `config`, `maxTicks` et
`runId` sont transmis sans être interprétés par ECHOS. `GET /api/control/status`
relaie l’état du serveur. Le navigateur ne contacte jamais directement le port
5181 ; l’API relaie les requêtes. Si le serveur est inaccessible, ECHOS retourne
503 et indique de lancer SYNE avec `--serve`.

Le mode serveur `--serve` expose à la fois le contrôle HTTP (`:5181`) et le flux
WebSocket (`:5180`). Sans `maxTicks`, un run démarré par `start` continue jusqu'à
une commande `pause`, `stop`, `reset` ou l'arrêt du processus SYNE.

## 4. La diffusion temps réel

- Prototype : ECHOS diffusait les métriques en temps réel via WebSocket (`ws://localhost:5180/metrics`) vers l'interface.
- **V0.1** : l'interface étant **intégrée à ECHOS**, cette diffusion alimente directement les vues d'analyse (consommation temps réel des `snapshot`/`event` de SYNE + métriques calculées en ligne).

## 5. L'optimisation

- **Agrégation incrémentale** : les métriques sont calculées à chaque snapshot **au moment de l'ingestion** (`consume()` → `analysis.compute_all`) et persistées dans `tick_metrics` — aucun recalcul complet à la lecture (ECHOS ph4). Depuis ph5, le coût par moteur est aussi tracé (contexte `profiling`, ECHOS-052) et les `decision_made` alimentent `decision_traces`. Le contexte `agents` reste servi aux vues lecture seule (croyances/relations).
- **Cache de séries** : `SeriesCache` LRU **borné** (256 entrées, thread-safe) ; les séries par run sont réutilisées et **invalidées sur `ingest_version`** (écriture d'un nouveau tick/contexte), pas sur le temps (ECHOS-044). Depuis ph6, les chaînes causales passent par un **`CausalCache`** LRU de même sémantique (ECHOS-063).
- **Parallélisation** : les calculs lourds (co-localisation O(n²), plus proche ressource) sont parallélisés.
- **Sous-échantillonnage** : `--sample-every=N` pour ne garder que 1 snapshot sur N à l'ingestion ; `?every=N` pour retourner des séries sous-échantillonnées à la lecture.

---

## Points restés ouverts dans ce document
- `/api/communication-heatmap` (périmètre UI) n'est pas implémenté en V0.1.
- Compatibilité de versionnage des réponses à aligner sur `VERSIONING.md` (évolutions additives = MINOR).
