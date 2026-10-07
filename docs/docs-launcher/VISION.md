# VISION.md

**Composant** : LIVEX (Launcher)
**Statut** : [DRAFT]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : `../../VISION.md`, `ARCHITECTURE.md`
**Source Monographie** : —

---

## 1. Ce qu'est le LIVEX Launcher

Le LIVEX Launcher est la **couche d'orchestration** du projet LIVEX. Il permet de
piloter, de combiner, de surveiller et d'archiver les travaux de recherche conduits
avec les autres composants du projet, depuis une interface unique et un format de
données unique.

Il ne contient **ni la logique de simulation, ni la logique d'analyse, ni la
logique de rendu**. Ces responsabilités appartiennent respectivement à SYNE, ECHOS
et PRISM. Le Launcher ne fait que les coordonner.

| Couche | Composant | Rôle |
| :-- | :-- | :-- |
| Moteur | **SYNE** | Simule le monde, produit l'état, les traces et les événements |
| Mode A — Analyse | **ECHOS** | Analyse, reproductibilité, calibration, exploration scientifique |
| Mode B — Immersion | **PRISM** | Représentation temps réel et interactive du monde simulé |
| Orchestration | **Launcher** | Cycle de vie, modes, campagnes, supervision, archivage |

Le Launcher est un composant **de poids égal** à SYNE, ECHOS et PRISM dans
l'architecture de LIVEX, et non un simple utilitaire. Il possède son modèle de
données, son format d'échange et ses propres invariants.

## 2. Le problème adressé

Un projet de recherche multi-composants comme LIVEX souffre de trois difficultés
récurrentes que le Launcher adresse frontalement.

1. **La friction de démarrage.** Chaque composant se lance, s'observe et s'arrête
   séparément, avec ses propres prérequis, ses propres paramètres et ses propres
   signaux de disponibilité. L'utilisateur doit recomposer la pile à chaque
   session.
2. **L'obscurité des campagnes.** Une campagne de plusieurs dizaines de runs produit
   des résultats dispersés, sans trace fiable de ce qui a été exécuté, dans quel
   ordre, avec quelles graines, et dans quel état. La reproductibilité se perd par
   construction.
3. **La fracture des outils.** L'observation, l'analyse et l'immersion vivent dans
   des applications distinctes. Passer de « je regarde le monde » à « j'analyse ce
   que ce monde a produit » n'a pas d'outil dédié.

Le Launcher fournit un point d'entrée, un modèle de données et un format d'archive
pour résoudre ces trois problèmes.

## 3. Les modes d'usage et les types de session

Le Launcher repose sur une distinction structurante : **SYNE est le moteur, ECHOS
et PRISM sont deux usages différents de ce moteur pour des objectifs différents.**

| Mode | Composant | Objectif | Nature du flux | Sortie |
| :-- | :-- | :-- | :-- | :-- |
| **Analyse** | ECHOS | Comprendre, mesurer, comparer, calibrer | Durable, post-run, reproductible | Campagnes analysées, rapports, métriques |
| **Immersion** | PRISM | Observer, explorer, vivre le monde | Immédiat, temps réel, non reproductible | Représentation interactive |

Ces deux modes sont **de poids égal**, et s'inscrivent dans une **matrice à deux
axes** : un mode d'usage — analyse pur, analyse et télémétrie, immersif, expérience,
ou personnalisé — et un type de session — production, expérience ou développement.
La matrice complète est dans `COMPONENTS.md` §8.

Cette égalité a une conséquence architecturale forte : **le Launcher ne doit pas
introduire de hiérarchie implicite entre ECHOS et PRISM**. Chaque mode a ses propres
vues, sa propre supervision et son propre contrat d'intégration. Le mode Immersion
n'est pas « la visualisation d'un mode Analyse ».

### 3.1 Ce que le Launcher n'est pas

Le Launcher **n'est pas un quatrième composant métier**. Il n'ajoute ni
simulation, ni analyse, ni rendu. Sa valeur ajoutée est entièrement dans
l'orchestration, la présentation, la traçabilité et l'archivage.

Le Launcher **présente** les résultats sans les **calculer**. C'est la distinction
qui le sépare d'ECHOS : il affiche le rapport d'émergence que ECHOS produit, il ne
produit ni statistique ni interprétation. Voir
`adr/ADR-003-analyse-propriete-de-echos.md`.

## 4. Principes de conception

| Principe | Application dans le Launcher |
| :-- | :-- |
| **Indépendance des composants** | Chaque composant reste exécutable et exploitable sans le Launcher. Le Launcher n'est jamais une dépendance technique. |
| **Orchestration plutôt qu'implémentation** | Le Launcher décide quoi lancer, quand, dans quel ordre et avec quelle configuration. Il ne décide pas comment fonctionne la simulation, l'analyse ou le rendu. |
| **Aucune science dans le Launcher** | Le Launcher ne **calcule** aucune métrique, aucun indicateur, aucune statistique. Il en **demande** la production, la **présente** et l'archive. |
| **Traçabilité intégrale** | Toute action du Launcher est datée, corrélée, attribuée. Une campagne est reconstituable à partir de son seul paquet. |
| **Reproductibilité déclarative** | Une campagne décrit tout ce qui est nécessaire pour la rejouer : configuration résolue, graines dérivées, versions, plateforme. |
| **Format unique** | Campagnes, runs, analyses et rapports sont portés par un format unique, le paquet `.livexp`. |
| **Remplaçabilité** | Le Launcher est mince, indépendant et remplaçable. La valeur de LIVEX ne doit pas résider dans le Launcher. |
| **Dégradation gracieuse** | Un composant indisponible n'empêche pas les autres de fonctionner. Le Launcher dégrade le mode concerné, pas la session entière. |

## 5. Périmètre fonctionnel

Le Launcher couvre les capacités suivantes.

### 5.1 Cycle de vie des composants

- **Détecter** les composants installés et leurs versions.
- **Sélectionner** les composants à activer selon un profil d'exécution.
- **Démarrer**, **arrêter**, **redémarrer** les composants dans un ordre défini.
- **Superviser** leur disponibilité et réagir à leur défaillance.

### 5.2 Modes et profils

- **Proposer** des profils d'exécution prédéfinis correspondant aux combinaisons
  usuelles (simulation seule, analyse, immersion, complet, analyse hors ligne).
- **Résoudre** les dépendances entre composants et compléter automatiquement une
  sélection partielle.
- **Permettre** une sélection manuelle fine.

### 5.3 Campagnes et runs

- **Définir** une campagne : simulation de référence, nombre de runs, horizon de
  ticks, stratégie de graines, politique d'échec.
- **Planifier** l'exécution séquentielle des runs.
- **Superviser** la progression, estimer la durée restante, réagir aux échecs.
- **Reprendre** une campagne interrompue sans rejouer les runs terminés.
- **Annuler** proprement une campagne en cours.

### 5.4 Collecte et archivage

- **Constituer** un paquet `.livexp` par campagne, écrit en continu pendant
  l'exécution.
- **Collecter** les artefacts de données et d'analyse produits pour chaque run.
- **Sceller** le paquet en archive immuable à l'issue de la campagne.
- **Exporter**, partager et réimporter des paquets.

### 5.5 Supervision et diagnostic

- **Agréger** l'état de santé de tous les composants et l'exposer avec sa cause
  principale.
- **Collecter** et **journaliser** les événements techniques et les jalons de
  campagne.
- **Alerter** sur les seuils et les anomalies.

### 5.6 Distribution

- **Installer** le Launcher et les composants dans une arborescence cohérente.
- **Effectuer** un premier lancement accompagné d'une vérification de
  l'environnement.
- **Détecter** les composants manquants et guider l'utilisateur.

## 6. Non-objectifs

Les exclusions suivantes sont explicites. Toute fonction qui en découle est **hors
périmètre**.

| Non-objectif | Raison |
| :-- | :-- |
| **Calcul scientifique** | Les métriques, indicateurs et statistiques appartiennent à ECHOS. Le Launcher ne produit aucun résultat analytique. |
| **Simulation ou rendu** | Le Launcher n'implémente aucune règle de simulation, aucun calcul de rendu, aucune logique d'agent. |
| **Substitution à un composant** | Le Launcher ne rejoue pas ECHOS en mode dégradé, ne fait pas de visualisation sommaire à la place de PRISM. |
| **Multi-utilisateur, multi-tenant** | Pas de comptes, pas de rôles, pas de serveur partagé. Un espace de travail appartient à un utilisateur. |
| **Cloud, distribution distante** | Pas d'hébergement, pas de déploiement sur des machines distantes dans le périmètre livré. |
| **Environnement de production** | Le Launcher est un outil de recherche et d'exploration, pas un orchestrateur de production. |
| **Rendu temps réel de l'interface** | Le Launcher n'a pas vocation à être une surface d'animation continue. Il affiche des états, des progressions et des métriques échantillonnées. |
| **Analyse de données hors ligne générique** | Le Launcher n'est pas un navigateur de fichiers. Il n'ouvre un paquet que pour l'inspecter dans le cadre d'une campagne qu'il connaît. |

## 7. Le format de paquet

Le Launcher adopte un **format unique** pour ses données : le paquet `.livexp`.

Ce format est **vivant puis scellé**. Il est ouvert dès la création d'une campagne
et écrit en continu pendant toute l'exécution ; au terme de la campagne, il est
scellé en archive immuable de partage. Cette double nature permet à la fois la
reprise après incident et le partage d'un artefact autonome.

Le format est spécifié dans `PACKAGE_FORMAT.md`.

## 8. Contraintes assumées

- **Mono-espace en V0.1.** Un seul jeu de composants, un seul espace de travail.
  Le multi-espace est prévu mais non livré en première version.
- **Exécution séquentielle en V0.1.** Les runs d'une campagne s'exécutent l'un
  après l'autre. La parallélisation est prévue, non livrée.
- **PRISM verrouillé.** Le composant est conçu comme un composant à part entière
  et présenté dans toute la documentation, mais son intégration reste verrouillée
  tant que son implémentation n'est pas disponible.
- **Cycle de vie piloté par le Launcher.** Le Launcher démarre et arrête les
  composants qu'il supervise. Un composant lancé manuellement reste détectable,
  mais sa durée de vie n'est pas pilotée.
- **Interface d'orchestration.** L'interface du Launcher reste focalisée sur
  l'orchestration. L'exploration scientifique reste le rôle d'ECHOS : le
  Launcher en **présente** les résultats dans ses fenêtres natives (consoles de
  logs, fenêtre d'analyse) sans jamais les calculer (ADR-003, ADR-007).

## 9. Références

- Architecture : `ARCHITECTURE.md`
- Modèle de composants : `COMPONENTS.md`
- Contrat d'intégration : `INTEGRATION_CONTRACT.md`
- Format de paquet : `PACKAGE_FORMAT.md`
- Design de l'interface : `USER_INTERFACE.md`

---

## Points restés ouverts dans ce document

- Le mode batch de SYNE doit être confirmé comme existant : sans lui, aucune
  campagne automatique n'est possible. C'est la porte **P2** de `ROADMAP.md`.
- La capacité d'ECHOS à être piloté sans son interface graphique n'est pas confirmée.
  C'est la porte **P3**.
- Le statut de PRISM (verrouillé) doit être réévalué à chaque jalon de son
  implémentation. Voir `ROADMAP.md` et `adr/ADR-006-prism-verrouille-en-attente.md`.
