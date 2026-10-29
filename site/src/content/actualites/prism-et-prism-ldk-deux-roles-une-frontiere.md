---
title: "PRISM et PRISM-LDK : le kit et la vision"
description: "PRISM-LDK est le cœur de la représentation 3D : le plugin Unreal qui traduit les données logiques de SYNE. PRISM est le projet artistique construit autour, la représentation stylisée par défaut."
date: 2026-10-29
tags: ["prism", "prism-ldk", "unreal", "rendu", "architecture"]
draft: false
---

SYNE simule un monde fait de données numériques et de logique : une grille, des cellules, des obstacles, des ressources, des agents, des ticks. Ce monde n'a pas d'apparence. Il est vrai, mais il n'est pas visible. C'est précisément le vide que PRISM-LDK vient combler.

**PRISM-LDK** (*LIVEX Development Kit*) est le **cœur de la représentation 3D** de LIVEX. C'est un plugin Unreal Engine qui assure la communication avec SYNE et la traduction de ses données en représentation graphique 3D. Sans lui, il n'y aurait pas d'image : seulement des nombres.

**PRISM**, lui, est le **projet Unreal construit autour de ce plugin**. C'est la représentation artistique et graphique de ce monde, selon la vision de son auteur. PRISM est ce que l'utilisateur voit par défaut. Il n'est qu'une représentation stylisée à terme, pas la vérité du monde.

## Séparer la logique de l'art

Cette séparation n'est pas un détail d'organisation. Elle est le cœur de la démarche.

D'un côté, la **partie scientifique** : SYNE détient la vérité de l'état simulé, les décisions, les actions, l'évolution des entités, le déterminisme bit-à-bit. Rien de tout cela ne doit dépendre de la manière dont on le regarde.

De l'autre, la **partie artistique** : la forme que prend le monde à l'écran, le style, l'ambiance, les choix de mise en scène. Ces choix sont légitimes, mais ils sont subjectifs, et ils ne doivent jamais rétroagir sur la simulation.

PRISM-LDK est la frontière entre les deux. Il traduit, il ne décide pas. Il transporte les données de SYNE vers Unreal, les convertit en types utilisables par Blueprint, et redistribue les événements. Tout ce qui est rendu reste une projection de la vérité simulée, jamais une cause.

## Le kit, pas le produit

Le nom le dit : *Development Kit*. PRISM-LDK est fourni pour ceux qui souhaiteraient créer leur propre représentation stylisée du monde simulé par SYNE. PRISM en est l'implémentation par défaut, la vision de l'auteur. Mais le kit est ouvert : un autre projet Unreal peut s'y brancher et inventer une autre manière de montrer le même monde.

Cette ouverture est volontaire. Le monde simulé est unique et partagé ; les manières de le voir peuvent être multiples. Tant que la règle tient — le rendu montre, il ne décide pas — plusieurs représentations peuvent coexister sans jamais fausser la simulation.

## Où en est le travail

PRISM-LDK est **réalisé et packagé en version 0.1.0** : couche C++ mince, monde logique et représentation procédurale. Ses Automation Specs se clôturent avec **69 spécifications documentées et 0 échec** sur Unreal Engine 5.8.

PRISM, le projet, reste en version de travail : l'expérience de rendu interactive complète est **hors périmètre V0.2**. Aucune version jouable n'est revendiquée à ce jour. Le moteur et l'observation passent d'abord ; le rendu stylisé vient ensuite, sans diluer la validation de ce qui fait la valeur scientifique du projet.

Le détail de l'intégration et des contrats vit dans le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les avancées du projet paraissent dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), disponible aussi en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml).
