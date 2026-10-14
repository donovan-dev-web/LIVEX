---
title: "Architecture : trois modules complémentaires (SYNE, ECHOS, PRISM)"
description: "LIVEX s'articule autour de SYNE (moteur de simulation), ECHOS (observation et analyse) et PRISM (rendu perceptif Unreal + plugin LDK)."
date: 2026-10-02
tags: ["architecture", "syne", "echos", "prism"]
draft: false
---

LIVEX s'articule autour de trois modules complémentaires, séparant clairement la **vérité du monde**, l'**observation** et le **rendu perceptif**.

## Les trois piliers

| Module | Nom complet | Rôle |
| :-- | :-- | :-- |
| **SYNE** | *Systems & Emergent Network Engine* | Moteur de simulation multi-agents. Il détient la vérité de l'état du monde, exécute la boucle de simulation et garantit le déterminisme. Indépendant du rendu. |
| **ECHOS** | *Emergent Cognition & Holistic Observation System* | Système d'observation, d'analyse, de calibrage et de pilotage. Il consomme le flux de SYNE, calcule des métriques, détecte des phénomènes émergents, produit des rapports de calibration et permet la comparaison entre runs. |
| **PRISM** | *Perceptual Rendering & Interactive Simulation Module* | Projet Unreal final de LIVEX, intégrant le **plugin PRISM-LDK** (*LIVEX Development Kit*). Il assure le rendu perceptif et l'immersion sans porter la logique décisionnelle. |

## Un découplage volontaire

Ce découplage répond à trois impératifs : **reproductibilité**, **traçabilité** et **portabilité**.

- **SYNE seul fait autorité** : ni ECHOS ni PRISM ne modifient l'état décisionnel. SYNE est l'unique source de vérité.
- **Observation non-intrusive** : ECHOS s'abonne au flux d'observabilité de SYNE (snapshots, événements, ticks) sans altérer la simulation. L'ordre de diffusion et la sémantique des trames sont définis par contrat.
- **Rendu découplé** : PRISM ne porte pas la cognition. Il consomme l'état et les événements de SYNE pour produire une représentation perceptive, tout en restant pilotable via l'orchestration.

## SYNE, cœur décisionnel

SYNE implémente un monde à temps discret (ticks), des agents autonomes dotés de besoins, de mémoire, de croyances, de perception, d'une formule d'utilité et de capacités d'action. La boucle est **déterministe** : à graine identique, même configuration partielle appliquée de manière cohérente, l'exécution produit un état aligné sur ses oracles. La persistance (SQLite) permet la reprise exacte, et le serveur de contrôle HTTP permet le pilotage programmatique.

## ECHOS, observateur rigoureux

ECHOS transforme l'observabilité brute en **connaissance traçable**. Il ingère le flux SYNE, persiste un journal d'événements et des instantanés, calcule des métriques normalisées, construit des séries temporelles, détecte des discontinuités de ticks, consigne la **provenance** des valeurs et produit des rapports de calibration (`schemaVersion 2`) incluant `outcome`, `extinctionTick` et un bloc `viability`. Il permet aussi des comparaisons entre runs (`/api/compare`) avec distinction claire entre verdict fondé sur l'empreinte bit-à-bit et résumé allégé.

## PRISM et le plugin PRISM-LDK

PRISM est le projet Unreal final de LIVEX. Il intègre le **PRISM-LDK**, module technique qui formalise l'interface avec SYNE/ECHOS : description `world_initialized` (incluant `simulatedSecondsPerTick`, `metersPerUnit`), flux de snapshots, événements, cycle de contrôle (`prepare/ready/start/pause/resume/stop/reset`) et supervision. L'ADR-002 acte ce choix (supersédant l'ADR-001 historique Godot) : SYNE reste seul décisionnel ; PRISM est un hôte de rendu perceptif.

## Cohérence par contrats

L'ensemble est tenu par des **contrats vivants** (`API_CONTRACTS.md`, `INTEGRATION_CONTRACT.md`, `TRANSPORT_API.md`). L'ordre « snapshot puis événements » sur le flux d'observabilité est strict. Le `run_id` canonique (`run-<seed>-<12hex>`) garantit la traçabilité entre runs. Ces règles assurent que l'observation n'influence jamais le cours de la simulation.

L'objectif est simple : **séparer ce qui fait tourner le monde (SYNE), de ce qui l'observe (ECHOS), de ce qui le montre (PRISM)**. Cette séparation est la condition même de la reproductibilité et de la traçabilité exigées par la question directrice.
