# ROADMAP.md

**Composant** : ECHOS
**Statut** : [DRAFT]
**Dernière mise à jour** : 23 septembre 2026
**Dépend de** : `../ROADMAP.md` (racine), `../docs-syne/ROADMAP.md`
**Source Monographie** : §9.6, §7.9 (adapté), §4.2 (architecture)

---

## 1. Principes

- Road map **en ordre, sans dates** (décision utilisateur).
- ECHOS **consomme les contrats SYNE** définis en phase 2 : il dépend des `API_CONTRACTS.md` SYNE et du schéma SQLite (Annexe G).

## 2. Les phases (ordre)

| # | Intitulé | Contenu |
| :-- | :-- | :-- |
| 0 | Fondation & stack | Choix FastAPI + React/Vite web local (tranché pour V0.1 ; **shell Electron conservé**, implémentation **différée à un horizon ultérieur** — décision 23/09/2026), structure monorepo `echos/`, outillage (pytest, CI) |
| 1 | Ingestion & stockage | Consommateur WebSocket :5180, agrégation incrémentale, SQLite + Parquet (stockage d'analyse séparé) — **LIVRÉ (ECHOS-010 → 013, jalon U1)** |
| 2 | Moteurs de métriques | Implémentation des 7 moteurs (`METRICS_SPEC.md`), golden files, tests unitaires — **LIVRÉ (ECHOS-020 → 027, jalon U2)** |
| 3 | Indicateurs d'émergence | Score composite, auto-détection des phénomènes, complexité, indice d'imprévisibilité — **LIVRÉ (ECHOS-030 → 033, jalon U3)** |
| 4 | API REST | Endpoints (`API_REST.md`), sous-échantillonnage, cache de séries, parallélisation O(n²) — **LIVRÉ (ECHOS-040 → 045, jalon U4)** : endpoints operatifs, métriques calculées à l'ingestion, `?every=N` + cache de séries (invalidation par version), export JSON/CSV reproductible, couverture API ≥ 80 % |
| 5 | Logging & instrumentation | 3 niveaux (structuré/traces/texte), profilage, console de débogage, export CSV/JSON — **LIVRÉ (ECHOS-050 → 052, jalon U5)** : package `echos/instrumentation/` (JSONL déterministe + tag `SSE-V2`), traces `decision_traces` (schéma v3) accessibles via `/api/runs/{id}/decisions`, profilage bit-à-bit des 8 moteurs (budgets V0.1 en garde-fou CI) ; console de débogage hors périmètre ECHOS (spécif. monographie, console SYNE) |
| 6 | Analyse causale | Reconstruction des chaînes causales depuis `decision_traces`, outillage de navigation — **LIVRÉ (ECHOS-060 → 063, jalon ph6, issues #215 → #218)** : ADR-002 [Accepted] (calcul hors ligne), `causal.build_chain` (7 couches), cycle/récurrence + profondeur bornée, `CausalCache` invalidé sur version, endpoint `GET /api/runs/{id}/causal-chains/{agentId}` |
| 7 | Comparaison expérimentale | `/api/compare`, métriques de reproductibilité, format d'export | **LIVRÉ (ECHOS-070 → 072, jalon ph7, U7)** : `GET /api/compare` (`is_reproducible` seed×version×SHA-256, `ReproducibilityScore = 1 − (CognitiveDiff+SocialDiff)/2`, L2 normalisées sur croyances/confiance — `echos/analysis/reproducibility.py`), exports `json`/`csv` (séries alignées `tick,engine,metric,run_a_value,run_b_value,diff`), 11 tests (`test_compare.py`), couverture 97,97 % |
| 8 | Interface intégrée | Écrans ECHOS (web local React/Vite servie par FastAPI) : vues de métriques, croyances, réseaux, calibration |
| 9 | Tests & couverture | ≥ 80 %, non-régression des scores (golden files) |
| 10 | Shell Electron (horizon ultérieur) | Packaging natif de bureau de `echos-ui` via **Electron** — **conservé** (non abandonné), implémentation **différée post-V0.1** (correction décision 23/09/2026, `ADR-001`) ; coordonné avec PRISM (`../docs-prism/`) |

## 3. Jalons de validation

| Jalon | Critère |
| :-- | :-- |
| J1 | Ingestion de 2 runs de démo → séries complètes en SQLite (déterministes et alignées sur SYNE) |
| J2 | 7 moteurs calculés sur fixtures = golden files (18+ tests du prototype porte-unitaire reconduits) |
| J3 | Score d'émergence dans [0,1] et stable entre runs identiques — **LIVRÉ (preuve `test_j3_determinism.py`, moteur `EmergenceIndicators`)** |
| J4 | Comparaison seed=12345, sociabilité 0.2 vs 0.8 → divergence mesurable et reproductible |
| J5 | API REST couverte par tests, ≥ 80 % — **LIVRÉ (preuve `test_api_routes.py` + `test_sqlite_store.py`/`test_pipeline.py` étendus, couverture totale 98,2 %)** ; étendue U5 : traces de décision exposées et profilage couvert (preuve `test_instrumentation.py`, 181 tests) |

## 4. Dépendances externes

- SYNE : WebSocket :5180 (snapshot/event), HTTP :5181 (contrôle), schéma SQLite Annexe G.
- PRISM : les vues d'analyse intégrées à ECHOS sont réutilisées dans PRISM (cf. `../docs-prism/UX_INTERACTION.md`) — coordination en `../ARCHITECTURE.md` racine.

## 5. Limites assumées (cf. `LIMITATIONS.md`)

- Volumétrie → sous-échantillonnage.
- Calculs O(n²) → parallélisation.
- Causalité → traces + reconstruction (pas de causalité magique).

---

## Points restés ouverts dans ce document
- Aucune date n'est posée. L'ordre ci-dessus est une proposition issue du Plan doc ; il sera ajusté selon l'avancement réel de SYNE (les jalons dépendent des contrats SYNE).
- Le périmètre exact des écrans **web (React/Vite)** est [OUVERT] au-delà des besoins définis par les parties 4 et 5 de la Monographie.