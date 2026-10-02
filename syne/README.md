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

SYNE n'a volontairement pas de `component.json` Launcher tant que les contrats
ne correspondent pas. Le mode `--serve` reste actif jusqu'à un arrêt de
processus et attend les commandes sous `/api/control/*` ; il n'exécute pas de run
et ne se termine pas automatiquement. Le Launcher actuel démarre un moteur avec
des arguments non reconnus par SYNE (`--headless`, `--control-port`,
`--instance-id`, chemins de travail/journaux et arguments de campagne
`--simulation`, `--ticks`, `--autostart`), attend une sonde HTTP et demande
l'arrêt par `POST /control/shutdown`. Accepter ces options sans exécuter leurs
promesses, ou traduire une campagne vers le serveur sans changer son cycle
scientifique, annoncerait une intégration fonctionnelle à tort. L'adaptation
attend donc une évolution coordonnée du contrat de run, de readiness et d'arrêt.