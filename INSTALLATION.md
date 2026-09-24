# Installation & démarrage

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 24 septembre 2026
**Dépend de** : `ROADMAP.md`, `COMMUNICATION.md`

Ce guide décrit l’installation locale de **SYNE**, de l’API et de l’interface **ECHOS**. Il inclut un exemple complet pour lancer une simulation observée et l’afficher dans le navigateur.

> **Périmètre actuel :** PRISM n’a pas encore de runtime. Son démarrage est planifié après U8 ; voir [`docs/docs-prism/ROADMAP.md`](docs/docs-prism/ROADMAP.md). La configuration ci-dessous lance SYNE et ECHOS avec son interface web.

## Prérequis

- **.NET SDK 10.0.4xx** : version demandée par [`syne/global.json`](syne/global.json).
- **Python 3.11 ou plus récent**.
- **Node.js 20 ou plus récent** et npm.
- Un navigateur récent et quatre terminaux pour l’exemple intégré.

Vérifiez les outils depuis la racine du dépôt :

```bash
dotnet --version
python3 --version
node --version
npm --version
```

## Préparer l’environnement

Depuis la racine du dépôt :

```bash
# Environnement Python ECHOS
python3 -m venv echos/.venv
echos/.venv/bin/python -m pip install --upgrade pip
echos/.venv/bin/pip install -r echos/requirements-dev.txt

# Dépendances de l’interface
cd echos/echos-ui
npm ci
cd ../..

# Vérifier et compiler SYNE
cd syne
dotnet restore Syne.sln
dotnet build Syne.sln --configuration Release
cd ..
```

Les dépendances Python sont isolées dans `echos/.venv`. Le fichier `echos/echos-ui/package-lock.json` verrouille les dépendances de l’interface.

## Démarrage complet : SYNE, ECHOS et interface

La pile de développement utilise **quatre terminaux** ouverts à la racine du dépôt. L’ordre est important : démarrez d’abord l’API et le consommateur ECHOS, puis l’interface, et enfin SYNE. Le consommateur doit être connecté avant que SYNE diffuse ses ticks.

Les données analytiques sont enregistrées dans `echos/data/livex-analytics.sqlite` et les séries d’agents dans `echos/data/livex-agents.parquet`.

### Terminal 1 — API ECHOS

```bash
mkdir -p echos/data
export ECHOS_ANALYTICS_DB="$PWD/echos/data/livex-analytics.sqlite"
PYTHONPATH=echos echos/.venv/bin/uvicorn echos.api.app:app \
  --app-dir echos --host 127.0.0.1 --port 5000
```

API : <http://127.0.0.1:5000> · documentation interactive : <http://127.0.0.1:5000/docs>.

### Terminal 2 — Ingestion du flux SYNE dans ECHOS

L’interface reçoit l’état temps réel directement par WebSocket. Ce consommateur alimente séparément la base historique utilisée par les métriques et les écrans d’analyse.

```bash
mkdir -p echos/data
export ECHOS_ANALYTICS_DB="$PWD/echos/data/livex-analytics.sqlite"
PYTHONPATH=echos echos/.venv/bin/python - <<'PY'
import time

from echos.ingestion import WsClient
from echos.storage import AnalyticsStore, consume

url = "ws://127.0.0.1:5180/"
database = "echos/data/livex-analytics.sqlite"
parquet = "echos/data/livex-agents.parquet"

# SYNE peut ne pas être démarré : réessayer jusqu’à l’ouverture du WebSocket.
while True:
    client = WsClient()
    try:
        client.connect(url)
        break
    except OSError:
        time.sleep(0.5)

with AnalyticsStore(database) as store:
    result = consume(client, store, parquet_path=parquet)
    print(f"Ingestion terminée : {result.ticks_written} ticks, "
          f"{result.events_written} événements")
PY
```

Le processus reste actif tant que SYNE diffuse. Quand le run se termine, ECHOS finalise le rapport de calibration. Arrêtez-le avec `Ctrl+C` si vous interrompez le run.

### Terminal 3 — Interface ECHOS

```bash
cd echos/echos-ui
npm run dev -- --host 127.0.0.1
```

Ouvrez <http://127.0.0.1:5173>. Vite relaie les requêtes REST vers l’API sur le port `5000`; l’interface se connecte au WebSocket SYNE sur le port `5180`.

### Terminal 4 — Simulation SYNE observée

```bash
dotnet run --project syne/Simulation.Console --configuration Release -- \
  --observe --seed 12345 --max-ticks 1000 --headless
```

SYNE publie ses snapshots et événements sur `ws://127.0.0.1:5180/`. La simulation observée est une exécution finie, bornée ici à 1 000 ticks. Les données historiques apparaissent dans ECHOS au fur et à mesure de leur ingestion.

### Contrôles et ports

| Service | Adresse | Utilisation |
|:--|:--|:--|
| SYNE — observation WebSocket | `127.0.0.1:5180` | Flux live consommé par l’interface et le worker ECHOS. |
| SYNE — contrôle HTTP | `127.0.0.1:5181` | Pilotage via `--serve` et l’écran de contrôle ECHOS. |
| ECHOS — API REST | `127.0.0.1:5000` | Runs, métriques, analyses et accès aux données. |
| ECHOS — interface Vite | `127.0.0.1:5173` | Interface web en développement. |

**Limite actuelle du mode de contrôle :** SYNE démarre en mode `--observe` ou en mode `--serve`. Ces deux modes ne sont pas réunis dans un même processus. L’écran de pilotage ECHOS peut joindre un serveur SYNE lancé séparément avec la commande ci-dessous, mais ce serveur est une instance distincte du run observé :

```bash
dotnet run --project syne/Simulation.Console --configuration Release -- --serve --serve-port 5181
```

Le serveur de contrôle attend ensuite les commandes HTTP de l’interface ; il ne diffuse pas le run `--observe` sur le WebSocket. Cette séparation doit être prise en compte lors des essais de pilotage.

## Lancer les composants séparément

### SYNE en mode batch

Pour exécuter le moteur sans WebSocket :

```bash
dotnet run --project syne/Simulation.Console --configuration Release -- \
  --seed 12345 --max-ticks 1000 --headless
```

Options utiles : `--config <fichier.json>`, `--world-size <largeur> <hauteur>`, `--seed <entier>`, `--max-ticks <nombre>`, `--headless`. Le mode observé ajoute `--observe` et `--observe-port <port>` (5180 par défaut).

### API ECHOS

```bash
export ECHOS_ANALYTICS_DB="$PWD/echos/data/livex-analytics.sqlite"
PYTHONPATH=echos echos/.venv/bin/uvicorn echos.api.app:app \
  --app-dir echos --host 127.0.0.1 --port 5000
```

Si la base n’existe pas encore, ECHOS initialise le schéma. Consultez les routes disponibles dans la documentation OpenAPI à `/docs`.

### Interface ECHOS

```bash
cd echos/echos-ui
npm run dev -- --host 127.0.0.1
```

Pour produire les fichiers optimisés :

```bash
npm run build
```

## Données locales et arrêt

Les fichiers `echos/data/livex-analytics.sqlite` et `echos/data/livex-agents.parquet` contiennent les données générées par l’exemple. Supprimez-les pour repartir d’un stockage analytique vierge :

```bash
rm -f echos/data/livex-analytics.sqlite echos/data/livex-agents.parquet
```

Arrêtez chaque processus dans son terminal avec `Ctrl+C`. Les bases d’analyse ECHOS et la persistance interne SYNE sont distinctes.

## Vérifications développeur

```bash
# SYNE
cd syne && dotnet test Syne.sln --configuration Release

# ECHOS — depuis la racine du dépôt
echos/.venv/bin/python -m pytest -q
echos/.venv/bin/python -m flake8 echos/echos

# Interface
cd echos/echos-ui
npm run lint
npm test -- --run
npm run build
```

## Documentation complémentaire

- [Architecture globale](ARCHITECTURE.md)
- [Contrats inter-composants](COMMUNICATION.md)
- [Guide SYNE](docs/docs-syne/README.md)
- [Guide ECHOS](docs/docs-echos/README.md)
- [Feuille de route PRISM](docs/docs-prism/ROADMAP.md)
- [Gitflow](GITFLOW.md) · [Contribuer](CONTRIBUTING.md)
