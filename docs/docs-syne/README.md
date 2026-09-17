# SYNE — Systems & Emergent Network Engine

**Composant** : SYNE
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : la documentation transversale (../)
**Source Monographie** : Partie 3, 7.2, 7.3.1

---

## Rôle

Cœur de simulation de LIVEX (C#/.NET). Il exécute le monde simulé, les entités BDI, la communication, la persistance et le **déterminisme bit-à-bit**. Il émet des données vers ECHOS (analyse) et PRISM (rendu) via les contrats de transport.

## Lancement seul

```console
dotnet run --project simulation-core/Simulation.Console \
  -- --seed 12345 --max-ticks 2000 --config config.json
```

Flags principaux : `--headless`, `--world-size <w> <h>`, `--seed <s>`, `--max-ticks <n>`, `--config <path>`. (Voir `CONFIGURATION.md`.)

## Dépendances

- .NET (C#) — bibliothèque `Simulation.Core` + exécutable `Simulation.Console`.
- SQLite (NuGet) pour la persistance V2, JSON en V1 (debug).
- Ports exposés : **5180** (WebSocket temps réel), **5181** (HTTP contrôle).

## Interfaces

- `API_CONTRACTS.md` — WorldSnapshot (WS) et ExternalEvent (WS) + contrôle HTTP.
- `COMMUNICATION.md` (../) — transport inter-composants.

## Documentation du composant

| Document | Rôle |
| :-- | :-- |
| `VISION.md` | Rôle, garanties, interdits |
| `ARCHITECTURE.md` | Couches internes et choix techno |
| `DATA_MODEL.md` | Modèle de données (entités, croyances, groupes, ressources) |
| `SIMULATION_LOOP.md` | Tick, ordre causal, scheduler, LOD |
| `COGNITIVE_ARCHITECTURE.md` | BDI, perception, mémoire, croyances, décision/utilité |
| `SYSTEMS_SPEC.md` | Systèmes transverses (groupes, conflits, livres, etc.) |
| `COMMUNICATION_PROTOCOL.md` | Communication inter-entités |
| `PERSISTENCE.md` | SQLite, sauvegarde/chargement, reprise |
| `DETERMINISM.md` | Garanties de reproductibilité |
| `CONFIGURATION.md` | Config, flags, paramétrages |
| `API_CONTRACTS.md` | Contrats de transport (WS/HTTP) |
| `PERFORMANCE.md` | Scalabilité, budget de tick, benchmarks |
| `TESTING.md` | Plan de tests (160+ tests, ≥ 80 %) |
| `ROADMAP.md` | Roadmap SYNE |
| `CHANGELOG.md` | Versions |
| `adr/` | Décisions d'architecture |