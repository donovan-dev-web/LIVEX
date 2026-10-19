---
title: "Architecture : trois modules complémentaires (SYNE, ECHOS, PRISM)"
description: "SYNE, ECHOS, PRISM : comment LIVEX sépare la vérité du monde, son observation et son rendu perceptif pour garantir reproductibilité et traçabilité."
date: 2026-10-02
tags: ["architecture", "syne", "echos", "prism"]
draft: false
---

Un agent a soif. Il consulte sa mémoire, repère un point d'eau qu'il connaît, pèse l'énergie du trajet contre le risque d'arriver trop tard, puis se met en marche. À l'instant précis où il bouge, trois questions se posent en même temps : qui a décidé de ce pas ? qui l'observe ? qui le donne à voir ?

Une architecture improvisée répondrait « un peu tout le monde ». LIVEX répond par une séparation stricte : **un seul module détient la vérité du monde, un autre l'observe, un troisième la rend perceptible.** Ni l'observateur ni le rendu ne peuvent modifier la plus petite décision d'un agent. Ce principe a été posé avant la première fonctionnalité, et il commande encore aujourd'hui chaque choix technique du projet.

## Trois rôles, une seule vérité

Le dispositif s'articule autour de trois modules complémentaires, dont le partage des responsabilités est volontairement net.

| Module | Nom complet | Rôle |
| :-- | :-- | :-- |
| **SYNE** | *Systems & Emergent Network Engine* | Moteur de simulation multi-agents. Il détient la vérité de l'état du monde, exécute la boucle de simulation et garantit le déterminisme. Indépendant du rendu. |
| **ECHOS** | *Emergent Cognition & Holistic Observation System* | Système d'observation, d'analyse, de calibrage et de pilotage. Il consomme le flux de SYNE, calcule des métriques, détecte des phénomènes émergents, produit des rapports de calibration et permet la comparaison entre runs. |
| **PRISM** | *Perceptual Rendering & Interactive Simulation Module* | Projet Unreal final de LIVEX, intégrant le **plugin PRISM-LDK** (*LIVEX Development Kit*). Il assure le rendu perceptif et l'immersion sans porter la logique décisionnelle. |

Ce découpage ressemble à une séparation de confort. C'est en réalité une contrainte scientifique. Un monde dont on veut prouver quelque chose doit pouvoir être rejoué à l'identique, et pour cela il faut savoir, à tout instant, qui fait autorité. Dès que deux composants peuvent écrire dans l'état du monde, la chaîne de causalité se brouille : impossible de dire si le phénomène observé vient des lois de la simulation ou d'une interférence de l'observation.

## Pourquoi découpler, et jusqu'où

Le découplage répond à trois impératifs qui se tiennent : **reproductibilité**, **traçabilité** et **portabilité**. Ils imposent une règle simple, presque brutale.

**SYNE seul fait autorité.** Ni ECHOS ni PRISM ne modifient l'état décisionnel ; SYNE est l'unique source de vérité. C'est ce qui permet d'affirmer qu'un run est déterministe — à graine et configuration données, il se répète — sans avoir à prouver que trois composants l'ont laissé tranquille.

**L'observation est non-intrusive.** ECHOS s'abonne au flux d'observabilité de SYNE — snapshots, événements, ticks — sans altérer la simulation. L'ordre de diffusion et la sémantique des trames sont fixés par contrat, précisément pour que l'observateur ne puisse pas introduire de rétroaction invisible.

**Le rendu est indépendant du cœur.** PRISM ne porte pas la cognition : il consomme l'état et les événements de SYNE pour produire une représentation perceptive, tout en restant pilotable par l'orchestration.

## SYNE, le cœur décisionnel

SYNE implémente un monde à **temps discret**, avancé de tick en tick. Les agents autonomes y disposent de besoins, d'une mémoire, de croyances, d'une perception partielle, d'une formule d'utilité et de capacités d'action. Rien de spectaculaire dans cette liste prise isolément ; c'est leur combinaison, tick après tick, qui produit des trajectoires qu'aucune ligne n'a écrites à l'avance.

La boucle est **déterministe** : à graine identique et configuration cohérente, l'exécution retrouve un état aligné sur ses oracles. La persistance, assurée par SQLite, permet de reprendre un run à l'endroit exact où il s'était arrêté, et le serveur de contrôle HTTP autorise un pilotage programmatique complet. Un monde peut donc être lancé, mis en pause, déplacé et repris sans que sa trajectoire change — c'est la condition d'une expérience rejouable.

## ECHOS, l'observateur qui ne touche à rien

De l'autre côté du flux, ECHOS transforme l'observabilité brute en **connaissance traçable**. Il ingère ce que SYNE publie, persiste un journal d'événements et des instantanés, calcule des métriques normalisées et en construit des séries temporelles. Il détecte aussi les discontinuités de ticks — les trous — et les consigne au lieu de les lisser. Chaque valeur porte sa **provenance**.

Ses rapports de calibration sont versionnés (`schemaVersion 2`) et incluent `outcome`, `extinctionTick` et un bloc `viability`. La comparaison entre runs (`/api/compare`) distingue clairement un verdict fondé sur l'**empreinte bit-à-bit** d'un simple résumé allégé : on ne confond jamais une égalité démontrée avec une ressemblance. Ce que produit ECHOS reste en lecture seule — l'observatoire écrit ses conclusions, il ne réécrit jamais le monde.

## PRISM et le plugin PRISM-LDK

PRISM est le projet Unreal final de LIVEX. Il intègre le **PRISM-LDK**, un module technique qui formalise l'interface avec SYNE et ECHOS : la description `world_initialized` (qui inclut `simulatedSecondsPerTick` et `metersPerUnit`), le flux de snapshots, les événements, le cycle de contrôle — `prepare`, `ready`, `start`, `pause`, `resume`, `stop`, `reset` — et la supervision.

L'**ADR-002** acte ce choix et supersède l'ADR-001 historique, qui décrivait un prototype Godot : SYNE reste seul décisionnel, et PRISM n'est qu'un hôte de rendu perceptif. Le projet a préféré assumer ce basculement par une décision écrite plutôt que par une dérive silencieuse.

## Des contrats, pas des promesses

Rien de tout cela ne tiendrait sur la seule bonne volonté des développeurs. L'ensemble est verrouillé par des **contrats vivants** — `API_CONTRACTS.md`, `INTEGRATION_CONTRACT.md`, `TRANSPORT_API.md` — dont les règles sont peu nombreuses mais non négociables. L'ordre « **snapshot puis événements** » sur le flux d'observabilité est strict, tick après tick. Le `run_id` canonique, au format `run-<seed>-<12hex>`, garantit la traçabilité entre exécutions, paquets et rapports. Et parce que ces règles sont testées, l'observation ne peut pas, même accidentellement, influencer le cours de la simulation.

## Séparer pour pouvoir prouver

L'objectif tient en une phrase : **séparer ce qui fait tourner le monde (SYNE), de ce qui l'observe (ECHOS), de ce qui le montre (PRISM).** Cette séparation n'est pas un raffinement d'architecte. C'est la condition même de la reproductibilité et de la traçabilité qu'exige la question directrice de LIVEX. Sans elle, chaque observation serait une anecdote ; avec elle, chaque observation devient une affirmation que l'on peut rejouer, vérifier et discuter.

Pour aller plus loin, le [portail de documentation](https://donovan-dev-web.github.io/LIVEX/documentation/) détaille l'architecture et les contrats, et ce [journal](https://donovan-dev-web.github.io/LIVEX/actualites/) — également disponible en [flux RSS](https://donovan-dev-web.github.io/LIVEX/actualites/feed.xml) — suit l'évolution des composants au fil des relevés.
