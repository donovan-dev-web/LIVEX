# Plan de Documentation Technique — Projet LIVEX

> **Objet du document** : ce plan liste l'intégralité des documents à produire avant de démarrer la réalisation du projet LIVEX (V0.1 réelle, post-prototypes). Il ne contient pas le contenu final de chaque document, mais sa raison d'être, son contenu attendu, ses sources dans la Monographie, ses dépendances et sa priorité. Une fois validé, ce plan devient la checklist de constitution de la documentation.
>
> **Portée** : documentation générale du projet LIVEX + documentation complète et autonome de chacun des trois composants **SYNE** (moteur de simulation), **ECHOS** (observation/analyse) et **PRISM** (rendu/interaction), + gouvernance projet (CI/CD, versioning, Gitflow, Issues/Kanban/PR).

---

## Table des matières

1. [Principes directeurs](#1-principes-directeurs)
2. [Arborescence cible de la documentation](#2-arborescence-cible-de-la-documentation)
3. [Documentation générale — LIVEX](#3-documentation-générale--livex)
4. [Gouvernance projet : CI/CD, Versioning, Gitflow, Issues/Kanban/PR](#4-gouvernance-projet--cicd-versioning-gitflow-issueskanbanpr)
5. [Gabarit standard de documentation par composant](#5-gabarit-standard-de-documentation-par-composant)
6. [Documentation SYNE](#6-documentation-syne)
7. [Documentation ECHOS](#7-documentation-echos)
8. [Documentation PRISM](#8-documentation-prism)
9. [ADR — Architecture Decision Records](#9-adr--architecture-decision-records)
10. [Ordre de fabrication recommandé (roadmap documentaire)](#10-ordre-de-fabrication-recommandé-roadmap-documentaire)
11. [Conventions de nommage et de rédaction](#11-conventions-de-nommage-et-de-rédaction)
12. [Checklist finale avant passage en réalisation](#12-checklist-finale-avant-passage-en-réalisation)

---

## 1. Principes directeurs

- **Deux niveaux de lecture** : un niveau **projet** (vision d'ensemble, comment les 3 composants s'articulent, dans quel ordre avancer) et un niveau **produit** (chaque composant documenté comme s'il était livrable et compréhensible seul, sans connaître les deux autres en détail).
- **Une seule source de vérité par sujet** : ce qui est générique (vision, éthique, licence, contribution, CI/CD, Gitflow) vit à la racine `docs/`. Ce qui est spécifique à un composant vit dans `docs/components/<composant>/`. On ne duplique jamais un contenu ; on y renvoie par lien.
- **Traçabilité vers la Monographie** : chaque document ci-dessous indique la ou les parties de `LIVEX-Livre-Blanc-Unifie.md` à partir desquelles il doit être rédigé, pour ne rien perdre du travail déjà fait.
- **Statut de maturité explicite** : chaque document porte un tag `[STABLE]`, `[DRAFT]` ou `[OUVERT]` en en-tête (repris du système `[HÉRITÉ]` / `[OUVERT]` déjà utilisé dans la Monographie), pour distinguer ce qui est figé de ce qui reste à trancher avant codage.
- **Documentation avant code** : rien dans `SPEC.md` d'un composant ne doit rester flou au point de bloquer l'implémentation — c'est le critère de sortie de cette phase.

---

## 2. Arborescence cible de la documentation

```
livex/
├── README.md                          # Vitrine du repo, entrée unique
├── LICENSE
├── VISION.md
├── ARCHITECTURE.md
├── COMMUNICATION.md
├── ROADMAP.md
├── GLOSSARY.md
├── FAQ.md
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
├── SECURITY.md
├── CHANGELOG.md                       # Changelog "méta" (releases globales du monorepo)
├── VERSIONING.md
├── GITFLOW.md
├── CI_CD.md
│
├── docs/
│   ├── governance/
│   │   ├── ISSUES.md                  # Conventions Issues GitHub
│   │   ├── PULL_REQUESTS.md           # Conventions PR
│   │   └── KANBAN.md                  # Organisation du board projet
│   │
│   ├── adr/                           # ADR transverses (impactant SYNE+ECHOS+PRISM)
│   │   ├── 0000-template.md
│   │   └── NNNN-titre.md
│   │
│   └── components/
│       ├── syne/
│       │   ├── README.md
│       │   ├── VISION.md
│       │   ├── ARCHITECTURE.md
│       │   ├── DATA_MODEL.md
│       │   ├── SIMULATION_LOOP.md
│       │   ├── COGNITIVE_ARCHITECTURE.md
│       │   ├── SYSTEMS_SPEC.md
│       │   ├── COMMUNICATION_PROTOCOL.md
│       │   ├── PERSISTENCE.md
│       │   ├── DETERMINISM.md
│       │   ├── CONFIGURATION.md
│       │   ├── API_CONTRACTS.md
│       │   ├── PERFORMANCE.md
│       │   ├── TESTING.md
│       │   ├── ROADMAP.md
│       │   ├── CHANGELOG.md
│       │   └── adr/
│       │
│       ├── echos/
│       │   ├── README.md
│       │   ├── VISION.md
│       │   ├── ARCHITECTURE.md
│       │   ├── METRICS_SPEC.md
│       │   ├── EMERGENCE_INDICATORS.md
│       │   ├── CAUSAL_ANALYSIS.md
│       │   ├── EXPERIMENT_COMPARISON.md
│       │   ├── API_REST.md
│       │   ├── LOGGING_INSTRUMENTATION.md
│       │   ├── LIMITATIONS.md
│       │   ├── TESTING.md
│       │   ├── ROADMAP.md
│       │   ├── CHANGELOG.md
│       │   └── adr/
│       │
│       └── prism/
│           ├── README.md
│           ├── VISION.md
│           ├── ARCHITECTURE.md
│           ├── SCENE_SPEC.md
│           ├── TRANSPORT_API.md
│           ├── RENDERING_SPEC.md
│           ├── VISUALIZATION_SPEC.md
│           ├── UX_INTERACTION.md
│           ├── ASSETS_CONVENTIONS.md
│           ├── TESTING.md
│           ├── ROADMAP.md
│           ├── CHANGELOG.md
│           └── adr/
│
└── .github/
    ├── ISSUE_TEMPLATE/
    │   ├── bug_report.md
    │   ├── feature_request.md
    │   ├── documentation.md
    │   └── adr_proposal.md
    ├── PULL_REQUEST_TEMPLATE.md
    └── workflows/
        ├── ci.yml
        └── release.yml
```

---

## 3. Documentation générale — LIVEX

Ces documents forment le **point d'entrée** du projet : quiconque (toi dans 6 mois, un contributeur, un recruteur technique) doit pouvoir comprendre LIVEX dans son ensemble et savoir où aller chercher le détail.

| # | Document | Contenu attendu | Source Monographie | Priorité |
|---|----------|------------------|---------------------|----------|
| 1 | `README.md` | Présentation courte, pitch, schéma des 3 modules, liens vers `VISION.md`/`ARCHITECTURE.md`/`ROADMAP.md`, statut du projet, stack, comment lancer le monorepo | Partie 2.1, 2.10 | 🔴 Critique |
| 2 | `VISION.md` | Problématique, principe d'émergence, philosophie, ce que LIVEX est / n'est pas, positionnement scientifique | Partie 1 (1.1 à 1.7), Partie 8.1 | 🔴 Critique |
| 3 | `ARCHITECTURE.md` | Architecture globale (3 modules), principe d'indépendance, contrats de transport, diagramme de flux haut niveau, matrice de dépendances, stack technologique complet, organisation du monorepo | Partie 2.2–2.3, Partie 7.1–7.3 | 🔴 Critique |
| 4 | `COMMUNICATION.md` | Comment SYNE / ECHOS / PRISM communiquent entre eux : WebSocket (données), HTTP (contrôle), formats de sérialisation, versionnement des contrats, garanties de compatibilité | Partie 2.2.3, Partie 7.3.2 | 🔴 Critique |
| 5 | `ROADMAP.md` | Roadmap **globale, par phase** (non détaillée) : ordre de construction SYNE → ECHOS → PRISM, jalons V0.1 → V1 → V2, dépendances inter-phases | Partie 9.6.2, 9.3.2, 7.9 | 🔴 Critique |
| 6 | `GLOSSARY.md` | Glossaire unifié (termes informatiques, simulation, visualisation, sigles) | Annexe E complète | 🟠 Important |
| 7 | `FAQ.md` | Questions ouvertes scientifiques/techniques/philosophiques reformulées en FAQ, limites connues, ce que le projet ne cherche pas à prouver | Partie 8.7, 8.2 | 🟡 Utile |
| 8 | `CONTRIBUTING.md` | Process de contribution (même si solo pour l'instant : convention pour "futur toi" ou contributeurs), style de code, exigences de tests/doc avant PR, référence à `GITFLOW.md` | — (à créer) | 🟠 Important |
| 9 | `CODE_OF_CONDUCT.md` | Charte minimale si le repo devient public | — (standard) | 🟢 Optionnel |
| 10 | `SECURITY.md` | Politique de signalement de faille, périmètre (projet de recherche, pas de données sensibles a priori) | — (à créer) | 🟢 Optionnel |
| 11 | `LICENSE` | Choix de licence (MIT/Apache-2.0/GPL...), à trancher avant publication | — (décision à prendre) | 🟠 Important |
| 12 | `CHANGELOG.md` (racine) | Changelog **méta** : releases du monorepo qui touchent plusieurs composants à la fois | — (à créer) | 🟡 Utile |
| 13 | `VERSIONING.md` | Voir section 4.2 | Partie 3.23.6, 3.26.4 | 🔴 Critique |
| 14 | `GITFLOW.md` | Voir section 4.3 | — (à créer) | 🔴 Critique |
| 15 | `CI_CD.md` | Voir section 4.1 | Partie 7.6 | 🔴 Critique |

**Note sur l'éthique et la portée** : le contenu de la Partie 8 (Portée, Risques, Éthique) mérite un document dédié si le projet doit être présenté publiquement (`docs/ETHICS_AND_SCOPE.md`), sinon il peut rester condensé dans `VISION.md` + `FAQ.md`. À trancher selon si tu comptes publier/pitcher LIVEX à l'extérieur.

---

## 4. Gouvernance projet : CI/CD, Versioning, Gitflow, Issues/Kanban/PR

### 4.1 `CI_CD.md`

| Élément | Contenu |
|---|---|
| Pipeline CI (`ci.yml`) | Étapes par composant (build/test/lint SYNE en .NET, ECHOS, PRISM en Godot/TS), matrice de jobs, couverture de test minimale |
| Pipeline Release (`release.yml`) | Déclenchement (tag, branche `release/*`), génération de changelog, publication d'artefacts, Docker |
| Docker | Images par composant, docker-compose pour lancer SYNE+ECHOS+PRISM ensemble en local |
| Environnements | local / staging (si pertinent) |
| Source Monographie | Partie 7.6 (pipeline CI existant du prototype, à faire évoluer) |

### 4.2 `VERSIONING.md`

- Stratégie de versionnement (SemVer recommandé : `MAJOR.MINOR.PATCH`) appliquée **indépendamment à chaque composant** (SYNE, ECHOS, PRISM ont chacun leur numéro de version) + une version "LIVEX" globale qui fige un triplet compatible (ex. `LIVEX v0.1.0 = SYNE 0.3.0 + ECHOS 0.1.0 + PRISM 0.2.0`).
- Règles de compatibilité des contrats de transport entre versions (référence à `COMMUNICATION.md`).
- Politique de versionnement des schémas de sérialisation (cf. Partie 3.23.6 gestion des versions de sauvegarde SQLite).
- Étiquetage Git (tags `syne-vX.Y.Z`, `echos-vX.Y.Z`, `prism-vX.Y.Z`, `livex-vX.Y.Z`).

### 4.3 `GITFLOW.md`

- Modèle de branches recommandé pour un monorepo à 3 composants semi-indépendants :
  - `main` : toujours stable/déployable
  - `develop` : intégration continue
  - `feature/<composant>-<sujet>` (ex. `feature/syne-perception-staggered`)
  - `release/<composant>-vX.Y.Z`
  - `hotfix/<composant>-<sujet>`
- Convention de nommage de commit (Conventional Commits recommandé : `feat(syne): ...`, `fix(prism): ...`, `docs(echos): ...`).
- Règles de merge (squash/rebase), protection de branches, revue obligatoire.

### 4.4 `docs/governance/ISSUES.md`

- Labels standards : `component:syne`, `component:echos`, `component:prism`, `component:docs`, `type:bug`, `type:feature`, `type:research`, `type:adr`, `priority:P0..P3`, `status:blocked`.
- Templates GitHub Issue (`.github/ISSUE_TEMPLATE/`) : bug report, feature request, documentation, proposition d'ADR.
- Règle de rattachement systématique d'une issue à un composant + une phase de roadmap.

### 4.5 `docs/governance/PULL_REQUESTS.md`

- Template de PR (`.github/PULL_REQUEST_TEMPLATE.md`) : lien vers l'issue, composant(s) touché(s), checklist (tests, doc mise à jour, changelog mis à jour, déterminisme non cassé pour SYNE).
- Règles de revue (au minimum auto-revue documentée si solo dev, sinon 1 reviewer).

### 4.6 `docs/governance/KANBAN.md`

- Colonnes recommandées : `Backlog` → `À faire (sprint/phase courante)` → `En cours` → `Revue/Test` → `Fait`.
- Une vue Kanban filtrable par `component:*` en plus de la vue globale.
- Correspondance entre les colonnes et les phases de `ROADMAP.md` (un board = une phase, ou un board unique avec swimlanes par composant — à trancher selon ta préférence d'usage GitHub Projects).

---

## 5. Gabarit standard de documentation par composant

Chaque composant (SYNE, ECHOS, PRISM) est traité **comme un produit autonome**. Le gabarit ci-dessous est appliqué aux trois ; les sections 6/7/8 précisent uniquement ce qui est **spécifique** à chacun.

| Document type | Rôle |
|---|---|
| `README.md` | Vitrine du composant : à quoi il sert, comment le lancer seul, dépendances, lien vers les autres docs du composant |
| `VISION.md` | Rôle fondamental du composant, ce qu'il doit garantir, ce qu'il **ne doit pas** faire (frontières explicites) |
| `ARCHITECTURE.md` | Architecture logique interne (couches, modules, diagramme), choix technologiques et pourquoi |
| **Spec technique** (1 ou plusieurs fichiers selon densité — voir sections 6/7/8) | Description exacte et exhaustive du comportement attendu : modèles de données, algorithmes, formats |
| `API_CONTRACTS.md` / `API_REST.md` / `TRANSPORT_API.md` | Contrats d'interface exposés aux autres composants (entrée/sortie, schémas, endpoints) |
| `TESTING.md` | Stratégie de test (unitaire, intégration, reproductibilité), critères de couverture, tests de non-régression spécifiques (ex. déterminisme pour SYNE) |
| `ROADMAP.md` | Roadmap **du composant seul**, par phase, alignée sur la roadmap globale mais détaillée à son échelle |
| `CHANGELOG.md` | Historique des versions du composant (format Keep a Changelog recommandé) |
| `adr/` | Décisions d'architecture propres au composant (voir section 9) |

---

## 6. Documentation SYNE

*Systems & Emergent Network Engine — le cœur de simulation (C#/.NET). C'est le composant le plus dense : il justifie plusieurs fichiers de spécification séparés plutôt qu'un `SPEC.md` monolithique.*

| Document | Contenu | Source Monographie |
|---|---|---|
| `README.md` | Lancer SYNE seul (console), dépendances .NET, ports exposés (5180 WS / 5181 HTTP) | Partie 7.2, 7.3.1 |
| `VISION.md` | Rôle fondamental, garanties (déterminisme, autonomie des entités...), interdits explicites | Partie 3.1 |
| `ARCHITECTURE.md` | Couches de SYNE, systèmes internes, modèle de données haut niveau, diagramme | Partie 3.2 |
| `DATA_MODEL.md` | Structure de l'entité (V1/V2), traits de personnalité, cycle de vie, structures de croyance/mémoire/besoin/objectif/relation/groupe/ressource | Partie 3.7, 3.10–3.13, 3.17, 3.19–3.20 |
| `SIMULATION_LOOP.md` | Définition du tick, ordre causal, scheduler adaptatif, LOD, fréquences, monde simulé, temps simulé, seed/PRNG | Partie 3.3–3.6 |
| `COGNITIVE_ARCHITECTURE.md` | Architecture BDI, boucle cognitive, perception, mémoire, croyances, besoins, objectifs, décision (Utility AI — formule complète), actions | Partie 3.8–3.15 |
| `SYSTEMS_SPEC.md` | Systèmes fonctionnels transverses : groupes, livres/savoirs tangibles, relations, ressources, environnement, obstacles | Partie 3.16–3.22 |
| `COMMUNICATION_PROTOCOL.md` | Système de communication inter-entités (pulsations lumineuses, portée, diffusion, dégradation, coûts, bande passante, latence) — *à ne pas confondre avec `COMMUNICATION.md` racine, qui traite du transport inter-composants* | Partie 3.16 |
| `PERSISTENCE.md` | Vue d'ensemble, tables, procédure de sauvegarde/chargement atomique, garantie bit-à-bit, gestion de versions, optimisation | Partie 3.23 |
| `DETERMINISM.md` | Pourquoi le déterminisme, mécanismes garantissant, test de reproductibilité, limites | Partie 3.25, 6.12 |
| `CONFIGURATION.md` | Principe de séparation config/code, groupes de paramètres V1/V2, philosophie de réglage | Partie 3.27 |
| `API_CONTRACTS.md` | Contrats de transport exposés (WebSocket + HTTP), schémas d'événements/commandes, versionnement | Partie 2.2.3, 3.24 |
| `PERFORMANCE.md` | Stratégies de scalabilité, budget de tick, benchmarks V1, optimisations mémoire, résultats attendus | Partie 7.4–7.5 |
| `TESTING.md` | Plan de test V2, tests d'intégration, état des tests V1, gestion des erreurs/robustesse (concurrence, reconnexion réseau, validation d'état) | Partie 7.7–7.8 |
| `ROADMAP.md` | Roadmap détaillée SYNE (V0.1 → V2), jalons de validation, risques et atténuation, 25 critères de réussite minimaux, 30 décisions à figer | Partie 9.6, 7.9 |
| `CHANGELOG.md` | Historique versions SYNE | — |
| `adr/` | Voir section 9 — ADR déjà identifiés dans la Monographie (ADR-001 à ADR-011) à formaliser ici | Annexe F |

---

## 7. Documentation ECHOS

*Emergent Cognition & Holistic Observation System — observation, métriques et analyse.*

| Document | Contenu | Source Monographie |
|---|---|---|
| `README.md` | Lancer ECHOS seul, dépendances, comment il se connecte à SYNE | Partie 4.2.2 |
| `VISION.md` | Rôle scientifique, dimensions d'observation, interdits | Partie 4.1 |
| `ARCHITECTURE.md` | Composants, intégration avec SYNE, séparation des données | Partie 4.2 |
| `METRICS_SPEC.md` | Les 7 moteurs de métriques (diversité cognitive, propagation d'information, complexité sociale, convergence d'objectifs, détection de boucles de rétroaction, durabilité des ressources, dynamique de groupes) | Partie 4.3 |
| `EMERGENCE_INDICATORS.md` | Score d'émergence composite, phénomènes auto-détectés, complexité du système, indice d'imprévisibilité | Partie 4.4 |
| `CAUSAL_ANALYSIS.md` | Corrélation vs causalité, outils causaux, limites de l'analyse causale | Partie 4.5 |
| `EXPERIMENT_COMPARISON.md` | Principe de comparaison expérimentale, métriques de reproductibilité, format d'export | Partie 4.6 |
| `API_REST.md` | Endpoints principaux, diffusion temps réel, optimisation | Partie 4.7 |
| `LOGGING_INSTRUMENTATION.md` | Niveaux de journalisation, événements structurés, traces de décision, logs Serilog, profilage, console de debug, export | Partie 4.8 |
| `LIMITATIONS.md` | Limites méthodologiques et techniques, règle d'or d'interprétation | Partie 4.10 |
| `TESTING.md` | Stratégie de test ECHOS (métriques testables, non-régression sur les scores) | *(à créer — non détaillé dans la Monographie)* |
| `ROADMAP.md` | Roadmap détaillée ECHOS par phase | *(à créer à partir de 9.6/7.9, adapté)* |
| `CHANGELOG.md` | Historique versions ECHOS | — |
| `adr/` | ADR spécifiques ECHOS (ex. choix du mode de calcul causal, choix de stack API) | *(à créer)* |

---

## 8. Documentation PRISM

*Perceptual Rendering & Interactive Simulation Module — rendu (Godot) et interaction.*

| Document | Contenu | Source Monographie |
|---|---|---|
| `README.md` | Lancer PRISM seul, dépendances Godot, comment il se connecte à SYNE/ECHOS | Partie 5.4 |
| `VISION.md` | Pourquoi un framework intermédiaire, ce que PRISM fait / ne doit pas faire | Partie 5.1–5.2 |
| `ARCHITECTURE.md` | Choix du moteur (Godot), structure des scènes V1/V2, mapping 2D→3D, assets | Partie 5.2.3, 5.3 |
| `SCENE_SPEC.md` | Structure détaillée des scènes, organisation des assets | Partie 5.3 |
| `TRANSPORT_API.md` | WebSocket (données), HTTP (contrôle), interface TypeScript exposée | Partie 5.4 |
| `RENDERING_SPEC.md` | Rendu des entités (représentation, code couleur, sélection), rendu des ressources | Partie 5.5–5.6 |
| `VISUALIZATION_SPEC.md` | Visualisation des croyances (BeliefViewer, heatmap, bulles), visualisation sociale (graphe de relations, heatmap de confiance), visualisation des groupes, communication visuelle | Partie 5.7–5.10 |
| `UX_INTERACTION.md` | Caméra et interaction, HUD, interface d'analyse intégrée à ECHOS | Partie 5.11–5.13 |
| `ASSETS_CONVENTIONS.md` | Conventions d'assets, code couleur, nommage | Partie 5.3.4, 5.5.2 |
| `TESTING.md` | Stratégie de test PRISM (tests visuels/manuels, tests d'intégration transport) | *(à créer)* |
| `ROADMAP.md` | Roadmap PRISM, évolution envisagée, fonctionnalités futures, principe invariant | Partie 5.15 |
| `CHANGELOG.md` | Historique versions PRISM | — |
| `adr/` | ADR spécifiques PRISM (choix Godot, choix du protocole de transport) | ADR-003, ADR-004 (Annexe F, à référencer aussi ici) |

---

## 9. ADR — Architecture Decision Records

Deux niveaux d'ADR, avec le même gabarit (`docs/adr/0000-template.md` à créer une seule fois et dupliqué) :

- **ADR transverses** (`docs/adr/`) : décisions qui engagent plusieurs composants ou le projet dans son ensemble (ex. choix du monorepo, choix du protocole de transport commun, politique de versionnement).
- **ADR par composant** (`docs/components/<composant>/adr/`) : décisions internes à un composant.

La Monographie contient déjà 11 ADR informels (Annexe F.1–F.12) à formaliser et répartir :

| ADR existant | Destination |
|---|---|
| ADR-001 Simulation.Core | `syne/adr/` |
| ADR-002 Simulation.Console | `syne/adr/` |
| ADR-003 API HTTP REST légère | transverse (`docs/adr/`) — impacte SYNE↔ECHOS/PRISM |
| ADR-004 WebSocket temps réel | transverse (`docs/adr/`) |
| ADR-005 Tick = minute | `syne/adr/` |
| ADR-006 PRNG reproductible | `syne/adr/` |
| ADR-007 Mémoire oubliante | `syne/adr/` |
| ADR-008 Communication non confidentielle | `syne/adr/` |
| ADR-009 Énergie comme monnaie d'action | `syne/adr/` |
| ADR-010 Abandon de la morphologie physique | `syne/adr/` |
| ADR-011 Persistance JSON → SQLite | `syne/adr/` |

De nouveaux ADR seront nécessaires pour ECHOS et PRISM (aucun n'existe encore dans la Monographie) — à créer au fil de l'avancement, pas a priori.

---

## 10. Ordre de fabrication recommandé (roadmap documentaire)

L'objectif de cet ordre : ne jamais rédiger un document qui dépend d'un autre pas encore stabilisé.

**Phase 0 — Socle & gouvernance**
`LICENSE` → `VERSIONING.md` → `GITFLOW.md` → `CI_CD.md` → `docs/governance/*` → templates `.github/`

**Phase 1 — Cadrage général**
`VISION.md` → `ARCHITECTURE.md` → `COMMUNICATION.md` → `GLOSSARY.md` → `ROADMAP.md` (racine) → `README.md` (racine, en dernier car il résume tout)

**Phase 2 — SYNE (le socle technique dont ECHOS et PRISM dépendent)**
`VISION.md` → `ARCHITECTURE.md` → `DATA_MODEL.md` → `SIMULATION_LOOP.md` → `COGNITIVE_ARCHITECTURE.md` → `SYSTEMS_SPEC.md` → `COMMUNICATION_PROTOCOL.md` → `PERSISTENCE.md` → `DETERMINISM.md` → `CONFIGURATION.md` → `API_CONTRACTS.md` → `PERFORMANCE.md` → `TESTING.md` → `ROADMAP.md` → `adr/*` → `README.md` → `CHANGELOG.md`

**Phase 3 — ECHOS (consomme les contrats SYNE définis en phase 2)**
`VISION.md` → `ARCHITECTURE.md` → `METRICS_SPEC.md` → `EMERGENCE_INDICATORS.md` → `CAUSAL_ANALYSIS.md` → `EXPERIMENT_COMPARISON.md` → `API_REST.md` → `LOGGING_INSTRUMENTATION.md` → `LIMITATIONS.md` → `TESTING.md` → `ROADMAP.md` → `adr/*` → `README.md` → `CHANGELOG.md`

**Phase 4 — PRISM (consomme les contrats SYNE + s'intègre aux vues ECHOS)**
`VISION.md` → `ARCHITECTURE.md` → `SCENE_SPEC.md` → `TRANSPORT_API.md` → `RENDERING_SPEC.md` → `VISUALIZATION_SPEC.md` → `UX_INTERACTION.md` → `ASSETS_CONVENTIONS.md` → `TESTING.md` → `ROADMAP.md` → `adr/*` → `README.md` → `CHANGELOG.md`

**Phase 5 — Consolidation**
Relecture croisée `ARCHITECTURE.md` (racine) et `COMMUNICATION.md` à la lumière des 3 docs `API_CONTRACTS.md`/`API_REST.md`/`TRANSPORT_API.md` pour vérifier la cohérence des contrats → `FAQ.md` → `CONTRIBUTING.md` → vérification finale (section 12).

---

## 11. Conventions de nommage et de rédaction

- Un document = un fichier Markdown, `MAJUSCULES_SNAKE_CASE.md` pour les fichiers "type" (README, ARCHITECTURE...), en cohérence avec les conventions GitHub usuelles.
- Chaque document commence par un bandeau d'en-tête :
  ```markdown
  # <Titre>
  **Composant** : SYNE | ECHOS | PRISM | LIVEX (général)
  **Statut** : [STABLE] / [DRAFT] / [OUVERT]
  **Dernière mise à jour** : YYYY-MM-DD
  **Dépend de** : liens vers les docs prérequis
  ```
- Reprendre les tags déjà utilisés dans la Monographie (`[HÉRITÉ]`, `[OUVERT]`) pour marquer ce qui vient du prototype vs ce qui reste à décider.
- Chaque `SPEC` technique doit inclure, quand pertinent : schéma de données, algorithme en pseudo-code, exemples concrets (la Monographie en contient déjà beaucoup, notamment pour la formule d'utilité et le cycle cognitif — à réutiliser).
- Diagrammes : privilégier Mermaid (rendu nativement dans GitHub et dans la plupart des visualiseurs Markdown) plutôt que des schémas ASCII comme dans la Monographie actuelle, sauf pour les diagrammes déjà figés.

---

## 12. Checklist finale avant passage en réalisation

- [ ] Tous les documents de la section 3 (général) sont à l'état `[STABLE]`.
- [ ] `LICENSE` et `VERSIONING.md`/`GITFLOW.md`/`CI_CD.md` sont tranchés (pas de `[OUVERT]` restant).
- [ ] Pour chaque composant : `VISION.md` + `ARCHITECTURE.md` + specs techniques sont à l'état `[STABLE]`.
- [ ] Les 3 documents de contrat d'interface (`API_CONTRACTS.md` SYNE, `API_REST.md` ECHOS, `TRANSPORT_API.md` PRISM) sont cohérents entre eux et avec `COMMUNICATION.md`.
- [ ] Tous les `[OUVERT]` identifiés dans la Monographie (coûts de communication, bande passante, limites de bande passante, latence, coûts des livres...) ont été explicitement tranchés ou consciemment reportés à une phase ultérieure documentée dans `ROADMAP.md`.
- [ ] Les 30 décisions à figer (Partie 9.6.4 de la Monographie) sont reprises et cochées dans `SYNE/ROADMAP.md`.
- [ ] Les templates GitHub (`.github/ISSUE_TEMPLATE/*`, `PULL_REQUEST_TEMPLATE.md`) et les workflows CI (`ci.yml`) existent, même en version minimale.
- [ ] Le board Kanban est créé et reflète les phases de `ROADMAP.md`.

---

*Ce plan est lui-même un document vivant : une fois la documentation rédigée, il peut être conservé comme index de référence (`docs/DOCUMENTATION_PLAN.md`) ou remplacé par un `docs/README.md` qui fait office de sommaire définitif.*
