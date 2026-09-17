# ETHICS_AND_SCOPE.md

**Composant** : LIVEX (général)
**Statut** : [DRAFT]
**Dernière mise à jour** : 17 septembre 2026
**Dépend de** : `VISION.md`, `FAQ.md`
**Source Monographie** : Partie 8 (Portée, Risques et Éthique), Annexe A.6 (références éthiques)

---

> Ce document condense la Partie 8 de la Monographie. Il devra être mis à jour si le projet est présenté publiquement.

## 1. Portée

### 1.1 Ce que LIVEX est

- Un **moteur de simulation multi-agents émergente** avec persistance.
- Un **terrain d'expérimentation** pour l'étude des comportements collectifs.
- Une **plateforme de preuve de concept** pour les mondes persistants.
- Un **projet open-source** (architecture ouverte, documentation publique).

### 1.2 Ce que LIVEX n'est pas

| Domaine | Position de LIVEX |
| :-- | :-- |
| Jeu commercial | Non — projet de démonstration et d'expérimentation |
| Simulation physique réaliste | Non — abstraction logique, pas de moteur physique |
| Plateforme d'IA de front | Non — LLM uniquement via adapter optionnel |
| Remplaçant des humains | Non — ouvre une fenêtre sur l'émergence |
| Modèle de la société humaine | Non — métaphore, pas reproduction |

### 1.3 Frontières temporelles

- Le scope est centré sur **V1 + V2** (S1 à S12 de la feuille de route).
- Les générations futures (construction, reproduction, LLM, multijoueur) sont des **pistes documentées**, pas des engagements.

## 2. Limites

### 2.1 Limites de la simulation

- Monde **2D logique** (500×500 en défaut), pas un espace 3D physique.
- Entités **abstraites** — pas des modèles cognitifs complets.
- Physique simplifiée (mouvement, blocage).
- Ressources = compteurs, pas des objets physiques.

### 2.2 Limites méthodologiques

- Une simulation ne prouve **rien** sur le monde réel.
- La correspondance des concepts (besoins, croyances) avec les sciences humaines est **approximative**.
- Une **absence d'émergence n'est pas un échec** du projet en soi.
- Le déterminisme bit-à-bit dépend du runtime, de la version .NET et de l'exécution → séquencée.

### 2.3 Limites technologiques

- Monorepo optimisé pour **1 développeur** (`.editorconfig` uniforme).
- Ressources externes à vérifier manuellement.
- Parallélisation bornée par le **déterminisme**.
- API REST de contrôle = compromis (JSON, pas de typage fort réseau).

## 3. Risques (résumé)

### 3.1 Risques projet

| Risque | Probabilité | Impact | Mitigation |
| :-- | :-- | :-- | :-- |
| Performance insuffisante | Moyen | Élevé | Benchmarks + optimisation dès Phase 1 ; objectifs mesurés (≥10 t/s à 1000 entités) |
| Complexité croissante du BDI | Élevé | Moyen | BDI minimal puis itérations de validation |
| Incohérence des données | Moyen | Élevé | Tests sauvegarde/chargement bit-à-bit à chaque itération |
| Burnout / portée trop large | Moyen | Élevé | « Lunettes de scope » : fonctionnalités nice-to-have déplacées en V3 |
| Dérive technologique | Moyen | Moyen | Principe de cohérence : 1 langue (C#) pour le cœur, moteur interchangeable |

### 3.2 Risques scientifiques

| Risque | Atténuation |
| :-- | :-- |
| Émergence = artefact statistique | Répétition multi-seeds, analyse causale ECHOS |
| Paramètres trop stéréotypés | Exploration systématique, comparaison de runs |
| Tests trop prescriptifs | Tests sur les invariants et sorties, pas sur trajectoires internes |

### 3.3 Risques techniques

- **Ring buffer d'événements** (500 000) : volume à borner et profiler.
- **Concurrence en EventBus** : perception parallèle séparée de l'écriture des événements.
- **Interopérabilité Godot** : le C# doit supporter le frontal Godot (DTO JSON sur WS/HTTP exclusivement).

## 4. Défis scientifiques et techniques

- **Mesurer l'émergence** (métriques micro→méso→macro) : valider le score sur des mondes connus, ne jamais l'utiliser seul, compléter par analyse qualitative.
- **Réductionnisme comportemental** : quel niveau de complexité les primitives permettent-elles d'atteindre ?
- **Échantillonnage** : combien de runs pour une conclusion robuste (question ouverte).
- **Passage à l'échelle déterministe** : parallélisme en lecture seule (grille), séquencement pour Cognition/Actions, JIT sur les benchmarks.
- **Persistance transactionnelle** : atomique, versionnée, vérifiable (checksum).
- **Intégration Godot** : dépendances du moteur isolées, communication par DTO uniquement.

## 5. Éthique

### 5.1 Questions soulevées

1. **Souffrance simulée** : les entités peuvent « mourir », « souffrir », être attaquées — représentation non-glorifiante ?
2. **Tromperie** : simuler le mensonge (confiance, désinformation) est-il légitime ?
3. **Responsabilité** : qui est responsable si un utilisateur est offensé par un comportement simulé ?
4. **Désinformation de niveau supérieur** : un système qui produit des rumeurs pourrait renforcer l'idée que « la désinformation est naturelle ».
5. **Frontière homme/machine** : l'utilisateur observe et manipule des entités ressemblant à des créatures vivantes.

### 5.2 Positionnement de LIVEX

| Principe | Application |
| :-- | :-- |
| **Transparence** | Mécanismes publics (source ouverte, ADR) |
| **Non-auto déception** | LIVEX ne prétend pas être une conscience ni une « société réelle » |
| **Abstraction** | Le joueur est informé du niveau d'abstraction |
| **Respect de l'information** | La désinformation n'est pas présentée comme « naturellement morale » dans le monde réel |
| **Responsabilisation** | Choix de conception documentés, discutables, amendables |

### 5.3 Éthique de l'observation (ECHOS)

- Qui observe, pourquoi, avec quels instruments ?
- Les métriques sont-elles biaisées par la conception ?
- Si un comportement « controversé » émerge (attaque organisée, exclusion) : **observer, documenter, analyser**, pas intervenir (posture de recherche).

### 5.4 Éthique du déterminisme

Un système déterministe rend-il l'entité dépourvue de « choix » ? Position LIVEX : l'entité a une **autonomie informatique** (elle choisit selon son état, ses croyances, ses objectifs) sans liberté métaphysique. Distinction documentée.

## 6. Questions ouvertes (Partie 8.7)

- ✨ Scientifiques : mesurer un « degré d'émergence » fiable ? primitives suffisantes pour des phénomènes complexes ? production de **culture** (normes, rituels, symboles) ? **coopération stable** sans mécanisme central ? distinguer émergence vs **complexité programmée** ?
- ⚙️ Techniques : au-delà de 1000 entités (10 000+ par décomposition spatiale/LOD) ? parallélisme distribué déterministe ? BDI + LLM sans perte de déterminisme ? **mondes multiples** parallèles ?
- 🧠 Philosophiques : où s'arrête la simulation et où commence le modèle ? une entité simulée peut-elle avoir une « fin » éthique ? la distinction vérité/croyances est-elle une bonne abstraction ?

## 7. Références (Annexe A.6)

- EU AI Act (2024) — responsabilité, transparence, classification des risques.
- Floridi, L. (2014) — éthique de l'information, manipulation et tromperie.
- (Autres références émergence/éthique : Annexe A.1–A.5 de la Monographie.)

---

## Points restés ouverts dans ce document
- Statut : ce document est une synthèse stabilisée de la Partie 8 ; les questions ouvertes (§6) y sont volontairement conservées en l'état, dans l'attente des décisions d'implémentation.
- A réviser si le projet est présenté/pitché publiquement.