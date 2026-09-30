# LAUNCHER — Orchestrateur de la pile LIVEX

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Version cible** : 0.1.0
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `../docs-syne/`, `../docs-echos/`, `../docs-prism/`, `../../COMMUNICATION.md`, `../../VERSIONING.md`
**Source Monographie** : —

---

## Rôle prévu

Le LIVEX Launcher est la **couche d'orchestration** de LIVEX. Il permet de
piloter, de combiner, de surveiller et d'archiver les travaux conduits avec SYNE,
ECHOS et PRISM, depuis une interface unique et un format de données unique.

Il ne contient **ni la logique de simulation, ni la logique d'analyse, ni la
logique de rendu**. Ces responsabilités appartiennent respectivement à SYNE, ECHOS
et PRISM. Le Launcher ne fait que les coordonner.

SYNE est le moteur unique du projet. **ECHOS et PRISM sont deux modes
d'utilisation de ce moteur, de poids égal et sélectionnables** : ECHOS pour
l'analyse, la reproductibilité et la calibration ; PRISM pour la représentation
temps réel du monde simulé. Le Launcher permet de les activer séparément ou
ensemble, sans jamais introduire de hiérarchie entre eux.

Le Launcher est prévu comme un composant **de poids égal** à SYNE, ECHOS et PRISM
dans l'architecture de LIVEX : il possède son modèle de données, son format
d'échange et ses propres invariants. Il est **indépendant de tout composant** : sa
suppression ne doit rendre aucun autre composant inutilisable.

## Lancement prévu

```console
livex-launcher            # fenêtre native, ouverture de l'espace de travail
livex-launcher --check    # vérification de l'environnement, sans interface
livex-launcher --package <chemin.livexp>   # ouverture d'une campagne
```

La V0.1 cible Windows et Linux. Les détails d'installation et de premier
démarrage sont dans `PACKAGING.md`.

## Dépendances

- **.NET (C#)** — application native de bureau, architecture MVVM.
- **Avalonia** — rendu multi-plateforme, interface native et non web.
- **SYNE** — moteur, indispensable pour tout profil d'exécution.
- **ECHOS** — analyse, reproductibilité, calibration.
- **PRISM** — représentation temps réel (verrouillé tant qu'il n'est pas
  implémenté, voir `ROADMAP.md`).

Le Launcher ne dépend d'aucun autre composant pour fonctionner. Les composants
sont des participants, pas des dépendances.

## Interfaces

- `INTEGRATION_CONTRACT.md` — exigences d'intégration imposées aux composants.
- `PACKAGE_FORMAT.md` — format de paquet `.livexp`.
- `../../COMMUNICATION.md` — transport inter-composants.
- `../../VERSIONING.md` — versionnage et compatibilité.

## Documentation du composant

| Document | Rôle |
| :-- | :-- |
| `VISION.md` | Finalité, deux modes, principes, non-objectifs |
| `ARCHITECTURE.md` | Couches, frontières, découpage .NET, contrôle vs données |
| `COMPONENTS.md` | Modèle de composants, cycle de vie, états, **matrice des modes** |
| `INTEGRATION_CONTRACT.md` | Manifeste, CLI, codes de sortie, arrêt propre, endpoints, stubs |
| `PACKAGE_FORMAT.md` | Format de paquet `.livexp` — architecture complète |
| `DATA_FLOW.md` | Arborescence, flux de bout en bout, schémas, volumétrie |
| `EXPERIMENTS.md` | Campagnes, runs, planification, échecs, reprise |
| `OBSERVABILITY.md` | **Six niveaux L0 à L5**, états, agrégation, catalogue de métriques, santé, journaux, alertes |
| `NETWORK.md` | **Adaptateur de protocole**, plan contrôle/données, topologies T0-T3, registre, Node Agent, Gateway, sécurité |
| `USER_INTERFACE.md` | Design, mise en page, navigation par modes |
| `PACKAGING.md` | Installation, premier démarrage, détection |
| `TESTING.md` | Stratégie, niveaux, matrice de tests |
| `ROADMAP.md` | Phases, jalons, portes de passage |
| `ISSUES.md` | Points ouverts du Launcher |
| `CHANGELOG.md` | Journal des modifications |
| `adr/` | Décisions d'architecture du Launcher |

## Documents transverses

| Document | Rôle |
| :-- | :-- |
| `../../ARCHITECTURE.md` | Architecture globale, responsabilités des composants |
| `../../COMMUNICATION.md` | Transport inter-composants, ports, découverte |
| `../../VERSIONING.md` | Semver par composant, compatibilité |
| `../../ROADMAP.md` | Feuille de route du projet |
| `../../VISION.md` | Vision d'ensemble de LIVEX |
| `../adr/` | Décisions d'architecture transverses |
