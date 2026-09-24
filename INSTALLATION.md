# Installation & démarrage

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 24 septembre 2026
**Dépend de** : `ROADMAP.md`, `COMMUNICATION.md`

Ce guide permet de démarrer localement **SYNE**, l’API et l’interface **ECHOS**, ainsi que l’ingestion des données de simulation.

> **PRISM** n’a pas encore de runtime. Sa réalisation est planifiée après U8 ; voir la [feuille de route PRISM](docs/docs-prism/ROADMAP.md).

## Prérequis

- **.NET SDK 10.0.4xx**, conformément à [`syne/global.json`](syne/global.json).
- **Python 3.11+**.
- **Node.js 20+** et npm.
- `curl` pour les vérifications de disponibilité du lanceur.

```bash
dotnet --version
python3 --version
node --version
npm --version
curl --version
```

## Démarrage complet : SYNE, ECHOS et interface

Depuis la racine du dépôt, lancez :

```bash
./scripts/dev-stack.sh
```

Le script prépare automatiquement l’environnement Python ECHOS et les dépendances npm s’ils manquent, compile SYNE, puis démarre dans le bon ordre :

1. l’API ECHOS sur le port `5000` ;
2. l’interface web sur le port `5173` ;
3. SYNE en mode serveur, avec contrôle HTTP sur `5181` et WebSocket sur `5180` ;
4. le consommateur ECHOS, connecté au WebSocket et en attente d’un run.

Ouvrez ensuite <http://127.0.0.1:5173>. L’API et sa documentation se trouvent sur <http://127.0.0.1:5000> et <http://127.0.0.1:5000/docs>. Les runs ingérés sont stockés dans `echos/data/livex-analytics.sqlite`.

Le script ne démarre **aucune simulation**. SYNE reste à l’état `Idle` jusqu’à ce que vous cliquiez sur `Start` dans l’interface. Pour arrêter toute la pile, utilisez `Ctrl+C` dans le terminal du script.

### Boutons de pilotage

L’interface relaie ses commandes à l’API ECHOS, qui contacte ensuite le serveur de contrôle SYNE sur le port `5181`. Le même processus SYNE expose aussi le WebSocket sur le port `5180` : dès que `Start` est envoyé, les snapshots et événements du run sont diffusés vers ECHOS pour analyse. Son état peut être vérifié avec :

```bash
curl http://127.0.0.1:5181/api/control/status
```

La seed est facultative dans le comportement conceptuel de l’UI : la valeur affichée par défaut est `12345`. La durée est également facultative. Si `maxTicks` n’est pas fourni, le run continue jusqu’à `Pause`, `Reset`, l’arrêt du processus SYNE ou `Ctrl+C`. Un `Start` ultérieur après `Reset` crée un nouveau run.

## Préparation manuelle (facultative)

Le lanceur s’occupe de ces étapes automatiquement. Pour préparer les dépendances à la main :

```bash
python3 -m venv echos/.venv
echos/.venv/bin/python -m pip install --upgrade pip
echos/.venv/bin/pip install -r echos/requirements-dev.txt

cd echos/echos-ui
npm ci
cd ../..

dotnet build syne/Syne.sln --configuration Release
```

## Démarrage manuel, service par service

Si vous ne souhaitez pas utiliser le lanceur, préparez d’abord l’environnement avec la section précédente. Ouvrez quatre terminaux depuis la racine du dépôt et démarrez les services dans cet ordre.

### Terminal 1 — API ECHOS

```bash
mkdir -p echos/data
export ECHOS_ANALYTICS_DB="$PWD/echos/data/livex-analytics.sqlite"
PYTHONPATH="$PWD/echos" echos/.venv/bin/python -m uvicorn echos.api.app:app \
  --app-dir echos --host 127.0.0.1 --port 5000
```

### Terminal 2 — Ingestion ECHOS

```bash
export ECHOS_ANALYTICS_DB="$PWD/echos/data/livex-analytics.sqlite"
PYTHONPATH="$PWD/echos" echos/.venv/bin/python -m echos.dev_ingest
```

Le consommateur attend le serveur WebSocket SYNE. Laissez ce terminal ouvert.

### Terminal 3 — Interface web

```bash
cd echos/echos-ui
npm run dev -- --host 127.0.0.1
```

### Terminal 4 — SYNE contrôle + observabilité

SYNE démarre en attente, sans lancer de simulation :

```bash
dotnet run --project syne/Simulation.Console --configuration Release -- --serve
```

Il écoute sur `127.0.0.1:5181` et `127.0.0.1:5180`. Le run est ensuite démarré depuis l’écran de pilotage ECHOS. Ne passez pas `maxTicks` dans la requête si vous souhaitez une durée illimitée ; utilisez `Pause`, `Resume`, `Stop` ou `Reset` depuis l’UI.

### Démarrer uniquement SYNE

Exécution batch sans API ni interface :

```bash
dotnet run --project syne/Simulation.Console --configuration Release -- \
  --seed 12345 --max-ticks 1000 --headless
```

## Ports utilisés

| Service | Adresse | Rôle |
|:--|:--|:--|
| SYNE WebSocket | `127.0.0.1:5180` | Snapshots et événements du run piloté par l’UI. |
| SYNE HTTP | `127.0.0.1:5181` | Contrôle du serveur SYNE et démarrage des runs. |
| API ECHOS | `127.0.0.1:5000` | Runs, métriques, analyses et relais de contrôle. |
| Interface ECHOS | `127.0.0.1:5173` | Application web Vite. |

## Données, arrêt et vérifications

Les fichiers analytiques locaux sont placés dans `echos/data/`. Pour repartir d’une base vierge, arrêtez les services et supprimez les données générées :

```bash
rm -f echos/data/livex-analytics.sqlite echos/data/livex-agents.parquet
```

Tests développeur :

```bash
# SYNE
cd syne && dotnet test Syne.sln --configuration Release

# ECHOS — depuis la racine
echos/.venv/bin/python -m pytest -q
echos/.venv/bin/python -m flake8 echos/echos

# Interface
cd echos/echos-ui
npm run lint
npm test -- --run
npm run build
```

## Documentation complémentaire

- [Architecture globale](ARCHITECTURE.md) · [Contrats inter-composants](COMMUNICATION.md)
- [Guide SYNE](docs/docs-syne/README.md) · [Guide ECHOS](docs/docs-echos/README.md)
- [Feuille de route](ROADMAP.md) · [Feuille de route PRISM](docs/docs-prism/ROADMAP.md)
- [Gitflow](GITFLOW.md) · [Contribuer](CONTRIBUTING.md)
