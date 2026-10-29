---
title: "PRISM-LDK V0.1.0 : traduire le monde logique en monde visible"
description: "Le plugin PRISM-LDK, packagé en V0.1.0, est le cœur de la représentation 3D : il traduit les données logiques de SYNE en représentation graphique Unreal, sans jamais devenir un second moteur."
date: 2026-10-29
tags: ["prism-ldk", "unreal", "prism", "contrats", "rendu"]
draft: false
---

Une trame arrive de SYNE. Elle contient un monde : ses dimensions, sa grille, ses obstacles, ses ressources, ses agents. Ce monde est vrai, mais il est fait de nombres. Côté Unreal, quelqu'un doit le recevoir, le convertir en types utilisables, et le redistribuer pour qu'il prenne forme. C'est tout le travail de **PRISM-LDK**. Rien de plus, et c'est précisément pour cela que c'est fiable.

Le plugin **PRISM-LDK** (*LIVEX Development Kit*, module Unreal `PrismLdk`) est désormais **réalisé et packagé en version 0.1.0**. Il est le cœur de la représentation 3D de LIVEX : le plugin qui traduit les données logiques de SYNE en représentation graphique 3D.

## Une couche mince, par discipline

PRISM-LDK fournit les types et les contrats SYNE, les fonctions de connexion et de contrôle, l'adaptation des messages et les répartiteurs d'événements. Un sous-système de l'instance de jeu porte l'accès au service et les fonctions Blueprint, que le graphe obtient par un nœud dédié.

Le plugin n'est pas un moteur. Il ne crée pas automatiquement le terrain, les acteurs, les widgets ou les animations, et ne prend aucune décision simulée. Le code C++ reste limité aux besoins d'intégration et au contrat Blueprint : les choix de rendu et de gameplay appartiennent au projet Unreal qui l'utilise — PRISM par défaut, ou tout autre projet construit par un utilisateur du kit.

Cette minceur est une contrainte, non une faible ambition. Tout ce qui n'est pas nécessaire pour transporter et convertir n'a pas sa place dans le plugin. C'est ce qui garantit que le plugin ne pourra jamais décider à la place du monde, même par accident.

## Deux transports, symétriques au moteur

Le plugin utilise deux transports, symétriques à ceux du moteur.

- Un flux **WebSocket** en lecture, sur le port 5180 par défaut, pour recevoir le monde préparé, les instantanés, les deltas et les événements.
- Un canal **HTTP** en écriture, sur le port 5181 par défaut, pour les commandes de contrôle et la lecture du statut.

Les adresses sont remplaçables par des options de connexion. Le lancement du moteur est distinct du lancement du projet Unreal : le moteur expose ses serveurs quand il est lancé avec les options correspondantes, et le plugin s'y connecte.

Cette symétrie simplifie la supervision : le Launcher connaît déjà ces deux ports et peut surveiller le lien du plugin de la même manière que celui des autres clients.

## Les événements exposés à Blueprint

Le plugin convertit chacun des messages du moteur en un événement utilisable par Blueprint.

| Message du moteur | Événement Blueprint | Rôle |
| :-- | :-- | :-- |
| `world_initialized` | `OnWorldInitialized` | description du monde préparé |
| `snapshot` | `OnSnapshot` | état dynamique complet du tick |
| `world_delta` | `OnWorldDelta` | mutations du monde (obstacles notamment) |
| événements typés | `OnSyneEvent` | décisions, actions, communication, groupes |

À côté de ces événements, des répartiteurs signalent le cycle de vie et les résultats : connexion, déconnexion, résultat de commande, erreur.

Un point d'ergonomie est souvent source de confusion. La lecture de l'état de connexion est une fonction pure, qui se branche sur un aiguillage d'énumération. Les répartiteurs d'événements, eux, ne se branchent pas sur le fil d'exécution d'une commande : il faut les assigner pour associer un événement personnalisé, qui sera appelé plus tard par le plugin lorsque la connexion sera réellement établie. Assigner un événement ne le déclenche pas immédiatement. Ce détail, apparemment mineur, est la cause la plus fréquente d'un graphe Blueprint qui ne réagit pas.

## Le cycle de connexion et de préparation

Le flux normal est : connecter, préparer, recevoir la description du monde, générer la scène, accuser réception, démarrer, recevoir les instantanés.

La connexion est déclenchée explicitement, jamais au démarrage du sous-système. Une reconnexion automatique peut être demandée après une perte inattendue, mais elle ne déclenche jamais la première connexion et s'annule sur une déconnexion volontaire. La graine doit être conservée, afin de pouvoir reproduire un run et de relancer une réinitialisation avec les mêmes paramètres.

Après une préparation explicite, le moteur exige l'accusé de réception avant le démarrage. Ignorer cet ordre, ou réutiliser une graine incompatible, produit une erreur : par exemple une erreur « monde non prêt ». Ces codes doivent être affichés, jamais supposés absents.

## L'ordre de génération de la scène

Dans l'événement d'initialisation, la scène se génère en phases, et l'accusé de réception n'est donné qu'une fois toutes les phases terminées.

1. **Topographie** : lire les dimensions et la taille de cellule, parcourir les cellules, créer les tuiles.
2. **Obstacles** : parcourir les obstacles, instancier les collisions visuelles depuis les positions et les rayons.
3. **Ressources** : instancier les marqueurs initiaux ; leurs coordonnées sont des indices de cellule, non des positions continues.
4. **Agents** : créer chaque acteur à sa position initiale, en l'indexant par identifiant, afin que le premier instantané mette à jour les mêmes acteurs sans doublon.
5. **Régions** : préparer les volumes ou découpages nécessaires.

L'ordre des cinq phases est contraignant : la topographie avant les obstacles, les obstacles avant les agents, et l'accusé de réception en dernier. Cet ordre garantit que, lorsque le monde est déclaré prêt, toutes ses couches existent.

## Projeter les instantanés

L'instantané est la source de vérité de l'état dynamique. À chaque instantané, la scène met à jour les acteurs : retrouver l'acteur par identifiant, convertir la position continue en cellule, déplacer l'acteur si la cellule a changé, mettre à jour les variables d'état (énergie, faim, soif, fatigue, intention, action), réconcilier les obstacles, mettre à jour les réserves, groupes, territoires, saisons et livres.

Deux optimisations évitent des erreurs classiques. Il ne faut pas interroger tous les acteurs d'une classe à chaque tick : mieux vaut conserver une table indexée par identifiant. Et il ne faut pas relancer une navigation à chaque instantané si la cellule cible n'a pas changé.

Un point de discipline important : la position projetée sur le NavMesh, avec son altitude, est une projection visuelle Unreal, non la position simulée. L'identifiant et les coordonnées logiques restent l'état autoritaire ; on ne renvoie jamais au moteur une position ou une altitude inventées par le rendu. C'est ce qui garantit qu'une scène différente (un autre terrain, un autre moteur de rendu) produirait le même monde.

## Deltas, événements et double traitement

Les deltas et les événements sont utiles aux notifications et aux effets ponctuels, mais leurs mutations peuvent aussi figurer dans l'instantané. Appliquer deux fois la même mutation produirait un double effet. La règle est donc nette : l'instantané est la source de vérité de l'état, les événements servent au journal, à l'interface ou aux effets ponctuels, sans réappliquer leurs changements.

## Un exemple concret : pourquoi les obstacles peuvent être absents

Un tableau d'obstacles vide dans le message d'initialisation signifie souvent que le moteur a préparé un monde sans layout, car les obstacles sont désactivés par défaut, et que le nœud de préparation du plugin n'envoie que la graine et la cadence. Activer le booléen sans fournir de layout ne crée aucun obstacle. Pour obtenir des obstacles, il faut fournir le layout côté moteur, ou étendre le contrat de préparation pour transmettre cette configuration. Ce n'est pas un défaut du rendu : c'est une conséquence du contrat.

Lorsqu'une scène paraît vide, la cause est souvent en amont, dans un contrat ou une configuration, non dans le rendu.

## Le monde logique et la représentation graphique

Au-delà du transport, le plugin packagé en V0.1.0 expose un **monde logique** et une **représentation graphique**. Le monde logique est la grille SYNE : ses dimensions, sa cellule, ses obstacles projetés, ses ressources. C'est la vérité géométrique et sémantique du monde simulé, sans aucun embellissement. La représentation graphique en est la traduction visuelle, déterministe et seedée par la graine SYNE.

Concrètement, le plugin fournit des données projetées prêtes à consommer (obstacles en coordonnées Unreal, ressources d'eau, bornes jouables) et un acteur de monde virtuel qui génère un maillage procédural : relief stylisé, lacs aux emplacements des ressources d'eau, mer en anneau hors grille. La grille logique occupe le rectangle central ; la falaise, la plage et la mer sont une décoration hors grille. Le maillage affiché vaut grille plus deux fois la bordure.

Toutes les hauteurs suivent la taille de tuile (`TileSizeUu`) ; les fréquences de bruit font l'inverse. Le bruit est le Perlin du moteur Unreal, seedé par la graine SYNE : aucune table externe, donc aucun écart entre deux builds. Les lacs sont placés à la cellule projetée de la ressource d'eau, jamais déplacés ; leur niveau d'eau est relatif au relief local.

Le C++ ne choisit aucune décision métier. Il applique des réglages exposés à Blueprint à des données SYNE. Le style « hand-painted » passe par les matériaux, pas par la géométrie. C'est ainsi que le kit laisse la liberté artistique à celui qui construit sa représentation, sans jamais toucher à la vérité simulée.

## Ce que ce jalon apporte

PRISM-LDK V0.1.0 ne fait pas de PRISM un moteur. Il fait de PRISM — et de tout projet qui s'y branchera — un client discipliné : un monde logique fidèle, une représentation graphique déterministe, et une règle stricte contre le double traitement. Le rendu montre ; il ne réinvente pas.

Le détail de l'intégration et des contrats vit dans le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les avancées du projet paraissent dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), disponible aussi en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml).
