---
title: "PRISM : le projet Unreal final et le plugin PRISM-LDK"
description: "PRISM est le projet Unreal final de LIVEX, intégrant le plugin PRISM-LDK. SYNE reste seul décisionnel ; PRISM assure le rendu perceptif."
date: 2026-10-06
tags: ["prism", "unreal", "prism-ldk", "rendu", "immersion"]
draft: false
---

**PRISM** (*Perceptual Rendering & Interactive Simulation Module*) est le projet Unreal final de LIVEX. Il intègre le **plugin PRISM-LDK** (*LIVEX Development Kit*). Son rôle : le **rendu perceptif** et l'**immersion**, sans porter la logique décisionnelle.

## Choix d'architecture

L'ADR-002 acte ce choix : **PRISM est le projet Unreal final de LIVEX** et intègre le plugin Unreal **PRISM-LDK**. Cette décision supersède l'ADR-001 (prototype Godot), conservé à titre historique. Le principe est catégorique :

> **SYNE reste le seul moteur décisionnel et l'unique autorité de l'état simulé.**

PRISM est un **hôte de rendu perceptif**, pas un second moteur.

## Séparation des responsabilités

| Composant | Responsabilité |
| :-- | :-- |
| **SYNE** | Vérité de l'état, boucle de simulation, décisions des agents, déterminisme. |
| **ECHOS** | Observation, analyse, métriques, calibration, comparaison de runs. |
| **PRISM** | Représentation visuelle/perceptive, immersion, interaction utilisateur orientée rendu. Aucun calcul décisionnel. |

Cette séparation garantit que l'observation/visualisation n'influence jamais le cours de la simulation.

## Le plugin PRISM-LDK

Le **PRISM-LDK** est le module technique qui formalise l'interface entre PRISM et l'écosystème LIVEX. Il définit :

- **Description `world_initialized` (v1.1)** : paramètres du monde fournis par SYNE, incluant `simulatedSecondsPerTick`, `metersPerUnit` (informatif), dimensions, cellule, TPS, etc.
- **Flux d'observabilité** : consommation des snapshots et événements SYNE (ordre strict « snapshot puis événements » par tick), alignement sur le tick courant.
- **Cycle de contrôle** : respect des phases `prepare/ready/start/pause/resume/stop/reset` selon les contrats.
- **Supervision** : sondes health/readiness (`health.path`, `intervalMs`, `startupMs`) et gestion des états (Démarrage → Prêt → En cours).
- **Transport** : s'aligne sur `TRANSPORT_API.md` et `INTEGRATION_CONTRACT.md` pour l'interopérabilité avec SYNE/ECHOS/Launcher.

## Échelle temporelle et représentation spatiale

PRISM consomme `simulatedSecondsPerTick` exposé dans `world_initialized`. Cette valeur (ex. 60 s/tick pour `reference`, 5 s/tick pour `prism`) définit l'**échelle temporelle simulée** sans altérer l'ordre causal ni les effets par action côté SYNE. `metersPerUnit` est informatif et permet d'harmoniser l'échelle spatiale entre simulation et rendu perceptif.

Le profil `prism` est conçu pour un rendu perceptif (monde 2240×2240, 6 TPS cibles, 5 s/tick) tout en restant cohérent avec les contrats SYNE.

## Intégration avec le Launcher

Le Launcher évalue le **déverrouillage Immersion** en lisant les exigences PRISM (mode porteur, `snapshotStream`, `renderCadence`, contrôle). Il sélectionne le **profil** (dont `prism`) dynamiquement, démarre les composants dans l'ordre de résolution et les arrête dans l'ordre inverse. PRISM est traité comme un composant supervisable (cycle de vie, sondes, gestion d'arrêts/échecs) via ses manifestes.

## Point de vue sur l'immersion

PRISM n'a pas vocation à inventer de la logique. Son rôle est de **rendre perceptible** ce qui émerge de SYNE :

- **Montrer, pas décider** : l'état visible reflète fidèlement les snapshots SYNE.
- **Respect du temps simulé** : la cadence de rendu s'aligne sur le flux (sans extrapolation qui romprait la traçabilité).
- **Traçabilité préservée** : tout élément visible remonte à un tick/snapshot SYNE identifiable.

## Distinction par rapport au prototype

L'ADR-002 acte formellement le passage à **Unreal + PRISM-LDK** comme socle final. Les références historiques à Godot (ADR-001) décrivent un prototype, non l'implémentation actuelle. Cette clarification est reflétée dans la documentation (VISION, ARCHITECTURE, PRISM).

## En résumé

PRISM est le **canal perceptif** de LIVEX. En intégrant le plugin PRISM-LDK dans un projet Unreal final, il préserve la règle d'or : **SYNE détient la vérité, ECHOS l'analyse, PRISM la perception**. Cette séparation est ce qui permet d'observer une émergence honnête, traçable et reproductible.
