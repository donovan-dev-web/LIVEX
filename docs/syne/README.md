# SYNE — Systems & Emergent Network Engine

[![Version: 0.17.0](https://img.shields.io/badge/Version-0.17.0-1f7f6f.svg)](CHANGELOG.md)
[![Statut: STABLE](https://img.shields.io/badge/Statut-STABLE-00d4a0.svg)](README.md)
[![Tests: 708](https://img.shields.io/badge/Tests-708-1f7f6f.svg)](TESTING.md)
[![Couverture: 81,19 %](https://img.shields.io/badge/Couverture-81%2C19%25-1f7f6f.svg)](TESTING.md)

**Composant** : SYNE
**Statut** : [STABLE]
**Version moteur** : 0.17.0 (contrat d'observabilité 0.5.0)
**Dernière mise à jour** : 4 novembre 2026
**Dépend de** : la documentation transversale (../)
**Source Monographie** : Partie 3, 7.2, 7.3.1

---

## Rôle actuel

SYNE reste un moteur .NET autonome qui exécute la simulation et la cognition.
Dans le mode historique V1, il détient l'ensemble du monde. Dans le couplage
PRISM V2, PRISM porte l'état physique et SYNE traite les décisions cognitives :
les commandes partent vers PRISM, puis son adaptateur transforme les
observations reçues en croyances SYNE. La boucle couplée et l'application de
ces observations sont implémentées et le transport a été exercé sur 1 000
ticks. L'extraction complète du monde historique de SYNE vers PRISM n'est pas
terminée ; les deux modes doivent donc rester distingués dans les descriptions
et les résultats.

Le répertoire séparé [`syne-mock`](../../syne-mock/) à la racine contient un
serveur Node.js destiné au développement et aux tests des clients V1 sans lancer
SYNE. Il simule une partie des contrats et flux utiles à cette intégration ;
ce n'est ni le moteur réel ni une référence d'équivalence algorithmique. Pour
les résultats et garanties du moteur, la source de vérité est l'implémentation
.NET de `syne` et les contrats décrits ici.

## Lancement

```console
dotnet run --project syne/Simulation.Console \
  -- --seed 12345 --max-ticks 2000 --config config.json
```

Flags principaux : `--headless`, `--world-size <w> <h>`, `--seed <s>`,
`--max-ticks <n>`, `--config <path>`, `--observe` et `--serve`. (Voir
`CONFIGURATION.md`.) L'observabilité WebSocket et le contrôle HTTP utilisent
par défaut les ports 5180 et 5181 sur `127.0.0.1`; les ports sont configurables
et ne doivent pas être supposés fixes.

## Dépendances

- .NET (C#) — bibliothèque `Simulation.Core` + exécutable `Simulation.Console`.
- SQLite (NuGet) pour la persistance V2, JSON en V1 (debug).
- Transports locaux : WebSocket d'observabilité (port par défaut 5180) et HTTP
  de contrôle (port par défaut 5181), configurables par options CLI.

## Interfaces

- `API_CONTRACTS.md` — snapshots globaux, événements WebSocket et contrôle HTTP.
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
| `TESTING.md` | Plan de tests (634 tests, couverture 87,4 %) |
| `ROADMAP.md` | Roadmap SYNE |
| `CHANGELOG.md` | Versions |
| `adr` | Décisions d'architecture |
