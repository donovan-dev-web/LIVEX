# TESTING.md

**Composant** : ECHOS
**Statut** : [DRAFT]
**Dernière mise à jour** : 22 septembre 2026
**Dépend de** : `ARCHITECTURE.md`, `METRICS_SPEC.md`
**Source Monographie** : §4.9.2 (instrumentation du prototype V1), Annexe I.3 (couverture ≥ 80 %)

---

## 1. Objectif

Garantir la **correction et la stabilité des métriques**. Objectif de couverture : **≥ 80 %** (Annexe I.3).

## 2. Référence validée dans le prototype V1 (Annexe §4.9.2)

- **18 tests xUnit** couvrant `MetricsTests`, `RunStoreTests`, `EmergenceTests` (prototype .NET de l'analyzer).
- **Couverture : 85.0 %** des lignes.
- **Agrégation incrémentale validée** : identique au calcul de référence.
- **Sous-échantillonnage validé** : `?every=10` retourne 9/86 échantillons.

## 3. Stratégie de test (V0.1 — FastAPI/Python)

| Niveau | Contenu |
| :-- | :-- |
| **Métriques** | Chaque moteur des 7 `METRICS_SPEC.md` testé sur des jeux de données synthétiques avec valeurs attendues calculées à la main (ex. entropie de Shannon, coefficient de clustering, communauté). |
| **Scores** | Tests du score d'émergence composite (bornes [0,1], poids = 1.0), des phénomènes auto-détectés (conditions de seuils). |
| **API** | Tests d'endpoints (`/health`, `/api/runs/*`, `/api/compare`) avec fixtures de runs. |
| **Ingestion** | Test du consommateur WebSocket : ingérer un fixture de `snapshot`/`event`, vérifier agrégation incrémentale. |
| **Non-régression** | Série temporelle de référence figée : recalculer les métriques, comparer aux valeurs dorées (golden files). |
| **Reproductibilité** | `ReproducibilityScore` vérifié sur deux runs identiques (1.0) et sur deux runs avec un paramètre modifié (< 1.0). |

## 4. Outillage

- **pytest** + fixtures dédiées (runs synthétiques exportés en fichiers Parquet/JSON).
- **Golden files** : valeurs attendues stockées pour détection de non-régression.
- CI : exécuté dans `ci.yml` GitHub Actions (`docs/../..` racine), job ECHOS.

### 4.1 Contrats d'ingestion (ECHOS-003/ECHOS-004, U0)

Fixtures et golden files **versionnés** dans `echos/echos/tests/` :

| Fichier | Rôle |
| :-- | :-- |
| `fixtures/world_snapshot.json` | Snapshot camelCase (API_CONTRACTS.md §2.1, format doc) |
| `fixtures/world_snapshot_v01.json` | Snapshot **V0.1 réel** émis par SYNE (sans `health`, avec `species`/`fatigue`) |
| `fixtures/external_event.json` | Événement `decision_made` (§2.2) |
| `fixtures/decision_made_v01.json`, `fixtures/tick_summary_v01.json` | Événements V0.1 réels émis par SYNE |
| `fixtures/invalid_message.json` | Payload hors contrat (tick négatif) |
| `golden/world_snapshot.json` | Forme canonique snake_case attendue après parse |
| `golden/world_snapshot_v01.json`, `golden/segment_tick1_v01.json` | Forme canonique V0.1 (dump `exclude_none`) |
| `golden/external_event.json` | Forme canonique du snapshot/événement |
| `golden/stream.json` | Séquençage déterministe type+tick d'un flux rejoué |

Double garde : (1) le parse conserve le JSON camelCase du contrat
(`model_dump(by_alias=True)` == fixture) ; (2) la vue interne snake_case reste
égale au golden — tout renommage de champ casse la non-régression. La
réception WebSocket est rejouée **déterministe** (même fixture → même
séquence type/tick), le client de contrôle vérifie le corps exact des
requêtes (`start`/`pause`/`resume`/`reset`).

### 4.2 Flux aligné par tick (ECHOS-010, U1)

`test_ingestion_stream.py` valide `aligned_ticks`/`TickSegment` (1 snapshot +
événements du même tick, `TickAlignmentError` sur désalignement) **deux façons** :

- transport simulé réjoué (déterministe, golden `segment_tick1_v01`) ;
- **serveur WebSocket réel in-process** (`websockets.sync.server`, port
  éphémère) rejouant le contrat V0.1 — consommation bout-en-bout.

### 4.3 Smoke E2E SYNE → ECHOS

Procédure documentaire (nécessite le binaire SYNE, hors CI) :

```bash
# terminal 1 — lancer SYNE en mode observation (seed déterministe)
dotnet run --project syne/Simulation.Console -c Release -- \
  --observe --seed 7 --world-size 200 200 --max-ticks 1200 --headless

# terminal 2 — consommation ECHOS réelle, alignée par tick
cd echos && python - <<'PY'
from echos.ingestion import WsClient, aligned_ticks
client = WsClient(); client.connect("ws://127.0.0.1:5180/")
for segment in aligned_ticks(client):
    print(segment.tick, segment.snapshot.alive_count, len(segment.events))
PY
```

Attendu : ticks consécutifs (ex. 201→202→203), `alive_count` constant,
chaque événement au tick de son snapshot (alignement strict).

**Validation du déterminisme (jalon J1)** : deux runs SYNE réels à seed
identique (`--seed 7 --world-size 400 400 --max-ticks 500 --observe`),
ingérés intégralement jusqu'à fermeture du WebSocket (SYNE sort proprement en
fin de run → `aligned_ticks` s'achève) puis comparés sur leur fenêtre
commune : **443 ticks de `tick_summaries` et 44 300 lignes de séries Parquet,
0 divergence** (procédure documentaire, hors CI).

### 4.4 Agrégation & stockage (ECHOS-011 → ECHOS-013)

`test_aggregation.py` : réduction **déterministe** d'un segment en
`TickRecord` (2 lectures → lignes identiques), conservation de **tous** les
ticks sans échantillonnage, `sample_every`/`downsample` (1 sur N).
`test_sqlite_store.py` : **schéma stable** (tables + `SCHEMA_VERSION`
comparées exactement), règles de réécriture (upsert idempotent),
persistance fermeture/réouverture. `test_parquet_store.py` : roundtrip
PyArrow ↔ Parquet bit à bit et **cohérence** SQLite↔Parquet
(`coherence_errors`). `test_pipeline.py` : bout-en-bout sur **serveur
WebSocket réel in-process** → SQLite + Parquet (compteurs exacts puis
relecture et jointure cohérente).

### 4.5 Moteurs de métriques & preuve J2 (ECHOS-020 → ECHOS-027)

`test_analysis.py` : contrat de registre (7 moteurs + seuil 1 métrique/moteur),
**pureté/déterminisme** (2 exécutions identiques, entrée non mutée, clés
inconnues ignorées), **données absentes → valeurs neutres 0.0** (incluant la
stabilité sans historique), **valeurs vérifiées à la main** par moteur (entropie
de Shannon, désaccord 2/3, variance 0.02, densité 1/3, cluster 0, boucles 4,
récupération 2 ticks, rotation 55,56…), **rétro-compat transport** : le modèle
`Agent` accepte `traits/beliefs/goals/trust/memoryCount` (camelCase, optionnels)
et le roundtrip `parse → model_dump(by_alias=True)` == fixture
(`world_snapshot_u2.json`).

**Preuve J2 (ECHOS-027)** : `test_j2_determinism.py` rejoue **deux runs
complets** du scénario de référence (`snapshot_analysis.json`, contexte par
tick : snapshot + événements passés + fenêtre d'historique accumulée de 100
ticks) :

```bash
cd echos && python -m pytest echos/tests/test_j2_determinism.py -q
# → 2 passed ; séries bit-à-bit identiques, dernier tick == golden
```

Deux rejeux → **séries de métriques strictement égales** (bit-à-bit) et dernier
contexte == `golden/analysis_golden.json`. Combinée à la preuve J1 (déterminisme
SYNE, ci-dessus §4.3), la chaîne SYNE → ECHOS est déterministe : **traces
d'entrée identiques (seed 7) → scores de métriques identiques**. Les golden
files sont versionnés dans `echos/echos/tests/fixtures/` + `golden/` (double
garde : fixture camelCase transport + golden camelCase attendu).

Suite : **115 tests**, couverture **98,2 %** (pytest `--cov-fail-under=80`),
flake8 sans alerte.

### 4.6 Indicateurs d'émergence & preuve J3 (ECHOS-030 → ECHOS-033)

`test_emergence.py` (moteur composite `EmergenceIndicators`, 8ᵉ moteur du
registre) : contrat (pureté/déterminisme, entrée non mutée, **données absentes →
score 0.0 / phénomènes vides**), **score composite vérifié à la main**
(0.7585336 sur `snapshot_analysis.json`, poids Σ=1.0), **bornes [0,1]**
(entropies pouvant excéder 1 → clamp), **`DiffusionSpeed_Norm`** (diminue avec
les ticks, neutralité à vitesse non mesurée), **auto-détection des 5
phénomènes** par seuils (> 2 / > 5 / > 0.7 / > 0.3 / > 5 ET > 0.1) avec trace
des signaux déclencheurs, **`SystemComplexity` et `UnpredictabilityIndex`
(=`LoopStrength × DecisionDiversity`)** vérifiés à la main, **disclaimer §4.10.3
invariant** (ECHOS-032). `compute(snapshot) ≡ compute_from_metrics(moteurs)`.

**Preuve J3** : `test_j3_determinism.py` rejoue deux fois le scénario de
référence (même contexte par tick qu'en J2) — séries d'indicateurs
**bit-à-bit identiques**, **score borné sur [0,1] à chaque cadre**, dernier
cadre == `golden/analysis_golden.json` (clé `EmergenceIndicators`).

Suite : **146 tests** (120 → +26), couverture **98,3 %** (pytest
`--cov-fail-under=80`), flake8 sans alerte.

### 4.7 API REST & preuve J5 (ECHOS-040 → ECHOS-045)

Le jalon **ph4 — API REST** (issues #383 → #388, milestone « ph4 (echos) —
API REST ») expose la couche lecture de l'analyse ECHOS. Les métriques sont
**calculées à l'ingestion** (pipeline `consume()` → `analysis.compute_all`),
jamais recalculées à la lecture (API_REST.md §4).

```bash
cd echos && python -m pytest echos/tests/test_api_routes.py -q
# → 15 passed ; contrat complet exercé
```

Couverture de la couche API (routes + cache + store v2 + pipeline) :

- `test_api_routes.py` : listage des runs (`/api/runs`), métriques complètes
  (`/api/runs/{id}`), **séries `/metrics`** (ticks dédupliqués, alignement
  moteur×métrique, `latest`), **sous-échantillonnage `?every=N`** (index-based,
  réseau aligné sur les ticks), **export reproductible** (JSON trié
  tick/engine/metric + **CSV RFC 4180** avec entête ; deux appels → corps
  identiques : aucune dépendance temporelle), croyances/relations par entité
  (`/beliefs/{agentId}`, `/relationships/{agentId}` — **lecture seule, sans
  intrusion**), groupes (`/groups`) et phénomènes (`/emergent-phenomena`),
  ex. 404 (**run/entité inconnus**, aucun run), 422 (`every` invalide), 400
  (format d'export inconnu) et **503** (API démarrée sans base —
  `ECHOS_ANALYTICS_DB`), résolution du run par défaut (le plus récent).
- `test_sqlite_store.py` (étendu) : **schéma v2** (tables `tick_metrics` +
  `tick_contexts` ajoutées au dump attendu, `SCHEMA_VERSION = "2"`), écriture
  idempotente des métriques (upsert, **sorties non numériques ignorées**),
  roundtrip contextes JSON + `latest_context`/`observations_for`, et
  **`ingest_version` incrémentée à chaque écriture** (moteur d'invalidation
  du cache).
- `test_pipeline.py` (étendu) : `consume()` écrit aussi `tick_metrics`
  (`metrics_written`) et les contextes `agents`/`groups`/`phenomena`
  (`contexts_written` = 3 × ticks) depuis les 8 moteurs `compute_all`.
- `test_api_routes.py::test_series_cache_*` : **cache de séries** LRU borné
  et thread-safe — série **réutilisée tant que `ingest_version` ne bouge pas**,
  **recalculée après une écriture**, capacité bornée (2 → éviction), capacité
  nulle refusée.

Suite : **167 tests** (146 → +21), couverture **98,2 %** (pytest
`--cov-fail-under=80`), flake8 sans alerte.

### 4.8 Logging & instrumentation & preuve J5 étendue (ECHOS-050 → ECHOS-052)

Le jalon **ph5 — Logging & instrumentation** (issues #389 → #391, milestone
« ph5 (echos) — Logging & instrumentation ») ajoute les trois niveaux de
`LOGGING_INSTRUMENTATION.md` (structuré / traces / texte) et le profilage
des 8 moteurs — tous branchés sur le pipeline d'ingestion.

```bash
cd echos && python -m pytest echos/tests/test_instrumentation.py -q
# → 14 passed ; JSONL déterministe, fusion BDI, profilage bit-à-bit
```

- `test_instrumentation.py` (nouveau, 14 tests) :
  - **`EchosLogger`** : lignes JSON Lines par fichier (`structured-<run>.jsonl`,
    `profilage-<run>.jsonl`, `decision-traces-<run>.jsonl`) **au format stable**
    (clés triées, compacts — deux appels → mêmes clés, seules les valeurs
    changent), événements taggés `[SSE-V2]` dans les logs texte quotidiens,
    `ECHOS_LOG_DIR` lu par `from_env()`.
  - **`build_decision_trace`** : fusion `decision_made` + **contexte BDI** du
    snapshot (action, utilité, `deliberated`/`interrupted`, cause, besoins,
    croyances/objectifs/mémoire — entité inconnue → contexte zéro), rejet
    déterministe des événements non-`decision_made`.
  - **Profilage** : `compute_all_profiled(snapshot)` == `compute_all(snapshot)`
    **bit-à-bit** (déterminisme ECHOS-027 intact), **les 8 moteurs couverts**
    avec 1 appel chacun, format `"{name:<15} : {total:10.2f} ms total,
    {avg:8.2f} ms avg"` (§5).
- `test_api_routes.py` (étendu) : `GET /api/runs/{run_id}/decisions` —
  traces triées `(tick, agent_id)`, **deux appels identiques** (aucun
  horodatage), contexte BDI de l'entité (croyances/objectifs/mémoire),
  **404** run inconnu.
- `test_pipeline.py` (étendu) : `consume()` écrit aussi le contexte
  `profiling` (`contexts_written` = 4 × ticks) et les traces
  (`decision_traces_written` = nombre de `decision_made`), relecture
  `store.decision_traces` triée.
- `test_sqlite_store.py` (étendu) : **schéma v3** — table `decision_traces`
  ajoutée au dump attendu, `SCHEMA_VERSION = "3"`.

Suite : **197 tests** (181 → +16), couverture **97,9 %** (pytest
`--cov-fail-under=80`), flake8 sans alerte.

### 4.9 Analyse causale & preuve J6 (ECHOS-060 → ECHOS-063)

Le jalon **ph6 — Analyse causale** (issues #215 → #218, milestone
« ph6 (echos) — Analyse causale ») reconstruit les chaînes causales **hors
ligne** (ADR-002 [Accepted]) avec détection des boucles et cache versionné.

```bash
cd echos && python -m pytest echos/tests/test_causal_analysis.py -q
# → 16 passed ; chaîne 7 couches, cycles, cache, endpoint REST
```

- `test_causal_analysis.py` (nouveau, 16 tests) :
  - **Reconstruction (ECHOS-061)** : `build_chain` sans `tick` → dernière
    décision de l'entité ; 7 couches exactement (`LAYERS`), ordre stable ;
    besoins classés (valeur desc, clé), croyances triées, perception =
    derniers `message_received` ; couche vide → `—` ; `depth` tronque et
    signale ; `depth=0`/`max_depth=13` rejetés (`CausalError`) ; entité/run
    inconnus → erreur.
  - **Boucles (ECHOS-062)** : action reprise aux ticks précédents ⇒
    `cycle=true` + `cycles[].ticks` ; première décision ⇒ aucun cycle.
  - **Cache (ECHOS-063)** : `CausalCache` — réutilisé tant que
    `ingest_version` ne bouge pas, **recalculé après une écriture**, capacity
    bornée (LRU) ; `capacity <= 0` refusé.
  - **Endpoint (API_REST.md §3.8)** : 200 chaîne complète (défauts),
    `?depth=` honoré, 422 (`depth` 0 et > 12), 404 (entité sans trace, run
    inconnu, tick sans trace), 503 sans store, **déterminisme** : deux
    magasins à données identiques → corps JSON identiques.
- `test_sqlite_store.py`/`test_api_routes.py` (non modifiés) : lecture
  `latest_decision_tick`/`context_before` couverte indirectement par
  `test_causal_analysis.py`.

## 5. Critères de non-régression

- Une modification qui **change un score calculé sur un fixture identique** est refusée (sauf changement de formule documenté dans `CHANGELOG.md` + mise à jour du score de version « moteur de métriques »).

---

## Points restés ouverts dans ce document
- Fenêtres temporelles et seuils des moteurs (100 ticks, fréquence > 2, amplification > 1,5) : valeurs `[HÉRITÉ]` à **confirmer en calibration** (METRICS_SPEC §6) — le code les expose en constantes de chaque module, la formule reste stables pour les golden files.
- Preuve J2/J3 ECHOS : rejeux synthétiques en CI ; l'ingestion **réelle** de deux runs SYNE (binaire .NET, hors CI) suivra au jalon J3 avec l'API `/api/compare` (ECHOS-070).