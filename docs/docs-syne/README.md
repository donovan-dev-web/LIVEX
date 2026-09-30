# SYNE — Systems & Emergent Network Engine

[![Version: 0.13.0](https://img.shields.io/badge/Version-0.13.0-1f7f6f.svg)](CHANGELOG.md)
[![Statut: STABLE](https://img.shields.io/badge/Statut-STABLE-00d4a0.svg)](README.md)
[![Tests: 556](https://img.shields.io/badge/Tests-556-1f7f6f.svg)](TESTING.md)
[![Coverage: ≥80%](https://img.shields.io/badge/Coverage-%E2%89%A580%25-1f7f6f.svg)](TESTING.md)

**Composant** : SYNE
**Statut** : [STABLE]
**Version moteur** : 0.13.0 (contrat d'observabilité 0.2.1)
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : la documentation transversale (../)
**Source Monographie** : Partie 3, 7.2, 7.3.1

---

## Rôle actuel

SYNE est le **moteur .NET autoritaire** de simulation et de décision de LIVEX :
il crée et fait évoluer l'état du monde, exécute les comportements et décisions
des entités, et définit leurs trajectoires. Il est indépendant de tout moteur
graphique. PRISM est le projet Unreal final de LIVEX ; il intègre le plugin
PRISM-LDK (*LIVEX Development Kit*, module technique `PrismLdk`). Ce plugin
visualise et pilote le moteur au moyen de ses
contrats, mais ne remplace pas sa logique de simulation.

Le répertoire séparé [`syne-mock`](../../syne-mock/) à la racine contient un
serveur Node.js destiné au développement et aux tests du plugin sans lancer
SYNE. Il simule une partie des contrats et flux utiles à cette intégration ;
ce n'est ni le moteur réel ni une référence d'équivalence algorithmique. Pour
les résultats et garanties du moteur, la source de vérité est l'implémentation
.NET de `syne/` et les contrats décrits ici.

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
| `TESTING.md` | Plan de tests (556 tests, ≥ 80 %) |
| `ROADMAP.md` | Roadmap SYNE |
| `CHANGELOG.md` | Versions |
| `adr/` | Décisions d'architecture |
| `MIGRATION_MORPHOLOGY.md` | Guide de migration SYNE-130 vers des entités logiques |