---
title: "PRISM 1.0.0 et PrismLdk : voir le monde, sans le recalculer"
description: "PRISM est packagé pour Windows et Linux ; son plugin PRISM-LDK expose les contrats SYNE et les points de contrôle aux projets Unreal et à Blueprint."
date: 2026-10-31
tags: ["prism", "unreal", "prism-ldk", "rendu-3d"]
draft: false
---

PRISM est la couche de rendu Unreal de LIVEX. Sa fonction est de donner une représentation visuelle au monde simulé : l'environnement, les agents et leurs états peuvent être présentés dans une scène 3D, tandis que SYNE continue de calculer les règles, les décisions et l'état autoritaire.

La release publique LIVEX 1.0.0 comprend PRISM packagé pour Windows et Linux. Une installation PRISM n'exige pas Unreal Engine : l'éditeur est requis lorsqu'on veut développer un projet Unreal ou intégrer le plugin.

## Le rôle du plugin PrismLdk

PRISM-LDK signifie **LIVEX Development Kit** ; `PrismLdk` est le nom technique du module Unreal. Réalisé et packagé en version 1.0.0, ce plugin C++ expose aux Blueprints les structures et événements des contrats SYNE, les commandes de contrôle, les réponses asynchrones ainsi qu'un registre reliant les identifiants d'agents SYNE à leurs acteurs visuels.

Le plugin reçoit la description du monde, les snapshots, les deltas et les événements via WebSocket. Il envoie les commandes de contrôle via HTTP. Il facilite ainsi la connexion d'un projet Unreal à SYNE sans transformer Unreal en moteur de simulation concurrent.

Le plugin est un kit d'intégration, pas une scène complète : il ne génère pas automatiquement le terrain, les acteurs, les collisions, la navigation, les widgets ou les animations. Le projet PRISM porte ces choix de présentation.

## Tests et périmètre

Les 76 Automation Specs PRISM/PrismLdk ont été exécutées pour la release : 71 réussies, 5 réussies avec avertissements et aucun échec. Elles couvrent notamment la projection, le registre du monde, le service, l'authentification, le rejeu des contrats et les transitions de contrôle. Cette validation porte sur les scénarios testés ; elle ne signifie pas que toutes les fonctions imaginées pour un rendu interactif sont achevées.

Consulter le [dossier technique PRISM](https://github.com/donovan-dev-web/LIVEX/tree/main/docs/prism), le [guide d'intégration Blueprint](https://github.com/donovan-dev-web/LIVEX/blob/main/docs/prism/PRISM_UNREAL_IMPLEMENTATION.md) ou télécharger la [release publique](https://github.com/donovan-dev-web/LIVEX/releases/tag/livex-v1.0.0).
