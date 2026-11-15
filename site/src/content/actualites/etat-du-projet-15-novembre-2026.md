---
title: "2 164 tests, deux portes : l'état du projet au 15 novembre 2026"
description: "Relevé daté des suites LIVEX : 2 164 tests réussis, G5 franchie, P11 livrée, G1 nommée. Ce qui a changé depuis octobre, et ce qui reste ouvert avant la V2."
seoTitle: "État du projet au 15 novembre 2026"
date: 2026-11-15
tags: ["livex", "statut", "tests", "g5", "g1", "prism"]
draft: false
---

Un relevé n'est pas un bilan. C'est une photographie datée, avec ses instruments et ses angles morts. Celui du 15 novembre 2026 tient en une phrase : le socle V2 est en service, deux portes ont été franchies, une troisième est nommée sans être franchie, et aucune release V2 n'est publiée.

## Les chiffres, composant par composant

Les suites exécutées sur l'arbre de travail, en Release sous Windows x64, totalisent **2 164 tests réussis, 16 ignorés, 0 échec**, sur **2 184 cas collectés**. Le détail compte autant que le total : PRISM V2 525, SYNE 813, ECHOS 419 (14 ignorés), Launcher 236, Installateur 120, syne-mock 51 (2 ignorés).

Quatre cas du Launcher s'ajoutent au décompte sans être des régressions : ils exigent `LIVEX_SYNE_PUBLISHED_ROOT`, s'auto-ignorent sans publication de SYNE, et VSTest les rapporte en `$XunitDynamicSkip$` plutôt qu'en ignorés. Les compter comme échecs serait faux ; les passer sous silence le serait aussi.

Deux réserves accompagnent ces chiffres. La couverture ECHOS est de **91,14 %** au 15 novembre ; la couverture SYNE affichée sur le site reste celle du **19 octobre** (81,20 %), faute d'un nouveau relevé. Et le total mélange des suites de natures différentes : tests unitaires, intégration, bout en bout, contrats. Un total ne dit rien de la qualité d'un test.

## G5 : la porte de la cognition

Le 14 novembre 2026, R3 a été produite à pleine échelle : 100 agents × 5 000 ticks, **25 chaînes terminées par passe, 0 abandon**, empreinte `855fdc2e1387e8d3` reproduite deux fois. Le coût propre à SYNE tombe à **30,5 ms/tick**, sous le budget de 50 ms. G5 est franchie.

Ce résultat ne dit pas que le monde est intéressant ; il dit que la cognition tient la cadence et que la trajectoire se rejoue. C'est une condition de mesure, pas une conclusion scientifique.

## G1 : le coût de l'hôte, nommé

Le tick couplé complet, lui, coûte **431,5 ms** à 100 agents (relevé B9 du 15 novembre, deux passes). **ApplyWrites pèse 404,7 ms, soit 94 %** du tick ; la perception (15,2 ms) et les macros (14,4 ms) sont marginales. À 1 000 agents, la dernière mesure valide reste 850 ms.

Le budget est de 50 ms à 100 agents : l'écart est de l'ordre de 8,6×. G1 reste donc **ouverte**, mais non bloquante pour l'implémentation. Le levier identifié n'est pas la parallélisation de la perception : c'est la suppression des recherches de chemin rejouées (PRISM-143), qui échouent puis recommencent.

## P11 et ce qui reste

P11 (viewer) est livrée le 15 novembre : rendu, inspecteur, sept calques, modes client/embarqué/rejeu, plan de contrôle relayé à l'hôte, session gatée par le Launcher. Il manque deux preuves pour la clore : le relevé B8 et la validation graphique Linux.

Avant la V2, le même relevé nomme les autres chantiers : une empreinte de monde `WorldGenVersion 2` et un rapport G2 daté ; une session réelle consignée et la recette INSTALLER-200 sur machine propre pour G6. Le calibrage et les ablations (G7) restent volontairement reportés : ils ne sont pas des préalables au reste du développement.

La règle de lecture ne change pas. Un chiffre sans date, sans commande et sans source n'est pas publié ; une porte franchie n'est pas une promesse ; une porte ouverte est écrite noir sur blanc.
