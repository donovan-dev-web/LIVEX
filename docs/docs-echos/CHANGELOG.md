# CHANGELOG — ECHOS

**Composant** : ECHOS
**Statut** : [DRAFT]
**Dernière mise à jour** : 6 octobre 2026
**Dépend de** : `../../VERSIONING.md`

Format : [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versionnement : SemVer (`echos-vX.Y.Z`).

## [Unreleased]

### Changed
- **Équations mathématiques rendues en KaTeX/LaTeX** : tous les blocs
  d'équations des documents ECHOS (`ANALYSIS_FOUNDATIONS.md`,
  `DYNAMIC_VIABILITY_INDEX.md`, `EMERGENCE_INDICATORS.md`, `METRICS_SPEC.md`)
  sont désormais écrits en LaTeX dans des blocs ` ```math `, rendus
  nativement par GitHub (MathJax/KaTeX) — fractions `\frac`, sommes
  `\sum_{i=1}^{k}`, exponentielles `\exp`, cas `\begin{cases}`. Remplace les
  blocs ` ```text ` en notation compacte Unicode qui ne se rendaient pas
  comme des équations scientifiques.
- **Contrat scientifique des métriques (P0 → P2, `RAPPORT-ANALYSE-ECHOS-LAUNCHER.md`)** :
  - **Registre versionné** (P1) : `echos/analysis/catalog.py` + `GET /api/metrics/catalog`
    — **61 fiches** en `CATALOG_VERSION 2.0.0` (identifiant, moteur, libellé français,
    unité, domaine, définition, formule, dénominateur, fenêtre, direction, statut,
    états de données, avertissement, forme visuelle, `renamedFrom`) + `HISTORY`
    (toute modification de formule ou d'unité augmente la version).
  - **Renommages P0/P1** (anciens noms conservés dans `renamedFrom` pour la migration
    des séries historiques) : `NetworkCentrality → SenderConcentration`,
    `InformationDiffusionSpeed → EmitterCoverageDelay`,
    `RumorAccuracyDegradation → TheoreticalHopDecay` (exploratoire),
    `CooperationPotential → GoalCategoryConcordance`, `DecisionDiversity →
    ActionDiversity` (+ `DecisionCount`), `IntentionStability → AverageGoalAge`,
    `IdentifiedLoops/LoopStrength/CriticalLoops/SystemStability/LoopTypes →
    RepeatedActionPairs/RepeatedActionShare/AmplifiedRepetitions/ActionDistributionBalance/
    RepeatedActionCounts`, `ActiveGroups/AverageGroupSize → InferredCommunities/
    AverageCommunitySize`, `GroupObjectiveSuccessRate → DissolvedGroupSuccessShare`,
    `MemberTurnoverRate → MemberExitsPerDissolution`, `ResourceToConsumptionRatio →
    ResourceFillRatio`, `CriticalityPoints → CriticalResourceCount`, `AverageCentrality →
    AverageOutDegree`, `CommunityStability → CommunitySizeMatch` ;
    `UnpredictabilityIndex` **retiré**.
  - **Corrections de formule** : `NetworkDensity` (dénominateur `n(n−1)/2` — le plafond
    à 0,5 disparaît), `SenderConcentration` (part du principal émetteur),
    `SystemComplexity` (moyenne de trois grandeurs normalisées, bornée [0,1] et non plus
    croissante avec la durée du run), `EmergenceScore` (composantes normalisées sur des
    bases défendables, contributions publiées, provenance par dépendances réelles),
    catégories normatives « positives/négatives » des boucles retirées.
  - **Statuts et états de données** : `measured` / `exploratory` / `suspended` et le
    vocabulaire `observed_zero`, `window_empty`, `insufficient_coverage`, `absent`,
    `unmeasured`, `censored` — distingués dans les fiches et affichés tels quels.
  - **Nouvelles mesures** : `BeliefDiversityNorm`, `GoalDiversityNorm`, `GoalCoverage`,
    `SenderCoverage`, `ResourceCoverage`, `ConsumptionPerTick`, `RecoveryEpisodes`,
    `UnresolvedCrisisCount`, `FormationCount`, `DissolutionCount`, `CommunityCoverage`.
  - **Phénomènes requalifiés** : identifiants stables, libellés/descriptions ramenés à
    ce qui est observé (comptage, répétitions, concordance, émissions) ; seuils restés
    hérités et signalés comme tels (absence de détection ≠ absence de phénomène).
  - **Provenance par tick** : `measured_by_tick` aligné sur `ticks` dans
    `GET /api/runs/{id}/metrics` (+ `missing_ticks`, `missing_ticks_count`,
    `latest_tick`) — trous publiés, jamais comblés ; `measured` (dernier tick) conservé
    pour compatibilité.

### Added
- **Fondements scientifiques et mathématiques de l'analyse** : `ANALYSIS_FOUNDATIONS.md` —
  documentation unique du **cœur analytique** (pas du composant) : outils partagés
  (entropie de Shannon, variance, rapports bornés), équations des 7 moteurs + composite
  (11 sections), reproductibilité (empreinte SHA-256, distances L2 normalisées), méthodes
  post-run (statistiques descriptives, pente par moindres carrés, viabilité) et analyse
  causale. Chaque équation suit la trame Nom · Explication globale · Expression
  mathématique · Pourquoi cette équation · Usage dans le projet (fichier:ligne) ·
  Interprétation. Aligné sur le code `echos/analysis/` et le catalogue 2.0.0 ; le code
  fait foi en cas d'écart. Renvois croisés ajoutés dans `METRICS_SPEC.md` (§1, §13) et
  `EMERGENCE_INDICATORS.md`.
- **Spécification de l'Indice de Viabilité Dynamique (DVI)** : `DYNAMIC_VIABILITY_INDEX.md`
  (**[DRAFT]**, non implémenté) — cadre mathématique unique pour estimer si une simulation
  est dans un **régime dynamique viable** (ni figé, ni chaotique). Deux couches : **moteur**
  (fenêtre ≤ 100 ticks, `ViabilityIndicators` à créer) et **post-run** (trajectoire OLS +
  marges aux seuils de crise, sans multiplicateur opaque du score). Sept composantes
  (activité optimale, diversité, structure, renouvellement, résilience, persistance,
  stabilité) agrégées par **moyenne géométrique pondérée** (aucune compensation entre
  composantes) avec couverture κ ; drapeaux STATIC/CHAOTIC ; constantes `[HÉRITÉ]`
  publiées, calibration sans ML en phase 3. Successeur conceptuel d'`EmergenceScore`
  (cohabitation, décision 1A) — `EmergenceScore` inchangé, le DVI ne le consomme pas.
  Renvois croisés ajoutés dans `README.md`, `EMERGENCE_INDICATORS.md` et
  `ANALYSIS_FOUNDATIONS.md` (table §1.1).
- **Journal d'événements publié (P3, manque B1)** : `GET /api/runs/{id}/events` —
  lecture du journal `events_log` **bornée** (`limit` défaut 500, plafond 2000,
  `total` donnant le réel), filtre `?type=`, comptage `types` déterministe, ordre
  `(tick, ordre d'émission)`. Sert les annotations de courbe de la fenêtre d'analyse :
  le marqueur signale *qu'un* événement existe à ce tick, jamais ce qu'il signifie
  (`API_REST.md` §3.19, tests bornes/filtre/404 dans `test_api_routes.py`).
- **Profil de viabilité et comparaison multi-runs (P1/P3)** : `GET /api/runs/{id}/viability`
  (populations initiale/finales/minimum, séries de besoins/réserves/décisions issues des
  résumés de tick, `completeness`, rapport post-run, chronologie d'extinction bornée à 50
  observations — sans cause racine inférée) et `GET /api/experiments/summary`
  (contexte de contrôle : version, graine, issue, conservation — + dispersion publiée
  `min`/`max`/`mean`/`spread` et dénominateurs, 2 à 12 runs, « un écart entre runs n'est
  pas un effet »).
- **Niveau de conservation persisté par run** : contexte `conservation` écrit au tick 0
  (`base` / `sampled_details` / `high_fidelity`) et publié avec les métadonnées du run :
  la fidélité réellement configurée est annoncée avant toute lecture.
- **Scénarios de référence (P2)** : `tests/test_reference_scenarios.py` — sept situations
  synthétiques connues (aucune communication, hub unique, diffusion répartie, couverture
  partielle, réseau complet/isolé, communauté stable en tailles mais changeante en
  identité, épuisement/récupération et crise censurée, routine vs cycle alterné) avec
  critères d'acceptation interprétables avant tout ajustement de seuil.
- **Documentation** : `REFERENCE_SCENARIOS.md` (baselines + exemples de rapports
  interprétés, dont cas non mesurés et résultats contradictoires),
  `METRICS_DICTIONARY.md` (dictionnaire des unités, conventions d'échelle, matrice
  métrique → API → vue), refonte de `METRICS_SPEC.md` (registre, statuts, **revue de
  changement de métrique**) et de `EMERGENCE_INDICATORS.md` (composantes normalisées,
  contributions), `API_REST.md` §3.15–§3.18 (catalogue, viabilité, synthèse, bornes de
  lecture).

### Added
- **Vue 2D et graphe de confiance : `GET /api/world` et `GET /api/trust-graph` (ADR-007)** :
  deux endpoints **lecture seule** qui alimentent la fenêtre d'analyse native du Launcher.
  - `world_initialized` n'est plus ignoré à l'ingestion : la description de monde (largeur,
    hauteur, cellules de terrain, obstacles, ressources initiales, régions) est persistée en
    contexte `world` au **tick 0**, une seule fois, pour le flux live comme pour l'archive
    `stream.jsonl` — elle ne peut pas être reconstituée après coup. Nouveau callback optionnel
    `on_world` de `aligned_ticks` : sans lui, le comportement historique est conservé.
  - Nouveau contexte `resources` (réserves du tick **avec position**), écrit à la cadence de
    `agents` : c'est la couche évolutive de la carte.
  - `/api/world?run_id=&tick=` renvoie `{world, agents, groups, resources, tick, world_tick}`
    au tick demandé (défaut : dernier observé), `tick: -1` en l'absence d'observation.
  - `/api/trust-graph?run_id=&tick=` assemble la **forme** du graphe : nœuds
    `{id, x, y, energy, hunger, thirst, action, group}` et arêtes
    `{source, target, weight}` où `weight` est le `trust` publié par l'entité — aucun
    agrégat, aucune moyenne (ADR-003).
  - Tests : persistance de `world_initialized`, cadence de `resources`, les deux endpoints
    (déterminisme, repli vide, 404), callback `on_world` — `flake8` sans avertissement,
    suite verte, couverture **93,5 %**.

### Removed
- **Interface web et shell Electron supprimés — ECHOS devient un moteur sans interface (ADR-007, 05/10/2026)** :
  - suppression de `echos-ui/` (React + Vite + TypeScript, 6 écrans) et de `echos-desktop/` (shell Electron, backend PyInstaller *onedir*, cibles `.deb`/NSIS, scripts de build) ;
  - suppression du montage statique de FastAPI : `create_app(store)` n'accepte plus `ui_dist`, `SpaStaticFiles` et `_mount_ui` disparaissent, `echos/server.py` n'expose plus `ECHOS_UI_DIST` ni `default_ui_dist()` — `/` publie la liste des endpoints et une route de navigateur répond 404 JSON (`test_no_interface_is_served`) ;
  - suppression du job CI `echos-ui` et du workflow `echos-desktop.yml`, du `setup-node` de `release.yml` et de `scripts/dev-stack-electron.sh` ; `scripts/dev-stack.sh` ne lance plus Vite ;
  - le présentatif est le **Launcher** : consoles de logs natifs par composant et fenêtre d'analyse (LiveCharts2) sondant l'API REST en 1 s — voir `../docs-launcher/adr/ADR-007-consoles-et-fenetre-analyse-natives.md`.
  - Tests : `test_server.py` réécrit (4 tests d'API seule) ; suite **329 passed, 2 skipped**, couverture **93,4 %**, `flake8` sans avertissement.

### Fixed
- **ECHOS ne démarre plus « Défaillant » depuis le Launcher** : `/health/ready` exige une base d'analyse interrogeable et répond 503 sans `ECHOS_ANALYTICS_DB` — c'est voulu, pour ne pas annoncer prêt un service dont les API d'analyse répondent 503. Mais le Launcher ne démarre ses composants qu'avec un environnement minimal (jeton de session, corrélation, racine d'installation) : cette variable devait être héritée du processus Launcher, et un lancement en binaire ne l'a jamais fournie. Le composant devenait donc `Défaillant` au bout du délai de démarrage, alors que le shell Electron, lui, appliquait déjà un défaut. L'adaptateur Linux applique désormais la même idée : à défaut d'une base imposée par l'opérateur, il crée et utilise `$LIVEX_DATA/echos/analytics.sqlite` (sinon `~/.livex-data/echos/analytics.sqlite`) — racine des données, donc partagée par toutes les instances et durable, là où `--work-dir` est propre à chaque instance et fragmenterait l'analyse. Une base par défaut inutilisable est signalée sur stderr et laisse la sonde publier son 503 explicite. Le mode manuel (`echos-serve`, `python -m echos.server`) garde son contrat inchangé.
- **Cache de séries jamais invalidé entre processus (retard croissant des graphiques)** : `AnalyticsStore.ingest_version` était un compteur **en mémoire du processus** — or l'API et l'ingestion sont deux processus séparés (shell bureau, ADR-003). Les écritures de l'ingestion n'incrémentaient donc jamais la version vue par l'API : les séries de `/api/runs/{id}/metrics` restaient figées sur leur premier chargement, et le tableau de bord affichait un « retard N ticks » croissant malgré des rafraîchissements normaux. Correctif : la version combine désormais `PRAGMA data_version` (réflète les écritures de **toutes** les connexions, y compris inter-processus) et le compteur local. Suite ECHOS verte (304+ tests, couverture 93 %).
- **Écrans d'analyse gelés pendant un run lancé depuis l'interface** : la liste des runs (`useLoadRuns`) n'était chargée qu'une seule fois au montage de l'app et le flux WebSocket SYNE ne transporte pas de `run_id` d'analyse — un run démarré après l'ouverture de la fenêtre n'apparaissait jamais dans le sélecteur, et tous les écrans alimentés par l'API (tableau de bord, graphes, phénomènes) restaient sur le run précédent pendant que les vues temps réel (2D, ticks) vivaient. Correctif : re-poll périodique de `/api/runs` (5 s, propriété d'`AppShell`) + après Start/Reset, l'écran de pilotage recharge la liste (boucle bornée, 10 × 500 ms) et sélectionne le `runId` renvoyé par le relais de contrôle. Le repli de première sélection prend le run **le plus avancé** (dernier tick max), pas le dernier de la liste (l'ordre de l'API n'est ni chronologique ni alphabétiquement significatif).
- **Tempête de requêtes `/metrics` pendant les runs longs** : les hooks d'analyse refetchaient le payload **complet** des séries à chaque tick WebSocket (10×/s) — les réponses s'empilaient plus vite qu'elles n'aboutissaient, l'affichage décrochait et le rendu se figeait. Correctif : cadence minimale de 2 s entre deux requêtes (une seule en vol, le surplus est sauté) ; le flux WS reste le détecteur d'activité.

### Added
- **Contrôles de rafraîchissement du tableau de bord** : bouton « Actualiser » (rechargement immédiat des métriques, groupes et phénomènes) et bascule « Live » (rafraîchissement automatique cadré) / « Figé » (gel manuel pour lire un instant précis).

### Added
- **Plan campagne-runs — axes A/B/C/D ECHOS réalisés (J-C1 → J-C4)** : réalisation du plan `docs/PLAN-CORRECTIFS-CAMPAGNE-RUNS.md` (décisions D1/D2/D3 du 29/09/2026).
  - **A1** : champ `seed` transporté (`WorldSnapshot.seed`, contrat SYNE 0.2.1 additif) — `consume()` enregistre le seed du snapshot et ne dérive depuis `run_id` qu'en **repli** (`_seed_of` étendu à `run-<seed>-<12hex>`) ; seed introuvable des deux côtés → `seed: ""` + avertissement d'ingestion, jamais une erreur fatale (observe-only).
  - **A2** : changement de `run_id` détecté en flux — run enregistré avant son premier tick (zéro FK error), fenêtres glissantes réinitialisées, série Parquet flushée, rapport de calibration bâti à la fin de **chaque** run (y compris interrompu par un reset).
  - **A3** : `outcome`/`extinctionTick` dans le rapport de calibration + `population_outcome()` en SQL (disponible pendant l'ingestion) + exposition `/api/runs` et `/api/runs/{id}`.
  - **B2** : bloc `viability` du rapport (`energySlopePerTick`, `actionSharesWhenHungry`, `resourceRegime` — colonnes `mean_food`/`mean_water`, **schéma v6**, migration additive) — `schemaVersion 2`.
  - **C1** : `/api/runs/{id}` et `/api/emergent-phenomena` lisent `observations_for(…, "phenomena")` au lieu de `contexts()` (fin des timeouts > 20 s / OOM).
  - **C2** : cadence `context_every` du contexte `agents` (défaut 20, `ECHOS_CONTEXT_EVERY`) + dernier tick toujours écrit ; empreinte de comparabilité documentée dans `EXPERIMENT_COMPARISON.md`.
  - **C3** : `/api/compare?light=1` — summary sans empreinte (`bit_identical`/`is_reproducible` = `null`) ; l'UI l'utilise par défaut.
  - **D3** : trous de ticks journalisés (`ConsumeResult.gaps_detected` + log structuré) — signalés, pas masqués.
  - **D4** : badge « éteint à tN » / « vivant » sur la liste des runs (`echos-ui`), consommant `outcome`.
  - Tests : suite **304 passed, 2 skipped** (dont 10 nouveaux : seed transporté/repli/absent, deux runs en une connexion, fenêtres réinitialisées, cadence `agents`, outcome API, light sans empreinte, extinction/viabilité/stabilité du rapport).
- **ECHOS ph10 — Shell de bureau Electron (ECHOS-100 → ECHOS-103, `ADR-003`)** :
  - **backend sert l'interface** : `create_app(store, ui_dist)` monte le build `echos-ui` à la racine via un `StaticFiles` à repli SPA (routeur React), routes `/api/*` prioritaires ; `echos/server.py` (`echos-serve`, script `pyproject`) lance uvicorn API + UI (env `ECHOS_UI_DIST`/`ECHOS_HOST`/`ECHOS_PORT`/`ECHOS_LOG_LEVEL`). Interface et API partagent la **même origine** → aucun CORS.
  - **paquet `echos/echos-desktop/`** : `electron/main.js` (port local libre, backend Python en processus enfant, attente de `/health`, arrêt du backend à la fermeture, `contextIsolation`/`sandbox`), `backend/echos-server.spec` (PyInstaller **onedir**, `pyarrow` collecté), `electron-builder.yml` (`.deb` Linux + NSIS `.exe` Windows, `extraResources` backend+UI), `scripts/build-backend.mjs` + `scripts/make-icon.mjs`.
  - **CI** `echos-desktop.yml` : matrice `ubuntu`/`windows`, build du paquet, smoke test `/health` du binaire packagé, artefacts attachés à la release sur tag `echos-v*`.
  - Tests : `tests/test_server.py` (8 tests, service statique/SPA/priorité API) ; suite ECHOS **291 passed, 2 skipped**, couverture **92,8 %**.
- Documentation technique V0.1 complète du composant (VISION, ARCHITECTURE, METRICS_SPEC, EMERGENCE_INDICATORS, CAUSAL_ANALYSIS, EXPERIMENT_COMPARISON, API_REST, LOGGING_INSTRUMENTATION, LIMITATIONS, TESTING, ROADMAP).
- ADR-001 (stack FastAPI + React/Vite web local pour V0.1 — **shell Electron conservé**, implémentation différée à un horizon ultérieur) et ADR-002 (mode de calcul causal hors ligne).
- ECHOS-1 : structure du monorepo — `echos/` (paquet Python `echos` : API FastAPI `create_app()`, `/health`, squelette des **7 moteurs de métriques** avec registre `known_engines()`, placeholder `ingestion`), `echos-ui/` (Vite + React + TypeScript : lint, build, tests vitest/jsdom), tests pytest (13 tests, couverture 100 %), CI `echos-python`/`echos-ui` activées.
- ECHOS-2 : contrats d'ingestion — modèles pydantic `WorldSnapshot` / `ExternalEvent` (JSON **camelCase**, API_CONTRACTS.md §2, alias ingress + sortie `by_alias`), `parse_message()` déterministe, client WebSocket `WsClient` (:5180, transport injectable), client de contrôle `ControlClient` (:5181 : start/pause/resume/reset), golden files versionnés (`fixtures/` + `golden/`), tests ingestion (41 tests, couverture 97 %).
- **ECHOS-010 (issue #367, milestone ph1)** : consommation du flux **alignée sur les ticks** — `TickSegment` (snapshot + événements d'un tick) et `aligned_ticks()` (refus déterministe des séquences désalignées) ; **modèles alignés sur l'émetteur SYNE V0.1** (agents sans `health` mais avec `species`/`fatigue`, `health` conservée pour compat doc) ; itération du client réel propres sur fermeture (`ConnectionClosed` → fin de flux) ; tests **serveur WebSocket réel in-process** rejouant le contrat V0.1 (goldens `*_v01`). Tests : 43 → **57**, couverture 98.7 %.
- **ECHOS-011 → ECHOS-013 (issues #368/#369/#370, milestone ph1)** : paquet `echos/storage/` — **agrégation incrémentale** `TickRecord.from_segment`/`summarize` (1 résumé par tick, **sans perte**), sous-échantillonnage `sample_every`/`downsample` (1 sur N, API_REST §4) ; **schéma SQLite d'analyse stable** `AnalyticsStore` (`SCHEMA_VERSION`, tables `runs`/`tick_summaries`/`events_log` **distinctes des tables SYNE**, FK activées) ; **séries lourdes Parquet** `agent_rows`/`write_agent_series`/`read_agent_series` (pyarrow, snappy) ; pipeline `consume()` boucle flux → SQLite + Parquet, **jointure SQLite ↔ Parquet cohérente** (`coherence_errors`). Dépendance ajoutée : `pyarrow>=20,<26`. Tests : 57 → **73**, couverture 98.7 %.
- **ECHOS ph2 — Moteurs de métriques (ECHOS-020 → ECHOS-027, issues #371 → #378, milestone ph2)** :
  - **Contrat enrichi SYNE U2 (additif)** : modèles d'ingestion — `Agent` accepte désormais `traits` (dict), `beliefs` (`subject`/`predicate`/`value`/`confidence`), `goals` (`kind`/`age`), `trust` (`peerId`/`trust`) et `memoryCount` (camelCase, optionnels) — rétro-compatibilité transport V0.1 (fixture `world_snapshot_u2.json`, roundtrip testé).
  - **7 moteurs pur·s déterministes** (`echos/analysis/`) : `CognitiveDiversityMetrics` (8 métriques), `InformationPropagationMetrics` (5, événements `message_sent`), `SocialComplexityMetrics` (7, graphe de confiance + **communautés par propagation d'étiquettes** — écart vs Louvain documenté dans `METRICS_SPEC.md` §4), `GoalConvergenceMetrics` (4), `FeedbackLoopDetector` (5, heuristique fenêtre **100 ticks / fréquence > 2**), `ResourceSustainabilityMetrics` (3, `resource_consumed` + `RecoveryTime` sur historique), `GroupDynamicsMetrics` (7, `group_formed`/`group_dissolved`).
  - **Contrat** : `compute(snapshot: dict) -> dict` (transport camelCase), **données manquantes → valeurs neutres 0.0**, clés inconnues ignorées, aucune mutation d'entrée, registre `known_engines()` inchangé.
  - **Preuve J2 (ECHOS-027)** : golden files versionnés `fixtures/snapshot_analysis.json` + `golden/analysis_golden.json` (tous les moteurs, valeurs vérifiées à la main) ; `test_j2_determinism.py` — **rejeu bit-à-bit de 2 runs** (séries de métriques strictement identiques) et dernier tick == golden ; tests par moteur à valeurs attendues calculées à la main. Tests : 73 → **115**, couverture **98,2 %**. flake8 + pytest `--cov-fail-under=80` verts.
- **ECHOS ph3 — Indicateurs d'émergence (ECHOS-030 → ECHOS-033, issues #379 → #382, milestone ph3)** :
  - **Moteur composite `EmergenceIndicators`** (`echos/analysis/emergence.py`, 8ᵉ moteur du registre `known_engines()` qui devient contractuel) : `compute(snapshot)` exécute les 6 moteurs entrants puis compose ; `compute_from_metrics(metrics)` miroir du `Calculate` du prototype. Fonctions pures/déterministes (aucun PRNG).
  - **ECHOS-030 Score composite [0,1]** : `EmergenceScore = BeliefDiversity×0.15 + GoalDiversity×0.15 + DiffusionSpeed_Norm×0.10 + ClusteringCoefficient×0.15 + LoopStrength×0.20 + (ActiveGroups/100)×0.25`, **clampé [0,1]** (entropies pouvant excéder 1), `DiffusionSpeed_Norm = clamp(1 − InformationDiffusionSpeed/100, 0, 1)` avec **neutralité étendue** (vitesse non mesurée → 0.0, convention ECHOS-006). Valeur de référence : 0.7585336.
  - **ECHOS-031 Phénomènes auto-détectés** : `DetectedPhenomena` — 5 phénomènes (CommunityFormation, FeedbackLoops, CollectiveCoordination, InformationBottleneck, OrganizationalDynamics, ordre stable) avec **trace des signaux déclencheurs** `[{metric, value, threshold}]` ; sur la fixture, seul InformationBottleneck (centralité 1.0 > 0.3).
  - **ECHOS-033 Complexité & imprévisibilité** : `SystemComplexity = (BeliefDiversity + GoalDiversity + InformationDiffusionSpeed)/3` (formule littérale, non bornée — incohérence d'échelle documentée) ; `UnpredictabilityIndex = LoopStrength × DecisionDiversity` (**décision [OUVERTE] résolue** : `DecisionVariability` n'existe dans aucun moteur, cf. EMERGENCE_INDICATORS.md §5).
  - **ECHOS-032 Règle d'or §4.10.3** : constante `DISCLAIMER` émise par le moteur (« jamais une preuve de l'existence d'une intelligence ou d'une société »), invariante et testée.
  - **Preuve J3** : `test_j3_determinism.py` — rejeu bit-à-bit des indicateurs, score borné sur [0,1] à chaque cadre, dernier cadre == golden ; golden `analysis_golden.json` étendu avec `EmergenceIndicators`. Tests : 120 → **146**, couverture **98,3 %**.
- **ECHOS ph4 — API REST (ECHOS-040 → ECHOS-045, issues #383 → #388, milestone ph4)** :
  - **Schéma SQLite v2** (`SCHEMA_VERSION = "2"`) : tables **`tick_metrics`** (métriques numériques des 8 moteurs, 1 ligne par métrique et par tick, PK `(run_id, tick, engine, metric)`) et **`tick_contexts`** (JSON par tick : `agents`, `groups`, `phenomena` — alimente croyances/relations/groupes/phénomènes) ; connexion **thread-safe** (`check_same_thread=False` + verrou partagé) — l'API FastAPI sert plusieurs requêtes de front.
  - **Métriques calculées à l'ingestion** : le pipeline `consume()` exécute `analysis.compute_all(snapshot)` (nouveau : **8 moteurs agrégés**, le composite réutilise les 6 moteurs entrants sans double calcul) et écrit `tick_metrics` + contextes — aucun recalcul à la lecture (API_REST.md §4).
  - **Endpoints REST** (`echos/api/routes.py`) : `GET /api/runs` (liste + bornes), `GET /api/runs/{id}` (métriques complètes + phénomènes), `GET /api/runs/{id}/metrics` (séries moteur×métrique + `latest`, filtres `engine`/`metric`, **`?every=N`** sous-échantillonnage index-based), `GET /api/runs/{id}/export` (**JSON ou CSV RFC 4180**, export **reproductible** — aucun horodatage, tri stable), `GET /api/beliefs/{agentId}` et `GET /api/relationships/{agentId}` (**lecture seule** — règle d'or §4.10.3), `GET /api/groups` et `GET /api/emergent-phenomena` (communautés + phénomènes via contexte), résolution du run par défaut (le plus récent).
  - **Cache de séries (ECHOS-044)** : `SeriesCache` LRU **borné** (256) et thread-safe, invalidé par `AnalyticsStore.ingest_version` (écritures) — séries longues servies sans mémoire explosive.
  - **Config API** : `create_app(store=None)` lit `ECHOS_ANALYTICS_DB` (sinon 503 sur les routes de données, contrat publié), `app.state.series_cache`, `/` annonce les 9 endpoints.
  - **Preuve J5 (ECHOS-045)** : `test_api_routes.py` (15 tests) + `test_sqlite_store.py`/`test_pipeline.py` étendus — couverture de la couche API ≥ 80 % (totale **98,2 %**), exports reproductibles testés. Tests : 146 → **167**, `SCHEMA_VERSION = "2"`.
- **ECHOS ph7 — Comparaison expérimentale (ECHOS-070 → ECHOS-072, jalon ph7, U7)** :
  - **`echos/analysis/reproducibility.py`** (ECHOS-070/071) — méta-métriques de reproductibilité,
    fonctions pures : `IsReproducible` = même seed ∩ même version moteur ∩ **empreinte SHA-256** du
    contenu canonique du run (séries de métriques, résumés de tick, événements, contextes
    `agents`/`groups`/`phenomena`, traces de décision — cellule `run_id` exclue, deux runs du même
    protocole ne diffèrent que par leur étiquette) ; `ReproducibilityScore` = `1.0` si reproductible,
    sinon `1.0 − (CognitiveDiff + SocialDiff)/2` ; `CognitiveDiff`/`SocialDiff` = normes **L2
    normalisées** (borne [0, 1]) entre distributions de croyances (`subject|predicate|value`,
    somme = 1 — comparable entre populations) resp. réseaux de confiance (poids de paire moyen, non
    orienté) au dernier contexte `agents` — clés triées, aucun PRNG ni dépendance temporelle
    (déterminisme ECHOS, METRICS_SPEC.md §10, EXPERIMENT_COMPARISON.md §4).
  - **`GET /api/compare`** (ECHOS-070) — `?run_a=&run_b=&format=json|csv` (API_REST.md §3.9) :
    métadonnées des deux runs, `same_seed`/`same_version`/`bit_identical`/`is_reproducible`,
    distances, `series` **alignées** ; `format=csv` export comparatif hors ligne (colonnes
    `tick,engine,metric,run_a_value,run_b_value,diff`, `diff = run_b_value − run_a_value`, tri
    `(tick, engine, metric)`) — reproductible ; erreurs 404 run inconnu / 400 format inconnu /
    422 params manquants ; `_ENDPOINTS` annonce désormais **12 endpoints**.
  - **Preuve J7** : `tests/test_compare.py` (**11 tests** — reproductibilité bit-à-bit, seed
    différente non reproductible, distance nulle/non-nulle sur croyances/confiance, stabilité
    **entre runs** (deux stores peuplés du même protocole ⇒ méta-métriques identiques, ECHOS-071) et
    **entre appels** (réponse déterministe), séries alignées JSON/CSV, 404/400/422, fonctions
    pures) ; fixture `snapshot_analysis.json` réutilisée pour peupler les runs. Tests : 197 → **208**,
    couverture **97,97 %** (flake8 vert).
- **ECHOS ph6 — Analyse causale (ECHOS-060 → ECHOS-063, issues #215 → #218, milestone ph6)** :
  - **`echos/analysis/causal.py`** (ECHOS-060/061) — reconstruction **hors ligne** (ADR-002 [Accepted]) : `build_chain(store, run_id, agent_id, tick=None, depth=7, max_depth=12)` produit la chaîne `Action ← Intention ← Objectif ← Besoin ← Croyance ← Mémoire ← Perception` depuis `decision_traces` + `events_log` (`decision_made` → intention, `message_received` → perception) + contexte `agents` (buts, croyances, mémoire) ; 1 nœud/couche (multiples agrégés dans `detail`, couche vide → `—`) ; besoins triés, croyances triées (déterminisme ECHOS-041).
  - **Boucles de rétroaction (ECHOS-062)** — récurrence de l'action aux ticks précédents (portée 16 dernières traces, `cycles[].ticks`) ; duplicat intra-chaîne arrêté au seuil du retour ; `depth` borné `[1, 12]` (défaut 7), `truncated` signalé.
  - **Cache (ECHOS-063)** — `echos/api/causal_cache.py::CausalCache` (LRU 256, thread-safe) invalidé sur `AnalyticsStore.ingest_version` : re-run ⇒ re-analyse, réponse **reproductible**.
  - **Endpoint** — `GET /api/runs/{run_id}/causal-chains/{agent_id}?tick=&depth=` (`API_REST.md` §3.8) : 404 entité sans trace / run inconnu, 422 `depth`, 503 sans store.
  - **Store** — `latest_decision_tick(run_id, agent_id)` et `context_before(run_id, context_type, tick)` (lecture déterministe, aucune écriture).
  - **Preuve J6** : `test_causal_analysis.py` (16 tests : chaîne complète/défauts, classement besoins, troncature, cycles, cache LRU + invalidation, endpoint 200/404/422/503, déterminisme deux-magasins). Tests : 181 → **197**, couverture **97,9 %**.
- **ECHOS ph5 — Logging & instrumentation (ECHOS-050 → ECHOS-052, issues #389 → #391, milestone ph5)** :
  - **Package `echos/instrumentation/`** (`LOGGING_INSTRUMENTATION.md` §1–§9) :
    - **ECHOS-050 Logging structuré** — `EchosLogger` : `structured-<run>.jsonl` (métriques par tick), `profilage-<run>.jsonl`, `decision-traces-<run>.jsonl`, logs texte quotidiens taggés `[SSE-V2]` ; sérialisation **déterministe** (`sort_keys=True`, compact — export reproductible) ; répertoire `ECHOS_LOG_DIR` (défaut `logs/`).
    - **ECHOS-051 Traces de décision** — `build_decision_trace` : fusion d'un `decision_made` SYNE avec le **contexte BDI observé** (action/utilité/`deliberated`/`interrupted`/cause/besoins/croyances/objectifs/mémoire, aucune écriture dans le monde) ; **table `decision_traces` (schéma v3)**, ingestion dans `consume()` (`ConsumeResult.decision_traces_written`), export API `GET /api/runs/{run_id}/decisions` (tri `(tick, agent_id)`).
    - **ECHOS-052 Profilage** — `profile.Markers` (`time.perf_counter`) autour de **chacun des 8 moteurs** via paramètre `profile` de `compute_all` (duck-typing, sortie inchangée) ; `compute_all_profiled` **bit-à-bit identique** à `compute_all` (déterminisme ECHOS-027) ; contexte `profiling` par tick ; budgets V0.1 en garde-fou CI (cibles de calibration, pas des sims réelles).
  - **Pipeline branché** : `consume(client, store, ..., logger=EchosLogger|None)` écrit 4 contextes par tick (`agents`, `groups`, `phenomena`, **`profiling`**) — `contexts_written == 12` pour 3 ticks.
  - **Preuve J5 étendue** : `test_instrumentation.py` (14 tests : JSONL déterministe + tag SSE-V2 + fusion BDI + profilage bit-à-bit/couverture 8 moteurs/format §5), `test_api_routes.py` (endpoint décisions reproductible + 404), `test_pipeline.py` étendu. Tests : 167 → **181**, `SCHEMA_VERSION = "3"`.
- **ECHOS ph8 — Interface web `echos-ui` (les 6 Écrans, UI_DESIGN.md A→F)** :
  - **Stack & config** : `react-router-dom` (routes A→F), `echarts` + `echarts-for-react` (timeline, graphe social), `lucide-react` (icônes), `zustand` (état runs/KPI live/run sélectionné/état WS) ; Vite proxy `/api` et `/health` → `http://127.0.0.1:5000` (dév.), export statique servi par FastAPI en prod ; code-splitting par écran + chunk vendor `echarts`/`react`.
  - **`src/api/`** — client REST typé sur les contrats `API_REST.md` §3 (runs, métriques `?every=`, export, décisions, chaînes causales, croyances/relations, groupes, phénomènes, `/compare`) ; 503/404 gérés ; `controlClient` = relais de pilotage ECHOS→SYNE :5181 (start/pause/resume/reset, **jamais en direct**).
  - **`src/ws/realtime.ts`** — client WebSocket :5180 (reconnexion, tick courant, état SYNE).
  - **Écran A `/dashboard`** : 4 KPICards (tabular-nums, delta ▲/▼, hint « score ≠ preuve ») + gauges 180° + timeline sous-échantillonnée + phénomènes auto-détectés.
  - **Écran B `/explore`** : onglets Entités / Groupes / Communications ; entités construites depuis `/groups` (pas d'endpoint « liste agents ») ; heatmap = **état « à venir »** (pas d'`/communication-heatmap`).
  - **Écran C `/social-graph`** : graphe force-directed depuis `/relationships/{agentId}` (sondage 2 s) + inspecteur latéral.
  - **Écran D `/analysis`** : onglets Métriques (8 moteurs) / Causale (`causal-chains/{agentId}`, profondeur ≤ 12, navigation par tick) / Comparaison (`/compare` + scores reproductibilité).
  - **Écran E `/control`** : pilotage relayé ECHOS→SYNE :5181, calibration, runs + export JSON/CSV, bandeau état WS.
  - **Écran F `/log`** : console 3 niveaux (flux WS) + panneau « limites de validité » (LIMITATIONS.md).
  - **Contraintes respectées** : vues **sans calcul de métrique** (pur affichage), pilotage = relais, pas de minimap. Qualité : eslint + `tsc -b` + vitest/RTL (13 tests, mock fetch + WS) verts.
- **Trace complète Parquet dans le batch** (`scripts/batch-analysis.py` + `echos/echos/reporting.py`) : le run batch écrit désormais `run_id.parquet` (trace d'analyse complète, schéma `_TRACE_SCHEMA` `section|run_id|tick|engine|metric|key|value|payload`, pyarrow) **et** le rapport `run_id.md` — fin du `.json` volatil ; la série agents reste `run_id.agents.parquet` (temp `.agents-<pid>.parquet` renommée ; `--parquet/--no-parquet`, env `ECHOS_PARQUET_PATH`). Rétrocompatible : `parquet=False` par défaut dans `write_reports`, CLI machine à part.
- **Planificateur d'analyse pour maîtriser la RAM** (`echos/echos/storage/pipeline.py` + `dev_ingest.py`) : `consume(..., analysis_every=1, parquet_flush_every=None)` — les métriques/contextes sont calculés **1 tick sur N** (déterministe, indiciel) pendant que l'ingestion fine (résumés, événements, traces de décision, série agents) reste **à chaque tick** ; écriture Parquet **bufferisée** (`_flush_agent_series`, remplace l'extension O(n²)) ; clôture forcée en fin de flux ; `engine_snapshot` construit seulement si cadence ou présence d'`decision_made`. Exposé au batch via `--analysis-every` / env `ECHOS_ANALYSIS_EVERY`, à l'entrée dev via `ECHOS_PARQUET_FLUSH_EVERY`.
- **Benchmark réel SYNE vs SYNE + ECHOS** (`scripts/benchmark.py`, `BENCHMARKS.md`) : grille entités × ticks × seeds (défauts 20/50/100 × 100/400/1000 × 3 seeds), scénarios `syne` (CLI `--headless`, moteur seul) et `echos` (SYNE `--serve` + API ECHOS + ingestion, pilotage HTTP :5181, **sans génération de rapport**) ; mesure **temps mur + pic RSS par processus** via `os.wait4`/`ru_maxrss` (aucune dépendance externe) ; sorties CSV brut + rapport Markdown (`benchmark-<horodatage>.csv/.md`) ; options `--ticks-per-second`, `--analysis-every`, `--parquet`, `--cell-timeout`, `--quick` ; **robustesse** : arrêt/ramassage des processus garanti dans un `finally` (aucun orphelin sur exception) et pré-vérification des ports `5000/5180/5181` avant chaque jeu (piles déjà actives refusées au démarrage).

- **Revue et refactor complets du composant** (`echos/ECHOS-REVIEW.md`, 584 lignes) : deux bugs critiques, 7 moteurs, l'analyse causale, l'API REST, la robustesse, les tests, puis l'interface `echos-ui` et les arbitrages des points ouverts. Détail et points à confirmer dans le rapport.
- **Provenance des métriques (`measured`, ECHOS)** : `analysis.provenance(snapshot)` calcule, par couple (moteur, métrique), si la valeur servie est une mesure ou le **repli neutre** du moteur faute de fenêtre. Persisté par tick (`tick_metrics.measured`, **schéma SQLite v5**, migration additive `DEFAULT 1`) et publié par `GET /api/runs/{id}/metrics` et `GET /api/runs/{id}` (API_REST §3.2-§3.3, METRICS_SPEC annexe). Sans ce flag, 7 métriques restaient à 0 sur tout run réel sans aucun signal pour l'interface.
- `dev_ingest` : `read_config()` + `ConfigurationError` — la configuration est lue et validée **avant** toute I/O, avec sortie 2 distincte de la sortie 1 d'échec d'ingestion et message nommant la variable fautive.
- Interface : sélecteur de run dans la barre supérieure, états d'erreur pour les groupes et les phénomènes, et exposition des entités isolées (agents hors communauté) dans l'onglet Entités.
- Tests : `test_dev_ingest.py` (configuration), `test_ingestion_models_v01.py` (normalisation des identifiants), et 8 fichiers de tests côté interface. Backend 233 → **283 tests** (couverture 93 %), interface 18 → **61 tests** sur 14 fichiers.

### Changed
- Divergence assumée vs prototype/Monographie : application **FastAPI** (pas Django), interface **React/TypeScript web local servie par FastAPI** (`echos-ui`, Vite — V0.1 ; **shell Electron conservé**, implémentation différée à un horizon ultérieur), analyse **Python** (pas C#/.NET), stockage **SQLite/Parquet**.
- `METRICS_SPEC.md` §4 : détection de communautés = **propagation d'étiquettes déterministe** (remplace la mention Louvain, non déterministe bit-à-bit ni disponible en stdlib Python pure) ; hygiène de déterminisme ECHOS préservée.

### Deprecated
- (aucun)

### Changed
- **Correction documentation (23/09/2026)** : le shell Electron **n'est pas abandonné** — il est **conservé**, son implémentation étant **différée à un horizon ultérieur (post-V0.1)**. Les mentions « PAS de shell Electron » sont reformulées dans `ADR-001`, `ARCHITECTURE.md` (ECHOS + racine), `FRONTEND_VISION.md`, `README.md`, `ROADMAP.md`, `ISSUES.md`, `CHANGELOG.md` (racine) et `TRANSPORT_API.md` (PRISM) ; l'interface V0.1 reste le web local React/Vite servi par FastAPI. La Monographie reste un snapshot figé (non modifié).

### Fixed
- **`/api/compare` renvoyait 500 sur tout run réel.** `_canonical_content` décompressait les événements avec six colonnes pour un `zip` de sept : la comparaison de reproductibilité — fonction centrale du module — était inaccessible sur tout run ingéré. Accès par nom de colonne via `AnalyticsStore.event_records()` / `events_by_type(...)` ; `compare()` valide désormais ses arguments hors HTTP.
- **`AnalyticsStore.runs()` plantait sur un run sans tick** (`int(None)`), rendant l'API entière inopérante sur une base fraîche ou un run en cours d'acquisition.
- **7 métriques à 0 sur tout run réel** : le snapshot ne portait que le tick courant, jamais `history` ni `communityHistory`. `_RollingContext` construit et borne désormais trois fenêtres glissantes transmises à chaque tick.
- **Dénominateur des taux de groupe dépendant de la charge** : borné en nombre d'événements seulement, il valait 1000 ticks sur un run calme et 20 sur un tick chargé — un facteur 50 sur la même métrique. La fenêtre est bornée en ticks et sa durée réellement observée est publiée dans le snapshot (`eventWindow`).
- **Communautés : les singletons n'étaient pas des communautés** — trois entités isolées en formaient trois, déclenchant faussement `CommunityFormation` et `CollectiveCoordination`. Une confiance à soi-même suffisait aussi.
- **`DecisionDiversity` calculée sur les objectifs** (redondante avec `GoalDiversity`, insensible aux décisions) ; **`SystemComplexity` non borné** (croissait avec la durée, rendant `SystemComplexity_Norm` négatif) ; **`_availability(100, None, 0)` renvoyait 1e11**, contaminant toute la moyenne du moteur de durabilité ; `GroupObjectiveSuccessRate` ignorait `success: false` ; liste des moteurs du composite hard-codée, divergente du registre.
- **Cycle d'import** entre `emergence` et le registre des moteurs : source unique dans `_common.py`, `analysis/__init__.py` n'est plus qu'une façade.
- **Identifiants de groupe non joints aux identifiants d'agent** : `groups[].members` et `leaderId` sont des entiers côté transport, `Agent.id` une chaîne — `"3" != 3`. Modèles typés `str` (`group_id` reste entier), normalisation appliquée aussi à `Territory.members` et `Book.author_id`/`readers`.
- **Parquet : réécriture O(n²)** — la série agents est regroupée en mémoire puis écrite en une passe.
- **Cadences nulles ou négatives** (`ZeroDivisionError` ou cadence silencieusement ignorée), **fuite de descripteurs** (`EchosLogger` sans `close()`), **reconnexion toutes les 100 ms** quand SYNE est arrêté, **fin de flux détectée par préfixe de libellé d'erreur** (nouveau type `StreamClosed`), **`pyarrow` importé mais non déclaré** dans `pyproject.toml` (un `pip install echos` produisait un paquet qui ne démarrait pas).
- **Interface — l'export « CSV » téléchargeait du JSON** : la route renvoie une enveloppe `{content_type, body}`, donc le fichier `.csv` produit était illisible par tout tableur. Le client normalise l'enveloppe et `/api/compare?format=csv` est exposé.
- **Interface — deux runs confondus** : `beliefs`, `relationships` et `phenomena` appelaient l'API sans `run_id`, donc le serveur résolvait « le run le plus récent » et non le run affiché, sans signal visible.
- **Interface — le cache de métriques mélangeait deux cadences** (la clé ignorait `every`), fusionnant des séries d'échantillonnages différents et produisant des `NaN` rendus comme des zéros. `TimelineChart` affiche désormais les trous en `null` avec leur décompte.
- **Interface — N+1 du graphe social** : une requête `/api/relationships` par membre, en série, à chaque tick. Requêtes menées par lots (6 en vol), une seule fois par tick réel.
- **Interface — une jauge alertait sur le bon comportement** : la vitesse de diffusion portait `warnAbove` et signalait en rouge un monde qui propageait bien. Séparation `warnBelow`/`warnAbove`, et **aucune alerte sur un repli neutre** (ni sur un `NaN`).
- **Interface — cycle de vie** : `disconnect()` se reconnectait 2 s plus tard via l'événement `close` ; `useLoadRuns()` était appelé par chaque écran (une requête par écran monté) ; `usePhenomena` avalait ses erreurs et affichait « aucun phénomène détecté » sur un échec réseau ; `useGroups` ne se rafraîchissait pas au tick.
- `npm test` lance désormais `vitest run` : le script par défaut (`vitest` interactif) ne termine jamais et accroche une CI.

## [0.0.0] — à venir

Version initiale (instrumentation prototype V1 : 18 tests xUnit, couverture 85 %, agrégation incrémentale validée — [HÉRITÉ]).

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 17 septembre 2026 | Création | Documentation V0.1 |
| 21 septembre 2026 | ECHOS-1 : structure code (echos + echos-ui) | Jalon U0 — socle ECHOS |
| 21 septembre 2026 | ECHOS-2 : contrats d'ingestion + golden files | Jalon U0 — ECHOS-003/004 |
| 21 septembre 2026 | ECHOS-010 : flux aligné par tick + serveur in-process | Jalon U1 — ECHOS-010 |
| 21 septembre 2026 | ECHOS-011 à 013 : stockage — agrégation, SQLite, Parquet (+ pipeline) | Jalon U1 — clôture ECHOS |
| 21 septembre 2026 | ECHOS-020 à 027 : 7 moteurs de métriques + golden files + preuve J2 | Jalon U2 — moteurs & déterminisme |
| 22 septembre 2026 | ECHOS-030 à 033 : indicateurs d'émergence (score composite, phénomènes, complexité/imprévisibilité) + preuve J3 | Jalon U3 — indicateurs d'émergence |
| 22 septembre 2026 | ECHOS-040 à 045 : API REST (runs, métriques/séries + `?every`, export JSON/CSV reproductible, croyances/relations, groupes, phénomènes, cache de séries) + preuve J5 | Jalon U4 — API REST |
| 23 septembre 2026 | Correction stack : shell Electron **conservé** (implémentation différée post-V0.1), « PAS de shell Electron » reformulé (ADR-001, ARCHITECTURE, FRONTEND_VISION, README, ROADMAP, ISSUES, docs racine, TRANSPORT_API PRISM) | Décision utilisateur — ECHOS garde Electron |
| 23 septembre 2026 | ECHOS ph8 : échos-ui — les 6 Écrans (A→F), client REST typé + WS, relais de pilotage, design system | Jalon U7 — interface web |
