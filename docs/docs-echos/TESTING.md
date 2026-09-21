# TESTING.md

**Composant** : ECHOS
**Statut** : [DRAFT]
**Dernière mise à jour** : 21 septembre 2026
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

## 5. Critères de non-régression

- Une modification qui **change un score calculé sur un fixture identique** est refusée (sauf changement de formule documenté dans `CHANGELOG.md` + mise à jour du score de version « moteur de métriques »).

---

## Points restés ouverts dans ce document
- Fenêtres temporelles et seuils des moteurs (100 ticks, fréquence > 2, amplification > 1,5) : valeurs `[HÉRITÉ]` à **confirmer en calibration** (METRICS_SPEC §6) — le code les expose en constantes de chaque module, la formule reste stables pour les golden files.
- Preuve J2 ECHOS : rejeu synthétique en CI ; l'ingestion **réelle** de deux runs SYNE (binaire .NET, hors CI) suivra au jalon J3 avec l'API `/api/compare` (ECHOS-070).