# TESTING.md

**Composant** : ECHOS
**Statut** : [DRAFT]
**Dernière mise à jour** : 17 septembre 2026
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
| **Métriques** | Chaque moteur des 7 `METRICS_SPEC.md` testé sur des jeux de données synthétiques avec valeurs attendues calculées à la main (ex. entropie de Shannon, coefficient de clustering, Louvain). |
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

## 5. Critères de non-régression

- Une modification qui **change un score calculé sur un fixture identique** est refusée (sauf changement de formule documenté dans `CHANGELOG.md` + mise à jour du score de version « moteur de métriques »).

---

## Points restés ouverts dans ce document
- Le périmètre exact de tests par moteur sera affiné à l'implémentation (nombres par moteur).
- Outillage (pytest, golden files) à valider en environnement CI en même temps que la stack FastAPI.