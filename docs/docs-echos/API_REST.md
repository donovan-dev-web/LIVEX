# API_REST.md

**Composant** : ECHOS
**Statut** : [STABLE]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : `ARCHITECTURE.md`, `METRICS_SPEC.md`, `METRICS_DICTIONARY.md`, `../docs-syne/API_CONTRACTS.md`
**Source Monographie** : §4.7

---

## 1. Positionnement

API REST **locale** d'ECHOS (FastAPI en V0.1 — voir `ARCHITECTURE.md`), port **5000**. Depuis l'ADR-007, ECHOS n'a **plus d'interface** : cette API est le **seul contrat de sortie** et elle alimente les fenêtres natives du Launcher (consoles + fenêtre d'analyse). Elle est distincte du contrat de contrôle de SYNE (HTTP :5181) et de transport de SYNE (WS :5180). L'API est **lecture seule** : aucune écriture dans le monde observé (règle d'or §4.10.3, ECHOS-042).

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
| GET | `/api/runs/{id}/viability` | Profil de viabilité : population, besoins, ressources, complétude, chronologie d'extinction |
| GET | `/api/metrics/catalog` | Catalogue versionné des métriques (fiches : unité, domaine, dénominateur, statut) |
| GET | `/api/experiments/summary` | Synthèse multi-runs : contexte de contrôle + dispersion publiée (`?runs=a,b`, 2 à 12) |
| GET | `/api/runs/{id}/events` | Journal d'événements publié, borné (`?type=`, `?limit=` ≤ 2000) — annotations de la vue temporelle |
| GET | `/api/runs/{id}/causal-chains/{agentId}` | Chaîne causale d'une entité (`?tick=`, `?depth=` ≤ 12) — jalon ph6 |
| GET | `/api/beliefs/{agentId}` | Croyances de l'entité au tick le plus récent |
| GET | `/api/relationships/{agentId}` | Réseau de confiance de l'entité |
| GET | `/api/groups` | Liste des groupes actifs (communautés) |
| GET | `/api/world` | Description de monde (terrain, obstacles, ressources, régions) + observation au tick — vue 2D |
| GET | `/api/trust-graph` | Graphe de confiance : nœuds (entités) et arêtes (`trust` publié) — ADR-007 |
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

Depuis **P1** : `conservation` porte le niveau de conservation **réellement
configuré** pour ce run (contexte `conservation` persisté au tick 0 : `base` /
`sampled_details` / `high_fidelity`, voir §3.18) — l'interface annonce la
fidélité avant toute lecture, sans jamais l'inventer.

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
 "measured_by_tick": {"EmergenceIndicators": {"EmergenceScore": [true, true, false]}},
 "missing_ticks": [4, 5], "missing_ticks_count": 2,
 "latest": {"EmergenceIndicators": {"EmergenceScore": 0.51}},
 "latest_tick": 3,
 "measured": {"EmergenceIndicators": {"EmergenceScore": true}}}
```

- `ticks` : union des ticks des séries demandées, **sous-échantillonnés** `ticks[::every]` (`?every=N`, `N ≥ 1` — `N = 0` → 422) ; `values` alignées sur `ticks`. Ce sont les **ticks réels** : l'axe temporel d'une courbe est cette liste, jamais un index.
- `every = 1` (défaut) : série complète ; `?engine=` / `?metric=` restreignent `values`.
- `values` : `null` à un tick sans observation — le **trou est visible**, jamais comblé ni décalé (les tableaux ont tous la longueur de `ticks`).
- `measured_by_tick` : provenance **alignée point par point** sur `ticks`, `{engine: {metric: [bool|null]}}` — `true` mesuré, `false` repli neutre du moteur (faute de fenêtre), `null` tick sans observation. Un consommateur qui affiche un chiffre traite `false` comme « non mesuré » et non comme `0`.
- `missing_ticks` / `missing_ticks_count` : ticks absents **entre** le premier et le dernier tick observé (lacunes publiées, jamais interpolées). La liste est bornée à **500** entrées détaillées (`missing_ticks_count` donne le total exact).
- `measured` : provenance du **dernier** tick seulement (contrat historique, aligné sur `latest`) — utiliser `measured_by_tick` pour tout nouveau code.
- `latest_tick` : dernier tick observationné de la sélection.
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
(inchangée), ainsi que `resources` (réserves du tick avec position, pour la
vue 2D). Le contexte `world` est lui écrit **une seule fois**, au tick 0, dès
que `world_initialized` est consigné. `latest_context`, `context_before`, `/beliefs` et
`/relationships` lisent « le plus récent disponible » : le contexte servi peut
dater de ≤ `context_every` ticks (documenté dans CAUSAL_ANALYSIS.md §4).
L'empreinte de reproductibilité n'en est pas affectée : la cadence fait partie
du protocole expérimental et deux runs comparés l'utilisent (EXPERIMENT_COMPARISON.md).

La cadence **réellement configurée** est publiée avec le run
(`conservation.sampledDetails.agentContextEvery`, §3.18) : l'interface affiche
« cadence N tick(s) » et l'écart au dernier tick observé au lieu de laisser
croire qu'un contexte échantillonné est complet (P3, manque B2).

### 3.12 Erreurs

- `400` : `format` d'export inconnu ou `format` de `/api/compare` hors {`json`, `csv`} ; synthèse multi-runs avec moins de 2 ou plus de 12 runs.
- `404` : run inconnu (explicite ou aucun run) ; entité absente du tick le plus récent ; entité sans trace de décision (`causal-chains`).
- `422` : `every < 1`, `depth` hors [1, 12], `limit` d'événements hors [1, 2000], `run_a`/`run_b` manquants (validation OpenAPI).
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

### 3.14 `GET /api/world` et `GET /api/trust-graph` (vue 2D, ADR-007)

Ces deux endpoints alimentent la **fenêtre d'analyse native du Launcher** et
restent lecture seule : ECHOS transporte ce qui est stocké, sans agrégat.

- `GET /api/world?run_id=&tick=` — réponse :
  `{run_id, tick, world_tick, world, agents, groups, resources}`.
  `world` est la description publiée une fois dans `world_initialized`
  (largeur, hauteur, `cells` de terrain, `obstacles`, `resources` initiales,
  `regions`), persistée en contexte `world` au **tick 0** à l'ingestion (flux
  live et archive `stream.jsonl`) ; `world_tick` vaut `-1` si elle n'a jamais
  été publiée. `agents`/`groups`/`resources` sont les contextes du tick demandé
  (ou du plus récent au plus fort ; `tick` absent = dernier observé, sans
  observation → `tick: -1` et listes vides).
- `GET /api/trust-graph?run_id=&tick=` — réponse :
  `{run_id, tick, nodes, edges}`. `nodes` : `{id, x, y, energy, hunger, thirst,
  action, group}` ; `edges` : `{source, target, weight}` où `weight` est le
  niveau de confiance **publié** par l'entité source (0–1). Seule la forme du
  graphe est assemblée : aucune valeur n'est moyennée ni estimée.

Les erreurs suivent §3.12 : run inconnu → `404`, aucun store → `503`.

### 3.15 `GET /api/metrics/catalog` (registre versionné, P1)

```json
{"version": "2.0.0", "metricCount": 61,
 "history": [{"version": "2.0.0", "changes": "NetworkCentrality → SenderConcentration ; …"}],
 "metrics": [{"id": "SenderConcentration", "engine": "InformationPropagationMetrics",
              "label": "…", "unit": "fraction", "domain": "[0, 1]",
              "definition": "…", "calculation": "…", "population": "…",
              "window": "…", "direction": "…", "status": "measured",
              "states": ["observed_zero", "window_empty", "unmeasured"],
              "warning": "…", "visual": "…", "renamedFrom": ["NetworkCentrality"]}]}
```

Source unique de vérité documentaire (`METRICS_SPEC.md` §1.1, code
`echos/analysis/catalog.py`). Le Launcher **restaure** ces fiches (libellés,
plages, avertissements, statuts) : aucune définition n'est redéfinie côté
interface (ADR-003).

- `version` suit le semver : toute modification de formule ou d'unité augmente
  la version et s'ajoute à `history` — deux versions incompatibles ne se
  comparent jamais silencieusement.
- `renamedFrom` porte les anciens identifiants : migration des séries
  historiques, affichage d'un libellé « anciennement … ».
- `status` ∈ {`measured`, `exploratory`, `suspended`} ; `states` ∈ {`observed_zero`,
  `window_empty`, `insufficient_coverage`, `absent`, `unmeasured`, `censored`}
  (`METRICS_SPEC.md` §12).
- Réponse constante (aucun run, aucun store requis) : elle ne dépend pas de la
  base d'analyse.

### 3.16 `GET /api/runs/{id}/viability` (profil de viabilité, P1/P3)

```json
{"run_id": "run-7", "outcome": "extinct", "extinction_tick": 812,
 "conservation": {"level": "sampled_details", "…": "…"},
 "population": {"initial": 24, "final": 0, "minimumAlive": 0,
                "extinctionTick": 812, "series": [[1, 24], [2, 23]]},
 "needs": {"energy": [[1, 51.2]], "hunger": [[1, 30.0]],
           "thirst": [[1, 20.0]], "fatigue": [[1, 10.5]]},
 "resources": {"food": [[1, 80.0]], "water": [[1, 65.0]]},
 "decisions": [[1, 24.0]],
 "completeness": {"ticksObserved": 812, "missingTickCount": 3,
                  "missingTicks": [41, 42, 43], "conservation": {"…": "…"},
                  "calibrationAvailable": true},
 "calibration": {"…": "rapport post-run complet"},
 "viability": {"energySlopePerTick": "…", "actionSharesWhenHungry": "…",
               "resourceRegime": "…"},
 "extinctionChronology": [{"tick": 810, "type": "agent_died", "agent_id": "A",
                           "action": "…", "cause": "…"}]}
```

- **Sources déjà conservées** : résumés de tick (`tick_summaries`, schéma ≥ v6)
  pour population/besoins/décisions/réserves + rapport de calibration post-run
  — aucun agrégat n'est produit ici.
- `population`, `needs`, `resources`, `decisions` sont des séries
  `[tick, valeur]`, bornées par `?every=N` (défaut 1). Une colonne absente de la
  base (schéma antérieur) vaut `null`, jamais une série inventée.
- **Temps réel vs post-run** : `population`/`needs`/`resources`/`decisions` sont
  des données **temps réel** ; `calibration` et `viability` sont le **rapport
  post-run** (pente d'énergie, actions sous faim > 70, régime des ressources).
  L'interface distingue les deux (« rapport post-run » affiché tel quel).
- **Aucune projection ni agrégat synthétique** : « aucune extinction observée
  jusqu'au tick N » est un fait ; la stabilité n'est jamais déclarée.
- `extinctionChronology` : chronologie **descriptive** avant l'extinction
  (derniers événements ≤ `extinction_tick`, bornés à **50** entrées) — la cause
  publiée est celle du moteur, jamais une cause racine inférée.
- `completeness` : fenêtre réellement couverte (ticks observés, lacunes bornées
  à 500, niveau de conservation persisté, présence du rapport de calibration).
- Erreurs §3.12 : run inconnu → `404`.

### 3.17 `GET /api/experiments/summary` (synthèse multi-runs, P3)

```json
{"run_ids": ["EXP-A-RUN-0001", "EXP-A-RUN-0002"],
 "runs": [{"run_id": "…", "seed": "7", "version": "0.1.0",
           "outcome": "surviving", "extinction_tick": null,
           "conservation": {"level": "sampled_details"}, "…": "…"}],
 "metrics": {"EmergenceIndicators": {"EmergenceScore": {
     "values": {"EXP-A-RUN-0001": 0.51, "EXP-A-RUN-0002": 0.49},
     "runsObserved": 2, "runsMeasured": 2,
     "min": 0.49, "max": 0.51, "mean": 0.5, "spread": 0.02}}},
 "metricCount": 61,
 "note": "Valeur au dernier tick observé de chaque run ; dispersion descriptive …"}
```

- **Contexte de contrôle** : chaque run est publié avec version, graine, issue et
  niveau de conservation — comparer sans ce contexte est une comparaison
  invalide.
- **Instant propre à chaque run** : la valeur est celle du **dernier tick
  observé** de ce run ; deux runs de longueurs différentes ne partagent pas
  nécessairement le même tick (les métadonnées le disent).
- **Dispersion descriptive publiée** : `min`/`max`/`mean`/`spread` (écart
  max−min) et dénominateurs `runsObserved`/`runsMeasured`. Aucun test
  d'hypothèse, aucune taille d'effet : **un écart entre runs n'est pas un effet**
  (voir la `note`, affichée telle quelle).
- **Bornes** : 2 à **12** runs (`400` au-delà ou en dessous de 2). `?engine=` /
  `?metric=` filtrent.
- La comparaison de **reproductibilité** (empreinte, `bit_identical`) reste sur
  `GET /api/compare` (§3.9).

### 3.18 Fenêtre réellement couverte et bornes de lecture

Aucune réponse ne présente une lacune comme une donnée :

| Limite | Valeur | Endroit |
| :-- | :-- | :-- |
| Lacunes de tick détaillées | 500 entrées (`missing_ticks_count` donne le total) | §3.3, §3.16 |
| Événements avant extinction | 50 dernières entrées (`extinctionChronology`) | §3.16 |
| Runs comparés | 2 à 12 | §3.17 |
| Profondeur de chaîne causale | ≤ 12, `truncated` signalé | §3.8 |
| Contextes détaillés | 1 tick sur `context_every` (défaut 20) + dernier tick | §3.11 |
| Fenêtre d'événements servie aux moteurs | 100 ticks (`_EVENT_WINDOW_TICKS`, aligné sur `WINDOW_SIZE`), plafond dur de 2000 événements | §3.18 |
| Journal d'événements détaillé servi au client | 500 lignes par défaut, 2000 au plus (`limit`), `total` donnant le réel | §3.19 |
| Sous-échantillonnage de lecture | `?every=N` (métriques, viabilité) | §3.3, §3.16 |

**Fenêtre d'événements et durée réellement observée.** Le pipeline ne transmet
jamais l'intégralité du flux aux moteurs : chaque tick poussé est tronqué à une
fenêtre glissante bornée **en ticks** (100, alignée sur la fenêtre de
`FeedbackLoopDetector`) puis **en nombre** (2000 événements, borne mémoire pour
les rafales). La durée réellement couverte est publiée **dans le snapshot**
(`eventWindow {ticks, from, to}`) : sans elle, une fenêtre vide est
`window_empty` (observé, sans occurrence) et non `absent`. C'est cette borne qui
rend les taux « par 1000 ticks » comparables d'un run à l'autre : une borne en
nombre d'événements rendait le taux dépendant de la charge du monde.

Les **chaînes causales** sont tronquées à `depth ≤ 12` avec un drapeau
`truncated` (§3.8) ; les **événements avant extinction** sont bornés à 50
entrées (§3.16). Aucune de ces troncatures n'est présentée comme une
observation complète : le drapeau ou la borne accompagne toujours la donnée.

Le niveau de conservation **réellement configuré** est persisté au tick 0 de
chaque run (contexte `conservation`, `base` / `sampled_details` /
`high_fidelity`) et publié avec les métadonnées du run : le lecteur sait, avant
toute lecture, ce qui est archivé à quel niveau (`METRICS_SPEC.md` Annexe B).

### 3.19 `GET /api/runs/{id}/events` (journal d'événements, P3)

```json
{"run_id": "EXP-A-RUN-0001", "total": 42, "limit": 500,
 "types": [{"type": "agent_died", "count": 31},
           {"type": "decision_made", "count": 11}],
 "events": [{"tick": 7, "type": "agent_died", "agent_id": "A-3",
             "action": null, "cause": "energy=0"}]}
```

- **Lecture seule du journal déjà persisté** (`events_log`), **bornée** :
  `limit` lignes détaillées (défaut **500**, plafond **2000**, `422` au-delà),
  `total` donnant le nombre réel de lignes du filtre — la borne est annoncée,
  jamais présentée comme l'intégralité.
- `?type=` filtre sur un type d'événement ; `types` liste les types présents
  avec leur comptage (ordre alphabétique, déterministe).
- **Ordre** : celui du journal `(tick, ordre d'émission)` — déterministe pour
  une base donnée. Aucune interprétation : `action` et `cause` sont transportés
  tels que SYNE les a émises ; jamais une cause racine déduite.
- **Usage** : annotations de la courbe dans la fenêtre d'analyse du Launcher
  (`USER_INTERFACE.md` §9.2) — le marqueur signale *qu'un* événement existe à
  ce tick, pas ce qu'il signifie. Distinct de `extinctionChronology` (§3.16) :
  celle-ci est bornée à 50 et limitée à l'avant-extinction.
- Erreurs §3.12 : run inconnu → `404` ; `limit` hors [1, 2000] → `422`.

## 4. La diffusion temps réel

- Prototype : ECHOS diffusait les métriques en temps réel via WebSocket (`ws://localhost:5180/metrics`) vers l'interface.
- **V0.1** : l'interface web ayant été retirée (ADR-007), cette diffusion alimente la **fenêtre d'analyse native du Launcher**, qui consomme l'API REST en relevé périodique (consommation temps réel des `snapshot`/`event` de SYNE + métriques calculées en ligne à l'ingestion).

## 5. L'optimisation

- **Agrégation incrémentale** : les métriques sont calculées à chaque snapshot **au moment de l'ingestion** (`consume()` → `analysis.compute_all`) et persistées dans `tick_metrics` — aucun recalcul complet à la lecture (ECHOS ph4). Depuis ph5, le coût par moteur est aussi tracé (contexte `profiling`, ECHOS-052) et les `decision_made` alimentent `decision_traces`. Le contexte `agents` reste servi aux vues lecture seule (croyances/relations).
- **Cache de séries** : `SeriesCache` LRU **borné** (256 entrées, thread-safe) ; les séries par run sont réutilisées et **invalidées sur `ingest_version`** (écriture d'un nouveau tick/contexte), pas sur le temps (ECHOS-044). Depuis ph6, les chaînes causales passent par un **`CausalCache`** LRU de même sémantique (ECHOS-063).
- **Parallélisation** : les calculs lourds (co-localisation O(n²), plus proche ressource) sont parallélisés.
- **Sous-échantillonnage** : `--sample-every=N` pour ne garder que 1 snapshot sur N à l'ingestion ; `?every=N` pour retourner des séries sous-échantillonnées à la lecture.

---

## Points restés ouverts dans ce document
- `/api/communication-heatmap` (périmètre UI) n'est pas implémenté en V0.1.
- Compatibilité de versionnage des réponses à aligner sur `VERSIONING.md` (évolutions additives = MINOR).
