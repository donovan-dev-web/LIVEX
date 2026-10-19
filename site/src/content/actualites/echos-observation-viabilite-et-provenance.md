---
title: "ECHOS : observation, viabilité et provenance"
description: "ECHOS observe le flux SYNE sans jamais le modifier : métriques de viabilité, provenance mesuré / absent / zéro observé et limites clairement assumées."
date: 2026-10-18
tags: ["echos", "observation", "viabilite", "provenance"]
draft: false
---

Un monde simulé se porte bien. La population tient, l'énergie reste stable, les ressources se renouvellent. Vous ouvrez le rapport, et une ligne affiche zéro. Est-ce que le monde est vide, ou est-ce que l'instrument a cessé de regarder à cet instant précis ? La question paraît anodine. Elle décide pourtant de la validité de toute conclusion qu'on en tirera.

**ECHOS**, l'observateur de LIVEX, a été construit autour de cette question. Sa version 0.1.0 ne promet pas seulement de mesurer : elle promet de dire, pour chaque valeur, si elle a été **mesurée**, si elle est **absente**, ou si elle est un **zéro observé**.

## Un observateur qui n'entre jamais dans la pièce

La première règle d'ECHOS tient en un mot : lecture seule. Il ingère le flux de SYNE — par WebSocket en direct, ou depuis un fichier batch —, il stocke les données, il calcule des métriques. Il n'écrit jamais dans le monde simulé. Aucune commande ne part de l'observateur vers la simulation.

Cette séparation n'est pas une coquetterie d'architecture. Si la mesure vivait dans le moteur, elle pourrait l'influencer : un compteur qui consomme du hasard, un calcul qui retarde un tick, une agrégation qui lit un état à moitié mis à jour. En plaçant l'observation en aval, LIVEX garantit que ce qu'il mesure n'a pas été modifié par le fait de le mesurer.

ECHOS se présente comme une API *headless* : un service REST sur le port 5000, consommé par le Launcher et par les outils d'analyse, adossé à une base SQLite versionnée.

## Ce que veut dire « viable »

La viabilité n'est pas une impression, c'est un ensemble de séries calculées par run et par tick. ECHOS suit l'énergie, la faim et la soif sous forme de moyennes et de distributions sur la population. Il détermine si le monde a survécu, avec un état explicite — `extinct` ou `surviving` — et, le cas échéant, le tick exact de l'extinction. Il observe les dynamiques de groupe : formation, taille, distances des liens. Il suit la propagation de l'information : messages émis, messages reçus, taux de réussite.

Ce vocabulaire n'est pas décoratif. Un monde sans agents vivants est `extinct`, pas simplement « faible ». Une population qui décline lentement n'est pas la même chose qu'une population morte, et le rapport doit permettre de les distinguer.

## Mesuré, absent, zéro observé

La provenance est le principe central d'ECHOS, et il mérite d'être posé en clair.

| État | Signification | Exemple |
| :-- | :-- | :-- |
| **mesuré** | valeur calculée à partir de données présentes | distance moyenne des liens = 4,2 |
| **absent** | la donnée n'existe pas dans ce contexte | pas de paires liées → `null` |
| **zéro observé** | la valeur a été mesurée et vaut zéro | distance = 0 (agents colocalisés) |

Confondre absence et zéro, c'est exactement l'erreur qui a failli fausser la campagne A0-4, où un recensement légitimement nul avait été lu comme une mesure manquante. ECHOS rend cette confusion difficile par construction : la provenance accompagne chaque série.

## Où vivent les faits

Derrière l'API, une base SQLite au schéma **v6**, migrée de façon additive pour préserver la compatibilité avec les bases existantes. Une campagne ne doit pas devenir illisible parce que l'outil a changé de version entre deux relevés. Les contextes (`agents`, `groups`, `phenomena`) sont persistés selon leur cadence, le dernier tick étant systématiquement conservé — de sorte que la fin d'un run ne soit jamais perdue. La migration BL-09 rend `utility` nullable et ajoute `held_utility`, `decision_state` et `decision_tick`, pour ne plus confondre absence d'utilité et score nul. Le compteur global `SCHEMA_VERSION` reste à `"6"`, une réserve documentée dans `LIVEX-STATUS.md`.

## L'API et le rapport de calibration

L'endpoint `/api/runs/{id}/viability` expose des séries et des diagnostics observés, y compris le **rapport de calibration** produit pour chaque run. Ce rapport est *observe-only* : il décrit ce qui a eu lieu, il ne corrige jamais une trajectoire jugée décevante. Il porte les critères qui avaient été fixés avant l'exécution, et il documente assez précisément pour qu'un autre puisse le contester.

Un point mérite d'être dit clairement, car il relève de l'honnêteté : l'endpoint expose des séries observées, **pas** l'indice DVI à sept composantes décrit comme projet dans la monographie. Cet indice n'est pas implémenté. Le nommer « score d'émergence » serait une promesse que le code ne tient pas.

## Les preuves, et ce qu'elles valent

Au relevé du 19 octobre 2026, la suite ECHOS affiche **383 tests réussis, 0 échec**, avec une couverture mesurée de 91,32 %, au-dessus du seuil configuré de 80 %. Ces chiffres ne disent pas que les conclusions scientifiques sont vraies ; ils disent que l'instrument fait ce qu'il annonce.

## Les limites, assumées

ECHOS ne certifie pas une émergence générale. Il fournit les données pour en débattre, et il connaît ses bornes. L'indice DVI à sept composantes reste hors périmètre. Les métriques sont des observations **bornées** par la configuration, la graine et l'horizon du run : elles décrivent un monde précis, pas tous les mondes possibles. Et l'observateur ne comble jamais un trou : s'il ne voit pas un tick, il le signale, il ne l'invente pas.

## L'observateur honnête

La valeur d'ECHOS n'est pas dans l'abondance de ses chiffres, mais dans la discipline qui les accompagne. Chaque mesure porte son statut de provenance ; aucune absence ne se déguise en zéro ; aucun trou n'est comblé par une approximation rassurante. C'est cette exigence qui permet à LIVEX d'affirmer un phénomène sur des bases traçables — et, tout aussi important, d'accepter qu'un autre ne soit pas confirmé.

L'architecture, les contrats et les routes de l'observatoire sont détaillés dans le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/). Les rapports de campagne paraissent dans ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/), également disponible en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml).
