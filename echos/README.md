# ECHOS

Observation et pilotage de SYNE : calculs d'analyse, ingestion et API REST.
**Moteur sans interface** (ADR-007) : ECHOS ne sert aucune page et ne porte aucun
shell de bureau ; la présentation — consoles de logs et fenêtre d'analyse — vit
chez le Launcher, qui consomme l'API locale.

**Statut** : [DRAFT] — jalon U4 (API REST). Documentation : `../docs/docs-echos/` (VISION, ARCHITECTURE, METRICS_SPEC, API_REST, TESTING).

## Structure

```
echos/
├── pyproject.toml            # paquet Python echos (API + analyse + ingestion)
├── requirements.txt          # dépendances runtime (dont httpx, websockets)
├── requirements-dev.txt      # dépendances test/lint
├── echos/
│   ├── __init__.py           # __version__
│   ├── api/app.py            # application FastAPI (create_app, /health)
│   ├── analysis/headless.py  # opérations d'analyse sans interface
│   ├── analysis/             # 7 moteurs de métriques (METRICS_SPEC §1)
│   └── ingestion/            # contrats + clients SYNE (API_CONTRACTS §2-3)
```

## API local (jalon U4 — API REST)

```bash
python -m venv .venv && .venv/bin/pip install -r requirements-dev.txt
ECHOS_ANALYTICS_DB=/chemin/analyse.db .venv/bin/uvicorn echos.api.app:app  # 127.0.0.1:5000
curl http://127.0.0.1:5000/health                    # {"status":"ok", ...}
curl http://127.0.0.1:5000/api/runs                   # runs enregistrés
curl "http://127.0.0.1:5000/api/runs/{id}/metrics?every=10"  # séries sous-échantillonnées
```

Sans `ECHOS_ANALYTICS_DB`, les routes de donnée répondent 503 (contrat publié).

## Analyse headless pour le Launcher (J2B)

Les opérations ci-dessous fonctionnent sans interface. Elles lisent
uniquement les runs enregistrés dans `ECHOS_ANALYTICS_DB` et ne modifient ni le
monde SYNE ni les fichiers d'entrée. Un `runPath` doit désigner un dossier
existant et son `runId` doit être présent dans la base ECHOS. Une expérience
est explicitement définie par `experiment.json` dans `experimentPath` :

```json
{"experimentId":"experiment-1","runIds":["run-7","run-8"]}
```

Tous les identifiants doivent être uniques et chaque run doit avoir au moins
un tick enregistré. Les réponses sont sans horodatage et ordonnées de façon
stable. Les champs `content` des fichiers retournés sont du base64 UTF-8 ;
aucun fichier n'est écrit par ces requêtes.

```bash
curl -X POST http://127.0.0.1:5000/analysis/run \
  -H 'Content-Type: application/json' \
  -d '{"experimentId":"experiment-1","runId":"run-7","runPath":"/runs/run-7"}'
curl -X POST http://127.0.0.1:5000/analysis/experiment \
  -H 'Content-Type: application/json' \
  -d '{"experimentId":"experiment-1","experimentPath":"/experiments/experiment-1"}'
curl -X POST http://127.0.0.1:5000/analysis/report \
  -H 'Content-Type: application/json' \
  -d '{"experimentId":"experiment-1","experimentPath":"/experiments/experiment-1"}'
```

`POST /analysis/run` retourne les rapports JSON et Markdown du run ;
`POST /analysis/experiment` retourne un fichier JSON d'agrégation contenant
les analyses des runs, les métriques numériques présentes (sans imputation) et
les phénomènes observés ; `POST /analysis/report` rend le rapport Markdown de
l'expérience. Les agrégats de métriques résument les dernières valeurs
enregistrées de chaque run et fournissent leur effectif, leur moyenne, minimum,
maximum et valeurs par run. Sans base configurée, ECHOS répond 503 ; un run
absent ou sans ticks répond respectivement 404 ou 409 ; un chemin, manifeste ou
identifiant invalide produit une erreur explicite 400/404/422. Ces opérations
ne nécessitent pas le jeton réservé à `/control/*`.

### Ingestion d'un run archivé

L'analyse ne calcule rien : elle lit des runs enregistrés. La base est donc
alimentée par le flux d'observabilité, soit en direct sur le WebSocket `:5180`,
soit — pour un run batch archivé — depuis le `stream.jsonl` écrit par SYNE dans
`<runPath>/data/stream.jsonl`.

```bash
curl -X POST http://127.0.0.1:5000/ingest/run \
  -H 'Content-Type: application/json' \
  -d '{"runId":"experiment-1-run-1","runPath":"/runs/EXP-1-RUN-0001"}'
```

L'archive est relue par le même pipeline que le flux live : mêmes métriques,
mêmes contextes, même rapport de calibration. `runId` est optionnel et sert de
**vérification** — l'identité inscrite dans le flux fait foi, et une divergence
est refusée avant toute écriture. L'ingestion est idempotente **par refus**
(`409`) : `events_log` n'a pas de clé d'idempotence, donc réécrire un run déjà
présent doublerait ses événements et produirait un rapport silencieusement faux.

L'intégrité de l'archive est contrôlée à trois niveaux : ticks de snapshot
strictement croissants, **exactement un** `tick_summary` par snapshot (un
résumé répété trahit un flux réécrit, et `consume` l'écraserait sans trace), puis
accord avec les ticks annoncés par le `result.json` voisin — seul ce dernier
contrôle détecte une troncature entre deux segments.

Une ingestion qui échoue en cours de parcours **purge ce qu'elle a écrit**. Le
pipeline valide chaque tick séparément, donc un flux invalidé à mi-parcours
laisserait sinon un run tronqué en base : à la fois partiel et protégé par la
garde anti-doublon, donc l'archive valide ne pourrait plus jamais être ingérée.

Comme le Launcher archive ce flux dans le paquet `.livexp`, la réanalyse d'une
campagne terminée ne dépend plus de SYNE : elle relit l'archive et rend les
**mêmes octets** que l'analyse faite pendant la campagne.

## Intégration au Launcher LIVEX

`component.json` déclare l'adaptateur Linux `echos-launcher`, qui utilise
`.venv/bin/python` lorsqu'il existe et sinon `python3`. Il accepte les arguments
communs du Launcher, mappe `--control-port` sur `ECHOS_PORT`, et lie le serveur
à `127.0.0.1`. `/health` mesure la vivacité HTTP ; la sonde déclarée
`/health/ready` ne réussit que si une base `ECHOS_ANALYTICS_DB` est configurée,
accessible et interrogeable avec son schéma attendu, afin de ne pas annoncer
comme prêt un service dont les API d'analyse répondent 503. L'arrêt propre
`POST /control/shutdown` exige
`Authorization: Bearer $LIVEX_SESSION_TOKEN` et demande la fermeture d'Uvicorn.

Le Launcher transmet le jeton par variable d'environnement et n'impose rien
d'autre : son environnement de départ est minimal. L'adaptateur applique donc une
base d'analyse par défaut — `$LIVEX_DATA/echos/analytics.sqlite`, sinon
`~/.livex-data/echos/analytics.sqlite` — et la crée au besoin. Elle est partagée
par toutes les instances et survit aux redémarrages, là où le répertoire de
travail est propre à chaque instance. Une base imposée par l'opérateur
(`ECHOS_ANALYTICS_DB`) reste prioritaire ; si la base par défaut est
inutilisable, l'adaptateur le signale sur sa sortie d'erreur et laisse la sonde
publier son 503 explicite plutôt que d'échouer en silence.

L'API `echos-serve` et `python -m echos.server` conservent leur interface
existante : sans `ECHOS_ANALYTICS_DB` elles seules continuent de répondre 503,
ce contrat est celui du mode manuel ; l'adaptateur n'accepte que les arguments
communs explicitement déclarés et refuse les autres.

## Tests

```bash
.venv/bin/flake8 echos && .venv/bin/pytest           # couverture exigée ≥ 80 %
```

## UI : chez le Launcher, pas ici (ADR-007)

ECHOS ne sert **aucune interface** : `/` publie la liste des endpoints et une
route de navigateur répond 404 (`test_no_interface_is_served`). Il n'y a ni
build Vite, ni shell Electron, ni instance Chromium.

Deux surfaces natives, dans le Launcher, consomment cette API :

- les **consoles de logs**, une fenêtre par composant supervisé, ouverte au
  démarrage du composant et à la demande depuis les cartes ;
- la **fenêtre d'analyse**, ouverte par un bouton, qui sonde l'API REST (1 s) et
  affiche séries, histogrammes, radar et graphe relationnel.

Voir `../docs/docs-launcher/adr/ADR-007-consoles-et-fenetre-analyse-natives.md`.

## Jalons

- **U0** : structure monorepo buildable/testable, API FastAPI + squelette des 8 moteurs, contrats d'ingestion (`WorldSnapshot`/`ExternalEvent` camelCase) + clients `WsClient` :5180 / `ControlClient` :5181 testés sur golden files. *(L'interface Vite/React prévue à l'U0 a été retirée en octobre 2026 — ADR-007 : la présentation vit dans le Launcher.)*
- **U1** : ingestion alignée par tick (`TickSegment`/`aligned_ticks`), agrégation incrémentale, stockage SQLite (`AnalyticsStore`, `SCHEMA_VERSION`) + Parquet, pipeline `consume()`.
- **U2** : les 7 moteurs de métriques (purs, déterministes, données absentes → neutres) + golden files + preuve J2.
- **U3** : indicateurs d'émergence (`EmergenceIndicators`, score composite, auto-détection des phénomènes, disclaimer §4.10.3) + preuve J3.
- **U4 (API REST)** : endpoints lecture seule (runs, métriques/séries `?every=N`, export JSON/CSV reproductible, croyances, relations, groupes, phénomènes), cache de séries validé, métriques calculées à l'ingestion (`compute_all`), preuve J5.
- **Ingestion batch (J2B)** : `POST /ingest/run` enregistre un `stream.jsonl` archivé par le pipeline du flux live — même contrat d'alignement, mêmes métriques. Le contrôle d'intégrité refuse un flux désordonné, un `tick_summary` manquant ou répété, et un `result.json` en désaccord. L'ingestion est idempotente par refus et **purge ses propres résidus** : un flux invalidé en cours de route ne laisse pas de run à moitié enregistré qui condamnerait la réingestion de l'archive valide. Le rapport exclut le contexte `profiling` (durées), seule source de non-déterminisme de la base.