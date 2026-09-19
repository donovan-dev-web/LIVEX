# LIVEX

**Living Intelligent Virtual Ecosystem eXperience**
*Une simulation multi-agents émergente, persistante et partiellement observable.*

[![Licence: MIT](https://img.shields.io/badge/Licence-MIT-00d4a0.svg)](LICENSE)
[![Version](https://img.shields.io/badge/Version-V0.1-9cf.svg)](VERSIONING.md)
[![Statut](https://img.shields.io/badge/Statut-Exploration%20V0.1-orange.svg)](ROADMAP.md)
[![Langue: FR](https://img.shields.io/badge/Langue-Fran%C3%A7ais-949494.svg)](GLOSSARY.md)

---

## Peut-on construire un monde qui écrit sa propre histoire ?

LIVEX ne programme pas de comportements. Il construit un environnement, des lois
et des entités autonomes — puis observe ce qui en émerge.

Chaque entité perçoit un fragment du monde, se souvient, croit des choses parfois
fausses, ressent des besoins, décide selon une logique d'utilité explicable, et agit.
Aucun scénario ne dicte ce qui doit se produire. Les structures sociales, les conflits,
les échanges, les coalitions ou les marchés informels ne sont jamais scriptés :
s'ils apparaissent, c'est parce que les mécanismes locaux les ont rendus possibles —
pas parce qu'une règle globale les a ordonnés.

C'est un projet de recherche expérimentale à la croisée de l'Artificial Life, de la
simulation multi-agents, des architectures cognitives BDI et du rendu temps réel —
mené en tant que développeur et infographiste 3D indépendant, avec la rigueur d'une
monographie de référence de 184 pages : fondements scientifiques, architecture
détaillée, choix techniques justifiés (ADR), éthique et limites assumées.

**Documentation complète du projet : [`LIVEX-Monographie.pdf`](docs/LIVEX-Monographie.pdf)**

---

## Ce que LIVEX démontre

| Exigence | Comment LIVEX y répond |
| :-- | :-- |
| **Déterminisme bit-à-bit** | Même seed + même config + même version moteur → même trajectoire, garantie par un PRNG reproductible (xoshiro256\*\*) et une persistance transactionnelle. |
| **Cognition explicable** | Architecture BDI (Beliefs–Desires–Intentions) : chaque décision produit un `DecisionRecord` traçable — jamais une boîte noire. |
| **Observabilité partielle** | Aucune entité ne connaît la vérité du monde : perception locale, mémoire à décroissance, croyances révisables — la source de la plupart des phénomènes intéressants. |
| **Rigueur scientifique** | Un score d'émergence est une mesure, pas une preuve. Chaque phénomène collectif doit être reproductible, causalement expliqué et distingué d'un artefact statistique (module ECHOS). |
| **Séparation stricte des responsabilités** | Trois modules indépendants, communiquant par contrats explicites — aucun ne fait autorité sur les deux autres. |

## Les trois modules

| Module | Rôle | Documentation |
| :-- | :-- | :-- |
| **SYNE** — *Systems & Emergent Network Engine* | Moteur de simulation. Seule source de vérité du monde : entités, cognition BDI, décision par utilité, ressources, persistance. | [`docs/docs-syne/README.md`](docs/docs-syne/README.md) |
| **ECHOS** — *Emergent Cognition & Holistic Observation System* | Observation et analyse. Mesure l'émergence, détecte les phénomènes, ne modifie jamais le monde. | [`docs/docs-echos/README.md`](docs/docs-echos/README.md) |
| **PRISM** — *Perceptual Rendering & Interactive Simulation Module* | Rendu 3D et interaction (Godot). Représente le monde, n'en est jamais la source de vérité. | [`docs/docs-prism/README.md`](docs/docs-prism/README.md) |

## État du projet

- **Phases 0 à 5 de documentation** : ✅ terminées (socle, cadrage, SYNE, ECHOS, PRISM, consolidation).
- **Statut global** : exploration V0.1 — documentation consolidée, implémentation des
  composants à venir. Un prototype historique (V1/V2, `docs/docs_prototype/`) reste
  fonctionnel et validé par 98 tests, et sert de socle d'héritage documenté (repères
  marqués `[HÉRITÉ]` dans la monographie).
- **Installation, commandes et état technique détaillé** : voir [`INSTALLATION.md`](INSTALLATION.md).

## Documentation

| | |
| :-- | :-- |
| [`VISION.md`](VISION.md) | Vision et philosophie du projet |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Architecture globale des trois modules |
| [`COMMUNICATION.md`](COMMUNICATION.md) | Contrats de communication inter-composants |
| [`GLOSSARY.md`](GLOSSARY.md) | Glossaire des concepts et termes du projet |
| [`ROADMAP.md`](ROADMAP.md) | Feuille de route |
| [`FAQ.md`](FAQ.md) | Questions fréquentes |
| [`docs/ETHICS_AND_SCOPE.md`](docs/ETHICS_AND_SCOPE.md) | Éthique, portée et limites assumées |
| [`docs/governance/`](docs/governance/) | Gouvernance du projet |

## Contribuer

Les contributions sont les bienvenues — voir [`CONTRIBUTING.md`](CONTRIBUTING.md) et
[`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md). Le processus de branches et de releases
est décrit dans [`GITFLOW.md`](GITFLOW.md), [`CI_CD.md`](CI_CD.md) et
[`VERSIONING.md`](VERSIONING.md).

## Licence

Distribué sous licence MIT — voir [`LICENSE`](LICENSE).

## Auteur

**Donovan Chartrain** — développeur & infographiste 3D.

- Portfolio développement : [donovan-dev-web.vercel.app](https://donovan-dev-web.vercel.app)
- Portfolio 3D : [donovan-dev-3d.vercel.app](https://donovan-dev-3d.vercel.app)

---

*« Construire les conditions. Observer les conséquences. Comprendre ce qui émerge. »*
