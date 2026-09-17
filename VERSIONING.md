# VERSIONING.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : §7.1 (organisation du monorepo), Annexe J (roadmap V2)

---

## 1. Objectif

Ce document définit la politique de versionnement de LIVEX et de chacun de ses trois composants (SYNE, ECHOS, PRISM). Il repose sur la **sémantique des versions (SemVer 2.0.0)** appliquée de manière **indépendante par composant**, doublée d'un marqueur de version **globale** pour les livraisons transverses.

## 2. Principe général

- Chaque composant (SYNE, ECHOS, PRISM) est versionné **indépendamment** selon SemVer : `MAJOR.MINOR.PATCH`.
- Un **numéro global LIVEX** est publié lorsque les trois composants sont livrés ensemble (release assemblée des trois composants). Il suit aussi SemVer mais son rythme est **transverse** (décidé au niveau du dépôt racine).
- Les dépendances inter-composants sont exprimées en **fourchettes SemVer compatibles** (`^`), jamais en versions exactes, sauf dans les contrats de compilation figés.

## 3. Définition des niveaux SemVer

Pour chaque composant appliquant SemVer :

| Niveau | Règle propre composant | Exemples |
| :-- | :-- | :-- |
| **MAJOR** | Changement **incompatible** d'API publique, de contrat de transport ou de format de persistance (sans migration fournie) | Suppression d'un endpoint, changement de schéma SQLite sans script de migration, changement du format `WorldSnapshot` |
| **MINOR** | Ajout **rétrocompatible** de fonctionnalité ; les anciens consommateurs continuent de fonctionner | Nouveau moteur de métriques ECHOS, nouveau type de message SYNE, nouvelle scène PRISM |
| **PATCH** | Correction de bug, optimisation interne, documentation ; aucune modification observable d'interface | Correction d'un calcul de santé, ajustement d'un coût d'action, typo dans un log |

Règles transverses :

- Version `0.x.y` : pendant la phase d'exploration (V0.1), un incrément de `x` est toléré pour un changement mineur, mais **tout contrat de transport ou de persistance cassé impose l'incrément de `x`** (pas de faux `MAJOR` avant la 1.0).
- Le **déterminisme bit-à-bit** (§2.3.4 de la Monographie) est un contrat : toute modification qui altère la trajectoire reproductible d'un run avec la même seed incrémente au minimum `MINOR` et impose la mise à jour de l'identifiant de version moteur (`engineVersion`) qui entre dans la définition d'un run.

## 4. Tags Git

| Préfixe | Portée | Exemple |
| :-- | :-- | :-- |
| `syne-v` | Moteur de simulation SYNE | `syne-v0.1.0` |
| `echos-v` | Système d'observation ECHOS | `echos-v0.2.1` |
| `prism-v` | Représentation PRISM | `prism-v0.1.0` |
| `livex-v` | Release globale assemblée | `livex-v0.1.0` |

- Le tag `livex-vX.Y.Z` référence l'ensemble des tags composants qui le composent (renseignés dans les notes de release).
- Les tags de composant sont posés sur le répertoire du composant ; le tag `livex-v` est posé sur la racine du dépôt.

## 5. Convention de branche

Le versionnement est déclenché par les évènements suivants :

- `MINOR`/`MAJOR` → fusion dans `develop` puis promotions successives des branches `release/` (cf. `GITFLOW.md`).
- `PATCH` → fusion directe (hotfix) dans `main` et `develop`.

## 6. Changelog

- Le fichier `CHANGELOG.md` de la racine et de chaque composant suit la convention **Keep a Changelog** : sections `[Unreleased]`, `[x.y.z] - AAAA-MM-JJ`.
- Les entrées sont regroupées en `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`.
- Chaque entrée mineure/majeure référence les exigeabilités issues de la Monographie quand applicable (ex. « ajout du moteur `FeedbackLoopDetector` (Monographie §4.3.5) »).

## 7. Règles d'incrément en pratique (V0.1)

- Le projet est en pré-1.0 : `MAJOR` reste à `0`.
- Toute libération composant visible publiquement incrémente au minimum `PATCH`.
- Le numéro global `livex-v` n'est pas posé à chaque incrément composant : il est posé au moment des jalons définis dans `ROADMAP.md`.

---

## Points restés ouverts dans ce document
- Aucun — document stabilisé. Le rythme de pose des tags `livex-v` sera affiné lors de la première release assemblée (phase CI/CD réelle).