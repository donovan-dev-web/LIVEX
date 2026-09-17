# VISION.md

**Composant** : LIVEX (général)
**Statut** : [STABLE]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : —
**Source Monographie** : Partie 1 (Préface & Fondements), Partie 2 (Présentation du Projet)

---

## 1. Ce qu'est LIVEX

LIVEX — **Systems & Emergent Network EXperiment** — est une plateforme de simulation multi-agents à émergence, persistante, temps réel et observable, construite sur trois modules complémentaires :

| Module | Nom complet | Rôle |
| :-- | :-- | :-- |
| **SYNE** | Systems & Emergent Network Engine | Moteur de simulation ; possède la vérité du monde ; indépendant du rendu |
| **ECHOS** | Emergent Complex Hierarchical Observation System | Système d'observation, d'analyse et de pilotage |
| **PRISM** | Perceptual Rendering & Interactive Simulation Module | Couche de représentation et d'interaction 3D |

LIVEX propose une **troisième voie** entre deux extrêmes :
- scripter des comportements prédéfinis, ou
- déléguer la cognition à un réseau de neurones.

À la place, le projet construit un moteur où des entités autonomes — dotées de besoins, de mémoire, de croyances et de capacités d'action — interagissent dans un environnement persistant et partiellement observable. Les structures collectives (regroupements, flux d'échange, spécialisations, conflits) **ne sont pas programmées : elles émergent** (Monographie §1.1).

## 2. Le problème adressé

Les simulations multi-agents classiques sont soit **émergentes mais imprécises** (simulations à base de règles simples), soit **précises mais scriptées** (déterminées par avance). LIVEX cherche une voie intermédiaire : un terrain d'expérimentation où la complexité sociale et cognitive émerge de mécanismes explicites, reproductibles et observables (Monographie §1.2).

Trois exigences dominantes structurent le projet :

1. **Déterminisme** : deux exécutions identiques produisent exactement la même trajectoire (reproductibilité bit-à-bit).
2. **Observabilité** : rien de ce qui se passe dans le monde n'est opaque ; tout est traçable (perceptions, décisions, actions, communications).
3. **Indépendance du rendu** : la vérité du monde appartient au moteur ; la visualisation n'est jamais la source de vérité.

## 3. Les piliers de conception

1. **Autonomie des entités** — le moteur fournit des structures (état, besoins, perception, mémoire, croyances), jamais de script de conduite (Monographie §1.4).
2. **À la source de la cognition** — architecture BDI (croyances, désirs, intentions) pour chaque entité.
3. **Observabilité partielle** — une entité ne connaît qu'une partie du monde ; l'information imparfaite est un moteur de diversité comportementale, d'erreur et d'exploration.
4. **Émergence par contraintes** — mémoire dégradée, communication non confidentielle, énergie comme monnaie d'action : les comportements collectifs naissent des contraintes, pas des règles globales.
5. **Déterminisme scientifique** — grâce à un PRNG reproductible (xoshiro256\*\*) et une persistance de l'état complet, LIVEX est un instrument valide pour l'étude de l'émergence.
6. **Indépendance du rendu** — SYNE fonctionne sans interface graphique ; PRISM est un framework intermédiaire interchangeable.

## 4. L'ambition scientifique et culturelle

LIVEX est à la fois :
- un **outil scientifique** : comparer des trajectoires contrôlées, tester l'effet d'un paramètre, reconstruire des chaînes causales ;
- une **œuvre contemplative** : observer un monde qui vit, qui se groupe, qui parle, qui meurt ;
- une **plateforme d'expérimentation philosophique** : qu'est-ce qui rend un système « vivant » ou « intelligent » ? (Monographie §2.1, §2.9).

**Avertissement méthodologique** (Monographie §2.1.2, §4.4.1) : LIVEX ne prétend pas démontrer l'existence d'une intelligence ou d'une société. Un score d'émergence est une **mesure particulière**, jamais une preuve.

## 5. Du prototype à la vision V0.1

- Le prototype (V1/V2) a validé les principes fondamentaux : moteur déterministe, 98 tests passants, émergence de comportements non scriptés (attaque émergente à 100 entités, Monographie §7.5).
- La cible V0.1 reprend ces acquis et les **consolide** : modularité en trois composants, contrats de transport stables, documentation exhaustive.
- La vision long terme est décrite dans `ROADMAP.md` ; la philosophie de fond est développée dans `GLOSSARY.md` et disséminée dans chaque `VISION.md` de composant.

## 6. Contraintes assumées

LIVEX assume ses limites (détaillées dans la Monographie, Partie 8 et dans `docs/nos docs par composant`) :
- pas de simulation physique morphologique réaliste (entités logiques, pouvant se chevaucher) ;
- pas d'IA d'apprentissage — la cognition reste explicite et inspectable ;
- pas d'objectif de « jeu » ou de « réalisme graphique » avant la maturité.

---

## Points restés ouverts dans ce document
- Aucun — vision stabilisée. Les nuances de ce que LIVEX prétend démontrer (score d'émergence) restent un sujet de discussion scientifique permanent, mais le cadrage méthodologique est ferme.