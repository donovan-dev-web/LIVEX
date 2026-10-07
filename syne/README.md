# SYNE — Simulation Engine

Moteur de simulation déterministe de LIVEX (1 tick = 1 minute simulée, monde 500×500).

| Projet | Rôle |
| :-- | :-- |
| `Simulation.Core` | Bibliothèque principale (configuration Annexe H, PRNG xoshiro256\*\*, monde + grille spatiale, entités + traits, boucle minimale) |
| `Simulation.Console` | Exécutable CLI et serveur de contrôle HTTP/WebSocket (`--serve`) |
| `Simulation.Core.Tests` | Tests unitaires xUnit (vecteurs PRNG & fabrique épinglés) |

## Commandes

```bash
dotnet run --project Simulation.Console -- --seed 12345 --max-ticks 1000
dotnet run --project Simulation.Console -- --serve --serve-port 5181 --observe-port 5180
dotnet test Syne.sln
```

Documentation : [`docs/docs-syne/`](../docs/docs-syne/) — contrat de configuration : [CONFIGURATION.md](../docs/docs-syne/CONFIGURATION.md).

## Intégration au Launcher LIVEX

`component.json` décrit l'installation Linux produite par `dotnet publish`
(SDK/runtime .NET 10 requis). Il expose le service supervisé sur `/health/ready`,
`/info` et `POST /control/shutdown`, ainsi que WebSocket d'observabilité. En
mode supervisé SYNE requiert `--instance-id`, `--control-port`, `--work-dir`,
`--log-dir`, `--correlation-id`, `LIVEX_CORRELATION_ID` et
`LIVEX_SESSION_TOKEN`. Le jeton de session protège l'arrêt et les commandes HTTP
mutantes. Un service sans `--autostart` reste actif jusqu'à un arrêt authentifié
ou Ctrl+C ; avec `--autostart`, il prépare puis exécute le batch et termine
après avoir écrit ses artefacts.

Le batch Launcher documenté accepte actuellement uniquement
`--simulation reference`. `--seed`, `--ticks`, `--export-dir` et `--autostart`
sont appliqués (avec validation de l'horizon strictement positif). En mode
supervisé, le dossier d'export par défaut est `--work-dir/data`; `result.json`
contient l'identifiant du scénario, seed, tick final, population finale et
checksum d'état. Le fichier n'inclut ni horodatage ni identifiant d'instance,
pour que son contenu reste reproductible à code/configuration/seed égaux.
`--export-dir` doit rester dans `--work-dir`.
Le WebSocket de flux n'est ouvert en batch que si `--observe-port` est fourni ;
le batch standard n'occupe donc pas de port d'observabilité partagé.

Publication locale de l'exécutable référencé par le manifeste :

```bash
dotnet publish Simulation.Console/Simulation.Console.csproj \
  --configuration Release \
  --output Simulation.Console/bin/Release/net10.0/publish
dotnet test Syne.sln
```

Limites explicites : le manifeste ne déclare que Linux ; seul le scénario
`reference` est pris en charge ; l'export est un résumé déterministe, pas une
archive complète de snapshots ; le mode batch ne fournit ni reprise ni
orchestration multi-simulation. `--serve` reste l'ancien mode de contrôle local
(`/api/control/*`) et n'est pas celui référencé par le manifeste. La conformité
du moteur scientifique n'est pas établie par la compatibilité de transport.