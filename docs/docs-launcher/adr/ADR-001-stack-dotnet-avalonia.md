# ADR-001 : .NET et Avalonia pour le Launcher

**Composant** : LIVEX (Launcher)
**Statut** : [Proposed]
**Dernière mise à jour** : 30 septembre 2026
**Dépend de** : —
**Source Monographie** : —

---

## Contexte

Le Launcher est le point d'entrée unique de LIVEX sur le poste de travail. Il doit
démarrer des composants, superviser des processus, exécuter des campagnes, écrire
un format binaire et présenter une interface de bureau dense en information.

Cinq exigences contraignent le choix technique.

1. **Un seul point d'entrée.** L'utilisateur doit lancer LIVEX par une commande
   unique, sans installer de langage ni de moteur.
2. **Une interface native.** L'interface est un outil d'orchestration, pas un site
   web. Elle doit être dense, réactive et exploitable au clavier.
3. **Deux plateformes.** Windows et Linux doivent être couverts par le même code.
4. **Aucune dépendance de langage dans les composants.** Le Launcher est un pair de
   SYNE, ECHOS et PRISM, pas leur bibliothèque. Il ne peut pas devenir une
   dépendance technique d'un composant.
5. **Un format binaire écrit en flux.** Le paquet `.livexp` est écrit en incrément
   pendant des heures, puis scellé.

Le choix du moteur graphique est déjà tranché pour PRISM par l'ADR-002 de PRISM,
qui retient Unreal Engine. Cette décision ne s'applique pas au Launcher : le
Launcher n'est pas un moteur graphique.

## Décision

**Le Launcher est une application de bureau .NET, avec Avalonia pour l'interface.**

Les éléments structurants :

- **.NET (C#)** — application autonome, cible `net8.0` ou ultérieure, architecture
  en couches avec un assembly `Launcher.Domain` sans dépendance de présentation ni
  d'infrastructure.
- **Avalonia** — rendu multi-plateforme, contrôles denses, liaison de données et
  liaison de commandes MVVM natives. Aucune page web, aucun conteneur web.
- **MVVM** — la présentation ne contient aucune logique métier. La couche
  application expose des intentions, la vue les traduit en actions.
- **Aucune référence de projet** vers SYNE, ECHOS ou PRISM. Les trois composants
  sont des processus externes ; le seul assembly de référence autorisé est
  `Simulation.Core`, et uniquement pour ses types de contrats sérialisés.
- **Format `.livexp`** écrit avec une bibliothèque ZIP compatible ZIP64, avec
  contrôle explicite des empreintes.

## Conséquences

### Positives
- Le langage de référence du projet est réutilisé : une seule compétence, une seule
  chaîne d'outils, un seul écosystème de test.
- Le découpage en assembly rend le domaine et le format du paquet testables **sans
  démarrer un seul composant**, ce qui est déterminant pour `TESTING.md` §3.
- Avalonia fournit un rendu multi-plateforme sans écrire deux fois l'interface, et
  sans imposer un moteur graphique à un orchestrateur.
- L'exécution autonome supprime toute installation de langage ou de moteur par
  l'utilisateur.
- L'interface dense et la navigation au clavier sont natives, ce que les
  technologies web rendent plus difficiles à garantir.

### Négatives
- Le coût de compilation .NET et le temps de démarrage de l'application sont
  supérieurs à ceux d'un exécutable natif minimal, pour une interface de bureau.
- Avalonia ajoute une dépendance de rendu et une couche d'abstraction qu'il faut
  suivre dans les versions.
- Le découpage en sept assembly impose de la discipline : la frontière entre domaine
  et infrastructure doit être tenue par l'analyse statique, pas seulement par la
  bonne volonté.
- Une application .NET autonome produit un exécutable volumineux, ce qui pèse sur la
  distribution.

### Risques
- **La frontière de contrôle se dégrade en couche de service.** Le Launcher
  accumule de la logique métier parce qu'il est le seul point de contact.
  Mitigation : tests d'analyse statique sur les dépendances d'assembly, et
  interdiction de toute référence à Avalonia dans `Launcher.Domain`.
- **Avalonia devient un point de blocage.** Une évolution de la bibliothèque
  contraindrait l'interface. Mitigation : la logique de présentation est isolée du
  domaine, donc remplaçable sans toucher à l'orchestration.
- **Dérive vers un composant de plus.** Le Launcher développe une fonctionnalité
  scientifique parce qu'il est sur le chemin. Mitigation : la frontière contrôle et
  données de `ARCHITECTURE.md` §3, et la revue des non-objectifs de `VISION.md` §6 à
  chaque nouvelle fonctionnalité.

## Alternatives considérées

- **Application web locale, avec navigateur ou serveur embarqué** : refusé.
  L'interface d'orchestration doit être un outil natif, et non un site. Cela
  imposerait un serveur, un second langage, et une dépendance à un navigateur pour
  une fonctionnalité de bureau.
- **Tauri ou application à interface système native** : refusé. Le gain en
  compacité est réel, mais le coût est un langage de plus et un modèle de liaison de
  données moins expressif pour une interface dense à liaison de commandes.
- **Qt pour C++** : refusé. Techniquement capable, mais cela ajoute un langage et une
  chaîne d'outils lourde pour un composant dont l'essentiel de la logique relève de
  l'orchestration et de la sérialisation, deux domaines bien servis par .NET.
- **Console pure** : refusé. Le volume d'état à afficher en permanence, les
  compte-rendus par composant et la lecture de campagne rendent une interface
  graphique nécessaire.
- **Avalonia sans MVVM** : refusé. Sans séparation stricte, la logique
  d'orchestration se retrouve dans les vues, ce qui rend l'interface intestable.
- **Référence directe à `Simulation.Core` au-delà des contrats** : refusé. Le
  Launcher doit rester découplé du moteur ; seuls les types de contrats sérialisés
  sont autorisés.

## Validation / rejet

- **Analyse statique** : les dépendances entre assembly sont vérifiées
  automatiquement, et toute dépendance interdite fait échouer la construction.
- **Testabilité** : `Launcher.Domain` et `Launcher.Package` s'exécutent dans une
  campagne de test qui ne démarre aucun processus, comme l'exige `TESTING.md` §3.
- **Couverture** : la cible de couverture du domaine est fixée à ≥ 85 % des lignes
  par `TESTING.md` §7.
- **Multi-plateforme** : les scénarios de bout en bout sont exécutés sur Windows et
  Linux.
- **Réouverture** : la décision est réexaminée si Avalonia ne permet pas d'atteindre
  les cibles de réactivité de `TESTING.md` §11, ou si l'interface dense devient
  impossible à maintenir. Le découpage en assembly, lui, n'est pas remis en cause.

---

## Mises à jour

| Date | Changement | Motif |
| :-- | :-- | :-- |
| 30 septembre 2026 | Création | Trancher la pile technique du Launcher |
