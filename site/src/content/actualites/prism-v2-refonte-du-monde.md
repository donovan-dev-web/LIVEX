---
title: "PRISM V2 : le monde n'est plus un décor"
description: "La cible PRISM V2 déplace l'autorité du monde physique vers un noyau .NET headless et redéfinit la frontière entre PRISM, SYNE et ECHOS."
seoTitle: "PRISM V2 : refondre le monde de LIVEX"
date: 2026-11-04
tags: ["prism", "v2", "architecture", "livex"]
draft: true
---

Dans la première version de LIVEX, PRISM donne une forme visuelle au monde calculé par SYNE. La cible V2 change cette relation en profondeur : PRISM ne représente plus seulement le monde, il en porte l'état physique.

Ce déplacement clarifie les responsabilités. **SYNE reste le lieu de la cognition** : besoins, croyances, mémoire, relations et décisions. **PRISM devient l'autorité de l'espace et de la matière** : terrain, objets, déplacements, actions et temps physique. **ECHOS observe** les traces produites par les deux. Le visualiseur, lui, se connecte pour lire le monde ; il reste facultatif.

## Un monde qui agit sur les possibilités

Le monde V2 est prévu comme un terrain voxel borné, défini par des données. Les agents pourraient y extraire des ressources, fabriquer des outils, cultiver, transporter des réserves et bâtir des structures. Ces activités ont des coûts : distance, temps, capacité de transport, usure et péremption.

L'enjeu n'est pas d'écrire à l'avance une histoire de coopération. Il est de donner aux agents des contraintes et des occasions observables. Une structure partagée peut-elle aider plusieurs agents ? Une récolte saisonnière encourage-t-elle le stockage ? Ces questions resteront des hypothèses à mesurer, y compris si les résultats ne vont pas dans le sens attendu.

## Le moteur avant l'image

La cible sépare un noyau .NET sans dépendance graphique, un host headless qui pilote les ticks et la persistance, et un visualiseur Silk.NET/OpenGL. Ainsi, un run n'a pas besoin d'une fenêtre pour avancer. SYNE demeure l'horloge maîtresse ; le contrat prévu entre SYNE et PRISM organise commandes, observations et événements.

Le visualiseur aide à comprendre une trajectoire, mais il ne décide pas de ce qui existe. Les positions et les actions viennent du monde simulé. L'affichage ne peut ni corriger une décision ni inventer une conséquence.

## Une cible, pas une annonce de livraison

PRISM 2.0.0 et le contrat SYNE↔PRISM 1.0 sont des versions cibles dans la documentation de conception. Cet article présente le périmètre prévu de la refonte ; il ne décrit pas un état de validation ou une version publiée. La page [PRISM V2](https://donovan-dev-web.github.io/LIVEX/technique/prism/) résume la nouvelle répartition des rôles.

La série poursuit ce parcours avec le modèle voxel, la perception locale, les chaînes de fabrication, l'agriculture et les chantiers.
