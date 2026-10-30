---
title: "Le monde logique et sa représentation procédurale"
description: "Comment PRISM-LDK projette le monde logique de SYNE en une représentation procédurale déterministe, sans jamais réinjecter le rendu dans la simulation."
date: 2026-10-29
tags: ["prism-ldk", "prism", "procedural", "monde-logique", "rendu"]
draft: false
---

Il y a deux mondes dans LIVEX. Le premier est logique : une grille, des cellules, des obstacles, des ressources, des agents. C'est le monde de SYNE, l'autorité. Le second est perceptif : un relief, des lacs, une mer, des acteurs animés. C'est le monde de PRISM. Entre les deux, PRISM-LDK tient la règle : le second ne doit jamais réécrire le premier.

Cette règle paraît simple. Elle est en réalité la plus difficile à tenir, parce que le rendu a toujours envie d'aider.

## Le monde logique, tel quel

Le monde logique arrive sous forme d'une description : `world_initialized`. Elle contient les dimensions, la taille de cellule, les TPS, les obstacles (position et rayon), les ressources (eau, nourriture, bois, minéral), les agents et les régions.

PRISM-LDK ne modifie rien de cette description. Il l'expose telle quelle à Blueprint, convertie en types utilisables. Les coordonnées logiques restent l'état autoritaire. L'identifiant de chaque agent reste son identifiant SYNE. Rien n'est arrondi, rien n'est lissé, rien n'est inventé.

## La projection, une traduction et non une décision

Projeter, c'est traduire. La position continue devient une cellule. Le rayon SYNE devient une étendue d'influence dans le monde Unreal. La ressource d'eau devient un lac à la cellule projetée, jamais déplacé.

Cette traduction obéit à un contrat strict : la grille logique SYNE occupe exactement le rectangle central, et elle reste entièrement terre ferme. La falaise, la plage et la mer sont une décoration hors grille, dans une bordure. Le maillage affiché vaut grille plus deux fois la bordure. Ainsi, la logique et le visuel ne se confondent jamais : le monde simulé reste dans son rectangle, le rendu l'entoure.

## Le relief, déterministe et seedé

Le relief est généré de manière déterministe, seedé par la graine SYNE. Le bruit est le Perlin du moteur Unreal : aucune table externe, donc aucune dépendance à une version d'engine, et un résultat identique sur tout build. Les hauteurs suivent la taille de tuile (`TileSizeUu`) ; les fréquences de bruit font l'inverse. Un gros obstacle devient une montagne, un petit un amer.

Les lacs sont placés à la cellule projetée de la ressource d'eau. Leur niveau d'eau est relatif au relief local : la cuvette descend sous la surface locale, et la nappe est rognée au bord de la grille. La mer est un disque irrégulier débordant du maillage, en anneau hors grille.

Tout cela est calculé une fois, de manière synchrone ou progressive selon les besoins, et jamais réinjecté dans SYNE.

## La règle qui ferme la boucle

La position projetée sur le NavMesh, avec son altitude, est une projection visuelle Unreal, non la position simulée. L'identifiant et les coordonnées logiques restent l'état autoritaire. On ne renvoie jamais au moteur une position ou une altitude inventées par le rendu.

Si la scène renvoyait au moteur la position qu'elle a projetée sur le terrain, le monde se mettrait à dépendre du rendu, et l'affichage deviendrait une cause. La règle interdit cette boucle : le monde produit les positions, la scène les projette, et rien ne remonte. C'est ce qui garantit qu'une scène différente (un autre terrain, un autre moteur de rendu) produirait le même monde.

## Le snapshot, source de vérité

À chaque instantané, la scène met à jour les acteurs à partir de l'état reçu. Les deltas et les événements servent aux notifications et aux effets ponctuels, mais leurs mutations peuvent aussi figurer dans l'instantané. Appliquer deux fois la même mutation produirait un double effet. La règle est donc nette : l'instantané est la source de vérité de l'état.

Un cas instructif : un tableau d'obstacles vide dans le message d'initialisation signifie souvent que le moteur a préparé un monde sans layout, car les obstacles sont désactivés par défaut. Ce n'est pas un défaut du rendu, c'est une conséquence du contrat. Lorsqu'une scène paraît vide, la cause est souvent en amont.

## Montrer fidèlement, voilà la valeur

PRISM-LDK V0.1.0 ne cherche pas à être un second moteur, et il ne le sera pas. Sa contribution est ailleurs : montrer fidèlement un monde qu'il n'a pas calculé, projeter sans trahir, et ne jamais laisser le rendu s'inviter dans la décision. Un champ inconnu arrive ? L'image continue. Un tick manque ? La scène ne l'invente pas.

Le détail de l'intégration et des contrats vit dans le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les avancées du projet paraissent dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), disponible aussi en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml).
