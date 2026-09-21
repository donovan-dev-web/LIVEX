# SYNE — Simulation Engine

Moteur de simulation déterministe de LIVEX (1 tick = 1 minute simulée, monde 500×500).

| Projet | Rôle |
| :-- | :-- |
| `Simulation.Core` | Bibliothèque principale (configuration Annexe H, PRNG xoshiro256\*\*, monde/entités à venir) |
| `Simulation.Console` | Exécutable CLI (mode serveur WebSocket/HTTP à venir — ADR-002) |
| `Simulation.Core.Tests` | Tests unitaires xUnit (vecteurs PRNG épinglés) |

## Commandes

```bash
dotnet run --project Simulation.Console -- --seed 12345 --max-ticks 1000
dotnet test Syne.sln
```

Documentation : [`docs/docs-syne/`](../docs/docs-syne/) — contrat de configuration : [CONFIGURATION.md](../docs/docs-syne/CONFIGURATION.md).