<div align="center">

<img src="docs/assets/banner.svg" alt="LIVEX — Living Intelligent Virtual Ecosystem eXperience : un laboratoire logiciel pour simuler, observer et étudier des mondes multi-agents" width="100%" />

<br />

<a href="docs/syne/README.md"><img alt="SYNE · simulation" src="https://img.shields.io/badge/SYNE-simulation-3fb8ea?style=plastic" /></a>
<a href="docs/echos/README.md"><img alt="ECHOS · observation" src="https://img.shields.io/badge/ECHOS-observation-a97bff?style=plastic" /></a>
<a href="docs/prism/README.md"><img alt="PRISM · monde physique V2 prévu" src="https://img.shields.io/badge/PRISM-monde%20physique%20V2%20pr%C3%A9vu-f0a846?style=plastic" /></a>

[![Version globale 1.0.0](https://img.shields.io/badge/LIVEX-1.0.0-315c9b?labelColor=090C16)](VERSIONING.md)
[![Licence GPLv3](https://img.shields.io/badge/Licence-GPLv3-216e5a?logo=gplv3&logoColor=BD0000&labelColor=090C16)](LICENSE)
[![Moteur SYNE 0.17.0](https://img.shields.io/badge/SYNE-0.17.0-3fb8ea?labelColor=090C16)](syne/component.json)
[![Contrat d’observabilité 0.5.0](https://img.shields.io/badge/Contrat-0.5.0-a97bff?labelColor=090C16)](docs/syne/API_CONTRACTS.md)

[![CI](https://github.com/donovan-dev-web/LIVEX/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/donovan-dev-web/LIVEX/actions/workflows/ci.yml)
[![Tests release: 1 566 réussis](https://img.shields.io/badge/Tests_release-1566%20réussis-orange)](docs/governance/LIVEX-STATUS.md#4-inventaire-des-tests)
[![Couverture de tests: 81,20 %](https://img.shields.io/badge/Couverture_de_tests-81%2C20%25-blue)](docs/governance/LIVEX-STATUS.md#couverture)

**[Site web](https://donovan-dev-web.github.io/LIVEX/)** &nbsp;·&nbsp; **[Le projet](#le-projet)** &nbsp;·&nbsp; **[Architecture](#architecture)** &nbsp;·&nbsp; **[Performances](#performances)** &nbsp;·&nbsp; **[Démarrage](#démarrage-rapide)** &nbsp;·&nbsp; **[Documentation](#documentation)** &nbsp;·&nbsp; **[Contribuer](#contribuer)**

</div>

<br />

<a id="le-projet"></a>
<img src="docs/assets/h-projet.svg" alt="Le projet" width="100%" />

**LIVEX explore une question : comment des comportements collectifs peuvent-ils émerger de règles locales simples, sans scénario qui les impose ?**

Dans LIVEX, « émergence » désigne d'abord des **motifs collectifs** — regroupements, échanges, spécialisations ou conflits — qui apparaissent à partir d'interactions locales. Le projet s'intéresse à cette émergence collective, dite faible dans son cadrage : les causes restent traçables aux règles explicites du modèle. Il n'étudie pas l'apprentissage ni l'amélioration individuelle comme objectif de recherche. Un état ou une maîtrise d'agent peut évoluer si une règle le prévoit ; cela ne constitue pas, à lui seul, l'émergence collective visée.

Le moteur fait évoluer des entités autonomes dans un monde régi par des règles explicites. Chaque entité perçoit une partie de son environnement, possède des besoins, des croyances et une mémoire, puis choisit ses actions selon une architecture cognitive BDI (*Beliefs, Desires, Intentions*). Le système observe les résultats sans les dicter.

> [!IMPORTANT]
> **Position scientifique.** LIVEX fournit un dispositif d’expérimentation, pas une preuve automatique d’intelligence ou d’émergence. Les résultats sont des observations à analyser, à lire avec leurs protocoles, leurs données et leurs limites. Dans la campagne documentée d’octobre 2026, le contraste de quête spatiale est confirmé ; le cycle social étudié est publié comme **non confirmé**. Voir le [rapport expérimental pérenne](docs/governance/realignment/EXPERIMENTAL-VALIDATION-2026-10.md).
>
> Les hypothèses, limites et choix du projet sont documentés dans la [monographie](LIVEX-Monographie_SnapV0-1.pdf) et les [décisions d’architecture](docs/syne/adr).

<div align="center">

<img src="docs/captures/launcher-home.png" alt="Accueil du Launcher LIVEX, l’orchestrateur de la pile logicielle" width="92%" />

<sub>Le Launcher LIVEX : orchestration des composants, journaux et analyse dans une interface native.</sub>

</div>

## Principes de conception

| Principe | Application dans LIVEX |
|:--|:--|
| **Déterminisme** | Une même graine, configuration et version du moteur permettent de reproduire une trajectoire. Toute altération volontaire (ex. la calibration de survie 0.13.0) impose un bump MINOR et le re-calage assumé des checksums dorés. |
| **Décisions explicables** | Les décisions BDI produisent des traces consultables et analysables. |
| **Information locale** | Les entités agissent à partir de ce qu’elles perçoivent et mémorisent, pas d’une vérité globale. |
| **Observation indépendante** | L’analyse mesure le monde sans en devenir la source de vérité. |
| **Contrats explicites** | Les composants échangent par des interfaces documentées et versionnées. |

<a id="architecture"></a>
<img src="docs/assets/h-architecture.svg" alt="Architecture" width="80%" />

LIVEX repose sur **trois composants séparés** — SYNE (cognition et décisions), PRISM (monde physique) et ECHOS (observation et analyse) — reliés par des contrats explicites. Après la release V1, la V2 prévoit une refonte complète de PRISM : il devient l’autorité du monde, tandis que SYNE conserve la cognition des agents. Le Launcher orchestre la pile et l’installateur la distribue. Cette présentation décrit la cible V2 prévue ; elle ne constitue pas un bilan de réalisation.

<div align="center">

<img src="docs/assets/architecture.svg" alt="Schéma historique de l’architecture LIVEX V1 ; voir la description textuelle pour la cible V2." width="100%" />

</div>

<sub>Schéma conservé pour l’architecture V1 ; il ne représente pas la refonte PRISM V2 décrite ci-dessous.</sub>

<details>
<summary>Version texte du schéma (Mermaid)</summary>

```mermaid
flowchart LR
    S[SYNE<br/>Cognition et décisions] <-->|Contrat 1.0<br/>commandes, observations, événements| P[PRISM<br/>Monde voxel autoritaire]
    P -->|Flux de lecture| I[Ingestion ECHOS]
    I --> D[(SQLite + Parquet)]
    D --> A[API ECHOS<br/>REST :5000]
    A --> U[Launcher<br/>consoles de logs + fenêtre d'analyse<br/>fenêtres natives Avalonia]
    INSTALL[Installateur LIVEX<br/>Windows / Linux] -->|installe et gère| U
    INSTALL --> S
    INSTALL --> D
    INSTALL --> P
    INSTALL -.->|distribution V2 prévue| V
```

</details>

### Les trois couches

| Composant | Rôle | Responsabilité |
|:--|:--|:--|
| **SYNE**<br/><sub>*Systems & Emergent Network Engine*</sub> | Cognition multi-agents | Porte besoins, croyances, mémoire, relations, groupes et décisions ; émet des commandes au monde et intègre observations et événements de PRISM. |
| **ECHOS**<br/><sub>*Emergent Cognition & Holistic Observation System*</sub> | Observation et analyse | Ingère les flux de SYNE et le flux de lecture PRISM, les stocke, calcule des métriques et expose les résultats par une API REST. |
| **PRISM**<br/><sub>*Persistent Reality & Interactive Simulation Module (V2)*</sub> | Monde physique autoritaire | Cible V2 : noyau .NET headless pour le monde voxel 2.5D, la matière, les actions, la navigation, le temps physique et les structures. Un visualiseur Silk.NET/OpenGL facultatif lit son flux sans piloter le tick. |

### Les outils

| Outil | Rôle | Responsabilité |
|:--|:--|:--|
| **Launcher** | Orchestration | Application de bureau Avalonia : orchestre l’environnement logiciel, supervise les composants et présente journaux et analyses dans une interface native. |
| **syne-mock** | Test d’intégration V1 | Outil Node.js conservé pour les contrats hérités ; la cible V2 prévoit des bots de scénario PRISM. |
| **Installateur LIVEX** | Distribution et maintenance | La distribution V2 prévoit des paquets du host PRISM et, séparément, du visualiseur facultatif, avec maintenance via l’installateur. |

### Communication entre les composants

Les applications communiquent par des interfaces réseau documentées, indépendantes de leurs langages et de leurs frameworks.

- **Contrat SYNE↔PRISM 1.0.0 (cible)** : commandes SYNE→PRISM, observations et événements PRISM→SYNE, et flux de lecture PRISM→ECHOS / visualiseur. Le tick reste verrouillé sur l’horloge de SYNE.
- **Plan de contrôle HTTP** : préparation et cycle de vie du run ; le plan de données de tick est indépendant du transport.
- **Contrats versionnés** : ils cadrent l’évolution des composants et la compatibilité des messages.

Cette séparation évite de coupler le moteur à une interface graphique ou à un système d’analyse particulier.

### Principes structurants

- **Séparation des responsabilités** : cognition, monde physique, analyse, visualisation et orchestration sont confiées à des composants distincts.
- **Indépendance technologique** : les composants se développent et se testent séparément, sans dépendre des implémentations internes des autres.
- **Responsabilités séparées** : PRISM est l’autorité de l’espace, de la matière et du temps physique ; SYNE est l’autorité de la cognition ; ECHOS observe les flux sans modifier le run.
- **Interfaces explicites et versionnées** : des contrats documentés facilitent l’intégration, les tests et l’évolution du système.

L’architecture et les contrats sont détaillés dans la [documentation technique](docs/ARCHITECTURE.md) et la [spécification des échanges inter-composants](docs/COMMUNICATION.md). Le [statut courant](docs/governance/LIVEX-STATUS.md) précise les versions, les tests validés pour la release et les portes qui restent ouvertes. Le badge de couverture décrit les rapports disponibles, sans présenter la mesure partielle comme la couverture globale complète.

<a id="garanties-preuves-et-limites"></a>
<img src="docs/assets/h-garanties.svg" alt="Garanties, preuves et limites" width="100%" />

| | |
|:--|:--|
| **Reproductibilité** | Une graine, une configuration et une version moteur fixées permettent de comparer et rejouer les trajectoires ; l’état du générateur pseudo-aléatoire est persisté pour les reprises. La garantie est testée, et ne signifie pas que deux configurations différentes produisent le même monde. |
| **Causalité et explicabilité** | Le pipeline BDI utilise les informations connues par l’agent ; les décisions peuvent porter cibles, preuves et candidats rejetés. Les effets différés ferment la fenêtre causale d’un tick. |
| **Mesures non intrusives** | ECHOS observe le flux et rend explicites les états mesurés, absents ou manquants ; l’analyse ne devient pas la source de vérité de la simulation. |
| **Validation documentée** | Les rapports versionnés de [gouvernance](docs/governance/realignment/README.md) conservent décisions, protocoles, résultats et limites du réalignement SYNE 0.15.0 → 0.17.0. Le [statut local courant](docs/governance/LIVEX-STATUS.md) distingue ces campagnes antérieures des tests réellement exécutés dans le checkout. |
| **Limites assumées** | L’émergence d’une société, d’une intelligence ou d’une conscience n’est pas garantie. Les campagnes sont bornées par leurs graines, horizons, scénarios et critères préenregistrés. La cible PRISM V2 prévoit une nouvelle autorité du monde et un viewer facultatif ; les résultats seront documentés avant publication. |

### Aperçu des interfaces

<div align="center">

| Launcher · expériences | Launcher · vue PRISM |
|:--:|:--:|
| <img src="docs/captures/launcher-experiences.png" alt="Vue des expériences dans le Launcher LIVEX" /> | <img src="docs/captures/launcher-prism.png" alt="Vue PRISM dans le Launcher LIVEX" /> |

| ECHOS · relations | ECHOS · agents |
|:--:|:--:|
| <img src="docs/captures/echos-relations.png" alt="Vue des relations observées dans ECHOS" /> | <img src="docs/captures/echos-agents.png" alt="Vue des agents observés dans ECHOS" /> |

</div>

<sub>Captures d’interface : elles présentent le Launcher et des vues de la génération V1. Elles ne représentent pas le viewer PRISM V2 prévu et ne constituent pas une preuve scientifique.</sub>

La capture dédiée de l'assistant d'installation sera ajoutée à `docs/captures/` après une capture réelle de la release 1.0.0 ; elle n'est pas encore disponible.

Les captures statiques de présentation sont conservées dans `docs/captures/` et inventoriées dans [`docs/captures/README.md`](docs/captures/README.md). Le dépôt n’embarque aucune animation GIF : si le projet en a besoin un jour, elle rejoint ce dossier, en WebP ou MP4 de préférence.

<a id="performances"></a>
<img src="docs/assets/h-performances.svg" alt="Performances" width="100%" />

Les chiffres ci-dessous proviennent d’un **relevé local V1** (une seule machine de bureau, octobre 2026) : ce sont des observations historiques, pas des engagements de performance pour PRISM V2. Les objectifs V2 figurent dans la [spécification PRISM](docs/prism/PERFORMANCE.md) ; les mesures V1 sont détaillées dans le [rapport de performance et de tests](docs/governance/LIVEX-PERFORMANCE-2026-10.md).

| Mesure | Cadence observée | Lecture |
|:--|--:|:--|
| SYNE seul, 100 agents (`--headless`) | ~67 t/s sur 1000 ticks | Dépend de l’horizon : ~129 t/s sur 300 ticks, ~2,9 t/s à 1000 agents |
| Ingestion ECHOS, 100 entités | ~16 t/s | Voie mono-thread ; l’analyse ajoute 0,5 à 2,7 s |
| Pile SYNE + ECHOS, 100 agents | **~13 t/s** | ECHOS (ingestion) est le facteur limitant, pas le moteur |
| Cadence PRISM visée, 100 agents | 6 t/s | ~2× de marge sur la pile ; SYNE consomme ~9 % de sa capacité |

| Configuration | Poste de travail | Charge type |
|:--|:--|:--|
| **Minimale** | 4 cœurs, 8 Go, SSD | SYNE 100 agents à 6 t/s + ECHOS, sans rendu Unreal |
| **Recommandée** | 6–8 cœurs, 16 Go, NVMe, GPU dédié | Pile complète SYNE + ECHOS + Launcher + PRISM à 6 t/s |

Un run long se dimensionne sur le débit **à l’horizon visé**, pas sur celui des premiers ticks ; alléger l’ingestion ECHOS (`analysisEvery` > 1 ou voie batch) est le premier levier pour tenir une cadence plus élevée. Justification et chiffres complets : [rapport de performance, § Recommandations](docs/governance/LIVEX-PERFORMANCE-2026-10.md#7-recommandations).

Les mesures sont reproductibles avec le harnais [`scripts/benchmark-windows.py`](scripts/benchmark-windows.py) (le [`scripts/benchmark.py`](scripts/benchmark.py) historique est réservé à Unix) ; la référence moteur publiée reste [`docs/syne/PERFORMANCE.md`](docs/syne/PERFORMANCE.md).

<a id="démarrage-rapide"></a>
<img src="docs/assets/h-demarrage.svg" alt="Télécharger et installer LIVEX" width="100%" />

## Installer LIVEX

LIVEX **1.0.0** est publiée sous le tag [`livex-v1.0.0`](https://github.com/donovan-dev-web/LIVEX/releases/tag/livex-v1.0.0). Téléchargez l’installateur correspondant à **Windows x64** ou **Linux x64** dans la [release publique](https://github.com/donovan-dev-web/LIVEX/releases/tag/livex-v1.0.0), puis suivez le [guide utilisateur](docs/installer/GUIDE-UTILISATEUR.md). Les variantes `core` et `full` sont disponibles pour les deux plateformes.

### livex-setup Captures
| Installateur · Accueil | Installateur · Choix Composant |
|:--:|:--:|
| <img src="docs/captures/Installer_home.png" alt="Vue de l'écran de lancement de l'installateur LIVEX" /> | <img src="docs/captures/Installer_Component.png" alt="Vue de l'écran de choix des composants installable de LIVEX" /> |


Le guide et les captures d’installateur décrivent la distribution V1. La cible V2 prévoit le host PRISM .NET et un viewer distinct facultatif ; son mode de distribution sera précisé avant publication. Consultez la [documentation de l’installateur](docs/installer/README.md) pour le périmètre V1.

Pour lancer les composants depuis les sources ou parcourir le mode démonstration de l’assistant, consultez le [guide de développement et d’installation locale](docs/INSTALLATION.md) et la [documentation du mode démo](docs/installer/README.md#5-bis-mode-démonstration-monde-fictif).

## Structure du dépôt

<details>
<summary>Afficher l’arborescence</summary>

```text
LIVEX/
├── syne/                   # Moteur de simulation C#/.NET
├── echos/
│   └── echos/              # API, analyse, ingestion et stockage (sans interface)
├── prism/
│   └── LDK/                # PRISM-LDK V1 + hôte technique historique
│       ├── LDK.uproject    # Hôte, pas le projet LIVEX complet
│       └── Plugins/PrismLdk/
├── syne-mock/              # Serveur Node.js de test des contrats SYNE
├── launcher/               # Orchestrateur Avalonia, paquets et tests du Launcher
├── docs/
│   ├── README.md           # Portail de la documentation
│   ├── ARCHITECTURE.md     # Vue d'ensemble technique
│   ├── COMMUNICATION.md    # Protocoles et ports inter-composants
│   ├── INSTALLATION.md     # Prérequis et commandes de lancement
│   ├── presentation/       # Vision, FAQ, glossaire, périmètre éthique
│   ├── governance/         # Feuille de route, état du dépôt, gitflow
│   ├── adr/                # Décisions d'architecture transverses
│   ├── syne/               # Documentation et décisions SYNE
│   ├── echos/              # Documentation ECHOS
│   ├── launcher/           # Spécification complète du Launcher
│   ├── prism/              # Contrats et feuille de route PRISM
│   └── installer/          # Installateur : code, tests et documentation
└── LIVEX-Monographie...    # Référence scientifique et technique
```

</details>

## Documentation

| Lire… | Pour… |
|:--|:--|
| [Site web](https://donovan-dev-web.github.io/LIVEX/) | Parcourir la présentation, le glossaire, la technique et le journal du projet. |
| [Vision](docs/presentation/VISION.md) | Comprendre les objectifs, le périmètre et les limites. |
| [Architecture](docs/ARCHITECTURE.md) | Voir les composants et leurs responsabilités. |
| [Installation & démarrage](docs/INSTALLATION.md) | Installer les dépendances et lancer la pile locale. |
| [Feuille de route](docs/governance/ROADMAP.md) | Suivre les jalons et les travaux à venir. |
| [Performances](docs/governance/LIVEX-PERFORMANCE-2026-10.md) | Connaître les débits, la capacité et la configuration d’exécution recommandée. |
| [Communication](docs/COMMUNICATION.md) | Consulter les protocoles et ports inter-composants. |
| [Glossaire](docs/presentation/GLOSSARY.md) | Retrouver le vocabulaire du projet. |
| [FAQ](docs/presentation/FAQ.md) | Obtenir des réponses aux questions fréquentes. |
| [Launcher LIVEX](launcher/README.md) · [spécification complète](docs/launcher/README.md) | Lire la release V1 et les évolutions prévues pour son orchestration de la pile V2. |
| [Installateur LIVEX](docs/installer/README.md) · [guide utilisateur](docs/installer/GUIDE-UTILISATEUR.md) | Télécharger, installer, mettre à jour, réparer et désinstaller LIVEX 1.0.0. |
| [Monographie](LIVEX-Monographie_SnapV0-1.pdf) | Lire les fondements, modèles et choix détaillés. |
| [SYNE](docs/syne/README.md) · [ECHOS](docs/echos/README.md) · [PRISM](docs/prism/README.md) | Entrer dans la documentation d’un composant. |
| [État courant et validations](docs/governance/LIVEX-STATUS.md) · [Documentation générale](docs/README.md) | Lire les résultats vérifiés localement, les portes ouvertes, les sources de vérité et les documents historiques. |

## Contribuer

Les contributions suivent les conventions du dépôt. Avant de proposer un changement, consultez le [guide de contribution](CONTRIBUTING.md), le [Gitflow](docs/governance/GITFLOW.md) et le [code de conduite](CODE_OF_CONDUCT.md). Une question ou un problème ? Le [canal d’assistance](SUPPORT.md) est là pour ça. Les choix techniques durables doivent rester cohérents avec l’[architecture](docs/ARCHITECTURE.md) et les ADR du composant concerné.

## Licence et auteur

Distribué sous licence [GNU GPL v3.0 ou ultérieure](LICENSE) (SPDX : `GPL-3.0-or-later`). Copyright (C) 2026 Donovan Chartrain. Le texte complet de la licence figure dans [`LICENSE`](LICENSE), la déclaration de copyright dans [`NOTICE`](NOTICE) ; les composants tiers et les fichiers non couverts par la GPL sont recensés dans [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md). Les éléments Unreal de PRISM V1 restent soumis aux conditions d'Epic Games ; la cible V2 .NET et ses dépendances seront documentées dans les notices avant distribution.

<img src="docs/assets/gplv3-or-later.png" alt="Logo GNU GPL v3.0 ou ultérieure" width="100px">

<br>

**Donovan Chartrain**, développeur et infographiste 3D · [Portfolio développement](https://donovan-dev-web.vercel.app) · [Portfolio 3D](https://donovan-dev-3d.vercel.app)

<br />

<div align="center">

<img src="docs/assets/footer.svg" alt="Construire les conditions. Observer les conséquences. Comprendre ce qui émerge." width="100%" />

Copyright (C) 2026 Donovan Chartrain.
</div>
